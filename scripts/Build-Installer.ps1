[CmdletBinding()]
param(
    [string]$SolidWorksDirectory,
    [string]$Version = '0.3.0'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot 'Build.ps1'
& $buildScript -SolidWorksDirectory $SolidWorksDirectory -Configuration Release

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "64-bit C# compiler was not found: $compiler"
}

$releaseDirectory = Join-Path $projectRoot 'src\SolidWorksToBambu\bin\x64\Release'
$installerSource = Join-Path $projectRoot 'installer\Installer.csx'
$artifactsDirectory = Join-Path $projectRoot 'artifacts'
$installerPath = Join-Path $artifactsDirectory "SolidWorksToBambu-Setup-v$Version.exe"
$hashPath = "$installerPath.sha256"

$payload = [ordered]@{
    'SolidWorksToBambu.dll' = (Join-Path $releaseDirectory 'SolidWorksToBambu.dll')
    'SolidWorksToBambu.pdb' = (Join-Path $releaseDirectory 'SolidWorksToBambu.pdb')
    'SolidWorksTools.dll' = (Join-Path $releaseDirectory 'SolidWorksTools.dll')
    'Repair-SystemRegistration.ps1' = (Join-Path $PSScriptRoot 'Repair-SystemRegistration.ps1')
    'Uninstall.ps1' = (Join-Path $PSScriptRoot 'Uninstall.ps1')
}

foreach ($entry in $payload.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
        throw "Installer payload was not found: $($entry.Value)"
    }
}

$pluginVersion = (Get-Item -LiteralPath $payload['SolidWorksToBambu.dll']).VersionInfo.FileVersion
if ($pluginVersion -ne "$Version.0") {
    throw "Package version $Version does not match plug-in file version $pluginVersion."
}

New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null
$compilerArguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    "/out:$installerPath",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Windows.Forms.dll'
)

foreach ($entry in $payload.GetEnumerator()) {
    $resourceName = "SolidWorksToBambu.Installer.Resources.$($entry.Key)"
    $compilerArguments += "/resource:$($entry.Value),$resourceName"
}

& $compiler @compilerArguments $installerSource
if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with C# compiler exit code $LASTEXITCODE."
}

$verification = Start-Process -FilePath $installerPath -ArgumentList '--verify' -WindowStyle Hidden -Wait -PassThru
if ($verification.ExitCode -ne 0) {
    throw "Installer payload verification failed with exit code $($verification.ExitCode)."
}

$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $hashPath -Value "$hash  $(Split-Path -Leaf $installerPath)" -Encoding ASCII

Write-Host "Installer built: $installerPath" -ForegroundColor Green
Write-Host "SHA-256:        $hash"
