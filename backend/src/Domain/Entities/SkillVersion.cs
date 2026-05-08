namespace SkillRegistry.Domain.Entities;

public sealed class SkillVersion
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public SkillPackage Package { get; set; } = null!;
    public string Version { get; set; } = "";
    public string? Tag { get; set; }
    public bool IsLatest { get; set; }
    public string ArtifactUri { get; set; } = "";
    /// <summary>When set, the ZIP is served from the registry DB via the install URL; otherwise clients follow <see cref="ArtifactUri"/> if it is HTTP(S).</summary>
    public byte[]? PackageZip { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public string? PublishedBySubject { get; set; }
}
