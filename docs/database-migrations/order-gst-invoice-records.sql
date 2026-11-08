SET XACT_ABORT ON;

IF DB_NAME() <> N'SRECommerce_OrderDB'
    THROW 50001, 'Wrong Order migration database', 1;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261104161256_SalesReportIndex'
)
    THROW 50002, 'Apply the preceding Order sales-report migration first', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory WITH (UPDLOCK, HOLDLOCK)
    WHERE MigrationId = N'20261108033142_GstExternalInvoiceRecords'
)
BEGIN
    CREATE TABLE dbo.GstInvoiceRecords (
        TenantId uniqueidentifier NOT NULL,
        Id uniqueidentifier NOT NULL,
        SupplierGstin varchar(15) NOT NULL,
        FinancialYearStart int NOT NULL,
        Number varchar(16) NOT NULL,
        Irn varchar(64) NULL,
        RequestHash varchar(64) NOT NULL,
        Document nvarchar(max) NOT NULL,
        CONSTRAINT PK_GstInvoiceRecords PRIMARY KEY (TenantId, Id),
        CONSTRAINT CK_GstInvoiceRecords_Document CHECK (ISJSON(Document) = 1),
        CONSTRAINT FK_GstInvoiceRecords_Orders_TenantId_Id
            FOREIGN KEY (TenantId, Id) REFERENCES dbo.Orders (TenantId, Id)
    );
    CREATE UNIQUE INDEX IX_GstInvoiceRecords_TenantId_Irn ON dbo.GstInvoiceRecords (TenantId, Irn)
        WHERE Irn IS NOT NULL;
    CREATE UNIQUE INDEX IX_GstInvoiceRecords_TenantId_SupplierGstin_FinancialYearStart_Number
        ON dbo.GstInvoiceRecords (TenantId, SupplierGstin, FinancialYearStart, Number);

    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261108033142_GstExternalInvoiceRecords', N'10.0.14');
END;

COMMIT;
