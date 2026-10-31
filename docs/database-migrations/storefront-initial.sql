IF DB_NAME() <> N'SRECommerce_StorefrontDB' THROW 50001, 'Wrong Storefront migration database', 1;
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
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
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
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
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
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
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
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE TABLE [LayoutTemplates] (
        [BusinessType] nvarchar(40) NOT NULL,
        [Document] nvarchar(max) NOT NULL,
        [Version] rowversion NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LayoutTemplates] PRIMARY KEY ([BusinessType]),
        CONSTRAINT [CK_LayoutTemplates_Json] CHECK (ISJSON([Document]) = 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE TABLE [StorefrontPages] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Slug] nvarchar(80) NOT NULL,
        [Title] nvarchar(160) NOT NULL,
        [Document] nvarchar(max) NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_StorefrontPages] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_StorefrontPages_Json] CHECK (ISJSON([Document]) = 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE TABLE [LayoutVersions] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PageId] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [Document] nvarchar(max) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LayoutVersions] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [AK_LayoutVersions_TenantId_PageId_Id] UNIQUE ([TenantId], [PageId], [Id]),
        CONSTRAINT [CK_LayoutVersions_Json] CHECK (ISJSON([Document]) = 1),
        CONSTRAINT [FK_LayoutVersions_StorefrontPages_TenantId_PageId] FOREIGN KEY ([TenantId], [PageId]) REFERENCES [StorefrontPages] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE TABLE [LayoutPublishHistory] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PageId] uniqueidentifier NOT NULL,
        [LayoutVersionId] uniqueidentifier NOT NULL,
        [Sequence] bigint NOT NULL,
        [Rollback] bit NOT NULL,
        [PublishedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LayoutPublishHistory] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_LayoutPublishHistory_LayoutVersions_TenantId_PageId_LayoutVersionId] FOREIGN KEY ([TenantId], [PageId], [LayoutVersionId]) REFERENCES [LayoutVersions] ([TenantId], [PageId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE TABLE [PublishedConfigurations] (
        [TenantId] uniqueidentifier NOT NULL,
        [PageId] uniqueidentifier NOT NULL,
        [LayoutVersionId] uniqueidentifier NOT NULL,
        [Sequence] bigint NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_PublishedConfigurations] PRIMARY KEY ([TenantId], [PageId]),
        CONSTRAINT [FK_PublishedConfigurations_LayoutVersions_TenantId_PageId_LayoutVersionId] FOREIGN KEY ([TenantId], [PageId], [LayoutVersionId]) REFERENCES [LayoutVersions] ([TenantId], [PageId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE INDEX [IX_LayoutPublishHistory_TenantId_PageId_LayoutVersionId] ON [LayoutPublishHistory] ([TenantId], [PageId], [LayoutVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LayoutPublishHistory_TenantId_PageId_Sequence] ON [LayoutPublishHistory] ([TenantId], [PageId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LayoutVersions_TenantId_PageId_Revision] ON [LayoutVersions] ([TenantId], [PageId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE INDEX [IX_PublishedConfigurations_TenantId_PageId_LayoutVersionId] ON [PublishedConfigurations] ([TenantId], [PageId], [LayoutVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StorefrontPages_TenantId_Slug] ON [StorefrontPages] ([TenantId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030162006_InitialStorefront'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030162006_InitialStorefront', N'10.0.14');
END;

COMMIT;
GO
