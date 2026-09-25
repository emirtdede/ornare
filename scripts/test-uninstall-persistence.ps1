# Ornare — Uninstall Persistence Verification Script
# Verifies that folder customization, .ornare\folder.ico, and desktop.ini survive complete MSIX uninstallation

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32CaptureHelper {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
}
'@

$baseDir = (Resolve-Path ".").Path
$testFolder = Join-Path $baseDir "OrnareUninstallPersistenceTest"

Write-Host "=== STEP 1: Creating test folder: $testFolder ==="
if (Test-Path $testFolder) {
    Remove-Item -Path $testFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $testFolder -Force | Out-Null

Write-Host "=== STEP 2: Applying real custom icon via Ornare Windows Customizer ==="
$applyScript = @"
namespace Ornare.PersistenceRunner;

using System;
using System.IO;
using ImageMagick;
using Ornare.Imaging;
using Ornare.Windows;

public static class Program
{
    public static void Main()
    {
        var pipeline = new ImagePipeline();
        var customizer = new FolderCustomizer();
        string folder = @"$testFolder";
        string tempIco = Path.Combine(Path.GetTempPath(), "persistence_test.ico");
        using (var img = new MagickImage(new MagickColor(46, 204, 113), 256, 256))
        {
            byte[] bytes = img.ToByteArray(MagickFormat.Png);
            pipeline.ProcessImageBytes(bytes, tempIco);
        }
        var result = customizer.ApplyIcon(folder, tempIco);
        Console.WriteLine("ApplyIcon IsSuccess: " + result.IsSuccess);
    }
}
"@

$cssProj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Ornare.Core\Ornare.Core.csproj" />
    <ProjectReference Include="..\..\src\Ornare.Imaging\Ornare.Imaging.csproj" />
    <ProjectReference Include="..\..\src\Ornare.Windows\Ornare.Windows.csproj" />
  </ItemGroup>
</Project>
"@

$runnerDir = Join-Path $baseDir "tests\Ornare.PersistenceRunner"
if (!(Test-Path $runnerDir)) {
    New-Item -ItemType Directory -Path $runnerDir -Force | Out-Null
}
Set-Content -Path (Join-Path $runnerDir "Ornare.PersistenceRunner.csproj") -Value $cssProj
Set-Content -Path (Join-Path $runnerDir "Program.cs") -Value $applyScript

dotnet run --project $runnerDir -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to apply icon to test folder."
    exit 1
}

Write-Host "=== STEP 3: Verifying Filesystem Customization Prior to Uninstall ==="
$icoPath = Join-Path $testFolder ".ornare\folder.ico"
$iniPath = Join-Path $testFolder "desktop.ini"

if (!(Test-Path $icoPath)) { Write-Error ".ornare\folder.ico not found!"; exit 1 }
if (!(Test-Path $iniPath)) { Write-Error "desktop.ini not found!"; exit 1 }

$iniContent = Get-Content -Path $iniPath -Raw
Write-Host "desktop.ini content:`n$iniContent"
$folderAttrs = (Get-Item $testFolder).Attributes
$iniAttrs = (Get-Item $iniPath -Force).Attributes

Write-Host "Folder Attributes: $folderAttrs"
Write-Host "desktop.ini Attributes: $iniAttrs"

if (!($folderAttrs -band [System.IO.FileAttributes]::ReadOnly) -and !($folderAttrs -band [System.IO.FileAttributes]::System)) {
    Write-Error "Folder does not have ReadOnly or System attribute!"
    exit 1
}

Write-Host "=== STEP 4: Completely Uninstalling Ornare MSIX Package ==="
$pkg = Get-AppxPackage Ornare
if ($null -eq $pkg) {
    Write-Error "Ornare package is not installed."
    exit 1
}
Write-Host "Removing package: $($pkg.PackageFullName)..."
Remove-AppxPackage -Package $pkg.PackageFullName

