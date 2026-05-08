using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.Abstractions;

public interface ISkillRegistryPersistence
{
    Task<IReadOnlyList<SkillNamespace>> ListNamespacesAsync(CancellationToken cancellationToken);

    Task<SkillNamespace?> GetNamespaceBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<bool> NamespaceSlugExistsAsync(string slug, CancellationToken cancellationToken);

    void AddNamespace(SkillNamespace entity);

    Task<IReadOnlyList<SkillPackage>> ListPackagesAsync(Guid namespaceId, CancellationToken cancellationToken);

    Task<SkillPackage?> GetPackageAsync(Guid namespaceId, string skillSlug, CancellationToken cancellationToken);

    void AddPackage(SkillPackage entity);

    Task<IReadOnlyList<SkillVersionListRow>> ListVersionsAsync(Guid packageId, CancellationToken cancellationToken);

    Task<bool> VersionExistsAsync(Guid packageId, string version, CancellationToken cancellationToken);

    void AddVersion(SkillVersion entity);

    Task ClearLatestFlagForPackageAsync(Guid packageId, CancellationToken cancellationToken);

    void AddAudit(AuditEvent auditEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SkillPackage>> SearchPackagesAsync(string? query, CancellationToken cancellationToken);

    Task<SkillVersion?> GetVersionBySlugTripleAsync(
        string namespaceSlug,
        string skillSlug,
        string version,
        CancellationToken cancellationToken);
}
