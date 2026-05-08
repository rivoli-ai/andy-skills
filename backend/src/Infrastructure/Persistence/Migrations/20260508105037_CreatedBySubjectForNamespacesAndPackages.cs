using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatedBySubjectForNamespacesAndPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBySubject",
                table: "skill_packages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBySubject",
                table: "skill_namespaces",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE skill_namespaces AS sn
                SET "CreatedBySubject" = sub."SubjectUserId"
                FROM (
                  SELECT DISTINCT ON ("NamespaceId") "NamespaceId", "SubjectUserId"
                  FROM namespace_members
                  WHERE "Role" = 0
                  ORDER BY "NamespaceId", "Id"
                ) AS sub
                WHERE sub."NamespaceId" = sn."Id"
                  AND sn."CreatedBySubject" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBySubject",
                table: "skill_packages");

            migrationBuilder.DropColumn(
                name: "CreatedBySubject",
                table: "skill_namespaces");
        }
    }
}
