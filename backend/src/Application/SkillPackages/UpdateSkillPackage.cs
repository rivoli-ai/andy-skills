using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.SkillPackages;

public sealed record UpdateSkillPackageCommand(
    string NamespaceSlug,
    string SkillSlug,
    string Title,
    string? Description,
    string ActorSubject) : IRequest<PackageSummaryResponse>;

public sealed class UpdateSkillPackageCommandHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<UpdateSkillPackageCommand, PackageSummaryResponse>
{
    public async Task<PackageSummaryResponse> Handle(UpdateSkillPackageCommand request, CancellationToken cancellationToken)
    {
        var nsSlug = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var skillSlug = SlugRules.NormalizeSlug(request.SkillSlug);
        var actor = string.IsNullOrWhiteSpace(request.ActorSubject) ? "anonymous" : request.ActorSubject.Trim();

        var ns = await NamespaceMutationGuard.RequireForSkillMutationAsync(persistence, nsSlug, actor, cancellationToken)
            .ConfigureAwait(false);

        var pkg = await persistence.GetPackageTrackedAsync(ns.Id, skillSlug, cancellationToken).ConfigureAwait(false)
                  ?? throw new KeyNotFoundException($"Skill '{skillSlug}' was not found in namespace '{nsSlug}'.");

        var title = request.Title.Trim();
        if (string.IsNullOrEmpty(title))
            throw new ArgumentException("Title is required.");

        pkg.Title = title;
        pkg.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        persistence.AddAudit(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorSubject = actor,
            Action = "skill.update",
            EntityType = nameof(SkillPackage),
            EntityId = pkg.Id.ToString(),
            Detail = $"{ns.Slug}/{skillSlug}",
        });

        await persistence.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var versions = await persistence.ListVersionsAsync(pkg.Id, cancellationToken).ConfigureAwait(false);
        var latest = versions.FirstOrDefault(v => v.IsLatest) ?? versions.MaxBy(v => v.PublishedAtUtc);

        return new PackageSummaryResponse(
            pkg.Id,
            ns.Slug,
            pkg.Slug,
            pkg.Title,
            pkg.Description,
            pkg.CreatedAtUtc,
            latest?.Version,
            latest != null,
            pkg.CreatedBySubject);
    }
}
