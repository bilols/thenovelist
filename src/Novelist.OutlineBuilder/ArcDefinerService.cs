using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates a multi-act story arc (3 – 5 acts per chapter-count rule)
    /// and advances the outline to ArcDefined.
    /// </summary>
    public sealed class ArcDefinerService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAudience;

        public ArcDefinerService(ILlmClient llm, bool includeAudience = true)
        {
            _llm            = llm;
            _includeAudience = includeAudience;
        }

        public async Task DefineArcAsync(
            string            outlinePath,
            string            modelId,
            CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                phase != OutlineProgress.PremiseExpanded)
            {
                throw new InvalidOperationException(
                    "Outline is not in the PremiseExpanded phase.");
            }

            int chapters = outline["chapterCount"]!.Value<int>();
            int acts     = chapters <= 15 ? 3 :
                           chapters <= 24 ? 4 : 5;

            string premise = outline["premise"]!.ToString();
            string genre   = outline["storyGenre"]?.ToString() ?? "General fiction";

            var audArr = outline["targetAudience"] as JArray;
            string aud = _includeAudience && audArr is { Count: > 0 }
                       ? $"Target audience: {string.Join(", ",
                                                         audArr.Select(a => a.ToString()))}."
                       : string.Empty;

            var prompt =
$@"You are a narrative-structure expert.

Create exactly {acts} ACT paragraphs (one blank line between each).
No headings, no markdown.
{aud}
Genre: {genre}.

Premise:
{premise}";

            int    max    = RetryPolicy.GetMaxRetries(modelId);
            string raw    = string.Empty;
            string[]? blocks = null;

            for (int i = 1; i <= max; i++)
            {
                raw    = await _llm.CompleteAsync(prompt, modelId, ct);
                blocks = raw.Split(
                              new[] { "\r\n\r\n", "\n\n" },
                              StringSplitOptions.RemoveEmptyEntries);

                if (blocks.Length == acts)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(i * 2), ct);
            }

            if (blocks is null || blocks.Length != acts)
                throw new InvalidOperationException(
                    "LLM failed to supply correct number of acts.");

            var jActs = new JArray();
            for (int i = 0; i < acts; i++)
            {
                jActs.Add(new JObject
                {
                    { "act",        $"Act {i + 1}" },
                    { "definition", blocks[i].Trim() },
                    { "beats",      new JArray() },
                    { "sub_plots",  new JArray() }
                });
            }

            outline["storyArc"]        = jActs;
            outline["outlineProgress"] = OutlineProgress.ArcDefined.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }
    }
}
