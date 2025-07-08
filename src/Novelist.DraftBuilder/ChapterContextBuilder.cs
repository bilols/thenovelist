using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Novelist.DraftBuilder
{
    internal static class ChapterContextBuilder
    {
        public static string Build(
            JObject           outline,
            int               chapterNumber,
            string            runningSummary,
            DraftStyleOptions style,
            int               actIndex)
        {
            var chapter = outline["chapters"]![chapterNumber - 1]!;
            var beats   = chapter["beats"]!.Select(b => "- " + b).ToArray();
            var plots   = chapter["sub_plots"]!.Select(p => "- " + p).ToArray();

            return
$@"GLOBAL
  Genre: {outline["storyGenre"]}
  Core theme: {outline["themes"]?.FirstOrDefault() ?? "N/A"}
  Act index: {actIndex + 1} of {outline["storyArc"]!.Count()}

STYLE
  Voice: {style.Voice}
  Tone : {style.PreferredTone}
  Hallmarks to hint: {style.Hallmarks}

RUNNING SUMMARY (≤120 words)
{runningSummary}

CHAPTER {chapterNumber}
Summary :
{chapter["summary"]}
Beats    :
{string.Join("\n", beats)}
Sub-plots:
{string.Join("\n", plots)}";
        }
    }
}
