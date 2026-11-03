IF DB_NAME() <> N'SRECommerce_ShippingDB' THROW 50001, 'Wrong service migration database', 1;
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
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
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
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
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
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
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
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [ReturnPickups] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ReturnId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [ContactDocument] nvarchar(4000) NOT NULL,
        [Items] nvarchar(max) NOT NULL,
        [WindowFrom] datetimeoffset NOT NULL,
        [WindowTo] datetimeoffset NOT NULL,
        [Status] int NOT NULL,
        [Note] nvarchar(300) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ReturnPickups] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ReturnPickups_Json] CHECK (ISJSON([ContactDocument])=1 AND ISJSON([Items])=1),
        CONSTRAINT [CK_ReturnPickups_Window] CHECK ([WindowTo]>[WindowFrom] AND [Status] BETWEEN 0 AND 2)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [ShippingOperations] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Action] nvarchar(80) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [Result] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ShippingOperations] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ShippingOperations_Result] CHECK (ISJSON([Result])=1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [ShippingZones] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Countries] nvarchar(max) NOT NULL,
        [PostalPrefixes] nvarchar(max) NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ShippingZones_Json] CHECK (ISJSON([Countries])=1 AND ISJSON([PostalPrefixes])=1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [ShippingMethods] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ZoneId] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Kind] int NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [FlatFee] decimal(18,4) NOT NULL,
        [PerUnitFee] decimal(18,4) NOT NULL,
        [TaxRate] decimal(18,4) NOT NULL,
        [FreeAbove] decimal(18,4) NULL,
        [MinimumOrder] decimal(18,4) NOT NULL,
        [MaximumOrder] decimal(18,4) NULL,
        [MaximumUnits] decimal(18,3) NOT NULL,
        [PickupWarehouseId] uniqueidentifier NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ShippingMethods] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ShippingMethods_Amounts] CHECK ([FlatFee]>=0 AND [PerUnitFee]>=0 AND [TaxRate]>=0 AND [TaxRate]<=100 AND [MinimumOrder]>=0 AND ([MaximumOrder] IS NULL OR [MaximumOrder]>=[MinimumOrder]) AND ([FreeAbove] IS NULL OR [FreeAbove]>=0) AND [MaximumUnits]>0 AND [MaximumUnits]<=50000),
        CONSTRAINT [CK_ShippingMethods_Pickup] CHECK ([Kind] BETWEEN 0 AND 2 AND (([Kind]=1 AND [PickupWarehouseId] IS NOT NULL) OR ([Kind]<>1 AND [PickupWarehouseId] IS NULL))),
        CONSTRAINT [FK_ShippingMethods_ShippingZones_TenantId_ZoneId] FOREIGN KEY ([TenantId], [ZoneId]) REFERENCES [ShippingZones] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [ShippingQuotes] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [MethodId] uniqueidentifier NOT NULL,
        [PreparationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Document] nvarchar(max) NOT NULL,
        [ConfigurationSnapshot] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ShippingQuotes] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ShippingQuotes_Json] CHECK (ISJSON([Document])=1 AND ISJSON([ConfigurationSnapshot])=1),
        CONSTRAINT [FK_ShippingQuotes_ShippingMethods_TenantId_MethodId] FOREIGN KEY ([TenantId], [MethodId]) REFERENCES [ShippingMethods] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [Shipments] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [QuoteId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Kind] int NOT NULL,
        [Provider] nvarchar(30) NOT NULL,
        [CourierReference] nvarchar(100) NOT NULL,
        [ContactDocument] nvarchar(4000) NOT NULL,
        [ProofHash] nvarchar(64) NOT NULL,
        [Status] int NOT NULL,
        [Revision] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [DispatchedAt] datetimeoffset NULL,
        [DeliveredAt] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Shipments_Contact] CHECK (ISJSON([ContactDocument])=1),
        CONSTRAINT [CK_Shipments_Provider] CHECK ([Provider] IN (N'Local',N'SandboxCourier')),
        CONSTRAINT [CK_Shipments_State] CHECK ([Status] BETWEEN 0 AND 5 AND [Kind] BETWEEN 0 AND 2 AND [Revision]>0),
        CONSTRAINT [FK_Shipments_ShippingQuotes_TenantId_QuoteId] FOREIGN KEY ([TenantId], [QuoteId]) REFERENCES [ShippingQuotes] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [ShipmentCallbacks] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ShipmentId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [TargetStatus] int NOT NULL,
        [RequestDocument] nvarchar(2000) NULL,
        [Sent] bit NOT NULL,
        [ManualReview] bit NOT NULL,
        [Attempts] int NOT NULL,
        [LastFault] nvarchar(80) NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [LeaseId] uniqueidentifier NULL,
        [LeaseUntil] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ShipmentCallbacks] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ShipmentCallbacks_Request] CHECK ([RequestDocument] IS NULL OR ISJSON([RequestDocument])=1),
        CONSTRAINT [CK_ShipmentCallbacks_State] CHECK ([Attempts]>=0 AND [TargetStatus] IN (2,3,4)),
        CONSTRAINT [FK_ShipmentCallbacks_Shipments_TenantId_ShipmentId] FOREIGN KEY ([TenantId], [ShipmentId]) REFERENCES [Shipments] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [ShipmentItems] (
        [TenantId] uniqueidentifier NOT NULL,
        [ShipmentId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_ShipmentItems] PRIMARY KEY ([TenantId], [ShipmentId], [VariantId]),
        CONSTRAINT [CK_ShipmentItems_Quantity] CHECK ([Quantity]>0 AND [Quantity]<=1000),
        CONSTRAINT [FK_ShipmentItems_Shipments_TenantId_ShipmentId] FOREIGN KEY ([TenantId], [ShipmentId]) REFERENCES [Shipments] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE TABLE [TrackingEvents] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ShipmentId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [Status] int NOT NULL,
        [Note] nvarchar(300) NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TrackingEvents] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_TrackingEvents_Shipments_TenantId_ShipmentId] FOREIGN KEY ([TenantId], [ShipmentId]) REFERENCES [Shipments] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_ReturnPickups_TenantId_CreatedAt] ON [ReturnPickups] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ReturnPickups_TenantId_ReturnId] ON [ReturnPickups] ([TenantId], [ReturnId]) WHERE [Status]<>2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_ShipmentCallbacks_Sent_ManualReview_NextAttemptAt_LeaseUntil] ON [ShipmentCallbacks] ([Sent], [ManualReview], [NextAttemptAt], [LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShipmentCallbacks_TenantId_ShipmentId_Sequence] ON [ShipmentCallbacks] ([TenantId], [ShipmentId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Shipments_TenantId_CourierReference] ON [Shipments] ([TenantId], [CourierReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_Shipments_TenantId_OrderId] ON [Shipments] ([TenantId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_Shipments_TenantId_QuoteId] ON [Shipments] ([TenantId], [QuoteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_Shipments_TenantId_Status_CreatedAt] ON [Shipments] ([TenantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShippingMethods_TenantId_Code] ON [ShippingMethods] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_ShippingMethods_TenantId_ZoneId] ON [ShippingMethods] ([TenantId], [ZoneId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_ShippingQuotes_TenantId_MethodId] ON [ShippingQuotes] ([TenantId], [MethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE INDEX [IX_ShippingQuotes_TenantId_PreparationId] ON [ShippingQuotes] ([TenantId], [PreparationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShippingZones_TenantId_Code] ON [ShippingZones] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TrackingEvents_TenantId_ShipmentId_Sequence] ON [TrackingEvents] ([TenantId], [ShipmentId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101153504_InitialShipping'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101153504_InitialShipping', N'10.0.14');
END;

COMMIT;
GO
