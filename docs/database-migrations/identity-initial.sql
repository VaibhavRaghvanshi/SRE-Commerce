SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
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
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [EmailOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Recipient] nvarchar(254) NOT NULL,
        [Subject] nvarchar(160) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Attempts] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [NextAttemptAt] datetimeoffset NOT NULL,
        [DeliveredAt] datetimeoffset NULL,
        CONSTRAINT [PK_EmailOutbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [OpenIddictApplications] (
        [Id] nvarchar(450) NOT NULL,
        [ApplicationType] nvarchar(50) NULL,
        [ClientId] nvarchar(100) NULL,
        [ClientSecret] nvarchar(max) NULL,
        [ClientType] nvarchar(50) NULL,
        [ConcurrencyToken] nvarchar(50) NULL,
        [ConsentType] nvarchar(50) NULL,
        [DisplayName] nvarchar(max) NULL,
        [DisplayNames] nvarchar(max) NULL,
        [JsonWebKeySet] nvarchar(max) NULL,
        [Permissions] nvarchar(max) NULL,
        [PostLogoutRedirectUris] nvarchar(max) NULL,
        [Properties] nvarchar(max) NULL,
        [RedirectUris] nvarchar(max) NULL,
        [Requirements] nvarchar(max) NULL,
        [Settings] nvarchar(max) NULL,
        CONSTRAINT [PK_OpenIddictApplications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [OpenIddictScopes] (
        [Id] nvarchar(450) NOT NULL,
        [ConcurrencyToken] nvarchar(50) NULL,
        [Description] nvarchar(max) NULL,
        [Descriptions] nvarchar(max) NULL,
        [DisplayName] nvarchar(max) NULL,
        [DisplayNames] nvarchar(max) NULL,
        [Name] nvarchar(200) NULL,
        [Properties] nvarchar(max) NULL,
        [Resources] nvarchar(max) NULL,
        CONSTRAINT [PK_OpenIddictScopes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [Permission] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [SecurityAuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NULL,
        [Action] nvarchar(100) NOT NULL,
        [Outcome] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SecurityAuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [LegacyPasswordHash] nvarchar(200) NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [OpenIddictAuthorizations] (
        [Id] nvarchar(450) NOT NULL,
        [ApplicationId] nvarchar(450) NULL,
        [ConcurrencyToken] nvarchar(50) NULL,
        [CreationDate] datetime2 NULL,
        [Properties] nvarchar(max) NULL,
        [Scopes] nvarchar(max) NULL,
        [Status] nvarchar(50) NULL,
        [Subject] nvarchar(400) NULL,
        [Type] nvarchar(50) NULL,
        CONSTRAINT [PK_OpenIddictAuthorizations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OpenIddictAuthorizations_OpenIddictApplications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [OpenIddictApplications] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [LegacyUserMappings] (
        [Id] uniqueidentifier NOT NULL,
        [Source] nvarchar(40) NOT NULL,
        [LegacyId] int NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LegacyRole] nvarchar(40) NOT NULL,
        [ImportedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LegacyUserMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LegacyUserMappings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [TenantMemberships] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_TenantMemberships] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantMemberships_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [UserProfiles] (
        [UserId] uniqueidentifier NOT NULL,
        [FullName] nvarchar(160) NOT NULL,
        [Kind] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_UserProfiles] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_UserProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [UserSessions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [AuthorizationId] nvarchar(100) NOT NULL,
        [SecurityStamp] nvarchar(100) NOT NULL,
        [MfaAuthenticated] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [RevokedAt] datetimeoffset NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_UserSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE TABLE [OpenIddictTokens] (
        [Id] nvarchar(450) NOT NULL,
        [ApplicationId] nvarchar(450) NULL,
        [AuthorizationId] nvarchar(450) NULL,
        [ConcurrencyToken] nvarchar(50) NULL,
        [CreationDate] datetime2 NULL,
        [ExpirationDate] datetime2 NULL,
        [Payload] nvarchar(max) NULL,
        [Properties] nvarchar(max) NULL,
        [RedemptionDate] datetime2 NULL,
        [ReferenceId] nvarchar(100) NULL,
        [Status] nvarchar(50) NULL,
        [Subject] nvarchar(400) NULL,
        [Type] nvarchar(150) NULL,
        CONSTRAINT [PK_OpenIddictTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OpenIddictTokens_OpenIddictApplications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [OpenIddictApplications] ([Id]),
        CONSTRAINT [FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId] FOREIGN KEY ([AuthorizationId]) REFERENCES [OpenIddictAuthorizations] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_EmailOutbox_DeliveredAt_NextAttemptAt] ON [EmailOutbox] ([DeliveredAt], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LegacyUserMappings_Source_LegacyId] ON [LegacyUserMappings] ([Source], [LegacyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_LegacyUserMappings_UserId] ON [LegacyUserMappings] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OpenIddictApplications_ClientId] ON [OpenIddictApplications] ([ClientId]) WHERE [ClientId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type] ON [OpenIddictAuthorizations] ([ApplicationId], [Status], [Subject], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OpenIddictScopes_Name] ON [OpenIddictScopes] ([Name]) WHERE [Name] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_OpenIddictTokens_ApplicationId_Status_Subject_Type] ON [OpenIddictTokens] ([ApplicationId], [Status], [Subject], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_OpenIddictTokens_AuthorizationId] ON [OpenIddictTokens] ([AuthorizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OpenIddictTokens_ReferenceId] ON [OpenIddictTokens] ([ReferenceId]) WHERE [ReferenceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_TenantId_Role_Permission] ON [RolePermissions] ([TenantId], [Role], [Permission]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_SecurityAuditLogs_TenantId_CreatedAt] ON [SecurityAuditLogs] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_SecurityAuditLogs_UserId_CreatedAt] ON [SecurityAuditLogs] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantMemberships_TenantId_UserId] ON [TenantMemberships] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_TenantMemberships_UserId] ON [TenantMemberships] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_UserSessions_UserId_RevokedAt_ExpiresAt] ON [UserSessions] ([UserId], [RevokedAt], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029014848_InitialIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029014848_InitialIdentity', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    IF EXISTS (SELECT 1 FROM [AspNetUserLogins] WHERE DATALENGTH([LoginProvider]) > 256 OR DATALENGTH([ProviderKey]) > 256) OR EXISTS (SELECT 1 FROM [AspNetUserTokens] WHERE DATALENGTH([LoginProvider]) > 256 OR DATALENGTH([Name]) > 256) THROW 50002, 'Long Identity keys require review before narrowing.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    ALTER TABLE [AspNetUserLogins] DROP CONSTRAINT [PK_AspNetUserLogins];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    ALTER TABLE [AspNetUserTokens] DROP CONSTRAINT [PK_AspNetUserTokens];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUserTokens]') AND [c].[name] = N'Name');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [AspNetUserTokens] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [AspNetUserTokens] ALTER COLUMN [Name] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUserTokens]') AND [c].[name] = N'LoginProvider');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUserTokens] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [AspNetUserTokens] ALTER COLUMN [LoginProvider] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUserLogins]') AND [c].[name] = N'ProviderKey');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUserLogins] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [AspNetUserLogins] ALTER COLUMN [ProviderKey] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUserLogins]') AND [c].[name] = N'LoginProvider');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUserLogins] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [AspNetUserLogins] ALTER COLUMN [LoginProvider] nvarchar(128) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    ALTER TABLE [AspNetUserLogins] ADD CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    ALTER TABLE [AspNetUserTokens] ADD CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020354_BoundIdentityTokenKeys'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029020354_BoundIdentityTokenKeys', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020826_BoundOidcSubjects'
)
BEGIN
    IF EXISTS (SELECT 1 FROM [OpenIddictTokens] WHERE DATALENGTH([Subject]) > 72) OR EXISTS (SELECT 1 FROM [OpenIddictAuthorizations] WHERE DATALENGTH([Subject]) > 72) THROW 50003, 'Non-GUID subjects require review before narrowing.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020826_BoundOidcSubjects'
)
BEGIN
    DROP INDEX [IX_OpenIddictTokens_ApplicationId_Status_Subject_Type] ON [OpenIddictTokens];
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OpenIddictTokens]') AND [c].[name] = N'Subject');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [OpenIddictTokens] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [OpenIddictTokens] ALTER COLUMN [Subject] nvarchar(36) NULL;
    CREATE INDEX [IX_OpenIddictTokens_ApplicationId_Status_Subject_Type] ON [OpenIddictTokens] ([ApplicationId], [Status], [Subject], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020826_BoundOidcSubjects'
)
BEGIN
    DROP INDEX [IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type] ON [OpenIddictAuthorizations];
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OpenIddictAuthorizations]') AND [c].[name] = N'Subject');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [OpenIddictAuthorizations] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [OpenIddictAuthorizations] ALTER COLUMN [Subject] nvarchar(36) NULL;
    CREATE INDEX [IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type] ON [OpenIddictAuthorizations] ([ApplicationId], [Status], [Subject], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029020826_BoundOidcSubjects'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029020826_BoundOidcSubjects', N'10.0.14');
END;

COMMIT;
GO
