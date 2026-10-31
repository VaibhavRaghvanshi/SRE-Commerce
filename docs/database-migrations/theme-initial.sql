IF DB_NAME() <> N'SRECommerce_ThemeDB' THROW 50001, 'Wrong Theme migration database', 1;
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
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
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
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
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
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
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
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE TABLE [ThemePresets] (
        [BusinessType] nvarchar(40) NOT NULL,
        [Document] nvarchar(max) NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ThemePresets] PRIMARY KEY ([BusinessType]),
        CONSTRAINT [CK_ThemePresets_Json] CHECK (ISJSON([Document]) = 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE TABLE [Themes] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Document] nvarchar(max) NOT NULL,
        [Archived] bit NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Themes] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Themes_Json] CHECK (ISJSON([Document]) = 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE TABLE [ThemeVersions] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ThemeId] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [Document] nvarchar(max) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ThemeVersions] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ThemeVersions_Json] CHECK (ISJSON([Document]) = 1),
        CONSTRAINT [FK_ThemeVersions_Themes_TenantId_ThemeId] FOREIGN KEY ([TenantId], [ThemeId]) REFERENCES [Themes] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE TABLE [TenantThemes] (
        [TenantId] uniqueidentifier NOT NULL,
        [ThemeVersionId] uniqueidentifier NOT NULL,
        [Sequence] bigint NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_TenantThemes] PRIMARY KEY ([TenantId]),
        CONSTRAINT [FK_TenantThemes_ThemeVersions_TenantId_ThemeVersionId] FOREIGN KEY ([TenantId], [ThemeVersionId]) REFERENCES [ThemeVersions] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE TABLE [ThemePublishHistory] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ThemeVersionId] uniqueidentifier NOT NULL,
        [Sequence] bigint NOT NULL,
        [Rollback] bit NOT NULL,
        [PublishedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ThemePublishHistory] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_ThemePublishHistory_ThemeVersions_TenantId_ThemeVersionId] FOREIGN KEY ([TenantId], [ThemeVersionId]) REFERENCES [ThemeVersions] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE INDEX [IX_TenantThemes_TenantId_ThemeVersionId] ON [TenantThemes] ([TenantId], [ThemeVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ThemePublishHistory_TenantId_Sequence] ON [ThemePublishHistory] ([TenantId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE INDEX [IX_ThemePublishHistory_TenantId_ThemeVersionId] ON [ThemePublishHistory] ([TenantId], [ThemeVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Themes_TenantId_Name] ON [Themes] ([TenantId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ThemeVersions_TenantId_ThemeId_Revision] ON [ThemeVersions] ([TenantId], [ThemeId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030144402_InitialTheme'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030144402_InitialTheme', N'10.0.14');
END;

COMMIT;
GO
