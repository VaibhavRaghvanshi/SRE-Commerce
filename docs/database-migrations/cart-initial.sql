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

CREATE TABLE [Carts] (
    [TenantId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [MergedIntoId] uniqueidentifier NULL,
    [Revision] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Carts] PRIMARY KEY ([TenantId], [Id]),
    CONSTRAINT [CK_Carts_Revision] CHECK ([Revision]>=0),
    CONSTRAINT [CK_Carts_State] CHECK ([Status] BETWEEN 0 AND 3)
);

CREATE TABLE [IntegrationInbox] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Producer] nvarchar(40) NOT NULL,
    [Type] nvarchar(120) NOT NULL,
    [PayloadHash] nvarchar(64) NOT NULL,
    [ProcessedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_IntegrationInbox] PRIMARY KEY ([Id])
);

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

CREATE TABLE [CartItems] (
    [TenantId] uniqueidentifier NOT NULL,
    [CartId] uniqueidentifier NOT NULL,
    [VariantId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [Sku] nvarchar(64) NOT NULL,
    [Name] nvarchar(160) NOT NULL,
    [Slug] nvarchar(120) NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_CartItems] PRIMARY KEY ([TenantId], [CartId], [VariantId]),
    CONSTRAINT [CK_CartItems_Quantity] CHECK ([Quantity]>0 AND [Quantity]<=1000),
    CONSTRAINT [FK_CartItems_Carts_TenantId_CartId] FOREIGN KEY ([TenantId], [CartId]) REFERENCES [Carts] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [CartOperations] (
    [TenantId] uniqueidentifier NOT NULL,
    [CartId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [Hash] nvarchar(64) NOT NULL,
    [Result] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CartOperations] PRIMARY KEY ([TenantId], [CartId], [Id]),
    CONSTRAINT [CK_CartOperations_Result] CHECK (ISJSON([Result])=1),
    CONSTRAINT [FK_CartOperations_Carts_TenantId_CartId] FOREIGN KEY ([TenantId], [CartId]) REFERENCES [Carts] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [CartSessions] (
    [TenantId] uniqueidentifier NOT NULL,
    [CartId] uniqueidentifier NOT NULL,
    [CapabilityHash] binary(32) NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [RevokedAt] datetimeoffset NULL,
    CONSTRAINT [PK_CartSessions] PRIMARY KEY ([TenantId], [CartId]),
    CONSTRAINT [FK_CartSessions_Carts_TenantId_CartId] FOREIGN KEY ([TenantId], [CartId]) REFERENCES [Carts] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [CheckoutPreparations] (
    [TenantId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [CartId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [CartRevision] int NOT NULL,
    [QuoteId] uniqueidentifier NOT NULL,
    [Hash] nvarchar(64) NOT NULL,
    [Document] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CheckoutPreparations] PRIMARY KEY ([TenantId], [Id]),
    CONSTRAINT [CK_CheckoutPreparations_Document] CHECK (ISJSON([Document])=1),
    CONSTRAINT [FK_CheckoutPreparations_Carts_TenantId_CartId] FOREIGN KEY ([TenantId], [CartId]) REFERENCES [Carts] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);

CREATE INDEX [IX_Carts_TenantId_ExpiresAt] ON [Carts] ([TenantId], [ExpiresAt]);

CREATE UNIQUE INDEX [IX_Carts_TenantId_UserId] ON [Carts] ([TenantId], [UserId]) WHERE [UserId] IS NOT NULL AND [Status]=0;

CREATE INDEX [IX_CheckoutPreparations_TenantId_CartId_CartRevision] ON [CheckoutPreparations] ([TenantId], [CartId], [CartRevision]);

CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);

CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);

CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031175137_InitialCart', N'10.0.14');

COMMIT;
GO
