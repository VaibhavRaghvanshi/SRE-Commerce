BEGIN TRANSACTION;
ALTER TABLE [CheckoutPreparations] ADD [CustomerRole] nvarchar(30) NULL;

CREATE TABLE [CartCheckoutClaims] (
    [TenantId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [CartId] uniqueidentifier NOT NULL,
    [PreparationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [CartRevision] int NOT NULL,
    [Status] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_CartCheckoutClaims] PRIMARY KEY ([TenantId], [Id]),
    CONSTRAINT [CK_CartCheckoutClaims_Status] CHECK ([Status] BETWEEN 0 AND 3),
    CONSTRAINT [FK_CartCheckoutClaims_Carts_TenantId_CartId] FOREIGN KEY ([TenantId], [CartId]) REFERENCES [Carts] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CartCheckoutClaims_CheckoutPreparations_TenantId_PreparationId] FOREIGN KEY ([TenantId], [PreparationId]) REFERENCES [CheckoutPreparations] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_CartCheckoutClaims_Status_ExpiresAt] ON [CartCheckoutClaims] ([Status], [ExpiresAt]);

CREATE UNIQUE INDEX [IX_CartCheckoutClaims_TenantId_CartId_CartRevision] ON [CartCheckoutClaims] ([TenantId], [CartId], [CartRevision]) WHERE [Status]<2;

CREATE INDEX [IX_CartCheckoutClaims_TenantId_PreparationId] ON [CartCheckoutClaims] ([TenantId], [PreparationId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261101031319_OrderCheckoutClaims', N'10.0.14');

COMMIT;
GO
