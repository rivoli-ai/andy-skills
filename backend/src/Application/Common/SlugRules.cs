namespace SkillRegistry.Application.Common;

public static class SlugRules
{
    /// <summary>Lowercase slug: letters, digits, hyphens; must not start/end with hyphen.</summary>
    public static bool IsValidSlug(string slug) =>
        slug.Length is >= 2 and <= 64
        && slug.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
        && !slug.StartsWith('-')
        && !slug.EndsWith('-');

    public static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();
}
