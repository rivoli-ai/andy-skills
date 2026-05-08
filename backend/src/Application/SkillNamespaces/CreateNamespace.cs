using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Domain.Entities;
using SkillRegistry.Domain.Enums;

namespace SkillRegistry.Application.SkillNamespaces;

public sealed record CreateNamespaceCommand(
    string Slug,
    string DisplayName,
    string? Description,
    NamespaceVisibility Visibility,
    string ActorSubject) : IRequest<NamespaceResponse>;

public sealed class CreateNamespaceCommandHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<CreateNamespaceCommand, NamespaceResponse>
{
    public async Task<NamespaceResponse> Handle(CreateNamespaceCommand request, CancellationToken cancellationToken)
    {
        var slug = SlugRules.NormalizeSlug(request.Slug);
        if (!SlugRules.IsValidSlug(slug))
            throw new ArgumentException("Invalid slug (use 2–64 chars: a-z, 0-9, hyphen).");

        if (await persistence.NamespaceSlugExistsAsync(slug, cancellationToken))
            throw new InvalidOperationException($"Namespace '{slug}' already exists.");

        var actor = string.IsNullOrWhiteSpace(request.ActorSubject) ? "anonymous" : request.ActorSubject.Trim();

        var ns = new SkillNamespace
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            DisplayName = request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Visibility = request.Visibility,
            CreatedAtUtc = DateTime.UtcNow,
        };

        ns.Members.Add(new NamespaceMember
        {
            Id = Guid.NewGuid(),
            Namespace = ns,
            SubjectUserId = actor,
            Role = NamespaceRole.Owner,
        });

        persistence.AddNamespace(ns);
        persistence.AddAudit(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorSubject = actor,
            Action = "namespace.create",
            EntityType = nameof(SkillNamespace),
            EntityId = ns.Id.ToString(),
            Detail = slug,
        });

        await persistence.SaveChangesAsync(cancellationToken);

        return new NamespaceResponse(
            ns.Id,
            ns.Slug,
            ns.DisplayName,
            ns.Description,
            ns.Visibility.ToString(),
            ns.CreatedAtUtc);
    }
}
