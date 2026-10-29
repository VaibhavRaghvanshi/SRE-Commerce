[CmdletBinding()]
param([switch]$Provision)
$ErrorActionPreference='Stop'
if(-not $Provision){throw 'Review this script and explicitly approve dedicated Tenant runtime login provisioning before using -Provision.'}
$repository=Split-Path $PSScriptRoot -Parent
$secretDirectory=Join-Path $env:APPDATA 'Microsoft\UserSecrets\SRECommerce-Tenant-Development'
$secretFile=Join-Path $secretDirectory 'secrets.json'
$values=@{}
if(Test-Path -LiteralPath $secretFile){$existing=Get-Content -LiteralPath $secretFile -Raw|ConvertFrom-Json;foreach($property in $existing.PSObject.Properties){$values[$property.Name]=$property.Value}}
$environmentFile=Join-Path $repository '.env'
$environmentText=if(Test-Path -LiteralPath $environmentFile){[IO.File]::ReadAllText($environmentFile)}else{''}
if($environmentText -match '(?m)^TENANT_SQL_CONNECTION=' -and -not $values['ContainerSql:User']){throw 'Existing Tenant connection has no matching managed credentials; it was preserved for review.'}
if(-not $values['ContainerSql:User']){
    $values['ContainerSql:User']='SRECommerce_TenantRuntime_'+[Guid]::NewGuid().ToString('N').Substring(0,12)
    $values['ContainerSql:Password']='Aa1!'+[Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}
$login=$values['ContainerSql:User'];$password=$values['ContainerSql:Password']
if($login -notmatch '^SRECommerce_TenantRuntime_[a-f0-9]{12}$' -or $password -notmatch '^Aa1![A-F0-9]{64}$'){throw 'Unexpected managed credential format; no login changed.'}
# Preserve generated credentials before server-side changes so an interrupted
# verification can resume without resetting or orphaning the runtime login.
New-Item -ItemType Directory -Path $secretDirectory -Force|Out-Null
[IO.File]::WriteAllText($secretFile,($values|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
$roleSql=[IO.File]::ReadAllText((Join-Path $repository 'docs\database-migrations\tenant-runtime-role.sql'))
$provisionSql=@"
IF DB_NAME() <> N'SRECommerce_TenantDB' THROW 50001, 'Wrong Tenant database', 1;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'$login')
    CREATE LOGIN [$login] WITH PASSWORD=N'$password', DEFAULT_DATABASE=[SRECommerce_TenantDB], CHECK_POLICY=ON, CHECK_EXPIRATION=OFF;
IF DATABASE_PRINCIPAL_ID(N'$login') IS NULL CREATE USER [$login] FOR LOGIN [$login];
$roleSql
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members r JOIN sys.database_principals p ON p.principal_id=r.member_principal_id JOIN sys.database_principals g ON g.principal_id=r.role_principal_id WHERE p.name=N'$login' AND g.name=N'srecommerce_tenant_runtime')
    ALTER ROLE [srecommerce_tenant_runtime] ADD MEMBER [$login];
"@
$temporarySql=[IO.Path]::GetTempFileName()
try{
    [IO.File]::WriteAllText($temporarySql,$provisionSql,[Text.UTF8Encoding]::new($false))
    & sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -d SRECommerce_TenantDB -i $temporarySql
    if($LASTEXITCODE -ne 0){throw 'Dedicated login provisioning failed; no credential values were printed.'}
}finally{Remove-Item -LiteralPath $temporarySql -ErrorAction SilentlyContinue}
try{
    $env:SQLCMDPASSWORD=$password
    $probe=& sqlcmd -S 'tcp:localhost,1433' -U $login -C -b -I -h -1 -W -s ',' -d SRECommerce_TenantDB -Q "SET NOCOUNT ON; SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE'),HAS_DBACCESS('SRECommerce_IdentityDB'),HAS_DBACCESS('SRECommerceDb');"
    if($LASTEXITCODE -ne 0 -or ($probe|Out-String).Trim() -ne '0,0,0'){throw 'Runtime privilege verification failed; configuration was not written.'}
}finally{Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue}
New-Item -ItemType Directory -Path $secretDirectory -Force|Out-Null
[IO.File]::WriteAllText($secretFile,($values|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
$connection="Server=host.docker.internal,1433;Database=SRECommerce_TenantDB;User ID=$login;Password=$password;Encrypt=true;TrustServerCertificate=true"
$line='TENANT_SQL_CONNECTION='+$connection
if($environmentText -match '(?m)^TENANT_SQL_CONNECTION='){$environmentText=[regex]::Replace($environmentText,'(?m)^TENANT_SQL_CONNECTION=[^\r\n]*',$line)}else{$environmentText=$environmentText.TrimEnd()+[Environment]::NewLine+$line+[Environment]::NewLine}
[IO.File]::WriteAllText($environmentFile,$environmentText,[Text.UTF8Encoding]::new($false))
Write-Output 'Dedicated Tenant runtime SQL login ready. DML only; no schema-creation or Identity/legacy database access. Credentials saved locally; values not printed.'
