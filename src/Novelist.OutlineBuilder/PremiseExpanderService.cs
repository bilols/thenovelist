using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Expands a short premise to roughly 200–300 words and advances the outline.
    /// </summary>
    public sealed class PremiseExpanderService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAudience;

        public PremiseExpanderService(ILlmClient llm, bool includeAudience = true)
        {
            _llm             = llm;
            _includeAudience = includeAudience;
        }

        public async Task ExpandPremiseAsync(string outlinePath,
                                             string modelId,
                                             CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                phase != OutlineProgress.Init)
                throw new InvalidOperationException("Outline is not in the Init phase.");

            var shortPremise = outline["premise"]!.ToString();
            var genre        = outline["storyGenre"]?.ToString() ?? "General fiction";
            var audienceArr  = outline["targetAudience"] as JArray;
            var audienceLine = _includeAudience && audienceArr is { Count: > 0 }
                                ? $"Target audience: {string.Join(", ", audienceArr)}."
                                : string.Empty;

            var prompt =
$@"Expand the premise below into a vivid 200‑300‑word paragraph in the style of {genre}.
Include tone, stakes, and atmosphere. {audienceLine}

Return ONLY the expanded premise text without markdown fences.

PREMISE:
{shortPremise}";

            var expanded = await _llm.CompleteAsync(prompt, modelId, ct);

            outline["premise"]         = expanded.Trim();
            outline["outlineProgress"] = OutlineProgress.PremiseExpanded.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }
    }
}
