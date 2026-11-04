IF DB_NAME() <> N'SRECommerce_InventoryDB' THROW 50001, 'Wrong Inventory migration database', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161221_LowStockAlerts'
)
BEGIN
    CREATE TABLE [LowStockStates] (
        [TenantId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [Low] bit NOT NULL,
        [Episode] bigint NOT NULL,
        [Available] decimal(18,3) NOT NULL,
        [ObservedAt] datetimeoffset NOT NULL,
        [NotifiedAt] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_LowStockStates] PRIMARY KEY ([TenantId], [VariantId]),
        CONSTRAINT [CK_LowStockStates_Values] CHECK ([Episode]>=0 AND [Available]>=0),
        CONSTRAINT [FK_LowStockStates_StockItems_TenantId_VariantId] FOREIGN KEY ([TenantId], [VariantId]) REFERENCES [StockItems] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161221_LowStockAlerts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104161221_LowStockAlerts', N'10.0.14');
END;

COMMIT;
GO
