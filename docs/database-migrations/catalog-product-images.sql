IF DB_NAME() <> N'SRECommerce_CatalogDB' THROW 50001, 'Wrong Catalog migration database', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161206_ProductImageAssets'
)
BEGIN
    CREATE TABLE [MediaAssets] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ContentType] nvarchar(40) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [Length] int NOT NULL,
        [Width] int NOT NULL,
        [Height] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MediaAssets] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_MediaAssets_Size] CHECK ([Length] BETWEEN 45 AND 750000 AND [Width] BETWEEN 1 AND 8192 AND [Height] BETWEEN 1 AND 8192)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161206_ProductImageAssets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104161206_ProductImageAssets', N'10.0.14');
END;

COMMIT;
GO
