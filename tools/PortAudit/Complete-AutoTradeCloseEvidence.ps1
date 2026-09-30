function Complete-AutoTradeCloseEvidence {
    param([Parameter(Mandatory=$true)][scriptblock]$Cleanup,
        [Parameter(Mandatory=$true)][string]$ManifestPath,
        [Parameter(Mandatory=$true)][object]$Manifest)
    $completed = $false
    try {
        & $Cleanup
        $completed = $true
    } finally {
        $Manifest | Add-Member -NotePropertyName CleanupCompleted -NotePropertyValue $completed -Force
        [IO.File]::WriteAllText($ManifestPath, ($Manifest | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))
    }
}
