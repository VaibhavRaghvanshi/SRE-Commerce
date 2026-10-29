[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$stamp=[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')
$restore='SRECommerce_TenantTests_Restore_'+$stamp
$metadataQuery="SET NOCOUNT ON; SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) AS backupDirectory,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')) AS dataDirectory,(SELECT name FROM sys.database_files WHERE type=0) AS dataLogical,(SELECT name FROM sys.database_files WHERE type=1) AS logLogical FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;"
$metadataLines=& sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -y 0 -w 65535 -d SRECommerce_TenantDB -Q $metadataQuery
if($LASTEXITCODE -ne 0){throw 'Backup metadata probe failed; no backup/restore was attempted.'}
$metadataJson=($metadataLines|Where-Object {$_.TrimStart().StartsWith('{')}) -join ''
$metadata=$metadataJson|ConvertFrom-Json
if(!$metadata.backupDirectory -or !$metadata.dataDirectory -or !$metadata.dataLogical -or !$metadata.logLogical){throw 'Expected one data/log file and configured local backup/data directories.'}
$backup=Join-Path $metadata.backupDirectory ('SRECommerce_TenantDB-'+$stamp+'.bak')
$dataFile=Join-Path $metadata.dataDirectory ($restore+'.mdf')
$logFile=Join-Path $metadata.dataDirectory ($restore+'_log.ldf')
function SqlLiteral([string]$value){return "N'"+$value.Replace("'","''")+"'"}
$sql=@"
SET NOCOUNT ON;
IF DB_NAME() <> N'SRECommerce_TenantDB' THROW 50001, 'Wrong backup source database', 1;
IF DB_ID(N'$restore') IS NOT NULL THROW 50001, 'Restore target already exists; refusing overwrite', 1;
BACKUP DATABASE [SRECommerce_TenantDB] TO DISK=$(SqlLiteral $backup) WITH COPY_ONLY,CHECKSUM;
RESTORE VERIFYONLY FROM DISK=$(SqlLiteral $backup) WITH CHECKSUM;
RESTORE DATABASE [$restore] FROM DISK=$(SqlLiteral $backup)
WITH MOVE $(SqlLiteral $metadata.dataLogical) TO $(SqlLiteral $dataFile), MOVE $(SqlLiteral $metadata.logLogical) TO $(SqlLiteral $logFile),CHECKSUM;
DBCC CHECKDB ([$restore]) WITH NO_INFOMSGS;
ALTER DATABASE [$restore] SET READ_ONLY;
SELECT COUNT(*) AS RestoredTenants FROM [$restore].dbo.Tenants;
SELECT COUNT(*) AS RestoredAuditRows FROM [$restore].dbo.TenantAuditHistory;
"@
$artifactDirectory=Join-Path $repository artifacts
New-Item -ItemType Directory -Path $artifactDirectory -Force|Out-Null
$sqlFile=Join-Path $artifactDirectory ('tenant-backup-restore-'+$stamp+'.sql')
$logFilePath=Join-Path $artifactDirectory ('tenant-backup-restore-'+$stamp+'.log')
[IO.File]::WriteAllText($sqlFile,$sql,[Text.UTF8Encoding]::new($false))
& sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -d SRECommerce_TenantDB -i $sqlFile -o $logFilePath
if($LASTEXITCODE -ne 0){throw 'Tenant backup/restore verification failed. Existing source/target files and logs are preserved.'}
@{Verified='COPY_ONLY,CHECKSUM,RESTORE VERIFYONLY,DBCC CHECKDB,READ_ONLY';Backup=$backup;RestoreDatabase=$restore;Log=$logFilePath}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifactDirectory 'tenant-backup-manifest.json') -Encoding utf8
Write-Output ('Tenant backup and separate read-only restore verified: '+$restore)
