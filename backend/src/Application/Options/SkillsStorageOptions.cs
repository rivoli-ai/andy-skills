namespace SkillRegistry.Application.Options;

public sealed class SkillsStorageOptions
{
    public const string SectionName = "Skills";

    /// <summary>Public origin users/agents use for install URLs (no trailing slash).</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5289";
}
