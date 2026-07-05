# Creates the development MSIX signing certificate used by NickeltownFinance.csproj.
# The .pfx output is gitignored — run this once per developer machine (or CI secret import).
param(
    [string]$ProjectDir = (Split-Path -Parent $PSScriptRoot),
    [string]$Password = "NickeltownFinance"
)

$ErrorActionPreference = "Stop"
$pfxPath = Join-Path $ProjectDir "NickeltownFinance_TemporaryKey.pfx"
$subject = "CN=Nickeltown Finance"

Write-Host "Creating self-signed code-signing certificate: $subject"
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $subject `
    -KeyUsage DigitalSignature `
    -FriendlyName "Nickeltown Finance MSIX Dev" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
    -NotAfter (Get-Date).AddYears(5)

$secure = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $secure | Out-Null

$cerPath = [System.IO.Path]::ChangeExtension($pfxPath, ".cer")
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
Import-PfxCertificate -FilePath $pfxPath -Password $secure -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null

Write-Host "Exported: $pfxPath"
Write-Host "Publisher thumbprint: $($cert.Thumbprint)"
Write-Host ""
Write-Host "IMPORTANT: MSIX install requires Local Machine trust (Administrator)."
Write-Host "Run:  .\scripts\Trust-MsixCertificate.ps1"
Write-Host ""

$propsPath = Join-Path $ProjectDir "NickeltownFinance.Signing.props"
$propsContent = @"
<Project>
  <PropertyGroup>
    <PackageCertificateThumbprint>$($cert.Thumbprint)</PackageCertificateThumbprint>
  </PropertyGroup>
</Project>
"@
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($propsPath, $propsContent, $utf8)
Write-Host "Updated $propsPath"
