IF DB_NAME() <> N'SRECommerce_NotificationDB' THROW 50001, 'Wrong Notification migration database', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161309_StockAlertSubscriptions'
)
BEGIN
    CREATE TABLE [StockAlertReceipts] (
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [QueuedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StockAlertReceipts] PRIMARY KEY ([TenantId], [UserId], [VariantId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161309_StockAlertSubscriptions'
)
BEGIN
    CREATE TABLE [StockAlertSubscriptions] (
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Enabled] bit NOT NULL,
        [CooldownMinutes] int NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_StockAlertSubscriptions] PRIMARY KEY ([TenantId], [UserId]),
        CONSTRAINT [CK_StockAlertSubscriptions_Cooldown] CHECK ([CooldownMinutes] BETWEEN 60 AND 10080)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161309_StockAlertSubscriptions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104161309_StockAlertSubscriptions', N'10.0.14');
END;

COMMIT;
GO
