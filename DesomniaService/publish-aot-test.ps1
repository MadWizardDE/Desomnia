#requires -Version 5
<#
    Publishes DesomniaService as a single self-contained NativeAOT win-x64 binary
    for the AOT feasibility test (see ..\AOT-FEASIBILITY.md).

    It prepends the Visual Studio Installer directory to PATH so the native link
    step can find vswhere.exe. Without this, `dotnet publish` from a plain shell
    fails at the link step with MSB3073 (link.exe exit code 123), even though the
    IL compilation itself succeeds.

    Usage:  .\publish-aot-test.ps1
    Output: .\bin\aot-test\DesomniaService.exe
#>
$ErrorActionPreference = 'Stop'

$installer = "C:\Program Files (x86)\Microsoft Visual Studio\Installer"
if (Test-Path (Join-Path $installer 'vswhere.exe')) {
    $env:PATH = "$installer;$env:PATH"
    Write-Host "vswhere on PATH: $installer" -ForegroundColor DarkGray
} else {
    Write-Warning "vswhere.exe not found under '$installer'. The native link step may fail (MSB3073). " +
                  "Install the VS 'Desktop development with C++' workload or build from Visual Studio."
}

$proj = Join-Path $PSScriptRoot 'DesomniaService.csproj'

$exe = Join-Path $PSScriptRoot 'bin\aot-test\DesomniaService.exe'

# A previous instance still running would lock the output and make the copy step fail (MSB3027).
$running = Get-Process -Name DesomniaService -ErrorAction SilentlyContinue
if ($running) {
    $pids = $running.Id -join ', '
    Write-Warning "DesomniaService.exe is already running (PID $pids); it locks the publish output. Stop it first:  Stop-Process -Name DesomniaService"
}

Write-Host "Publishing NativeAOT (win-x64)..." -ForegroundColor Cyan
# -p:DesomniaAot=true is a global property so it reaches referenced projects (defines DESOMNIA_AOT).
dotnet publish $proj -p:PublishProfile=AotTest -p:DesomniaAot=true
$exit = $LASTEXITCODE

# Trust the build's exit code, not merely the presence of a (possibly stale) file.
if ($exit -ne 0) {
    if (Get-Process -Name DesomniaService -ErrorAction SilentlyContinue) {
        Write-Warning "Output is locked by a running DesomniaService.exe. Stop it and rebuild:  Stop-Process -Name DesomniaService"
    }
    Write-Error "Publish FAILED (exit $exit). The binary at '$exe' may be stale. See errors above."
    exit $exit
}

if (Test-Path $exe) {
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "AOT binary ready: $exe ($mb MB)" -ForegroundColor Green
    Write-Host "Run elevated (with your usual config in place) to exercise the Autofac graph and measure memory." -ForegroundColor Green
} else {
    Write-Error "Publish reported success but '$exe' was not found."
    exit 1
}
