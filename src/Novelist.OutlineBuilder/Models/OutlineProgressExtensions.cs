namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Convenience helpers for comparing outline-progress phases.
    /// </summary>
    public static class OutlineProgressExtensions
    {
        /// <summary>
        /// Returns <c>true</c> when the current phase is
        /// equal to or comes after the <paramref name="target"/> phase
        /// in the pipeline ordering.
        /// </summary>
        public static bool AtLeast(this OutlineProgress current,
                                   OutlineProgress target) =>
            current >= target;

        /// <summary>
        /// Returns <c>true</c> when the current phase precedes
        /// the <paramref name="target"/> phase.
        /// </summary>
        public static bool IsBefore(this OutlineProgress current,
                                    OutlineProgress target) =>
            current < target;

        // --------------------------------------------------------------------
        // Semantic helpers for commonly checked milestones
        // --------------------------------------------------------------------

        /// <summary>
        /// True once the character roster has been generated.
        /// </summary>
        public static bool IsCharactersOutlinedOrLater(this OutlineProgress p) =>
            p >= OutlineProgress.CharactersOutlined;

        /// <summary>
        /// True once the chapter structure (scenes / beats) has been generated.
        /// </summary>
        public static bool IsStructureOutlinedOrLater(this OutlineProgress p) =>
            p >= OutlineProgress.StructureOutlined;
    }
}
