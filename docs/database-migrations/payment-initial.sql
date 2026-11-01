IF DB_NAME() <> N'SRECommerce_PaymentDB' THROW 50001, 'Wrong service migration database', 1;
GO
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [TenantId] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [ActorService] nvarchar(40) NOT NULL,
        [Action] nvarchar(120) NOT NULL,
        [Resource] nvarchar(160) NOT NULL,
        [SupportReason] nvarchar(300) NULL,
        [CorrelationId] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE TABLE [IntegrationInbox] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Producer] nvarchar(40) NOT NULL,
        [Type] nvarchar(120) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [ProcessedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_IntegrationInbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE TABLE [IntegrationOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] nvarchar(120) NOT NULL,
        [Envelope] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [PublishedAt] datetimeoffset NULL,
        [Attempts] int NOT NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [LeaseId] uniqueidentifier NULL,
        [LeaseUntil] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_IntegrationOutbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE TABLE [PaymentProviderConfigurations] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Active] bit NOT NULL,
        [AllowCard] bit NOT NULL,
        [AllowUPI] bit NOT NULL,
        [AllowNetBanking] bit NOT NULL,
        [AllowCOD] bit NOT NULL,
        [Outcome] int NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_PaymentProviderConfigurations] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_PaymentConfiguration_Outcome] CHECK ([Outcome] BETWEEN 0 AND 2)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE TABLE [PaymentTransactions] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Refunded] decimal(18,4) NOT NULL,
        [Method] nvarchar(20) NOT NULL,
        [Provider] nvarchar(30) NOT NULL,
        [ProviderReference] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [CancellationRequested] bit NOT NULL,
        [OrderProof] nvarchar(2000) NOT NULL,
        [Revision] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Payments_Amounts] CHECK ([Amount]>0 AND [Refunded]>=0 AND [Refunded]<=[Amount]),
        CONSTRAINT [CK_Payments_Proof] CHECK (ISJSON([OrderProof])=1),
        CONSTRAINT [CK_Payments_Revision] CHECK ([Revision]>0),
        CONSTRAINT [CK_Payments_Sandbox] CHECK ([Provider]=N'Sandbox'),
        CONSTRAINT [CK_Payments_Status] CHECK ([Status] BETWEEN 0 AND 7 AND ([Status] IN (5,6) OR [Refunded]=0) AND ([Status]<>6 OR [Refunded]=[Amount]) AND ([Status]<>5 OR ([Refunded]>0 AND [Refunded]<[Amount])))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE TABLE [PaymentAttempts] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [Action] nvarchar(60) NOT NULL,
        [Previous] int NULL,
        [Current] int NOT NULL,
        [ProviderReference] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PaymentAttempts] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_PaymentAttempts_PaymentTransactions_TenantId_PaymentId] FOREIGN KEY ([TenantId], [PaymentId]) REFERENCES [PaymentTransactions] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE TABLE [PaymentWebhooks] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [Outcome] nvarchar(20) NOT NULL,
        [Applied] bit NOT NULL,
        [ProviderTimestamp] datetimeoffset NOT NULL,
        [ReceivedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PaymentWebhooks] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_PaymentWebhooks_PaymentTransactions_TenantId_PaymentId] FOREIGN KEY ([TenantId], [PaymentId]) REFERENCES [PaymentTransactions] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE TABLE [RefundTransactions] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [ProviderReference] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RefundTransactions] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Refunds_Amount] CHECK ([Amount]>0),
        CONSTRAINT [FK_RefundTransactions_PaymentTransactions_TenantId_PaymentId] FOREIGN KEY ([TenantId], [PaymentId]) REFERENCES [PaymentTransactions] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentAttempts_TenantId_PaymentId_Sequence] ON [PaymentAttempts] ([TenantId], [PaymentId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentProviderConfigurations_TenantId] ON [PaymentProviderConfigurations] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentTransactions_TenantId_OrderId] ON [PaymentTransactions] ([TenantId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentTransactions_TenantId_ProviderReference] ON [PaymentTransactions] ([TenantId], [ProviderReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_TenantId_Status_CreatedAt] ON [PaymentTransactions] ([TenantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_TenantId_UserId_CreatedAt] ON [PaymentTransactions] ([TenantId], [UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE INDEX [IX_PaymentWebhooks_TenantId_PaymentId_ReceivedAt] ON [PaymentWebhooks] ([TenantId], [PaymentId], [ReceivedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE INDEX [IX_RefundTransactions_TenantId_PaymentId_CreatedAt] ON [RefundTransactions] ([TenantId], [PaymentId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefundTransactions_TenantId_ProviderReference] ON [RefundTransactions] ([TenantId], [ProviderReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065605_InitialPayment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101065605_InitialPayment', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065905_PaymentCurrencyPrecision'
)
BEGIN
    ALTER TABLE [PaymentTransactions] ADD [DecimalPlaces] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065905_PaymentCurrencyPrecision'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentTransactions] ADD CONSTRAINT [CK_Payments_Precision] CHECK ([DecimalPlaces] BETWEEN 0 AND 3)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101065905_PaymentCurrencyPrecision'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101065905_PaymentCurrencyPrecision', N'10.0.14');
END;

COMMIT;
GO
