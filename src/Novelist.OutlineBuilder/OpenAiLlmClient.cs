using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Minimal wrapper around the OpenAI REST chat‑completion endpoint.
    /// Streaming is not yet implemented.
    /// </summary>
    public sealed class OpenAiLlmClient : ILlmClient, IDisposable
    {
        private const string DefaultEndpoint =
            "https://api.openai.com/v1/chat/completions";

        private readonly HttpClient _http;

        public OpenAiLlmClient(HttpClient? http = null)
        {
            _http = http ?? new HttpClient();

            var key = Environment.GetEnvironmentVariable("OPENAI_KEY")?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException(
                    "Set the OPENAI_KEY environment variable with your API key.");

            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
        }

        public async Task<string> CompleteAsync(
            string prompt,
            string model,
            CancellationToken ct = default)
        {
            var body = new
            {
                model,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.7,
                top_p = 0.95
            };

            var endpoint =
                Environment.GetEnvironmentVariable("OPENAI_BASE") ??
                DefaultEndpoint;

            using var resp = await _http.PostAsJsonAsync(endpoint, body,
                JsonSerializerOptions.Default, ct);

            resp.EnsureSuccessStatusCode();

            var doc = await JsonDocument.ParseAsync(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            var content = doc.RootElement
                             .GetProperty("choices")[0]
                             .GetProperty("message")
                             .GetProperty("content")
                             .GetString();

            return content ?? string.Empty;
        }

        public void Dispose() => _http.Dispose();
    }
}
