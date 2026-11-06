[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$manifest=Join-Path $repository 'artifacts\review-website\mailpit-process.json'
if(!(Test-Path -LiteralPath $manifest)){Write-Output 'No owned native Mailpit manifest exists.';return}
$record=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
$expectedExecutable=Join-Path $repository 'artifacts\mailpit-v1.32.0\mailpit.exe'
$expectedDatabase=Join-Path $repository 'artifacts\review-website\mailpit.db'
$process=Get-CimInstance Win32_Process -Filter ('ProcessId='+[int]$record.ProcessId)
if(!$process){return}
if($record.Executable -ne $expectedExecutable -or $record.Database -ne $expectedDatabase -or $process.ExecutablePath -ne $expectedExecutable -or !$process.CommandLine.Contains($expectedDatabase,[StringComparison]::OrdinalIgnoreCase)){throw 'The Mailpit PID did not match its owned executable/database; it was preserved.'}
Stop-Process -Id $process.ProcessId
Write-Output 'Only owned native Mailpit stopped; captured mail and Docker volumes were preserved.'
