using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Generates global sub‑plots and injects them into every act,
    /// advancing the outline to SubPlotsDefined.
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

            var rel = outline["header"]?["projectFile"]?.ToString()
                      ?? throw new InvalidDataException("header.projectFile missing.");

            var projectPath = Path.GetFullPath(
                                Path.Combine(Path.GetDirectoryName(outlinePath)!, rel));
            var project     = JObject.Parse(File.ReadAllText(projectPath));

            int depth = project["subPlotDepth"]?.Value<int>() ?? 1;

            // If depth == 0, skip generation but still advance phase.
            if (depth <= 0)
            {
                foreach (var act in outline["storyArc"]!)
                    act["sub_plots"] = new JArray();

                outline["outlineProgress"] = OutlineProgress.SubPlotsDefined.ToString();
                outline["header"]!["schemaVersion"] =
                    outline["header"]!["schemaVersion"]!.Value<int>() + 1;

                File.WriteAllText(outlinePath, outline.ToString());
                return;
            }

            string premise = outline["premise"]!.ToString();
            string genre   = project["storyGenre"]?.ToString() ?? "General fiction";

            var audArr = project["targetAudience"] as JArray;
            string aud = _includeAudience && audArr is { Count: > 0 }
                       ? $"Target audience: {string.Join(", ",
                                                         audArr.Select(a => a.ToString()))}."
                       : string.Empty;

            string hall = project["famousAuthorPreset"]?.ToString() ?? string.Empty;
            string hallLine = !string.IsNullOrWhiteSpace(hall)
                            ? $"Emulate the narrative flair of {hall.Replace(".json", "")} " +
                              "while keeping originality."
                            : string.Empty;

            var acts = outline["storyArc"]!
                       .Select(a => a["definition"]!.ToString())
                       .ToArray();

            var prompt =
$@"You are a plotting assistant.

Create exactly {depth} distinct one-sentence sub-plots
that will weave through ALL acts of the main story.
Return ONLY a raw JSON array of strings. If you cannot comply, reply RETRY.

{aud}
{hallLine}
Genre: {genre}.

Premise:
{premise}

MAIN ACT DEFINITIONS:
{string.Join("\n", acts)}";

            int         max   = RetryPolicy.GetMaxRetries(modelId);
            JArray?     subs  = null;
            Exception?  last  = null;

            int attempt = 0;
            while (attempt < max)
            {
                attempt++;

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

                reply = reply.Trim();

                // Model explicitly declined
                if (string.Equals(reply, "RETRY", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                    continue;
                }

                // Try to extract a JSON array from the reply
                if (!TryExtractJsonArray(reply, out var jsonFragment))
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                    continue;
                }

                try
                {
                    subs = JArray.Parse(jsonFragment);
                    if (subs.Count == depth)
                        break; // success
                }
                catch (Exception ex)
                {
                    last = ex;
                }

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }

            if (subs is null || subs.Count != depth)
                throw new InvalidOperationException(
                    "LLM failed to return correct sub-plot list.", last);

            foreach (var act in outline["storyArc"]!)
                act["sub_plots"] = new JArray(subs.Select(s => s.DeepClone()));

            outline["outlineProgress"] = OutlineProgress.SubPlotsDefined.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }

        // ---------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to pull the first JSON array found in the string.
        /// Handles code fences and extra commentary.
        /// </summary>
        private static bool TryExtractJsonArray(string input, out string json)
        {
            json = string.Empty;
            string s = input.Trim();

            // Remove ```json fences if present
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
