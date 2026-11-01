using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReservationServiceOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceService",
                table: "StockReservations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceService",
                table: "StockReservations");
        }
    }
}
