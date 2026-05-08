using SkillRegistry.Domain.Enums;

namespace SkillRegistry.Domain.Entities;

public sealed class SkillNamespace
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public NamespaceVisibility Visibility { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Actor identifier (email or subject id) when the namespace was created.</summary>
    public string? CreatedBySubject { get; set; }

    public ICollection<NamespaceMember> Members { get; set; } = new List<NamespaceMember>();
    public ICollection<SkillPackage> Packages { get; set; } = new List<SkillPackage>();
}
