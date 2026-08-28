[CmdletBinding()]
param(
    [string]$AddinPath = (Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)) 'SolidWorksToBambu\addin\SolidWorksToBambu.dll'),
    [string]$ProgId = 'SolidWorksToBambu.SwAddin'
)

$ErrorActionPreference = 'Stop'
$application = $null
$ownsProcess = $false
$ownedProcessId = $null
try {
    $interopPath = 'D:\solidworks_crop\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll'
    Add-Type -Path $interopPath

    $beforeIds = @(Get-Process -Name 'SLDWORKS' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    Write-Output 'STEP=CREATE_SOLIDWORKS'
    $application = New-Object SolidWorks.Interop.sldworks.SldWorksClass
    Start-Sleep -Seconds 3
    $afterIds = @(Get-Process -Name 'SLDWORKS' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    $newIds = @($afterIds | Where-Object { $beforeIds -notcontains $_ })
    if ($newIds.Count -ne 1) {
        throw 'The test did not create exactly one new SOLIDWORKS process. It will not modify or close an existing instance.'
    }
    $ownsProcess = $true
    $ownedProcessId = [int]$newIds[0]
    Write-Output "OWNED_PROCESS_ID=$ownedProcessId"

    Write-Output 'STEP=WAIT_FOR_STARTUP'
    Start-Sleep -Seconds 5
    $deadline = [DateTime]::Now.AddSeconds(60)
    while (-not $application.StartupProcessCompleted -and [DateTime]::Now -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }
    Write-Output "STARTUP_COMPLETED=$($application.StartupProcessCompleted)"

    Write-Output 'STEP=SET_HIDDEN'
    $application.Visible = $false

    Write-Output 'STEP=READ_REVISION'
    $revision = $application.RevisionNumber()
    Write-Output "SOLIDWORKS_REVISION=$revision"

    $installedDll = [IO.Path]::GetFullPath($AddinPath)
    Write-Output "ADDIN_PATH=$installedDll"
    Write-Output "ADDIN_FILE_EXISTS=$(Test-Path -LiteralPath $installedDll -PathType Leaf)"
    Write-Output 'STEP=LOAD_ADDIN'
    $loaded = $application.LoadAddIn($installedDll)
    Write-Output "LOAD_ADDIN_RESULT=$loaded"
    Start-Sleep -Seconds 3

    Write-Output 'STEP=GET_ADDIN_OBJECT'
    $addin = $application.GetAddInObject($ProgId)
    Write-Output "ADDIN_OBJECT_FOUND=$($null -ne $addin)"

    $log = Join-Path $env:LOCALAPPDATA 'SolidWorksToBambu\SolidWorksToBambu.log'
    Write-Output "LOG_EXISTS=$(Test-Path -LiteralPath $log -PathType Leaf)"
    if (Test-Path -LiteralPath $log -PathType Leaf) {
        Get-Content -LiteralPath $log -Tail 30
    }
}
finally {
    if ($ownsProcess -and $null -ne $application) {
        try {
            $application.ExitApp()
        }
        catch {
            Write-Output "EXIT_APP_ERROR=$($_.Exception.GetBaseException().Message)"
        }
        if ([Runtime.InteropServices.Marshal]::IsComObject($application)) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($application)
        }
    }

    if ($ownsProcess -and $null -ne $ownedProcessId) {
        Start-Sleep -Seconds 2
        $remaining = Get-Process -Id $ownedProcessId -ErrorAction SilentlyContinue
        if ($null -ne $remaining) {
            Write-Output "FORCE_CLOSING_OWNED_PROCESS=$ownedProcessId"
            Stop-Process -Id $ownedProcessId -Force -ErrorAction Stop
        }
    }
}
