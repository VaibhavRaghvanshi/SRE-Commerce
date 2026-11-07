using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LocalizedProductContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Translations",
                table: "Products",
                type: "nvarchar(max)",
                maxLength: 65536,
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Translations",
                table: "Products",
                sql: "ISJSON([Translations])=1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Translations",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Translations",
                table: "Products");
        }
    }
}
