using Microsoft.EntityFrameworkCore;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Infrastructure.Persistence;

public sealed class SkillRegistryPersistence(SkillRegistryDbContext db) : ISkillRegistryPersistence
{
    public async Task<IReadOnlyList<SkillNamespace>> ListNamespacesAsync(CancellationToken cancellationToken)
    {
        var list = await db.Namespaces.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return list;
    }

    public Task<SkillNamespace?> GetNamespaceBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Namespaces.AsNoTracking().FirstOrDefaultAsync(n => n.Slug == slug, cancellationToken);

    public Task<bool> NamespaceSlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        db.Namespaces.AnyAsync(n => n.Slug == slug, cancellationToken);

    public void AddNamespace(SkillNamespace entity) => db.Namespaces.Add(entity);

    public async Task<IReadOnlyList<SkillPackage>> ListPackagesAsync(Guid namespaceId, CancellationToken cancellationToken)
    {
        var list = await db.SkillPackages.AsNoTracking()
            .Where(p => p.NamespaceId == namespaceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return list;
    }

    public Task<SkillPackage?> GetPackageAsync(Guid namespaceId, string skillSlug, CancellationToken cancellationToken) =>
        db.SkillPackages.AsNoTracking().FirstOrDefaultAsync(
            p => p.NamespaceId == namespaceId && p.Slug == skillSlug,
            cancellationToken);

    public void AddPackage(SkillPackage entity) => db.SkillPackages.Add(entity);

    public async Task<IReadOnlyList<SkillVersionListRow>> ListVersionsAsync(Guid packageId, CancellationToken cancellationToken)
    {
        var list = await db.SkillVersions.AsNoTracking()
            .Where(v => v.PackageId == packageId)
            .Select(v => new SkillVersionListRow(
                v.Id,
                v.PackageId,
                v.Version,
                v.Tag,
                v.IsLatest,
                v.ArtifactUri,
                v.PublishedAtUtc,
                v.PackageZip != null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return list;
    }

    public Task<bool> VersionExistsAsync(Guid packageId, string version, CancellationToken cancellationToken) =>
        db.SkillVersions.AnyAsync(v => v.PackageId == packageId && v.Version == version, cancellationToken);

    public void AddVersion(SkillVersion entity) => db.SkillVersions.Add(entity);

    public Task ClearLatestFlagForPackageAsync(Guid packageId, CancellationToken cancellationToken) =>
        db.SkillVersions
            .Where(v => v.PackageId == packageId && v.IsLatest)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsLatest, false), cancellationToken);

    public void AddAudit(AuditEvent auditEvent) => db.AuditEvents.Add(auditEvent);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<SkillPackage>> SearchPackagesAsync(string? query, CancellationToken cancellationToken)
    {
        var q = db.SkillPackages.AsNoTracking().Include(p => p.Namespace).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = "%" + EscapeLike(query.Trim()) + "%";
            q = q.Where(p =>
                EF.Functions.ILike(p.Title, pattern)
                || (p.Description != null && EF.Functions.ILike(p.Description, pattern))
                || EF.Functions.ILike(p.Slug, pattern)
                || EF.Functions.ILike(p.Namespace.Slug, pattern));
        }

        return await q.OrderByDescending(p => p.CreatedAtUtc).Take(50).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<SkillVersion?> GetVersionBySlugTripleAsync(
        string namespaceSlug,
        string skillSlug,
        string version,
        CancellationToken cancellationToken) =>
        db.SkillVersions.AsNoTracking()
            .Include(v => v.Package)
            .ThenInclude(p => p.Namespace)
            .FirstOrDefaultAsync(
                v => v.Package.Namespace.Slug == namespaceSlug
                     && v.Package.Slug == skillSlug
                     && v.Version == version,
                cancellationToken);

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
