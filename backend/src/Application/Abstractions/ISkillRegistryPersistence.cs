using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.Abstractions;

public interface ISkillRegistryPersistence
{
    Task<IReadOnlyList<SkillNamespace>> ListNamespacesVisibleAsync(string? viewerSubject, CancellationToken cancellationToken);

    /// <summary>Namespace visible to viewer (includes members); otherwise null.</summary>
    Task<SkillNamespace?> GetNamespaceBySlugForViewerAsync(string slug, string? viewerSubject, CancellationToken cancellationToken);

    /// <summary>Tracked namespace when <paramref name="actorSubject"/> is an Owner; otherwise null.</summary>
    Task<SkillNamespace?> GetNamespaceTrackedForMutationAsync(string slug, string actorSubject, CancellationToken cancellationToken);

    Task<bool> NamespaceSlugExistsAsync(string slug, CancellationToken cancellationToken);

    void AddNamespace(SkillNamespace entity);

    void RemoveNamespace(SkillNamespace entity);

    Task<IReadOnlyList<SkillPackage>> ListPackagesAsync(Guid namespaceId, CancellationToken cancellationToken);

    Task<SkillPackage?> GetPackageAsync(Guid namespaceId, string skillSlug, CancellationToken cancellationToken);

    Task<SkillPackage?> GetPackageTrackedAsync(Guid namespaceId, string skillSlug, CancellationToken cancellationToken);

    void AddPackage(SkillPackage entity);

    void RemovePackage(SkillPackage entity);

    Task<IReadOnlyList<SkillVersionListRow>> ListVersionsAsync(Guid packageId, CancellationToken cancellationToken);

    Task<bool> VersionExistsAsync(Guid packageId, string version, CancellationToken cancellationToken);

    void AddVersion(SkillVersion entity);

    Task ClearLatestFlagForPackageAsync(Guid packageId, CancellationToken cancellationToken);

    void AddAudit(AuditEvent auditEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SkillPackage>> SearchPackagesAsync(string? query, string? viewerSubject, CancellationToken cancellationToken);

    Task<SkillVersion?> GetVersionBySlugTripleAsync(
        string namespaceSlug,
        string skillSlug,
        string version,
        CancellationToken cancellationToken);
}
