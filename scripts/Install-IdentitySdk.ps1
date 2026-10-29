[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=Join-Path $root 'artifacts\dotnet10'
$exe=Join-Path $target 'dotnet.exe'
if((Test-Path -LiteralPath $exe) -and ((& $exe --list-sdks) -match '^10\.0\.403 ')){
    Write-Output 'Stable Identity SDK 10.0.403 already installed locally.'
    return
}
New-Item -ItemType Directory -Path (Join-Path $root artifacts) -Force|Out-Null
$metadata=Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$sdk=@($metadata.releases|ForEach-Object{$_.sdks}|Where-Object version -eq '10.0.403')[0]
$file=@($sdk.files|Where-Object{$_.rid -eq 'win-x64' -and $_.name -like '*.zip'})[0]
if(-not $file){throw 'Official SDK metadata is unavailable.'}
$archive=Join-Path $root 'artifacts\dotnet-sdk-10.0.403-win-x64.zip'
Invoke-WebRequest -Uri $file.url -OutFile $archive
if((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $file.hash){throw 'SDK SHA512 mismatch; archive will not be installed.'}
Expand-Archive -LiteralPath $archive -DestinationPath $target
Write-Output 'Stable Identity SDK installed locally; official SHA512 verified. Global SDK settings were not changed.'
