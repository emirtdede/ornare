# Test calling FolderCustomizer.ApplyIcon via compiled Ornare.Windows.dll
$testFolder = "C:\Users\emir\Desktop\TestFolderForIcon"
if (Test-Path $testFolder) {
    Remove-Item $testFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $testFolder -Force | Out-Null
Write-Host "Created test folder: $testFolder"

$appDir = "src\Ornare.App\bin\Debug\net10.0-windows10.0.26100.0"
Add-Type -Path (Join-Path $appDir "Ornare.Core.dll")
Add-Type -Path (Join-Path $appDir "Ornare.Windows.dll")

$customizer = New-Object Ornare.Windows.FolderCustomizer
$sourceIco = "src\Ornare.App\Assets\app.ico"

Write-Host "Calling ApplyIcon..."
$result = $customizer.ApplyIcon($testFolder, (Resolve-Path $sourceIco).Path)
Write-Host "ApplyIcon IsSuccess:" $result.IsSuccess
if (!$result.IsSuccess) {
    Write-Host "Error:" $result.ErrorMessage
}

# Check icon index
& powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check-icon-index.ps1 -Path $testFolder
