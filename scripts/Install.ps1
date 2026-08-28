[CmdletBinding()]
param(
    [string]$SolidWorksDirectory
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if (-not [string]::IsNullOrWhiteSpace($SolidWorksDirectory)) {
        $arguments += @('-SolidWorksDirectory', "`"$SolidWorksDirectory`"")
    }

    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -Wait
    exit
}

$solidWorksProcess = Get-Process -Name 'SLDWORKS' -ErrorAction SilentlyContinue
if ($null -ne $solidWorksProcess) {
    throw 'Close SOLIDWORKS before installing. The add-in DLL cannot be updated safely while it is loaded.'
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot 'Build.ps1'
& $buildScript -SolidWorksDirectory $SolidWorksDirectory -Configuration Release

$sourceDirectory = Join-Path $projectRoot 'src\SolidWorksToBambu\bin\x64\Release'
$sourceDll = Join-Path $sourceDirectory 'SolidWorksToBambu.dll'
$documentsDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)
$installDirectory = Join-Path $documentsDirectory 'SolidWorksToBambu\addin'
$installedDll = Join-Path $installDirectory 'SolidWorksToBambu.dll'
$regasm = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe'

if (-not (Test-Path -LiteralPath $regasm -PathType Leaf)) {
    throw "64-bit RegAsm was not found: $regasm"
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
if (Test-Path -LiteralPath $installedDll -PathType Leaf) {
    # Unregister the currently installed assembly before replacing it. RegAsm
    # registrations are versioned, so unregistering only after a version bump
    # can leave the previous version's registry entry behind.
    & $regasm $installedDll /unregister /silent 2>$null
}

Copy-Item -LiteralPath $sourceDll -Destination $installedDll -Force

$interopDependencies = @('SolidWorksTools.dll')
foreach ($dependencyName in $interopDependencies) {
    $dependencySource = Join-Path $sourceDirectory $dependencyName
    if (-not (Test-Path -LiteralPath $dependencySource -PathType Leaf)) {
        throw "A SOLIDWORKS interop dependency is missing from the build output: $dependencySource"
    }

    Copy-Item -LiteralPath $dependencySource -Destination (Join-Path $installDirectory $dependencyName) -Force
}

$sourcePdb = Join-Path $sourceDirectory 'SolidWorksToBambu.pdb'
if (Test-Path -LiteralPath $sourcePdb -PathType Leaf) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $installDirectory 'SolidWorksToBambu.pdb') -Force
}

& $regasm $installedDll /codebase
if ($LASTEXITCODE -ne 0) {
    throw "COM registration failed with RegAsm exit code $LASTEXITCODE."
}

Write-Host ''
Write-Host 'Installation completed. Start SOLIDWORKS 2025 and verify the add-in under Tools > Add-Ins.' -ForegroundColor Green
Write-Host "Installed at: $installedDll"
