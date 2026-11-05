[CmdletBinding()]
param([switch]$Apis)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$manifest=Join-Path $repository 'artifacts\review-website\process.json'
$server=Join-Path $repository 'SRECommerce.UI\dist\SRECommerce.UI\server\server.mjs'
if(Test-Path -LiteralPath $manifest){
    $record=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
    $running=Get-CimInstance Win32_Process -Filter ('ProcessId='+[int]$record.ProcessId)
    if($running){
        if($record.Server -ne $server -or $running.ExecutablePath -ne $record.Executable -or !$running.CommandLine.Contains($server,[StringComparison]::OrdinalIgnoreCase)){throw 'Review PID identity did not match; the process was preserved.'}
        Stop-Process -Id $running.ProcessId
        Write-Output 'Owned review UI stopped.'
    }
}
if($Apis){& (Join-Path $PSScriptRoot 'Stop-CommerceDevelopment.ps1')}
