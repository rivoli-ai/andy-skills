using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SkillRegistry.Infrastructure.Persistence;

/// <summary>Design-time only (dotnet ef). Uses env <c>ConnectionStrings__DefaultConnection</c> or local Postgres defaults.</summary>
public sealed class SkillRegistryDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SkillRegistryDbContext>
{
    public SkillRegistryDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                 ?? "Host=localhost;Port=5432;Username=analyser;Password=analyser_password;Database=skillregistry";

        var opt = new DbContextOptionsBuilder<SkillRegistryDbContext>();
        opt.UseNpgsql(cs);
        return new SkillRegistryDbContext(opt.Options);
    }
}
