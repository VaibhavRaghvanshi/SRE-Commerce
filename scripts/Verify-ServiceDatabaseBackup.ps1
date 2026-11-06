[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Catalog','Inventory','Pricing','Cart','Order','Payment','Shipping','Notification','Manufacturing','Discovery')]
    [string]$Service,
    [switch]$BackupOnly
)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$database='SRECommerce_'+$Service+'DB'
$stamp=[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$restore='SRECommerce_'+$Service+'Tests_Restore_'+$stamp
if($database -notmatch '^SRECommerce_[A-Za-z]+DB$' -or $restore -notmatch '^SRECommerce_[A-Za-z]+Tests_Restore_[0-9]{17}_[a-f0-9]{8}$'){throw 'Unexpected database namespace'}
$metadataQuery="SET NOCOUNT ON; SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) AS backupDirectory,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')) AS dataDirectory,(SELECT name FROM sys.database_files WHERE type=0) AS dataLogical,(SELECT name FROM sys.database_files WHERE type=1) AS logLogical FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;"
$metadataLines=& sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -y 0 -w 65535 -d $database -Q $metadataQuery
if($LASTEXITCODE -ne 0){throw 'Backup metadata probe failed; no backup/restore was attempted.'}
$metadataJson=($metadataLines|Where-Object {$_.TrimStart().StartsWith('{')}) -join ''
$metadata=$metadataJson|ConvertFrom-Json
if(!$metadata.backupDirectory -or !$metadata.dataDirectory -or !$metadata.dataLogical -or !$metadata.logLogical){throw 'Expected one data/log file and configured local backup/data directories.'}
$backup=Join-Path $metadata.backupDirectory ($database+'-'+$stamp+'.bak')
$dataFile=Join-Path $metadata.dataDirectory ($restore+'.mdf')
$logFile=Join-Path $metadata.dataDirectory ($restore+'_log.ldf')
function SqlLiteral([string]$value){return "N'"+$value.Replace("'","''")+"'"}
$sql=@"
SET NOCOUNT ON;
IF DB_NAME() <> $(SqlLiteral $database) THROW 50001, 'Wrong backup source database', 1;
IF DB_ID($(SqlLiteral $restore)) IS NOT NULL THROW 50001, 'Restore target already exists; refusing overwrite', 1;
BACKUP DATABASE [$database] TO DISK=$(SqlLiteral $backup) WITH COPY_ONLY,CHECKSUM;
RESTORE VERIFYONLY FROM DISK=$(SqlLiteral $backup) WITH CHECKSUM;
RESTORE DATABASE [$restore] FROM DISK=$(SqlLiteral $backup)
WITH MOVE $(SqlLiteral $metadata.dataLogical) TO $(SqlLiteral $dataFile), MOVE $(SqlLiteral $metadata.logLogical) TO $(SqlLiteral $logFile),CHECKSUM;
DBCC CHECKDB ([$restore]) WITH NO_INFOMSGS;
ALTER DATABASE [$restore] SET READ_ONLY;
SELECT COUNT(*) AS RestoredAuditRows FROM [$restore].dbo.AuditEntries;
SELECT MigrationId FROM [$restore].dbo.__EFMigrationsHistory ORDER BY MigrationId;
"@
if($BackupOnly){
    $sql=@"
SET NOCOUNT ON;
IF DB_NAME() <> $(SqlLiteral $database) THROW 50001, 'Wrong backup source database', 1;
BACKUP DATABASE [$database] TO DISK=$(SqlLiteral $backup) WITH COPY_ONLY,CHECKSUM;
RESTORE VERIFYONLY FROM DISK=$(SqlLiteral $backup) WITH CHECKSUM;
"@
}
$artifactDirectory=Join-Path $repository artifacts
New-Item -ItemType Directory -Path $artifactDirectory -Force|Out-Null
$name=$Service.ToLowerInvariant()+$(if($BackupOnly){'-checkpoint-backup-'}else{'-backup-restore-'})+$stamp
$sqlFile=Join-Path $artifactDirectory ($name+'.sql')
$logFilePath=Join-Path $artifactDirectory ($name+'.log')
[IO.File]::WriteAllText($sqlFile,$sql,[Text.UTF8Encoding]::new($false))
& sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -d $database -i $sqlFile -o $logFilePath
if($LASTEXITCODE -ne 0){throw 'Service backup/restore verification failed. Existing source/target files and logs are preserved.'}
if($BackupOnly){
    @{Service=$Service;SourceDatabase=$database;Verified='COPY_ONLY,CHECKSUM,RESTORE VERIFYONLY';Backup=$backup;Log=$logFilePath}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifactDirectory ($Service.ToLowerInvariant()+'-checkpoint-backup-manifest.json')) -Encoding utf8
    Write-Output ($Service+' checkpoint backup checksum/VERIFYONLY passed; no restore database was created.')
}else{
    @{Service=$Service;SourceDatabase=$database;Verified='COPY_ONLY,CHECKSUM,RESTORE VERIFYONLY,DBCC CHECKDB,READ_ONLY';Backup=$backup;RestoreDatabase=$restore;Log=$logFilePath}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifactDirectory ($Service.ToLowerInvariant()+'-backup-manifest.json')) -Encoding utf8
    Write-Output ($Service+' backup and separate read-only restore verified: '+$restore)
}
