IF DB_NAME() <> N'SRECommerce_ManufacturingDB' THROW 51000, 'Wrong database for Manufacturing migration.', 1;
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
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
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
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE TABLE [BillsOfMaterials] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(48) NOT NULL,
        [Revision] int NOT NULL,
        [OutputVariantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Materials] nvarchar(max) NOT NULL,
        [Stages] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_BillsOfMaterials] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Bom_Document] CHECK (ISJSON([Materials])=1 AND ISJSON([Stages])=1 AND [Revision]>0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
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
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
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
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE TABLE [ProductionOrders] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [BomId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [OutputVariantId] uniqueidentifier NOT NULL,
        [PlannedQuantity] decimal(18,3) NOT NULL,
        [Materials] nvarchar(max) NOT NULL,
        [Stages] nvarchar(max) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [StandardMaterialCost] decimal(18,4) NOT NULL,
        [OverheadCost] decimal(18,4) NOT NULL,
        [GoodQuantity] decimal(18,3) NOT NULL,
        [WasteQuantity] decimal(18,3) NOT NULL,
        [OutputLot] nvarchar(64) NULL,
        [OutputExpiresAt] datetimeoffset NULL,
        [ReservationExpiresAt] datetimeoffset NULL,
        [StagesCompleted] int NOT NULL,
        [QualityPassed] bit NOT NULL,
        [QualityChecked] bit NOT NULL,
        [InputsConsumed] bit NOT NULL,
        [State] int NOT NULL,
        [ResumeState] int NULL,
        [Attempts] int NOT NULL,
        [LastFault] nvarchar(80) NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [LeaseId] uniqueidentifier NULL,
        [LeaseUntil] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_ProductionOrders] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Production_Document] CHECK (ISJSON([Materials])=1 AND ISJSON([Stages])=1),
        CONSTRAINT [CK_Production_Quantities] CHECK ([PlannedQuantity]>0 AND [GoodQuantity]>=0 AND [WasteQuantity]>=0 AND [GoodQuantity]+[WasteQuantity]<=[PlannedQuantity] AND [StandardMaterialCost]>=0 AND [OverheadCost]>=0),
        CONSTRAINT [CK_Production_Workflow] CHECK ([State] BETWEEN 0 AND 8 AND [Attempts]>=0 AND [StagesCompleted] BETWEEN 0 AND 12),
        CONSTRAINT [FK_ProductionOrders_BillsOfMaterials_TenantId_BomId] FOREIGN KEY ([TenantId], [BomId]) REFERENCES [BillsOfMaterials] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE TABLE [ProductionHistory] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [State] int NOT NULL,
        [Action] nvarchar(64) NOT NULL,
        [Note] nvarchar(300) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProductionHistory] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_ProductionHistory_ProductionOrders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [ProductionOrders] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BillsOfMaterials_TenantId_Code_Revision] ON [BillsOfMaterials] ([TenantId], [Code], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE INDEX [IX_ProductionHistory_TenantId_OrderId_CreatedAt] ON [ProductionHistory] ([TenantId], [OrderId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_State_NextAttemptAt_LeaseUntil] ON [ProductionOrders] ([State], [NextAttemptAt], [LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_TenantId_BomId] ON [ProductionOrders] ([TenantId], [BomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_TenantId_CreatedAt] ON [ProductionOrders] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103162240_InitialManufacturing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103162240_InitialManufacturing', N'10.0.14');
END;

COMMIT;
GO
