# Ornare — Generate Package Assets for MSIX
# Creates StoreLogo.png, Square150x150Logo.png, Square44x44Logo.png

param(
    [string]$OutputDir = "src/Ornare.Package/Images"
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

Add-Type -AssemblyName System.Drawing

function Create-Logo([int]$size, [string]$fileName) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Accent color background rounded rectangle or diamond
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 30, 32))
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $g.FillRectangle($brush, $rect)

    # Stylized minimalist folder outline in Ornare accent blue
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 96, 165, 250)), ([Math]::Max(1.0, $size / 16.0))
    $margin = [int]($size * 0.22)
    $w = $size - (2 * $margin)
    $h = [int]($w * 0.75)
    $y = [int](($size - $h) / 2)
    $g.DrawRectangle($pen, $margin, $y, $w, $h)

    $outPath = Join-Path $OutputDir $fileName
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $pen.Dispose()
    $brush.Dispose()
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "Created asset: $outPath ($size x $size)"
}

Create-Logo 50 "StoreLogo.png"
Create-Logo 150 "Square150x150Logo.png"
Create-Logo 44 "Square44x44Logo.png"

Write-Host "Package assets generated successfully."
