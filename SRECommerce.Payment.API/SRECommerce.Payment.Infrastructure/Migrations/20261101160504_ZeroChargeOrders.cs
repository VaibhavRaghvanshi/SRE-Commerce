using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Payment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ZeroChargeOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amounts",
                table: "PaymentTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amounts",
                table: "PaymentTransactions",
                sql: "[Amount]>=0 AND [Refunded]>=0 AND [Refunded]<=[Amount]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amounts",
                table: "PaymentTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amounts",
                table: "PaymentTransactions",
                sql: "[Amount]>0 AND [Refunded]>=0 AND [Refunded]<=[Amount]");
        }
    }
}
