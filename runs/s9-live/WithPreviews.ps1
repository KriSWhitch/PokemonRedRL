param([Parameter(Mandatory)][string]$Manifest)
$ErrorActionPreference = 'Stop'
$directory = Split-Path $Manifest
$assembly = Join-Path $PSScriptRoot '../s9-preview/bin/Debug/net10.0-windows/Preview.dll'
$eventName = 'Local\PokemonRedRLS9Preview-' + [Guid]::NewGuid().ToString('N')
$ready = [Threading.EventWaitHandle]::new($false, [Threading.EventResetMode]::ManualReset, $eventName)
$preview = $null
try {
    $preview = Start-Process dotnet -PassThru -NoNewWindow `
        -ArgumentList @($assembly, $Manifest, '150', $eventName) `
        -RedirectStandardOutput (Join-Path $directory 'preview-on.log') `
        -RedirectStandardError (Join-Path $directory 'preview-on.stderr.log')
    if (-not $ready.WaitOne(15000)) { throw 'Preview window did not become ready in 15 seconds.' }
    & (Join-Path $PSScriptRoot 'RunConcurrent.ps1') -Manifest $Manifest -Condition 'previews-on'
    if ($preview.HasExited) { throw 'Preview window exited before the concurrent tests finished.' }
    if (-not $preview.CloseMainWindow()) { throw 'Could not close the diagnostic-owned preview window.' }
    if (-not $preview.WaitForExit(10000)) { throw 'Preview window did not close in 10 seconds.' }
    $preview.WaitForExit()
    Get-Content (Join-Path $directory 'preview-on.log')
    if ($preview.ExitCode -ne 0) { throw "Preview failed with exit $($preview.ExitCode)." }
}
finally {
    if ($null -ne $preview) {
        if (-not $preview.HasExited) {
            [void]$preview.CloseMainWindow()
            if (-not $preview.WaitForExit(3000)) { $preview.Kill($true); $preview.WaitForExit() }
        }
        $preview.Dispose()
    }
    $ready.Dispose()
}