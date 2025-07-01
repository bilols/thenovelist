using System;
using System.Threading;
using System.Threading.Tasks;
using Novelist.OutlineBuilder;

namespace Novelist.Tests;

/// <summary>
/// Returns deterministic, schema‑compliant text.
/// Detects whether the prompt is asking for a multi‑act structure or an expanded premise.
/// </summary>
public sealed class StubLlmClient : ILlmClient
{
    public Task<string> CompleteAsync(string prompt, string modelId, CancellationToken ct = default)
    {
        // Heuristic: arc prompts contain "create a clear" or "multi-act"
        var isArcRequest = prompt.Contains("create a clear", StringComparison.OrdinalIgnoreCase)
                        || prompt.Contains("multi-act",     StringComparison.OrdinalIgnoreCase);

        if (isArcRequest)
        {
            // Three paragraphs >150 chars total, separated by blank lines
            return Task.FromResult(
                "Act 1: Setup paragraph with sufficient length to exceed the validation threshold, introducing protagonists, setting, and initial tension that hints at deeper conflict and stakes for later acts.\n\n" +
                "Act 2: Confrontation paragraph where complications escalate, alliances shift, and the protagonist faces mounting obstacles that test core beliefs and push the narrative toward a decisive crisis point.\n\n" +
                "Act 3: Resolution paragraph delivering climax and aftermath, resolving central conflict, transforming characters, and leaving a poignant thematic echo that underscores the story’s core message.");
        }

        // Premise expansion → one paragraph >150 chars and contains sentinel text
        return Task.FromResult(
            "Expanded premise from StubLlmClient. " +
            "This deliberately verbose paragraph exceeds one hundred fifty characters to satisfy schema requirements while providing a coherent, if generic, elaboration on tone, stakes, protagonist motivation, and thematic resonance for the test cases.");
    }
}
