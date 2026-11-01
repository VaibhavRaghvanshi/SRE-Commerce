[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$secretDirectory=Join-Path $env:APPDATA 'Microsoft\UserSecrets\SRECommerce-Payment-Development'
$secretFile=Join-Path $secretDirectory 'secrets.json'
$values=if(Test-Path -LiteralPath $secretFile){Get-Content -LiteralPath $secretFile -Raw|ConvertFrom-Json -AsHashtable}else{@{}}
$keyName='Sandbox:WebhookSigningKey'
if($values.ContainsKey($keyName)){
    if([Text.Encoding]::UTF8.GetByteCount([string]$values[$keyName]) -lt 32){throw 'Existing sandbox signing key is too short; it was preserved.'}
    Write-Output 'Existing Payment sandbox signing key preserved.'
    return
}
$values[$keyName]=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
New-Item -ItemType Directory -Path $secretDirectory -Force|Out-Null
$json=$values|ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($secretFile,$json,[Text.UTF8Encoding]::new($false))
Write-Output 'Private development-only Payment sandbox signing key initialized. No SQL login or OAuth client was provisioned.'
