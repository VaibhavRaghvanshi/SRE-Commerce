SET XACT_ABORT ON;

IF DB_NAME() <> N'SRECommerce_IdentityDB'
    THROW 50001, 'Wrong Identity migration database', 1;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261029020826_BoundOidcSubjects'
)
    THROW 50002, 'Apply the preceding Identity OIDC migration first', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory WITH (UPDLOCK, HOLDLOCK)
    WHERE MigrationId = N'20261108042802_MembershipDirectoryIndex'
)
BEGIN
    CREATE INDEX IX_TenantMemberships_TenantId_IsActive_Role_UserId
        ON dbo.TenantMemberships (TenantId, IsActive, Role, UserId);

    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261108042802_MembershipDirectoryIndex', N'10.0.14');
END;

COMMIT;
