# Ornare — Package Builder
# Assembles package layout and creates production MSIX using Windows SDK MakeAppx

param(
    [string]$LayoutDir = "production_artifacts/release/layout",
    [string]$OutputMsix = "production_artifacts/release/Ornare_1.0.0.0_x64.msix"
)

$ErrorActionPreference = "Stop"

$makeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
if (!(Test-Path $makeAppx)) {
    throw "makeappx.exe not found at $makeAppx"
}

Write-Host "1. Preparing layout directory at: $LayoutDir"
if (Test-Path $LayoutDir) {
    Remove-Item $LayoutDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $LayoutDir | Out-Null
New-Item -ItemType Directory -Force -Path "$LayoutDir/Images" | Out-Null

# Copy publish files
$publishDir = "src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish"
if (!(Test-Path $publishDir)) {
    throw "Publish directory not found at $publishDir"
}
Copy-Item "$publishDir/*" $LayoutDir -Recurse -Force

# Copy native Shell Extension DLL
$shellDll = "src/Ornare.ShellExtension/x64/Release/Ornare.ShellExtension.dll"
if (!(Test-Path $shellDll)) {
    throw "Shell extension DLL not found at $shellDll"
}
Copy-Item $shellDll $LayoutDir -Force

# Copy package images
Copy-Item "src/Ornare.Package/Images/*" "$LayoutDir/Images" -Force

# Copy AppxManifest.xml
Copy-Item "src/Ornare.Package/Package.appxmanifest" "$LayoutDir/AppxManifest.xml" -Force

Write-Host "2. Packaging layout into: $OutputMsix"
$releaseDir = Split-Path $OutputMsix -Parent
if (!(Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
}
if (Test-Path $OutputMsix) {
    Remove-Item $OutputMsix -Force
}

$packArgs = @("pack", "/v", "/o", "/d", (Resolve-Path $LayoutDir).Path, "/p", (Join-Path (Resolve-Path $releaseDir).Path "Ornare_1.0.0.0_x64.msix"))
$process = Start-Process -FilePath $makeAppx -ArgumentList $packArgs -NoNewWindow -PassThru -Wait

if ($process.ExitCode -ne 0) {
    throw "makeappx failed with exit code $($process.ExitCode)"
}

Write-Host "MSIX Package created successfully: $OutputMsix"
$pkgItem = Get-Item $OutputMsix
Write-Host "Package size: $([math]::Round($pkgItem.Length / 1MB, 2)) MB"
