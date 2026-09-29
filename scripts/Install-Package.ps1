<#
.SYNOPSIS
    Installs the signed WhichCOM package for the current user.

.DESCRIPTION
    Installs artifacts\WhichCOM_<platform>.msix, which Build-Package.ps1 creates. The certificate
    the package was signed with must be trusted on this computer; see New-DevCert.ps1 -Trust.

.PARAMETER Path
    Path of the .msix file. Default: the package in the artifacts folder.

.EXAMPLE
    ./scripts/Install-Package.ps1
#>
[CmdletBinding()]
param(
    [string]$Path
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if (-not $Path) {
    $platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }
    $Path = Join-Path (Get-ArtifactsDirectory) "WhichCOM_$platform.msix"
}
if (-not (Test-Path $Path)) {
    throw "$Path was not found. Run ./scripts/Build-Package.ps1 first."
}

$signature = Get-AuthenticodeSignature $Path
if ($signature.Status -eq 'NotSigned') {
    throw 'The package is not signed. Run ./scripts/Build-Package.ps1 without -NoSign.'
}
if ($signature.Status -ne 'Valid') {
    $trustScript = Join-Path $PSScriptRoot 'New-DevCert.ps1'
    throw "Windows does not trust the signature of the package ($($signature.Status)). Start PowerShell as administrator and run: pwsh -File `"$trustScript`" -Trust"
}

# A registration made by Register-DevPackage.ps1 cannot be updated by a signed package.
$installed = Get-AppxPackage -Name $script:PackageName
if ($installed -and $installed.IsDevelopmentMode) {
    Write-Host 'Removing the development registration...'
    $installed | Remove-AppxPackage
}

Stop-Provider
Add-AppxPackage -Path $Path -ForceApplicationShutdown -ForceUpdateFromAnyVersion

Show-InstalledPackage
Write-Host ''
Write-Host 'Open the widget board (Win + W), choose "Add widgets" and look for WhichCOM.'
