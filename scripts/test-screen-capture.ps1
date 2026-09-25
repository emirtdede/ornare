Add-Type -AssemblyName System.Drawing, System.Windows.Forms
$screen = [System.Windows.Forms.Screen]::PrimaryScreen
Write-Host "PrimaryScreen Bounds: $($screen.Bounds.Width) x $($screen.Bounds.Height)"
try {
    $bmp = New-Object System.Drawing.Bitmap $screen.Bounds.Width, $screen.Bounds.Height
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen(0, 0, 0, 0, (New-Object System.Drawing.Size($screen.Bounds.Width, $screen.Bounds.Height)))
    $gfx.Dispose()
    Write-Host "CopyFromScreen succeeded!"
    $out = "production_artifacts/design/runtime-screenshots/test-screen.png"
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Saved to $out"
} catch {
    Write-Host "CopyFromScreen failed: $($_.Exception.Message)"
}
