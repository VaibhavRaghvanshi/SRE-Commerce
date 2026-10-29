-- Only the owner-approved Tenant database. Preserve an existing database.
USE [master];
GO
IF DB_ID(N'SRECommerce_TenantDB') IS NULL
    CREATE DATABASE [SRECommerce_TenantDB];
GO
