[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$limit = 5000000
$packageScript = Get-Content -LiteralPath (Join-Path $root 'scripts\package.ps1') -Raw
if ($packageScript -match '(?i)[A-Z]:\\Users\\[^\\]+') {
    throw 'Packaging source must not contain a machine-specific user profile path.'
}
if ($packageScript -notmatch 'cacheMaterial' -or $packageScript -match 'rev-parse --short') {
    throw 'Packaging must derive the plugin cachebuster from release contents.'
}
foreach ($scriptPath in Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -Filter '*.ps1' -File) {
    $source = Get-Content -LiteralPath $scriptPath.FullName -Raw
    if ($source -match '(?ms)&\s*\(Join-Path[^\r\n]+\.ps1''\)[^\r\n]*\r?\nif\s*\(\$LASTEXITCODE') {
        throw "PowerShell child scripts must be checked with `$? instead of `$LASTEXITCODE: $($scriptPath.Name)"
    }
}
$required = @(
    (Join-Path $root 'artifacts\CodexCue-Setup-x64.exe'),
    (Join-Path $root 'artifacts\CodexCue-portable-x64.zip'),
    (Join-Path $root 'artifacts\SHA256SUMS.txt')
)

foreach ($path in $required) {
    if (!(Test-Path -LiteralPath $path)) { throw "Missing artifact: $path" }
}
foreach ($path in $required[0..1]) {
    if ((Get-Item -LiteralPath $path).Length -ge $limit) { throw "$path is not below $limit bytes" }
}

$zip = [IO.Compression.ZipFile]::OpenRead($required[1])
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName })
    if ($entries -notcontains 'plugins/codex-cue/bin/CodexCue.exe') {
        throw 'Portable ZIP is missing the MCP executable.'
    }
    if ($entries -notcontains 'plugins/codex-cue/hooks/hooks.json') {
        throw 'Portable ZIP is missing the automatic routing hooks.'
    }
    if ($entries -notcontains 'HOOK-TRUST.txt') {
        throw 'Portable ZIP is missing the one-time hook trust instructions.'
    }
    foreach ($legal in @('LICENSE', 'NOTICE')) {
        if ($entries -notcontains $legal) { throw "Portable ZIP is missing required attribution file: $legal" }
    }
    foreach ($entry in $entries) {
        if ($entry -match '(^|/)(\.tools|tests?|node_modules)(/|$)' -or $entry -match '\.(pdb|py|mjs)$') {
            throw "Portable ZIP contains a forbidden release entry: $entry"
        }
    }
} finally { $zip.Dispose() }

$hashLines = @(Get-Content -LiteralPath $required[2] | Where-Object { $_.Trim() })
if ($hashLines.Count -ne 2) { throw 'SHA256SUMS.txt must contain exactly two hashes.' }
foreach ($line in $hashLines) {
    if ($line -notmatch '^[0-9a-f]{64}  CodexCue-(Setup-x64\.exe|portable-x64\.zip)$') {
        throw "Invalid SHA256SUMS line: $line"
    }
}

Write-Output 'Package gates passed.'
