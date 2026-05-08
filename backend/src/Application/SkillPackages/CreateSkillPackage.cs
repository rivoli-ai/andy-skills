using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.SkillPackages;

public sealed record CreateSkillPackageCommand(
    string NamespaceSlug,
    string Slug,
    string Title,
    string? Description,
    string ActorSubject) : IRequest<PackageSummaryResponse>;

public sealed class CreateSkillPackageCommandHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<CreateSkillPackageCommand, PackageSummaryResponse>
{
    public async Task<PackageSummaryResponse> Handle(CreateSkillPackageCommand request, CancellationToken cancellationToken)
    {
        var actor = string.IsNullOrWhiteSpace(request.ActorSubject) ? "anonymous" : request.ActorSubject.Trim();
        var nsSlug = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var ns = await NamespaceMutationGuard.RequireForSkillMutationAsync(persistence, nsSlug, actor, cancellationToken)
            .ConfigureAwait(false);

        var slug = SlugRules.NormalizeSlug(request.Slug);
        if (!SlugRules.IsValidSlug(slug))
            throw new ArgumentException("Invalid skill slug.");

        if (await persistence.GetPackageAsync(ns.Id, slug, cancellationToken) != null)
            throw new InvalidOperationException($"Skill '{slug}' already exists in this namespace.");

        var pkg = new SkillPackage
        {
            Id = Guid.NewGuid(),
            NamespaceId = ns.Id,
            Slug = slug,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBySubject = actor,
        };

        persistence.AddPackage(pkg);

        persistence.AddAudit(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorSubject = actor,
            Action = "skill.create",
            EntityType = nameof(SkillPackage),
            EntityId = pkg.Id.ToString(),
            Detail = $"{ns.Slug}/{slug}",
        });

        await persistence.SaveChangesAsync(cancellationToken);

        return new PackageSummaryResponse(
            pkg.Id,
            ns.Slug,
            pkg.Slug,
            pkg.Title,
            pkg.Description,
            pkg.CreatedAtUtc,
            null,
            false,
            pkg.CreatedBySubject);
    }
}
