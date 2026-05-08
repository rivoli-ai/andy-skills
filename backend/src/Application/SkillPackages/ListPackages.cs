using MediatR;
using SkillRegistry.Application.Abstractions;

namespace SkillRegistry.Application.SkillPackages;

public sealed record ListPackagesQuery(string NamespaceSlug) : IRequest<IReadOnlyList<PackageSummaryResponse>>;

public sealed record PackageSummaryResponse(
    Guid Id,
    string NamespaceSlug,
    string Slug,
    string Title,
    string? Description,
    DateTime CreatedAtUtc,
    string? LatestVersion,
    bool HasLatest);

public sealed class ListPackagesQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<ListPackagesQuery, IReadOnlyList<PackageSummaryResponse>>
{
    public async Task<IReadOnlyList<PackageSummaryResponse>> Handle(
        ListPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var ns = await persistence.GetNamespaceBySlugAsync(request.NamespaceSlug, cancellationToken)
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
                latest != null));
        }

        return results;
    }
}
