# Ornare - ARM64 Toolchain Forensic Verification
$ErrorActionPreference = "Stop"

$vsWhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vsWhere)) {
    throw "vswhere.exe not found at $vsWhere"
}

Write-Host "=== 1. VSWHERE COMPONENT QUERY ==="
$installPath = & $vsWhere -all -products * -requires Microsoft.VisualStudio.Component.VC.Tools.ARM64 -property installationPath
if (!$installPath) {
    throw "Component Microsoft.VisualStudio.Component.VC.Tools.ARM64 is NOT reported by vswhere!"
}
Write-Host "Found VS Installation with ARM64 C++ Build Tools: $installPath"

Write-Host "`n=== 2. COMPILER & LINKER ==="
$vcToolsDir = Join-Path $installPath "VC\Tools\MSVC"
$msvcVersions = Get-ChildItem $vcToolsDir -Directory
$latestMsvc = ($msvcVersions | Sort-Object Name -Descending)[0].FullName
Write-Host "MSVC Version Directory: $latestMsvc"

$clHostX64 = Join-Path $latestMsvc "bin\Hostx64\arm64\cl.exe"
$linkHostX64 = Join-Path $latestMsvc "bin\Hostx64\arm64\link.exe"

if (!(Test-Path $clHostX64)) {
    throw "ARM64 cl.exe not found at $clHostX64"
}
Write-Host "cl.exe (Hostx64\arm64): $clHostX64 (Exists: True)"

if (!(Test-Path $linkHostX64)) {
    throw "ARM64 link.exe not found at $linkHostX64"
}
Write-Host "link.exe (Hostx64\arm64): $linkHostX64 (Exists: True)"

Write-Host "`n=== 3. CRT & LIBRARIES ==="
$arm64Lib = Join-Path $latestMsvc "lib\arm64"
if (!(Test-Path $arm64Lib)) {
    throw "ARM64 lib directory not found at $arm64Lib"
}
$libCount = (Get-ChildItem $arm64Lib -Filter "*.lib").Count
Write-Host "ARM64 CRT/Lib Directory: $arm64Lib ($libCount .lib files found)"

Write-Host "`n=== 4. MSBUILD VC PLATFORMS ==="
$msbuildPlatformArm64 = Join-Path $installPath "MSBuild\Microsoft\VC\v170\Platforms\ARM64"
if (!(Test-Path $msbuildPlatformArm64)) {
    throw "MSBuild VC ARM64 platform not found at $msbuildPlatformArm64"
}
Write-Host "MSBuild VC Platforms\ARM64: $msbuildPlatformArm64 (Exists: True)"

Write-Host "`n=== 5. WINDOWS SDK ARM64 LIBS ==="
$sdkRoot = "C:\Program Files (x86)\Windows Kits\10\Lib"
$sdkArm64 = Get-ChildItem $sdkRoot -Directory -Filter "arm64" -Recurse
Write-Host "Windows SDK ARM64 directories found: $($sdkArm64.Count)"
foreach ($d in $sdkArm64) {
    Write-Host "  -> $($d.FullName)"
}

Write-Host "`n=== ARM64 TOOLCHAIN: VERIFIED ==="
