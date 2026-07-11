[CmdletBinding()]
param(
    [string]$SetupPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SetupPath)) {
    $SetupPath = Join-Path $PSScriptRoot '..\artifacts\CodexOptionPrompts-Setup-x64.exe'
}
$setup = [IO.Path]::GetFullPath($SetupPath)
if (!(Test-Path -LiteralPath $setup)) { throw "Installer missing: $setup" }

$program = Join-Path $env:LOCALAPPDATA 'Programs\CodexOptionPrompts\CodexOptionPrompts.exe'
$plugin = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'plugins\codex-option-prompts'
$marketplace = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.agents\plugins\marketplace.json'
$data = Join-Path $env:LOCALAPPDATA 'CodexOptionPrompts'
$backups = Join-Path $data 'backups'
$oldPrototype = (Test-Path -LiteralPath (Join-Path $plugin 'scripts\server.mjs')) -or
    (Test-Path -LiteralPath (Join-Path $plugin 'scripts\option-overlay.py'))
$before = @()
if (Test-Path -LiteralPath $backups) { $before = @(Get-ChildItem -LiteralPath $backups -Directory | ForEach-Object { $_.FullName }) }

Get-CimInstance Win32_Process -Filter "Name='CodexOptionPrompts.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -eq [IO.Path]::GetFullPath($program) } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

$install = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER' -WindowStyle Hidden -PassThru
$install.WaitForExit()
if ($install.ExitCode -ne 0) { throw "Installer exited with code $($install.ExitCode)." }

foreach ($required in @(
    $program,
    (Join-Path $plugin '.codex-plugin\plugin.json'),
    (Join-Path $plugin '.mcp.json'),
    (Join-Path $plugin 'bin\CodexOptionPrompts.exe'),
    (Join-Path $plugin 'skills\option-prompts\SKILL.md'),
    (Join-Path $plugin 'hooks\hooks.json'),
    $marketplace,
    (Join-Path $data 'managed-manifest.json'),
    (Join-Path $data 'install-status.json')
)) {
    if (!(Test-Path -LiteralPath $required)) { throw "Installed file missing: $required" }
}

$manifest = Get-Content -LiteralPath (Join-Path $plugin '.codex-plugin\plugin.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'codex-option-prompts') { throw 'Installed plugin name is invalid.' }
if ($manifest.version -notmatch '^1\.2\.1\+codex\.[0-9a-f]{7,40}$') {
    throw "Installed plugin cachebuster version is invalid: $($manifest.version)"
}

$marketplaceJson = Get-Content -LiteralPath $marketplace -Raw | ConvertFrom-Json
$entry = @($marketplaceJson.plugins | Where-Object { $_.name -eq 'codex-option-prompts' })
if ($entry.Count -ne 1) { throw 'Personal marketplace does not contain exactly one managed plugin entry.' }
if ($entry[0].source.path -ne './plugins/codex-option-prompts') { throw 'Personal marketplace plugin path is invalid.' }

$after = @()
if (Test-Path -LiteralPath $backups) { $after = @(Get-ChildItem -LiteralPath $backups -Directory | Sort-Object Name) }
$newBackups = @($after | Where-Object { $before -notcontains $_.FullName })
if ($newBackups.Count -eq 0) { throw 'Installation did not create a timestamped backup.' }
$latest = $newBackups[-1]
if (!(Test-Path -LiteralPath (Join-Path $latest.FullName 'manifest.json'))) { throw 'Backup manifest is missing.' }
if ($oldPrototype) {
    $nodeBackup = Get-ChildItem -LiteralPath $latest.FullName -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq 'server.mjs' -or $_.Name -eq 'option-overlay.py' }
    if (@($nodeBackup).Count -eq 0) { throw 'The previous Node/Tkinter prototype was not preserved in the new backup.' }
} else {
    $prototypeBackup = Get-ChildItem -LiteralPath $backups -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq 'server.mjs' -or $_.Name -eq 'option-overlay.py' }
    if (@($prototypeBackup).Count -eq 0) { Write-Warning 'No earlier Node/Tkinter prototype backup was found on this machine.' }
}

$status = Get-Content -LiteralPath (Join-Path $data 'install-status.json') -Raw | ConvertFrom-Json
if ($status.productVersion -ne $manifest.version) { throw 'Install status and plugin versions do not match.' }
if ([string]::IsNullOrWhiteSpace($status.codexCli) -or !(Test-Path -LiteralPath $status.codexCli)) {
    throw 'A compatible Codex CLI was not found during installation.'
}
$codexConfig = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex\config.toml'
$codexConfigText = Get-Content -LiteralPath $codexConfig -Raw
if ($codexConfigText -notmatch '(?ms)\[plugins\."codex-option-prompts@personal"\.mcp_servers\.codex_option_prompts\].*?default_tools_approval_mode\s*=\s*"approve"') {
    throw 'Codex config does not auto-approve the direct option prompt MCP tool.'
}
$startupCommand = Get-ItemPropertyValue -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'CodexOptionPrompts' -ErrorAction SilentlyContinue
$expectedStartup = '"' + $program + '" --host'
if ($startupCommand -ne $expectedStartup) { throw 'The independent desktop prompt host is not registered for user startup.' }
$hostProcess = Get-CimInstance Win32_Process -Filter "Name='CodexOptionPrompts.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -eq [IO.Path]::GetFullPath($program) -and $_.CommandLine -match '--host' }
if (@($hostProcess).Count -lt 1) { throw 'The independent desktop prompt host is not running after installation.' }
if ($status.pluginInstallExitCode -ne 0) {
    if ($status.refreshExitCode -ne 0) {
        & $status.codexCli plugin marketplace add ([Environment]::GetFolderPath('UserProfile'))
        if ($LASTEXITCODE -ne 0) {
            & $status.codexCli plugin marketplace upgrade $status.marketplaceName
            if ($LASTEXITCODE -ne 0) { throw "Codex personal marketplace registration failed with exit code $LASTEXITCODE." }
        }
    }
    & $status.codexCli plugin add ('codex-option-prompts@' + $status.marketplaceName)
    if ($LASTEXITCODE -ne 0) { throw "Codex plugin activation failed with exit code $LASTEXITCODE." }
}

$pluginList = (& $status.codexCli plugin list 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $pluginList -notmatch 'codex-option-prompts@personal\s+installed, enabled') {
    throw 'Codex plugin list does not show codex-option-prompts as installed and enabled.'
}
$mcpList = (& $status.codexCli mcp list 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $mcpList -notmatch '(?m)^codex_option_prompts\s+') {
    throw 'Codex MCP list does not show codex_option_prompts.'
}

$stagedProgram = Join-Path ([IO.Path]::GetDirectoryName($setup)) 'staging\CodexOptionPrompts.exe'
if (Test-Path -LiteralPath $stagedProgram) {
    $installedHash = (Get-FileHash -LiteralPath $program -Algorithm SHA256).Hash
    $stagedHash = (Get-FileHash -LiteralPath $stagedProgram -Algorithm SHA256).Hash
    if ($installedHash -ne $stagedHash) { throw 'Installed executable does not match the staged release.' }
}

[pscustomobject]@{
    Program = $program
    Plugin = $plugin
    Version = $manifest.version
    Backup = $latest.FullName
    CodexCli = $status.codexCli
    PluginEnabled = $true
    McpEnabled = $true
}
Write-Output 'Local installation verification passed. Open a new Codex task to load the plugin.'
