<#
.SYNOPSIS
    Creates a self-signed certificate for signing the WhichCOM package on this computer.

.DESCRIPTION
    Every developer creates an own certificate. It is never committed.

    The certificate and its private key are stored in the certificate store of the current user
    (Cert:\CurrentUser\My). The private key cannot be exported, no file holds it and no password
    is needed. Build-Package.ps1 signs with the certificate straight from the store.

    Windows installs a package only when it trusts the certificate. Run this script once more
    with -Trust from an elevated PowerShell to add the public certificate to the trusted people
    store of this computer. Only do that on computers you develop on.

.PARAMETER Trust
    Adds the public certificate to Cert:\LocalMachine\TrustedPeople. Needs administrator rights.

.PARAMETER ExportPfx
    Also exports a .pfx file to the artifacts folder, for signing on a computer without the
    certificate store (e.g. a build server). The private key of such a certificate is exportable.
    The password is taken from -Password or from the environment variable WHICHCOM_CERT_PASSWORD.

.PARAMETER Password
    Password of the .pfx file. Never write it into a script.

.PARAMETER ValidYears
    Validity of a new certificate. Default: 2 years.

.PARAMETER Force
    Creates a new certificate even when a valid one exists.

.EXAMPLE
    ./scripts/New-DevCert.ps1

.EXAMPLE
    ./scripts/New-DevCert.ps1 -Trust      # in an elevated PowerShell
#>
[CmdletBinding()]
param(
    [switch]$Trust,
    [switch]$ExportPfx,
    [securestring]$Password,
    [ValidateRange(1, 10)]
    [int]$ValidYears = 2,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

$certificate = Get-SigningCertificate

# An elevated PowerShell may run as another user, whose store does not hold the certificate.
# Trust the exported public certificate then, instead of creating a second certificate that
# does not match the one the package is signed with.
if ($Trust -and -not $certificate) {
    if (-not (Test-Administrator)) {
        throw 'Adding a trusted certificate needs administrator rights. Start PowerShell as administrator and run this script with -Trust.'
    }

    $exported = Join-Path (Get-ArtifactsDirectory) 'WhichCOM-dev.cer'
    if (-not (Test-Path $exported)) {
        throw 'No certificate was found. Run ./scripts/New-DevCert.ps1 without -Trust as your normal user first.'
    }

    $imported = Import-Certificate -FilePath $exported -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
    Write-Host "The certificate $($imported.Subject) ($($imported.Thumbprint)) is now trusted on this computer." -ForegroundColor Green
    return
}

if ($Force -or -not $certificate -or ($ExportPfx -and -not (Test-PrivateKeyExportable $certificate))) {
    $exportPolicy = if ($ExportPfx) { 'Exportable' } else { 'NonExportable' }

    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $script:Publisher `
        -FriendlyName $script:CertificateFriendlyName `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy $exportPolicy `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears($ValidYears) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

    Write-Host "Created certificate $($script:Publisher), valid until $($certificate.NotAfter.ToString('yyyy-MM-dd'))." -ForegroundColor Green
}
else {
    Write-Host "Using existing certificate $($script:Publisher), valid until $($certificate.NotAfter.ToString('yyyy-MM-dd'))."
}

Write-Host "Thumbprint: $($certificate.Thumbprint)"

$artifacts = Get-ArtifactsDirectory
$publicFile = Join-Path $artifacts 'WhichCOM-dev.cer'
Export-Certificate -Cert $certificate -FilePath $publicFile | Out-Null
Write-Host "Public certificate: $publicFile"

if ($ExportPfx) {
    if (-not $Password -and $env:WHICHCOM_CERT_PASSWORD) {
        $Password = ConvertTo-SecureString $env:WHICHCOM_CERT_PASSWORD -AsPlainText -Force
    }
    if (-not $Password) {
        throw 'A password is required for the .pfx file. Pass -Password or set WHICHCOM_CERT_PASSWORD.'
    }

    $privateFile = Join-Path $artifacts 'WhichCOM-dev.pfx'
    Export-PfxCertificate -Cert $certificate -FilePath $privateFile -Password $Password | Out-Null
    Write-Host "Private key file:   $privateFile" -ForegroundColor Yellow
    Write-Host 'Keep this file secret. The artifacts folder is ignored by git.' -ForegroundColor Yellow
}

if ($Trust) {
    if (-not (Test-Administrator)) {
        throw "Adding a trusted certificate needs administrator rights. Start PowerShell as administrator and run: pwsh -File `"$PSCommandPath`" -Trust"
    }

    Import-Certificate -FilePath $publicFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    Write-Host 'The certificate is now trusted on this computer.' -ForegroundColor Green
}
elseif (-not (Test-CertificateTrusted $certificate)) {
    Write-Host ''
    Write-Host 'The certificate is not trusted yet. To install the signed package, start PowerShell' -ForegroundColor Yellow
    Write-Host "as administrator and run:  pwsh -File `"$PSCommandPath`" -Trust" -ForegroundColor Yellow
}
