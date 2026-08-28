[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Guid]$Clsid
)

$ErrorActionPreference = 'Stop'
$swAddinIid = [Guid]'DA306A0D-EAC5-4406-8610-B1DA805D9270'
$instance = $null
$unknown = [IntPtr]::Zero
$interfacePointer = [IntPtr]::Zero
try {
    $type = [Type]::GetTypeFromCLSID($Clsid, $true)
    $instance = [Activator]::CreateInstance($type)
    $unknown = [Runtime.InteropServices.Marshal]::GetIUnknownForObject($instance)
    $hresult = [Runtime.InteropServices.Marshal]::QueryInterface($unknown, [ref]$swAddinIid, [ref]$interfacePointer)
    Write-Output "CLSID={$($Clsid.ToString().ToUpperInvariant())}"
    Write-Output ('QUERY_INTERFACE_HRESULT=0x{0:X8}' -f ([uint32]$hresult))
    Write-Output "INTERFACE_FOUND=$($hresult -eq 0 -and $interfacePointer -ne [IntPtr]::Zero)"
}
finally {
    if ($interfacePointer -ne [IntPtr]::Zero) {
        [void][Runtime.InteropServices.Marshal]::Release($interfacePointer)
    }
    if ($unknown -ne [IntPtr]::Zero) {
        [void][Runtime.InteropServices.Marshal]::Release($unknown)
    }
    if ($null -ne $instance -and [Runtime.InteropServices.Marshal]::IsComObject($instance)) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($instance)
    }
}
