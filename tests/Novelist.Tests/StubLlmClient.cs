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
            if (prompt.StartsWith("You are an elite character", StringComparison.Ordinal))
            {
                // 1 protagonist + 2 supporting + 2 minor = 5
                return Task.FromResult(
                    "[" +
                    "{\"name\":\"Jack Carpenter\",\"role\":\"Protagonist: Haunted carpenter\",\"traits\":[\"skilled\",\"anxious\"],\"arc\":\"Faces past.\"}," +
                    "{\"name\":\"Art Monroe\",\"role\":\"Supporting character: Building manager\",\"traits\":[\"eccentric\"],\"arc\":\"Learns courage.\"}," +
                    "{\"name\":\"Grace Ellis\",\"role\":\"Supporting character: Nurse\",\"traits\":[\"compassionate\"],\"arc\":\"Rediscovers faith.\"}," +
                    "{\"name\":\"Tom Reed\",\"role\":\"Minor character: Elderly tenant\",\"traits\":[\"nosy\"],\"arc\":\"Finds closure.\"}," +
                    "{\"name\":\"Mrs. Patel\",\"role\":\"Minor character: Neighbor\",\"traits\":[\"kind\"],\"arc\":\"Offers wisdom.\"}" +
                    "]");
            }

            if (prompt.Contains("\"beats\"", StringComparison.OrdinalIgnoreCase))
            {
                // Structure request
                return Task.FromResult(
                    "[" +
                    "{\"number\":1,\"summary\":\"Opens with tension.\"," +
                    "\"beats\":[\"Jack arrives\",\"Odd door\",\"Warning\"]," +
                    "\"themes\":[\"isolation\"]}," +
                    "{\"number\":2,\"summary\":\"Escalation.\"," +
                    "\"beats\":[\"Nightmare\",\"Keys missing\",\"Light flicker\"]}" +
                    "]");
            }

            // Premise / arc fallback
            return Task.FromResult(
                "StubLlmClient expanded premise text exceeding 150 characters to" +
                " satisfy validation length. Lorem ipsum dolor sit amet, consectetur" +
                " adipiscing elit, sed do eiusmod tempor incididunt ut labore.");
        }
    }
}
