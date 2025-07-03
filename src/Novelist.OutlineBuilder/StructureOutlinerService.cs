using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates chapter list (plus optional prologue/epilogue) and
    /// advances the outline to StructureOutlined.
    /// </summary>
    public sealed class StructureOutlinerService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAudience;

        public StructureOutlinerService(ILlmClient llm, bool includeAudience = true)
        {
            _llm             = llm;
            _includeAudience = includeAudience;
        }

        public async Task DefineStructureAsync(string outlinePath,
                                               string modelId,
                                               CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                phase != OutlineProgress.CharactersOutlined)
                throw new InvalidOperationException("Outline is not in the CharactersOutlined phase.");

            // ------------------------------------------------ project settings
            var relProj = outline["header"]?["projectFile"]?.ToString()
                          ?? throw new InvalidDataException("header.projectFile missing.");

            var projectPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(outlinePath)!, relProj));
            var project     = JObject.Parse(File.ReadAllText(projectPath));

            bool wantPrologue  = project["includePrologue"]?.Value<bool>()  ?? false;
            bool wantEpilogue  = project["includeEpilogue"]?.Value<bool>()  ?? false;

            int  coreChapters  = outline["chapterCount"]!.Value<int>();
            int  totalExpected = coreChapters + (wantPrologue ? 1 : 0) + (wantEpilogue ? 1 : 0);
            int  wordsPerChap  = outline["totalWordCount"]!.Value<int>() / coreChapters;

            // ------------------------------------------------ prompt pieces
            var premise  = outline["premise"]!.ToString();
            var arc      = outline["storyArc"]?.ToString() ?? "";
            var roster   = outline["characters"]?.ToString() ?? "[]";
            var audArr   = outline["targetAudience"] as JArray;

            string audienceLine = _includeAudience && audArr is { Count: > 0 }
                ? $"Target audience: {string.Join(", ", audArr)}."
                : string.Empty;

            string prologueLine = wantPrologue
                ? "Include a Prologue object first (number = 0)."
                : "";

            string epilogueLine = wantEpilogue
                ? "Include an Epilogue object last (number = N+1)."
                : "";

            string prompt =
$@"You are a seasoned story‑structure editor.

Return ONLY a JSON array with {totalExpected} objects:

  {prologueLine}
  • {coreChapters} numbered chapters (1‑based)
  {epilogueLine}

Each object must have:
  ""number""  : integer
  ""summary"" : ≤80 words
  ""beats""   : 3‑6 lines
  ""themes""  : optional 1‑3 tags

{audienceLine}

If you cannot comply, reply exactly: RETRY

MATERIAL
Premise:
{premise}

Arc:
{arc}

Characters:
{roster}

Average words per chapter: ~{wordsPerChap}";

            // ------------------------------------------------ retry loop
            int     max   = RetryPolicy.GetMaxRetries(modelId);
            JArray? arr   = null;  Exception? last = null;

            for (int attempt = 1; attempt <= max; attempt++)
            {
                try
                {
                    string raw = await _llm.CompleteAsync(prompt, modelId, ct);
                    string txt = Sanitize(raw);

                    if (txt.Equals("RETRY", StringComparison.OrdinalIgnoreCase) ||
                        !txt.TrimStart().StartsWith("["))
                        throw new JsonReaderException("Model did not return JSON array.");

                    arr = JArray.Parse(txt);
                    if (arr.Count == totalExpected) break;
                    throw new JsonReaderException("Incorrect item count.");
                }
                catch (Exception ex) when (attempt < max)
                {
                    last = ex;
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }
            }

            if (arr is null || arr.Count != totalExpected)
                throw new InvalidOperationException("Unable to obtain valid chapter array.", last);

            outline["chapters"]        = arr;
            outline["outlineProgress"] = OutlineProgress.StructureOutlined.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }

        // --------------------------------------------------------------------
        private static string Sanitize(string raw)
        {
            string t = raw.Trim();
            if (t.StartsWith("```"))
            {
                int firstNL   = t.IndexOf('\n');
                int lastFence = t.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNL >= 0 && lastFence > firstNL)
                    return t.Substring(firstNL + 1, lastFence - firstNL - 1).Trim();
            }
            return t;
        }
    }
}
