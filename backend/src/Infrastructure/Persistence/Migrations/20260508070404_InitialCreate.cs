using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "skill_namespaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_namespaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "namespace_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_namespace_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_namespace_members_skill_namespaces_NamespaceId",
                        column: x => x.NamespaceId,
                        principalTable: "skill_namespaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skill_packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_skill_packages_skill_namespaces_NamespaceId",
                        column: x => x.NamespaceId,
                        principalTable: "skill_namespaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skill_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsLatest = table.Column<bool>(type: "boolean", nullable: false),
                    ArtifactUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedBySubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_skill_versions_skill_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "skill_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_namespace_members_NamespaceId_SubjectUserId",
                table: "namespace_members",
                columns: new[] { "NamespaceId", "SubjectUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_skill_namespaces_Slug",
                table: "skill_namespaces",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_skill_packages_NamespaceId_Slug",
                table: "skill_packages",
                columns: new[] { "NamespaceId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_skill_versions_PackageId_Version",
                table: "skill_versions",
                columns: new[] { "PackageId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "namespace_members");

            migrationBuilder.DropTable(
                name: "skill_versions");

            migrationBuilder.DropTable(
                name: "skill_packages");

            migrationBuilder.DropTable(
                name: "skill_namespaces");
        }
    }
}
