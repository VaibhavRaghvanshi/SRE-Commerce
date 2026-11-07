SET XACT_ABORT ON;

IF DB_NAME() <> N'SRECommerce_CartDB'
    THROW 50001, 'Wrong Cart migration database', 1;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20261104161239_CustomerAccount'
)
    THROW 50002, 'Apply the earlier Cart customer-account migration first', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory WITH (UPDLOCK, HOLDLOCK)
    WHERE MigrationId = N'20261107151130_SavedCartReminders'
)
BEGIN
    CREATE TABLE dbo.CartReminders (
        TenantId uniqueidentifier NOT NULL,
        CartId uniqueidentifier NOT NULL,
        RequestedAt datetimeoffset NOT NULL,
        Enabled bit NOT NULL,
        QueuedEventId uniqueidentifier NULL,
        QueuedAt datetimeoffset NULL,
        Version rowversion NOT NULL,
        CONSTRAINT PK_CartReminders PRIMARY KEY (TenantId, CartId),
        CONSTRAINT CK_CartReminders_Queue CHECK (
            (QueuedAt IS NULL AND QueuedEventId IS NULL) OR
            (QueuedAt IS NOT NULL AND QueuedEventId IS NOT NULL)
        ),
        CONSTRAINT FK_CartReminders_Carts_TenantId_CartId
            FOREIGN KEY (TenantId, CartId) REFERENCES dbo.Carts (TenantId, Id)
    );

    CREATE INDEX IX_CartReminders_RequestedAt ON dbo.CartReminders (RequestedAt)
        WHERE Enabled = 1 AND QueuedAt IS NULL;

    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261107151130_SavedCartReminders', N'10.0.14');
END;

COMMIT;
