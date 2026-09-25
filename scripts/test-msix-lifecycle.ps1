# Ornare — Gate 3 MSIX Lifecycle Verification (Install, Upgrade, Uninstall, Reinstall, Data Ownership)
param(
    [string]$CandidateMsix = "production_artifacts/release/candidate/Ornare_1.0.0.0_x64.msix"
)

$ErrorActionPreference = "Stop"

Write-Host "=== 1. VERIFY BASELINE INSTALLED PACKAGE ==="
$pkg = Get-AppxPackage Ornare
if ($null -eq $pkg) {
    Write-Host "Package not installed. Installing candidate package..."
    Add-AppxPackage -Path (Resolve-Path $CandidateMsix).Path
    $pkg = Get-AppxPackage Ornare
}
Write-Host "Installed Package: $($pkg.PackageFullName), Status: $($pkg.Status)"

Write-Host "=== 2. CREATE AND CUSTOMIZE TEST FOLDER ==="
$testFolder = Join-Path $env:TEMP ("ornare_lifecycle_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $testFolder | Out-Null
$testIco = Join-Path $testFolder ".ornare\folder.ico"
New-Item -ItemType Directory -Force -Path (Split-Path $testIco -Parent) | Out-Null
Set-Content -Path $testIco -Value "ornare_custom_icon_data"
$desktopIni = Join-Path $testFolder "desktop.ini"
Set-Content -Path $desktopIni -Value "[.ShellClassInfo]`r`nIconResource=.ornare\folder.ico,0`r`n"
Set-ItemProperty -Path $desktopIni -Name Attributes -Value ([System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System)
Set-ItemProperty -Path $testFolder -Name Attributes -Value ([System.IO.FileAttributes]::ReadOnly)

Write-Host "Customized folder created at: $testFolder"
Write-Host "Folder ReadOnly: $((Get-Item $testFolder).Attributes.HasFlag([System.IO.FileAttributes]::ReadOnly))"
Write-Host "Desktop.ini exists: $(Test-Path $desktopIni)"

Write-Host "=== 3. SIMULATE IN-PLACE UPGRADE (v1.0.0.0 -> v1.0.1.0) ==="
$upgradeLayout = Join-Path $env:TEMP ("ornare_upgrade_layout_" + [Guid]::NewGuid().ToString("N"))
Copy-Item "production_artifacts/release/candidate/layout" $upgradeLayout -Recurse -Force
$manifestPath = Join-Path $upgradeLayout "AppxManifest.xml"
$manifestXml = Get-Content $manifestPath -Raw
$manifestXml = $manifestXml.Replace('Version="1.0.0.0"', 'Version="1.0.1.0"')
Set-Content -Path $manifestPath -Value $manifestXml -Encoding utf8

$upgradeMsix = Join-Path $env:TEMP "Ornare_1.0.1.0_x64.msix"
if (Test-Path $upgradeMsix) { Remove-Item $upgradeMsix -Force }
$makeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
$signTool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"

& $makeAppx pack /v /o /d $upgradeLayout /p $upgradeMsix
& $signTool sign /fd SHA256 /sha1 54D6998855C4FFAC09B8F043F2A6AE0A2FD7FDF4 $upgradeMsix

Add-AppxPackage -Path $upgradeMsix
$pkgAfterUpgrade = Get-AppxPackage Ornare
Write-Host "Upgraded Package: $($pkgAfterUpgrade.PackageFullName), Version: $($pkgAfterUpgrade.Version), Status: $($pkgAfterUpgrade.Status)"
if ($pkgAfterUpgrade.Version -ne "1.0.1.0") {
    throw "Upgrade failed to advance package version!"
}
if (!(Test-Path $testIco) -or !(Test-Path $desktopIni)) {
    throw "Upgrade destroyed user folder customization!"
}
Write-Host "Upgrade verification PASSED: Version advanced and user customization intact."
Remove-Item $upgradeLayout -Recurse -Force
Remove-Item $upgradeMsix -Force

Write-Host "=== 4. TEST UNINSTALL AND DATA OWNERSHIP ==="
Remove-AppxPackage -Package $pkgAfterUpgrade.PackageFullName
$pkgAfterRemoval = Get-AppxPackage Ornare
if ($null -ne $pkgAfterRemoval) {
    throw "Package removal failed!"
}
Write-Host "Package successfully removed from system."

# Critical Invariant check: User folder customization MUST survive package uninstallation!
if (!(Test-Path $testFolder) -or !(Test-Path $testIco) -or !(Test-Path $desktopIni)) {
    throw "Uninstall violated User Data Ownership invariant: user folder or icon was deleted!"
}
Write-Host "Uninstall invariant PASSED: Folder and icon fully preserved on disk."

Write-Host "=== 5. TEST REINSTALL ==="
Add-AppxPackage -Path (Resolve-Path $CandidateMsix).Path
$pkgReinstalled = Get-AppxPackage Ornare
if ($null -eq $pkgReinstalled) {
    throw "Reinstallation failed!"
}
Write-Host "Reinstall verification PASSED: $($pkgReinstalled.PackageFullName) is active."

# Cleanup test directory
Remove-Item $testFolder -Recurse -Force
Write-Host "=== ALL MSIX LIFECYCLE TESTS PASSED CLEANLY ==="
