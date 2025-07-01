using System.Text.RegularExpressions;

namespace Novelist.OutlineBuilder;

/// <summary>
/// Utility to create URL‑ and file‑safe slugs from arbitrary text.
/// </summary>
public static class SlugHelper
{
    private static readonly Regex NonAlphanumeric =
        new("[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "untitled";

        var slug = NonAlphanumeric.Replace(input.Trim().ToLowerInvariant(), "-");
        slug = slug.Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "untitled" : slug;
    }
}
