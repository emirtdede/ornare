# Ornare — One-Time Development Machine Setup: Trust Dev Certificate
# Trusts the local development public certificate in LocalMachine\TrustedPeople for MSIX deployment testing.
# This script does NOT alter security policies, disable Defender, or bypass any OS protections.

param(
    [string]$CerPath = "production_artifacts/release/OrnareDevCert.cer"
)

$ErrorActionPreference = "Stop"

$fullCerPath = (Resolve-Path $CerPath).Path
if (!(Test-Path $fullCerPath)) {
    throw "Certificate file not found at: $fullCerPath"
}

$isAdmin = [bool](([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))

if (!$isAdmin) {
    Write-Host "Elevating with UAC to add certificate to LocalMachine\TrustedPeople..."
    $cmd = "certutil.exe -addstore TrustedPeople `"$fullCerPath`""
    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -Command `"$cmd`"" -Verb RunAs -PassThru -Wait
    if ($proc.ExitCode -eq 0) {
        Write-Host "Successfully trusted development certificate in LocalMachine\TrustedPeople."
    } else {
        Write-Warning "Elevation cancelled or failed with exit code $($proc.ExitCode)."
        exit $proc.ExitCode
    }
} else {
    Write-Host "Running as Administrator. Adding certificate directly..."
    $res = & certutil.exe -addstore "TrustedPeople" $fullCerPath
    Write-Host $res
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully trusted development certificate in LocalMachine\TrustedPeople."
    } else {
        throw "Failed to add certificate to TrustedPeople store."
    }
}

# Verify certificate exists in LocalMachine\TrustedPeople
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPeople", "LocalMachine")
$store.Open("ReadOnly")
$found = $store.Certificates | Where-Object { $_.Subject -eq "CN=Ornare" }
$store.Close()

if ($found) {
    Write-Host "VERIFIED: CN=Ornare is trusted in LocalMachine\TrustedPeople."
    exit 0
} else {
    Write-Warning "CN=Ornare was not found in LocalMachine\TrustedPeople."
    exit 1
}
