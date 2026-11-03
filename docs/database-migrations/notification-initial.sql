IF DB_NAME() <> N'SRECommerce_NotificationDB' THROW 51000, 'Wrong database for Notification migration.', 1;
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
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
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
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
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
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
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
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE TABLE [NotificationJobs] (
        [TenantId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [Channel] int NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Kind] int NOT NULL,
        [Recipient] nvarchar(254) NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Body] nvarchar(2400) NOT NULL,
        [State] int NOT NULL,
        [Attempts] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [LeaseId] uniqueidentifier NULL,
        [LeaseUntil] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationJobs] PRIMARY KEY ([TenantId], [EventId], [Channel]),
        CONSTRAINT [CK_NotificationJobs_State] CHECK ([State] BETWEEN 0 AND 5 AND [Attempts] BETWEEN 0 AND 5 AND [Channel] BETWEEN 0 AND 2)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE TABLE [NotificationPreferences] (
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Email] bit NOT NULL,
        [Sms] bit NOT NULL,
        [WhatsApp] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([TenantId], [UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE TABLE [NotificationTemplates] (
        [TenantId] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [Channel] int NOT NULL,
        [Subject] nvarchar(160) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [Enabled] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationTemplates] PRIMARY KEY ([TenantId], [Kind], [Channel])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE TABLE [NotificationDeliveries] (
        [TenantId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [Channel] int NOT NULL,
        [Attempt] int NOT NULL,
        [State] int NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [RecordedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_NotificationDeliveries] PRIMARY KEY ([TenantId], [EventId], [Channel], [Attempt]),
        CONSTRAINT [FK_NotificationDeliveries_NotificationJobs_TenantId_EventId_Channel] FOREIGN KEY ([TenantId], [EventId], [Channel]) REFERENCES [NotificationJobs] ([TenantId], [EventId], [Channel]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE INDEX [IX_NotificationJobs_State_NextAttemptAt_LeaseUntil] ON [NotificationJobs] ([State], [NextAttemptAt], [LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    CREATE INDEX [IX_NotificationJobs_TenantId_CreatedAt] ON [NotificationJobs] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103160732_InitialNotification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103160732_InitialNotification', N'10.0.14');
END;

COMMIT;
GO
