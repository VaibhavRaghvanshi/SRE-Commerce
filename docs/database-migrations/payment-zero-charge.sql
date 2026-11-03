IF DB_NAME() <> N'SRECommerce_PaymentDB' THROW 50001, 'Wrong service migration database', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160504_ZeroChargeOrders'
)
BEGIN
    ALTER TABLE [PaymentTransactions] DROP CONSTRAINT [CK_Payments_Amounts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160504_ZeroChargeOrders'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentTransactions] ADD CONSTRAINT [CK_Payments_Amounts] CHECK ([Amount]>=0 AND [Refunded]>=0 AND [Refunded]<=[Amount])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160504_ZeroChargeOrders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101160504_ZeroChargeOrders', N'10.0.14');
END;

COMMIT;
GO
