[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifacts = Join-Path $root 'artifacts'
$staging = Join-Path $artifacts 'staging'
$setup = Join-Path $artifacts 'CodexOptionPrompts-Setup-x64.exe'
$portable = Join-Path $artifacts 'CodexOptionPrompts-portable-x64.zip'
$sums = Join-Path $artifacts 'SHA256SUMS.txt'
$limit = 5000000

function Assert-UnderRoot([string]$Path, [string]$AllowedRoot) {
    $full = [IO.Path]::GetFullPath($Path)
    $allowed = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\') + '\'
    if (!$full.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe generated path: $full" }
}

function Remove-GeneratedDirectory([string]$Path) {
    Assert-UnderRoot $Path $artifacts
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
}

function Invoke-Checked([string]$File, [string[]]$Arguments) {
    & $File $Arguments
    if ($LASTEXITCODE -ne 0) { throw "$File failed with exit code $LASTEXITCODE" }
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
& (Join-Path $PSScriptRoot 'bootstrap.ps1') -Packaging
if (!$?) { throw 'Packaging bootstrap failed.' }
& (Join-Path $root 'installer\assets\generate-icon.ps1')
if (!$?) { throw 'Icon generation failed.' }
& (Join-Path $PSScriptRoot 'test.ps1')
if (!$?) { throw 'Test suite failed.' }
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
if (!$?) { throw 'Release build failed.' }

Remove-GeneratedDirectory $staging
New-Item -ItemType Directory -Path $staging -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'build\Release\CodexOptionPrompts.exe') -Destination (Join-Path $staging 'CodexOptionPrompts.exe') -Force
Copy-Item -LiteralPath (Join-Path $root 'installer\assets\HOOK-TRUST.txt') -Destination (Join-Path $staging 'HOOK-TRUST.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $staging 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $root 'NOTICE') -Destination (Join-Path $staging 'NOTICE') -Force
$pluginDestination = Join-Path $staging 'plugins\codex-option-prompts'
New-Item -ItemType Directory -Path (Split-Path $pluginDestination -Parent) -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'plugins\codex-option-prompts') -Destination $pluginDestination -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $pluginDestination 'bin') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'build\Release\CodexOptionPrompts.exe') -Destination (Join-Path $pluginDestination 'bin\CodexOptionPrompts.exe') -Force

$cacheMaterial = New-Object Text.StringBuilder
foreach ($file in Get-ChildItem -LiteralPath $pluginDestination -File -Recurse | Sort-Object FullName) {
    $relative = $file.FullName.Substring($pluginDestination.Length + 1).Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    [void]$cacheMaterial.Append($relative).Append("`0").Append($hash).Append("`n")
}
$cacheHasher = [Security.Cryptography.SHA256]::Create()
try {
    $cacheBytes = [Text.Encoding]::UTF8.GetBytes($cacheMaterial.ToString())
    $cachebuster = ([BitConverter]::ToString($cacheHasher.ComputeHash($cacheBytes))).Replace('-', '').ToLowerInvariant().Substring(0, 12)
} finally { $cacheHasher.Dispose() }
$codexHome = if ([string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
    Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex'
} else {
    [IO.Path]::GetFullPath($env:CODEX_HOME)
}
$cacheScript = Join-Path $codexHome 'skills\.system\plugin-creator\scripts\update_plugin_cachebuster.py'
$skillValidator = Join-Path $codexHome 'skills\.system\skill-creator\scripts\quick_validate.py'
$pluginValidator = Join-Path $codexHome 'skills\.system\plugin-creator\scripts\validate_plugin.py'
foreach ($validator in @($cacheScript, $skillValidator, $pluginValidator)) {
    if (!(Test-Path -LiteralPath $validator)) { throw "Required Codex validator not found: $validator" }
}
Invoke-Checked 'python' @($cacheScript, $pluginDestination, '--cachebuster', $cachebuster)
Invoke-Checked 'python' @($skillValidator, (Join-Path $pluginDestination 'skills\option-prompts'))
Invoke-Checked 'python' @($pluginValidator, $pluginDestination)

$forbidden = @()
foreach ($file in Get-ChildItem -LiteralPath $staging -File -Recurse) {
    $relative = $file.FullName.Substring($staging.Length + 1).Replace('\', '/')
    if ($relative -match '(^|/)(\.tools|tests?|node_modules)(/|$)' -or $relative -match '\.(pdb|py|mjs)$') { $forbidden += $relative }
}
if ($forbidden.Count -gt 0) { throw "Staging contains forbidden release files: $($forbidden -join ', ')" }

foreach ($old in @($setup, $portable, $sums)) { if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force } }
$iscc = (Get-Content -LiteralPath (Join-Path $root '.tools\inno-path.txt') -Raw).Trim()
if (!(Test-Path -LiteralPath $iscc)) { throw "Inno compiler missing: $iscc" }
$iss = Join-Path $root 'installer\CodexOptionPrompts.iss'
Invoke-Checked $iscc @('/Qp', ("/DSourceRoot=$staging"), ("/DOutputDir=$artifacts"), $iss)
if (!(Test-Path -LiteralPath $setup)) { throw 'Inno Setup did not create the expected installer.' }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($portable, [IO.Compression.ZipArchiveMode]::Create)
try {
    $files = @(Get-ChildItem -LiteralPath $staging -File -Recurse | Sort-Object FullName)
    foreach ($file in $files) {
        $entryName = $file.FullName.Substring($staging.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }

foreach ($path in @($setup, $portable)) {
    $length = (Get-Item -LiteralPath $path).Length
    if ($length -ge $limit) { throw "$path is not below $limit bytes (actual: $length)" }
}

$lines = @()
foreach ($path in @($setup, $portable)) {
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines += "$hash  $(Split-Path $path -Leaf)"
}
[IO.File]::WriteAllLines($sums, $lines, [Text.UTF8Encoding]::new($false))

& (Join-Path $root 'tests\PackageTests.ps1')
if (!$?) { throw 'Package verification failed.' }
Write-Output 'Release packages created.'
Get-ChildItem -LiteralPath $artifacts -File | Select-Object Name,Length
