using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Pricing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPricing : Migration
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
                name: "CustomerPriceGroups",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPriceGroups", x => new { x.TenantId, x.Id });
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
                name: "PricingSettings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingSettings", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PricingSettings_Precision", "[DecimalPlaces] BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "TaxPolicies",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Inclusive = table.Column<bool>(type: "bit", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPolicies", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_TaxPolicies_Rate", "[Rate]>=0 AND [Rate]<=100");
                });

            migrationBuilder.CreateTable(
                name: "CustomerPriceGroupMembers",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPriceGroupMembers", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CustomerPriceGroupMembers_CustomerPriceGroups_TenantId_GroupId",
                        columns: x => new { x.TenantId, x.GroupId },
                        principalTable: "CustomerPriceGroups",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceLists",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    CustomerGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PriceLists_Audience", "([Audience]=2 AND [CustomerGroupId] IS NOT NULL) OR ([Audience] IN (0,1) AND [CustomerGroupId] IS NULL)");
                    table.CheckConstraint("CK_PriceLists_Window", "[EndsAt]>[StartsAt]");
                    table.ForeignKey(
                        name: "FK_PriceLists_CustomerPriceGroups_TenantId_CustomerGroupId",
                        columns: x => new { x.TenantId, x.CustomerGroupId },
                        principalTable: "CustomerPriceGroups",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BuyQuantity = table.Column<int>(type: "int", nullable: false),
                    GetQuantity = table.Column<int>(type: "int", nullable: false),
                    MinimumSpend = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MaximumDiscount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    VariantIds = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    CustomerGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    B2BOnly = table.Column<bool>(type: "bit", nullable: false),
                    Automatic = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_Promotions_Amounts", "[Value]>=0 AND [MinimumSpend]>=0 AND ([MaximumDiscount] IS NULL OR [MaximumDiscount]>=0)");
                    table.CheckConstraint("CK_Promotions_Targets", "ISJSON([VariantIds])=1");
                    table.CheckConstraint("CK_Promotions_Window", "[EndsAt]>[StartsAt]");
                    table.ForeignKey(
                        name: "FK_Promotions_CustomerPriceGroups_TenantId_CustomerGroupId",
                        columns: x => new { x.TenantId, x.CustomerGroupId },
                        principalTable: "CustomerPriceGroups",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductPrices",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsBundle = table.Column<bool>(type: "bit", nullable: false),
                    TaxPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Mrp = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrices", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProductPrices_Amounts", "[MinimumQuantity]>0 AND [Mrp]>=0 AND [SellingPrice]>=0 AND [SellingPrice]<=[Mrp] AND [CostPrice]>=0");
                    table.ForeignKey(
                        name: "FK_ProductPrices_PriceLists_TenantId_PriceListId",
                        columns: x => new { x.TenantId, x.PriceListId },
                        principalTable: "PriceLists",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPrices_TaxPolicies_TenantId_TaxPolicyId",
                        columns: x => new { x.TenantId, x.TaxPolicyId },
                        principalTable: "TaxPolicies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GlobalLimit = table.Column<int>(type: "int", nullable: false),
                    PerCustomerLimit = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_Coupons_Limits", "[GlobalLimit]>0 AND [PerCustomerLimit]>=0 AND [PerCustomerLimit]<=[GlobalLimit]");
                    table.ForeignKey(
                        name: "FK_Coupons_Promotions_TenantId_PromotionId",
                        columns: x => new { x.TenantId, x.PromotionId },
                        principalTable: "Promotions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceHistory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Before = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    After = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistory", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PriceHistory_After", "ISJSON([After])=1");
                    table.CheckConstraint("CK_PriceHistory_Before", "ISJSON([Before])=1 OR [Before]=N'null'");
                    table.ForeignKey(
                        name: "FK_PriceHistory_ProductPrices_TenantId_PriceId",
                        columns: x => new { x.TenantId, x.PriceId },
                        principalTable: "ProductPrices",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingQuotes",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GroupContextHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Document = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingQuotes", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PricingQuotes_Document", "ISJSON([Document])=1");
                    table.ForeignKey(
                        name: "FK_PricingQuotes_Coupons_TenantId_CouponId",
                        columns: x => new { x.TenantId, x.CouponId },
                        principalTable: "Coupons",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingQuotes_CustomerPriceGroups_TenantId_CustomerGroupId",
                        columns: x => new { x.TenantId, x.CustomerGroupId },
                        principalTable: "CustomerPriceGroups",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CouponRedemptions",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouponRedemptions", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CouponRedemptions_Coupons_TenantId_CouponId",
                        columns: x => new { x.TenantId, x.CouponId },
                        principalTable: "Coupons",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CouponRedemptions_PricingQuotes_TenantId_QuoteId",
                        columns: x => new { x.TenantId, x.QuoteId },
                        principalTable: "PricingQuotes",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId_Id",
                table: "AuditEntries",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_TenantId_CouponId_Status_ExpiresAt",
                table: "CouponRedemptions",
                columns: new[] { "TenantId", "CouponId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_TenantId_CouponId_UserId_Status",
                table: "CouponRedemptions",
                columns: new[] { "TenantId", "CouponId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_TenantId_QuoteId",
                table: "CouponRedemptions",
                columns: new[] { "TenantId", "QuoteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_TenantId_Code",
                table: "Coupons",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_TenantId_PromotionId",
                table: "Coupons",
                columns: new[] { "TenantId", "PromotionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPriceGroupMembers_TenantId_GroupId",
                table: "CustomerPriceGroupMembers",
                columns: new[] { "TenantId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPriceGroupMembers_TenantId_UserId",
                table: "CustomerPriceGroupMembers",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPriceGroups_TenantId_Code",
                table: "CustomerPriceGroups",
                columns: new[] { "TenantId", "Code" },
                unique: true);

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
                name: "IX_PriceHistory_TenantId_PriceId_ChangedAt",
                table: "PriceHistory",
                columns: new[] { "TenantId", "PriceId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_Code",
                table: "PriceLists",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_CustomerGroupId",
                table: "PriceLists",
                columns: new[] { "TenantId", "CustomerGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingQuotes_ExpiresAt",
                table: "PricingQuotes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PricingQuotes_TenantId_CouponId",
                table: "PricingQuotes",
                columns: new[] { "TenantId", "CouponId" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingQuotes_TenantId_CustomerGroupId",
                table: "PricingQuotes",
                columns: new[] { "TenantId", "CustomerGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingQuotes_TenantId_UserId_CreatedAt",
                table: "PricingQuotes",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingSettings_TenantId",
                table: "PricingSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_TenantId_PriceListId_VariantId_MinimumQuantity",
                table: "ProductPrices",
                columns: new[] { "TenantId", "PriceListId", "VariantId", "MinimumQuantity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_TenantId_TaxPolicyId",
                table: "ProductPrices",
                columns: new[] { "TenantId", "TaxPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_TenantId_VariantId_Active",
                table: "ProductPrices",
                columns: new[] { "TenantId", "VariantId", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_TenantId_Code",
                table: "Promotions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_TenantId_CustomerGroupId",
                table: "Promotions",
                columns: new[] { "TenantId", "CustomerGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPolicies_TenantId_Code",
                table: "TaxPolicies",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "CouponRedemptions");

            migrationBuilder.DropTable(
                name: "CustomerPriceGroupMembers");

            migrationBuilder.DropTable(
                name: "IntegrationInbox");

            migrationBuilder.DropTable(
                name: "IntegrationOutbox");

            migrationBuilder.DropTable(
                name: "PriceHistory");

            migrationBuilder.DropTable(
                name: "PricingSettings");

            migrationBuilder.DropTable(
                name: "PricingQuotes");

            migrationBuilder.DropTable(
                name: "ProductPrices");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropTable(
                name: "PriceLists");

            migrationBuilder.DropTable(
                name: "TaxPolicies");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropTable(
                name: "CustomerPriceGroups");
        }
    }
}
