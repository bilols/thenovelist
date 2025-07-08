using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Novelist.DraftBuilder
{
    /// <summary>
    /// Thread‑safe cost tracker that appends one CSV row per LLM call.
    /// </summary>
    internal static class CostLogger
    {
        private static readonly object _lock = new();
        private static long _promptTokens, _completionTokens;
        private static double _usdTotal;
        private static string _csvPath = string.Empty;

        /// <summary>
        /// Sets the target CSV file. Must be called once before Record().
        /// </summary>
        public static void Init(string csvPath)
        {
            _csvPath = csvPath;
            if (!File.Exists(csvPath))
                File.AppendAllText(csvPath, "utc_timestamp,model,prompt_tokens,completion_tokens,usd_cost\r\n");
        }

        /// <summary>
        /// Record a single LLM call.
        /// </summary>
        public static void Record(string model, int promptTokens, int completionTokens)
        {
            // --- simple approximate pricing (USD per 1K tokens) --------------
            double rate = model.Contains("gpt-4") ? 0.01 : 0.0015; // adjust later
            double usd  = (promptTokens + completionTokens) / 1000.0 * rate;

            lock (_lock)
            {
                _promptTokens     += promptTokens;
                _completionTokens += completionTokens;
                _usdTotal         += usd;

                if (!string.IsNullOrEmpty(_csvPath))
                {
                    string row = string.Join(',',
                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        model,
                        promptTokens,
                        completionTokens,
                        usd.ToString("F4", CultureInfo.InvariantCulture));
                    File.AppendAllText(_csvPath, row + "\r\n");
                }
            }
        }

        public static (long prompt, long completion, double usd) Totals()
        {
            lock (_lock)
                return (_promptTokens, _completionTokens, _usdTotal);
        }
    }
}
