using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;

namespace SkillRegistry.Application.SkillPackages;

public sealed record GetSkillZipTreeQuery(string NamespaceSlug, string SkillSlug, string Version, string? ViewerSubject)
    : IRequest<SkillZipTreeResult>;

public abstract record SkillZipTreeResult;

public sealed record SkillZipTreeFound(IReadOnlyList<string> Paths) : SkillZipTreeResult;

public sealed record SkillZipTreeNotFound(string Reason) : SkillZipTreeResult;

public sealed class GetSkillZipTreeQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<GetSkillZipTreeQuery, SkillZipTreeResult>
{
    public async Task<SkillZipTreeResult> Handle(GetSkillZipTreeQuery request, CancellationToken cancellationToken)
    {
        var ns = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var skill = SlugRules.NormalizeSlug(request.SkillSlug);
        var ver = request.Version.Trim();
        if (string.IsNullOrEmpty(ver))
            return new SkillZipTreeNotFound("Version is required.");

        var entity = await persistence
            .GetVersionBySlugTripleAsync(ns, skill, ver, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            return new SkillZipTreeNotFound("Version not found.");

        if (!NamespaceAccess.CanView(entity.Package.Namespace, request.ViewerSubject))
            return new SkillZipTreeNotFound("Version not found.");

        if (entity.PackageZip is not { Length: > 0 })
            return new SkillZipTreeNotFound(
                "This version has no ZIP in the registry (remote URI only). Upload a ZIP to browse files.");

        var paths = SkillPackageZipBrowser.ListPaths(entity.PackageZip);
        return new SkillZipTreeFound(paths);
    }
}
