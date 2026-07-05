# Trusts the Nickeltown Finance MSIX signing certificate for sideload install.
# Must run as Administrator (Local Machine certificate stores).
param(
    [string]$ProjectDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "src\NickeltownFinance")
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Re-launching as Administrator..."
    $scriptArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $PSCommandPath, "-ProjectDir", $ProjectDir)
    Start-Process powershell.exe -Verb RunAs -ArgumentList $scriptArgs -Wait
    exit $LASTEXITCODE
}

$cerPath = Join-Path $ProjectDir "NickeltownFinance_TemporaryKey.cer"
if (-not (Test-Path $cerPath)) {
    throw "Certificate not found: $cerPath. Run scripts/Create-DevCertificate.ps1 first."
}

Write-Host "Installing certificate to Local Machine trust stores..."
Write-Host "(Automatic import often puts the cert in Intermediate CA — that does NOT work for MSIX.)"
Write-Host ""

certutil -addstore Root $cerPath
if ($LASTEXITCODE -ne 0) { throw "Failed to add certificate to Local Machine\Root." }

certutil -addstore TrustedPeople $cerPath
if ($LASTEXITCODE -ne 0) { throw "Failed to add certificate to Local Machine\TrustedPeople." }

Write-Host ""
Write-Host "Verify: cert should appear in Local Machine > Trusted Root Certification Authorities."
Write-Host "Done. Double-click NickeltownFinance.msix to install."
