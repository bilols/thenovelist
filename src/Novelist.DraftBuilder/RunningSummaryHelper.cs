using System;

namespace Novelist.DraftBuilder
{
    /// <summary>
    /// Maintains a rolling 120-word summary that is passed to each draft request.
    /// </summary>
    internal static class RunningSummaryHelper
    {
        /// <summary>
        /// Concatenates the previous running summary with the new chapter text
        /// and returns the last 120 words (or fewer if total words are < 120).
        /// </summary>
        public static string Update(string previous, string chapterContent)
        {
            string combined = string.Concat(previous, " ", chapterContent);
            var words = combined.Split(
                new[] { ' ', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            if (words.Length <= 120)
                return string.Join(' ', words);

            return string.Join(' ', words[^120..]);
        }
    }
}
