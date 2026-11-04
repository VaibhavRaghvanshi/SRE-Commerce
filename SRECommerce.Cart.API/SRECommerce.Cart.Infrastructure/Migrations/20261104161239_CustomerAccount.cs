using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => new { x.TenantId, x.UserId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "SavedProducts",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedProducts", x => new { x.TenantId, x.UserId, x.Kind, x.ProductId });
                    table.CheckConstraint("CK_SavedProducts_Kind", "[Kind] BETWEEN 0 AND 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_TenantId_UserId_Active_UpdatedAt",
                table: "CustomerAddresses",
                columns: new[] { "TenantId", "UserId", "Active", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedProducts_TenantId_UserId_Kind_Active_UpdatedAt",
                table: "SavedProducts",
                columns: new[] { "TenantId", "UserId", "Kind", "Active", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "SavedProducts");
        }
    }
}
