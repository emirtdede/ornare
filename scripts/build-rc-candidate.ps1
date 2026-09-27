# Ornare — Gate 3 Release Candidate Assembly and Verification Script
param(
    [string]$CandidateDir = "production_artifacts/release/candidate"
)

$ErrorActionPreference = "Stop"

$makeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
$makePri = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makepri.exe"
$signTool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$appCert = "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe"
$dumpbin = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207\bin\Hostx64\x64\dumpbin.exe"
$thumbprint = "2159112CBF6E9F647006D998CF42D23F45F14DA1"

if (!(Test-Path $makeAppx)) { throw "makeappx.exe not found at $makeAppx" }
if (!(Test-Path $makePri)) { throw "makepri.exe not found at $makePri" }
if (!(Test-Path $signTool)) { throw "signtool.exe not found at $signTool" }

$layoutDir = Join-Path $CandidateDir "layout"
$outputMsix = Join-Path $CandidateDir "Ornare_1.0.0.0_x64.msix"

Write-Host "1. Assembling layout directory at: $layoutDir"
if (Test-Path $layoutDir) {
    Remove-Item $layoutDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null
New-Item -ItemType Directory -Force -Path "$layoutDir/Images" | Out-Null

$publishCandidates = @(
    "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/publish",
    "src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish"
)
$publishDir = $publishCandidates | Where-Object { Test-Path $_ } | Sort-Object { (Get-Item $_).LastWriteTime } -Descending | Select-Object -First 1
if (!$publishDir -or !(Test-Path $publishDir)) {
    throw "Publish directory not found"
}
Copy-Item "$publishDir/*" $layoutDir -Recurse -Force

# Copy Ornare.Cli executable
$cliCandidates = @(
    "src/Ornare.Cli/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.Cli.exe",
    "src/Ornare.Cli/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.Cli.exe"
)
$cliExe = $cliCandidates | Where-Object { Test-Path $_ } | Sort-Object { (Get-Item $_).LastWriteTime } -Descending | Select-Object -First 1
if (!$cliExe -or !(Test-Path $cliExe)) {
    throw "Ornare.Cli.exe not found"
}
Copy-Item $cliExe $layoutDir -Force

# Audit: Remove PDBs from production MSIX package layout
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

# Compile resources.pri using makepri.exe
Write-Host "Compiling root resources.pri for x64..."
$priConfigPath = Join-Path $CandidateDir "priconfig_x64.xml"
$configContent = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<resources targetOsVersion="10.0.0" majorVersion="1">
	<index root="\" startIndexAt="\">
		<default>
			<qualifier name="Language" value="en-US"/>
			<qualifier name="Contrast" value="standard"/>
			<qualifier name="Scale" value="100"/>
			<qualifier name="HomeRegion" value="001"/>
			<qualifier name="TargetSize" value="256"/>
			<qualifier name="LayoutDirection" value="LTR"/>
			<qualifier name="Theme" value="dark"/>
			<qualifier name="AlternateForm" value=""/>
			<qualifier name="DXFeatureLevel" value="DX9"/>
			<qualifier name="Configuration" value=""/>
			<qualifier name="DeviceFamily" value="Universal"/>
			<qualifier name="Custom" value=""/>
		</default>
		<indexer-config type="folder" foldernameAsQualifier="true" filenameAsQualifier="true" qualifierDelimiter="."/>
		<indexer-config type="resw" convertDotsToSlashes="true" initialPath=""/>
		<indexer-config type="resjson" initialPath=""/>
		<indexer-config type="PRI"/>
	</index>
</resources>
"@
Set-Content -Path $priConfigPath -Value $configContent -Encoding utf8
$priOutput = Join-Path $layoutDir "resources.pri"
$tempPri = [System.IO.Path]::GetFullPath((Join-Path $CandidateDir "temp_resources_x64.pri"))
if (Test-Path $tempPri) { Remove-Item $tempPri -Force }
if (Test-Path $priOutput) { Remove-Item $priOutput -Force }
$manifestPath = (Join-Path (Resolve-Path $layoutDir).Path "AppxManifest.xml")
$priArgs = @("new", "/pr", (Resolve-Path $layoutDir).Path, "/cf", (Resolve-Path $priConfigPath).Path, "/mn", $manifestPath, "/of", $tempPri, "/o")
$priProc = Start-Process -FilePath $makePri -ArgumentList $priArgs -NoNewWindow -PassThru -Wait
if ($priProc.ExitCode -ne 0 -or !(Test-Path $tempPri)) {
    throw "makepri new failed for x64 with exit code $($priProc.ExitCode)"
}
Move-Item $tempPri $priOutput -Force
Write-Host "resources.pri successfully compiled for x64 (Size: $((Get-Item $priOutput).Length) bytes)."

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

Write-Host "3. Signing MSIX package with certificate ($thumbprint)"
$signArgs = @("sign", "/fd", "SHA256", "/sha1", $thumbprint, $outputMsix)
$proc = Start-Process -FilePath $signTool -ArgumentList $signArgs -NoNewWindow -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    throw "signtool failed with exit code $($proc.ExitCode)"
}

Write-Host "4. Package signed successfully: $outputMsix"
$pkgItem = Get-Item $outputMsix
Write-Host "Package Size: $([math]::Round($pkgItem.Length / 1MB, 2)) MB"

# 5. Extract and audit contents
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

# Check PE Machine for all binaries in package
$peFiles = Get-ChildItem -Path $unpackedDir -Recurse -Include *.exe, *.dll
Write-Host "Auditing $($peFiles.Count) binaries in $outputMsix (Expected: x64 / 0x8664):"
foreach ($pe in $peFiles) {
    $clrDump = & $dumpbin /clrheader $pe.FullName 2>$null
    $isIlOnly = ($clrDump | Select-String "IL Only") -ne $null
    if ($isIlOnly) {
        continue
    }
    $headerDump = & $dumpbin /headers $pe.FullName
    $machineLine = ($headerDump | Select-String "machine \(([^)]+)\)")
    if ($machineLine -match "machine \(([^)]+)\)") {
        $mach = $matches[1]
        Write-Host "  -> $($pe.Name): $mach"
        if ($mach -ne "x64") {
            throw "BINARY MACHINE MISMATCH: $($pe.FullName) has machine $mach, expected x64!"
        }
    }
}
Write-Host "Package binary architecture audit PASSED: all native binaries are x64!"

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
