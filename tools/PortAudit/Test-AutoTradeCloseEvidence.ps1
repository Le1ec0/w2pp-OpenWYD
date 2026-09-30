param([switch]$SkipBuild, [switch]$CleanupFailureOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Complete-AutoTradeCloseEvidence.ps1')
if ($CleanupFailureOnly) {
    $projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
    $root = Join-Path $projectRoot ('Backup/Build/AutoTradeCloseSmoke/cleanup-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root | Out-Null
    $manifestPath = Join-Path $root 'evidence.json'
    $caught = $false
    try {
        Complete-AutoTradeCloseEvidence -Cleanup { throw 'isolated-cleanup-fault' } -ManifestPath $manifestPath -Manifest ([pscustomobject]@{ Version=1; ExitCode=-7; Artifacts=@() })
    } catch { $caught = $_.Exception.Message -eq 'isolated-cleanup-fault' }
    if (!$caught -or !(Test-Path -LiteralPath $manifestPath)) { throw 'Cleanup fault suppressed the manifest or original failure.' }
    $saved = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($saved.ExitCode -ne -7 -or $saved.CleanupCompleted) { throw 'Cleanup failure falsely recorded success or lost native exit.' }
    "CLEANUP_FAILURE_EVIDENCE_PASS root=$root"
    return
}
$output = @(& (Join-Path $PSScriptRoot 'Test-AutoTradeClose.ps1') -SkipBuild:$SkipBuild)
$output | Write-Output
$line = $output | Where-Object { $_ -is [string] -and $_.StartsWith('EVIDENCE=') } | Select-Object -Last 1
if (!$line) { throw 'Runner did not report an evidence directory.' }
$root = $line.Substring(9)
$manifestPath = Join-Path $root 'evidence.json'
if (!(Test-Path -LiteralPath $manifestPath)) { throw 'Smoke evidence lacks runtime/harness hash manifest.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.ExitCode -ne 0 -or !$manifest.AssertionsPassed -or !$manifest.CleanupCompleted) { throw 'Evidence falsely reports a successful run.' }
foreach ($artifact in $manifest.Artifacts) {
    if ((Get-FileHash -LiteralPath $artifact.Path -Algorithm SHA256).Hash -ne $artifact.Hash) { throw 'Executed artifact hash differs from evidence.' }
}
if ($manifest.Artifacts.Count -lt 4) { throw 'Evidence omitted runtime or harness artifacts.' }
$clientLog = Get-Content -LiteralPath (Join-Path $root 'client.log') -Raw
if ($clientLog -notmatch 'CLIENT_E2E_PASS autotrade-open-query-close' -or $clientLog -notmatch 'SHOP_RX') { throw 'Evidence omitted packet observations or result.' }
if ($clientLog -match '(?i)password=|numericToken=|loginPayload=') { throw 'Sensitive authentication content in smoke output.' }
'EVIDENCE_GUARD_PASS runtime-harness-hashes packet-output result'
