using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesReportIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_CreatedAt",
                table: "Orders",
                columns: new[] { "TenantId", "CreatedAt" })
                .Annotation("SqlServer:Include", new[] { "Currency", "Historical", "Status", "PaymentState", "Total", "Refunded" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_CreatedAt",
                table: "Orders");
        }
    }
}
