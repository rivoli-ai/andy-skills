using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;

namespace SkillRegistry.Application.SkillPackages;

public sealed record GetSkillZipFileQuery(string NamespaceSlug, string SkillSlug, string Version, string RelativePath, string? ViewerSubject)
    : IRequest<SkillZipFileResult>;

public abstract record SkillZipFileResult;

public sealed record SkillZipFileFound(string Path, string ContentUtf8, bool IsBinary, long SizeBytes) : SkillZipFileResult;

public sealed record SkillZipFileNotFound(string Reason) : SkillZipFileResult;

public sealed class GetSkillZipFileQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<GetSkillZipFileQuery, SkillZipFileResult>
{
    public async Task<SkillZipFileResult> Handle(GetSkillZipFileQuery request, CancellationToken cancellationToken)
    {
        var ns = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var skill = SlugRules.NormalizeSlug(request.SkillSlug);
        var ver = request.Version.Trim();
        if (string.IsNullOrEmpty(ver))
            return new SkillZipFileNotFound("Version is required.");

        if (!SkillPackageZipBrowser.TryNormalizeClientPath(request.RelativePath, out var relPath, out var pathErr))
            return new SkillZipFileNotFound(pathErr ?? "Invalid path.");

        var entity = await persistence
            .GetVersionBySlugTripleAsync(ns, skill, ver, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            return new SkillZipFileNotFound("Version not found.");

        if (!NamespaceAccess.CanView(entity.Package.Namespace, request.ViewerSubject))
            return new SkillZipFileNotFound("Version not found.");

        if (entity.PackageZip is not { Length: > 0 })
            return new SkillZipFileNotFound(
                "This version has no ZIP in the registry (remote URI only). Upload a ZIP to browse files.");

        var preview = SkillPackageZipBrowser.TryReadEntryPreview(entity.PackageZip, relPath);
        if (!preview.Found)
            return new SkillZipFileNotFound(preview.Error ?? "File not found.");

        return new SkillZipFileFound(relPath, preview.ContentUtf8, preview.IsBinary, preview.TotalUncompressedBytes);
    }
}
