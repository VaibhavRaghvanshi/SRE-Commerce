BEGIN TRANSACTION;
ALTER TABLE [StockReservations] ADD [SourceService] nvarchar(40) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261101041853_ReservationServiceOwnership', N'10.0.14');

COMMIT;
GO
