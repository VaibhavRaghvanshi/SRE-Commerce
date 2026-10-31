using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialStorefront : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorService = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SupportReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationInbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Producer = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationInbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Envelope = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LayoutTemplates",
                columns: table => new
                {
                    BusinessType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Document = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutTemplates", x => x.BusinessType);
                    table.CheckConstraint("CK_LayoutTemplates_Json", "ISJSON([Document]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "StorefrontPages",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Document = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontPages", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_StorefrontPages_Json", "ISJSON([Document]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "LayoutVersions",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Document = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutVersions", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_LayoutVersions_TenantId_PageId_Id", x => new { x.TenantId, x.PageId, x.Id });
                    table.CheckConstraint("CK_LayoutVersions_Json", "ISJSON([Document]) = 1");
                    table.ForeignKey(
                        name: "FK_LayoutVersions_StorefrontPages_TenantId_PageId",
                        columns: x => new { x.TenantId, x.PageId },
                        principalTable: "StorefrontPages",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LayoutPublishHistory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LayoutVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Rollback = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutPublishHistory", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_LayoutPublishHistory_LayoutVersions_TenantId_PageId_LayoutVersionId",
                        columns: x => new { x.TenantId, x.PageId, x.LayoutVersionId },
                        principalTable: "LayoutVersions",
                        principalColumns: new[] { "TenantId", "PageId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublishedConfigurations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LayoutVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishedConfigurations", x => new { x.TenantId, x.PageId });
                    table.ForeignKey(
                        name: "FK_PublishedConfigurations_LayoutVersions_TenantId_PageId_LayoutVersionId",
                        columns: x => new { x.TenantId, x.PageId, x.LayoutVersionId },
                        principalTable: "LayoutVersions",
                        principalColumns: new[] { "TenantId", "PageId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId_Id",
                table: "AuditEntries",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationInbox_TenantId_ProcessedAt",
                table: "IntegrationInbox",
                columns: new[] { "TenantId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOutbox_LeaseUntil",
                table: "IntegrationOutbox",
                column: "LeaseUntil");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOutbox_PublishedAt_NextAttemptAt",
                table: "IntegrationOutbox",
                columns: new[] { "PublishedAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LayoutPublishHistory_TenantId_PageId_LayoutVersionId",
                table: "LayoutPublishHistory",
                columns: new[] { "TenantId", "PageId", "LayoutVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LayoutPublishHistory_TenantId_PageId_Sequence",
                table: "LayoutPublishHistory",
                columns: new[] { "TenantId", "PageId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LayoutVersions_TenantId_PageId_Revision",
                table: "LayoutVersions",
                columns: new[] { "TenantId", "PageId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublishedConfigurations_TenantId_PageId_LayoutVersionId",
                table: "PublishedConfigurations",
                columns: new[] { "TenantId", "PageId", "LayoutVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontPages_TenantId_Slug",
                table: "StorefrontPages",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "IntegrationInbox");

            migrationBuilder.DropTable(
                name: "IntegrationOutbox");

            migrationBuilder.DropTable(
                name: "LayoutPublishHistory");

            migrationBuilder.DropTable(
                name: "LayoutTemplates");

            migrationBuilder.DropTable(
                name: "PublishedConfigurations");

            migrationBuilder.DropTable(
                name: "LayoutVersions");

            migrationBuilder.DropTable(
                name: "StorefrontPages");
        }
    }
}
