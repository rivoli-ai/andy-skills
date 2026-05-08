using MediatR;
using SkillRegistry.Application.Abstractions;

namespace SkillRegistry.Application.SkillPackages;

public sealed record SearchPackagesQuery(string? Query, string? ViewerSubject) : IRequest<IReadOnlyList<PackageSearchHitResponse>>;

public sealed record PackageSearchHitResponse(
    string NamespaceSlug,
    string SkillSlug,
    string Title,
    string? Description,
    DateTime CreatedAtUtc,
    string? CreatedBySubject);

public sealed class SearchPackagesQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<SearchPackagesQuery, IReadOnlyList<PackageSearchHitResponse>>
{
    public async Task<IReadOnlyList<PackageSearchHitResponse>> Handle(
        SearchPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var packages = await persistence.SearchPackagesAsync(request.Query, request.ViewerSubject, cancellationToken);
        return packages
            .Select(p => new PackageSearchHitResponse(
                p.Namespace.Slug,
                p.Slug,
                p.Title,
                p.Description,
                p.CreatedAtUtc,
                p.CreatedBySubject))
            .ToList();
    }
}
