param()
$ErrorActionPreference = 'Stop'

Write-Host 'Ornare prerequisite check'
$commands = @('git', 'dotnet', 'winget')
foreach ($cmd in $commands) {
    $found = Get-Command $cmd -ErrorAction SilentlyContinue
    if ($null -eq $found) { Write-Error "Missing required command: $cmd" }
    Write-Host ("OK  {0}: {1}" -f $cmd, $found.Source)
}

dotnet --info

$winapp = Get-Command winapp -ErrorAction SilentlyContinue
if ($null -eq $winapp) {
    Write-Warning 'winapp CLI not found. It is a preview helper; install when needed with winget install Microsoft.winappcli --source winget.'
} else {
    Write-Host ("OK  winapp: {0}" -f $winapp.Source)
}

Write-Host 'Visual Studio C++/WinUI workloads and Windows SDK must also be verified from the actual build; this script does not mutate the machine.'
