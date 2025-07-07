using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Expands the original premise into a concise, plot‑oriented paragraph
    /// while leaving the original premise unchanged.
    /// Writes the result to <c>premiseExpanded</c> and advances the phase.
    /// </summary>
    public sealed class PremiseExpanderService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAudience;

        public PremiseExpanderService(ILlmClient llm, bool includeAudience = true)
        {
            _llm            = llm;
            _includeAudience = includeAudience;
        }

        /// <param name="maxWords">
        /// Hard upper limit for the expansion (150–350). The method enforces it.
        /// </param>
        public async Task ExpandPremiseAsync(
            string            outlinePath,
            string            modelId,
            int               maxWords        = 250,
            CancellationToken ct              = default)
        {
            if (maxWords < 150 || maxWords > 350)
                throw new ArgumentOutOfRangeException(
                    nameof(maxWords), "Word limit must be between 150 and 350.");

            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                phase != OutlineProgress.Init)
            {
                throw new InvalidOperationException(
                    "Outline is not in the Init phase.");
            }

            string premise = outline["premise"]!.ToString();
            string genre   = outline["storyGenre"]?.ToString() ?? "General fiction";

            var audArr = outline["targetAudience"] as JArray;
            string aud = _includeAudience && audArr is { Count: > 0 }
                       ? $"Target audience: {string.Join(", ",
                                                         audArr.Select(a => a.ToString()))}."
                       : string.Empty;

            var prompt =
$@"You are a concise plotting assistant.

Expand the ORIGINAL premise below by adding clear conflict, stakes,
and trajectory for the main storyline. Preserve the premise's core
ideas and wording; do NOT rewrite it in flowery prose.

Return ONE paragraph no longer than {maxWords} words.
No headings, no markdown, no commentary — just the paragraph.

{aud}
Genre: {genre}.

ORIGINAL PREMISE:
{premise}";

            int    maxRetries = RetryPolicy.GetMaxRetries(modelId);
            string best       = string.Empty;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                best = (await _llm.CompleteAsync(prompt, modelId, ct)).Trim();

                // Strip ``` fences if present
                if (best.StartsWith("```"))
                {
                    int first = best.IndexOf('\n');
                    int last  = best.LastIndexOf("```", StringComparison.Ordinal);
                    if (first >= 0 && last > first)
                        best = best.Substring(first + 1, last - first - 1).Trim();
                }

                int wordCount = CountWords(best);

                if (wordCount <= maxWords && wordCount >= 50)
                    break; // accept
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }

            if (CountWords(best) > maxWords)
                throw new InvalidOperationException(
                    "Failed to obtain a succinct premise expansion within word limit.");

            outline["premiseExpanded"] = best;
            outline["outlineProgress"] = OutlineProgress.PremiseExpanded.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }

        private static int CountWords(string s)
            => string.IsNullOrWhiteSpace(s)
               ? 0
               : s.Split(' ', '\n', '\r', '\t')
                  .Count(t => t.Length > 0);
    }
}
