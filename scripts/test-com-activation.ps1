$t = [Type]::GetTypeFromCLSID([Guid]'4B1F8E1C-6C4D-4E10-9F1C-9E29DA21A77E')
Write-Host "Type resolved:" ($t -ne $null)
try {
    $o = [Activator]::CreateInstance($t)
    Write-Host "COM instance created:" ($o -ne $null)
    Write-Host "Type name:" $o.GetType().FullName
} catch {
    Write-Host "Activation error:" $_.Exception.Message
}
