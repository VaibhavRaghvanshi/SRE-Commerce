-- Apply only inside the approved Identity database; this does not create a server login.
IF DB_NAME() <> N'SRECommerce_IdentityDB' THROW 50001, 'Wrong database', 1;
IF DATABASE_PRINCIPAL_ID(N'srecommerce_identity_runtime') IS NULL
    CREATE ROLE [srecommerce_identity_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [srecommerce_identity_runtime];
-- No CREATE/ALTER/DROP permission is granted. A deployment administrator must map
-- its dedicated Windows/SQL login to a user and add that user to this role.
-- Local developer integrated authentication is not claimed to be least privileged.
