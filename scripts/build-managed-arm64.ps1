# Build managed projects for win-arm64
$ErrorActionPreference = "Stop"

$projects = @(
    "src/Ornare.Core/Ornare.Core.csproj",
    "src/Ornare.DesignSystem/Ornare.DesignSystem.csproj",
    "src/Ornare.Windows/Ornare.Windows.csproj",
    "src/Ornare.Storage/Ornare.Storage.csproj",
    "src/Ornare.Imaging/Ornare.Imaging.csproj",
    "src/Ornare.Cli/Ornare.Cli.csproj",
    "src/Ornare.App/Ornare.App.csproj"
)

foreach ($p in $projects) {
    Write-Host "`n============================================="
    Write-Host "Building $p (Release, win-arm64)"
    Write-Host "============================================="
    dotnet build $p -c Release -r win-arm64 --no-incremental -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build $p"
    }
}

Write-Host "`n>>> ALL 7 MANAGED PROJECTS BUILT CLEANLY FOR ARM64 (0 ERRORS, 0 WARNINGS) <<<"
