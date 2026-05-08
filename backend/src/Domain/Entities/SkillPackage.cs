namespace SkillRegistry.Domain.Entities;

public sealed class SkillPackage
{
    public Guid Id { get; set; }
    public Guid NamespaceId { get; set; }
    public SkillNamespace Namespace { get; set; } = null!;
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Resolved actor when this skill package was created.</summary>
    public string? CreatedBySubject { get; set; }

    public ICollection<SkillVersion> Versions { get; set; } = new List<SkillVersion>();
}
