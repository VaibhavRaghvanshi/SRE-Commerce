using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
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
                name: "Branches",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    TenantLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => new { x.TenantId, x.Id });
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
                name: "StockItems",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockItems", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_StockItems_ReorderLevel", "[ReorderLevel]>=0");
                });

            migrationBuilder.CreateTable(
                name: "StockOperations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockOperations", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_StockOperations_Result", "ISJSON([Result])=1");
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Warehouses_Branches_TenantId_BranchId",
                        columns: x => new { x.TenantId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBatches",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Lot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBatches", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_InventoryBatches_TenantId_Id_WarehouseId_VariantId", x => new { x.TenantId, x.Id, x.WarehouseId, x.VariantId });
                    table.ForeignKey(
                        name: "FK_InventoryBatches_StockItems_TenantId_VariantId",
                        columns: x => new { x.TenantId, x.VariantId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBatches_Warehouses_TenantId_WarehouseId",
                        columns: x => new { x.TenantId, x.WarehouseId },
                        principalTable: "Warehouses",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptRevision = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_TenantId_SupplierId",
                        columns: x => new { x.TenantId, x.SupplierId },
                        principalTable: "Suppliers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Warehouses_TenantId_WarehouseId",
                        columns: x => new { x.TenantId, x.WarehouseId },
                        principalTable: "Warehouses",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_StockReservations_Warehouses_TenantId_WarehouseId",
                        columns: x => new { x.TenantId, x.WarehouseId },
                        principalTable: "Warehouses",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBalances",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OnHand = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Damaged = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBalances", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_InventoryBalances_TenantId_Id_VariantId", x => new { x.TenantId, x.Id, x.VariantId });
                    table.CheckConstraint("CK_InventoryBalances_Quantities", "[OnHand]>=0 AND [Reserved]>=0 AND [Damaged]>=0 AND [Reserved]<=[OnHand]");
                    table.ForeignKey(
                        name: "FK_InventoryBalances_InventoryBatches_TenantId_BatchId_WarehouseId_VariantId",
                        columns: x => new { x.TenantId, x.BatchId, x.WarehouseId, x.VariantId },
                        principalTable: "InventoryBatches",
                        principalColumns: new[] { "TenantId", "Id", "WarehouseId", "VariantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordered = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Received = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PurchaseLines_Quantities", "[Ordered]>0 AND [Received]>=0 AND [Received]<=[Ordered] AND [UnitCost]>=0");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_StockItems_TenantId_VariantId",
                        columns: x => new { x.TenantId, x.VariantId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BalanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OnHandDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReservedDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DamagedDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_InventoryMovements_InventoryBalances_TenantId_BalanceId",
                        columns: x => new { x.TenantId, x.BalanceId },
                        principalTable: "InventoryBalances",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReservationLines",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BalanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservationLines", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ReservationLines_Quantity", "[Quantity]>0");
                    table.ForeignKey(
                        name: "FK_StockReservationLines_InventoryBalances_TenantId_BalanceId_VariantId",
                        columns: x => new { x.TenantId, x.BalanceId, x.VariantId },
                        principalTable: "InventoryBalances",
                        principalColumns: new[] { "TenantId", "Id", "VariantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservationLines_StockReservations_TenantId_ReservationId",
                        columns: x => new { x.TenantId, x.ReservationId },
                        principalTable: "StockReservations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId_Id",
                table: "AuditEntries",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_Code",
                table: "Branches",
                columns: new[] { "TenantId", "Code" },
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
                name: "IX_InventoryBalances_TenantId_BatchId_WarehouseId_VariantId",
                table: "InventoryBalances",
                columns: new[] { "TenantId", "BatchId", "WarehouseId", "VariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_TenantId_WarehouseId_VariantId_BatchId",
                table: "InventoryBalances",
                columns: new[] { "TenantId", "WarehouseId", "VariantId", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_TenantId_ExpiresAt",
                table: "InventoryBatches",
                columns: new[] { "TenantId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_TenantId_VariantId",
                table: "InventoryBatches",
                columns: new[] { "TenantId", "VariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_TenantId_WarehouseId_VariantId_Lot",
                table: "InventoryBatches",
                columns: new[] { "TenantId", "WarehouseId", "VariantId", "Lot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId_BalanceId_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "BalanceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId_OperationId",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_TenantId_PurchaseOrderId_VariantId",
                table: "PurchaseOrderLines",
                columns: new[] { "TenantId", "PurchaseOrderId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_TenantId_VariantId",
                table: "PurchaseOrderLines",
                columns: new[] { "TenantId", "VariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_Reference",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_SupplierId",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_WarehouseId",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_TenantId_Barcode",
                table: "StockItems",
                columns: new[] { "TenantId", "Barcode" });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_TenantId_Sku",
                table: "StockItems",
                columns: new[] { "TenantId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReservationLines_TenantId_BalanceId_VariantId",
                table: "StockReservationLines",
                columns: new[] { "TenantId", "BalanceId", "VariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservationLines_TenantId_ReservationId_BalanceId",
                table: "StockReservationLines",
                columns: new[] { "TenantId", "ReservationId", "BalanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_Status_ExpiresAt",
                table: "StockReservations",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_TenantId_Status_ExpiresAt",
                table: "StockReservations",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_TenantId_WarehouseId",
                table: "StockReservations",
                columns: new[] { "TenantId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_Code",
                table: "Suppliers",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId_BranchId",
                table: "Warehouses",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId_Code",
                table: "Warehouses",
                columns: new[] { "TenantId", "Code" },
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
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "StockOperations");

            migrationBuilder.DropTable(
                name: "StockReservationLines");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "InventoryBalances");

            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "InventoryBatches");

            migrationBuilder.DropTable(
                name: "StockItems");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropTable(
                name: "Branches");
        }
    }
}
