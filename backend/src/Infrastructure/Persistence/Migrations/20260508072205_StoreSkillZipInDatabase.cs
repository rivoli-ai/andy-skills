using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreSkillZipInDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PackageZip",
                table: "skill_versions",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackageZip",
                table: "skill_versions");
        }
    }
}
