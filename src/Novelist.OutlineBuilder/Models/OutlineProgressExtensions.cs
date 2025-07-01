using System.Globalization;

namespace Novelist.OutlineBuilder.Models
{
    /// <summary>
    /// Helper for UI‑friendly, localisable labels.
    /// </summary>
    public static class OutlineProgressExtensions
    {
        private static readonly IReadOnlyDictionary<OutlineProgress, string> DefaultLabels =
            new Dictionary<OutlineProgress, string>
            {
                { OutlineProgress.Init,               "Initialised" },
                { OutlineProgress.PremiseExpanded,    "Premise expanded" },
                { OutlineProgress.ArcDefined,         "Story arc defined" },
                { OutlineProgress.CharactersOutlined, "Characters outlined" },
                { OutlineProgress.ChaptersSketched,   "Chapters sketched" },
                { OutlineProgress.BeatsDetailed,      "Beats detailed" },
                { OutlineProgress.Finalized,          "Outline finalised" }
            };

        /// <summary>Returns a display string; swap with IStringLocalizer later.</summary>
        public static string ToDisplayString(this OutlineProgress value, CultureInfo? culture = null) =>
            DefaultLabels[value];
    }
}
