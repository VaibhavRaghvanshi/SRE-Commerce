-- Container runtime permissions only; login provisioning requires owner approval.
IF DB_NAME() <> N'SRECommerce_TenantDB' THROW 50001, 'Wrong Tenant database', 1;
IF DATABASE_PRINCIPAL_ID(N'srecommerce_tenant_runtime') IS NULL
    CREATE ROLE [srecommerce_tenant_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [srecommerce_tenant_runtime];
-- No CREATE/ALTER/DROP permission. No access to Identity or legacy databases.
