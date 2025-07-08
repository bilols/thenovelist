using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates the character roster and advances the outline.
    /// </summary>
    public sealed class CharactersOutlinerService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAuthorPreset;

        public CharactersOutlinerService(ILlmClient llm, bool includeAuthorPreset = false)
        {
            _llm                 = llm;
            _includeAuthorPreset = includeAuthorPreset;
        }

        public async Task DefineCharactersAsync(string outlinePath,
                                                string modelId,
                                                CancellationToken ct = default)
        {
            var outlineJson = JObject.Parse(await File.ReadAllTextAsync(outlinePath, ct));

            if (!Enum.TryParse(outlineJson["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                phase != OutlineProgress.ArcDefined)
                throw new InvalidOperationException("Outline is not in the ArcDefined phase.");

            // ----------------------------------------------------------------
            // Resolve originating project file (fixes extra '..' bug)
            // ----------------------------------------------------------------
            var rel = outlineJson["header"]?["projectFile"]?.ToString()
                      ?? throw new InvalidDataException("header.projectFile missing.");

            var projectPath = Path.GetFullPath(
                                  Path.Combine(Path.GetDirectoryName(outlinePath)!, rel));

            if (!File.Exists(projectPath))
                throw new FileNotFoundException("Cannot locate project file.", projectPath);

            var project = JObject.Parse(File.ReadAllText(projectPath));

            // ----------------------------------------------------------------
            // Read counts (fail fast)
            // ----------------------------------------------------------------
            int supporting = project["supportingCharacters"]?.Type == JTokenType.Integer
                             ? project["supportingCharacters"]!.Value<int>()
                             : throw new InvalidDataException("'supportingCharacters' missing or not integer.");

            int minor      = project["minorCharacters"]?.Type == JTokenType.Integer
                             ? project["minorCharacters"]!.Value<int>()
                             : throw new InvalidDataException("'minorCharacters' missing or not integer.");

            int total = 1 + supporting + minor; // protagonist + supporting + minor

            // ----------------------------------------------------------------
            // Build strict prompt
            // ----------------------------------------------------------------
            var audience = (project["targetAudience"] as JArray)?
                               .Select(t => t.ToString())
                               .Where(s => !string.IsNullOrWhiteSpace(s))
                               .ToArray() ?? Array.Empty<string>();

            var famousPreset = _includeAuthorPreset
                 ? project["famousAuthorPreset"]?.ToString()
                 : null;

            var prompt = BuildPrompt(outlineJson, total, supporting, minor,
                                     audience, famousPreset);

            // ----------------------------------------------------------------
            // Call LLM with retry until correct JSON length
            // ----------------------------------------------------------------
            var maxRetries = RetryPolicy.GetMaxRetries(modelId);
            JArray? roster = null; Exception? last = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var reply = await _llm.CompleteAsync(prompt, modelId, ct);
                    roster    = JArray.Parse(Sanitize(reply));

                    if (roster.Count == total) break;   // success
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    last = ex;
                }

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }

            if (roster is null || roster.Count != total)
                throw new InvalidOperationException("LLM failed to return correct character roster.", last);

            outlineJson["characters"]      = roster;
            outlineJson["outlineProgress"] = OutlineProgress.CharactersOutlined.ToString();
            outlineJson["header"]!["schemaVersion"] =
                outlineJson["header"]!["schemaVersion"]!.Value<int>() + 1;

            await File.WriteAllTextAsync(outlinePath, outlineJson.ToString(), ct);
        }

        // --------------------------------------------------------------------
        private static string Sanitize(string raw)
        {
            var t = raw.Trim();
            if (t.StartsWith("```"))
            {
                var firstNl  = t.IndexOf('\n');
                var lastFence= t.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNl >= 0 && lastFence > firstNl)
                    return t.Substring(firstNl + 1, lastFence - firstNl - 1).Trim();
            }
            return t;
        }

        private static string BuildPrompt(JObject outline,
                                          int total, int supporting, int minor,
                                          string[] audience,
                                          string? authorPreset)
        {
            var premise = outline["premise"]!.ToString();
            var audienceLine = audience.Length > 0
                ? $"Target audience: {string.Join(", ", audience)}."
                : string.Empty;

            var styleLine = !string.IsNullOrWhiteSpace(authorPreset)
                ? $"Emulate the tonal style of {authorPreset.Replace(".json", "")}."
                : string.Empty;

            return
$@"You are an elite character-development assistant.

Create exactly {total} characters for the premise below:

  • 1 protagonist – MUST be the first object
  • {supporting} supporting characters
  • {minor} minor characters

Each object MUST contain:
  ""name""  : full name
  ""role""  : starts with one of exactly
             ""Protagonist: "", ""Supporting character: "", ""Minor character: ""
  ""traits"": 1–5 short descriptors
  ""arc""   : ≤100-word personal journey

Return ONLY the raw JSON array. Do NOT wrap in Markdown.  
If you cannot comply, reply only with the word: RETRY.

{audienceLine}
{styleLine}

PREMISE:
{premise}";
        }
    }
}
