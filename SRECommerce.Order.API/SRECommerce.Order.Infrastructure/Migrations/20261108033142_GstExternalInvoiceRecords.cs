using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GstExternalInvoiceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GstInvoiceRecords",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierGstin = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    FinancialYearStart = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Irn = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Document = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GstInvoiceRecords", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_GstInvoiceRecords_Document", "ISJSON([Document])=1");
                    table.ForeignKey(
                        name: "FK_GstInvoiceRecords_Orders_TenantId_Id",
                        columns: x => new { x.TenantId, x.Id },
                        principalTable: "Orders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GstInvoiceRecords_TenantId_Irn",
                table: "GstInvoiceRecords",
                columns: new[] { "TenantId", "Irn" },
                unique: true,
                filter: "[Irn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GstInvoiceRecords_TenantId_SupplierGstin_FinancialYearStart_Number",
                table: "GstInvoiceRecords",
                columns: new[] { "TenantId", "SupplierGstin", "FinancialYearStart", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GstInvoiceRecords");
        }
    }
}
