using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Payment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorService = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SupportReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationInbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Producer = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationInbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Envelope = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentProviderConfigurations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    AllowCard = table.Column<bool>(type: "bit", nullable: false),
                    AllowUPI = table.Column<bool>(type: "bit", nullable: false),
                    AllowNetBanking = table.Column<bool>(type: "bit", nullable: false),
                    AllowCOD = table.Column<bool>(type: "bit", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentProviderConfigurations", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PaymentConfiguration_Outcome", "[Outcome] BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Refunded = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancellationRequested = table.Column<bool>(type: "bit", nullable: false),
                    OrderProof = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_Payments_Amounts", "[Amount]>0 AND [Refunded]>=0 AND [Refunded]<=[Amount]");
                    table.CheckConstraint("CK_Payments_Proof", "ISJSON([OrderProof])=1");
                    table.CheckConstraint("CK_Payments_Revision", "[Revision]>0");
                    table.CheckConstraint("CK_Payments_Sandbox", "[Provider]=N'Sandbox'");
                    table.CheckConstraint("CK_Payments_Status", "[Status] BETWEEN 0 AND 7 AND ([Status] IN (5,6) OR [Refunded]=0) AND ([Status]<>6 OR [Refunded]=[Amount]) AND ([Status]<>5 OR ([Refunded]>0 AND [Refunded]<[Amount]))");
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Previous = table.Column<int>(type: "int", nullable: true),
                    Current = table.Column<int>(type: "int", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PaymentAttempts_PaymentTransactions_TenantId_PaymentId",
                        columns: x => new { x.TenantId, x.PaymentId },
                        principalTable: "PaymentTransactions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhooks",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Applied = table.Column<bool>(type: "bit", nullable: false),
                    ProviderTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhooks", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PaymentWebhooks_PaymentTransactions_TenantId_PaymentId",
                        columns: x => new { x.TenantId, x.PaymentId },
                        principalTable: "PaymentTransactions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundTransactions",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundTransactions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_Refunds_Amount", "[Amount]>0");
                    table.ForeignKey(
                        name: "FK_RefundTransactions_PaymentTransactions_TenantId_PaymentId",
                        columns: x => new { x.TenantId, x.PaymentId },
                        principalTable: "PaymentTransactions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId_Id",
                table: "AuditEntries",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationInbox_TenantId_ProcessedAt",
                table: "IntegrationInbox",
                columns: new[] { "TenantId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOutbox_LeaseUntil",
                table: "IntegrationOutbox",
                column: "LeaseUntil");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOutbox_PublishedAt_NextAttemptAt",
                table: "IntegrationOutbox",
                columns: new[] { "PublishedAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_TenantId_PaymentId_Sequence",
                table: "PaymentAttempts",
                columns: new[] { "TenantId", "PaymentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderConfigurations_TenantId",
                table: "PaymentProviderConfigurations",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TenantId_OrderId",
                table: "PaymentTransactions",
                columns: new[] { "TenantId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TenantId_ProviderReference",
                table: "PaymentTransactions",
                columns: new[] { "TenantId", "ProviderReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TenantId_Status_CreatedAt",
                table: "PaymentTransactions",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TenantId_UserId_CreatedAt",
                table: "PaymentTransactions",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhooks_TenantId_PaymentId_ReceivedAt",
                table: "PaymentWebhooks",
                columns: new[] { "TenantId", "PaymentId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefundTransactions_TenantId_PaymentId_CreatedAt",
                table: "RefundTransactions",
                columns: new[] { "TenantId", "PaymentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefundTransactions_TenantId_ProviderReference",
                table: "RefundTransactions",
                columns: new[] { "TenantId", "ProviderReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "IntegrationInbox");

            migrationBuilder.DropTable(
                name: "IntegrationOutbox");

            migrationBuilder.DropTable(
                name: "PaymentAttempts");

            migrationBuilder.DropTable(
                name: "PaymentProviderConfigurations");

            migrationBuilder.DropTable(
                name: "PaymentWebhooks");

            migrationBuilder.DropTable(
                name: "RefundTransactions");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");
        }
    }
}
