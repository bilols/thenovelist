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

            // Accept both modern and legacy entry phases
            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                (phase != OutlineProgress.BeatsExpanded &&
                 phase != OutlineProgress.CharactersOutlined))
            {
                throw new InvalidOperationException(
                    "Outline is not ready for structure definition.");
            }

            // ---------- static values ----------------------------------------
            int chapters = outline["chapterCount"]!.Value<int>();
            int acts     = outline["storyArc"]!.Count();

            // project settings for validation
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

            // beats per act: fall back to formula if beats array empty
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

The novel has {chapters} chapters distributed across {acts} acts.
For each chapter, produce:

  ""number""   : integer 1‑based
  ""summary""  : 1 – 2 sentences
  ""beats""    : exactly 3 strings (taken from the act's beat list)
  ""sub_plots"": 0 – {depth} strings (chapter‑specific refinements)

Return ONLY a raw JSON array of chapter objects in order.
If you cannot comply, reply RETRY.

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
                    if (Validate(arr, chapters, beatsPerAct, depth))
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

        private static bool Validate(JArray arr, int expectedChapters,
                                     int beatsPerChapter, int depth)
        {
            if (arr.Count != expectedChapters)
                return false;

            foreach (var node in arr)
            {
                if (node.Type != JTokenType.Object)
                    return false;

                var obj = (JObject)node;

                if (!obj.ContainsKey("number")   ||
                    !obj.ContainsKey("summary")  ||
                    !obj.ContainsKey("beats"))
                    return false;

                // beats must be array length 3
                var beats = obj["beats"] as JArray;
                if (beats is null || beats.Count != beatsPerChapter /  (beatsPerChapter/3))
                    return false;

                // ensure sub_plots array exists & length <= depth
                if (!obj.ContainsKey("sub_plots") || obj["sub_plots"]!.Type != JTokenType.Array)
                    obj["sub_plots"] = new JArray();

                var plots = (JArray)obj["sub_plots"]!;
                if (plots.Count > depth)
                    obj["sub_plots"] = new JArray(plots.Take(depth));
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
