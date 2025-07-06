using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Expands each act into a fixed‑length list of beats, then advances the outline
    /// to <see cref="OutlineProgress.BeatsExpanded" />.
    /// </summary>
    public sealed class BeatsExpanderService
    {
        private readonly ILlmClient _llm;
        private readonly bool       _includeAudience;

        public BeatsExpanderService(ILlmClient llm, bool includeAudience = true)
        {
            _llm            = llm;
            _includeAudience = includeAudience;
        }

        public async Task ExpandBeatsAsync(
            string            outlinePath,
            string            modelId,
            CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress phase) ||
                phase != OutlineProgress.SubPlotsDefined)
            {
                throw new InvalidOperationException(
                    "Outline is not in the SubPlotsDefined phase.");
            }

            int chapters = outline["chapterCount"]!.Value<int>();
            int acts     = outline["storyArc"]!.Count();

            int beatsPerAct = (chapters / acts) * 3;
            if (beatsPerAct <= 0)
                beatsPerAct = 3;

            string premise = outline["premise"]!.ToString();
            string genre   = outline["storyGenre"]?.ToString() ?? "General fiction";

            var audArr = outline["targetAudience"] as JArray;
            string aud = _includeAudience && audArr is { Count: > 0 }
                       ? $"Target audience: {string.Join(", ",
                                                         audArr.Select(a => a.ToString()))}."
                       : string.Empty;

            // Character names (if any) assist the model with continuity.
            var charNames = outline["characters"]?
                                .Select(c => c["name"]?.ToString())
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .ToArray() ?? Array.Empty<string>();

            string charsLine = charNames.Length > 0
                             ? $"Principal characters: {string.Join(", ", charNames)}."
                             : string.Empty;

            // ---------- iterate through each act and generate beats ------------
            int maxRetries = RetryPolicy.GetMaxRetries(modelId);

            for (int i = 0; i < acts; i++)
            {
                var actNode     = outline["storyArc"]![i]!;
                string actTitle = actNode["act"]!.ToString();
                string actDef   = actNode["definition"]!.ToString();

                var subPlots    = actNode["sub_plots"]!
                                  .Select(s => s.ToString())
                                  .ToArray();

                string plotsLine = subPlots.Length > 0
                                 ? $"Sub‑plots in this act: {string.Join("; ", subPlots)}."
                                 : string.Empty;

                string prompt =
$@"You are a pacing assistant.

Create exactly {beatsPerAct} concise, chronological story beats
(< 25 words each) for {actTitle}.

Return ONLY a raw JSON array of strings.
If unable, reply RETRY.

{aud}
{charsLine}
{plotsLine}
Genre: {genre}.

Premise:
{premise}

{actTitle} definition:
{actDef}";

                JArray? beats = null;
                Exception? last = null;

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

                    reply = reply.Trim();

                    if (string.Equals(reply, "RETRY",
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
                        beats = JArray.Parse(jsonFrag);
                        if (beats.Count == beatsPerAct &&
                            beats.All(b => b.Type == JTokenType.String))
                        {
                            break; // success
                        }
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }

                if (beats is null)
                    throw new InvalidOperationException(
                        $"LLM failed to generate beats for {actTitle}.", last);

                actNode["beats"] = beats;
            }

            // ---------- advance phase & save -----------------------------------
            outline["outlineProgress"] = OutlineProgress.BeatsExpanded.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }

        // ---------------------------------------------------------------------
        //  Helper – extracts first JSON array from a string.
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
