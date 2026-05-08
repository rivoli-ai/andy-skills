using MediatR;
using Microsoft.Extensions.Options;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Application.Options;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.SkillPackages;

public sealed record PublishSkillVersionCommand(
    string NamespaceSlug,
    string SkillSlug,
    string Version,
    string? Tag,
    string ArtifactUri,
    string ActorSubject,
    string? PublisherPatOneTime = null,
    byte[]? PackageZip = null) : IRequest<SkillVersionResponse>;

public sealed record SkillVersionResponse(
    Guid Id,
    Guid PackageId,
    string Version,
    string? Tag,
    bool IsLatest,
    string ArtifactUri,
    DateTime PublishedAtUtc,
    bool HasStoredZip,
    bool RemoteFetchRequiresPat);

public sealed class PublishSkillVersionCommandHandler(
    ISkillRegistryPersistence persistence,
    IRemoteArtifactFetcher remoteArtifactFetcher,
    IOptionsSnapshot<SkillsStorageOptions> skillsStorageOptions)
    : IRequestHandler<PublishSkillVersionCommand, SkillVersionResponse>
{
    public async Task<SkillVersionResponse> Handle(PublishSkillVersionCommand request, CancellationToken cancellationToken)
    {
        var nsSlug = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var actor = string.IsNullOrWhiteSpace(request.ActorSubject) ? "anonymous" : request.ActorSubject.Trim();
        var ns = await NamespaceMutationGuard.RequireForSkillMutationAsync(persistence, nsSlug, actor, cancellationToken)
            .ConfigureAwait(false);

        var skillSlug = SlugRules.NormalizeSlug(request.SkillSlug);
        var pkg = await persistence.GetPackageAsync(ns.Id, skillSlug, cancellationToken)
                  ?? throw new KeyNotFoundException($"Skill '{skillSlug}' was not found.");

        var version = request.Version.Trim();
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64)
            throw new ArgumentException("Invalid version string.");

        if (await persistence.VersionExistsAsync(pkg.Id, version, cancellationToken))
            throw new InvalidOperationException($"Version '{version}' already exists for this skill.");

        var uriRaw = request.ArtifactUri.Trim();
        if (uriRaw.Length is < 1 or > 2048)
            throw new ArgumentException("Artifact URI must be 1–2048 characters.");

        var patOneTime = string.IsNullOrWhiteSpace(request.PublisherPatOneTime)
            ? null
            : request.PublisherPatOneTime.Trim();
        if (patOneTime != null && patOneTime.Length > 8192)
            throw new ArgumentException("Publisher access token is too long.");

        byte[] zipBytes;
        string storedArtifactUri;

        if (request.PackageZip is { Length: > 0 })
        {
            // Multipart upload: bytes already provided; URI is the registry install URL from the API.
            zipBytes = request.PackageZip;
            storedArtifactUri = uriRaw;
        }
        else
        {
            var fetch = ArtifactUriNormalizer.ResolveFetch(uriRaw);
            if (fetch.FetchUri.Length > 2048)
                throw new ArgumentException("Normalized artifact URI exceeds 2048 characters.");

            var downloaded = await remoteArtifactFetcher
                .DownloadZipAsync(fetch, patOneTime ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            if (downloaded == null || downloaded.Length == 0)
            {
                throw new ArgumentException(
                    "Could not download a ZIP from this URL. Use a direct link to a .zip file, a GitHub repo or folder link (Tree page per branch), or an Azure DevOps repo URL; add an optional PAT if the repo is private (token is used once and not stored).");
            }

            zipBytes = downloaded;
            storedArtifactUri = BuildInstallZipUrl(skillsStorageOptions.Value, nsSlug, skillSlug, version);
        }

        await persistence.ClearLatestFlagForPackageAsync(pkg.Id, cancellationToken);

        var detail = request.PackageZip is { Length: > 0 }
            ? $"{ns.Slug}/{skillSlug}@{version}"
            : TruncateDetail($"{ns.Slug}/{skillSlug}@{version} source={uriRaw}");

        var entity = new SkillVersion
        {
            Id = Guid.NewGuid(),
            PackageId = pkg.Id,
            Version = version,
            Tag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim(),
            IsLatest = true,
            ArtifactUri = storedArtifactUri,
            RemoteFetchRequiresPat = false,
            PackageZip = zipBytes,
            PublishedAtUtc = DateTime.UtcNow,
            PublishedBySubject = actor,
        };

        persistence.AddVersion(entity);

        persistence.AddAudit(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorSubject = actor,
            Action = "skill.version.publish",
            EntityType = nameof(SkillVersion),
            EntityId = entity.Id.ToString(),
            Detail = detail,
        });

        await persistence.SaveChangesAsync(cancellationToken);

        return new SkillVersionResponse(
            entity.Id,
            entity.PackageId,
            entity.Version,
            entity.Tag,
            entity.IsLatest,
            entity.ArtifactUri,
            entity.PublishedAtUtc,
            entity.PackageZip is { Length: > 0 },
            entity.RemoteFetchRequiresPat);
    }

    private static string BuildInstallZipUrl(SkillsStorageOptions opts, string ns, string skill, string ver)
    {
        var baseUrl = opts.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/api/install/{Uri.EscapeDataString(ns)}/{Uri.EscapeDataString(skill)}/{Uri.EscapeDataString(ver)}/package.zip";
    }

    private static string TruncateDetail(string s)
    {
        const int max = 4000;
        return s.Length <= max ? s : s[..max];
    }
}
