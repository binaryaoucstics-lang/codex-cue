[CmdletBinding()]
param(
    [string]$ArtifactRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $PSScriptRoot '..\artifacts\staging'
}
$artifact = [IO.Path]::GetFullPath($ArtifactRoot)
$executable = Join-Path $artifact 'CodexCue.exe'
$installedExecutable = Join-Path $env:LOCALAPPDATA 'Programs\CodexCue\CodexCue.exe'
if (!(Test-Path -LiteralPath $executable)) { throw "Staged executable missing: $executable" }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$tracked = New-Object System.Collections.ArrayList
$spawnedHostIds = New-Object System.Collections.ArrayList

function Assert-True([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if (![Object]::Equals($Expected, $Actual)) {
        throw "$Message Expected <$Expected>, actual <$Actual>."
    }
}

function Stop-StagedProcesses {
    Get-CimInstance Win32_Process -Filter "Name='CodexCue.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            if (!$_.ExecutablePath) { return $false }
            $path = [IO.Path]::GetFullPath($_.ExecutablePath)
            return $path -eq $executable -or $path -eq [IO.Path]::GetFullPath($installedExecutable)
        } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

function Start-AppProcess([string]$Arguments, [bool]$RedirectIo) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $executable
    $start.Arguments = $Arguments
    $start.WorkingDirectory = $artifact
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    if ($RedirectIo) {
        $start.RedirectStandardInput = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
    }
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    if (!$process.Start()) { throw "Could not start: $Arguments" }
    [void]$tracked.Add($process)
    return $process
}

function Start-McpClient {
    return Start-AppProcess '--mcp' $true
}

function Write-Mcp($Client, $Message) {
    $json = $Message | ConvertTo-Json -Depth 30 -Compress
    $Client.StandardInput.WriteLine($json)
    $Client.StandardInput.Flush()
}

function Read-Mcp($Client, [int]$TimeoutMs = 10000) {
    $read = $Client.StandardOutput.ReadLineAsync()
    if (!$read.Wait($TimeoutMs)) { throw "MCP response timed out after $TimeoutMs ms." }
    $line = $read.Result
    if ([string]::IsNullOrWhiteSpace($line)) {
        $diagnostic = if ($Client.HasExited) { $Client.StandardError.ReadToEnd() } else { '' }
        throw "MCP returned an empty response. $diagnostic"
    }
    return $line | ConvertFrom-Json
}

function Invoke-Mcp($Client, $Message, [int]$TimeoutMs = 10000) {
    Write-Mcp $Client $Message
    return Read-Mcp $Client $TimeoutMs
}

function Initialize-Mcp($Client, [int]$Id) {
    $initialize = Invoke-Mcp $Client ([ordered]@{
        jsonrpc = '2.0'; id = $Id; method = 'initialize'
        params = [ordered]@{ protocolVersion = '2024-11-05'; capabilities = @{}; clientInfo = @{ name = 'e2e'; version = '1.0' } }
    })
    Assert-Equal 'codex-cue' $initialize.result.serverInfo.name 'Unexpected MCP server.'
}

function New-Question([string]$Id, [string]$Prompt, [string]$Mode, [object[]]$Options, [bool]$AllowOther) {
    return [ordered]@{
        id = $Id; prompt = $Prompt; mode = $Mode; required = $true
        allowOther = $AllowOther; options = $Options
    }
}

function New-Ask([int]$Id, [string]$SessionId, [object[]]$Questions, [string]$ReviewMode = 'auto') {
    return [ordered]@{
        jsonrpc = '2.0'; id = $Id; method = 'tools/call'
        params = [ordered]@{
            name = 'ask_options'
            arguments = [ordered]@{
                sessionId = $SessionId; title = 'E2E choices'; questions = $Questions
                reviewMode = $ReviewMode; maxWaitMs = 30000
            }
        }
    }
}

function Find-AutomationElement([string]$AutomationId, [int]$ProcessId = 0, [int]$TimeoutMs = 5000) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $TimeoutMs) {
        $items = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants, $condition)
        foreach ($item in $items) {
            if ($ProcessId -eq 0 -or $item.Current.ProcessId -eq $ProcessId) { return $item }
        }
        Start-Sleep -Milliseconds 40
    }
    return $null
}

