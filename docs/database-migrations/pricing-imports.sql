BEGIN TRANSACTION;
CREATE TABLE [PricingImports] (
    [TenantId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [Source] nvarchar(80) NOT NULL,
    [Hash] nvarchar(64) NOT NULL,
    [Result] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PricingImports] PRIMARY KEY ([TenantId], [Id]),
    CONSTRAINT [CK_PricingImports_Result] CHECK (ISJSON([Result])=1)
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031173531_AuditedPriceImports', N'10.0.14');

COMMIT;
GO
