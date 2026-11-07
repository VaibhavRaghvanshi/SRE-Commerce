using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SavedCartReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CartReminders",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    QueuedEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartReminders", x => new { x.TenantId, x.CartId });
                    table.CheckConstraint("CK_CartReminders_Queue", "([QueuedAt] IS NULL AND [QueuedEventId] IS NULL) OR ([QueuedAt] IS NOT NULL AND [QueuedEventId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CartReminders_Carts_TenantId_CartId",
                        columns: x => new { x.TenantId, x.CartId },
                        principalTable: "Carts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartReminders_RequestedAt",
                table: "CartReminders",
                column: "RequestedAt",
                filter: "[Enabled]=1 AND [QueuedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartReminders");
        }
    }
}
