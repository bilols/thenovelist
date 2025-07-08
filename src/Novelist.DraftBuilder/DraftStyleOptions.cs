namespace Novelist.DraftBuilder
{
    /// <summary>
    /// Voice, tone, and stylistic knobs derived from a famous-author preset.
    /// </summary>
    public sealed record DraftStyleOptions(
        string Voice,
        double LexicalDensity,
        int    SentenceLength,
        string Hallmarks,
        string PreferredTone,
        string ForbiddenElements,
        string AuthorName);
}
