using Microsoft.EntityFrameworkCore;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Infrastructure.Persistence;

public sealed class SkillRegistryDbContext(DbContextOptions<SkillRegistryDbContext> options)
    : DbContext(options)
{
    public DbSet<SkillNamespace> Namespaces => Set<SkillNamespace>();
    public DbSet<NamespaceMember> NamespaceMembers => Set<NamespaceMember>();
    public DbSet<SkillPackage> SkillPackages => Set<SkillPackage>();
    public DbSet<SkillVersion> SkillVersions => Set<SkillVersion>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SkillNamespace>(e =>
        {
            e.ToTable("skill_namespaces");
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(64).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.CreatedBySubject).HasMaxLength(256);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasMany(x => x.Members).WithOne(x => x.Namespace).HasForeignKey(x => x.NamespaceId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Packages).WithOne(x => x.Namespace).HasForeignKey(x => x.NamespaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NamespaceMember>(e =>
        {
            e.ToTable("namespace_members");
            e.HasKey(x => x.Id);
            e.Property(x => x.SubjectUserId).HasMaxLength(256).IsRequired();
            e.HasIndex(x => new { x.NamespaceId, x.SubjectUserId }).IsUnique();
        });

        modelBuilder.Entity<SkillPackage>(e =>
        {
            e.ToTable("skill_packages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(64).IsRequired();
            e.Property(x => x.Title).HasMaxLength(256).IsRequired();
            e.Property(x => x.Description).HasMaxLength(8000);
            e.Property(x => x.CreatedBySubject).HasMaxLength(256);
            e.HasIndex(x => new { x.NamespaceId, x.Slug }).IsUnique();
            e.HasMany(x => x.Versions).WithOne(x => x.Package).HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SkillVersion>(e =>
        {
            e.ToTable("skill_versions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).HasMaxLength(64).IsRequired();
            e.Property(x => x.Tag).HasMaxLength(64);
            e.Property(x => x.ArtifactUri).HasMaxLength(2048).IsRequired();
            e.Property(x => x.RemoteFetchRequiresPat).HasDefaultValue(false);
            e.Property(x => x.PackageZip).HasColumnType("bytea");
            e.Property(x => x.PublishedBySubject).HasMaxLength(256);
            e.HasIndex(x => new { x.PackageId, x.Version }).IsUnique();
        });

        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.ActorSubject).HasMaxLength(256).IsRequired();
            e.Property(x => x.Action).HasMaxLength(128).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(4000);
        });
    }
}
