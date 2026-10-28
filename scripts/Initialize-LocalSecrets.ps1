[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$secretsId = '49e676f4-b10e-4f80-a2bd-cf932c81b015'
$secretsDirectory = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretsId"
$secretsFile = Join-Path $secretsDirectory 'secrets.json'
$values = @{}
if (Test-Path -LiteralPath $secretsFile) {
    $existing = Get-Content -LiteralPath $secretsFile -Raw | ConvertFrom-Json
    foreach ($property in $existing.PSObject.Properties) { $values[$property.Name] = $property.Value }
}
if (-not $values['Jwt:Key']) {
    $keyBytes = New-Object byte[] 64
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($keyBytes) } finally { $generator.Dispose() }
    $values['Jwt:Key'] = [Convert]::ToBase64String($keyBytes)
    New-Item -ItemType Directory -Path $secretsDirectory -Force | Out-Null
    $values | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $secretsFile -Encoding utf8
    Write-Output 'Created a random local development JWT key in ASP.NET user-secrets. Its value is not printed.'
} else {
    Write-Output 'Existing local JWT key preserved.'
}
Write-Output 'Database initialization and demo seeding remain disabled.'
