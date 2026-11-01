IF DB_NAME() <> N'SRECommerce_OrderDB' THROW 50001, 'Wrong service migration database', 1;
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
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
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
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [CheckoutIntents] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [PreparationId] uniqueidentifier NOT NULL,
        [ShippingQuoteId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [CustomerRole] nvarchar(30) NULL,
        [GuestKeyHash] binary(32) NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [RequestDocument] nvarchar(max) NOT NULL,
        [State] int NOT NULL,
        [Phase] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Deadline] datetimeoffset NOT NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [Attempts] int NOT NULL,
        [LastFault] nvarchar(80) NULL,
        [CartClaimDocument] nvarchar(max) NOT NULL,
        [CancellationReason] nvarchar(300) NULL,
        [LeaseId] uniqueidentifier NULL,
        [LeaseUntil] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_CheckoutIntents] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_CheckoutIntents_Attempts] CHECK ([Attempts]>=0),
        CONSTRAINT [CK_CheckoutIntents_Claim] CHECK (ISJSON([CartClaimDocument])=1),
        CONSTRAINT [CK_CheckoutIntents_Request] CHECK (ISJSON([RequestDocument])=1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
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
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
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
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderImports] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Source] nvarchar(80) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [Result] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OrderImports] PRIMARY KEY ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderPolicies] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [AllowGuest] bit NOT NULL,
        [AllowCOD] bit NOT NULL,
        [AllowExchanges] bit NOT NULL,
        [ReturnWindowDays] int NOT NULL,
        [MaximumTotal] decimal(18,4) NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_OrderPolicies] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_OrderPolicies_Maximum] CHECK ([MaximumTotal]>0),
        CONSTRAINT [CK_OrderPolicies_Returns] CHECK ([ReturnWindowDays] BETWEEN 0 AND 365)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [Orders] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [CartId] uniqueidentifier NULL,
        [CartRevision] int NULL,
        [PreparationId] uniqueidentifier NULL,
        [QuoteId] uniqueidentifier NULL,
        [ShippingQuoteId] uniqueidentifier NULL,
        [Revision] int NOT NULL,
        [Number] nvarchar(80) NOT NULL,
        [Status] int NOT NULL,
        [PaymentState] int NOT NULL,
        [PaymentMethod] nvarchar(20) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [DecimalPlaces] int NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [Discount] decimal(18,4) NOT NULL,
        [Net] decimal(18,4) NOT NULL,
        [Tax] decimal(18,4) NOT NULL,
        [ItemsTotal] decimal(18,4) NOT NULL,
        [ShippingTotal] decimal(18,4) NOT NULL,
        [Total] decimal(18,4) NOT NULL,
        [ContactDocument] nvarchar(4000) NOT NULL,
        [PriceSnapshot] nvarchar(max) NOT NULL,
        [ShippingSnapshot] nvarchar(max) NOT NULL,
        [InvoiceReference] nvarchar(80) NULL,
        [Historical] bit NOT NULL,
        [SourceReference] nvarchar(120) NULL,
        [Refunded] decimal(18,4) NOT NULL,
        [DeliveredAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_Orders_Documents] CHECK (ISJSON([ContactDocument])=1 AND ISJSON([PriceSnapshot])=1 AND ISJSON([ShippingSnapshot])=1),
        CONSTRAINT [CK_Orders_Precision] CHECK ([DecimalPlaces] BETWEEN 0 AND 3),
        CONSTRAINT [CK_Orders_Totals] CHECK ([Subtotal]>=0 AND [Discount]>=0 AND [Net]>=0 AND [Tax]>=0 AND [ShippingTotal]>=0 AND [ItemsTotal]=[Net]+[Tax] AND [Total]=[ItemsTotal]+[ShippingTotal] AND [Refunded]>=0 AND [Refunded]<=[Total])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderFulfillments] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [Items] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [CourierReference] nvarchar(160) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Attempts] int NOT NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [LastFault] nvarchar(80) NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_OrderFulfillments] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_OrderFulfillments_Items] CHECK (ISJSON([Items])=1),
        CONSTRAINT [FK_OrderFulfillments_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [Orders] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [TenantId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Sku] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [ShipmentRequested] decimal(18,3) NOT NULL,
        [RefundAllocated] decimal(18,4) NOT NULL,
        [Fulfilled] decimal(18,3) NOT NULL,
        [Delivered] decimal(18,3) NOT NULL,
        [ReturnRequested] decimal(18,3) NOT NULL,
        [Returned] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [Discount] decimal(18,4) NOT NULL,
        [Net] decimal(18,4) NOT NULL,
        [Tax] decimal(18,4) NOT NULL,
        [Total] decimal(18,4) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([TenantId], [OrderId], [VariantId]),
        CONSTRAINT [CK_OrderItems_Quantities] CHECK ([Quantity]>0 AND [ShipmentRequested]>=0 AND [ShipmentRequested]<=[Quantity] AND [Fulfilled]>=0 AND [Fulfilled]<=[ShipmentRequested] AND [Delivered]>=0 AND [Delivered]<=[Fulfilled] AND [ReturnRequested]>=0 AND [ReturnRequested]<=[Delivered] AND [Returned]>=0 AND [Returned]<=[ReturnRequested] AND [RefundAllocated]>=0 AND [RefundAllocated]<=[Total]),
        CONSTRAINT [FK_OrderItems_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [Orders] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderOperations] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Action] nvarchar(80) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [Result] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OrderOperations] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_OrderOperations_Result] CHECK (ISJSON([Result])=1),
        CONSTRAINT [FK_OrderOperations_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [Orders] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderReservations] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [Items] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [RemoteVersion] nvarchar(30) NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_OrderReservations] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_OrderReservations_Items] CHECK (ISJSON([Items])=1),
        CONSTRAINT [FK_OrderReservations_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [Orders] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderReturns] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Items] nvarchar(max) NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [Status] int NOT NULL,
        [RefundAmount] decimal(18,4) NOT NULL,
        [WarehouseId] uniqueidentifier NULL,
        [Resalable] bit NOT NULL,
        [ReceiptRecorded] bit NOT NULL,
        [ExchangeId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Attempts] int NOT NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [LastFault] nvarchar(80) NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_OrderReturns] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [CK_OrderReturns_Items] CHECK (ISJSON([Items])=1),
        CONSTRAINT [CK_OrderReturns_Refund] CHECK ([RefundAmount]>=0),
        CONSTRAINT [FK_OrderReturns_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [Orders] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderStatusHistory] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Previous] int NULL,
        [Current] int NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OrderStatusHistory] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_OrderStatusHistory_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [Orders] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE TABLE [OrderExchanges] (
        [TenantId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [ReturnId] uniqueidentifier NOT NULL,
        [OriginalVariantId] uniqueidentifier NOT NULL,
        [ReplacementVariantId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Status] int NOT NULL,
        [ReplacementOrderId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_OrderExchanges] PRIMARY KEY ([TenantId], [Id]),
        CONSTRAINT [FK_OrderExchanges_OrderReturns_TenantId_ReturnId] FOREIGN KEY ([TenantId], [ReturnId]) REFERENCES [OrderReturns] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderExchanges_Orders_TenantId_OrderId] FOREIGN KEY ([TenantId], [OrderId]) REFERENCES [Orders] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderExchanges_Orders_TenantId_ReplacementOrderId] FOREIGN KEY ([TenantId], [ReplacementOrderId]) REFERENCES [Orders] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Id] ON [AuditEntries] ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_CheckoutIntents_State_NextAttemptAt_LeaseUntil] ON [CheckoutIntents] ([State], [NextAttemptAt], [LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_TenantId_ProcessedAt] ON [IntegrationInbox] ([TenantId], [ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_LeaseUntil] ON [IntegrationOutbox] ([LeaseUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_PublishedAt_NextAttemptAt] ON [IntegrationOutbox] ([PublishedAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_OrderExchanges_TenantId_OrderId] ON [OrderExchanges] ([TenantId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_OrderExchanges_TenantId_ReplacementOrderId] ON [OrderExchanges] ([TenantId], [ReplacementOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderExchanges_TenantId_ReturnId] ON [OrderExchanges] ([TenantId], [ReturnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_OrderFulfillments_Status_CreatedAt] ON [OrderFulfillments] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_OrderFulfillments_TenantId_OrderId] ON [OrderFulfillments] ([TenantId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_OrderOperations_TenantId_OrderId] ON [OrderOperations] ([TenantId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderPolicies_TenantId] ON [OrderPolicies] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderReservations_TenantId_OrderId_WarehouseId] ON [OrderReservations] ([TenantId], [OrderId], [WarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_OrderReturns_Status_CreatedAt] ON [OrderReturns] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_OrderReturns_TenantId_OrderId] ON [OrderReturns] ([TenantId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Orders_TenantId_CartId_CartRevision] ON [Orders] ([TenantId], [CartId], [CartRevision]) WHERE [CartId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Orders_TenantId_InvoiceReference] ON [Orders] ([TenantId], [InvoiceReference]) WHERE [InvoiceReference] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_TenantId_Number] ON [Orders] ([TenantId], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Orders_TenantId_SourceReference] ON [Orders] ([TenantId], [SourceReference]) WHERE [SourceReference] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_Orders_TenantId_Status_CreatedAt] ON [Orders] ([TenantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_Orders_TenantId_UserId_CreatedAt] ON [Orders] ([TenantId], [UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistory_TenantId_OrderId_CreatedAt] ON [OrderStatusHistory] ([TenantId], [OrderId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101060312_InitialOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101060312_InitialOrder', N'10.0.14');
END;

COMMIT;
GO
