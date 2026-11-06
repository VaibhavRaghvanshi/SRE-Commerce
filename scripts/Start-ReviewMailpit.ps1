[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $repository 'artifacts\review-website'
$executable=Join-Path $repository 'artifacts\mailpit-v1.32.0\mailpit.exe'
New-Item -ItemType Directory -Path $directory -Force|Out-Null
$occupied=@(Get-NetTCPConnection -LocalPort 1025,8025 -State Listen -ErrorAction SilentlyContinue)
if($occupied.Count){
    if(@($occupied.LocalPort|Sort-Object -Unique).Count -eq 2){
        try{$page=Invoke-WebRequest -Uri 'http://127.0.0.1:8025/' -TimeoutSec 5;if($page.StatusCode -eq 200 -and $page.Content -match '<title>\s*Mailpit') {Write-Output 'Existing loopback Mailpit inbox was preserved.';return}}catch{}
    }
    throw 'A mail capture port is occupied by an unverified process. It was preserved.'
}
if(!(Test-Path -LiteralPath $executable)){throw 'The previously installed Mailpit executable is missing. No software was downloaded or installed.'}
$database=Join-Path $directory 'mailpit.db'
$process=Start-Process -FilePath $executable -ArgumentList @('--listen','127.0.0.1:8025','--smtp','127.0.0.1:1025','--database',$database,'--smtp-disable-rdns','--quiet') -WorkingDirectory $repository -WindowStyle Hidden -RedirectStandardOutput (Join-Path $directory 'mailpit.out.log') -RedirectStandardError (Join-Path $directory 'mailpit.err.log') -PassThru
@{ProcessId=$process.Id;Executable=$executable;Database=$database}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $directory 'mailpit-process.json')
$deadline=[DateTime]::UtcNow.AddSeconds(20)
while([DateTime]::UtcNow -lt $deadline){
    if($process.HasExited){throw 'Native Mailpit exited. Its database and logs were preserved.'}
    try{$page=Invoke-WebRequest -Uri 'http://127.0.0.1:8025/' -TimeoutSec 2;if($page.StatusCode -eq 200){Write-Output 'Native Mailpit capture is available at http://localhost:8025; SMTP is loopback port 1025.';return}}catch{}
    Start-Sleep -Milliseconds 300
}
throw 'Mailpit readiness timed out; owned process and logs were preserved.'
