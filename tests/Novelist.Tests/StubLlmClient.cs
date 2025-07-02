using System;
using System.Threading;
using System.Threading.Tasks;
using Novelist.OutlineBuilder;

namespace Novelist.Tests
{
    public sealed class StubLlmClient : ILlmClient
    {
        public Task<string> CompleteAsync(
            string prompt, string model, CancellationToken ct = default)
        {
            // Structure request
            if (prompt.Contains("\"beats\"", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(
                    "[" +
                    "{\"number\":1,\"summary\":\"Opens with tension.\"," +
                    "\"beats\":[\"Jack arrives\",\"Odd door\",\"Warning\"]," +
                    "\"themes\":[\"isolation\"]}," +
                    "{\"number\":2,\"summary\":\"Escalation.\"," +
                    "\"beats\":[\"Nightmare\",\"Keys missing\",\"Light flicker\"]}" +
                    "]");
            }

            // Characters request
            if (prompt.Contains("JSON array", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(
                    "[{\"name\":\"Jack Carpenter\",\"role\":\"Protagonist\"," +
                    "\"traits\":[\"haunted\"],\"arc\":\"Faces past.\"}]");
            }

            // Premise / arc fallback: include literal "StubLlmClient" for tests
            return Task.FromResult(
                "StubLlmClient expanded premise text exceeding 150 characters " +
                "to satisfy validation length. Lorem ipsum dolor sit amet, " +
                "consectetur adipiscing elit, sed do eiusmod tempor incididunt.");
        }
    }
}
