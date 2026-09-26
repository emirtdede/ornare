# Ornare - Multi-Architecture (x64 + ARM64) Release Assembly and Verification
$ErrorActionPreference = "Stop"

$makeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
$makePri = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makepri.exe"
$signTool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$dumpbin = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207\bin\Hostx64\x64\dumpbin.exe"
$thumbprint = "2159112CBF6E9F647006D998CF42D23F45F14DA1"

$releaseDir = "production_artifacts/release/candidate"
if (!(Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
}

function Build-ArchPackage([string]$arch) {
    Write-Host "`n======================================================="
    Write-Host "ASSEMBLING MSIX FOR ARCHITECTURE: $arch"
    Write-Host "======================================================="

    $layoutDir = Join-Path $releaseDir "layout-$arch"
    if (Test-Path $layoutDir) {
        Remove-Item $layoutDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $layoutDir "Images") | Out-Null

    # 1. Publish files (Native AOT)
    $publishCandidates = if ($arch -eq "arm64") {
        @( "src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish",
           "src/Ornare.App/bin/ARM64/Release/net10.0-windows10.0.26100.0/win-arm64/publish" )
    } else {
        @( "src/Ornare.App/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish",
           "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/publish" )
    }
    $publishDir = $publishCandidates | Where-Object { Test-Path $_ } | Sort-Object { (Get-Item $_).LastWriteTime } -Descending | Select-Object -First 1
    if (!$publishDir -or !(Test-Path $publishDir)) {
        throw "Publish directory not found for $arch"
    }
    Copy-Item "$publishDir/*" $layoutDir -Recurse -Force

    # Strip PDBs from production package
    Get-ChildItem -Path $layoutDir -Filter *.pdb -Recurse | Remove-Item -Force

    # 1b. Ornare.Cli executable (F24)
    $cliCandidates = if ($arch -eq "arm64") {
        @( "src/Ornare.Cli/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish/Ornare.Cli.exe",
           "src/Ornare.Cli/bin/ARM64/Release/net10.0-windows10.0.26100.0/win-arm64/publish/Ornare.Cli.exe" )
    } else {
        @( "src/Ornare.Cli/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.Cli.exe",
           "src/Ornare.Cli/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.Cli.exe" )
    }
    $cliExe = $cliCandidates | Where-Object { Test-Path $_ } | Sort-Object { (Get-Item $_).LastWriteTime } -Descending | Select-Object -First 1
    if (!$cliExe -or !(Test-Path $cliExe)) {
        throw "Ornare.Cli.exe publish binary not found for $arch"
    }
    Copy-Item $cliExe $layoutDir -Force

    # 2. Native Shell Extension DLL
    $shellDll = if ($arch -eq "arm64") {
        "src/Ornare.ShellExtension/ARM64/Release/Ornare.ShellExtension.dll"
    } else {
        "src/Ornare.ShellExtension/x64/Release/Ornare.ShellExtension.dll"
    }
    if (!(Test-Path $shellDll)) {
        throw "Shell extension DLL not found: $shellDll"
    }
    Copy-Item $shellDll $layoutDir -Force

    # 3. Package Images
    Copy-Item "src/Ornare.Package/Images/*" (Join-Path $layoutDir "Images") -Force

    # 4. AppxManifest.xml with specific architecture
    $manifestContent = Get-Content "src/Ornare.Package/Package.appxmanifest" -Raw
    $targetArch = if ($arch -eq "arm64") { "arm64" } else { "x64" }
    $manifestContent = $manifestContent -replace 'ProcessorArchitecture="[^"]+"', "ProcessorArchitecture=`"$targetArch`""
    Set-Content -Path (Join-Path $layoutDir "AppxManifest.xml") -Value $manifestContent -Encoding utf8

    # 5. Compile resources.pri using makepri.exe (merging Ornare.App.pri to retain XAML/XBF and strings)
    Write-Host "Compiling root resources.pri for $arch..."
    $priConfigPath = Join-Path $releaseDir "priconfig_$arch.xml"
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
    $tempPri = [System.IO.Path]::GetFullPath((Join-Path $releaseDir "temp_resources_$arch.pri"))
    if (Test-Path $tempPri) { Remove-Item $tempPri -Force }
    if (Test-Path $priOutput) { Remove-Item $priOutput -Force }
    $manifestPath = (Join-Path (Resolve-Path $layoutDir).Path "AppxManifest.xml")
    $priArgs = @("new", "/pr", (Resolve-Path $layoutDir).Path, "/cf", (Resolve-Path $priConfigPath).Path, "/mn", $manifestPath, "/of", $tempPri, "/o")
    $priProc = Start-Process -FilePath $makePri -ArgumentList $priArgs -NoNewWindow -PassThru -Wait
    if ($priProc.ExitCode -ne 0 -or !(Test-Path $tempPri)) {
        throw "makepri new failed for $arch with exit code $($priProc.ExitCode)"
    }
    Move-Item $tempPri $priOutput -Force
    Write-Host "resources.pri successfully compiled for $arch (Size: $((Get-Item $priOutput).Length) bytes)."


    # 6. Pack MSIX
    $outputMsix = Join-Path $releaseDir "Ornare_1.0.0.0_$arch.msix"
    if (Test-Path $outputMsix) {
        Remove-Item $outputMsix -Force
    }
    $fullOutputPath = [System.IO.Path]::GetFullPath($outputMsix)
    $packArgs = @("pack", "/v", "/o", "/d", (Resolve-Path $layoutDir).Path, "/p", $fullOutputPath)
    $proc = Start-Process -FilePath $makeAppx -ArgumentList $packArgs -NoNewWindow -PassThru -Wait
    if ($proc.ExitCode -ne 0) {
        throw "makeappx pack failed for $arch with exit code $($proc.ExitCode)"
    }

    # 7. Sign package
    $signArgs = @("sign", "/fd", "SHA256", "/sha1", $thumbprint, $outputMsix)
    $proc = Start-Process -FilePath $signTool -ArgumentList $signArgs -NoNewWindow -PassThru -Wait
    if ($proc.ExitCode -ne 0) {
        throw "signtool failed for $outputMsix with exit code $($proc.ExitCode)"
    }
    Write-Host "Successfully packaged and signed: $outputMsix"
    return $outputMsix
}

# Build both packages
$x64Msix = Build-ArchPackage "x64"
$arm64Msix = Build-ArchPackage "arm64"

# Audit packages
Write-Host "`n======================================================="
Write-Host "AUDITING UNPACKED PACKAGES"
Write-Host "======================================================="

function Audit-Package([string]$msixPath, [string]$expectedMachine) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($msixPath)
    $unpackDir = Join-Path $releaseDir "audit_$baseName"
    if (Test-Path $unpackDir) { Remove-Item $unpackDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $unpackDir | Out-Null

    $unpackArgs = @("unpack", "/v", "/p", $msixPath, "/d", $unpackDir)
    $proc = Start-Process -FilePath $makeAppx -ArgumentList $unpackArgs -NoNewWindow -PassThru -Wait
    if ($proc.ExitCode -ne 0) { throw "unpack failed for $msixPath" }

    # Check forbidden files
    $forbidden = Get-ChildItem -Path $unpackDir -Recurse | Where-Object {
        $_.Extension -in @(".pdb", ".cs", ".cpp", ".h", ".vcxproj", ".csproj", ".ps1") -or
        $_.Name -match "(?i)(test|benchmark|mock|fake|cert\.pfx)"
    }
    if ($forbidden.Count -gt 0) {
        throw "Package contains forbidden files: $($forbidden.FullName -join ', ')"
    }

    # PE Machine audit on all native .exe and .dll
    $peFiles = Get-ChildItem -Path $unpackDir -Recurse -Include *.exe, *.dll
    Write-Host "Auditing $($peFiles.Count) binaries in $msixPath (Expected machine: $expectedMachine):"
    foreach ($pe in $peFiles) {
        $clrDump = & $dumpbin /clrheader $pe.FullName 2>$null
        $isIlOnly = ($clrDump | Select-String "IL Only") -ne $null
        if ($isIlOnly) {
            # AnyCPU / IL-Only managed assemblies are executed by the arch-specific CLR host
            continue
        }
        $headerDump = & $dumpbin /headers $pe.FullName
        $machineLine = ($headerDump | Select-String "machine \(([^)]+)\)")
        if ($machineLine -match "machine \(([^)]+)\)") {
            $mach = $matches[1]
            Write-Host "  -> $($pe.Name): $mach"
            if ($mach -ne $expectedMachine) {
                throw "BINARY MACHINE MISMATCH: $($pe.FullName) has machine $mach, expected $expectedMachine!"
            }
        }
    }
    Write-Host "Package audit passed for $($msixPath): All native binaries match $expectedMachine!"
    Start-Sleep -Milliseconds 400
    Remove-Item $unpackDir -Recurse -Force -ErrorAction SilentlyContinue
}

Audit-Package $x64Msix "x64"
Audit-Package $arm64Msix "ARM64"

Write-Host "`n======================================================="
Write-Host "BUILDING MULTI-ARCH MSIXBUNDLE"
Write-Host "======================================================="

$bundleDir = Join-Path $releaseDir "bundle_staging"
if (Test-Path $bundleDir) { Remove-Item $bundleDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null

Copy-Item $x64Msix $bundleDir -Force
Copy-Item $arm64Msix $bundleDir -Force

$outputBundle = Join-Path $releaseDir "Ornare_1.0.0.0_x64_arm64.msixbundle"
if (Test-Path $outputBundle) { Remove-Item $outputBundle -Force }

$bundleArgs = @("bundle", "/v", "/o", "/bv", "1.0.0.0", "/d", (Resolve-Path $bundleDir).Path, "/p", (Join-Path (Resolve-Path $releaseDir).Path "Ornare_1.0.0.0_x64_arm64.msixbundle"))
$proc = Start-Process -FilePath $makeAppx -ArgumentList $bundleArgs -NoNewWindow -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    throw "makeappx bundle failed with exit code $($proc.ExitCode)"
}

Write-Host "Signing MSIXBundle: $outputBundle"
$signArgs = @("sign", "/fd", "SHA256", "/sha1", $thumbprint, $outputBundle)
$proc = Start-Process -FilePath $signTool -ArgumentList $signArgs -NoNewWindow -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    throw "signtool failed for $outputBundle with exit code $($proc.ExitCode)"
}
Write-Host "Bundle packaged and signed successfully: $outputBundle"

# Audit Bundle Contents
Write-Host "`n======================================================="
Write-Host "AUDITING MSIXBUNDLE CONTENTS"
Write-Host "======================================================="
$unpackBundleDir = Join-Path $releaseDir "bundle_unpacked"
if (Test-Path $unpackBundleDir) { Remove-Item $unpackBundleDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $unpackBundleDir | Out-Null

$proc = Start-Process -FilePath $makeAppx -ArgumentList @("unbundle", "/v", "/p", $outputBundle, "/d", $unpackBundleDir) -NoNewWindow -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    throw "makeappx unbundle failed with exit code $($proc.ExitCode)"
}

$bundleManifest = Get-Content (Join-Path $unpackBundleDir "AppxMetadata\AppxBundleManifest.xml") -Raw
Write-Host "Bundle Manifest Package Elements:"
[xml]$manifestXml = $bundleManifest
foreach ($pkg in $manifestXml.Bundle.Packages.Package) {
    Write-Host "  -> Architecture: $($pkg.Architecture), Version: $($pkg.Version), FileName: $($pkg.FileName)"
}
Remove-Item $unpackBundleDir -Recurse -Force
# Build and sign Installer
Write-Host "`n======================================================="
Write-Host "BUILDING AND SIGNING ORNARE SETUP INSTALLER"
Write-Host "======================================================="
$msBuild = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
$installerProj = "src/Ornare.Installer/Ornare.Installer.vcxproj"
& $msBuild $installerProj /p:Configuration=Release /p:Platform=x64 /t:Rebuild /nodeReuse:false
if ($LASTEXITCODE -ne 0) {
    throw "Installer MSBuild failed with exit code $LASTEXITCODE"
}
$builtCandidates = @(
    "production_artifacts/release/candidate/OrnareSetup.exe",
    "src/Ornare.Installer/production_artifacts/release/candidate/OrnareSetup.exe",
    "src/Ornare.Installer/x64/Release/OrnareSetup.exe",
    "src/Ornare.Installer/x64/Release/Ornare.Installer.exe"
)
$builtInstaller = $builtCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$builtInstaller) {
    throw "Built installer executable not found among candidates"
}
$candidateInstaller = Join-Path $releaseDir "OrnareSetup.exe"
if ([System.IO.Path]::GetFullPath($builtInstaller) -ne [System.IO.Path]::GetFullPath($candidateInstaller)) {
    Copy-Item $builtInstaller $candidateInstaller -Force
}
$signArgs = @("sign", "/fd", "SHA256", "/sha1", $thumbprint, $candidateInstaller)
$proc = Start-Process -FilePath $signTool -ArgumentList $signArgs -NoNewWindow -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    throw "signtool failed for $candidateInstaller with exit code $($proc.ExitCode)"
}
Write-Host "Installer built and signed successfully: $candidateInstaller"

# Also update Desktop copy for the user
$desktopSetup = "C:\Users\emir\Desktop\OrnareSetup.exe"
Copy-Item $candidateInstaller $desktopSetup -Force
Write-Host "Updated Desktop copy: $desktopSetup"

# Update SHA256SUMS.txt
$candidateFiles = Get-ChildItem -Path $releaseDir -File | Where-Object { $_.Extension -in @(".msix", ".msixbundle", ".exe") }
$hashFile = Join-Path $releaseDir "SHA256SUMS.txt"
$hashes = @()
foreach ($f in $candidateFiles) {
    $h = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash
    $hashes += "$h  $($f.Name)"
}
$hashes | Out-File -FilePath $hashFile -Encoding utf8
Write-Host "`nSHA256SUMS.txt updated in $releaseDir"
foreach ($line in $hashes) {
    Write-Host "  $line"
}

Write-Host "`n=== MULTI-ARCH PACKAGING AND AUDIT COMPLETE ==="
