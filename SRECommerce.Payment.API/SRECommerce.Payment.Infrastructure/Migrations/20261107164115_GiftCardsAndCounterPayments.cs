using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Payment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GiftCardsAndCounterPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Sandbox",
                table: "PaymentTransactions");

            migrationBuilder.AddColumn<Guid>(
                name: "GiftCardId",
                table: "PaymentTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GiftCards",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodeHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCards", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_GiftCards_Balance", "[Value]>0 AND [Balance]>=0 AND [Balance]<=[Value]");
                    table.CheckConstraint("CK_GiftCards_State", "(([ClaimedBy] IS NULL AND [ClaimedAt] IS NULL) OR ([ClaimedBy] IS NOT NULL AND [ClaimedAt] IS NOT NULL)) AND ([VoidedAt] IS NULL OR ([ClaimedBy] IS NULL AND [Balance]=0))");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TenantId_GiftCardId",
                table: "PaymentTransactions",
                columns: new[] { "TenantId", "GiftCardId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Tender",
                table: "PaymentTransactions",
                sql: "([Provider]=N'Sandbox' AND [Method] NOT IN (N'GiftCard',N'CounterCash') AND [GiftCardId] IS NULL) OR ([Provider]=N'GiftCard' AND [Method]=N'GiftCard' AND ([Status] IN (3,4) OR [GiftCardId] IS NOT NULL)) OR ([Provider]=N'Counter' AND [Method]=N'CounterCash' AND [GiftCardId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCards_TenantId_ClaimedBy_IssuedAt",
                table: "GiftCards",
                columns: new[] { "TenantId", "ClaimedBy", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCards_TenantId_CodeHash",
                table: "GiftCards",
                columns: new[] { "TenantId", "CodeHash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_GiftCards_TenantId_GiftCardId",
                table: "PaymentTransactions",
                columns: new[] { "TenantId", "GiftCardId" },
                principalTable: "GiftCards",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_GiftCards_TenantId_GiftCardId",
                table: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "GiftCards");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_TenantId_GiftCardId",
                table: "PaymentTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Tender",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "GiftCardId",
                table: "PaymentTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Sandbox",
                table: "PaymentTransactions",
                sql: "[Provider]=N'Sandbox'");
        }
    }
}
