[CmdletBinding()]
param([switch]$RestartRecovery)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$serviceName='MSSQL$MSSQL22'
$journal=Join-Path $repository 'artifacts\review-website\sql-memory-setting.json'
if(!(Test-Path -LiteralPath $journal)){throw 'The recorded original SQL setting is required.'}
$record=Get-Content -LiteralPath $journal -Raw|ConvertFrom-Json
if($record.Instance -ne 'Vaibhav\MSSQL22' -or $record.PreviousMaxMB -ne 2147483647){throw 'Unexpected recovery target; no SQL settings changed.'}
$sql=@"
SET NOCOUNT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))<>N'MSSQL22' THROW 50001,'Wrong instance',1;
DECLARE @advanced int=(SELECT CONVERT(int,value_in_use) FROM sys.configurations WHERE name='show advanced options');
IF @advanced=0 BEGIN EXEC sp_configure 'show advanced options',1; RECONFIGURE; END;
EXEC sp_configure 'max server memory (MB)',2147483647; RECONFIGURE;
IF @advanced=0 BEGIN EXEC sp_configure 'show advanced options',0; RECONFIGURE; END;
SELECT name,value_in_use FROM sys.configurations WHERE name='max server memory (MB)';
"@
$sqlcmd=(Get-Command sqlcmd -ErrorAction Stop).Source
function Restore-Setting([string]$server='tcp:localhost,1433') {
    & $sqlcmd -S $server -E -C -b -I -l 5 -t 15 -d master -Q $sql | Out-Host
    return $LASTEXITCODE
}
if(!$RestartRecovery){
    $result=Restore-Setting
    if($result -ne 0){throw 'SQL cannot accept a normal connection. The reviewed -RestartRecovery option requires an Administrator PowerShell and briefly restarts only MSSQL22.'}
}else{
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    $principal=[Security.Principal.WindowsPrincipal]::new($identity)
    if(!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run this recovery option from an Administrator PowerShell. No service was stopped.'}
    $service=Get-CimInstance Win32_Service -Filter 'Name="MSSQL$MSSQL22"'
    if(!$service -or $service.PathName -notmatch 'MSSQL16\.MSSQL22\\MSSQL\\Binn\\sqlservr\.exe'){throw 'Expected MSSQL22 service binary did not match.'}
    if((Get-Service 'SQLAgent$MSSQL22').Status -ne 'Stopped'){throw 'SQL Agent is running; stop and review its workload before recovery.'}
    & (Join-Path $PSScriptRoot 'Stop-CommerceDevelopment.ps1')
    $restored=$false
    Stop-Service -Name $serviceName -ErrorAction Stop
    try{
        # Microsoft documents -f for recovery after an excessively low memory limit.
        # These are temporary service-start arguments, not registry/startup changes.
        & net.exe start $serviceName /f /mSQLCMD
        if($LASTEXITCODE -ne 0){throw 'Minimal-configuration startup failed.'}
        $result=Restore-Setting -server 'lpc:.\MSSQL22'
        if($result -ne 0){throw 'Original memory setting could not be restored.'}
        $restored=$true
    }finally{
        if((Get-Service -Name $serviceName).Status -ne 'Stopped'){Stop-Service -Name $serviceName -ErrorAction Stop}
        Start-Service -Name $serviceName -ErrorAction Stop
    }
    if(!$restored){throw 'Recovery failed; MSSQL22 was returned to normal startup mode. No databases were deleted or reset.'}
}
& $sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -l 10 -t 15 -d master -Q "SET NOCOUNT ON; IF CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))<>N'MSSQL22' OR CONVERT(int,SERVERPROPERTY('IsSingleUser'))<>0 OR (SELECT CONVERT(bigint,value_in_use) FROM sys.configurations WHERE name='max server memory (MB)')<>2147483647 THROW 50001,'Recovery state is not verified',1; SELECT CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')) AS instance_name,SERVERPROPERTY('IsSingleUser') AS single_user; SELECT name,value_in_use FROM sys.configurations WHERE name='max server memory (MB)';"
if($LASTEXITCODE -ne 0){throw 'Post-recovery verification failed.'}
$record|Add-Member -NotePropertyName RestoredAt -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
$record|ConvertTo-Json|Set-Content -LiteralPath $journal
Write-Output 'Original SQL memory setting restored and normal connection verified. No data/schema/security settings changed.'
