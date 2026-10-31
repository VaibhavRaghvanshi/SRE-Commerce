IF DB_NAME() <> N'SRECommerce_CatalogDB' THROW 50001, 'Wrong Catalog migration database', 1;
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
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [AttributeDefinitions] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(120) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [DataType] int NOT NULL,
        [Options] nvarchar(max) NOT NULL,
        [Unit] nvarchar(30) NULL,
        [Archived] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_AttributeDefinitions] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_AttributeDefinitions_Options] CHECK (ISJSON([Options])=1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
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
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [Brands] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [Archived] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Brands] PRIMARY KEY ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [CatalogImports] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Source] nvarchar(80) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [Result] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CatalogImports] PRIMARY KEY ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [Categories] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [Archived] bit NOT NULL,
        [BindingRevision] int NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_Categories_Categories_TenantId_ParentId] FOREIGN KEY ([TenantId], [ParentId]) REFERENCES [Categories] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
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
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
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
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [ProductCollections] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [Archived] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ProductCollections] PRIMARY KEY ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [CategoryAttributeMappings] (
        [TenantId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [AttributeId] uniqueidentifier NOT NULL,
        [Required] bit NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_CategoryAttributeMappings] PRIMARY KEY ([TenantId], [CategoryId], [AttributeId]),
        CONSTRAINT [FK_CategoryAttributeMappings_AttributeDefinitions_TenantId_AttributeId] FOREIGN KEY ([TenantId], [AttributeId]) REFERENCES [AttributeDefinitions] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CategoryAttributeMappings_Categories_TenantId_CategoryId] FOREIGN KEY ([TenantId], [CategoryId]) REFERENCES [Categories] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [Products] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [BrandId] uniqueidentifier NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [SeoTitle] nvarchar(160) NOT NULL,
        [SeoDescription] nvarchar(320) NOT NULL,
        [Kind] int NOT NULL,
        [Status] int NOT NULL,
        [Visible] bit NOT NULL,
        [B2BOnly] bit NOT NULL,
        [Attributes] nvarchar(max) NOT NULL,
        [Specifications] nvarchar(max) NOT NULL,
        [PublishedRevision] int NOT NULL,
        [Version] rowversion NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Products_Attributes] CHECK (ISJSON([Attributes])=1),
        CONSTRAINT [CK_Products_Specifications] CHECK (ISJSON([Specifications])=1),
        CONSTRAINT [FK_Products_Brands_TenantId_BrandId] FOREIGN KEY ([TenantId], [BrandId]) REFERENCES [Brands] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_Categories_TenantId_CategoryId] FOREIGN KEY ([TenantId], [CategoryId]) REFERENCES [Categories] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [CollectionProducts] (
        [TenantId] uniqueidentifier NOT NULL,
        [CollectionId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Order] int NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_CollectionProducts] PRIMARY KEY ([TenantId], [CollectionId], [ProductId]),
        CONSTRAINT [FK_CollectionProducts_ProductCollections_TenantId_CollectionId] FOREIGN KEY ([TenantId], [CollectionId]) REFERENCES [ProductCollections] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollectionProducts_Products_TenantId_ProductId] FOREIGN KEY ([TenantId], [ProductId]) REFERENCES [Products] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [ProductMedia] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Url] nvarchar(1024) NOT NULL,
        [Alt] nvarchar(200) NOT NULL,
        [Order] int NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_ProductMedia] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_ProductMedia_Products_TenantId_ProductId] FOREIGN KEY ([TenantId], [ProductId]) REFERENCES [Products] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [ProductPublications] (
        [TenantId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [BrandId] uniqueidentifier NULL,
        [Visible] bit NOT NULL,
        [B2BOnly] bit NOT NULL,
        [Document] nvarchar(max) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [PublishedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProductPublications] PRIMARY KEY ([TenantId], [ProductId], [Revision]),
        CONSTRAINT [CK_ProductPublications_Document] CHECK (ISJSON([Document])=1),
        CONSTRAINT [FK_ProductPublications_Products_TenantId_ProductId] FOREIGN KEY ([TenantId], [ProductId]) REFERENCES [Products] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [ProductRelationships] (
        [TenantId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [RelatedProductId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(30) NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_ProductRelationships] PRIMARY KEY ([TenantId], [ProductId], [RelatedProductId]),
        CONSTRAINT [FK_ProductRelationships_Products_TenantId_ProductId] FOREIGN KEY ([TenantId], [ProductId]) REFERENCES [Products] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductRelationships_Products_TenantId_RelatedProductId] FOREIGN KEY ([TenantId], [RelatedProductId]) REFERENCES [Products] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Sku] nvarchar(64) NOT NULL,
        [Barcode] nvarchar(64) NULL,
        [Options] nvarchar(4000) NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ProductVariants_Options] CHECK (ISJSON([Options])=1),
        CONSTRAINT [FK_ProductVariants_Products_TenantId_ProductId] FOREIGN KEY ([TenantId], [ProductId]) REFERENCES [Products] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE TABLE [ProductBundles] (
        [TenantId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ComponentVariantId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_ProductBundles] PRIMARY KEY ([TenantId], [ProductId], [ComponentVariantId]),
        CONSTRAINT [CK_ProductBundles_Quantity] CHECK ([Quantity]>0),
        CONSTRAINT [FK_ProductBundles_ProductVariants_TenantId_ComponentVariantId] FOREIGN KEY ([TenantId], [ComponentVariantId]) REFERENCES [ProductVariants] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductBundles_Products_TenantId_ProductId] FOREIGN KEY ([TenantId], [ProductId]) REFERENCES [Products] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttributeDefinitions_TenantId_Code] ON [AttributeDefinitions] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Brands_TenantId_Slug] ON [Brands] ([TenantId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_Categories_TenantId_ParentId] ON [Categories] ([TenantId], [ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_TenantId_Slug] ON [Categories] ([TenantId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_CategoryAttributeMappings_TenantId_AttributeId] ON [CategoryAttributeMappings] ([TenantId], [AttributeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_CollectionProducts_TenantId_ProductId] ON [CollectionProducts] ([TenantId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_ProductBundles_TenantId_ComponentVariantId] ON [ProductBundles] ([TenantId], [ComponentVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductCollections_TenantId_Slug] ON [ProductCollections] ([TenantId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_ProductMedia_TenantId_ProductId] ON [ProductMedia] ([TenantId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_ProductPublications_TenantId_Slug] ON [ProductPublications] ([TenantId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_ProductRelationships_TenantId_RelatedProductId] ON [ProductRelationships] ([TenantId], [RelatedProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_Products_TenantId_BrandId] ON [Products] ([TenantId], [BrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_Products_TenantId_CategoryId_Status] ON [Products] ([TenantId], [CategoryId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_TenantId_Slug] ON [Products] ([TenantId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProductVariants_TenantId_Barcode] ON [ProductVariants] ([TenantId], [Barcode]) WHERE [Barcode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_TenantId_ProductId] ON [ProductVariants] ([TenantId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductVariants_TenantId_Sku] ON [ProductVariants] ([TenantId], [Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031121126_InitialCatalog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031121126_InitialCatalog', N'10.0.14');
END;

COMMIT;
GO
