using System.Collections.Generic;

namespace Novelist.OutlineBuilder;

/// <summary>
/// Central location for model‑specific retry counts. Can be replaced by config later.
/// </summary>
public static class RetryPolicy
{
    private static readonly IReadOnlyDictionary<string, int> Map = new Dictionary<string, int>
    {
        ["gpt-3.5-turbo"]        = 6,
        ["gpt-3.5-turbo-16k"]    = 6,
        ["gpt-4"]                = 4,
        ["gpt-4o-mini"]          = 6,
        ["gpt-4o"]               = 4,
        ["gpt-4.1"]              = 4,
        ["gpt-4.1-mini"]         = 6,
        ["gpt-4.1-nano"]         = 6,
        ["gpt-4-turbo"]          = 6,
        ["gpt-4.5-preview"]      = 4
    };

    /// <summary>Returns model‑specific retry count or a safe default (3).</summary>
    public static int GetMaxRetries(string modelId) => Map.TryGetValue(modelId, out var v) ? v : 3;
}
