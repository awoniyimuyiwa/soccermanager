<#
.SYNOPSIS
    Generates a PFX certificate and outputs a Base64 string for .NET Data Protection.

.PARAMETER Password
    REQUIRED. The password used to protect the PFX file. If not provided via CLI, you will be prompted securely.

.PARAMETER CertName
    The name of the certificate file. Defaults to 'DataProtectionCert'.

.PARAMETER Days
    The number of days the certificate should be valid. Defaults to 1 day for security.

.DESCRIPTION
    This script is for Windows. If you are on Linux or macOS, use these OpenSSL commands:
    1. Generate Private Key & Cert:
       openssl req -x509 -newkey rsa:2048 -nodes -keyout test.key -out test.crt -days 1 -subj "/CN=DataProtectionTest"
    2. Export to PFX:
       openssl pkcs12 -export -out test.pfx -inkey test.key -in test.crt -passout pass:YourSecurePassword123
    3. Convert to Base64:
       base64 -i test.pfx (or base64 -w0 test.pfx)
#>

Param(
    [Parameter(Mandatory=$false)]
    [string]$Password,

    [string]$CertName = "DataProtectionCert",

    [int]$Days = 1
)

# 1. If Password wasn't passed as a parameter, prompt for it securely
if ([string]::IsNullOrWhiteSpace($Password)) {
    # This returns a SecureString object
    $SecureInput = Read-Host -Prompt "Enter password for PFX (input will be hidden)" -AsSecureString
    
    # Correct way to decrypt SecureString to plain text for the Export process
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureInput)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringUni($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
}

# 2. Setup Paths
# Use the folder where the script is located
$pfxPath = Join-Path -Path $PSScriptRoot -ChildPath "$CertName.pfx"
$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText

# Ensure the directory exists (Safety check for the HRESULT: 0x80070003 error)
if (!(Test-Path $PSScriptRoot)) {
    New-Item -ItemType Directory -Path $PSScriptRoot -Force
}

# 3. Create the Self-Signed Certificate
Write-Host "Generating certificate '$CertName' (Valid for $Days days)..." -ForegroundColor Cyan
$cert = New-SelfSignedCertificate -Subject "CN=$CertName" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyAlgorithm RSA -KeyLength 2048 `
    -NotBefore (Get-Date) -NotAfter (Get-Date).AddDays($Days) `
    -HashAlgorithm SHA256

# 4. Export to PFX file
Write-Host "Exporting to PFX at: $pfxPath" -ForegroundColor Cyan
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword

# 5. Convert PFX to Base64 String
if (Test-Path $pfxPath) {
    $fileBytes = [System.IO.File]::ReadAllBytes($pfxPath)
    $base64String = [System.Convert]::ToBase64String($fileBytes)

    # 5. Output the result
    Write-Host "`n--- Base64 String for appsettings.json ---" -ForegroundColor Green
    Write-Host $base64String
   
    # Cleanup the temporary PFX file
    Remove-Item -Path $pfxPath -Force
    Write-Host "`nDone! PFX removed. Use the password you just typed in your config." -ForegroundColor Yellow
}
else {
    Write-Error "Failed to create PFX file at $pfxPath"
}