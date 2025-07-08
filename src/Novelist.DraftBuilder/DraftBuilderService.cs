using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Novelist.OutlineBuilder;

namespace Novelist.DraftBuilder
{
    public sealed class DraftBuilderService : IDraftBuilder
    {
        private readonly ILlmClient _llm;

        private const int PieceSizeWords  = 350;
        private const int TokenWindow     = 1600;
        private const int MaxPieceRetries = 5;

        private static readonly Regex EndsCleanRx =
            new(@"[.!?][""»”)]?\s*$", RegexOptions.Multiline);

        private static readonly Regex TagRx =
            new(@"^---\s*piece\s+(\d+)\/(\d+)\s*---",
                 RegexOptions.IgnoreCase);

        public DraftBuilderService(ILlmClient llm) => _llm = llm;

        // ---------------------------------------------------------------------
        public async Task BuildDraftAsync(
            string  outlinePath,
            string  outputDir,
            string  modelId,
            int     startChapter = 1,
            int     endChapter   = int.MaxValue,
            CancellationToken ct = default)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));
            if (!Enum.TryParse(outline["outlineProgress"]?.ToString(),
                               out OutlineProgress p) ||
                p != OutlineProgress.StructureOutlined)
                throw new InvalidOperationException("Outline must be StructureOutlined.");

            Directory.CreateDirectory(outputDir);
            CostLogger.Init(Path.Combine(outputDir, "cost_log.csv"));

            int totalWords   = outline["totalWordCount"]!.Value<int>();
            int chapterCount = outline["chapterCount"]!.Value<int>();
            int targetPer    = (int)Math.Round(totalWords / (double)chapterCount,
                                               MidpointRounding.AwayFromZero);

            var style = FamousAuthorPresetLoader.Load(
                Path.Combine(Environment.CurrentDirectory, "author_presets"),
                outline["famousAuthorPreset"]?.ToString() ?? "");

            string runningSummary = "None yet.";

            for (int ch = startChapter; ch <= Math.Min(chapterCount, endChapter); ch++)
            {
                int actIdx     = GetActIndex(outline, ch);
                int pieceCount = (int)Math.Ceiling(targetPer / (double)PieceSizeWords);

                int remainingBeats = outline["chapters"]![ch - 1]!["beats"]!.Count();
                int remainingPlots = outline["chapters"]![ch - 1]!["sub_plots"]!.Count();

                string drafted     = string.Empty;
                string lastAttempt = string.Empty;

                for (int pIdx = 1; pIdx <= pieceCount; pIdx++)
                {
                    int retries = 0;
                    while (retries < MaxPieceRetries)
                    {
                        string prev   = TrimToLastTokens(drafted, TokenWindow);
                        string prompt = BuildPiecePrompt(
                                outline, ch, pIdx, pieceCount,
                                remainingBeats, remainingPlots,
                                runningSummary, style, actIdx, prev, retries);

                        string reply = await _llm.CompleteAsync(prompt, modelId, ct);
                        int tPrompt  = EstimateTokens(prompt);
                        int tReply   = EstimateTokens(reply);
                        CostLogger.Record(modelId, tPrompt, tReply);

                        lastAttempt = reply;
                        string piece = Sanitize(reply);

                        // Tag validation / correction --------------------------
                        if (!TagRx.IsMatch(piece))
                        {
                            retries++; continue;
                        }
                        var m = TagRx.Match(piece);
                        int tagIdx = int.Parse(m.Groups[1].Value);
                        int tagMax = int.Parse(m.Groups[2].Value);

                        if (tagIdx != pIdx || tagMax != pieceCount)
                        {
                            if (retries < 3)
                            { retries++; continue; }         // ask again
                            // after 3 tries, rewrite tag so piece is usable
                            piece = TagRx.Replace(piece,
                                     $"--- piece {pIdx}/{pieceCount} ---", 1);
                        }

                        // length / clean / duplicate --------------------------
                        int words = WordCount(piece);
                        if (words < PieceSizeWords - 100 ||
                            words > PieceSizeWords + 150)
                        { retries++; continue; }

                        if (!EndsCleanRx.IsMatch(piece))
                        { retries++; continue; }

                        if (DuplicateDetector.AreSimilar(piece, drafted))
                        { retries++; continue; }

                        drafted = drafted.Length == 0
                                ? piece.Trim()
                                : $"{drafted.Trim()}\n\n{piece.Trim()}";
                        break;
                    }
                }

                // fallback if all pieces rejected
                if (drafted.Length == 0 && lastAttempt.Length > 0)
                {
                    drafted = Sanitize(lastAttempt).Trim();
                    Console.WriteLine(
                        $"WARNING: accepted fallback draft for chapter {ch}.");
                }

                string fileName = Path.Combine(outputDir, $"chapter_{ch:D2}.md");
                await File.WriteAllTextAsync(fileName, drafted, ct);
                Console.WriteLine($"Draft written: {fileName} ({WordCount(drafted)} words)");

                runningSummary = RunningSummaryHelper.Update(runningSummary, drafted);
            }
        }

        // ---------------------------------------------------------------------
        private static int EstimateTokens(string text)
            => (int)Math.Round(WordCount(text) * 4 / 3.0);

        private static int WordCount(string s)
            => s.Split(new[] { ' ', '\n', '\r', '\t' },
                       StringSplitOptions.RemoveEmptyEntries).Length;

        private static string TrimToLastTokens(string text, int maxWords)
        {
            var words = text.Split(new[] { ' ', '\n', '\r', '\t' },
                                   StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= maxWords) return text;
            return string.Join(' ', words[^maxWords..]);
        }

        private static string BuildPiecePrompt(
            JObject outline, int chapterNum, int pieceIdx, int pieceCount,
            int remainingBeats, int remainingPlots,
            string running, DraftStyleOptions style, int actIdx,
            string prevText, int retryCount)
        {
            string retryMsg = retryCount switch
            {
                0 => "",
                1 => "The tag was wrong. Use the exact tag shown.",
                2 => "Do not skip ahead. Write piece " + pieceIdx + " only.",
                _ => "Final attempt. Any length ≥ 150 words accepted."
            };

            return
$@"You are {style.AuthorName}.

Write ONLY piece {pieceIdx}/{pieceCount} of Chapter {chapterNum}.
Start with:
--- piece {pieceIdx}/{pieceCount} ---

Target about {PieceSizeWords} words.
Remaining beats   : {remainingBeats}
Remaining subplots: {remainingPlots}
{retryMsg}

RUNNING SUMMARY
{running}

PREVIOUS TEXT
{(string.IsNullOrWhiteSpace(prevText) ? "(none)" : prevText)}

--- piece {pieceIdx}/{pieceCount} ---";
        }

        private static string Sanitize(string t)
        {
            var s = t.Trim();
            if (s.StartsWith("```"))
            {
                int i = s.IndexOf('\n');
                int j = s.LastIndexOf("```", StringComparison.Ordinal);
                if (i >= 0 && j > i)
                    s = s.Substring(i + 1, j - i - 1).Trim();
            }
            return s;
        }

        private static int GetActIndex(JObject outline, int chapterNumber)
        {
            int seen = 0;
            for (int i = 0; i < outline["storyArc"]!.Count(); i++)
            {
                int actChaps = outline["chapters"]!
                               .Count(c => (int)c["number"]! > seen);
                seen += actChaps;
                if (chapterNumber <= seen) return i;
            }
            return 0;
        }
    }
}
