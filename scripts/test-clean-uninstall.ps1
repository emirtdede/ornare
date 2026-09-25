# Ornare — Clean Uninstall & Reinstall Verification
$pkgBefore = Get-AppxPackage Ornare
if ($null -eq $pkgBefore) {
    Write-Host "Package not installed prior to test, installing..."
    Add-AppxPackage -Path "production_artifacts/release/Ornare_1.0.0.0_x64.msix"
    $pkgBefore = Get-AppxPackage Ornare
}

Write-Host "Initial Installed Package: $($pkgBefore.PackageFullName)"
Write-Host "Executing clean uninstall via Remove-AppxPackage..."
Remove-AppxPackage -Package $pkgBefore.PackageFullName

$pkgAfter = Get-AppxPackage Ornare
if ($null -eq $pkgAfter) {
    Write-Host "CLEAN UNINSTALL RESULT: SUCCESS. Package is completely removed from Windows AppX repository."
} else {
    Write-Error "CLEAN UNINSTALL FAILED: Package still present ($($pkgAfter.PackageFullName))."
    exit 1
}

Write-Host "Reinstalling signed MSIX package to maintain environment readiness..."
Add-AppxPackage -Path "production_artifacts/release/Ornare_1.0.0.0_x64.msix"
$pkgReinstalled = Get-AppxPackage Ornare
if ($null -ne $pkgReinstalled -and $pkgReinstalled.Status -eq "Ok") {
    Write-Host "REINSTALL RESULT: SUCCESS ($($pkgReinstalled.PackageFullName), Status: $($pkgReinstalled.Status))."
} else {
    Write-Error "REINSTALL FAILED."
    exit 1
}
