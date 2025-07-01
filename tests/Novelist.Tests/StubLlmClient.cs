using System.Threading;
using System.Threading.Tasks;
using Novelist.OutlineBuilder;

namespace Novelist.Tests;

/// <summary>
/// Returns deterministic text for unit tests.
/// </summary>
public sealed class StubLlmClient : ILlmClient
{
    public Task<string> CompleteAsync(string prompt, string modelId, CancellationToken ct = default) =>
        Task.FromResult("Expanded premise from StubLlmClient.");
}
