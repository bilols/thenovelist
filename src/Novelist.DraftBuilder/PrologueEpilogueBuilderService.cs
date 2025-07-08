using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Novelist.OutlineBuilder;          // for ILlmClient

namespace Novelist.DraftBuilder
{
    /// <summary>
    /// Builds the prologue or epilogue (½ chapter budget, no sub‑plots).
    /// </summary>
    public sealed class PrologueEpilogueBuilderService
    {
        private readonly ILlmClient _llm;

        public PrologueEpilogueBuilderService(ILlmClient llm) => _llm = llm;

        public async Task BuildAsync(
            string  outlinePath,
            string  outputDir,
            string  modelId,
            bool    isPrologue,
            CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            int chapterCount = outline["chapterCount"]!.Value<int>();
            int totalWords   = outline["totalWordCount"]!.Value<int>();
            int targetPer    = (int)Math.Round(totalWords / (double)chapterCount / 2,
                                               MidpointRounding.AwayFromZero);

            var style = FamousAuthorPresetLoader.Load(
                Path.Combine(Environment.CurrentDirectory, "author_presets"),
                outline["famousAuthorPreset"]?.ToString() ?? "");

            string label   = isPrologue ? "prologue" : "epilogue";
            string outFile = Path.Combine(outputDir, $"{label}.md");
            int    beats   = Math.Max(1,
                            outline["chapters"]![0]!["beats"]!.Count() / 2);

            string prompt =
$@"You are {style.AuthorName}.
Write the {label} of this novel (~{targetPer} words).
Include exactly {beats} thematic beats; no sub‑plots.

Begin with the heading: {label.ToUpperInvariant()}.

Premise:
{outline["premiseExpanded"] ?? outline["premise"]}

Genre: {outline["storyGenre"]}

Do not exceed {targetPer + 150} words.";

            string reply = await _llm.CompleteAsync(prompt, modelId, ct);

            int tokens = (int)Math.Round(reply.Split(' ').Length * 4 / 3.0);
            CostLogger.Record(modelId, tokens, tokens);

            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(outFile, reply.Trim(), ct);
            Console.WriteLine($"{label} written: {outFile}");

            var section = new JArray
            {
                new JObject
                {
                    ["number"]  = 0,
                    ["summary"] = "(auto‑generated)",
                    ["beats"]   = new JArray("TBD")
                }
            };
            outline[label] = section;
            File.WriteAllText(outlinePath, outline.ToString());
        }
    }
}
