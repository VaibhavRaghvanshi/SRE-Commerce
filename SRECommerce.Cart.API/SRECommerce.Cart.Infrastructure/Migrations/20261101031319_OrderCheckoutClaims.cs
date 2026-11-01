using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrderCheckoutClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerRole",
                table: "CheckoutPreparations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CartCheckoutClaims",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CartRevision = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartCheckoutClaims", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_CartCheckoutClaims_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_CartCheckoutClaims_Carts_TenantId_CartId",
                        columns: x => new { x.TenantId, x.CartId },
                        principalTable: "Carts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CartCheckoutClaims_CheckoutPreparations_TenantId_PreparationId",
                        columns: x => new { x.TenantId, x.PreparationId },
                        principalTable: "CheckoutPreparations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartCheckoutClaims_Status_ExpiresAt",
                table: "CartCheckoutClaims",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CartCheckoutClaims_TenantId_CartId_CartRevision",
                table: "CartCheckoutClaims",
                columns: new[] { "TenantId", "CartId", "CartRevision" },
                unique: true,
                filter: "[Status]<2");

            migrationBuilder.CreateIndex(
                name: "IX_CartCheckoutClaims_TenantId_PreparationId",
                table: "CartCheckoutClaims",
                columns: new[] { "TenantId", "PreparationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartCheckoutClaims");

            migrationBuilder.DropColumn(
                name: "CustomerRole",
                table: "CheckoutPreparations");
        }
    }
}
