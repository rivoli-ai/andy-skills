using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;

namespace SkillRegistry.Application.SkillPackages;

public abstract record SkillDownloadOutcome;

public sealed record SkillDownloadZipBlob(byte[] ZipBytes) : SkillDownloadOutcome;

public sealed record SkillDownloadRedirect(Uri Url) : SkillDownloadOutcome;

/// <summary>Installer must send <c>X-Artifact-PAT</c> with their own token (per request).</summary>
public sealed record SkillDownloadPatRequired : SkillDownloadOutcome;

public sealed record SkillDownloadNotFound : SkillDownloadOutcome;

public sealed record ResolveSkillDownloadQuery(
    string NamespaceSlug,
    string SkillSlug,
    string Version,
    string? ViewerSubject,
    string? InstallerArtifactPat)
    : IRequest<SkillDownloadOutcome>;

public sealed class ResolveSkillDownloadQueryHandler(
    ISkillRegistryPersistence persistence,
    IRemoteArtifactFetcher remoteArtifactFetcher)
    : IRequestHandler<ResolveSkillDownloadQuery, SkillDownloadOutcome>
{
    public async Task<SkillDownloadOutcome> Handle(ResolveSkillDownloadQuery request, CancellationToken cancellationToken)
    {
        var ns = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var skill = SlugRules.NormalizeSlug(request.SkillSlug);
        var ver = request.Version.Trim();

        if (string.IsNullOrEmpty(ver))
            return new SkillDownloadNotFound();

        var entity = await persistence
            .GetVersionBySlugTripleAsync(ns, skill, ver, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            return new SkillDownloadNotFound();

        if (!NamespaceAccess.CanView(entity.Package.Namespace, request.ViewerSubject))
            return new SkillDownloadNotFound();

        if (entity.PackageZip is { Length: > 0 })
            return new SkillDownloadZipBlob(entity.PackageZip);

        if (!Uri.TryCreate(entity.ArtifactUri, UriKind.Absolute, out var absUri)
            || (absUri.Scheme != Uri.UriSchemeHttp && absUri.Scheme != Uri.UriSchemeHttps))
            return new SkillDownloadNotFound();

        if (entity.RemoteFetchRequiresPat)
        {
            var installerPat = request.InstallerArtifactPat?.Trim();
            if (string.IsNullOrEmpty(installerPat))
                return new SkillDownloadPatRequired();

            var bytes = await remoteArtifactFetcher
                .DownloadZipAsync(entity.ArtifactUri, installerPat, cancellationToken)
                .ConfigureAwait(false);
            return bytes == null ? new SkillDownloadNotFound() : new SkillDownloadZipBlob(bytes);
        }

        return new SkillDownloadRedirect(absUri);
    }
}
