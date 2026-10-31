[CmdletBinding()]
param([switch]$Build,[switch]$InitializeIdentity,[switch]$StartUi,[ValidateSet('Identity','Tenant','Theme','Storefront','Catalog','Inventory','ApiGateway')][string[]]$Services=@('Identity','Tenant','Catalog','Inventory','ApiGateway'))
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$runtime=Join-Path $repository 'artifacts\dotnet10\dotnet.exe'
if(!(Test-Path -LiteralPath $runtime)){throw 'Run Install-IdentitySdk.ps1 first.'}
$runDirectory=Join-Path $repository 'artifacts\commerce-development'
New-Item -ItemType Directory -Force -Path $runDirectory|Out-Null
if($Build){& $runtime build (Join-Path $repository 'SRECommerce.slnx') -c Release -m:1 -nr:false --nologo;if($LASTEXITCODE -ne 0){throw 'Backend build failed; no services started.'}}
$specs=@(
    @{Name='Identity';Port=5181;Extra=@{'Identity__Issuer'='http://localhost:5181/';'Mailpit__Enabled'='true';'Mailpit__Host'='localhost';'Mailpit__Port'='1025';'Cors__Origins__0'='http://localhost:4200';'Cors__Origins__1'='http://localhost:4000'}},
    @{Name='Tenant';Port=5182;Extra=@{'Identity__BaseUrl'='http://localhost:5181/'}},
    @{Name='Theme';Port=5183;Extra=@{'Identity__BaseUrl'='http://localhost:5181/';'Tenant__BaseUrl'='http://localhost:5182/'}},
    @{Name='Storefront';Port=5184;Extra=@{'Identity__BaseUrl'='http://localhost:5181/';'Tenant__BaseUrl'='http://localhost:5182/'}},
    @{Name='Catalog';Port=5185;Extra=@{'Identity__BaseUrl'='http://localhost:5181/';'Tenant__BaseUrl'='http://localhost:5182/'}},
    @{Name='Inventory';Port=5186;Extra=@{'Identity__BaseUrl'='http://localhost:5181/';'Tenant__BaseUrl'='http://localhost:5182/';'Catalog__BaseUrl'='http://localhost:5185/'}},
    @{Name='ApiGateway';Port=5180;Extra=@{'Services__Identity'='http://localhost:5181/';'Services__Tenant'='http://localhost:5182/';'Services__Theme'='http://localhost:5183/';'Services__Storefront'='http://localhost:5184/';'Services__Catalog'='http://localhost:5185/';'Services__Inventory'='http://localhost:5186/'}}
)
$processes=@{}
$processManifest=Join-Path $runDirectory 'processes.json'
if(Test-Path -LiteralPath $processManifest){foreach($record in (Get-Content -LiteralPath $processManifest -Raw|ConvertFrom-Json)){$processes[$record.Service]=$record}}
foreach($spec in $specs|Where-Object {$_.Name -in $Services}){
    $apiName=if($spec.Name -eq 'ApiGateway'){'SRECommerce.ApiGateway'}else{'SRECommerce.'+$spec.Name+'.API'}
    $working=Join-Path $repository ($apiName+'\'+$apiName)
    $dll=Join-Path $working ('bin\Release\net10.0\'+$apiName+'.dll')
    if(!(Test-Path -LiteralPath $dll)){throw ('Missing build: '+$spec.Name+'; rerun with -Build.')}
    if(Get-NetTCPConnection -LocalPort $spec.Port -State Listen -ErrorAction SilentlyContinue){
        try{$health=Invoke-RestMethod -Uri ('http://localhost:'+$spec.Port+'/health/live') -TimeoutSec 3}catch{throw ('Port '+$spec.Port+' is occupied by an unverified process; it was not stopped.')}
        if($health.service -ne $spec.Name){throw ('Port '+$spec.Port+' belongs to another service; it was preserved.')}
        Write-Output ($spec.Name+' already listening on '+$spec.Port);continue
    }
    $environment=@{'ASPNETCORE_ENVIRONMENT'='Development'}+$spec.Extra;$previous=@{}
    foreach($key in $environment.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$environment[$key],'Process')}
    try{
        if($spec.Name -eq 'Identity' -and $InitializeIdentity){Push-Location $working;try{& $runtime $dll --initialize;if($LASTEXITCODE -ne 0){throw 'Explicit Identity client initialization failed'}}finally{Pop-Location}}
        $process=Start-Process -FilePath $runtime -ArgumentList @($dll,'--urls',('http://localhost:'+$spec.Port)) -WorkingDirectory $working -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runDirectory ($spec.Name+'.out.log')) -RedirectStandardError (Join-Path $runDirectory ($spec.Name+'.err.log')) -PassThru
        $processes[$spec.Name]=@{Service=$spec.Name;Port=$spec.Port;ProcessId=$process.Id;Executable=$runtime}
    }finally{foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key],'Process')}}
    $ready=$false
    for($attempt=0;$attempt -lt 15;$attempt++){try{$health=Invoke-RestMethod -Uri ('http://localhost:'+$spec.Port+'/health/live') -TimeoutSec 2;if($health.service -eq $spec.Name){$ready=$true;break}}catch{};if($process.HasExited){break};Start-Sleep -Milliseconds 400}
    if(!$ready){@($processes.Values)|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $processManifest;throw ($spec.Name+' failed to start. Logs and previously started services were preserved under ignored artifacts.')}
    Write-Output ($spec.Name+' started on http://localhost:'+$spec.Port)
}
if($processes.Count){@($processes.Values)|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $processManifest}
if($StartUi){
    if(Get-NetTCPConnection -LocalPort 4200 -State Listen -ErrorAction SilentlyContinue){Write-Output 'UI port 4200 already occupied; existing UI was preserved.'}
    else{
        $node=(Get-Command node -ErrorAction Stop).Source;$cli=Join-Path $repository 'SRECommerce.UI\node_modules\@angular\cli\bin\ng.js'
        $previousWorkers=$env:NG_BUILD_MAX_WORKERS;$env:NG_BUILD_MAX_WORKERS='1'
        try{$ui=Start-Process -FilePath $node -ArgumentList @($cli,'serve','--proxy-config','proxy.conf.json','--host','localhost','--port','4200') -WorkingDirectory (Join-Path $repository 'SRECommerce.UI') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runDirectory 'UI.out.log') -RedirectStandardError (Join-Path $runDirectory 'UI.err.log') -PassThru;$processes['UI']=@{Service='UI';Port=4200;ProcessId=$ui.Id;Executable=$node};@($processes.Values)|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $processManifest;Write-Output ('Angular development server starting on http://localhost:4200; process '+$ui.Id)}finally{$env:NG_BUILD_MAX_WORKERS=$previousWorkers}
    }
}
Write-Output 'No SQL migrations, database resets, runtime-login provisioning or Docker volume changes were performed by this launcher.'
