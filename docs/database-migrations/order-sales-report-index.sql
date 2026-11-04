IF DB_NAME() <> N'SRECommerce_OrderDB' THROW 50001, 'Wrong Order migration database', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161256_SalesReportIndex'
)
BEGIN
    CREATE INDEX [IX_Orders_TenantId_CreatedAt] ON [Orders] ([TenantId], [CreatedAt]) INCLUDE ([Currency], [Historical], [Status], [PaymentState], [Total], [Refunded]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161256_SalesReportIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104161256_SalesReportIndex', N'10.0.14');
END;

COMMIT;
GO
