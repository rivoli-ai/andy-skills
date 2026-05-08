namespace SkillRegistry.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string ActorSubject { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string? Detail { get; set; }
}
