BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060251_CheckoutCompensationAndReturns'
)
BEGIN
    ALTER TABLE [StockReservations] ADD [RestoreOperationId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060251_CheckoutCompensationAndReturns'
)
BEGIN
    ALTER TABLE [StockReservations] ADD [RestoredAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060251_CheckoutCompensationAndReturns'
)
BEGIN
    CREATE TABLE [ReservationCancellations] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [SourceService] nvarchar(40) NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ReservationCancellations] PRIMARY KEY ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060251_CheckoutCompensationAndReturns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101060251_CheckoutCompensationAndReturns', N'10.0.14');
END;

COMMIT;
GO
