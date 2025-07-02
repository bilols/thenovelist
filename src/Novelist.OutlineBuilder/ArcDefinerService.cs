using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates a concise multi‑act story arc
    /// and advances the outline to ArcDefined.
    /// </summary>
    public sealed class ArcDefinerService
    {
        private readonly ILlmClient _llm;
        public ArcDefinerService(ILlmClient llm) => _llm = llm;

        public async Task DefineArcAsync(
            string outlinePath,
            string modelId,
            CancellationToken ct = default)
        {
            var outlineJson =
                JObject.Parse(await File.ReadAllTextAsync(outlinePath, ct));

            if (!Enum.TryParse(
                    outlineJson["outlineProgress"]?.ToString(),
                    out OutlineProgress phase) ||
                phase != OutlineProgress.PremiseExpanded)
                throw new InvalidOperationException(
                    "Outline is not in the PremiseExpanded phase.");

            // ----------------------------------------------------------------
            // Retrieve premise as non‑nullable string
            // ----------------------------------------------------------------
            var premise = outlineJson["premise"]?.ToString() ?? string.Empty;
            var prompt  = BuildPrompt(premise);

            var retries   = RetryPolicy.GetMaxRetries(modelId);
            string? arc   = null;
            Exception? ex = null;

            for (var attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    arc = await _llm
                               .CompleteAsync(prompt, modelId, ct)
                               .ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(arc)) break;
                }
                catch (Exception err) when (attempt < retries)
                {
                    ex = err;
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }
            }

            if (string.IsNullOrWhiteSpace(arc))
                throw new InvalidOperationException(
                    "LLM did not return a story arc.", ex);

            outlineJson["storyArc"]        = arc.Trim();
            outlineJson["outlineProgress"] = OutlineProgress.ArcDefined.ToString();
            outlineJson["header"]!["schemaVersion"] =
                outlineJson["header"]!["schemaVersion"]!.Value<int>() + 1;

            await File.WriteAllTextAsync(
                outlinePath, outlineJson.ToString(), ct);
        }

        private static string BuildPrompt(string premise) =>
$@"You are a narrative‑structure expert.

Based on the premise below, write a concise multi‑act outline
(three to five acts).  Return the outline as plain text paragraphs.

PREMISE:
{premise}";
    }
}
