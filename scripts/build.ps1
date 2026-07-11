[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'bootstrap.ps1')
if (!$?) { throw 'Build bootstrap failed.' }

& (Join-Path $root 'installer\assets\generate-icon.ps1')
if (!$?) { throw 'Application icon generation failed.' }

$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
if (!(Test-Path -LiteralPath $msbuild)) { throw "MSBuild not found: $msbuild" }

$frameworkPath = Join-Path $root '.tools\net48\1.0.3\build\.NETFramework\v4.8'
$arguments = @(
    (Join-Path $root 'CodexOptionPrompts.sln'),
    '/nologo',
    '/m',
    '/t:Build',
    ('/p:Configuration=' + $Configuration),
    '/p:Platform=x64',
    ('/p:FrameworkPathOverride=' + $frameworkPath),
    '/v:minimal'
)

& $msbuild $arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
