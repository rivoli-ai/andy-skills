using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.SkillPackages;

public sealed record DeleteSkillPackageCommand(string NamespaceSlug, string SkillSlug, string ActorSubject) : IRequest;

public sealed class DeleteSkillPackageCommandHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<DeleteSkillPackageCommand>
{
    public async Task Handle(DeleteSkillPackageCommand request, CancellationToken cancellationToken)
    {
        var nsSlug = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var skillSlug = SlugRules.NormalizeSlug(request.SkillSlug);
        var actor = string.IsNullOrWhiteSpace(request.ActorSubject) ? "anonymous" : request.ActorSubject.Trim();

        var ns = await NamespaceMutationGuard.RequireForSkillMutationAsync(persistence, nsSlug, actor, cancellationToken)
            .ConfigureAwait(false);

        var pkg = await persistence.GetPackageTrackedAsync(ns.Id, skillSlug, cancellationToken).ConfigureAwait(false)
                  ?? throw new KeyNotFoundException($"Skill '{skillSlug}' was not found in namespace '{nsSlug}'.");

        persistence.AddAudit(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorSubject = actor,
            Action = "skill.delete",
            EntityType = nameof(SkillPackage),
            EntityId = pkg.Id.ToString(),
            Detail = $"{ns.Slug}/{skillSlug}",
        });

        persistence.RemovePackage(pkg);
        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
