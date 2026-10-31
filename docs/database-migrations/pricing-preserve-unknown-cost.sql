BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031170016_PreserveUnknownSourceCost'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductPrices]') AND [c].[name] = N'CostPrice');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [ProductPrices] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [ProductPrices] ALTER COLUMN [CostPrice] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031170016_PreserveUnknownSourceCost'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031170016_PreserveUnknownSourceCost', N'10.0.14');
END;

COMMIT;
GO
