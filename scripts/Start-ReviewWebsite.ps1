[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$uiServer=Join-Path $repository 'SRECommerce.UI\dist\SRECommerce.UI\server\server.mjs'
if(!(Test-Path -LiteralPath $uiServer)){throw 'Build the Angular application before starting the review website.'}
$directory=Join-Path $repository 'artifacts\review-website'
New-Item -ItemType Directory -Path $directory -Force|Out-Null
$manifest=Join-Path $directory 'process.json'
$reuseUi=$false
if(Get-NetTCPConnection -LocalPort 4000 -State Listen -ErrorAction SilentlyContinue){
    if(Test-Path -LiteralPath $manifest){
        $record=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
        $running=Get-CimInstance Win32_Process -Filter ('ProcessId='+[int]$record.ProcessId)
        if($running -and $running.ExecutablePath -eq $record.Executable -and $running.CommandLine.Contains($uiServer,[StringComparison]::OrdinalIgnoreCase)){
            $reuseUi=$true
        }
    }
    if(!$reuseUi){throw 'Port 4000 is occupied by another process. It was preserved.'}
}
# Detect the actual named SQL instance's availability before claiming the website
# is ready. Never start the unrelated default MSSQLSERVER service to replace it.
$sqlProbe=& sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -l 5 -t 5 -d SRECommerce_TenantDB -Q 'SET NOCOUNT ON; SELECT TOP(1) Code FROM dbo.BusinessTypes;' 2>&1
if($LASTEXITCODE -ne 0){throw 'MSSQL22 did not answer the read-only Tenant probe. No database or service configuration was changed; review the SQL recovery runbook.'}
& (Join-Path $PSScriptRoot 'Start-ReviewMailpit.ps1')
# Only implemented services used by the current storefront/admin review; the optional
# Manufacturing service, Kafka, SQL containers and credential scripts are not started.
& (Join-Path $PSScriptRoot 'Start-CommerceDevelopment.ps1') -Services Identity,Tenant,Theme,Storefront,Catalog,Inventory,Pricing,Cart,Order,Payment,Shipping,Notification,Discovery,ApiGateway
$node=(Get-Command node -ErrorAction Stop).Source
Push-Location $repository
try{
    & $node (Join-Path $PSScriptRoot 'verify-review-readiness.mjs')
    if($LASTEXITCODE -ne 0){throw 'A selected API failed SQL-backed readiness. Processes/logs were preserved; review artifacts/review-website/readiness.json.'}
}finally{Pop-Location}
if($reuseUi){Write-Output 'Owned review UI was reused and all fourteen readiness probes passed at http://localhost:4000';return}
$environment=@{'HOST'='127.0.0.1';'PORT'='4000';'COMMERCE_PROXY_TARGET'='http://127.0.0.1:5180'};$previous=@{}
foreach($key in $environment.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$environment[$key],'Process')}
try {
    $ui=Start-Process -FilePath $node -ArgumentList @($uiServer) -WorkingDirectory (Join-Path $repository 'SRECommerce.UI') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $directory 'ui.out.log') -RedirectStandardError (Join-Path $directory 'ui.err.log') -PassThru
    @{ProcessId=$ui.Id;Executable=$node;Server=$uiServer;Url='http://localhost:4000';BoundHost='127.0.0.1'}|ConvertTo-Json|Set-Content -LiteralPath $manifest
} finally {
    foreach($key in $previous.Keys){if($null -eq $previous[$key]){Remove-Item -LiteralPath ('Env:'+$key) -ErrorAction SilentlyContinue}else{[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}}
}
$deadline=[DateTime]::UtcNow.AddSeconds(60)
while([DateTime]::UtcNow -lt $deadline){
    if($ui.HasExited){throw 'Review UI exited. Logs and API processes were preserved in artifacts.'}
    try{$response=Invoke-WebRequest -Uri 'http://127.0.0.1:4000/' -TimeoutSec 3;if($response.StatusCode -eq 200){Write-Output 'Review website responds at http://localhost:4000 (loopback only).';return}}catch{}
    Start-Sleep -Milliseconds 500
}
throw 'Review website did not respond within 60 seconds. Its process and logs were preserved for inspection.'
