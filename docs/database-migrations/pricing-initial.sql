IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [ActorService] nvarchar(40) NOT NULL,
        [Action] nvarchar(120) NOT NULL,
        [Resource] nvarchar(160) NOT NULL,
        [SupportReason] nvarchar(300) NULL,
        [CorrelationId] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [CustomerPriceGroups] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_CustomerPriceGroups] PRIMARY KEY ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [IntegrationInbox] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Producer] nvarchar(40) NOT NULL,
        [Type] nvarchar(120) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [ProcessedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_IntegrationInbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [IntegrationOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] nvarchar(120) NOT NULL,
        [Envelope] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [PublishedAt] datetimeoffset NULL,
        [Attempts] int NOT NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [LeaseId] uniqueidentifier NULL,
        [LeaseUntil] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_IntegrationOutbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [PricingSettings] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [DecimalPlaces] int NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_PricingSettings] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_PricingSettings_Precision] CHECK ([DecimalPlaces] BETWEEN 0 AND 3)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [TaxPolicies] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Rate] decimal(7,4) NOT NULL,
        [Inclusive] bit NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_TaxPolicies] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_TaxPolicies_Rate] CHECK ([Rate]>=0 AND [Rate]<=100)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [CustomerPriceGroupMembers] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_CustomerPriceGroupMembers] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_CustomerPriceGroupMembers_CustomerPriceGroups_TenantId_GroupId] FOREIGN KEY ([TenantId], [GroupId]) REFERENCES [CustomerPriceGroups] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [PriceLists] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Audience] int NOT NULL,
        [CustomerGroupId] uniqueidentifier NULL,
        [Priority] int NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [EndsAt] datetimeoffset NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_PriceLists] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_PriceLists_Audience] CHECK (([Audience]=2 AND [CustomerGroupId] IS NOT NULL) OR ([Audience] IN (0,1) AND [CustomerGroupId] IS NULL)),
        CONSTRAINT [CK_PriceLists_Window] CHECK ([EndsAt]>[StartsAt]),
        CONSTRAINT [FK_PriceLists_CustomerPriceGroups_TenantId_CustomerGroupId] FOREIGN KEY ([TenantId], [CustomerGroupId]) REFERENCES [CustomerPriceGroups] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [Promotions] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Kind] int NOT NULL,
        [Value] decimal(18,4) NOT NULL,
        [BuyQuantity] int NOT NULL,
        [GetQuantity] int NOT NULL,
        [MinimumSpend] decimal(18,4) NOT NULL,
        [MaximumDiscount] decimal(18,4) NULL,
        [VariantIds] nvarchar(max) NOT NULL,
        [CustomerGroupId] uniqueidentifier NULL,
        [B2BOnly] bit NOT NULL,
        [Automatic] bit NOT NULL,
        [Priority] int NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [EndsAt] datetimeoffset NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Promotions] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Promotions_Amounts] CHECK ([Value]>=0 AND [MinimumSpend]>=0 AND ([MaximumDiscount] IS NULL OR [MaximumDiscount]>=0)),
        CONSTRAINT [CK_Promotions_Targets] CHECK (ISJSON([VariantIds])=1),
        CONSTRAINT [CK_Promotions_Window] CHECK ([EndsAt]>[StartsAt]),
        CONSTRAINT [FK_Promotions_CustomerPriceGroups_TenantId_CustomerGroupId] FOREIGN KEY ([TenantId], [CustomerGroupId]) REFERENCES [CustomerPriceGroups] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [ProductPrices] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PriceListId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [Sku] nvarchar(64) NOT NULL,
        [IsBundle] bit NOT NULL,
        [TaxPolicyId] uniqueidentifier NOT NULL,
        [MinimumQuantity] decimal(18,3) NOT NULL,
        [Mrp] decimal(18,4) NOT NULL,
        [SellingPrice] decimal(18,4) NOT NULL,
        [CostPrice] decimal(18,4) NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ProductPrices] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ProductPrices_Amounts] CHECK ([MinimumQuantity]>0 AND [Mrp]>=0 AND [SellingPrice]>=0 AND [SellingPrice]<=[Mrp] AND [CostPrice]>=0),
        CONSTRAINT [FK_ProductPrices_PriceLists_TenantId_PriceListId] FOREIGN KEY ([TenantId], [PriceListId]) REFERENCES [PriceLists] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductPrices_TaxPolicies_TenantId_TaxPolicyId] FOREIGN KEY ([TenantId], [TaxPolicyId]) REFERENCES [TaxPolicies] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [Coupons] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PromotionId] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [GlobalLimit] int NOT NULL,
        [PerCustomerLimit] int NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Coupons] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Coupons_Limits] CHECK ([GlobalLimit]>0 AND [PerCustomerLimit]>=0 AND [PerCustomerLimit]<=[GlobalLimit]),
        CONSTRAINT [FK_Coupons_Promotions_TenantId_PromotionId] FOREIGN KEY ([TenantId], [PromotionId]) REFERENCES [Promotions] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [PriceHistory] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PriceId] uniqueidentifier NOT NULL,
        [Before] nvarchar(max) NOT NULL,
        [After] nvarchar(max) NOT NULL,
        [ChangedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PriceHistory] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_PriceHistory_After] CHECK (ISJSON([After])=1),
        CONSTRAINT [CK_PriceHistory_Before] CHECK (ISJSON([Before])=1 OR [Before]=N'null'),
        CONSTRAINT [FK_PriceHistory_ProductPrices_TenantId_PriceId] FOREIGN KEY ([TenantId], [PriceId]) REFERENCES [ProductPrices] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [PricingQuotes] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [CustomerGroupId] uniqueidentifier NULL,
        [GroupContextHash] nvarchar(64) NOT NULL,
        [UserId] uniqueidentifier NULL,
        [CouponId] uniqueidentifier NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [Document] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PricingQuotes] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_PricingQuotes_Document] CHECK (ISJSON([Document])=1),
        CONSTRAINT [FK_PricingQuotes_Coupons_TenantId_CouponId] FOREIGN KEY ([TenantId], [CouponId]) REFERENCES [Coupons] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PricingQuotes_CustomerPriceGroups_TenantId_CustomerGroupId] FOREIGN KEY ([TenantId], [CustomerGroupId]) REFERENCES [CustomerPriceGroups] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE TABLE [CouponRedemptions] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [QuoteId] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_CouponRedemptions] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_CouponRedemptions_Coupons_TenantId_CouponId] FOREIGN KEY ([TenantId], [CouponId]) REFERENCES [Coupons] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CouponRedemptions_PricingQuotes_TenantId_QuoteId] FOREIGN KEY ([TenantId], [QuoteId]) REFERENCES [PricingQuotes] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_CouponRedemptions_TenantId_CouponId_Status_ExpiresAt] ON [CouponRedemptions] ([TenantId], [CouponId], [Status], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_CouponRedemptions_TenantId_CouponId_UserId_Status] ON [CouponRedemptions] ([TenantId], [CouponId], [UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CouponRedemptions_TenantId_QuoteId] ON [CouponRedemptions] ([TenantId], [QuoteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Coupons_TenantId_Code] ON [Coupons] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_Coupons_TenantId_PromotionId] ON [Coupons] ([TenantId], [PromotionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_CustomerPriceGroupMembers_TenantId_GroupId] ON [CustomerPriceGroupMembers] ([TenantId], [GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerPriceGroupMembers_TenantId_UserId] ON [CustomerPriceGroupMembers] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerPriceGroups_TenantId_Code] ON [CustomerPriceGroups] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_PriceHistory_TenantId_PriceId_ChangedAt] ON [PriceHistory] ([TenantId], [PriceId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PriceLists_TenantId_Code] ON [PriceLists] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_PriceLists_TenantId_CustomerGroupId] ON [PriceLists] ([TenantId], [CustomerGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_PricingQuotes_ExpiresAt] ON [PricingQuotes] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_PricingQuotes_TenantId_CouponId] ON [PricingQuotes] ([TenantId], [CouponId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_PricingQuotes_TenantId_CustomerGroupId] ON [PricingQuotes] ([TenantId], [CustomerGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_PricingQuotes_TenantId_UserId_CreatedAt] ON [PricingQuotes] ([TenantId], [UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PricingSettings_TenantId] ON [PricingSettings] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductPrices_TenantId_PriceListId_VariantId_MinimumQuantity] ON [ProductPrices] ([TenantId], [PriceListId], [VariantId], [MinimumQuantity]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_ProductPrices_TenantId_TaxPolicyId] ON [ProductPrices] ([TenantId], [TaxPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_ProductPrices_TenantId_VariantId_Active] ON [ProductPrices] ([TenantId], [VariantId], [Active]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Promotions_TenantId_Code] ON [Promotions] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE INDEX [IX_Promotions_TenantId_CustomerGroupId] ON [Promotions] ([TenantId], [CustomerGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaxPolicies_TenantId_Code] ON [TaxPolicies] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031161247_InitialPricing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031161247_InitialPricing', N'10.0.14');
END;

COMMIT;
GO
