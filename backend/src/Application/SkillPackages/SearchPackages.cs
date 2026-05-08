using MediatR;
using SkillRegistry.Application.Abstractions;

namespace SkillRegistry.Application.SkillPackages;

public sealed record SearchPackagesQuery(string? Query) : IRequest<IReadOnlyList<PackageSearchHitResponse>>;

public sealed record PackageSearchHitResponse(
    string NamespaceSlug,
    string SkillSlug,
    string Title,
    string? Description,
    DateTime CreatedAtUtc);

public sealed class SearchPackagesQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<SearchPackagesQuery, IReadOnlyList<PackageSearchHitResponse>>
{
    public async Task<IReadOnlyList<PackageSearchHitResponse>> Handle(
        SearchPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var packages = await persistence.SearchPackagesAsync(request.Query, cancellationToken);
        return packages
            .Select(p => new PackageSearchHitResponse(
                p.Namespace.Slug,
                p.Slug,
                p.Title,
                p.Description,
                p.CreatedAtUtc))
            .ToList();
    }
}
