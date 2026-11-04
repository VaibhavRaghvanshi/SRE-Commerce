using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LowStockAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LowStockStates",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Low = table.Column<bool>(type: "bit", nullable: false),
                    Episode = table.Column<long>(type: "bigint", nullable: false),
                    Available = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LowStockStates", x => new { x.TenantId, x.VariantId });
                    table.CheckConstraint("CK_LowStockStates_Values", "[Episode]>=0 AND [Available]>=0");
                    table.ForeignKey(
                        name: "FK_LowStockStates_StockItems_TenantId_VariantId",
                        columns: x => new { x.TenantId, x.VariantId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LowStockStates");
        }
    }
}