function Find-Descendant($Parent, [string]$AutomationId) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Wait-Prompt([string]$Prompt, [int]$TimeoutMs = 8000) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'PromptWindow')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $TimeoutMs) {
        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants, $condition)
        foreach ($window in $windows) {
            $question = Find-Descendant $window 'QuestionPrompt'
            if ($question -and $question.Current.Name -eq $Prompt) { return $window }
        }
        Start-Sleep -Milliseconds 40
    }
    throw "Prompt window did not show: $Prompt"
}

function Click-Element([string]$AutomationId, [int]$ProcessId) {
    $element = Find-AutomationElement $AutomationId $ProcessId 2500
    if (!$element) { throw "Automation element missing: $AutomationId" }
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
    } elseif ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
    } elseif ($element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
        $pattern.Toggle()
    } else { throw "Element cannot be activated: $AutomationId" }
    Start-Sleep -Milliseconds 100
}

function Set-ElementText([string]$AutomationId, [int]$ProcessId, [string]$Value) {
    $element = Find-AutomationElement $AutomationId $ProcessId 2500
    if (!$element) { throw "Automation text field missing: $AutomationId" }
    $pattern = $null
    if (!$element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        throw "Element has no value pattern: $AutomationId"
    }
    $pattern.SetValue($Value)
    Start-Sleep -Milliseconds 100
}

function Assert-Submitted($Response, [string]$SessionId) {
    if ($Response.error) { throw "MCP error: $($Response.error.message)" }
    $payload = $Response.result.structuredContent
    Assert-Equal 'submitted' $payload.status 'Prompt was not submitted.'
    Assert-Equal $SessionId $payload.sessionId 'Unexpected session ID.'
    Assert-Equal 'desktop-wpf' $payload.source 'Unexpected result source.'
    return $payload
}

function Complete-OneQuestion($Client, [int]$Id, [string]$SessionId, [string]$Prompt, [string]$OptionId) {
    $question = New-Question ($SessionId + '-q') $Prompt 'single' @(
        [ordered]@{ id = $OptionId; label = 'Continue' }
    ) $false
    Write-Mcp $Client (New-Ask $Id $SessionId @($question) 'never')
    $window = Wait-Prompt $Prompt
    $processId = $window.Current.ProcessId
    Click-Element ('Option_' + $OptionId) $processId
    Click-Element 'NextButton' $processId
    $response = Read-Mcp $Client 10000
    [void](Assert-Submitted $response $SessionId)
    return $processId
}

