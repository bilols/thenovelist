using System.Threading;
using System.Threading.Tasks;

namespace Novelist.DraftBuilder
{
    /// <summary>
    /// Public surface for generating chapter drafts from a completed outline.
    /// </summary>
    public interface IDraftBuilder
    {
        Task BuildDraftAsync(
            string            outlinePath,
            string            outputDir,
            string            modelId,
            int               startChapter = 1,
            int               endChapter   = int.MaxValue,
            CancellationToken ct           = default);
    }
}
