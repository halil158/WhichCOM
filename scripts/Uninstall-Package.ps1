<#
.SYNOPSIS
    Removes WhichCOM from this computer.

.DESCRIPTION
    Removes the package, which also removes the widgets from the widget board.
    Settings in %LOCALAPPDATA%\WhichCOM are kept unless -RemoveSettings is given.

.PARAMETER RemoveSettings
    Also deletes the settings folder with your nicknames.

.PARAMETER RemoveCertificate
    Also deletes the development certificate from the store of the current user and, when run
    as administrator, from the trusted people store of this computer.

.EXAMPLE
    ./scripts/Uninstall-Package.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$RemoveSettings,
    [switch]$RemoveCertificate
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

Stop-Provider

$installed = Get-AppxPackage -Name $script:PackageName
if ($installed) {
    if ($PSCmdlet.ShouldProcess($installed.PackageFullName, 'Remove package')) {
        $installed | Remove-AppxPackage
        Write-Host 'Package removed.' -ForegroundColor Green
    }
}
else {
    Write-Host 'The package is not installed.'
}

if ($RemoveSettings) {
    $settings = Join-Path $env:LOCALAPPDATA 'WhichCOM'
    if ((Test-Path $settings) -and $PSCmdlet.ShouldProcess($settings, 'Delete settings')) {
        Remove-Item $settings -Recurse -Force
        Write-Host 'Settings deleted.'
    }
}

if ($RemoveCertificate) {
    $stores = @('Cert:\CurrentUser\My')
    if (Test-Administrator) {
        $stores += 'Cert:\LocalMachine\TrustedPeople'
    }
    else {
        Write-Host 'Not running as administrator: the trusted copy of the certificate is kept.' -ForegroundColor Yellow
        Write-Host 'To remove it, run this script with -RemoveCertificate from an elevated PowerShell.' -ForegroundColor Yellow
    }

    foreach ($store in $stores) {
        Get-ChildItem $store |
            Where-Object { $_.Subject -eq $script:Publisher -and $_.FriendlyName -eq $script:CertificateFriendlyName } |
            ForEach-Object {
                if ($PSCmdlet.ShouldProcess("$store\$($_.Thumbprint)", 'Delete certificate')) {
                    Remove-Item $_.PSPath
                    Write-Host "Certificate deleted from $store."
                }
            }
    }
}
