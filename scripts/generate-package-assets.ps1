# Ornare - Generate Package Assets for MSIX
# Runs Ornare.AssetGenerator to produce complete Windows 11 & Microsoft Store visual asset suite
param(
    [string]$OutputDir = "src/Ornare.Package/Images"
)

$ErrorActionPreference = "Stop"

Write-Host "Running Ornare.AssetGenerator..."
dotnet run --project tools/Ornare.AssetGenerator/Ornare.AssetGenerator.csproj

Write-Host "Package assets generated successfully."
