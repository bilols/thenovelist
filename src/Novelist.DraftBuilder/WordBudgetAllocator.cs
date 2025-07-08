using System;

namespace Novelist.DraftBuilder
{
    /// <summary>
    /// Allocates and adjusts word budgets across chapters.
    /// </summary>
    internal static class WordBudgetAllocator
    {
        public static int InitialTarget(int totalWords, int chapterCount)
        {
            if (chapterCount <= 0) throw new ArgumentOutOfRangeException(nameof(chapterCount));
            return (int)Math.Round((double)totalWords / chapterCount, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Simple roll-forward allocator: if a chapter finishes under budget,
        /// leftover words return to the pool.
        /// </summary>
        public static int Reallocate(int remainingWords, int chaptersLeft)
            => InitialTarget(remainingWords, chaptersLeft);
    }
}
