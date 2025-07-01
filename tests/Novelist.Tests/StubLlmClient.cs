using System;
using System.Threading;
using System.Threading.Tasks;
using Novelist.OutlineBuilder;

namespace Novelist.Tests;

public sealed class StubLlmClient : ILlmClient
{
    public Task<string> CompleteAsync(string prompt, string model, CancellationToken ct = default)
    {
        if (prompt.Contains("JSON array", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                "[{\"name\":\"Jack Carpenter\",\"role\":\"Protagonist\",\"traits\":[\"haunted\",\"skilled\"],\"arc\":\"Confronts past tragedy to find redemption.\"}," +
                " {\"name\":\"Art Monroe\",\"role\":\"Supporting\",\"traits\":[\"eccentric\"],\"arc\":\"Learns to protect what matters.\"}]");
        }

        if (prompt.Contains("multi-act", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("create a clear", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                "Act 1: Setup paragraph with ample length for schema.\n\n" +
                "Act 2: Conflict escalates over several sentences.\n\n" +
                "Act 3: Resolution and fallout adequately described.");
        }

        return Task.FromResult(
            "Expanded premise from StubLlmClient. This paragraph intentionally exceeds 150 characters to satisfy validation requirements, describing tone, stakes, protagonist motivation, and thematic undercurrents for the test.");
    }
}