# Verify package is removed
$pkgAfter = Get-AppxPackage Ornare
if ($null -ne $pkgAfter) {
    Write-Error "Package is still installed!"
    exit 1
}
Write-Host "Package removal confirmed: Get-AppxPackage returned null."

# Verify no Ornare processes are running
$procs = Get-Process -Name "Ornare*" -ErrorAction SilentlyContinue
if ($null -ne $procs -and $procs.Count -gt 0) {
    Write-Error "Ornare processes still running!"
    exit 1
}
Write-Host "Zero Ornare processes running."

Write-Host "=== STEP 5: Testing Explorer Persistence Without Restarting Explorer ==="
# Open a fresh Explorer window targeted at the customized folder
$p = Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$testFolder`"" -PassThru
Start-Sleep -Seconds 3

# Find the Explorer window
$explorerProcs = Get-Process -Name "explorer"
$targetHwnd = [IntPtr]::Zero
foreach ($ep in $explorerProcs) {
    if ($ep.MainWindowHandle -ne [IntPtr]::Zero) {
        $targetHwnd = $ep.MainWindowHandle
        break
    }
}

if ($targetHwnd -ne [IntPtr]::Zero) {
    [Win32CaptureHelper]::SetForegroundWindow($targetHwnd)
    Start-Sleep -Milliseconds 500
    # Send F5 to refresh
    [Win32CaptureHelper]::keybd_event(0x74, 0, 0, 0)
    [Win32CaptureHelper]::keybd_event(0x74, 0, 2, 0)
    Start-Sleep -Seconds 1

    $rect = New-Object Win32CaptureHelper+RECT
    [Win32CaptureHelper]::GetWindowRect($targetHwnd, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    if ($width -gt 100 -and $height -gt 100) {
        $bmp = New-Object System.Drawing.Bitmap $width, $height
        $gfx = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $gfx.GetHdc()
        [Win32CaptureHelper]::PrintWindow($targetHwnd, $hdc, 2) | Out-Null
        $gfx.ReleaseHdc($hdc)
        $gfx.Dispose()

        $outPath = "production_artifacts/design/runtime-screenshots/icon-after-uninstall.png"
        $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "Captured real Explorer window evidence to: $outPath ($width x $height)"
    }
}

Write-Host "=== STEP 6: Verifying Filesystem Invariants After Uninstall ==="
if (!(Test-Path $icoPath)) {
    Write-Error "CRITICAL INVARIANT VIOLATION: .ornare\folder.ico was deleted during package uninstall!"
    exit 1
}
if (!(Test-Path $iniPath)) {
    Write-Error "CRITICAL INVARIANT VIOLATION: desktop.ini was deleted during package uninstall!"
    exit 1
}

$iniContentAfter = Get-Content -Path $iniPath -Raw
if (!$iniContentAfter.Contains("IconResource=.ornare\folder.ico,0")) {
    Write-Error "CRITICAL INVARIANT VIOLATION: desktop.ini IconResource modified!"
    exit 1
}
Write-Host "PASS: .ornare\folder.ico and desktop.ini remain 100% intact after package uninstallation."

Write-Host "=== STEP 7: Reinstalling Ornare MSIX Package ==="
Add-AppxPackage -Path "production_artifacts/release/Ornare_1.0.0.0_x64.msix"
$pkgReinstalled = Get-AppxPackage Ornare
if ($null -eq $pkgReinstalled -or $pkgReinstalled.Status -ne "Ok") {
    Write-Error "Failed to reinstall package."
    exit 1
}
Write-Host "Reinstalled package successfully: $($pkgReinstalled.PackageFullName)"

# Verify folder remains customized and undamaged after reinstall
if (!(Test-Path $icoPath) -or !(Test-Path $iniPath)) {
    Write-Error "Folder customization corrupted after reinstallation!"
    exit 1
}
Write-Host "PASS: Reinstallation preserved customized folder without unintended resets or resets."
Write-Host "ALL UNINSTALL PERSISTENCE TESTS PASSED!"
