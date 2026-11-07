SET XACT_ABORT ON;

IF DB_NAME() <> N'SRECommerce_CatalogDB'
    THROW 50001, 'Wrong Catalog migration database', 1;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20261104161206_ProductImageAssets'
)
    THROW 50002, 'Apply the earlier Catalog image-assets migration first', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory WITH (UPDLOCK, HOLDLOCK)
    WHERE MigrationId = N'20261106022752_LocalizedProductContent'
)
BEGIN
    ALTER TABLE dbo.Products ADD Translations nvarchar(max) NOT NULL DEFAULT N'{}'
        CONSTRAINT CK_Products_Translations CHECK (ISJSON(Translations) = 1);

    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261106022752_LocalizedProductContent', N'10.0.14');
END;

COMMIT;
