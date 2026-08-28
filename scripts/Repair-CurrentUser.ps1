[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot 'src\SolidWorksToBambu\bin\x64\Release'
$sourceDll = Join-Path $sourceDirectory 'SolidWorksToBambu.dll'
$documentsDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)
$installDirectory = Join-Path $documentsDirectory 'SolidWorksToBambu\addin'
$installedDll = Join-Path $installDirectory 'SolidWorksToBambu.dll'
$addinGuid = '{B759D7C6-C514-4A51-9C81-56047875A846}'
$className = 'SolidWorksToBambu.SwAddin'
$progId = 'SolidWorksToBambu.SwAddin'

if (Get-Process -Name 'SLDWORKS' -ErrorAction SilentlyContinue) {
    throw 'Close SOLIDWORKS before repairing the add-in.'
}

if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) {
    throw "The release build was not found: $sourceDll"
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination $installedDll -Force
foreach ($dependencyName in @(
    'SolidWorksTools.dll'
)) {
    Copy-Item -LiteralPath (Join-Path $sourceDirectory $dependencyName) -Destination (Join-Path $installDirectory $dependencyName) -Force
}

$sourcePdb = Join-Path $sourceDirectory 'SolidWorksToBambu.pdb'
if (Test-Path -LiteralPath $sourcePdb -PathType Leaf) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $installDirectory 'SolidWorksToBambu.pdb') -Force
}

$assembly = [Reflection.Assembly]::LoadFrom($installedDll)
$assemblyName = $assembly.FullName
$assemblyVersion = $assembly.GetName().Version.ToString()
$runtimeVersion = $assembly.ImageRuntimeVersion
$codeBase = ([Uri]$installedDll).AbsoluteUri

function Set-RegistryValues {
    param(
        [Microsoft.Win32.RegistryKey]$Root,
        [string]$Path,
        [hashtable]$Values
    )

    $key = $Root.CreateSubKey($Path)
    try {
        foreach ($entry in $Values.GetEnumerator()) {
            $valueName = [string]$entry.Key
            $value = $entry.Value
            $kind = if ($value -is [int]) {
                [Microsoft.Win32.RegistryValueKind]::DWord
            }
            else {
                [Microsoft.Win32.RegistryValueKind]::String
            }
            $key.SetValue($valueName, $value, $kind)
        }
    }
    finally {
        $key.Close()
    }
}

$userRoot = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::CurrentUser,
    [Microsoft.Win32.RegistryView]::Registry64)
try {
    $classRoot = "Software\Classes\CLSID\$addinGuid"
    Set-RegistryValues -Root $userRoot -Path $classRoot -Values @{ '' = $className }
    Set-RegistryValues -Root $userRoot -Path "$classRoot\Implemented Categories\{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}" -Values @{}

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
    Set-RegistryValues -Root $userRoot -Path "Software\Classes\$progId" -Values @{ '' = $className }
    Set-RegistryValues -Root $userRoot -Path "Software\Classes\$progId\CLSID" -Values @{ '' = $addinGuid }
    Set-RegistryValues -Root $userRoot -Path "Software\SolidWorks\AddInsStartup\$addinGuid" -Values @{ '' = [int]1 }
}
finally {
    $userRoot.Close()
}

Write-Host "Current-user repair completed: $assemblyName" -ForegroundColor Green
Write-Host "Installed at: $installedDll"