Stop-StagedProcesses
try {
    $explicitHost = Start-AppProcess '--host --automation' $false
    $client = Start-McpClient
    Initialize-Mcp $client 1
    $tools = Invoke-Mcp $client ([ordered]@{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} })
    Assert-True (@($tools.result.tools | Where-Object { $_.name -eq 'ask_options' }).Count -eq 1) 'ask_options is not listed.'

    $questions = @(
        (New-Question 'release' 'Choose release mode' 'single' @(
            [ordered]@{ id = 'installer'; label = 'Installer' },
            [ordered]@{ id = 'portable'; label = 'Portable' }
        ) $true),
        (New-Question 'targets' 'Choose release targets' 'multiple' @(
            [ordered]@{ id = 'windows'; label = 'Windows' },
            [ordered]@{ id = 'zip'; label = 'Portable ZIP' },
            [ordered]@{ id = 'docs'; label = 'Documentation' }
        ) $true)
    )
    Write-Mcp $client (New-Ask 3 'e2e-roundtrip' $questions)
    $firstWindow = Wait-Prompt 'Choose release mode'
    $hostPid = $firstWindow.Current.ProcessId
    Assert-Equal $explicitHost.Id $hostPid 'The explicit host did not own the first prompt.'
    Click-Element 'Option_installer' $hostPid
    Click-Element 'NextButton' $hostPid
    [void](Wait-Prompt 'Choose release targets')
    Click-Element 'Option_windows' $hostPid
    Click-Element 'Option_zip' $hostPid
    Set-ElementText 'OtherText' $hostPid 'signed hashes'
    Click-Element 'NextButton' $hostPid
    $submit = Find-AutomationElement 'SubmitButton' $hostPid 2500
    Assert-True ($null -ne $submit) 'Review screen did not appear.'
    Click-Element 'SubmitButton' $hostPid
    $roundTrip = Assert-Submitted (Read-Mcp $client 10000) 'e2e-roundtrip'
    $answers = @($roundTrip.answers)
    Assert-Equal 2 $answers.Count 'Unexpected answer count.'
    Assert-Equal 'release' $answers[0].questionId 'Question order changed.'
    Assert-Equal 'installer' @($answers[0].selectedOptionIds)[0] 'Single choice changed.'
    Assert-Equal 'targets' $answers[1].questionId 'Question order changed.'
    Assert-Equal 2 @($answers[1].selectedOptionIds).Count 'Multiple choices were not preserved.'
    Assert-Equal 'windows' @($answers[1].selectedOptionIds)[0] 'Multiple choice order changed.'
    Assert-Equal 'zip' @($answers[1].selectedOptionIds)[1] 'Multiple choice order changed.'
    Assert-Equal 'signed hashes' $answers[1].otherText 'Other text changed.'
    Write-Output 'PASS MCP and desktop wizard round trip'

    Stop-Process -Id $explicitHost.Id -Force
    $explicitHost.WaitForExit(3000) | Out-Null
    $relaunchPid = Complete-OneQuestion $client 4 'e2e-relaunch' 'Verify automatic host relaunch' 'relaunched'
    [void]$spawnedHostIds.Add($relaunchPid)
    Assert-True ($relaunchPid -ne $explicitHost.Id) 'Host was not relaunched in a new process.'
    Write-Output 'PASS MCP automatically relaunches the desktop host'

    $fifoOne = Start-McpClient
    $fifoTwo = Start-McpClient
    Initialize-Mcp $fifoOne 10
    Initialize-Mcp $fifoTwo 20
    $firstQuestion = New-Question 'fifo-one-q' 'FIFO first prompt' 'single' @(
        [ordered]@{ id = 'fifo-one'; label = 'First' }
    ) $false
    $secondQuestion = New-Question 'fifo-two-q' 'FIFO second prompt' 'single' @(
        [ordered]@{ id = 'fifo-two'; label = 'Second' }
    ) $false
    Write-Mcp $fifoOne (New-Ask 11 'fifo-one-session' @($firstQuestion) 'never')
    $fifoWindow = Wait-Prompt 'FIFO first prompt'
    $fifoHostPid = $fifoWindow.Current.ProcessId
    Write-Mcp $fifoTwo (New-Ask 21 'fifo-two-session' @($secondQuestion) 'never')
    Start-Sleep -Milliseconds 350
    $premature = Find-AutomationElement 'Option_fifo-two' $fifoHostPid 200
    Assert-True ($null -eq $premature) 'Second FIFO prompt appeared before the first completed.'
    Click-Element 'Option_fifo-one' $fifoHostPid
    Click-Element 'NextButton' $fifoHostPid
    [void](Assert-Submitted (Read-Mcp $fifoOne 10000) 'fifo-one-session')
    [void](Wait-Prompt 'FIFO second prompt')
    Click-Element 'Option_fifo-two' $fifoHostPid
    Click-Element 'NextButton' $fifoHostPid
    [void](Assert-Submitted (Read-Mcp $fifoTwo 10000) 'fifo-two-session')
    Write-Output 'PASS two MCP clients are displayed in FIFO order'

    Write-Output 'E2E release round trip passed.'
} finally {
    foreach ($spawnedHostId in @($spawnedHostIds | Select-Object -Unique)) {
        Stop-Process -Id $spawnedHostId -Force -ErrorAction SilentlyContinue
    }
    foreach ($process in $tracked) {
        try {
            if (!$process.HasExited) {
                if ($process.StartInfo.RedirectStandardInput) { $process.StandardInput.Close() }
                if (!$process.WaitForExit(800)) { $process.Kill() }
            }
            $process.Dispose()
        } catch { }
    }
    Stop-StagedProcesses
}
