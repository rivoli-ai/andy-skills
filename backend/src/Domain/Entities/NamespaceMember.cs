using SkillRegistry.Domain.Enums;

namespace SkillRegistry.Domain.Entities;

public sealed class NamespaceMember
{
    public Guid Id { get; set; }
    public Guid NamespaceId { get; set; }
    public SkillNamespace Namespace { get; set; } = null!;
    public string SubjectUserId { get; set; } = "";
    public NamespaceRole Role { get; set; }
}
