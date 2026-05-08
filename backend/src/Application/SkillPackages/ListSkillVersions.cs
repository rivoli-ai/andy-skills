using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;

namespace SkillRegistry.Application.SkillPackages;

public sealed record ListSkillVersionsQuery(string NamespaceSlug, string SkillSlug, string? ViewerSubject)
    : IRequest<IReadOnlyList<SkillVersionResponse>>;

public sealed class ListSkillVersionsQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<ListSkillVersionsQuery, IReadOnlyList<SkillVersionResponse>>
{
    public async Task<IReadOnlyList<SkillVersionResponse>> Handle(
        ListSkillVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var nsSlug = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var ns = await persistence.GetNamespaceBySlugForViewerAsync(nsSlug, request.ViewerSubject, cancellationToken)
                 ?? throw new KeyNotFoundException($"Namespace '{nsSlug}' was not found.");

        var skillSlug = SlugRules.NormalizeSlug(request.SkillSlug);
        var pkg = await persistence.GetPackageAsync(ns.Id, skillSlug, cancellationToken)
                  ?? throw new KeyNotFoundException($"Skill '{skillSlug}' was not found.");

        var rows = await persistence.ListVersionsAsync(pkg.Id, cancellationToken).ConfigureAwait(false);
        return rows
            .OrderByDescending(v => v.PublishedAtUtc)
            .Select(v => new SkillVersionResponse(
                v.Id,
                v.PackageId,
                v.Version,
                v.Tag,
                v.IsLatest,
                v.ArtifactUri,
                v.PublishedAtUtc,
                v.HasStoredZip,
                v.RemoteFetchRequiresPat))
            .ToList();
    }
}
