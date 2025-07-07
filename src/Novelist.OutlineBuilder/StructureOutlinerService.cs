using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates chapter‑level structure (summary + beats + sub_plots)
    /// and advances the outline to StructureOutlined.
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

            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                (phase != OutlineProgress.BeatsExpanded &&
                 phase != OutlineProgress.CharactersOutlined))
            {
                throw new InvalidOperationException(
                    "Outline is not ready for structure definition.");
            }

            int chapters = outline["chapterCount"]!.Value<int>();
            int acts     = outline["storyArc"]!.Count();

            int depth = 0;
            {
                var rel = outline["header"]?["projectFile"]?.ToString();
                if (rel is not null)
                {
                    var proj = JObject.Parse(File.ReadAllText(
                                 Path.Combine(Path.GetDirectoryName(outlinePath)!, rel)));
                    depth = proj["subPlotDepth"]?.Value<int>() ?? 0;
                }
            }

            int beatsPerAct = outline["storyArc"]![0]!["beats"]!.Count();
            if (beatsPerAct == 0)
                beatsPerAct = (chapters / acts) * 3;

            string premise = outline["premiseExpanded"]?.ToString()
                           ?? outline["premise"]!.ToString();

            string genre   = outline["storyGenre"]?.ToString() ?? "General fiction";

            var audArr = outline["targetAudience"] as JArray;
            string aud = _includeAudience && audArr is { Count: > 0 }
                       ? $"Target audience: {string.Join(", ",
                                                         audArr.Select(a => a.ToString()))}."
                       : string.Empty;

            // ---------- build ACT / BEAT / SUB‑PLOT grid ---------------------
            var actsBlock = string.Join("\n\n",
                outline["storyArc"]!.Select((a,idx) =>
                {
                    var beats = string.Join(" | ", a["beats"]!.Select(b => b.ToString()));
                    var plots = string.Join("; ",  a["sub_plots"]!.Select(p => p.ToString()));
                    return $"Act {idx+1}: {a["definition"]}\n" +
                           $"SUB_PLOTS: {plots}\nBEATS: {beats}";
                }));

            // ---------- prompt ----------------------------------------------
            var prompt =
$@"You are a seasoned development editor.

The novel has {chapters} chapters across {acts} acts.
For each chapter, create:

  ""number""   : 1‑based integer
  ""summary""  : 1–2 sentences
  ""beats""    : exactly 3 strings drawn from the act's beat list
  ""sub_plots"": 1–{depth} strings, each beginning with its ID (S1:, S2:, …)

You must include AT LEAST one subplot line per chapter.

Return ONLY a raw JSON array.  If you cannot comply, reply RETRY.

{aud}
Genre: {genre}.

Premise:
{premise}

ACT / BEAT / SUB_PLOT GRID:
{actsBlock}";

            // ---------- LLM loop --------------------------------------------
            int    maxRetries = RetryPolicy.GetMaxRetries(modelId);
            JArray? chaptersArray = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var reply = await _llm.CompleteAsync(prompt, modelId, ct);

                if (string.Equals(reply.Trim(), "RETRY",
                                  StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                    continue;
                }

                if (!TryExtractJsonArray(reply, out var jsonFrag))
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                    continue;
                }

                try
                {
                    var arr = JArray.Parse(jsonFrag);
                    if (Validate(arr, chapters, depth))
                    {
                        chaptersArray = arr;
                        break;
                    }
                }
                catch
                {
                    // ignore & retry
                }

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }

            if (chaptersArray is null)
                throw new InvalidOperationException(
                    "LLM failed to return valid chapter list.");

            outline["chapters"]        = chaptersArray;
            outline["outlineProgress"] = OutlineProgress.StructureOutlined.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }

        // ---------------------------------------------------------------------
        //  Validation helpers
        // ---------------------------------------------------------------------

        private static bool Validate(JArray arr, int expectedChapters, int depth)
        {
            if (arr.Count != expectedChapters) return false;

            var idSet = Enumerable.Range(1, depth)
                                  .Select(i => $"S{i}:")
                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < arr.Count; i++)
            {
                var obj = arr[i] as JObject;
                if (obj is null) return false;

                // must have beats array length 3
                if (obj["beats"] is not JArray beats || beats.Count != 3) return false;

                // ensure sub_plots exists
                if (obj["sub_plots"] is not JArray plots)
                    return false;

                if (plots.Count == 0 || plots.Count > depth)
                    return false;

                // each entry must start with S#: prefix
                foreach (var p in plots.Select(p => p.ToString()))
                {
                    if (!idSet.Any(id => p.TrimStart().StartsWith(id,
                                            StringComparison.OrdinalIgnoreCase)))
                        return false;
                }
            }

            return true;
        }

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
