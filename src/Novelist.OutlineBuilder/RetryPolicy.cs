using System;
using System.Collections.Generic;

namespace Novelist.OutlineBuilder;

public static class RetryPolicy
{
    private static readonly IReadOnlyDictionary<string, int> Defaults = new Dictionary<string, int>
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

    public static int GetMaxRetries(string modelId)
    {
        var envVar = Environment.GetEnvironmentVariable($"NOVELIST_MAX_RETRIES_{modelId}");
        if (int.TryParse(envVar, out var fromEnv) && fromEnv > 0) return fromEnv;

        return Defaults.TryGetValue(modelId, out var d) ? d : 3;
    }
}
