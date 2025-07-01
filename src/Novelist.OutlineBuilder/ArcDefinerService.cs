using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder;

/// <summary>
/// Generates a multi‑act story arc and advances outline progress to ArcDefined.
/// </summary>
public sealed class ArcDefinerService
{
    private readonly ILlmClient _llm;

    public ArcDefinerService(ILlmClient llm) => _llm = llm;

    public async Task DefineArcAsync(string outlinePath, string modelId, CancellationToken ct = default)
    {
        var outlineJson = JObject.Parse(await File.ReadAllTextAsync(outlinePath, ct));

        // Guard: must be PremiseExpanded
        if (!Enum.TryParse(outlineJson["outlineProgress"]?.Value<string>(), out OutlineProgress current) ||
            current != OutlineProgress.PremiseExpanded)
            throw new InvalidOperationException("Outline is not in the PremiseExpanded phase.");

        var projectHeaderVersion = outlineJson["header"]!["schemaVersion"]!.Value<int>();

        // Determine act count based on total words
        var totalWords = CalculateTotalWordCount(outlineJson);
        var actCount   = totalWords switch
        {
            >= 130_000 => 5,
            >= 100_000 => 4,
            _          => 3
        };

        // Build prompt
        var prompt = BuildPrompt(outlineJson, actCount);

        var maxRetries = RetryPolicy.GetMaxRetries(modelId);
        string? llmResult = null;
        Exception? lastEx = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                llmResult = await _llm.CompleteAsync(prompt, modelId, ct);
                if (!string.IsNullOrWhiteSpace(llmResult))
                    break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastEx = ex;
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }

        if (string.IsNullOrWhiteSpace(llmResult))
            throw new InvalidOperationException("LLM failed to return arc definition.", lastEx);

        // Merge result – naive parse: each act separated by blank line
        var storyArc = new JArray();
        var segments = llmResult.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var actIndex = 1;
        foreach (var seg in segments)
        {
            storyArc.Add(new JObject
            {
                ["act"]            = actIndex++,
                ["summary"]        = seg.Trim(),
                ["turningPoints"]  = new JArray()      // can be filled in later passes
            });
        }

        outlineJson["storyArc"]       = storyArc;
        outlineJson["outlineProgress"] = OutlineProgress.ArcDefined.ToString();
        outlineJson["header"]!["schemaVersion"] = projectHeaderVersion + 1;

        await File.WriteAllTextAsync(outlinePath, outlineJson.ToString(), ct);
    }

    // ---------------------------------------------------------------------
    private static int CalculateTotalWordCount(JObject outline)
    {
        var chapters = outline["chapters"] as JArray;
        var perChapter = chapters?[0]?["wordBudget"]?.Value<int>() ?? 0;
        return perChapter * (chapters?.Count ?? 0);
    }

    private static string BuildPrompt(JObject outline, int actCount)
    {
        var premise = outline["premise"]!.Value<string>();
        var author  = outline["famousAuthor"]?.HasValues == true
            ? outline["famousAuthor"]!["name"]?.Value<string>()
            : null;

        var authorLine = author is null ? string.Empty : $"Write in a voice reminiscent of {author}.";

        return @$"
You are a professional story structure expert.
Using the premise below, create a clear {actCount}-act outline.
For each act, provide a short paragraph summary suitable for a novelist's planning doc.
Label each act as 'Act 1', 'Act 2', etc. {authorLine}

PREMISE:
{premise}

Respond with {actCount} paragraphs separated by a blank line. Do not add headings beyond the act labels.";
    }
}
