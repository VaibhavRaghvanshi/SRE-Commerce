IF DB_NAME() <> N'SRECommerce_CartDB' THROW 50001, 'Wrong Cart migration database', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161239_CustomerAccount'
)
BEGIN
    CREATE TABLE [CustomerAddresses] (
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Id] uniqueidentifier NOT NULL,
        [Label] nvarchar(40) NOT NULL,
        [FullName] nvarchar(160) NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [Phone] nvarchar(25) NULL,
        [AddressLine1] nvarchar(200) NOT NULL,
        [AddressLine2] nvarchar(200) NULL,
        [City] nvarchar(100) NOT NULL,
        [CountryCode] nvarchar(2) NOT NULL,
        [PostalCode] nvarchar(16) NOT NULL,
        [Active] bit NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_CustomerAddresses] PRIMARY KEY ([TenantId], [UserId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161239_CustomerAccount'
)
BEGIN
    CREATE TABLE [SavedProducts] (
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [Active] bit NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_SavedProducts] PRIMARY KEY ([TenantId], [UserId], [Kind], [ProductId]),
        CONSTRAINT [CK_SavedProducts_Kind] CHECK ([Kind] BETWEEN 0 AND 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161239_CustomerAccount'
)
BEGIN
    CREATE INDEX [IX_CustomerAddresses_TenantId_UserId_Active_UpdatedAt] ON [CustomerAddresses] ([TenantId], [UserId], [Active], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161239_CustomerAccount'
)
BEGIN
    CREATE INDEX [IX_SavedProducts_TenantId_UserId_Kind_Active_UpdatedAt] ON [SavedProducts] ([TenantId], [UserId], [Kind], [Active], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104161239_CustomerAccount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104161239_CustomerAccount', N'10.0.14');
END;

COMMIT;
GO
