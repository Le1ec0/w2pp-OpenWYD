param([Parameter(Mandatory=$true)][int]$StalledHelperId)
$ErrorActionPreference = 'Stop'
$target = 'C:\WYDCDK\Server\test-releases\20260924-v7670\WydCdk.Server-v7670-20260924'
$backup = 'C:\WYDCDK\Server\test-releases\backup-autotrade-close-e9e148a1fa4c4bc69ecab37a8c150af7'
$tasks = @('WYDCDK-Server-UP','WYDCDK-Server-PVP')
if ((Resolve-Path -LiteralPath $target).Path -ne $target -or (Resolve-Path -LiteralPath $backup).Path -ne $backup) { throw 'Recovery path mismatch.' }
if ((Get-FileHash -LiteralPath (Join-Path $backup 'WydCdk.Server.Core.dll')).Hash -ne 'AAB4BE2800AF1BF5C25CF0EEE6F7D11EF1EE314EE894D5621BBB5C7869450E71' -or
    (Get-FileHash -LiteralPath (Join-Path $backup 'WydCdk.World.dll')).Hash -ne 'E3267EF6AA5E824651E360DEEC3A5372E17FC3FEC008CA050FA749947AB069AD') { throw 'Prior runtime backup mismatch.' }
$files = @(Get-ChildItem -LiteralPath $backup -File)
foreach ($file in $files) {
    if ($file.Extension -notin '.dll','.exe' -and $file.Name -notin 'WydCdk.Server.Core.deps.json','WydCdk.Server.Core.runtimeconfig.json') { throw 'Non-runtime backup file.' }
}
$helper = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $StalledHelperId)
if (!$helper -or $helper.Name -ne 'powershell.exe' -or
    $helper.CommandLine -notlike '*stage-quittrade-crack-20260927-01/Apply.ps1*' -or
    $helper.CommandLine -notlike '*372DB837B8A17E85E96BECB61F3A4D8A58E8A43CD04D231BCDC941AC02D7F646*') { throw 'Stalled helper identity mismatch.' }
Stop-Process -Id $StalledHelperId -Force
if (Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 8281,8282 }) { throw 'Runtime already listening; refusing overwrite.' }
foreach ($task in $tasks) {
    $state = Get-ScheduledTask -TaskName $task
    if ($state.Actions.WorkingDirectory -ne 'C:\WYDCDK\Server\test-releases\20260924-v7670' -or $state.State -eq 'Running') { throw 'Recovery task mismatch or unexpectedly running.' }
}
foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name) -Force }
foreach ($file in $files) {
    if ((Get-FileHash -LiteralPath (Join-Path $target $file.Name)).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw 'Restored file hash mismatch.' }
}
foreach ($task in $tasks) { Start-ScheduledTask -TaskName $task }
'SANDBOX_RESTORE_PASS prior-runtime-files=' + $files.Count
