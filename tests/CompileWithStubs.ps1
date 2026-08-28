[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "找不到 C# 编译器：$compiler"
}

$outputDirectory = Join-Path $projectRoot 'obj\stub-check'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputAssembly = Join-Path $outputDirectory 'SolidWorksToBambu.StubCheck.dll'
$sourceRoot = Join-Path $projectRoot 'src\SolidWorksToBambu'
$sources = @(
    (Join-Path $sourceRoot 'Core.csx'),
    (Join-Path $sourceRoot 'IconFactory.csx'),
    (Join-Path $sourceRoot 'SettingsForm.csx'),
    (Join-Path $sourceRoot 'SwAddin.csx'),
    (Join-Path $sourceRoot 'Properties\AssemblyInfo.csx'),
    (Join-Path $PSScriptRoot 'InteropStubs.csx')
)

$arguments = @(
    '/nologo',
    '/target:library',
    "/out:$outputAssembly",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
) + $sources

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "离线编译检查失败，csc 退出代码：$LASTEXITCODE"
}

Write-Host "离线编译检查通过：$outputAssembly" -ForegroundColor Green
