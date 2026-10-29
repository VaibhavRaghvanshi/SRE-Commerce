-- Tenant schema; generated from the reviewed InitialTenant EF migration.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'SRECommerce_TenantDB' THROW 50001, 'Wrong Tenant database', 1;
GO
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
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [BusinessTypes] (
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_BusinessTypes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [SubscriptionPlans] (
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [MaxLocations] int NOT NULL,
        [MaxDomains] int NOT NULL,
        [MaxUsers] int NOT NULL,
        CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [TenantPlatformAudit] (
        [Id] bigint NOT NULL IDENTITY,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [Resource] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenantPlatformAudit] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [RegistrationRequestId] uniqueidentifier NOT NULL,
        [RegistrationFingerprint] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(63) NOT NULL,
        [BusinessTypeCode] nvarchar(40) NOT NULL,
        [CustomBusinessType] nvarchar(100) NULL,
        [Status] int NOT NULL,
        [DatabaseStrategy] int NOT NULL,
        [SuspensionReason] nvarchar(300) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tenants_BusinessTypes_BusinessTypeCode] FOREIGN KEY ([BusinessTypeCode]) REFERENCES [BusinessTypes] ([Code]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [PlanFeatures] (
        [PlanCode] nvarchar(40) NOT NULL,
        [Feature] nvarchar(60) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_PlanFeatures] PRIMARY KEY ([PlanCode], [Feature]),
        CONSTRAINT [FK_PlanFeatures_SubscriptionPlans_PlanCode] FOREIGN KEY ([PlanCode]) REFERENCES [SubscriptionPlans] ([Code]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [TenantAuditHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [PlatformOperation] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenantAuditHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantAuditHistory_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [TenantDomains] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Host] nvarchar(253) NOT NULL,
        [VerificationHash] nvarchar(64) NOT NULL,
        [VerificationExpiresAt] datetimeoffset NOT NULL,
        [VerifiedAt] datetimeoffset NULL,
        [IsPrimary] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_TenantDomains] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantDomains_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [TenantLocations] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [CountryCode] nvarchar(2) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_TenantLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantLocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [TenantModules] (
        [TenantId] uniqueidentifier NOT NULL,
        [Module] nvarchar(40) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_TenantModules] PRIMARY KEY ([TenantId], [Module]),
        CONSTRAINT [FK_TenantModules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [TenantOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] nvarchar(100) NOT NULL,
        [Payload] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [DispatchedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TenantOutbox] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantOutbox_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [TenantSettings] (
        [TenantId] uniqueidentifier NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Culture] nvarchar(20) NOT NULL,
        [TimeZone] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(160) NOT NULL,
        [LogoUrl] nvarchar(500) NULL,
        CONSTRAINT [PK_TenantSettings] PRIMARY KEY ([TenantId]),
        CONSTRAINT [FK_TenantSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE TABLE [TenantSubscriptions] (
        [TenantId] uniqueidentifier NOT NULL,
        [PlanCode] nvarchar(40) NOT NULL,
        [Status] int NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [EndsAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenantSubscriptions] PRIMARY KEY ([TenantId]),
        CONSTRAINT [FK_TenantSubscriptions_SubscriptionPlans_PlanCode] FOREIGN KEY ([PlanCode]) REFERENCES [SubscriptionPlans] ([Code]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantSubscriptions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_TenantAuditHistory_TenantId_CreatedAt] ON [TenantAuditHistory] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantDomains_Host] ON [TenantDomains] ([Host]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TenantDomains_TenantId] ON [TenantDomains] ([TenantId]) WHERE [IsPrimary] = 1 AND [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_TenantLocations_TenantId_IsActive] ON [TenantLocations] ([TenantId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_TenantOutbox_DispatchedAt_CreatedAt] ON [TenantOutbox] ([DispatchedAt], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_TenantOutbox_TenantId] ON [TenantOutbox] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_TenantPlatformAudit_CreatedAt] ON [TenantPlatformAudit] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_Tenants_BusinessTypeCode] ON [Tenants] ([BusinessTypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_OwnerUserId_RegistrationRequestId] ON [Tenants] ([OwnerUserId], [RegistrationRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_Tenants_OwnerUserId_Status] ON [Tenants] ([OwnerUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_Slug] ON [Tenants] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    CREATE INDEX [IX_TenantSubscriptions_PlanCode] ON [TenantSubscriptions] ([PlanCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029143617_InitialTenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029143617_InitialTenant', N'10.0.14');
END;

COMMIT;
GO
