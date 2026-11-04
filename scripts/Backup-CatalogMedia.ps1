[CmdletBinding()]
param([string]$MediaRoot=(Join-Path $env:LOCALAPPDATA 'SRECommerce\Media\Catalog'))
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
if(![IO.Path]::IsPathFullyQualified($MediaRoot)){throw 'An absolute managed media root is required.'}
$source=[IO.Path]::GetFullPath($MediaRoot)
$stamp=[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$destination=Join-Path $repository ('artifacts\catalog-media-backup-'+$stamp)
$artifactRoot=[IO.Path]::GetFullPath((Join-Path $repository 'artifacts'))+[IO.Path]::DirectorySeparatorChar
if(![IO.Path]::GetFullPath($destination).StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected backup destination.'}
if(Test-Path -LiteralPath $destination){throw 'Backup destination already exists; refusing overwrite.'}
New-Item -ItemType Directory -Path $destination|Out-Null
# Back up SQL separately with Verify-ServiceDatabaseBackup.ps1. Stop upload writers
# during both snapshots; SQL metadata and filesystem bytes are separate resources.
$query="SET NOCOUNT ON;IF DB_NAME()<>N'SRECommerce_CatalogDB' THROW 50001,'Wrong media metadata database',1;SELECT TenantId,Id,Hash,Length FROM dbo.MediaAssets ORDER BY TenantId,Id FOR JSON PATH;"
$metadata=& sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -l 10 -t 30 -d SRECommerce_CatalogDB -y 0 -w 65535 -Q $query
if($LASTEXITCODE -ne 0){throw 'Managed media metadata read failed; no source files were changed.'}
$output=$metadata -join ''
$start=$output.IndexOf('[');$end=$output.LastIndexOf(']')
if($start -lt 0 -or $end -lt $start){throw 'Expected media metadata JSON.'}
$json=$output.Substring($start,$end-$start+1)
$assets=@($json|ConvertFrom-Json)
$copied=@()
foreach($asset in $assets){
    $tenant=[Guid]::Parse($asset.TenantId);$id=[Guid]::Parse($asset.Id)
    if($tenant -eq [Guid]::Empty -or $id -eq [Guid]::Empty -or $asset.Hash -notmatch '^[A-F0-9]{64}$'){throw 'Invalid managed media identity/hash; source preserved.'}
    $relative=Join-Path $tenant.ToString('N') ($id.ToString('N')+'.image')
    $file=Join-Path $source $relative
    if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw 'A registered media file is missing; partial backup retained for inspection.'}
    $info=Get-Item -LiteralPath $file
    if($info.Length -ne [long]$asset.Length -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $asset.Hash){throw 'Registered media size/hash mismatch; source preserved.'}
    $target=Join-Path $destination $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    Copy-Item -LiteralPath $file -Destination $target -ErrorAction Stop
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $asset.Hash){throw 'Copied media checksum mismatch; backup retained for inspection.'}
    $copied+=@{TenantId=$tenant;Id=$id;RelativePath=$relative;Hash=$asset.Hash;Length=[long]$asset.Length}
}
@{Database='SRECommerce_CatalogDB';CreatedAt=[DateTimeOffset]::UtcNow;Files=$copied}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $destination 'manifest.json') -Encoding utf8
@{Directory=$destination;Files=$copied.Count;Verified='Registered metadata,source size,source SHA256,copied SHA256'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $repository 'artifacts\catalog-media-backup-manifest.json') -Encoding utf8
Write-Output ('Managed Catalog media backup verified: '+$copied.Count+' file(s). Source and unreferenced files preserved.')
