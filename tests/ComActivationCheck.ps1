[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$guid = [Guid]'B759D7C6-C514-4A51-9C81-56047875A846'
$type = [Type]::GetTypeFromCLSID($guid, $true)
Write-Output "COM_TYPE=$($type.FullName)"
$instance = [Activator]::CreateInstance($type)
try {
    Write-Output "INSTANCE_TYPE=$($instance.GetType().FullName)"
    Write-Output "ASSEMBLY=$($instance.GetType().Assembly.FullName)"
}
finally {
    if ($null -ne $instance -and [Runtime.InteropServices.Marshal]::IsComObject($instance)) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($instance)
    }
}
