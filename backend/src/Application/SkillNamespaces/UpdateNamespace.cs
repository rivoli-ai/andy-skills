using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Domain.Entities;
using SkillRegistry.Domain.Enums;

namespace SkillRegistry.Application.SkillNamespaces;

public sealed record UpdateNamespaceCommand(
    string Slug,
    string DisplayName,
    string? Description,
    NamespaceVisibility Visibility,
    string ActorSubject) : IRequest<NamespaceResponse>;

public sealed class UpdateNamespaceCommandHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<UpdateNamespaceCommand, NamespaceResponse>
{
    public async Task<NamespaceResponse> Handle(UpdateNamespaceCommand request, CancellationToken cancellationToken)
    {
        var slug = SlugRules.NormalizeSlug(request.Slug);
        var actor = string.IsNullOrWhiteSpace(request.ActorSubject) ? "anonymous" : request.ActorSubject.Trim();
        var ns = await NamespaceMutationGuard.RequireForNamespaceMutationAsync(persistence, slug, actor, cancellationToken)
            .ConfigureAwait(false);

        var display = request.DisplayName.Trim();
        if (string.IsNullOrEmpty(display))
            throw new ArgumentException("Display name is required.");

        ns.DisplayName = display;
        ns.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ns.Visibility = request.Visibility;

        persistence.AddAudit(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorSubject = actor,
            Action = "namespace.update",
            EntityType = nameof(SkillNamespace),
            EntityId = ns.Id.ToString(),
            Detail = slug,
        });

        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new NamespaceResponse(
            ns.Id,
            ns.Slug,
            ns.DisplayName,
            ns.Description,
            ns.Visibility.ToString(),
            ns.CreatedAtUtc,
            ns.CreatedBySubject);
    }
}
