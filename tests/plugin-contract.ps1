[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$plugin = Join-Path $root 'plugins\codex-cue'
$manifestPath = Join-Path $plugin '.codex-plugin\plugin.json'
$mcpPath = Join-Path $plugin '.mcp.json'
$skillPath = Join-Path $plugin 'skills\cue-prompts\SKILL.md'
$agentPath = Join-Path $plugin 'skills\cue-prompts\agents\openai.yaml'
$nextSkillPath = Join-Path $plugin 'skills\next-step-options\SKILL.md'
$nextAgentPath = Join-Path $plugin 'skills\next-step-options\agents\openai.yaml'
$hooksPath = Join-Path $plugin 'hooks\hooks.json'

foreach ($path in @($manifestPath, $mcpPath, $skillPath, $agentPath, $nextSkillPath, $nextAgentPath, $hooksPath)) {
    if (!(Test-Path -LiteralPath $path)) { throw "Missing plugin contract file: $path" }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$mcp = Get-Content -LiteralPath $mcpPath -Raw | ConvertFrom-Json
$skillText = Get-Content -LiteralPath $skillPath -Raw
$agentText = Get-Content -LiteralPath $agentPath -Raw
$nextSkillText = Get-Content -LiteralPath $nextSkillPath -Raw
$nextAgentText = Get-Content -LiteralPath $nextAgentPath -Raw
$hooks = Get-Content -LiteralPath $hooksPath -Raw | ConvertFrom-Json

if ($manifest.name -ne 'codex-cue') { throw 'Unexpected plugin name.' }
if ($manifest.version -notmatch '^2\.1\.0(\+codex\.[0-9A-Za-z.-]+)?$') { throw 'Plugin version is not a valid 2.1.0 release.' }
if ($manifest.mcpServers -ne './.mcp.json') { throw 'Plugin manifest does not reference .mcp.json.' }
if ($manifest.skills -ne './skills/') { throw 'Plugin manifest does not reference skills/.' }
if ($manifest.interface.category -ne 'Productivity') { throw 'Plugin category must be Productivity.' }
if ($manifest.interface.longDescription -notmatch 'lifecycle hooks') { throw 'Plugin metadata must describe automatic hook routing.' }
if ($manifest.license -ne 'Apache-2.0') { throw 'Plugin license must be Apache-2.0.' }
if ($manifest.repository -ne 'https://github.com/binaryaoucstics-lang/codex-cue') { throw 'Plugin repository URL is invalid.' }

$server = $mcp.mcpServers.codex_cue
if (!$server) { throw 'Missing codex_cue MCP server.' }
if ($server.command -ne './bin/CodexCue.exe') { throw 'MCP command must use the bundled executable.' }
if ($server.args -notcontains '--mcp') { throw 'MCP server does not start in --mcp mode.' }
if ($server.tool_timeout_sec -ne 900) { throw 'MCP tool timeout must be 900 seconds.' }

foreach ($eventName in @('SessionStart', 'UserPromptSubmit', 'Stop')) {
    $eventHooks = @($hooks.hooks.$eventName)
    if ($eventHooks.Count -ne 1) { throw "Hook configuration must define exactly one $eventName group." }
    $handler = @($eventHooks[0].hooks)[0]
    if ($handler.type -ne 'command') { throw "$eventName must use a command hook." }
    if ($handler.commandWindows -notmatch 'CodexCue\.exe.+--hook') {
        throw "$eventName must route through the bundled executable."
    }
}

foreach ($required in @('ask_options', 'codex_cue', 'user-facing question', 'PowerShell', 'single', 'multiple', 'allowOther', 'recommended', 'skipped', 'cancelled', 'timed_out', 'open text')) {
    if ($skillText -notmatch [regex]::Escape($required)) { throw "Skill is missing required guidance: $required" }
}
if ($skillText -match '\[TODO') { throw 'Skill still contains TODO placeholders.' }
if ($agentText -notmatch 'Use \$cue-prompts') { throw 'Agent metadata default prompt does not invoke the skill.' }
foreach ($required in @('configured option count', 'default to 3', 'ask_options', 'codex_cue', 'allowOther', 'cancelLabel', 'cancelResult', 'submitted', 'skipped', 'cancelled', 'timed_out', 'Skip', 'PowerShell')) {
    if ($nextSkillText -notmatch [regex]::Escape($required)) { throw "Next-step skill is missing required guidance: $required" }
}
if ($nextSkillText -match '\[TODO') { throw 'Next-step skill still contains TODO placeholders.' }
if ($nextAgentText -notmatch 'Use \$next-step-options') { throw 'Next-step agent metadata does not invoke the skill.' }

Write-Output 'Plugin contract passed.'
