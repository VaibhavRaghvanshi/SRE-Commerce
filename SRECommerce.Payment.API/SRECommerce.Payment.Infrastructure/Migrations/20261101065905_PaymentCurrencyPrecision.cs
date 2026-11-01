using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Payment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentCurrencyPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DecimalPlaces",
                table: "PaymentTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Precision",
                table: "PaymentTransactions",
                sql: "[DecimalPlaces] BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Precision",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "DecimalPlaces",
                table: "PaymentTransactions");
        }
    }
}
