using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoteFetchRequiresPatOnVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RemoteFetchRequiresPat",
                table: "skill_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemoteFetchRequiresPat",
                table: "skill_versions");
        }
    }
}
