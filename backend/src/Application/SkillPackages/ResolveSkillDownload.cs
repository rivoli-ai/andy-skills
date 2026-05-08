using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;

namespace SkillRegistry.Application.SkillPackages;

public abstract record SkillDownloadOutcome;

public sealed record SkillDownloadZipBlob(byte[] ZipBytes) : SkillDownloadOutcome;

public sealed record SkillDownloadRedirect(Uri Url) : SkillDownloadOutcome;

public sealed record SkillDownloadNotFound : SkillDownloadOutcome;

public sealed record ResolveSkillDownloadQuery(string NamespaceSlug, string SkillSlug, string Version)
    : IRequest<SkillDownloadOutcome>;

public sealed class ResolveSkillDownloadQueryHandler(ISkillRegistryPersistence persistence)
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

        if (entity.PackageZip is { Length: > 0 })
            return new SkillDownloadZipBlob(entity.PackageZip);

        if (Uri.TryCreate(entity.ArtifactUri, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return new SkillDownloadRedirect(uri);

        return new SkillDownloadNotFound();
    }
}
