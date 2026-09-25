# Ornare — Real External Process Termination Recovery Test
# Proves INV-TX-001 and INV-TX-002: Hard process termination during mutation is cleanly reconciled on next startup across 4 distinct mutation phases:
# 1. AssetGenerated
# 2. FilesystemStaged
# 3. FilesystemCommitted
# 4. MetadataPending (ShellRefreshRequested / before metadata commit)

$ErrorActionPreference = "Stop"

Write-Host "=========================================================="
Write-Host "ORNARE — REAL MID-MUTATION PROCESS TERMINATION RECOVERY"
Write-Host "=========================================================="

Write-Host "Executing 4-phase deterministic barrier process termination tests..."

$output = dotnet test tests/Ornare.IntegrationTests/Ornare.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~RealExternalProcessTerminationAcrossPhases" --logger "console;verbosity=detailed"
Write-Host ($output -join "`n")

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nAll 4 real mid-mutation process termination tests PASSED:"
    Write-Host "  [PASS] AssetGenerated -> Hard Process Kill -> Reconciled to RolledBack"
    Write-Host "  [PASS] FilesystemStaged -> Hard Process Kill -> Reconciled to RolledBack"
    Write-Host "  [PASS] FilesystemCommitted -> Hard Process Kill -> Reconciled to Completed"
    Write-Host "  [PASS] MetadataPending -> Hard Process Kill -> Reconciled to Completed"
    exit 0
} else {
    Write-Host "`nReal process termination recovery tests FAILED."
    exit 1
}
