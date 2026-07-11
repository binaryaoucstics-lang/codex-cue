[CmdletBinding()]
param(
    [string]$Filter = '',
    [switch]$SkipUi
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Debug
if (!$?) { throw 'Debug build failed.' }

$tests = Join-Path $root 'build\Debug\CodexCue.Tests.exe'
if (!(Test-Path -LiteralPath $tests)) { throw "Test runner not found: $tests" }
& $tests $Filter
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (!$SkipUi) {
    $uiTests = Join-Path $root 'build\Debug\CodexCue.UiTests.exe'
    if (Test-Path -LiteralPath $uiTests) {
        & $uiTests $Filter
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
