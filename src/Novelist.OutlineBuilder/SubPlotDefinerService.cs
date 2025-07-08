using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates evolving sub-plots for every act and prefixes each line with
    /// S1:, S2:, … so threads can be traced in later stages.
    /// </summary>
    public sealed class SubPlotDefinerService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAudience;

        public SubPlotDefinerService(ILlmClient llm, bool includeAudience = true)
        {
            _llm            = llm;
            _includeAudience = includeAudience;
        }

        public async Task DefineSubPlotsAsync(
            string            outlinePath,
            string            modelId,
            CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                phase != OutlineProgress.CharactersOutlined)
            {
                throw new InvalidOperationException(
                    "Outline is not in the CharactersOutlined phase.");
            }

            // ---------------- project & settings -----------------------------
            var rel = outline["header"]?["projectFile"]?.ToString()
                      ?? throw new InvalidDataException("header.projectFile missing.");

            var projectPath = Path.GetFullPath(
                                Path.Combine(Path.GetDirectoryName(outlinePath)!, rel));
            var project     = JObject.Parse(File.ReadAllText(projectPath));

            int depth = project["subPlotDepth"]?.Value<int>() ?? 1;
            if (depth < 0) depth = 0;
            int acts  = outline["storyArc"]!.Count();

            if (depth == 0)
            {
                foreach (var act in outline["storyArc"]!)
                    act["sub_plots"] = new JArray();

                AdvancePhase(outline, OutlineProgress.SubPlotsDefined);
                File.WriteAllText(outlinePath, outline.ToString());
                return;
            }

            // ------------------ build prompt ---------------------------------
            string premise = outline["premiseExpanded"]?.ToString()
                           ?? outline["premise"]!.ToString();

            string genre = project["storyGenre"]?.ToString() ?? "General fiction";

            var audArr = project["targetAudience"] as JArray;
            string aud = _includeAudience && audArr is { Count: > 0 }
                       ? $"Target audience: {string.Join(", ", audArr.Select(a => a.ToString()))}."
                       : string.Empty;

            var actDefs = outline["storyArc"]!
                          .Select((a,idx) => $"Act {idx+1}: {a["definition"]}")
                          .ToArray();

            string idLegend = string.Join(", ",
                               Enumerable.Range(1, depth).Select(i => $"S{i}"));

            var prompt =
$@"You are a narrative architect.

Create {depth} subplot threads (IDs S1…S{depth}).  For EACH act, give the
next stage of every thread.  Start each sentence with its ID followed by a
colon, e.g.  ""S2: The rivalry escalates …"".

Return a raw JSON object:
  {{ ""Act 1"": [ ""S1: …"", ""S2: …"" ], ""Act 2"": [ … ], … }}

All arrays must contain exactly {depth} entries.

No commentary, no markdown.  If you cannot comply, reply RETRY.

{aud}
Genre: {genre}.

Premise / stakes:
{premise}

Main story arc:
{string.Join("\n", actDefs)}";

            // ------------------ LLM / validation loop ------------------------
            int        maxRetries = RetryPolicy.GetMaxRetries(modelId);
            JObject?   result     = null;
            Exception? last       = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                string reply;
                try
                {
                    reply = await _llm.CompleteAsync(prompt, modelId, ct);
                }
                catch (Exception ex)
                {
                    last = ex;
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                    continue;
                }

                if (string.Equals(reply.Trim(), "RETRY",
                                  StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                    continue;
                }

                if (!TryExtractJsonObject(reply, out var jsonFrag))
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                    continue;
                }

                try
                {
                    var obj = JObject.Parse(jsonFrag);

                    if (Validate(obj, acts, depth))
                    {
                        result = obj;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    last = ex;
                }

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }

            if (result is null)
                throw new InvalidOperationException(
                    "LLM failed to produce valid sub-plots.", last);

            // --------------- inject into outline -----------------------------
            for (int i = 0; i < acts; i++)
            {
                string key = $"Act {i + 1}";
                var arr = (JArray)result[key]!;
                outline["storyArc"]![i]!["sub_plots"] = arr;
            }

            AdvancePhase(outline, OutlineProgress.SubPlotsDefined);
            File.WriteAllText(outlinePath, outline.ToString());
        }

        // ---------------------------------------------------------------------
        //  Validation helpers
        // ---------------------------------------------------------------------

        private static bool Validate(JObject obj, int acts, int depth)
        {
            // Check keys & prefixes
            for (int a = 0; a < acts; a++)
            {
                string key = $"Act {a + 1}";
                if (!obj.ContainsKey(key)) return false;

                var arr = obj[key] as JArray;
                if (arr is null || arr.Count != depth) return false;

                for (int d = 0; d < depth; d++)
                {
                    string expectedPrefix = $"S{d + 1}:";
                    if (!arr[d]!.ToString().TrimStart().StartsWith(expectedPrefix,
                           StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            // Each subplot sentence must evolve (not identical every act)
            for (int d = 0; d < depth; d++)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int a = 0; a < acts; a++)
                    set.Add(obj[$"Act {a + 1}"]![d]!.ToString().Trim());
                if (set.Count == 1) return false;
            }

            return true;
        }

        private static bool TryExtractJsonObject(string input, out string json)
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

            int open  = s.IndexOf('{');
            int close = s.LastIndexOf('}');

            if (open >= 0 && close > open)
            {
                json = s.Substring(open, close - open + 1).Trim();
                return true;
            }
            return false;
        }

        private static void AdvancePhase(JObject outline, OutlineProgress next)
        {
            outline["outlineProgress"] = next.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;
        }
    }
}
