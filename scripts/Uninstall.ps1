[CmdletBinding()]
param(
    [switch]$RemoveUserData
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($RemoveUserData) {
        $arguments += '-RemoveUserData'
    }

    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -Wait
    exit
}

$solidWorksProcess = Get-Process -Name 'SLDWORKS' -ErrorAction SilentlyContinue
if ($null -ne $solidWorksProcess) {
    throw 'Close SOLIDWORKS before uninstalling.'
}

$addinGuid = '{B759D7C6-C514-4A51-9C81-56047875A846}'
$documentsDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)
$installDirectories = @(
    (Join-Path $documentsDirectory 'SolidWorksToBambu\addin'),
    (Join-Path $env:LOCALAPPDATA 'SolidWorksToBambu\addin')
)
$regasm = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe'

if (Test-Path -LiteralPath $regasm -PathType Leaf) {
    foreach ($directory in $installDirectories) {
        $installedDll = Join-Path $directory 'SolidWorksToBambu.dll'
        if (Test-Path -LiteralPath $installedDll -PathType Leaf) {
            & $regasm $installedDll /unregister /silent 2>$null
            break
        }
    }
}

Remove-Item -LiteralPath "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\SolidWorks\Addins\$addinGuid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "Registry::HKEY_CURRENT_USER\Software\SolidWorks\AddInsStartup\$addinGuid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "Registry::HKEY_CURRENT_USER\Software\Classes\CLSID\$addinGuid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'Registry::HKEY_CURRENT_USER\Software\Classes\SolidWorksToBambu.SwAddin' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'Registry::HKEY_CURRENT_USER\Software\Classes\SolidWorksToBambu.Addin' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\$addinGuid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SolidWorksToBambu.SwAddin' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SolidWorksToBambu.Addin' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SolidWorksToBambu' -Recurse -Force -ErrorAction SilentlyContinue

$expectedInstallDirectories = @(
    [IO.Path]::GetFullPath((Join-Path $documentsDirectory 'SolidWorksToBambu\addin')).TrimEnd('\'),
    [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'SolidWorksToBambu\addin')).TrimEnd('\')
)
foreach ($installDirectory in $installDirectories) {
    $resolvedInstallDirectory = [IO.Path]::GetFullPath($installDirectory).TrimEnd('\')
    if ($expectedInstallDirectories -notcontains $resolvedInstallDirectory) {
        throw "Refusing to delete an unverified installation directory: $resolvedInstallDirectory"
    }

    if (Test-Path -LiteralPath $resolvedInstallDirectory -PathType Container) {
        Remove-Item -LiteralPath $resolvedInstallDirectory -Recurse -Force
    }
}

if ($RemoveUserData) {
    $userDataDirectory = Join-Path $env:LOCALAPPDATA 'SolidWorksToBambu'
    $expectedUserDataDirectory = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'SolidWorksToBambu')).TrimEnd('\')
    $resolvedUserDataDirectory = [IO.Path]::GetFullPath($userDataDirectory).TrimEnd('\')
    if ($resolvedUserDataDirectory -ne $expectedUserDataDirectory) {
        throw "Refusing to delete an unverified user data directory: $resolvedUserDataDirectory"
    }

    if (Test-Path -LiteralPath $resolvedUserDataDirectory -PathType Container) {
        Remove-Item -LiteralPath $resolvedUserDataDirectory -Recurse -Force
    }

    Remove-Item -LiteralPath 'Registry::HKEY_CURRENT_USER\Software\SolidWorksToBambu' -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'SolidWorks to Bambu Studio was uninstalled.' -ForegroundColor Green
if (-not $RemoveUserData) {
    Write-Host 'Settings, logs, and export cache were retained. Use -RemoveUserData to delete them too.'
}
