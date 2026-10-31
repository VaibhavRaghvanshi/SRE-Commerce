[CmdletBinding()]
param([ValidateSet('Identity','Tenant','Theme','Storefront','Catalog','Inventory','Pricing','ApiGateway','UI')][string[]]$Services=@('Identity','Tenant','Theme','Storefront','Catalog','Inventory','Pricing','ApiGateway','UI'))
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$manifest=Join-Path $repository 'artifacts\commerce-development\processes.json'
if(!(Test-Path -LiteralPath $manifest)){Write-Output 'No owned development process manifest found.';return}
foreach($record in Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json){
    if($record.Service -notin $Services){continue}
    $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+[int]$record.ProcessId)
    if(!$process){continue}
    $expected=if($record.Service -eq 'UI'){'SRECommerce.UI.*ng.js'}elseif($record.Service -eq 'ApiGateway'){'SRECommerce.ApiGateway.dll'}else{'SRECommerce.'+$record.Service+'.API.dll'}
    if($process.ExecutablePath -ne $record.Executable -or $process.CommandLine -notmatch [regex]::Escape($expected).Replace('\.\*','.*')){Write-Output ('Preserved unmatched process for '+$record.Service);continue}
    Stop-Process -Id $record.ProcessId -ErrorAction SilentlyContinue
    Write-Output ('Stopped own '+$record.Service+' development process; databases/Docker resources preserved.')
}
