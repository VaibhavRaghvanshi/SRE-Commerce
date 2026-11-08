SET XACT_ABORT ON;

IF DB_NAME() <> N'SRECommerce_PaymentDB'
    THROW 50001, 'Wrong Payment migration database', 1;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261101160504_ZeroChargeOrders'
)
    THROW 50002, 'Apply the preceding Payment zero-charge migration first', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory WITH (UPDLOCK, HOLDLOCK)
    WHERE MigrationId = N'20261107164115_GiftCardsAndCounterPayments'
)
BEGIN
    CREATE TABLE dbo.GiftCards (
        TenantId uniqueidentifier NOT NULL,
        Id uniqueidentifier NOT NULL,
        IssuedBy uniqueidentifier NOT NULL,
        ClaimedBy uniqueidentifier NULL,
        CodeHash varchar(64) NOT NULL,
        Currency nvarchar(3) NOT NULL,
        Value decimal(18,4) NOT NULL,
        Balance decimal(18,4) NOT NULL,
        Reference nvarchar(300) NOT NULL,
        IssuedAt datetimeoffset NOT NULL,
        ClaimedAt datetimeoffset NULL,
        VoidedAt datetimeoffset NULL,
        Version rowversion NOT NULL,
        CONSTRAINT PK_GiftCards PRIMARY KEY (TenantId, Id),
        CONSTRAINT CK_GiftCards_Balance CHECK (Value > 0 AND Balance >= 0 AND Balance <= Value),
        CONSTRAINT CK_GiftCards_State CHECK (
            ((ClaimedBy IS NULL AND ClaimedAt IS NULL) OR (ClaimedBy IS NOT NULL AND ClaimedAt IS NOT NULL))
            AND (VoidedAt IS NULL OR (ClaimedBy IS NULL AND Balance = 0))
        )
    );
    CREATE UNIQUE INDEX IX_GiftCards_TenantId_CodeHash ON dbo.GiftCards (TenantId, CodeHash);
    CREATE INDEX IX_GiftCards_TenantId_ClaimedBy_IssuedAt ON dbo.GiftCards (TenantId, ClaimedBy, IssuedAt);

    ALTER TABLE dbo.PaymentTransactions ADD GiftCardId uniqueidentifier NULL;
    EXEC(N'
        CREATE INDEX IX_PaymentTransactions_TenantId_GiftCardId ON dbo.PaymentTransactions (TenantId, GiftCardId);
        ALTER TABLE dbo.PaymentTransactions ADD CONSTRAINT FK_PaymentTransactions_GiftCards_TenantId_GiftCardId
            FOREIGN KEY (TenantId, GiftCardId) REFERENCES dbo.GiftCards (TenantId, Id);
        ALTER TABLE dbo.PaymentTransactions ADD CONSTRAINT CK_Payments_Tender CHECK (
            (Provider = N''Sandbox'' AND Method NOT IN (N''GiftCard'', N''CounterCash'') AND GiftCardId IS NULL)
            OR (Provider = N''GiftCard'' AND Method = N''GiftCard'' AND (Status IN (3,4) OR GiftCardId IS NOT NULL))
            OR (Provider = N''Counter'' AND Method = N''CounterCash'' AND GiftCardId IS NULL)
        );');
    ALTER TABLE dbo.PaymentTransactions DROP CONSTRAINT CK_Payments_Sandbox;

    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261107164115_GiftCardsAndCounterPayments', N'10.0.14');
END;

COMMIT;
