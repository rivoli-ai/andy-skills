using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.SkillPackages;

public sealed record PublishSkillVersionCommand(
    string NamespaceSlug,
    string SkillSlug,
    string Version,
    string? Tag,
    string ArtifactUri,
    string ActorSubject,
    byte[]? PackageZip = null) : IRequest<SkillVersionResponse>;

public sealed record SkillVersionResponse(
    Guid Id,
    Guid PackageId,
    string Version,
    string? Tag,
    bool IsLatest,
    string ArtifactUri,
    DateTime PublishedAtUtc,
    bool HasStoredZip);

public sealed class PublishSkillVersionCommandHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<PublishSkillVersionCommand, SkillVersionResponse>
{
    public async Task<SkillVersionResponse> Handle(PublishSkillVersionCommand request, CancellationToken cancellationToken)
    {
        var nsSlug = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var ns = await persistence.GetNamespaceBySlugAsync(nsSlug, cancellationToken)
                 ?? throw new KeyNotFoundException($"Namespace '{nsSlug}' was not found.");

        var skillSlug = SlugRules.NormalizeSlug(request.SkillSlug);
        var pkg = await persistence.GetPackageAsync(ns.Id, skillSlug, cancellationToken)
                  ?? throw new KeyNotFoundException($"Skill '{skillSlug}' was not found.");

        var version = request.Version.Trim();
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64)
            throw new ArgumentException("Invalid version string.");

        if (await persistence.VersionExistsAsync(pkg.Id, version, cancellationToken))
            throw new InvalidOperationException($"Version '{version}' already exists for this skill.");

        var uri = request.ArtifactUri.Trim();
        if (uri.Length is < 1 or > 2048)
            throw new ArgumentException("Artifact URI must be 1–2048 characters.");

        var actor = string.IsNullOrWhiteSpace(request.ActorSubject) ? "anonymous" : request.ActorSubject.Trim();

        await persistence.ClearLatestFlagForPackageAsync(pkg.Id, cancellationToken);

        var entity = new SkillVersion
        {
            Id = Guid.NewGuid(),
            PackageId = pkg.Id,
            Version = version,
            Tag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim(),
            IsLatest = true,
            ArtifactUri = uri,
            PackageZip = request.PackageZip,
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
            Detail = $"{ns.Slug}/{skillSlug}@{version}",
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
            entity.PackageZip is { Length: > 0 });
    }
}
