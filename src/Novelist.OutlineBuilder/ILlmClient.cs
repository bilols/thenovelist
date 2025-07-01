using System.Threading;
using System.Threading.Tasks;

namespace Novelist.OutlineBuilder;

/// <summary>
/// Minimal abstraction for a Large Language Model client.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Completes the prompt and returns raw text.
    /// </summary>
    Task<string> CompleteAsync(string prompt, string modelId, CancellationToken ct = default);
}
