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
    /// Generates evolving sub‑plots for every act.
    /// Each act receives <c>subPlotDepth</c> descriptions that either
    /// continue or replace earlier threads, ensuring variety.
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
                // No sub‑plots requested – just clear arrays and advance.
                foreach (var act in outline["storyArc"]!)
                    act["sub_plots"] = new JArray();

                AdvancePhase(outline, OutlineProgress.SubPlotsDefined);
                File.WriteAllText(outlinePath, outline.ToString());
                return;
            }

            // ------------------ build prompt ---------------------------------
            string premise = outline["premiseExpanded"]?.ToString() ??
                             outline["premise"]!.ToString();

            string genre = project["storyGenre"]?.ToString() ?? "General fiction";

            var audArr = project["targetAudience"] as JArray;
            string aud = _includeAudience && audArr is { Count: > 0 }
                       ? $"Target audience: {string.Join(", ", audArr.Select(a => a.ToString()))}."
                       : string.Empty;

            var actDefs = outline["storyArc"]!
                          .Select((a,idx) => $"Act {idx+1}: {a["definition"]}")
                          .ToArray();

            var prompt =
$@"You are a narrative architect.

Create {depth} sub‑plot threads that weave through the story.
For EACH act, evolve these threads (continue, escalate, or
conclude them). Do NOT repeat the exact sentence in a later act.

Return a raw JSON object where each property is ""Act N"" (1‑based)
and the value is an array of {depth} strings, one per sub‑plot
description for that act.

Example (depth = 2):
{{
  ""Act 1"": [""subplot‑A intro"", ""subplot‑B intro""],
  ""Act 2"": [""subplot‑A complication"", ""subplot‑B twist""],
  ""Act 3"": [""subplot‑A resolution"", ""subplot‑B resolution""]
}}

No commentary, no markdown. If you cannot comply, reply RETRY.

{aud}
Genre: {genre}.

Premise / stakes:
{premise}

Main story arc:
{string.Join("\n", actDefs)}";

            // ------------------ LLM / validation loop ------------------------
            int         maxRetries = RetryPolicy.GetMaxRetries(modelId);
            JObject?    result     = null;
            Exception?  last       = null;

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
                    "LLM failed to produce evolving sub‑plots.", last);

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
            // Check keys
            for (int i = 0; i < acts; i++)
            {
                string k = $"Act {i + 1}";
                if (!obj.ContainsKey(k)) return false;

                var arr = obj[k] as JArray;
                if (arr is null || arr.Count != depth) return false;
                if (arr.Any(t => t.Type != JTokenType.String)) return false;
            }

            // Ensure not every act repeats sentences verbatim.
            // For each subplot index, gather descriptions across acts.
            for (int d = 0; d < depth; d++)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int a = 0; a < acts; a++)
                {
                    string desc = obj[$"Act {a + 1}"]![d]!.ToString().Trim();
                    set.Add(desc);
                }
                if (set.Count == 1) // identical across all acts
                    return false;
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
