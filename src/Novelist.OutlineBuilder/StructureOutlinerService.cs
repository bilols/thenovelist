using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Creates the chapter list (summary, beats, optional themes)
    /// and advances the outline to StructureOutlined.
    /// </summary>
    public sealed class StructureOutlinerService
    {
        private readonly ILlmClient _llm;
        public StructureOutlinerService(ILlmClient llm) => _llm = llm;

        public async Task DefineStructureAsync(
            string outlinePath,
            string modelId,
            CancellationToken ct = default)
        {
            var outlineJson =
                JObject.Parse(await File.ReadAllTextAsync(outlinePath, ct));

            if (!Enum.TryParse(
                    outlineJson["outlineProgress"]?.Value<string>(),
                    out OutlineProgress phase) ||
                phase != OutlineProgress.CharactersOutlined)
                throw new InvalidOperationException(
                    "Outline is not in the CharactersOutlined phase.");

            // Word and chapter counts come from earlier passes
            var chapterCount   = outlineJson["chapterCount"]!.Value<int>();
            var totalWordCount = outlineJson["totalWordCount"]!.Value<int>();
            var wordsPerChap   = totalWordCount / chapterCount;

            var prompt = BuildPrompt(outlineJson, chapterCount, wordsPerChap);

            var retries   = RetryPolicy.GetMaxRetries(modelId);
            string? json  = null;
            Exception? ex = null;

            for (var attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    json = await _llm.CompleteAsync(prompt, modelId, ct);
                    if (!string.IsNullOrWhiteSpace(json)) break;
                }
                catch (Exception err) when (attempt < retries)
                {
                    ex = err;
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }
            }

            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException(
                    "LLM did not return valid chapter JSON.", ex);

            outlineJson["chapters"]        = JArray.Parse(json);
            outlineJson["outlineProgress"] =
                OutlineProgress.StructureOutlined.ToString();

            outlineJson["header"]!["schemaVersion"] =
                outlineJson["header"]!["schemaVersion"]!.Value<int>() + 1;

            await File.WriteAllTextAsync(
                outlinePath, outlineJson.ToString(), ct);
        }

        private static string BuildPrompt(
            JObject outline, int chapters, int wordsPerChapter)
        {
            var premise = outline["premise"]!.ToString();
            var arc     = outline["storyArc"]?.ToString() ?? string.Empty;
            var roster  = outline["characters"]?.ToString() ?? "[]";

            return
$@"You are an experienced story‑structure editor.

Return a JSON array (no Markdown) with exactly {chapters} objects. Each object must have:
  ""number"":  (1‑based integer)
  ""summary"": single paragraph, max 80 words
  ""beats""  : 3 to 6 short beat descriptions, each max 30 words
  ""themes"" : optional array of 1 to 3 one‑phrase themes

Assume roughly {wordsPerChapter} words per chapter total.

MATERIAL
Premise:
{premise}

Arc:
{arc}

Characters:
{roster}

Only output valid JSON.";
        }
    }
}
