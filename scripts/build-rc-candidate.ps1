# Ornare — Gate 3 Release Candidate Assembly and Verification Script
param(
    [string]$CandidateDir = "production_artifacts/release/candidate"
)

$ErrorActionPreference = "Stop"

$makeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
$signTool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$appCert = "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe"

if (!(Test-Path $makeAppx)) { throw "makeappx.exe not found at $makeAppx" }
if (!(Test-Path $signTool)) { throw "signtool.exe not found at $signTool" }

$layoutDir = Join-Path $CandidateDir "layout"
$outputMsix = Join-Path $CandidateDir "Ornare_1.0.0.0_x64.msix"

Write-Host "1. Assembling layout directory at: $layoutDir"
if (Test-Path $layoutDir) {
    Remove-Item $layoutDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null
New-Item -ItemType Directory -Force -Path "$layoutDir/Images" | Out-Null

$publishDir = "src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish"
if (!(Test-Path $publishDir)) {
    throw "Publish directory not found at $publishDir"
}
Copy-Item "$publishDir/*" $layoutDir -Recurse -Force

# Audit Section 73: Remove PDBs from production MSIX package layout
Get-ChildItem -Path $layoutDir -Filter *.pdb -Recurse | Remove-Item -Force

# Copy native Shell Extension DLL
$shellDll = "src/Ornare.ShellExtension/x64/Release/Ornare.ShellExtension.dll"
if (!(Test-Path $shellDll)) {
    throw "Shell extension DLL not found at $shellDll"
}
Copy-Item $shellDll $layoutDir -Force

# Copy package images
Copy-Item "src/Ornare.Package/Images/*" "$layoutDir/Images" -Force

# Copy AppxManifest.xml
Copy-Item "src/Ornare.Package/Package.appxmanifest" "$layoutDir/AppxManifest.xml" -Force

Write-Host "2. Packaging layout into: $outputMsix"
if (Test-Path $outputMsix) {
    Remove-Item $outputMsix -Force
}

$targetMsixPath = Join-Path (Resolve-Path $CandidateDir).Path "Ornare_1.0.0.0_x64.msix"
$packArgs = @("pack", "/v", "/o", "/d", (Resolve-Path $layoutDir).Path, "/p", $targetMsixPath)
$proc = Start-Process -FilePath $makeAppx -ArgumentList $packArgs -NoNewWindow -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    throw "makeappx failed with exit code $($proc.ExitCode)"
}

Write-Host "3. Signing MSIX package with development certificate"
$signArgs = @("sign", "/fd", "SHA256", "/sha1", "54D6998855C4FFAC09B8F043F2A6AE0A2FD7FDF4", $outputMsix)
$proc = Start-Process -FilePath $signTool -ArgumentList $signArgs -NoNewWindow -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    throw "signtool failed with exit code $($proc.ExitCode)"
}

Write-Host "4. Package signed successfully: $outputMsix"
$pkgItem = Get-Item $outputMsix
Write-Host "Package Size: $([math]::Round($pkgItem.Length / 1MB, 2)) MB"

# 5. Extract and audit contents (Section 73)
$unpackedDir = Join-Path $CandidateDir "unpacked_audit"
if (Test-Path $unpackedDir) {
    Remove-Item $unpackedDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $unpackedDir | Out-Null
$unpackArgs = @("unpack", "/v", "/p", $outputMsix, "/d", $unpackedDir)
$proc = Start-Process -FilePath $makeAppx -ArgumentList $unpackArgs -NoNewWindow -PassThru -Wait

$forbiddenFiles = Get-ChildItem -Path $unpackedDir -Recurse | Where-Object {
    $_.Extension -in @(".pdb", ".cs", ".cpp", ".h", ".vcxproj", ".csproj", ".ps1") -or
    $_.Name -match "(?i)(test|benchmark|mock|fake|secret|cert\.pfx)"
}

if ($forbiddenFiles.Count -gt 0) {
    Write-Warning "Forbidden files found in package:"
    $forbiddenFiles | ForEach-Object { Write-Warning " - $($_.FullName)" }
    throw "Package hygiene audit failed: forbidden files present!"
}
Write-Host "Package hygiene audit PASSED: 0 forbidden files in MSIX."
Remove-Item $unpackedDir -Recurse -Force

# 6. Generate SHA256SUMS.txt
$candidateFiles = Get-ChildItem -Path $CandidateDir -File -Filter *.msix
$hashFile = Join-Path $CandidateDir "SHA256SUMS.txt"
$hashes = @()
foreach ($f in $candidateFiles) {
    $h = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash
    $hashes += "$h  $($f.Name)"
}
$hashes | Out-File -FilePath $hashFile -Encoding utf8
Write-Host "SHA256 sums written to $hashFile"
