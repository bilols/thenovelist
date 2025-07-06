using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates chapter‑level structure and advances the outline to StructureOutlined.
    /// </summary>
    public sealed class StructureOutlinerService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAudience;

        public StructureOutlinerService(ILlmClient llm, bool includeAudience = true)
        {
            _llm            = llm;
            _includeAudience = includeAudience;
        }

        public async Task DefineStructureAsync(
            string            outlinePath,
            string            modelId,
            CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            // Accept both modern and legacy entry phases
            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                (phase != OutlineProgress.BeatsExpanded &&
                 phase != OutlineProgress.CharactersOutlined))     // legacy
            {
                throw new InvalidOperationException(
                    "Outline is not ready for structure definition.");
            }

            int chapters = outline["chapterCount"]!.Value<int>();
            int acts     = outline["storyArc"]!.Count();

            var beatsPerAct = outline["storyArc"]![0]!["beats"]!.Count();
            if (beatsPerAct == 0)
                beatsPerAct = (chapters / acts) * 3; // fallback if beats not filled

            string premise = outline["premise"]!.ToString();
            string genre   = outline["storyGenre"]?.ToString() ?? "General fiction";

            var audArr = outline["targetAudience"] as JArray;
            string aud = _includeAudience && audArr is { Count: > 0 }
                       ? $"Target audience: {string.Join(", ",
                                                         audArr.Select(a => a.ToString()))}."
                       : string.Empty;

            // Prompt includes acts, beats, and sub‑plots if present
            var actsBlock = string.Join("\n\n",
                outline["storyArc"]!.Select(a =>
                    $"{a["act"]}: {a["definition"]}\n" +
                    $"SUBPLOTS: {string.Join("; ", a["sub_plots"]!)}\n" +
                    $"BEATS: {string.Join(" | ", a["beats"]!)}"));

            var prompt =
$@"You are a seasoned editor.

The novel has {chapters} chapters arranged across {acts} acts.
Each chapter should be summarised in 1–2 sentences and include
exactly 3 beats from the act.

Return ONLY a raw JSON array where each element is:
{{ ""number"": <int>, ""summary"": ""..."", ""beats"": [ ""beat1"", ""beat2"", ""beat3"" ] }}

If you cannot comply, reply RETRY.

{aud}
Genre: {genre}.

Premise:
{premise}

ACT / SUB‑PLOT / BEAT GRID:
{actsBlock}";

            int    max  = RetryPolicy.GetMaxRetries(modelId);
            JArray? arr = null;

            for (int i = 1; i <= max; i++)
            {
                var reply = await _llm.CompleteAsync(prompt, modelId, ct);

                if (string.Equals(reply.Trim(), "RETRY",
                                  StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(TimeSpan.FromSeconds(i * 2), ct);
                    continue;
                }

                if (!TryExtractJsonArray(reply, out var jsonFrag))
                {
                    await Task.Delay(TimeSpan.FromSeconds(i * 2), ct);
                    continue;
                }

                try
                {
                    arr = JArray.Parse(jsonFrag);
                    if (arr.Count == chapters)
                        break;
                }
                catch
                {
                    // ignore and retry
                }

                await Task.Delay(TimeSpan.FromSeconds(i * 2), ct);
            }

            if (arr is null || arr.Count != chapters)
                throw new InvalidOperationException(
                    "LLM failed to return the expected chapter list.");

            outline["chapters"]        = arr;
            outline["outlineProgress"] = OutlineProgress.StructureOutlined.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }

        // ---------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------

        private static bool TryExtractJsonArray(string input, out string json)
        {
            json = string.Empty;
            string s = input.Trim();

            if (s.StartsWith("```"))
            {
                int first = s.IndexOf('\n');
                int last  = s.LastIndexOf("```", StringComparison.Ordinal);
                if (first >= 0 && last > first)
                    s = s.Substring(first + 1, last - first - 1).Trim();
            }

            int open  = s.IndexOf('[');
            int close = s.LastIndexOf(']');

            if (open >= 0 && close > open)
            {
                json = s.Substring(open, close - open + 1).Trim();
                return true;
            }

            return false;
        }
    }
}
