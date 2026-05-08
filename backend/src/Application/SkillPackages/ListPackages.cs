using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;

namespace SkillRegistry.Application.SkillPackages;

public sealed record ListPackagesQuery(string NamespaceSlug, string? ViewerSubject) : IRequest<IReadOnlyList<PackageSummaryResponse>>;

public sealed record PackageSummaryResponse(
    Guid Id,
    string NamespaceSlug,
    string Slug,
    string Title,
    string? Description,
    DateTime CreatedAtUtc,
    string? LatestVersion,
    bool HasLatest,
    string? CreatedBySubject);

public sealed class ListPackagesQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<ListPackagesQuery, IReadOnlyList<PackageSummaryResponse>>
{
    public async Task<IReadOnlyList<PackageSummaryResponse>> Handle(
        ListPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var nsSlug = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var ns = await persistence.GetNamespaceBySlugForViewerAsync(nsSlug, request.ViewerSubject, cancellationToken)
                 ?? throw new KeyNotFoundException($"Namespace '{request.NamespaceSlug}' was not found.");

        var packages = await persistence.ListPackagesAsync(ns.Id, cancellationToken);
        var results = new List<PackageSummaryResponse>();
        foreach (var p in packages.OrderBy(x => x.Slug))
        {
            var versions = await persistence.ListVersionsAsync(p.Id, cancellationToken);
            var latest = versions.FirstOrDefault(v => v.IsLatest) ?? versions.MaxBy(v => v.PublishedAtUtc);
            results.Add(new PackageSummaryResponse(
                p.Id,
                ns.Slug,
                p.Slug,
                p.Title,
                p.Description,
                p.CreatedAtUtc,
                latest?.Version,
                latest != null,
                p.CreatedBySubject));
        }

        return results;
    }
}
