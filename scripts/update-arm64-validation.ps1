# Copy ARM64 validation packages and generate SHA256SUMS
$ErrorActionPreference = "Stop"

$valDir = "production_artifacts/release/arm64-validation"
Copy-Item "production_artifacts/release/candidate/Ornare_1.0.0.0_arm64.msix" $valDir -Force
Copy-Item "production_artifacts/release/candidate/Ornare_1.0.0.0_x64_arm64.msixbundle" $valDir -Force

$files = Get-ChildItem -Path $valDir -File | Where-Object { $_.Extension -in @(".msix", ".msixbundle") }
$hashes = @()
foreach ($f in $files) {
    $h = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash
    $hashes += "$h  $($f.Name)"
}
$hashes | Out-File -FilePath (Join-Path $valDir "SHA256SUMS.txt") -Encoding utf8
Write-Host "Copied packages and generated SHA256SUMS.txt in $valDir"
foreach ($line in $hashes) {
    Write-Host "  $line"
}
