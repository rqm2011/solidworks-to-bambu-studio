[CmdletBinding()]
param(
    [string]$SolidWorksDirectory,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

function Find-SolidWorksDirectory {
    param([string]$RequestedDirectory)

    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($RequestedDirectory)) {
        $candidates.Add($RequestedDirectory)
    }

    $candidates.Add('C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS')

    $registryPaths = @(
        'HKLM:\SOFTWARE\SolidWorks\SOLIDWORKS 2025\Setup',
        'HKLM:\SOFTWARE\WOW6432Node\SolidWorks\SOLIDWORKS 2025\Setup'
    )
    foreach ($registryPath in $registryPaths) {
        $setup = Get-ItemProperty -LiteralPath $registryPath -ErrorAction SilentlyContinue
        if ($null -eq $setup) {
            continue
        }

        foreach ($propertyName in @('SolidWorks Folder', 'InstallDir', 'Install Location')) {
            $value = $setup.$propertyName
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                $candidates.Add([string]$value)
            }
        }
    }

    foreach ($candidate in $candidates) {
        $redist = Join-Path $candidate 'api\redist'
        $interop = Join-Path $redist 'SolidWorks.Interop.sldworks.dll'
        if (Test-Path -LiteralPath $interop -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'SOLIDWORKS 2025 API was not found. Use -SolidWorksDirectory to specify the SOLIDWORKS installation directory.'
}

function Find-MSBuild {
    $vswhereCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe')
    )

    foreach ($vswhere in $vswhereCandidates) {
        if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
            continue
        }

        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($found) -and (Test-Path -LiteralPath $found -PathType Leaf)) {
            return $found
        }
    }

    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    return $null
}

$swDirectory = Find-SolidWorksDirectory -RequestedDirectory $SolidWorksDirectory
$apiRedist = Join-Path $swDirectory 'api\redist'
$msbuild = Find-MSBuild
$solution = Join-Path $projectRoot 'SolidWorksToBambu.sln'
$frameworkReferencePath = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\mscorlib.dll'
Write-Host "SOLIDWORKS: $swDirectory"

$output = Join-Path $projectRoot "src\SolidWorksToBambu\bin\x64\$Configuration\SolidWorksToBambu.dll"
$outputDirectory = Split-Path -Parent $output

if (-not [string]::IsNullOrWhiteSpace($msbuild) -and (Test-Path -LiteralPath $frameworkReferencePath -PathType Leaf)) {
    Write-Host "MSBuild:    $msbuild"
    $buildArguments = @(
        $solution,
        '/m',
        '/t:Build',
        "/p:Configuration=$Configuration",
        '/p:Platform=x64',
        "/p:SolidWorksInstallDir=$swDirectory",
        "/p:SolidWorksApiRedistDir=$apiRedist",
        '/verbosity:minimal'
    )

    & $msbuild @buildArguments
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE."
    }
}
else {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
        throw 'Neither MSBuild nor the system C# compiler was found. Install the .NET desktop build tools for Visual Studio 2022.'
    }

    Write-Host "C# compiler: $compiler (compatibility mode)"
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $pdb = [IO.Path]::ChangeExtension($output, '.pdb')
    $sourceRoot = Join-Path $projectRoot 'src\SolidWorksToBambu'
    $sources = @(
        (Join-Path $sourceRoot 'Core.csx'),
        (Join-Path $sourceRoot 'IconFactory.csx'),
        (Join-Path $sourceRoot 'SettingsForm.csx'),
        (Join-Path $sourceRoot 'SwAddin.csx'),
        (Join-Path $sourceRoot 'Properties\AssemblyInfo.csx')
    )
    $compilerArguments = @(
        '/nologo',
        '/target:library',
        '/platform:x64',
        "/out:$output",
        "/pdb:$pdb",
        '/reference:System.dll',
        '/reference:System.Core.dll',
        '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll',
        "/reference:$(Join-Path $swDirectory 'SolidWorksTools.dll')",
        "/link:$(Join-Path $apiRedist 'SolidWorks.Interop.sldworks.dll')",
        "/link:$(Join-Path $apiRedist 'SolidWorks.Interop.swconst.dll')",
        "/link:$(Join-Path $apiRedist 'SolidWorks.Interop.swpublished.dll')"
    )
    if ($Configuration -eq 'Debug') {
        $compilerArguments += @('/debug:full', '/optimize-', '/define:DEBUG;TRACE')
    }
    else {
        $compilerArguments += @('/debug:pdbonly', '/optimize+', '/define:TRACE')
    }

    & $compiler @compilerArguments $sources
    if ($LASTEXITCODE -ne 0) {
        throw "Compatibility build failed with C# compiler exit code $LASTEXITCODE."
    }

    foreach ($dependencyName in @(
        'SolidWorksTools.dll',
        'SolidWorks.Interop.sldworks.dll',
        'SolidWorks.Interop.swconst.dll',
        'SolidWorks.Interop.swpublished.dll'
    )) {
        $dependencySource = if ($dependencyName -eq 'SolidWorksTools.dll') {
            Join-Path $swDirectory $dependencyName
        }
        else {
            Join-Path $apiRedist $dependencyName
        }
        Copy-Item -LiteralPath $dependencySource -Destination (Join-Path $outputDirectory $dependencyName) -Force
    }
}

if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
    throw "Build completed without the expected output file: $output"
}

Write-Host "Build succeeded: $output" -ForegroundColor Green
