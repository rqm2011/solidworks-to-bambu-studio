[CmdletBinding()]
param(
    [string]$InstalledDll = (Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)) 'SolidWorksToBambu\addin\SolidWorksToBambu.dll')
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', "`"$PSCommandPath`"",
        '-InstalledDll', "`"$InstalledDll`""
    )
    $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $process.ExitCode
}

if (-not (Test-Path -LiteralPath $InstalledDll -PathType Leaf)) {
    throw "Installed add-in DLL was not found: $InstalledDll"
}

$installedFile = Get-Item -LiteralPath $InstalledDll
$assembly = [Reflection.Assembly]::LoadFrom($installedFile.FullName)
$assemblyName = $assembly.FullName
$assemblyVersion = $assembly.GetName().Version.ToString()
$runtimeVersion = $assembly.ImageRuntimeVersion
$codeBase = ([Uri]$installedFile.FullName).AbsoluteUri

$addinGuid = '{B759D7C6-C514-4A51-9C81-56047875A846}'
$className = 'SolidWorksToBambu.SwAddin'
$progId = 'SolidWorksToBambu.SwAddin'
$categoryGuid = '{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}'

function Set-RegistryValues {
    param(
        [Microsoft.Win32.RegistryKey]$Root,
        [string]$Path,
        [hashtable]$Values
    )

    $key = $Root.CreateSubKey($Path)
    if ($null -eq $key) {
        throw "Unable to create registry key: HKLM\$Path"
    }

    try {
        foreach ($entry in $Values.GetEnumerator()) {
            $kind = if ($entry.Value -is [int]) {
                [Microsoft.Win32.RegistryValueKind]::DWord
            }
            else {
                [Microsoft.Win32.RegistryValueKind]::String
            }
            $key.SetValue([string]$entry.Key, $entry.Value, $kind)
        }
    }
    finally {
        $key.Close()
    }
}

$machineRoot = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::LocalMachine,
    [Microsoft.Win32.RegistryView]::Registry64)

try {
    $classRoot = "SOFTWARE\Classes\CLSID\$addinGuid"
    Set-RegistryValues -Root $machineRoot -Path $classRoot -Values @{ '' = $className }
    Set-RegistryValues -Root $machineRoot -Path "$classRoot\Implemented Categories\$categoryGuid" -Values @{}

    $inprocPath = "$classRoot\InprocServer32"
    Set-RegistryValues -Root $machineRoot -Path $inprocPath -Values @{
        '' = 'mscoree.dll'
        'ThreadingModel' = 'Both'
        'Class' = $className
        'Assembly' = $assemblyName
        'RuntimeVersion' = $runtimeVersion
        'CodeBase' = $codeBase
    }

    $inprocKey = $machineRoot.OpenSubKey($inprocPath, $true)
    try {
        foreach ($subKeyName in $inprocKey.GetSubKeyNames()) {
            $parsedVersion = $null
            if ([Version]::TryParse($subKeyName, [ref]$parsedVersion) -and $subKeyName -ne $assemblyVersion) {
                $inprocKey.DeleteSubKeyTree($subKeyName, $false)
            }
        }
    }
    finally {
        $inprocKey.Close()
    }

    Set-RegistryValues -Root $machineRoot -Path "$inprocPath\$assemblyVersion" -Values @{
        'Class' = $className
        'Assembly' = $assemblyName
        'RuntimeVersion' = $runtimeVersion
        'CodeBase' = $codeBase
    }
    Set-RegistryValues -Root $machineRoot -Path "$classRoot\ProgId" -Values @{ '' = $progId }
    Set-RegistryValues -Root $machineRoot -Path "SOFTWARE\Classes\$progId" -Values @{ '' = $className }
    Set-RegistryValues -Root $machineRoot -Path "SOFTWARE\Classes\$progId\CLSID" -Values @{ '' = $addinGuid }
    Set-RegistryValues -Root $machineRoot -Path "SOFTWARE\SolidWorks\Addins\$addinGuid" -Values @{
        '' = [int]0
        'Title' = 'SOLIDWORKS -> Bambu Studio'
        'Description' = 'Send the active SOLIDWORKS part to Bambu Studio'
    }
}
finally {
    $machineRoot.Close()
}

$userRoot = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::CurrentUser,
    [Microsoft.Win32.RegistryView]::Registry64)

try {
    $classRoot = "SOFTWARE\Classes\CLSID\$addinGuid"
    Set-RegistryValues -Root $userRoot -Path $classRoot -Values @{ '' = $className }
    Set-RegistryValues -Root $userRoot -Path "$classRoot\Implemented Categories\$categoryGuid" -Values @{}

    $inprocPath = "$classRoot\InprocServer32"
    Set-RegistryValues -Root $userRoot -Path $inprocPath -Values @{
        '' = 'mscoree.dll'
        'ThreadingModel' = 'Both'
        'Class' = $className
        'Assembly' = $assemblyName
        'RuntimeVersion' = $runtimeVersion
        'CodeBase' = $codeBase
    }
    $inprocKey = $userRoot.OpenSubKey($inprocPath, $true)
    try {
        foreach ($subKeyName in $inprocKey.GetSubKeyNames()) {
            $parsedVersion = $null
            if ([Version]::TryParse($subKeyName, [ref]$parsedVersion) -and $subKeyName -ne $assemblyVersion) {
                $inprocKey.DeleteSubKeyTree($subKeyName, $false)
            }
        }
    }
    finally {
        $inprocKey.Close()
    }
    Set-RegistryValues -Root $userRoot -Path "$inprocPath\$assemblyVersion" -Values @{
        'Class' = $className
        'Assembly' = $assemblyName
        'RuntimeVersion' = $runtimeVersion
        'CodeBase' = $codeBase
    }
    Set-RegistryValues -Root $userRoot -Path "$classRoot\ProgId" -Values @{ '' = $progId }
    Set-RegistryValues -Root $userRoot -Path "SOFTWARE\Classes\$progId" -Values @{ '' = $className }
    Set-RegistryValues -Root $userRoot -Path "SOFTWARE\Classes\$progId\CLSID" -Values @{ '' = $addinGuid }
    Set-RegistryValues -Root $userRoot -Path "SOFTWARE\SolidWorks\AddInsStartup\$addinGuid" -Values @{ '' = [int]1 }
}
finally {
    $userRoot.Close()
}

Write-Host "System and current-user COM registration repaired: $codeBase" -ForegroundColor Green
