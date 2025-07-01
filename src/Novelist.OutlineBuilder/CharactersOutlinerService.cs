using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder;

/// <summary>
/// Generates a character roster and advances the outline to CharactersOutlined.
/// </summary>
public sealed class CharactersOutlinerService
{
    private readonly ILlmClient _llm;

    public CharactersOutlinerService(ILlmClient llm) => _llm = llm;

    public async Task DefineCharactersAsync(
        string outlinePath,
        string modelId,
        CancellationToken ct = default)
    {
        var outlineJson = JObject.Parse(await File.ReadAllTextAsync(outlinePath, ct));

        if (!Enum.TryParse(outlineJson["outlineProgress"]?.Value<string>(),
                           out OutlineProgress phase) ||
            phase != OutlineProgress.ArcDefined)
        {
            throw new InvalidOperationException("Outline is not in the ArcDefined phase.");
        }

        // Resolve originating project file (relative path stored in outline header)
        var projectRel = outlineJson["header"]?["projectFile"]?.Value<string>() ?? string.Empty;
        var projectAbs = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(outlinePath)!,
            "..",
            projectRel));

        var project = File.Exists(projectAbs)
            ? JObject.Parse(File.ReadAllText(projectAbs))
            : null;

        var totalCharacters =
              1 /*protagonist*/ +
              (project?["additionalProtagonists"]?.Value<int>() ?? 0) +
              (project?["supportingCharacters"]?.Value<int>()   ?? 0) +
              (project?["minorCharacters"]?.Value<int>()        ?? 0);

        totalCharacters = Math.Min(totalCharacters, 7);

        // ---------- FIX: produce non‑nullable string[] -----------------
        var audience = (project?["targetAudience"] as JArray)?
                           .Select(t => t.Value<string>())
                           .Where(s => !string.IsNullOrWhiteSpace(s))
                           .Select(s => s!)               // s is non‑null after Where
                           .ToArray()
                       ?? Array.Empty<string>();
        // ----------------------------------------------------------------

        var prompt = BuildPrompt(outlineJson, totalCharacters, audience);

        var maxRetries = RetryPolicy.GetMaxRetries(modelId);
        string? result = null; Exception? lastEx = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                result = await _llm.CompleteAsync(prompt, modelId, ct);
                if (!string.IsNullOrWhiteSpace(result)) break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastEx = ex;
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
        }

        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("LLM failed to return character roster.", lastEx);

        var characters = JArray.Parse(result); // strict JSON array expected
        outlineJson["characters"]      = characters;
        outlineJson["outlineProgress"] = OutlineProgress.CharactersOutlined.ToString();
        outlineJson["header"]!["schemaVersion"] =
            outlineJson["header"]!["schemaVersion"]!.Value<int>() + 1;

        await File.WriteAllTextAsync(outlinePath, outlineJson.ToString(), ct);
    }

    private static string BuildPrompt(JObject outline, int totalCharacters, string[] audience)
    {
        var premise = outline["premise"]!.Value<string>();
        var audienceLine = audience.Length > 0
            ? $"Target audience: {string.Join(", ", audience)}."
            : string.Empty;

        return @$"
You are an elite character‑development assistant.

Based on the premise below, create a JSON array (no markdown) of up to {totalCharacters} characters.
Each object MUST contain:
  - ""name""  : full name
  - ""role""  : 1 – 7‑word description
  - ""traits"": array of 1‑5 one‑ or two‑word descriptors
  - ""arc""   : ≤ 100‑word description of the character's personal journey

The first object MUST be the story's primary protagonist.

{audienceLine}

PREMISE:
{premise}";
    }
}
