[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$plugin = Join-Path $root 'plugins\codex-option-prompts'
$manifestPath = Join-Path $plugin '.codex-plugin\plugin.json'
$mcpPath = Join-Path $plugin '.mcp.json'
$skillPath = Join-Path $plugin 'skills\option-prompts\SKILL.md'
$agentPath = Join-Path $plugin 'skills\option-prompts\agents\openai.yaml'
$hooksPath = Join-Path $plugin 'hooks\hooks.json'

foreach ($path in @($manifestPath, $mcpPath, $skillPath, $agentPath, $hooksPath)) {
    if (!(Test-Path -LiteralPath $path)) { throw "Missing plugin contract file: $path" }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$mcp = Get-Content -LiteralPath $mcpPath -Raw | ConvertFrom-Json
$skillText = Get-Content -LiteralPath $skillPath -Raw
$agentText = Get-Content -LiteralPath $agentPath -Raw
$hooks = Get-Content -LiteralPath $hooksPath -Raw | ConvertFrom-Json

if ($manifest.name -ne 'codex-option-prompts') { throw 'Unexpected plugin name.' }
if ($manifest.version -notmatch '^1\.2\.1(\+codex\.[0-9A-Za-z.-]+)?$') { throw 'Plugin version is not a valid 1.2.1 release.' }
if ($manifest.mcpServers -ne './.mcp.json') { throw 'Plugin manifest does not reference .mcp.json.' }
if ($manifest.skills -ne './skills/') { throw 'Plugin manifest does not reference skills/.' }
if ($manifest.interface.category -ne 'Productivity') { throw 'Plugin category must be Productivity.' }
if ($manifest.interface.longDescription -notmatch 'lifecycle hooks') { throw 'Plugin metadata must describe automatic hook routing.' }
if ($manifest.license -ne 'Apache-2.0') { throw 'Plugin license must be Apache-2.0.' }
if ($manifest.repository -ne 'https://github.com/binaryaoucstics-lang/codex-option-prompts') { throw 'Plugin repository URL is invalid.' }

$server = $mcp.mcpServers.codex_option_prompts
if (!$server) { throw 'Missing codex_option_prompts MCP server.' }
if ($server.command -ne './bin/CodexOptionPrompts.exe') { throw 'MCP command must use the bundled executable.' }
if ($server.args -notcontains '--mcp') { throw 'MCP server does not start in --mcp mode.' }
if ($server.tool_timeout_sec -ne 900) { throw 'MCP tool timeout must be 900 seconds.' }

foreach ($eventName in @('SessionStart', 'UserPromptSubmit')) {
    $eventHooks = @($hooks.hooks.$eventName)
    if ($eventHooks.Count -ne 1) { throw "Hook configuration must define exactly one $eventName group." }
    $handler = @($eventHooks[0].hooks)[0]
    if ($handler.type -ne 'command') { throw "$eventName must use a command hook." }
    if ($handler.commandWindows -notmatch 'CodexOptionPrompts\.exe.+--hook') {
        throw "$eventName must route through the bundled executable."
    }
}

foreach ($required in @('ask_options', 'codex_option_prompts', 'every user-facing question', 'PowerShell', 'single', 'multiple', 'allowOther', 'recommended', 'cancelled', 'timed_out', 'open-ended')) {
    if ($skillText -notmatch [regex]::Escape($required)) { throw "Skill is missing required guidance: $required" }
}
if ($skillText -match '\[TODO') { throw 'Skill still contains TODO placeholders.' }
if ($agentText -notmatch 'Use \$option-prompts') { throw 'Agent metadata default prompt does not invoke the skill.' }

Write-Output 'Plugin contract passed.'
