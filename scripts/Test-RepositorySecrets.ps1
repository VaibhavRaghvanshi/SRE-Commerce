[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$privateValues=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
function AddPrivateValue([string]$value) {
    if($value.Length -ge 12){[void]$privateValues.Add($value)}
    if($value -match '(?i)(?:^|;)\s*Password\s*=\s*([^;]+)'){
        if($Matches[1].Length -ge 12){[void]$privateValues.Add($Matches[1])}
    }
}
$environmentFile=Join-Path $repository '.env'
if(Test-Path -LiteralPath $environmentFile){
    foreach($line in Get-Content -LiteralPath $environmentFile){
        if($line -match '^([^#=]+)=(.+)$'){
            $entryName=$Matches[1];$entryValue=$Matches[2]
            if($entryName -match '(?i)PASSWORD|SECRET|TOKEN|CONNECTION'){AddPrivateValue $entryValue.Trim().Trim('"').Trim("'")}
        }
    }
}
$secretRoot=Join-Path $env:APPDATA 'Microsoft\UserSecrets'
if(Test-Path -LiteralPath $secretRoot){
    foreach($directory in Get-ChildItem -LiteralPath $secretRoot -Directory -Filter 'SRECommerce-*'){
        $file=Join-Path $directory.FullName 'secrets.json'
        if(Test-Path -LiteralPath $file){
            $values=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
            foreach($property in $values.PSObject.Properties){
                if($property.Name -match '(?i)PASSWORD|SECRET|TOKEN|PRIVATEKEY|CONNECTION') {AddPrivateValue ([string]$property.Value)}
            }
        }
    }
}
Push-Location $repository
try{
    $files=@(& git ls-files --cached --others --exclude-standard)
    if($LASTEXITCODE -ne 0){throw 'Could not enumerate repository candidates'}
    $failures=[Collections.Generic.List[string]]::new()
    foreach($path in $files){
        if(!(Test-Path -LiteralPath $path -PathType Leaf)){continue}
        if($path -notmatch '(?i)(^|/)\.env\.example$' -and $path -match '(?i)(^|/)(\.env($|\.)|secrets\.json$)|\.(pfx|p12|pem|key|bak|mdf|ldf|db)$'){$failures.Add($path);continue}
        $file=Get-Item -LiteralPath $path
        if($file.Length -gt 5MB){continue}
        $text=[IO.File]::ReadAllText($file.FullName)
        foreach($value in $privateValues){if($text.Contains($value,[StringComparison]::Ordinal)){$failures.Add($path);break}}
        if($text -match '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,}'){$failures.Add($path)}
    }
    if($failures.Count){$failures|Sort-Object -Unique|ForEach-Object{Write-Output ('Private material detected in: '+$_)};throw 'Repository secret check failed; credential values were not printed.'}
    Write-Output ('Repository secret check passed for '+$files.Count+' candidate files; private values were not printed.')
}finally{Pop-Location}
