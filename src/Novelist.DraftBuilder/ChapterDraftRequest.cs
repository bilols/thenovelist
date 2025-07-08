using Newtonsoft.Json.Linq;

namespace Novelist.DraftBuilder
{
    /// <summary>
    /// Immutable payload passed to the LLM for a single draft pass.
    /// </summary>
    public sealed record ChapterDraftRequest(
        int               ChapterNumber,
        int               TargetWords,
        string            RunningSummary,
        JArray            Beats,
        JArray            SubPlots,
        string            ChapterSummary,
        DraftStyleOptions Style);
}
