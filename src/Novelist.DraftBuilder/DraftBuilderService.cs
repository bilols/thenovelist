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
        private static readonly Regex HeadingRx = new(@"^chapter\s+\d+\b.*",
                                                      RegexOptions.IgnoreCase);
        private const int TokenWindow = 1600;  // ~ 1,600 tokens ≈ 1,200 words

        public DraftBuilderService(ILlmClient llm) => _llm = llm;

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
                               out OutlineProgress phase) ||
                phase != OutlineProgress.StructureOutlined)
                throw new InvalidOperationException("Outline must be StructureOutlined.");

            Directory.CreateDirectory(outputDir);

            int totalWords   = outline["totalWordCount"]!.Value<int>();
            int chapterCount = outline["chapterCount"]!.Value<int>();
            int targetPer    = WordBudgetAllocator.InitialTarget(totalWords, chapterCount);

            // ---- style ------------------------------------------------------
            var presetDir  = Path.Combine(Environment.CurrentDirectory, "author_presets");
            var presetFile = outline["famousAuthorPreset"]?.ToString();
            var style = !string.IsNullOrWhiteSpace(presetFile)
                      ? FamousAuthorPresetLoader.Load(presetDir, presetFile)
                      : new DraftStyleOptions("Neutral", 0.45, 14, "", "Neutral", "", "");

            string running = "None yet.";

            for (int ch = startChapter; ch <= Math.Min(chapterCount, endChapter); ch++)
            {
                int actIdx = GetActIndex(outline, ch);
                string draftSoFar = string.Empty;

                for (int pass = 0; pass < 4; pass++)
                {
                    int remainingWords = targetPer - WordCount(draftSoFar);
                    if (remainingWords <= targetPer * 0.05) break; // within ±5%

                    int passTarget = pass == 0
                                   ? (int)Math.Ceiling(targetPer * 0.6)
                                   : remainingWords;

                    string ctx = ChapterContextBuilder.Build(outline, ch, running, style, actIdx);

                    string continuation = pass == 0
                        ? string.Empty
                        : TrimTokens(draftSoFar, TokenWindow);

                    string prompt =
$@"You are {style.AuthorName}.

Write ~{passTarget} new words for Chapter {ch}.
Begin with ""Chapter {ch}"" heading **only if** this is the first text; do not
repeat the heading in continuations.

Requirements:
- Integrate any remaining beats/subplot lines not yet covered.
- Do NOT repeat sentences already written.
- End on a complete sentence.

CONTEXT
{ctx}

ALREADY WRITTEN
{(string.IsNullOrWhiteSpace(continuation) ? "(none)" : continuation)}";

                    string reply = Sanitize(await _llm.CompleteAsync(prompt, modelId, ct));
                    reply = pass == 0 ? EnsureSingleHeading(reply, ch)
                                      : RemoveHeading(reply, ch);

                    if (IsDuplicate(reply, draftSoFar))
                    {
                        // retry once with stricter instruction
                        prompt += "\n\nWARNING: Your previous attempt repeated text.";
                        reply = Sanitize(await _llm.CompleteAsync(prompt, modelId, ct));
                        reply = RemoveHeading(reply, ch);
                    }

                    draftSoFar = string.IsNullOrWhiteSpace(draftSoFar)
                               ? reply.Trim()
                               : $"{draftSoFar.Trim()}\n\n{reply.Trim()}";
                }

                // ensure exactly one heading
                draftSoFar = EnsureSingleHeading(draftSoFar, ch);

                // ---------- save ---------------------------------------------
                string fileName = Path.Combine(outputDir, $"chapter_{ch:D2}.md");
                await File.WriteAllTextAsync(fileName, draftSoFar, ct);
                Console.WriteLine($"Draft written: {fileName} ({WordCount(draftSoFar)} words)");

                running = RunningSummaryHelper.Update(running, draftSoFar);

                int remaining = totalWords - (targetPer * ch);
                targetPer = WordBudgetAllocator.Reallocate(remaining, chapterCount - ch);
            }
        }

        // ---------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------
        private static int WordCount(string s)
            => s.Split(new[] { ' ', '\n', '\r', '\t' },
                       StringSplitOptions.RemoveEmptyEntries).Length;

        private static string TrimTokens(string text, int maxWords)
        {
            var words = text.Split(new[] { ' ', '\n', '\r', '\t' },
                                   StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= maxWords) return text;
            return string.Join(' ', words[^maxWords..]);
        }

        private static bool IsDuplicate(string segment, string existing)
        {
            var tail = TrimTokens(existing, 30);
            return segment.StartsWith(tail, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureSingleHeading(string text, int chapterNum)
        {
            var lines = text.Split('\n').ToList();
            // remove all headings except first
            for (int i = lines.Count - 1; i > 0; i--)
            {
                if (HeadingRx.IsMatch(lines[i]))
                    lines.RemoveAt(i);
            }
            // if first line isn’t heading, prepend one
            if (!HeadingRx.IsMatch(lines[0]))
                lines.Insert(0, $"Chapter {chapterNum}");
            return string.Join('\n', lines).Trim();
        }

        private static string RemoveHeading(string text, int chapterNum)
        {
            var lines = text.Split('\n').ToList();
            if (lines.Count == 0) return text;
            if (HeadingRx.IsMatch(lines[0]))
                lines.RemoveAt(0);
            return string.Join('\n', lines).TrimStart();
        }

        private static string Sanitize(string text)
        {
            var t = text.Trim();
            if (t.StartsWith("```"))
            {
                int first = t.IndexOf('\n');
                int last  = t.LastIndexOf("```", StringComparison.Ordinal);
                if (first >= 0 && last > first)
                    return t.Substring(first + 1, last - first - 1).Trim();
            }
            return t;
        }

        private static int GetActIndex(JObject outline, int chapterNumber)
        {
            int chaptersSeen = 0;
            for (int i = 0; i < outline["storyArc"]!.Count(); i++)
            {
                int actChaps = outline["chapters"]!
                               .Count(c => (int)c["number"]! > chaptersSeen);
                chaptersSeen += actChaps;
                if (chapterNumber <= chaptersSeen) return i;
            }
            return 0;
        }
    }
}
