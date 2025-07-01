using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder;

/// <summary>
/// Expands the story premise by invoking an LLM and merging the result into the outline.
/// </summary>
public sealed class PremiseExpanderService
{
    private readonly ILlmClient _llm;
    private readonly int        _maxRetries;
    private readonly TimeSpan   _initialDelay;

    public PremiseExpanderService(ILlmClient llm, int maxRetries = 3, TimeSpan? initialDelay = null)
    {
        _llm          = llm;
        _maxRetries   = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>
    /// Mutates the outline in place (overwrites file).
    /// </summary>
    public async Task ExpandPremiseAsync(string outlinePath, string modelId, CancellationToken ct = default)
    {
        var outlineJson = JObject.Parse(await File.ReadAllTextAsync(outlinePath, ct));

        // Validate current phase
        if (!Enum.TryParse(outlineJson["outlineProgress"]?.Value<string>(), out OutlineProgress current) ||
            current != OutlineProgress.Init)
            throw new InvalidOperationException("Outline is not in the Init phase.");

        // Compose prompt
        var prompt = BuildPrompt(outlineJson);

        // Retry loop
        Exception? lastEx = null;
        string?    llmResult = null;

        for (var attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                llmResult = await _llm.CompleteAsync(prompt, modelId, ct);
                if (!string.IsNullOrWhiteSpace(llmResult))
                    break;
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                lastEx = ex;
                await Task.Delay(_initialDelay * attempt, ct); // simple back‑off
            }
        }

        if (string.IsNullOrWhiteSpace(llmResult))
            throw new InvalidOperationException("LLM failed to return premise.", lastEx);

        // Merge result
        outlineJson["premise"]         = llmResult.Trim();
        outlineJson["outlineProgress"] = OutlineProgress.PremiseExpanded.ToString();

        // Overwrite file
        await File.WriteAllTextAsync(outlinePath, outlineJson.ToString(), ct);
    }

    // ---------------------------------------------------------------------
    // Prompt builder (const string template)
    // ---------------------------------------------------------------------
    private static string BuildPrompt(JObject outline)
    {
        var workingPremise = outline["premise"]!.Value<string>();
        var authorSection  = outline["famousAuthor"]?.HasValues == true
            ? $"Write in the general style of {outline["famousAuthor"]!["name"]}."
            : string.Empty;

        return @$"
You are an expert fiction development editor.
Given the rough premise below, expand it to a rich 300‑500‑word high‑concept premise
that establishes tone, stakes, protagonist, and antagonist without revealing the ending.

{authorSection}

ROUGH PREMISE:
{workingPremise}

Format as plain paragraphs; do not prepend headings or bullet points.";
    }
}
