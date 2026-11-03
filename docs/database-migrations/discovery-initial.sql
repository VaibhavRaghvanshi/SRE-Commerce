IF DB_NAME() <> N'SRECommerce_DiscoveryDB' THROW 51000, 'Wrong database for Discovery migration.', 1;
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
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
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
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
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
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
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
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE TABLE [ProductReviews] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Rating] int NOT NULL,
        [Body] nvarchar(1500) NOT NULL,
        [State] int NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ProductReviews] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ProductReviews_State] CHECK ([Rating] BETWEEN 1 AND 5 AND [State] BETWEEN 0 AND 2)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE TABLE [SearchDocuments] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [BrandId] uniqueidentifier NULL,
        [Revision] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [SearchText] nvarchar(max) NOT NULL,
        [Attributes] nvarchar(max) NOT NULL,
        [IndexedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SearchDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SearchDocuments_Attributes] CHECK (ISJSON([Attributes])=1 AND [Revision]>0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE TABLE [ReviewDecisions] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ReviewId] uniqueidentifier NOT NULL,
        [ModeratorId] uniqueidentifier NOT NULL,
        [State] int NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ReviewDecisions] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_ReviewDecisions_ProductReviews_TenantId_ReviewId] FOREIGN KEY ([TenantId], [ReviewId]) REFERENCES [ProductReviews] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductReviews_TenantId_ProductId_UserId] ON [ProductReviews] ([TenantId], [ProductId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE INDEX [IX_ProductReviews_TenantId_State_ProductId_UpdatedAt] ON [ProductReviews] ([TenantId], [State], [ProductId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE INDEX [IX_ReviewDecisions_TenantId_ReviewId] ON [ReviewDecisions] ([TenantId], [ReviewId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE INDEX [IX_SearchDocuments_TenantId_CategoryId_BrandId] ON [SearchDocuments] ([TenantId], [CategoryId], [BrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SearchDocuments_TenantId_ProductId] ON [SearchDocuments] ([TenantId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161607_InitialDiscovery'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103161607_InitialDiscovery', N'10.0.14');
END;

COMMIT;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161608_SearchFullText'
)
BEGIN
    IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') <> 1
        THROW 51000, 'SQL Server Full-Text Search is required for Discovery.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name=N'DiscoveryFullText')
        EXEC(N'CREATE FULLTEXT CATALOG DiscoveryFullText');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161608_SearchFullText'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SearchDocuments'))
        EXEC(N'CREATE FULLTEXT INDEX ON dbo.SearchDocuments(SearchText LANGUAGE 0)
            KEY INDEX PK_SearchDocuments ON DiscoveryFullText WITH (CHANGE_TRACKING=AUTO, STOPLIST=OFF)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103161608_SearchFullText'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103161608_SearchFullText', N'10.0.14');
END;
GO
