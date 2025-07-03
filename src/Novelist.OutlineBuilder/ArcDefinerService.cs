using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates a multi‑act story arc and advances the outline.
    /// </summary>
    public sealed class ArcDefinerService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAudience;

        public ArcDefinerService(ILlmClient llm, bool includeAudience = true)
        {
            _llm             = llm;
            _includeAudience = includeAudience;
        }

        public async Task DefineArcAsync(string outlinePath,
                                         string modelId,
                                         CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                phase != OutlineProgress.PremiseExpanded)
                throw new InvalidOperationException("Outline is not in the PremiseExpanded phase.");

            var premise  = outline["premise"]!.ToString();
            var genre    = outline["storyGenre"]?.ToString() ?? "General fiction";
            var audience = outline["targetAudience"] as JArray;
            var audienceLine = _includeAudience && audience is { Count: > 0 }
                                ? $"Target audience: {string.Join(", ", audience)}."
                                : string.Empty;

            var prompt =
$@"You are a narrative‑structure expert.

Create a concise three‑ to five‑act outline (plain text, one paragraph per act)
for the {genre} premise below. {audienceLine}

Return ONLY the outline paragraphs (no markdown).

PREMISE:
{premise}";

            var arc = await _llm.CompleteAsync(prompt, modelId, ct);

            outline["storyArc"]        = arc.Trim();
            outline["outlineProgress"] = OutlineProgress.ArcDefined.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }
    }
}
