[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Identity','Theme','Storefront','Catalog','Inventory','Pricing','Cart','Order','Payment','Shipping','Notification','Manufacturing','Discovery')][string]$Service,
    [switch]$ClientOnly,[switch]$Provision
)
$ErrorActionPreference='Stop'
if(!$Provision){throw 'Review and approve the backend SQL/OAuth provisioning batch before using -Provision.'}
if($Service -eq 'Identity' -and !$ClientOnly){throw 'Existing Identity SQL login is preserved. Use -ClientOnly for its new scoped machine identity.'}
$repository=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $env:APPDATA ('Microsoft\UserSecrets\SRECommerce-'+$Service+'-Development')
$secretFile=Join-Path $directory 'secrets.json'
$values=@{}
if(Test-Path -LiteralPath $secretFile){$existing=Get-Content -LiteralPath $secretFile -Raw|ConvertFrom-Json;foreach($entry in $existing.PSObject.Properties){$values[$entry.Name]=$entry.Value}}
function NewPrivateValue {return [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))}
if(!$values['ServiceAuth:ClientId']){$values['ServiceAuth:ClientId']='srecommerce-service-'+$Service.ToLowerInvariant()}
if(!$values['ServiceAuth:ClientSecret']){$values['ServiceAuth:ClientSecret']=NewPrivateValue}
if($Service -in 'Payment','Shipping' -and !$values['Webhooks:Secret']){$values['Webhooks:Secret']=NewPrivateValue}
$environmentFile=Join-Path $repository '.env'
$environmentText=if(Test-Path -LiteralPath $environmentFile){[IO.File]::ReadAllText($environmentFile)}else{''}
$environmentKey=$Service.ToUpperInvariant()+'_SQL_CONNECTION'
if(!$ClientOnly){
    if($environmentText -match ('(?m)^'+$environmentKey+'=') -and !$values['ContainerSql:User']){throw 'Existing unmanaged service connection was preserved for review.'}
    if(!$values['ContainerSql:User']){$values['ContainerSql:User']='SRECommerce_'+$Service+'Runtime_'+[Guid]::NewGuid().ToString('N').Substring(0,12);$values['ContainerSql:Password']='Aa1!'+(NewPrivateValue)}
}
New-Item -ItemType Directory -Path $directory -Force|Out-Null
[IO.File]::WriteAllText($secretFile,($values|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
if(!$ClientOnly){
    $database='SRECommerce_'+$Service+'DB';$role='srecommerce_'+$Service.ToLowerInvariant()+'_runtime';$login=$values['ContainerSql:User'];$password=$values['ContainerSql:Password']
    if($login -notmatch ('^SRECommerce_'+$Service+'Runtime_[a-f0-9]{12}$') -or $password -notmatch '^Aa1![A-F0-9]{64}$'){throw 'Unexpected managed credential format; no login changed.'}
    $sql=@"
IF DB_NAME() <> N'$database' THROW 50001, 'Wrong service database', 1;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'$login')
 CREATE LOGIN [$login] WITH PASSWORD=N'$password',DEFAULT_DATABASE=[$database],CHECK_POLICY=ON,CHECK_EXPIRATION=OFF;
IF DATABASE_PRINCIPAL_ID(N'$login') IS NULL CREATE USER [$login] FOR LOGIN [$login];
IF DATABASE_PRINCIPAL_ID(N'$role') IS NULL CREATE ROLE [$role];
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::dbo TO [$role];
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members r JOIN sys.database_principals p ON p.principal_id=r.member_principal_id JOIN sys.database_principals g ON g.principal_id=r.role_principal_id WHERE p.name=N'$login' AND g.name=N'$role')
 ALTER ROLE [$role] ADD MEMBER [$login];
"@
    $temporarySql=[IO.Path]::GetTempFileName()
    try{[IO.File]::WriteAllText($temporarySql,$sql,[Text.UTF8Encoding]::new($false)); & sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -d $database -i $temporarySql;if($LASTEXITCODE -ne 0){throw 'Service login provisioning failed; private values were not printed.'}}
    finally{Remove-Item -LiteralPath $temporarySql -ErrorAction SilentlyContinue}
    try{
        $env:SQLCMDPASSWORD=$password
        $probe=& sqlcmd -S 'tcp:localhost,1433' -U $login -C -b -I -h -1 -W -s ',' -d $database -Q "SET NOCOUNT ON; SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE'),HAS_DBACCESS('SRECommerce_IdentityDB'),HAS_DBACCESS('SRECommerce_TenantDB'),HAS_DBACCESS('SRECommerceDb');"
        if($LASTEXITCODE -ne 0 -or ($probe|Out-String).Trim() -ne '0,0,0,0'){throw 'Service privilege probe failed; environment connection was not written.'}
    }finally{Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue}
    $connection="Server=host.docker.internal,1433;Database=$database;User ID=$login;Password=$password;Encrypt=true;TrustServerCertificate=true"
    $line=$environmentKey+'='+$connection
    if($environmentText -match ('(?m)^'+$environmentKey+'=')){$environmentText=[regex]::Replace($environmentText,('(?m)^'+$environmentKey+'=[^\r\n]*'),$line)}else{$environmentText=$environmentText.TrimEnd()+[Environment]::NewLine+$line+[Environment]::NewLine}
    [IO.File]::WriteAllText($environmentFile,$environmentText,[Text.UTF8Encoding]::new($false))
}
$manifestFile=Join-Path $repository 'artifacts\backend-service-clients.json';New-Item -ItemType Directory -Path (Split-Path $manifestFile -Parent) -Force|Out-Null
$manifest=@{};if(Test-Path -LiteralPath $manifestFile){$old=Get-Content -LiteralPath $manifestFile -Raw|ConvertFrom-Json;foreach($entry in $old.PSObject.Properties){$manifest[$entry.Name]=$entry.Value}}
if($manifest.ContainsKey($Service) -and $manifest[$Service] -ne $values['ServiceAuth:ClientSecret']){throw 'Existing client manifest secret differs; it was preserved.'}
$manifest[$Service]=$values['ServiceAuth:ClientSecret'];[IO.File]::WriteAllText($manifestFile,($manifest|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output ($Service+': private service credentials prepared; existing values preserved'+$(if(!$ClientOnly){'; SQL DML-only/no-cross-service-access probes passed.'}else{'. Existing SQL permissions unchanged.'}))
