[CmdletBinding()]
param(
    [switch]$Packaging
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tools = [IO.Path]::GetFullPath((Join-Path $root '.tools'))

function Assert-UnderTools([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (!$resolved.StartsWith($tools + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe tools path: $resolved"
    }
}

function Ensure-Net48References {
    $toolRoot = Join-Path $tools 'net48\1.0.3'
    $referenceAssembly = Join-Path $toolRoot 'build\.NETFramework\v4.8\mscorlib.dll'
    if (Test-Path -LiteralPath $referenceAssembly) {
        Write-Host "net48 reference assemblies ready: $toolRoot"
        return
    }

    $downloadRoot = Join-Path $tools 'downloads'
    New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
    $nupkg = Join-Path $downloadRoot 'Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3.nupkg'
    $zip = Join-Path $downloadRoot 'Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3.zip'
    $staging = Join-Path $tools 'net48\1.0.3.staging'
    Assert-UnderTools $staging
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $uri = 'https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net48/1.0.3'
    Invoke-WebRequest -UseBasicParsing -Uri $uri -OutFile $nupkg
    Copy-Item -LiteralPath $nupkg -Destination $zip -Force
    Expand-Archive -LiteralPath $zip -DestinationPath $staging -Force

    $stagedReference = Join-Path $staging 'build\.NETFramework\v4.8\mscorlib.dll'
    if (!(Test-Path -LiteralPath $stagedReference)) { throw "Reference assembly missing after extraction: $stagedReference" }
    Assert-UnderTools $toolRoot
    if (Test-Path -LiteralPath $toolRoot) { Remove-Item -LiteralPath $toolRoot -Recurse -Force }
    Move-Item -LiteralPath $staging -Destination $toolRoot
    Remove-Item -LiteralPath $nupkg,$zip -Force
    Write-Host "Installed net48 reference assemblies: $toolRoot"
}

function Find-InnoCompiler {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return [IO.Path]::GetFullPath($candidate) }
    }
    return $null
}

function Test-InnoVersion([string]$Compiler) {
    if (!$Compiler -or !(Test-Path -LiteralPath $Compiler)) { return $false }
    $version = (Get-Item -LiteralPath $Compiler).VersionInfo.ProductVersion
    if ($version -match '^6\.7\.3(\.|$)') { return $true }
    $uninstallRoots = @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )
    $compilerDirectory = [IO.Path]::GetFullPath((Split-Path $Compiler -Parent)).TrimEnd('\')
    foreach ($root in $uninstallRoots) {
        if (!(Test-Path $root)) { continue }
        foreach ($key in Get-ChildItem $root) {
            $entry = Get-ItemProperty $key.PSPath
            if ($entry.DisplayName -like 'Inno Setup*' -and $entry.DisplayVersion -eq '6.7.3' -and $entry.InstallLocation) {
                $installDirectory = [IO.Path]::GetFullPath($entry.InstallLocation).TrimEnd('\')
                if ($installDirectory -eq $compilerDirectory) { return $true }
            }
        }
    }
    return $false
}

function Ensure-InnoSetup {
    $compiler = Find-InnoCompiler
    if (!(Test-InnoVersion $compiler)) {
        $winget = Get-Command winget -ErrorAction SilentlyContinue
        if ($winget) {
            & $winget.Source install --id JRSoftware.InnoSetup -e -s winget --scope user --version 6.7.3 --silent --accept-package-agreements --accept-source-agreements
            if ($LASTEXITCODE -ne 0) { Write-Warning "winget could not install Inno Setup 6.7.3; using signed fallback." }
            $compiler = Find-InnoCompiler
        }
    }

    if (!(Test-InnoVersion $compiler)) {
        $downloadRoot = Join-Path $tools 'downloads'
        New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
        $installer = Join-Path $downloadRoot 'innosetup-6.7.3.exe'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $installer
        $signature = Get-AuthenticodeSignature -LiteralPath $installer
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B\.V\.') {
            throw 'Inno Setup fallback signature validation failed.'
        }
        $process = Start-Process -FilePath $installer -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER' -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0) { throw "Inno Setup installer failed with exit code $($process.ExitCode)." }
        $compiler = Find-InnoCompiler
    }

    if (!(Test-InnoVersion $compiler)) { throw 'Inno Setup 6.7.3 compiler was not found after installation.' }
    $pathFile = Join-Path $tools 'inno-path.txt'
    Set-Content -LiteralPath $pathFile -Value $compiler -Encoding UTF8
    Write-Host "Inno Setup 6.7.3 ready: $compiler"
}

Ensure-Net48References
if ($Packaging) { Ensure-InnoSetup }
