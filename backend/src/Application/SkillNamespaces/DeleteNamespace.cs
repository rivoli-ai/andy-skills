using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.SkillNamespaces;

public sealed record DeleteNamespaceCommand(string Slug, string ActorSubject) : IRequest;

public sealed class DeleteNamespaceCommandHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<DeleteNamespaceCommand>
{
    public async Task Handle(DeleteNamespaceCommand request, CancellationToken cancellationToken)
    {
        var slug = SlugRules.NormalizeSlug(request.Slug);
        var actor = string.IsNullOrWhiteSpace(request.ActorSubject) ? "anonymous" : request.ActorSubject.Trim();
        var ns = await NamespaceMutationGuard.RequireForNamespaceMutationAsync(persistence, slug, actor, cancellationToken)
            .ConfigureAwait(false);

        persistence.AddAudit(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorSubject = actor,
            Action = "namespace.delete",
            EntityType = nameof(SkillNamespace),
            EntityId = ns.Id.ToString(),
            Detail = slug,
        });

        persistence.RemoveNamespace(ns);
        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
