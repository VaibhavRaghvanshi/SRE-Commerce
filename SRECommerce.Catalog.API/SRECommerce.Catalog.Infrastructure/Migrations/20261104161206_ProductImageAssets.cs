using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductImageAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Length = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_MediaAssets_Size", "[Length] BETWEEN 45 AND 750000 AND [Width] BETWEEN 1 AND 8192 AND [Height] BETWEEN 1 AND 8192");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaAssets");
        }
    }
}
