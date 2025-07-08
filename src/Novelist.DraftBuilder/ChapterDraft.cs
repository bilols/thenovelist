namespace Novelist.DraftBuilder
{
    /// <summary>
    /// Result of a single multi-pass session for a chapter.
    /// </summary>
    public sealed record ChapterDraft(
        int    ChapterNumber,
        int    WordsGenerated,
        string Content,
        string NewRunningSummary);
}
