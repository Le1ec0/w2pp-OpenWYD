param(
    [Parameter(Mandatory=$true)][string]$ZipHash,
    [Parameter(Mandatory=$true)][string]$CoreHash,
    [Parameter(Mandatory=$true)][string]$WorldHash,
    [Parameter(Mandatory=$true)][string]$ProtocolHash
)
$ErrorActionPreference = 'Stop'
$target = 'C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924'
$tasks = @('WYDCDK-Server-UP','WYDCDK-Server-PVP')
$archive = Join-Path $PSScriptRoot 'runtime.zip'
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $ZipHash) { throw 'Archive hash mismatch.' }
if ((Resolve-Path -LiteralPath $target).Path -ne $target) { throw 'Unexpected sandbox target.' }
foreach ($taskName in $tasks) {
    $task = Get-ScheduledTask -TaskName $taskName
    if ($task.Actions.WorkingDirectory -ne 'C:\WYDCDK\Server\test-releases\20260924-v7670') { throw 'Unexpected task working directory.' }
}
$listeners = @(Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -in 8281,8282 })
if ($listeners.Count -ne 2) { throw 'Expected exactly two sandbox listeners before deploy.' }
$oldIds = @($listeners.OwningProcess | Select-Object -Unique)
foreach ($pidValue in $oldIds) {
    $old = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $pidValue)
    if ($old.ExecutablePath -ne (Join-Path $target 'WydCdk.Server.Core.exe')) { throw 'Listener executable differs from approved sandbox target.' }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -ne $entry.Name -or $entry.Name -match '\.\.' -or
            ([IO.Path]::GetExtension($entry.Name) -notin '.dll','.exe' -and $entry.Name -notin 'WydCdk.Server.Core.deps.json','WydCdk.Server.Core.runtimeconfig.json')) {
            throw 'Archive contains non-runtime or nested files.'
        }
    }
} finally { $zip.Dispose() }
$source = Join-Path $PSScriptRoot ('runtime-' + [Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $archive -DestinationPath $source
$expected = @{ 'WydCdk.Server.Core.dll'=$CoreHash; 'WydCdk.World.dll'=$WorldHash; 'WydCdk.Protocol.dll'=$ProtocolHash }
foreach ($name in $expected.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $source $name) -Algorithm SHA256).Hash -ne $expected[$name]) { throw 'Extracted runtime hash mismatch.' }
}
$files = @(Get-ChildItem -LiteralPath $source -File)
$backup = 'C:\WYDCDK\Server\test-releases\backup-autotrade-close-' + [Guid]::NewGuid().ToString('N')
New-Item -ItemType Directory -Path $backup | Out-Null
$added = @()
foreach ($file in $files) {
    $destination = Join-Path $target $file.Name
    if (Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $destination -Destination (Join-Path $backup $file.Name) }
    else { $added += $destination }
}
$stopped = $false
try {
    $stopped = $true
    'SANDBOX_STOP_TASKS'
    foreach ($taskName in $tasks) { Stop-ScheduledTask -TaskName $taskName }
    foreach ($pidValue in $oldIds) {
        $old = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $pidValue)
        if ($old -and $old.ExecutablePath -eq (Join-Path $target 'WydCdk.Server.Core.exe')) { Stop-Process -Id $pidValue -Force }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 8281,8282 }) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Sandbox listeners did not stop.' }
        Start-Sleep -Milliseconds 200
    }
    'SANDBOX_COPY_RUNTIME'
    foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name) -Force }
    foreach ($file in $files) {
        if ((Get-FileHash -LiteralPath (Join-Path $target $file.Name) -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw 'Installed file hash mismatch.' }
    }
} catch {
    foreach ($file in Get-ChildItem -LiteralPath $backup -File) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name) -Force }
    foreach ($path in $added) {
        if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($path)) -ne $target) { throw 'Rollback path outside validated target.' }
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    throw
} finally {
    if ($stopped) { foreach ($taskName in $tasks) { Start-ScheduledTask -TaskName $taskName } }
}
$deadline = [DateTime]::UtcNow.AddSeconds(30)
do {
    $ports = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 8281,8282 })
    if ($ports.Count -eq 2) { break }
    if ([DateTime]::UtcNow -gt $deadline) { throw 'Updated sandbox listeners did not become ready; backup retained.' }
    Start-Sleep -Milliseconds 200
} while ($true)
foreach ($port in $ports) {
    $running = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $port.OwningProcess)
    if ($running.ExecutablePath -ne (Join-Path $target 'WydCdk.Server.Core.exe')) { throw 'Updated listener executable mismatch.' }
}
foreach ($taskName in $tasks) { if ((Get-ScheduledTask -TaskName $taskName).State -ne 'Running') { throw 'Updated sandbox task is not Running.' } }
"SANDBOX_DEPLOY_PASS files=$($files.Count) backup=$backup"
$ports | Select-Object LocalAddress,LocalPort,OwningProcess | ConvertTo-Json -Compress
foreach ($name in $expected.Keys) { "$name=$((Get-FileHash -LiteralPath (Join-Path $target $name) -Algorithm SHA256).Hash)" }
