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
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
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
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [Branches] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [TenantLocationId] uniqueidentifier NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
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
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
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
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [StockItems] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Sku] nvarchar(64) NOT NULL,
        [Barcode] nvarchar(64) NULL,
        [Active] bit NOT NULL,
        [ReorderLevel] decimal(18,3) NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_StockItems] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_StockItems_ReorderLevel] CHECK ([ReorderLevel]>=0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [StockOperations] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Action] int NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [Result] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StockOperations] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_StockOperations_Result] CHECK (ISJSON([Result])=1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [ContactEmail] nvarchar(254) NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [Warehouses] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Active] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Warehouses] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_Warehouses_Branches_TenantId_BranchId] FOREIGN KEY ([TenantId], [BranchId]) REFERENCES [Branches] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [InventoryBatches] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [Lot] nvarchar(64) NOT NULL,
        [ExpiresAt] datetimeoffset NULL,
        [ReceivedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_InventoryBatches] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [AK_InventoryBatches_TenantId_Id_WarehouseId_VariantId] UNIQUE ([TenantId], [Id], [WarehouseId], [VariantId]),
        CONSTRAINT [FK_InventoryBatches_StockItems_TenantId_VariantId] FOREIGN KEY ([TenantId], [VariantId]) REFERENCES [StockItems] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryBatches_Warehouses_TenantId_WarehouseId] FOREIGN KEY ([TenantId], [WarehouseId]) REFERENCES [Warehouses] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [PurchaseOrders] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ReceiptRevision] int NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(120) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_PurchaseOrders_Suppliers_TenantId_SupplierId] FOREIGN KEY ([TenantId], [SupplierId]) REFERENCES [Suppliers] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_Warehouses_TenantId_WarehouseId] FOREIGN KEY ([TenantId], [WarehouseId]) REFERENCES [Warehouses] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [StockReservations] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(120) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [Status] int NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_StockReservations] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_StockReservations_Warehouses_TenantId_WarehouseId] FOREIGN KEY ([TenantId], [WarehouseId]) REFERENCES [Warehouses] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [InventoryBalances] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [BatchId] uniqueidentifier NOT NULL,
        [OnHand] decimal(18,3) NOT NULL,
        [Reserved] decimal(18,3) NOT NULL,
        [Damaged] decimal(18,3) NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_InventoryBalances] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [AK_InventoryBalances_TenantId_Id_VariantId] UNIQUE ([TenantId], [Id], [VariantId]),
        CONSTRAINT [CK_InventoryBalances_Quantities] CHECK ([OnHand]>=0 AND [Reserved]>=0 AND [Damaged]>=0 AND [Reserved]<=[OnHand]),
        CONSTRAINT [FK_InventoryBalances_InventoryBatches_TenantId_BatchId_WarehouseId_VariantId] FOREIGN KEY ([TenantId], [BatchId], [WarehouseId], [VariantId]) REFERENCES [InventoryBatches] ([TenantId], [Id], [WarehouseId], [VariantId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [PurchaseOrderLines] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [Ordered] decimal(18,3) NOT NULL,
        [Received] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,4) NOT NULL,
        CONSTRAINT [PK_PurchaseOrderLines] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_PurchaseLines_Quantities] CHECK ([Ordered]>0 AND [Received]>=0 AND [Received]<=[Ordered] AND [UnitCost]>=0),
        CONSTRAINT [FK_PurchaseOrderLines_PurchaseOrders_TenantId_PurchaseOrderId] FOREIGN KEY ([TenantId], [PurchaseOrderId]) REFERENCES [PurchaseOrders] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderLines_StockItems_TenantId_VariantId] FOREIGN KEY ([TenantId], [VariantId]) REFERENCES [StockItems] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [InventoryMovements] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [BalanceId] uniqueidentifier NOT NULL,
        [Action] int NOT NULL,
        [OnHandDelta] decimal(18,3) NOT NULL,
        [ReservedDelta] decimal(18,3) NOT NULL,
        [DamagedDelta] decimal(18,3) NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InventoryMovements] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_InventoryMovements_InventoryBalances_TenantId_BalanceId] FOREIGN KEY ([TenantId], [BalanceId]) REFERENCES [InventoryBalances] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE TABLE [StockReservationLines] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [ReservationId] uniqueidentifier NOT NULL,
        [BalanceId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_StockReservationLines] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_ReservationLines_Quantity] CHECK ([Quantity]>0),
        CONSTRAINT [FK_StockReservationLines_InventoryBalances_TenantId_BalanceId_VariantId] FOREIGN KEY ([TenantId], [BalanceId], [VariantId]) REFERENCES [InventoryBalances] ([TenantId], [Id], [VariantId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockReservationLines_StockReservations_TenantId_ReservationId] FOREIGN KEY ([TenantId], [ReservationId]) REFERENCES [StockReservations] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Branches_TenantId_Code] ON [Branches] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_InventoryBalances_TenantId_BatchId_WarehouseId_VariantId] ON [InventoryBalances] ([TenantId], [BatchId], [WarehouseId], [VariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryBalances_TenantId_WarehouseId_VariantId_BatchId] ON [InventoryBalances] ([TenantId], [WarehouseId], [VariantId], [BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_InventoryBatches_TenantId_ExpiresAt] ON [InventoryBatches] ([TenantId], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_InventoryBatches_TenantId_VariantId] ON [InventoryBatches] ([TenantId], [VariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryBatches_TenantId_WarehouseId_VariantId_Lot] ON [InventoryBatches] ([TenantId], [WarehouseId], [VariantId], [Lot]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_TenantId_BalanceId_OccurredAt] ON [InventoryMovements] ([TenantId], [BalanceId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_TenantId_OperationId] ON [InventoryMovements] ([TenantId], [OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseOrderLines_TenantId_PurchaseOrderId_VariantId] ON [PurchaseOrderLines] ([TenantId], [PurchaseOrderId], [VariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderLines_TenantId_VariantId] ON [PurchaseOrderLines] ([TenantId], [VariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseOrders_TenantId_Reference] ON [PurchaseOrders] ([TenantId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_TenantId_SupplierId] ON [PurchaseOrders] ([TenantId], [SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_TenantId_WarehouseId] ON [PurchaseOrders] ([TenantId], [WarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_StockItems_TenantId_Barcode] ON [StockItems] ([TenantId], [Barcode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockItems_TenantId_Sku] ON [StockItems] ([TenantId], [Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_StockReservationLines_TenantId_BalanceId_VariantId] ON [StockReservationLines] ([TenantId], [BalanceId], [VariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockReservationLines_TenantId_ReservationId_BalanceId] ON [StockReservationLines] ([TenantId], [ReservationId], [BalanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_StockReservations_Status_ExpiresAt] ON [StockReservations] ([Status], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_StockReservations_TenantId_Status_ExpiresAt] ON [StockReservations] ([TenantId], [Status], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_StockReservations_TenantId_WarehouseId] ON [StockReservations] ([TenantId], [WarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Suppliers_TenantId_Code] ON [Suppliers] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE INDEX [IX_Warehouses_TenantId_BranchId] ON [Warehouses] ([TenantId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Warehouses_TenantId_Code] ON [Warehouses] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031132333_InitialInventory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031132333_InitialInventory', N'10.0.14');
END;

COMMIT;
GO
