[CmdletBinding()]
param([switch]$Provision)
$ErrorActionPreference='Stop'
if(!$Provision){throw 'Review and approve local RabbitMQ/Valkey account provisioning before using -Provision.'}
$repository=Split-Path $PSScriptRoot -Parent
$services=@('Identity','Tenant','Theme','Storefront','Catalog','Inventory','Pricing','Cart','Order','Payment','Shipping','Notification','Manufacturing','Discovery')
$rootDirectory=Join-Path $env:APPDATA 'Microsoft\UserSecrets\SRECommerce-Backend-Infrastructure'
$rootFile=Join-Path $rootDirectory 'secrets.json'
$root=@{};if(Test-Path -LiteralPath $rootFile){$old=Get-Content -LiteralPath $rootFile -Raw|ConvertFrom-Json;foreach($entry in $old.PSObject.Properties){$root[$entry.Name]=$entry.Value}}
function NewPrivateValue{return [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))}
if(!$root['Broker:AdminUser']){$root['Broker:AdminUser']='srecommerce_bootstrap_'+[Guid]::NewGuid().ToString('N').Substring(0,12)}
if(!$root['Broker:AdminPassword']){$root['Broker:AdminPassword']=NewPrivateValue}
New-Item -ItemType Directory -Path $rootDirectory -Force|Out-Null
[IO.File]::WriteAllText($rootFile,($root|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
$environmentFile=Join-Path $repository '.env';$environmentText=if(Test-Path -LiteralPath $environmentFile){[IO.File]::ReadAllText($environmentFile)}else{''}
foreach($pair in @{BACKEND_BROKER_ADMIN_USER=$root['Broker:AdminUser'];BACKEND_BROKER_ADMIN_PASSWORD=$root['Broker:AdminPassword']}.GetEnumerator()){
    $line=$pair.Key+'='+$pair.Value
    if($environmentText -match ('(?m)^'+$pair.Key+'=')){$environmentText=[regex]::Replace($environmentText,('(?m)^'+$pair.Key+'=[^\r\n]*'),$line)}else{$environmentText=$environmentText.TrimEnd()+[Environment]::NewLine+$line+[Environment]::NewLine}
}
[IO.File]::WriteAllText($environmentFile,$environmentText,[Text.UTF8Encoding]::new($false))
$acl=@('user default off');$accounts=@()
foreach($service in $services){
    $directory=Join-Path $env:APPDATA ('Microsoft\UserSecrets\SRECommerce-'+$service+'-Development');$file=Join-Path $directory 'secrets.json';$values=@{}
    if(Test-Path -LiteralPath $file){$old=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json;foreach($entry in $old.PSObject.Properties){$values[$entry.Name]=$entry.Value}}
    if(!$values['Messaging:Username']){$values['Messaging:Username']='svc_'+$service.ToLowerInvariant()+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)}
    if(!$values['Messaging:Password']){$values['Messaging:Password']=NewPrivateValue}
    if(!$values['Cache:User']){$values['Cache:User']='cache_'+$service.ToLowerInvariant()}
    if(!$values['Cache:Password']){$values['Cache:Password']=NewPrivateValue}
    $values['Messaging:Host']='localhost';$values['Messaging:Port']='5672';$values['Messaging:VirtualHost']='srecommerce';$values['Cache:Host']='localhost';$values['Cache:Port']='6379'
    if($values['Cache:User'] -notmatch '^cache_[a-z]+$' -or $values['Cache:Password'] -notmatch '^[A-F0-9]{64}$'){throw 'Unexpected managed cache credential format; existing files preserved.'}
    $acl+=('user '+$values['Cache:User']+' on >'+$values['Cache:Password']+' ~SRECommerce:'+$service+':* &* +@read +@write +@connection +eval +evalsha +script|load +info -config -acl -flushall -flushdb -shutdown -monitor -replicaof -client|kill -client|pause -client|unpause')
    New-Item -ItemType Directory -Path $directory -Force|Out-Null
    [IO.File]::WriteAllText($file,($values|ConvertTo-Json),[Text.UTF8Encoding]::new($false));$accounts+=@{Service=$service;User=$values['Messaging:Username'];Password=$values['Messaging:Password']}
}
$configurationDirectory=Join-Path $repository 'artifacts\backend-infra';New-Item -ItemType Directory -Path $configurationDirectory -Force|Out-Null
[IO.File]::WriteAllLines((Join-Path $configurationDirectory 'users.acl'),$acl,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $configurationDirectory 'valkey.conf'),"bind 0.0.0.0`nprotected-mode yes`naclfile /etc/valkey/users.acl`ndir /data`nappendonly yes`nmaxmemory 64mb`nmaxmemory-policy allkeys-lru`n",[Text.UTF8Encoding]::new($false))
Push-Location $repository
try{& docker compose -f compose.yaml -f compose.infra.yaml --profile backend-infra up -d rabbitmq valkey;if($LASTEXITCODE -ne 0){throw 'Backend infrastructure startup failed; volumes/configuration preserved.'}}
finally{Pop-Location}
$headers=@{Authorization='Basic '+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($root['Broker:AdminUser']+':'+$root['Broker:AdminPassword']))}
$base='http://127.0.0.1:15672/api';$ready=$false
for($attempt=0;$attempt -lt 12 -and !$ready;$attempt++){
    try{[void](Invoke-RestMethod "$base/overview" -Headers $headers -TimeoutSec 3);$ready=$true}catch{Start-Sleep -Seconds 2}
}
if(!$ready){throw 'RabbitMQ management not ready; no existing credential was reset.'}
foreach($account in $accounts){
    $name=[uri]::EscapeDataString($account.User);$exists=$true
    try{[void](Invoke-RestMethod "$base/users/$name" -Headers $headers -TimeoutSec 5)}catch{if($_.Exception.Response.StatusCode.value__ -eq 404){$exists=$false}else{throw 'Broker account lookup failed; values not printed.'}}
    if(!$exists){Invoke-RestMethod "$base/users/$name" -Headers $headers -Method Put -ContentType 'application/json' -Body (@{password=$account.Password;tags=''}|ConvertTo-Json) -TimeoutSec 5|Out-Null}
    $prefix='srecommerce\.'+$account.Service.ToLowerInvariant()+'\.'
    $permission=@{configure='^(srecommerce\.events|srecommerce\.dead|'+$prefix+'.*)$';write='^(srecommerce\.events|srecommerce\.dead|'+$prefix+'.*)$';read='^'+$prefix+'.*$'}
    Invoke-RestMethod "$base/permissions/srecommerce/$name" -Headers $headers -Method Put -ContentType 'application/json' -Body ($permission|ConvertTo-Json) -TimeoutSec 5|Out-Null
    $topic=@{exchange='srecommerce.events';write='^'+$account.Service.ToLowerInvariant()+'\..*';read='.*'}
    Invoke-RestMethod "$base/topic-permissions/srecommerce/$name" -Headers $headers -Method Put -ContentType 'application/json' -Body ($topic|ConvertTo-Json) -TimeoutSec 5|Out-Null
}
Write-Output 'Local broker/cache accounts configured. Existing passwords and named volumes preserved; no credential values printed. Authenticated transport/cache integration probes still required.'
