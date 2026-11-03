using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Manufacturing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialManufacturing : Migration
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
                name: "BillsOfMaterials",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    OutputVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Materials = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    Stages = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillsOfMaterials", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_Bom_Document", "ISJSON([Materials])=1 AND ISJSON([Stages])=1 AND [Revision]>0");
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
                name: "ProductionOrders",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutputVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Materials = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    Stages = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StandardMaterialCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OverheadCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GoodQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WasteQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OutputLot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OutputExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReservationExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StagesCompleted = table.Column<int>(type: "int", nullable: false),
                    QualityPassed = table.Column<bool>(type: "bit", nullable: false),
                    QualityChecked = table.Column<bool>(type: "bit", nullable: false),
                    InputsConsumed = table.Column<bool>(type: "bit", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ResumeState = table.Column<int>(type: "int", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastFault = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_Production_Document", "ISJSON([Materials])=1 AND ISJSON([Stages])=1");
                    table.CheckConstraint("CK_Production_Quantities", "[PlannedQuantity]>0 AND [GoodQuantity]>=0 AND [WasteQuantity]>=0 AND [GoodQuantity]+[WasteQuantity]<=[PlannedQuantity] AND [StandardMaterialCost]>=0 AND [OverheadCost]>=0");
                    table.CheckConstraint("CK_Production_Workflow", "[State] BETWEEN 0 AND 8 AND [Attempts]>=0 AND [StagesCompleted] BETWEEN 0 AND 12");
                    table.ForeignKey(
                        name: "FK_ProductionOrders_BillsOfMaterials_TenantId_BomId",
                        columns: x => new { x.TenantId, x.BomId },
                        principalTable: "BillsOfMaterials",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionHistory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionHistory", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProductionHistory_ProductionOrders_TenantId_OrderId",
                        columns: x => new { x.TenantId, x.OrderId },
                        principalTable: "ProductionOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId_Id",
                table: "AuditEntries",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfMaterials_TenantId_Code_Revision",
                table: "BillsOfMaterials",
                columns: new[] { "TenantId", "Code", "Revision" },
                unique: true);

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
                name: "IX_ProductionHistory_TenantId_OrderId_CreatedAt",
                table: "ProductionHistory",
                columns: new[] { "TenantId", "OrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_State_NextAttemptAt_LeaseUntil",
                table: "ProductionOrders",
                columns: new[] { "State", "NextAttemptAt", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_BomId",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "BomId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_CreatedAt",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "CreatedAt" });
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
                name: "ProductionHistory");

            migrationBuilder.DropTable(
                name: "ProductionOrders");

            migrationBuilder.DropTable(
                name: "BillsOfMaterials");
        }
    }
}
