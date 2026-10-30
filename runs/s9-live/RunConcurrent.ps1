param(
    [Parameter(Mandatory)][string]$Manifest,
    [string]$Condition = 'previews-off'
)
$ErrorActionPreference = 'Stop'
$agent = Join-Path $PSScriptRoot '../../src/PokemonRedRL.Agent/bin/Debug/net10.0/PokemonRedRL.Agent.dll'
$directory = Split-Path $Manifest
$run = Get-Content $Manifest -Raw | ConvertFrom-Json
foreach ($rate in @(4, 40)) {
    $owned = @()
    $deadline = [DateTime]::UtcNow.AddSeconds(80)
    try {
        foreach ($slot in $run.Agents) {
            $log = Join-Path $directory "$Condition-$rate-hz-slot-$($slot.Index).log"
            $errorLog = Join-Path $directory "$Condition-$rate-hz-slot-$($slot.Index).stderr.log"
            $arguments = @($agent, '--Vision:Enabled', 'true', '--vision-diagnostics', 'true',
                '--manifest', $Manifest, '--agent-index', $slot.Index, '--host', $slot.Host,
                '--port', $slot.Port, '--viewport', '74,69,191,173', '--duration-seconds', '60',
                '--capture-hz', $rate)
            $process = Start-Process dotnet -ArgumentList $arguments -PassThru -NoNewWindow `
                -RedirectStandardOutput $log -RedirectStandardError $errorLog
            $owned += [pscustomobject]@{ Process = $process; Log = $log; Slot = $slot.Index }
        }
        foreach ($item in $owned) {
            $remaining = [Math]::Max(0, ($deadline - [DateTime]::UtcNow).TotalMilliseconds)
            if (-not $item.Process.WaitForExit([int]$remaining)) {
                throw "Diagnostic exceeded the 80-second condition deadline: slot $($item.Slot)."
            }
            $item.Process.WaitForExit()
            $final = Get-Content $item.Log | Select-Object -Last 1
            Write-Output "$Condition slot=$($item.Slot) rate=$rate exit=$($item.Process.ExitCode) $final"
            if ($item.Process.ExitCode -ne 0) { throw "Diagnostic failed for slot $($item.Slot)." }
        }
    }
    finally {
        foreach ($item in $owned) {
            if (-not $item.Process.HasExited) { $item.Process.Kill($true); $item.Process.WaitForExit() }
            $item.Process.Dispose()
        }
    }
}