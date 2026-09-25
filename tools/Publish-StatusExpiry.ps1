[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$StatusFile,

    [string]$HeartbeatDirectory,

    [ValidateRange(1, 100)]
    [int]$SlotCount = 10,

    [ValidateRange(30, 3600)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$StatusFile = [IO.Path]::GetFullPath($StatusFile)
if ([string]::IsNullOrWhiteSpace($HeartbeatDirectory)) {
    $HeartbeatDirectory = Join-Path ([IO.Path]::GetDirectoryName($StatusFile)) '.wyd-status'
}
$HeartbeatDirectory = [IO.Path]::GetFullPath($HeartbeatDirectory)
$lock = $null

try {
    $lockPath = "$StatusFile.lock"
    for ($attempt = 0; $attempt -lt 20 -and $null -eq $lock; $attempt++) {
        try {
            $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        } catch [IO.IOException] {
            Start-Sleep -Milliseconds 100
        }
    }
    if ($null -eq $lock) {
        throw "Could not acquire status-file lock: $lockPath"
    }

    $values = @(-1) * $SlotCount
    $values[0] = 1
    if (Test-Path -LiteralPath $StatusFile) {
        $raw = [IO.File]::ReadAllText($StatusFile)
        $lines = if ($raw.Contains('\n', [StringComparison]::Ordinal)) {
            $raw -split '\\n' | Where-Object { $_ -ne '' }
        } else {
            $raw -split "`r?`n" | Where-Object { $_ -ne '' }
        }
        for ($index = 0; $index -lt [Math]::Min($lines.Count, $SlotCount); $index++) {
            $parsed = 0
            if ([int]::TryParse($lines[$index].Trim(), [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
                $values[$index] = $parsed
            }
        }
    }

    $now = [DateTimeOffset]::UtcNow
    $changed = $false
    for ($slot = 1; $slot -lt $SlotCount; $slot++) {
        $heartbeat = Join-Path $HeartbeatDirectory ("slot-{0}.heartbeat" -f $slot)
        $stale = $true
        if (Test-Path -LiteralPath $heartbeat) {
            $age = $now - (Get-Item -LiteralPath $heartbeat).LastWriteTimeUtc
            $stale = $age.TotalSeconds -gt $TimeoutSeconds
        }

        if ($stale -and $values[$slot] -ne -1) {
            $values[$slot] = -1
            $changed = $true
        }
    }

    if ($changed -or -not (Test-Path -LiteralPath $StatusFile)) {
        $directory = [IO.Path]::GetDirectoryName($StatusFile)
        [IO.Directory]::CreateDirectory($directory) | Out-Null
        $temporaryPath = "$StatusFile.$PID.watchdog.tmp"
        [IO.File]::WriteAllText($temporaryPath, (($values -join '\n') + '\n'), [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $StatusFile) {
            Remove-Item -LiteralPath $StatusFile -Force
        }
        Move-Item -LiteralPath $temporaryPath -Destination $StatusFile -Force
        Write-Output "Status expiry updated: $StatusFile"
    }
} finally {
    if ($null -ne $lock) {
        $lock.Dispose()
    }
}
