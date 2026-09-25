[CmdletBinding()]
param(
    [string]$ServerRoot = 'C:\WYDCDK\Server',
    [string]$SiteRoot = 'C:\WYDCDK\Site',
    [string]$StagingRoot = 'C:\WYDCDK\Deploy\status-20260918',
    [string]$BackupRoot = 'C:\WYDCDK\Deploy\status-backup-20260918'
)

$ErrorActionPreference = 'Stop'
$statusFile = Join-Path $SiteRoot 'servtest.htm'
$watchdogSource = Join-Path $StagingRoot 'Publish-StatusExpiry.ps1'
$watchdogTarget = Join-Path $ServerRoot 'tools\Publish-StatusExpiry.ps1'

if (-not (Test-Path -LiteralPath $watchdogSource)) {
    throw "Watchdog not found in staging: $watchdogSource"
}

New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $ServerRoot 'tools') | Out-Null

Stop-ScheduledTask -TaskName 'WYDCDK-Server-UP' -ErrorAction SilentlyContinue
Stop-ScheduledTask -TaskName 'WYDCDK-Server-PVP' -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

foreach ($name in @(
        'WydCdk.Server.Core.exe',
        'WydCdk.Server.Core.dll',
        'WydCdk.Server.Core.deps.json',
        'WydCdk.Server.Core.runtimeconfig.json',
        'WydCdk.Protocol.dll',
        'WydCdk.World.dll',
        'MySqlConnector.dll',
        'BCrypt.Net-Next.dll')) {
    $current = Join-Path $ServerRoot $name
    if (Test-Path -LiteralPath $current) {
        Copy-Item -LiteralPath $current -Destination (Join-Path $BackupRoot $name) -Force
    }
}

Copy-Item -Path (Join-Path $StagingRoot '*') -Destination $ServerRoot -Force
Copy-Item -LiteralPath $watchdogSource -Destination $watchdogTarget -Force

$upAction = New-ScheduledTaskAction -Execute (Join-Path $ServerRoot 'WydCdk.Server.Core.exe') -Argument "--bind 0.0.0.0 --port 8281 --world UP --mariadb-account-config $ServerRoot\secrets\site-db.json --status-file $statusFile --status-slot 1" -WorkingDirectory $ServerRoot
$pvpAction = New-ScheduledTaskAction -Execute (Join-Path $ServerRoot 'WydCdk.Server.Core.exe') -Argument "--bind 0.0.0.0 --port 8282 --world PVP --mariadb-account-config $ServerRoot\secrets\site-db.json --status-file $statusFile --status-slot 2" -WorkingDirectory $ServerRoot
Set-ScheduledTask -TaskName 'WYDCDK-Server-UP' -Action $upAction | Out-Null
Set-ScheduledTask -TaskName 'WYDCDK-Server-PVP' -Action $pvpAction | Out-Null

$watchdogAction = New-ScheduledTaskAction -Execute 'PowerShell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$watchdogTarget`" -StatusFile `"$statusFile`" -TimeoutSeconds 120" -WorkingDirectory $ServerRoot
$watchdogTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1) -RepetitionDuration (New-TimeSpan -Days 3650)
Register-ScheduledTask -TaskName 'WYDCDK-StatusExpiry' -Action $watchdogAction -Trigger $watchdogTrigger -User 'SYSTEM' -RunLevel Highest -Force | Out-Null

if (-not (Test-Path -LiteralPath $statusFile)) {
    [IO.Directory]::CreateDirectory($SiteRoot) | Out-Null
    [IO.File]::WriteAllText($statusFile, ((@(1) + (@(-1) * 9)) -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
}

Start-ScheduledTask -TaskName 'WYDCDK-Server-UP'
Start-ScheduledTask -TaskName 'WYDCDK-Server-PVP'
& PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File $watchdogTarget -StatusFile $statusFile -TimeoutSeconds 120

Write-Output "Status publishing installed. Backup: $BackupRoot"
