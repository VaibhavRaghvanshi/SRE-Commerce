[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$directory=Join-Path $env:APPDATA 'Microsoft\UserSecrets\SRECommerce-Identity-Development'
$file=Join-Path $directory 'secrets.json'
$values=@{}
if(Test-Path -LiteralPath $file){$existing=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json; foreach($property in $existing.PSObject.Properties){$values[$property.Name]=$property.Value}}
if(-not $values['Bootstrap:Email']){$values['Bootstrap:Email']='platform-admin@srecommerce.local'}
if(-not $values['Bootstrap:Password']){
    $bytes=New-Object byte[] 32
    $rng=[System.Security.Cryptography.RandomNumberGenerator]::Create()
    try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}
    $values['Bootstrap:Password']='Aa1!'+[Convert]::ToBase64String($bytes)
}
New-Item -ItemType Directory -Path $directory -Force|Out-Null
$values|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $file -Encoding utf8
Write-Output 'Development bootstrap credentials saved/preserved in Identity user-secrets. Values are not printed. Run --initialize explicitly after applying reviewed migrations.'
