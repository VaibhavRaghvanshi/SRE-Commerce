using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockAlertSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockAlertReceipts",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlertReceipts", x => new { x.TenantId, x.UserId, x.VariantId });
                });

            migrationBuilder.CreateTable(
                name: "StockAlertSubscriptions",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CooldownMinutes = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlertSubscriptions", x => new { x.TenantId, x.UserId });
                    table.CheckConstraint("CK_StockAlertSubscriptions_Cooldown", "[CooldownMinutes] BETWEEN 60 AND 10080");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockAlertReceipts");

            migrationBuilder.DropTable(
                name: "StockAlertSubscriptions");
        }
    }
}
