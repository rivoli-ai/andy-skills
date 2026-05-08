using Microsoft.EntityFrameworkCore;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;
using SkillRegistry.Domain.Entities;
using SkillRegistry.Domain.Enums;

namespace SkillRegistry.Infrastructure.Persistence;

public sealed class SkillRegistryPersistence(SkillRegistryDbContext db) : ISkillRegistryPersistence
{
    public async Task<IReadOnlyList<SkillNamespace>> ListNamespacesVisibleAsync(
        string? viewerSubject,
        CancellationToken cancellationToken)
    {
        var v = NamespaceAccess.NormalizeSubject(viewerSubject);
        IQueryable<SkillNamespace> q = db.Namespaces.AsNoTracking().Include(n => n.Members);
        if (v == null)
        {
            q = q.Where(n => n.Visibility == NamespaceVisibility.OrgVisible);
        }
        else
        {
            q = q.Where(n =>
                n.Visibility == NamespaceVisibility.OrgVisible
                || n.Members.Any(m => m.SubjectUserId == v));
        }

        var list = await q.OrderBy(n => n.Slug).ToListAsync(cancellationToken).ConfigureAwait(false);
        return list;
    }

    public async Task<SkillNamespace?> GetNamespaceBySlugForViewerAsync(
        string slug,
        string? viewerSubject,
        CancellationToken cancellationToken)
    {
        var ns = await db.Namespaces.AsNoTracking()
            .Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Slug == slug, cancellationToken)
            .ConfigureAwait(false);
        if (ns == null)
            return null;
        return NamespaceAccess.CanView(ns, viewerSubject) ? ns : null;
    }

    public async Task<SkillNamespace?> GetNamespaceTrackedForMutationAsync(
        string slug,
        string actorSubject,
        CancellationToken cancellationToken)
    {
        var ns = await db.Namespaces
            .Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Slug == slug, cancellationToken)
            .ConfigureAwait(false);
        if (ns == null)
            return null;
        return NamespaceAccess.CanManage(ns, actorSubject) ? ns : null;
    }

    public Task<bool> NamespaceSlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        db.Namespaces.AnyAsync(n => n.Slug == slug, cancellationToken);

    public void AddNamespace(SkillNamespace entity) => db.Namespaces.Add(entity);

    public void RemoveNamespace(SkillNamespace entity) => db.Namespaces.Remove(entity);

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

    public Task<SkillPackage?> GetPackageTrackedAsync(Guid namespaceId, string skillSlug, CancellationToken cancellationToken) =>
        db.SkillPackages.FirstOrDefaultAsync(
            p => p.NamespaceId == namespaceId && p.Slug == skillSlug,
            cancellationToken);

    public void AddPackage(SkillPackage entity) => db.SkillPackages.Add(entity);

    public void RemovePackage(SkillPackage entity) => db.SkillPackages.Remove(entity);

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
                v.PackageZip != null,
                v.RemoteFetchRequiresPat))
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

    public async Task<IReadOnlyList<SkillPackage>> SearchPackagesAsync(
        string? query,
        string? viewerSubject,
        CancellationToken cancellationToken)
    {
        var v = NamespaceAccess.NormalizeSubject(viewerSubject);
        var q = db.SkillPackages.AsNoTracking().Include(p => p.Namespace).ThenInclude(n => n.Members).AsQueryable();

        if (v == null)
        {
            q = q.Where(p => p.Namespace.Visibility == NamespaceVisibility.OrgVisible);
        }
        else
        {
            q = q.Where(p =>
                p.Namespace.Visibility == NamespaceVisibility.OrgVisible
                || p.Namespace.Members.Any(m => m.SubjectUserId == v));
        }

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
            .ThenInclude(n => n.Members)
            .FirstOrDefaultAsync(
                v => v.Package.Namespace.Slug == namespaceSlug
                     && v.Package.Slug == skillSlug
                     && v.Version == version,
                cancellationToken);

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
