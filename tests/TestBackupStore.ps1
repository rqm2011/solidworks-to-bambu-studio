[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "C# compiler was not found: $compiler"
}

$outputDirectory = Join-Path $projectRoot 'obj\backup-store-tests'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputAssembly = Join-Path $outputDirectory 'BackupStoreTests.exe'
$sources = @(
    (Join-Path $projectRoot 'src\SolidWorksToBambu\Core.csx'),
    (Join-Path $PSScriptRoot 'InteropStubs.csx'),
    (Join-Path $PSScriptRoot 'BackupStoreTests.csx')
)

& $compiler /nologo /target:exe "/out:$outputAssembly" /reference:System.dll /reference:System.Core.dll $sources
if ($LASTEXITCODE -ne 0) {
    throw "Backup-store test compilation failed with exit code $LASTEXITCODE."
}

& $outputAssembly
if ($LASTEXITCODE -ne 0) {
    throw "Backup-store tests failed with exit code $LASTEXITCODE."
}
