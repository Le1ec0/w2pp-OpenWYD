param(
    [string]$Runtime = '',
    [switch]$SkipBuild,
    [switch]$DeadQuit,
    [switch]$Motion,
    [switch]$MotionNegativeHp,
    [switch]$TradeList,
    [switch]$TradeListNegativeHp,
    [switch]$Action,
    [switch]$ActionNegativeHp,
    [switch]$ActionRange,
    [switch]$ActionIllusionEligibility,
    [switch]$ActionIllusionMana,
    [switch]$AttackDeadGate,
    [switch]$AttackRvrGate,
    [switch]$AttackTimingGate,
    [switch]$ActionRecall,
    [switch]$MapRecall,
    [switch]$ActionAnalysis
)
$ErrorActionPreference = 'Stop'
if ($MotionNegativeHp) { $Motion = $true }
if ($TradeListNegativeHp) { $TradeList = $true }
if ($ActionNegativeHp) { $Action = $true }
if ($ActionRange) { $Action = $true }
if ($ActionIllusionEligibility) { $Action = $true }
if ($ActionIllusionMana) { $Action = $true }
if (($AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate) -and (($AttackDeadGate -and $AttackRvrGate) -or ($AttackDeadGate -and $AttackTimingGate) -or ($AttackRvrGate -and $AttackTimingGate) -or $DeadQuit -or $Motion -or $TradeList -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack gates are separate smoke modes.' }
if (($ActionRecall -or $MapRecall) -and ($ActionRecall -and $MapRecall -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionAnalysis -or $TradeList -or $Motion -or $DeadQuit)) { throw 'Recall modes are separate smoke modes.' }
if ($ActionAnalysis -and ($Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $TradeList -or $Motion -or $DeadQuit)) { throw 'Action account Analysis is a separate smoke mode.' }
if ($ActionAnalysis) { $Action = $true }
if ($ActionRange -and $ActionNegativeHp) { throw 'Action range and HP modes are separate.' }
if ($ActionIllusionEligibility -and ($ActionNegativeHp -or $ActionRange -or $ActionAnalysis)) { throw 'Action3 eligibility is a separate smoke mode.' }
if ($ActionIllusionMana -and ($ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionAnalysis)) { throw 'Action3 mana is a separate smoke mode.' }
if ($Action -and ($TradeList -or $Motion -or $DeadQuit)) { throw 'Action is a separate smoke mode.' }
if ($TradeList -and ($Motion -or $DeadQuit)) { throw 'ReqTradeList is a separate smoke mode.' }
if ($Motion -and $DeadQuit) { throw 'Motion and DeadQuit are separate smoke modes.' }
. (Join-Path $PSScriptRoot 'Complete-AutoTradeCloseEvidence.ps1')
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$serverRoot = Join-Path $projectRoot 'Server'

function Get-TcpListenerProcessIds([int]$LocalPort) {
    $pattern = '^\s*TCP\s+\S+:' + [regex]::Escape([string]$LocalPort) + '\s+\S+\s+LISTENING\s+(\d+)\s*$'
    $processIds = [Collections.Generic.List[int]]::new()
    foreach ($line in & netstat.exe -ano -p tcp) {
        if ($line -match $pattern) { $processIds.Add([int]$Matches[1]) }
    }
    return $processIds.ToArray()
}

$originalRoot = Join-Path $projectRoot 'Backup/Build/E2E7670/account/T'
$originals = @((Join-Path $originalRoot 'TESTE'), (Join-Path $originalRoot 'TESTE2'))
$hashes = @($originals | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash })
if (@(Get-TcpListenerProcessIds 18283).Count -gt 0) { throw 'Loopback port18283 is already occupied; no existing process will be stopped.' }
if (!$SkipBuild) {
    $wydCdkBuildInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $wydCdkBuildInfo.FileName = (Get-Command dotnet).Source
    $wydCdkBuildInfo.Arguments = 'build "' + (Join-Path $serverRoot 'WydCdk.Engine.slnx') + '" --no-restore -v quiet -m:1 -p:UseSharedCompilation=false'
    $wydCdkBuildInfo.WorkingDirectory = $serverRoot
    $wydCdkBuildInfo.UseShellExecute = $false
    $wydCdkBuildInfo.CreateNoWindow = $true
    $wydCdkBuildInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $wydCdkBuildInfo.RedirectStandardOutput = $true
    $wydCdkBuildInfo.RedirectStandardError = $true
    $wydCdkBuildProcess = [System.Diagnostics.Process]::new()
    $wydCdkBuildProcess.StartInfo = $wydCdkBuildInfo
    if (!$wydCdkBuildProcess.Start()) { throw 'Could not start the hidden smoke build process.' }
    $wydCdkBuildStdoutTask = $wydCdkBuildProcess.StandardOutput.ReadToEndAsync()
    $wydCdkBuildStderrTask = $wydCdkBuildProcess.StandardError.ReadToEndAsync()
    $wydCdkBuildProcess.WaitForExit()
    $wydCdkBuildExitCode = $wydCdkBuildProcess.ExitCode
    $wydCdkBuildStdout = $wydCdkBuildStdoutTask.GetAwaiter().GetResult()
    $wydCdkBuildStderr = $wydCdkBuildStderrTask.GetAwaiter().GetResult()
    $wydCdkBuildProcess.Dispose()
    if ($wydCdkBuildStdout) { Write-Output $wydCdkBuildStdout }
    if ($wydCdkBuildStderr) { Write-Output $wydCdkBuildStderr }
    if ($wydCdkBuildExitCode -ne 0) { throw 'Smoke build failed; build output is shown above.' }
}
if (!$Runtime) { $Runtime = Join-Path $serverRoot 'src/WydCdk.Server/bin/Debug/net10.0/WydCdk.Server.Core.dll' }
$Runtime = (Resolve-Path -LiteralPath $Runtime).Path
$runFamily = if ($DeadQuit) { 'Backup/Build/E2E7670/' } else { 'Backup/Build/AutoTradeCloseSmoke/' }
$runRoot = Join-Path $projectRoot ($runFamily + [Guid]::NewGuid().ToString('N'))
$accountRoot = Join-Path $runRoot 'account'
$accountReviewRoot = Join-Path $runRoot 'account-review'
$accountFolder = Join-Path $accountRoot 'T'
New-Item -ItemType Directory -Path $accountFolder | Out-Null
$actor = Join-Path $accountFolder 'TESTE2'
$peer = Join-Path $accountFolder 'TESTE'
$failureActor = Join-Path $accountFolder 'TESTE3'
$distantObserver = Join-Path $accountFolder 'TESTE4'
Copy-Item -LiteralPath $originals[0] -Destination $peer
Copy-Item -LiteralPath $originals[1] -Destination $actor
Copy-Item -LiteralPath $originals[1] -Destination $failureActor
Copy-Item -LiteralPath $originals[1] -Destination $distantObserver
# Runtime fixture preparation only. Original blobs are never written.
foreach ($path in @($actor, $peer, $failureActor, $distantObserver)) {
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 7945) { throw 'Unexpected account ABI.' }
    [Array]::Clear($bytes, 3480, 1024)
    [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $bytes, 216 + 28, 4)
    [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $bytes, 4504, 4)
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]1), 0, $bytes, $score, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $bytes, $score + 24, 4)
    }
    $bytes[7944] = 0
    [IO.File]::WriteAllBytes($path, $bytes)
}
$bytes = [IO.File]::ReadAllBytes($actor)
[Array]::Copy([BitConverter]::GetBytes([int16]900), 0, $bytes, 3480 + 4 * 8, 2)
[IO.File]::WriteAllBytes($actor, $bytes)
if ($DeadQuit) {
    [Array]::Clear($bytes, 216 + 268 + 7 * 8, 8)
    [Array]::Copy([BitConverter]::GetBytes([int16]900), 0, $bytes, 216 + 268 + 7 * 8, 2)
    [IO.File]::WriteAllBytes($actor, $bytes)
}
if ($Motion) {
    $motionHp = if ($MotionNegativeHp) { -1 } else { 0 }
    [Array]::Copy([BitConverter]::GetBytes([int]$motionHp), 0, $bytes, 216 + 92 + 24, 4)
    [IO.File]::WriteAllBytes($actor, $bytes)
}
if ($AttackDeadGate) {
    $bytes = [IO.File]::ReadAllBytes($actor)
    [Array]::Copy([BitConverter]::GetBytes([int]0), 0, $bytes, 216 + 92 + 24, 4)
    [IO.File]::WriteAllBytes($actor, $bytes)
}
if ($AttackRvrGate) {
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 16] = 0 # MOB.Clan: the source gate denies clanless attackers.
    [IO.File]::WriteAllBytes($actor, $bytes)
    $peerBytes = [IO.File]::ReadAllBytes($peer)
    $peerBytes[216 + 16] = 1 # The clanned observer is moved to the same position by the TCP smoke.
    [IO.File]::WriteAllBytes($peer, $peerBytes)
}
if ($TradeList) {
    $listHp = if ($TradeListNegativeHp) { -1 } else { 0 }
    [Array]::Copy([BitConverter]::GetBytes([int]$listHp), 0, $bytes, 216 + 92 + 24, 4)
    [IO.File]::WriteAllBytes($actor, $bytes)
    $ownerBytes = [IO.File]::ReadAllBytes($peer)
    [Array]::Copy([BitConverter]::GetBytes([int16]900), 0, $ownerBytes, 3480 + 4 * 8, 2)
    [IO.File]::WriteAllBytes($peer, $ownerBytes)
}
if ($Action) {
    $actionHp = if ($ActionAnalysis -or $ActionIllusionMana) { 1000 } elseif ($ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility) { -1 } else { 0 }
    [Array]::Copy([BitConverter]::GetBytes([int]$actionHp), 0, $bytes, 216 + 92 + 24, 4)
    [Array]::Copy([BitConverter]::GetBytes([int16]2100), 0, $bytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2100), 0, $bytes, 216 + 42, 2)
    if ($ActionRange -or $ActionIllusionMana) {
        $bytes[216 + 20] = 3 # Keep the range fixture eligible for MSG_Action3.
        [Array]::Copy([BitConverter]::GetBytes([int]2), 0, $bytes, 216 + 780, 4) # LearnedSkill bit 2.
    }
    if ($ActionRange -or $ActionIllusionEligibility) {
        foreach ($score in @(216 + 92)) {
            [Array]::Copy([BitConverter]::GetBytes([int]10000), 0, $bytes, $score + 20, 4)
            [Array]::Copy([BitConverter]::GetBytes([int]10000), 0, $bytes, $score + 28, 4)
        }
    }
    if ($ActionIllusionMana) {
        [Array]::Copy([BitConverter]::GetBytes([int]90), 0, $bytes, 216 + 92 + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]90), 0, $bytes, 216 + 92 + 28, 4)
    }
    [IO.File]::WriteAllBytes($actor, $bytes)
    $peerBytes = [IO.File]::ReadAllBytes($peer)
    [Array]::Copy([BitConverter]::GetBytes([int16]2101), 0, $peerBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2100), 0, $peerBytes, 216 + 42, 2)
    [IO.File]::WriteAllBytes($peer, $peerBytes)
}
if ($ActionIllusionEligibility) {
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 20] = 2 # STRUCT_MOB.Class: invalid for Illusion.
    [Array]::Clear($bytes, 216 + 780, 4) # STRUCT_MOB.LearnedSkill lacks bit 2.
    [Array]::Copy([BitConverter]::GetBytes([int]398), 0, $bytes, 216 + 92, 4) # Below MAX_LEVEL 399.
    [IO.File]::WriteAllBytes($actor, $bytes)
}
if ($ActionRecall -or $MapRecall) {
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 16] = 0 # STRUCT_MOB.Clan: enter the 7.69 no-clan restricted-sector gate.
    $originX = if ($MapRecall) { 2063 } else { 1664 }
    $originY = if ($MapRecall) { 2100 } else { 1920 }
    [Array]::Copy([BitConverter]::GetBytes([int16]$originX), 0, $bytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]$originY), 0, $bytes, 216 + 42, 2)
    if ($MapRecall) {
        [Array]::Copy([BitConverter]::GetBytes([int]35), 0, $bytes, 216 + 44, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]35), 0, $bytes, 216 + 92, 4)
    }
    [IO.File]::WriteAllBytes($actor, $bytes)
    $peerBytes = [IO.File]::ReadAllBytes($peer)
    $peerBytes[216 + 16] = 1 # Keep the nearby observer in sector (13,15).
    [Array]::Copy([BitConverter]::GetBytes([int16]$originX), 0, $peerBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]$originY), 0, $peerBytes, 216 + 42, 2)
    [IO.File]::WriteAllBytes($peer, $peerBytes)
    $destinationBytes = [IO.File]::ReadAllBytes($failureActor)
    $destinationBytes[216 + 16] = 1
    $destinationX = if ($MapRecall) { 2093 } else { 2112 }
    $destinationY = if ($MapRecall) { 2100 } else { 2042 }
    [Array]::Copy([BitConverter]::GetBytes([int16]$destinationX), 0, $destinationBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]$destinationY), 0, $destinationBytes, 216 + 42, 2)
    [IO.File]::WriteAllBytes($failureActor, $destinationBytes)
    $distantBytes = [IO.File]::ReadAllBytes($distantObserver)
    $distantBytes[216 + 16] = 1
    $distantX = if ($MapRecall) { 1400 } else { 2100 }
    $distantY = if ($MapRecall) { 1400 } else { 2100 }
    [Array]::Copy([BitConverter]::GetBytes([int16]$distantX), 0, $distantBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]$distantY), 0, $distantBytes, 216 + 42, 2)
    [IO.File]::WriteAllBytes($distantObserver, $distantBytes)
    foreach ($dropperPath in @($actor, $failureActor)) {
        $dropperBytes = [IO.File]::ReadAllBytes($dropperPath)
        $carryItemOffset = 216 + 284 # STRUCT_ACCOUNTINFO + STRUCT_MOB.Carry[0] in the active W2PP-compatible fixture ABI.
        [Array]::Clear($dropperBytes, $carryItemOffset, 12)
        [Array]::Copy([BitConverter]::GetBytes([int16]500), 0, $dropperBytes, $carryItemOffset, 2)
        [IO.File]::WriteAllBytes($dropperPath, $dropperBytes)
    }
    foreach ($identity in @(
        @{ Path = $actor; Name = 'TESTE2'; Password = 'SmokeA2!' },
        @{ Path = $peer; Name = 'TESTE'; Password = 'Control1!' },
        @{ Path = $failureActor; Name = 'TESTE3'; Password = 'SmokeA3!' },
        @{ Path = $distantObserver; Name = 'TESTE4'; Password = 'SmokeA4!' }
    )) {
        $identityBytes = [IO.File]::ReadAllBytes($identity.Path)
        [Array]::Clear($identityBytes, 0, 28)
        $nameBytes = [Text.Encoding]::ASCII.GetBytes($identity.Name)
        $passwordBytes = [Text.Encoding]::ASCII.GetBytes($identity.Password)
        if ($nameBytes.Length -gt 15 -or $passwordBytes.Length -gt 11) { throw 'Synthetic account header exceeds legacy field limits.' }
        [Array]::Copy($nameBytes, 0, $identityBytes, 0, $nameBytes.Length)
        [Array]::Copy($passwordBytes, 0, $identityBytes, 16, $passwordBytes.Length)
        [IO.File]::WriteAllBytes($identity.Path, $identityBytes)
    }
}
if ($ActionAnalysis) { Copy-Item -LiteralPath $actor -Destination $failureActor -Force }
if ($ActionAnalysis) {
    foreach ($identity in @(
        @{ Path = $actor; Name = 'TESTE2'; Password = 'SmokeA2!' },
        @{ Path = $peer; Name = 'TESTE'; Password = 'Control1!' },
        @{ Path = $failureActor; Name = 'TESTE3'; Password = 'SmokeA3!' }
    )) {
        $identityBytes = [IO.File]::ReadAllBytes($identity.Path)
        [Array]::Clear($identityBytes, 0, 28)
        $nameBytes = [Text.Encoding]::ASCII.GetBytes($identity.Name)
        $passwordBytes = [Text.Encoding]::ASCII.GetBytes($identity.Password)
        if ($nameBytes.Length -gt 15 -or $passwordBytes.Length -gt 11) { throw 'Synthetic account header exceeds legacy field limits.' }
        [Array]::Copy($nameBytes, 0, $identityBytes, 0, $nameBytes.Length)
        [Array]::Copy($passwordBytes, 0, $identityBytes, 16, $passwordBytes.Length)
        [IO.File]::WriteAllBytes($identity.Path, $identityBytes)
    }
    $peerBytes = [IO.File]::ReadAllBytes($peer)
    $peerBytes[216 + 92 + 13] = [byte]255 # STRUCT_MOB.CurrentScore.AttackRun in the isolated slot-zero fixture.
    [IO.File]::WriteAllBytes($peer, $peerBytes)
}
$state = Join-Path $runRoot 'autotrade.json'
$heightMapPath = Join-Path $runRoot 'recall-heightmap.dat'
$attributeMapPath = Join-Path $runRoot 'recall-attributemap.dat'
if ($MapRecall) {
    $heightMap = [IO.File]::Open($heightMapPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $heightMap.SetLength(16777216) } finally { $heightMap.Dispose() }
    $attributeMap = [IO.File]::Open($attributeMapPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $attributeMap.SetLength(1048576)
        $targetX = 2064
        $targetY = 2100
        $attributeIndex = (($targetY -shr 2) * 1024) + ($targetX -shr 2)
        $attributeMap.Position = $attributeIndex
        $attributeMap.WriteByte(0x80)
    } finally { $attributeMap.Dispose() }
}
$stdout = Join-Path $runRoot 'server.stdout.log'
$stderr = Join-Path $runRoot 'server.stderr.log'
$clientDll = Join-Path $serverRoot 'tests/WydCdk.Client.E2E/bin/Debug/net10.0/WydCdk.Client.E2E.dll'
$runtimeRoot = Split-Path -Parent $Runtime
$artifactPaths = @($Runtime, (Join-Path $runtimeRoot 'WydCdk.World.dll'), (Join-Path $runtimeRoot 'WydCdk.Protocol.dll'),
    $clientDll, (Join-Path (Split-Path -Parent $clientDll) 'WydCdk.Protocol.dll'), $PSCommandPath,
    (Join-Path $PSScriptRoot 'Complete-AutoTradeCloseEvidence.ps1'))
if ($ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate) {
    $artifactPaths += Join-Path $projectRoot 'Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv'
}
$artifacts = @($artifactPaths | ForEach-Object { [pscustomobject]@{ Path=$_; Hash=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
$e2eExit = -1
$assertionsPassed = $false
$process = $null
$serverLogs = [Collections.Generic.List[string]]::new()

function ConvertTo-WindowsQuotedArgument([string]$Argument) {
    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $slashes = 0
    for ($index = 0; $index -lt $Argument.Length; $index++) {
        $character = $Argument[$index]
        if ($character -eq '\') {
            $slashes++
        } elseif ($character -eq '"') {
            [void]$builder.Append(('\' * (($slashes * 2) + 1)))
            [void]$builder.Append('"')
            $slashes = 0
        } else {
            [void]$builder.Append(('\' * $slashes))
            [void]$builder.Append($character)
            $slashes = 0
        }
    }
    [void]$builder.Append(('\' * ($slashes * 2)))
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Start-LoggedChildProcess([string]$Executable, [string[]]$Arguments, [string]$StdoutPath, [string]$StderrPath) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.WorkingDirectory = $runRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = [string]::Join(' ', @($Arguments | ForEach-Object { ConvertTo-WindowsQuotedArgument $_ }))
    $child = [System.Diagnostics.Process]::new()
    $child.StartInfo = $startInfo
    if (!$child.Start()) { throw 'Could not start the owned smoke child process.' }
    return [pscustomobject]@{
        Process = $child
        StdoutTask = $child.StandardOutput.ReadToEndAsync()
        StderrTask = $child.StandardError.ReadToEndAsync()
        StdoutPath = $StdoutPath
        StderrPath = $StderrPath
    }
}

function Save-LoggedChildOutput($ChildRecord) {
    if (!$ChildRecord.Process.WaitForExit(10000)) { throw "Child process $($ChildRecord.Process.Id) did not finish its output streams." }
    $encoding = [System.Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($ChildRecord.StdoutPath, $ChildRecord.StdoutTask.GetAwaiter().GetResult(), $encoding)
    [IO.File]::WriteAllText($ChildRecord.StderrPath, $ChildRecord.StderrTask.GetAwaiter().GetResult(), $encoding)
}

function Start-OwnedSmokeHost([string]$Phase, [switch]$IncludeReviewRoot) {
    $arguments = @('--bind','127.0.0.1','--port','18283','--accounts-root',$accountRoot,
        '--autotrade-state',$state,'--item-data',(Join-Path $projectRoot 'Client/7670/ItemList.bin'))
    if ($ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate) {
        $arguments += @('--skill-data',(Join-Path $projectRoot 'Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv'))
    }
    if ($MapRecall) { $arguments += @('--heightmap',$heightMapPath,'--attribute-map',$attributeMapPath) }
    if ($IncludeReviewRoot) { $arguments += @('--account-review-root',$accountReviewRoot) }
    $executable = $Runtime
    if ([IO.Path]::GetExtension($Runtime) -eq '.dll') {
        $executable = (Get-Command dotnet).Source
        $arguments = @($Runtime) + $arguments
    }
    $phaseStdout = Join-Path $runRoot ($Phase + '.stdout.log')
    $phaseStderr = Join-Path $runRoot ($Phase + '.stderr.log')
    $serverLogs.Add($phaseStdout)
    $serverLogs.Add($phaseStderr)
    $script:ownedHost = Start-LoggedChildProcess $executable $arguments $phaseStdout $phaseStderr
    $started = $script:ownedHost.Process
    $script:process = $started
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while (!(@(Get-TcpListenerProcessIds 18283) -contains $started.Id)) {
        $started.Refresh()
        if ($started.HasExited) { throw "Owned smoke host exited before readiness in phase $Phase; inspect retained logs." }
        if ([DateTime]::UtcNow -gt $deadline) { throw "Owned smoke listener did not start in phase $Phase." }
        Start-Sleep -Milliseconds 200
    }
}

function Stop-OwnedSmokeHost {
    if ($script:process) {
        $script:process.Refresh()
        if (!$script:process.HasExited) {
            Stop-Process -Id $script:process.Id
            if (!$script:process.WaitForExit(10000)) { throw 'Owned smoke host did not stop within ten seconds.' }
        }
        Save-LoggedChildOutput $script:ownedHost
        $ownedId = $script:process.Id
        $script:process = $null
        $script:ownedHost = $null
        if (@(Get-TcpListenerProcessIds 18283).Count -gt 0) {
            throw "Loopback port 18283 remains occupied after stopping owned PID $ownedId."
        }
    }
}

function Invoke-AccountAnalysisE2e([string]$AccountFile, [string]$LogPath, [switch]$Thresholds, [switch]$Mark, [switch]$WriteFailure, [switch]$Login, [switch]$ReviewUnavailable) {
    $arguments = @('--host','127.0.0.1','--port','18283','--account-file',$AccountFile,
        '--account-review-peer-account-file',$peer,'--account-review-root',$accountReviewRoot,'--timeout-seconds','15')
    if ($Mark) { $arguments += '--action-account-analysis-smoke' }
    if ($Thresholds) { $arguments += '--action-account-analysis-threshold-smoke' }
    if ($WriteFailure) { $arguments += '--expect-review-write-failure' }
    if ($Login) { $arguments += '--account-analysis-login-smoke' }
    if ($ReviewUnavailable) { $arguments += '--account-review-unavailable-login-smoke' }
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet $clientDll @arguments 2>&1 | Tee-Object -FilePath $LogPath
        $phaseExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = 'Stop' }
    if ($phaseExit -ne 0) { throw "Account Analysis E2E failed; retained log: $LogPath" }
}

try {
    if ($ActionAnalysis) {
        $missingRootOut = Join-Path $runRoot 'missing-review-root.stdout.log'
        $missingRootErr = Join-Path $runRoot 'missing-review-root.stderr.log'
        $missingArgs = @('--bind','127.0.0.1','--port','18283','--accounts-root',$accountRoot,
            '--autotrade-state',$state,'--item-data',(Join-Path $projectRoot 'Client/7670/ItemList.bin'))
        $missingExe = $Runtime
        if ([IO.Path]::GetExtension($Runtime) -eq '.dll') {
            $missingExe = (Get-Command dotnet).Source
            $missingArgs = @($Runtime) + $missingArgs
        }
        $serverLogs.Add($missingRootOut)
        $serverLogs.Add($missingRootErr)
        $missingRun = Start-LoggedChildProcess $missingExe $missingArgs $missingRootOut $missingRootErr
        $missing = $missingRun.Process
        if (!$missing.WaitForExit(30000)) {
            $missing.Refresh()
            if (!$missing.HasExited) { Stop-Process -Id $missing.Id; $missing.WaitForExit(10000) | Out-Null }
            Save-LoggedChildOutput $missingRun
            throw 'Authenticated host did not reject startup within 30 seconds without --account-review-root.'
        }
        Save-LoggedChildOutput $missingRun
        $missing.Refresh()
        $missingText = (Get-Content -LiteralPath $missingRootOut -Raw -ErrorAction SilentlyContinue) +
            (Get-Content -LiteralPath $missingRootErr -Raw -ErrorAction SilentlyContinue)
        if ($missing.ExitCode -eq 0 -or $missingText -notmatch '--account-review-root is required whenever account authentication is enabled') {
            throw 'Authenticated host did not fail closed with the required review-root startup error.'
        }
        if (@(Get-TcpListenerProcessIds 18283).Count -gt 0) {
            throw 'Missing-review-root startup unexpectedly left a listener on port 18283.'
        }
        'ACCOUNT_REVIEW_REQUIRED_ROOT_STARTUP_PASS'

        Start-OwnedSmokeHost 'analysis-first-run' -IncludeReviewRoot
        Invoke-AccountAnalysisE2e $actor (Join-Path $runRoot 'client-analysis-thresholds.log') -Thresholds
        Invoke-AccountAnalysisE2e $failureActor (Join-Path $runRoot 'client-write-failure.log') -Mark -WriteFailure
        Invoke-AccountAnalysisE2e $actor (Join-Path $runRoot 'client-analysis-mark.log') -Mark
        Stop-OwnedSmokeHost

        $analysisRecord = Join-Path $accountReviewRoot 'TESTE2.review.json'
        if (!(Test-Path -LiteralPath $analysisRecord -PathType Leaf)) { throw 'Durable review record is missing before controlled restart.' }
        Start-OwnedSmokeHost 'analysis-after-restart' -IncludeReviewRoot
        Invoke-AccountAnalysisE2e $actor (Join-Path $runRoot 'client-analysis-relogin.log') -Login
        $heldReviewRoot = Join-Path $runRoot 'account-review-held'
        if (Test-Path -LiteralPath $heldReviewRoot) { throw 'The isolated held-review path already exists.' }
        [IO.Directory]::Move($accountReviewRoot, $heldReviewRoot)
        try {
            Invoke-AccountAnalysisE2e $actor (Join-Path $runRoot 'client-review-read-failure.log') -ReviewUnavailable
        } finally {
            if (Test-Path -LiteralPath $heldReviewRoot) {
                if (Test-Path -LiteralPath $accountReviewRoot) { throw 'Refusing to overwrite a review root recreated during the unavailable-store smoke.' }
                [IO.Directory]::Move($heldReviewRoot, $accountReviewRoot)
            }
        }
        Stop-OwnedSmokeHost

        $clientPasses = @('client-analysis-thresholds.log','client-write-failure.log','client-analysis-mark.log','client-analysis-relogin.log','client-review-read-failure.log') |
            ForEach-Object { Get-Content -LiteralPath (Join-Path $runRoot $_) -Raw }
        if (($clientPasses -join "`n") -notmatch 'ACCOUNT_ANALYSIS_THRESHOLDS_PASS' -or
            ($clientPasses -join "`n") -notmatch 'ACCOUNT_REVIEW_WRITE_FAILURE_PASS' -or
            ($clientPasses -join "`n") -notmatch 'ACCOUNT_ANALYSIS_MARK_PASS' -or
            ($clientPasses -join "`n") -notmatch 'ACCOUNT_ANALYSIS_RELOGIN_PASS' -or
            ($clientPasses -join "`n") -notmatch 'ACCOUNT_REVIEW_READ_FAILURE_PASS') { throw 'One or more Account Analysis E2E phases lacks its pass marker.' }
        'ACCOUNT_ANALYSIS_NETWORK_PASS'
        $e2eExit = 0
        $assertionsPassed = $true
    } else {
    $stdout = Join-Path $runRoot 'main.stdout.log'
    $stderr = Join-Path $runRoot 'main.stderr.log'
    Start-OwnedSmokeHost 'main' -IncludeReviewRoot
    $process = $script:process
    $clientArgs = @('--host','127.0.0.1','--port','18283','--account-file',$actor,'--timeout-seconds','15')
    if ($DeadQuit) { $clientArgs += @('--trade-quit-peer-account-file',$peer,'--delete-slot','7','--trade-quit-dead') }
    else { $clientArgs += @('--autotrade-close-peer-account-file',$peer,'--autotrade-state-file',$state) }
    if ($AttackDeadGate) { $clientArgs += '--attack-dead-gate-smoke' }
    if ($AttackRvrGate) { $clientArgs += '--attack-rvr-gate-smoke' }
    if ($AttackTimingGate) { $clientArgs += '--attack-timing-gate-smoke' }
    if ($Motion) { $clientArgs += '--motion-gate-smoke' }
    if ($MotionNegativeHp) { $clientArgs += '--motion-negative-hp' }
    if ($TradeList) { $clientArgs += '--trade-list-gate-smoke' }
    if ($TradeListNegativeHp) { $clientArgs += '--trade-list-negative-hp' }
    if ($Action) { $clientArgs += '--action-gate-smoke' }
    if ($ActionNegativeHp) { $clientArgs += '--action-negative-hp' }
    if ($ActionRange) { $clientArgs += '--action-range-smoke' }
    if ($ActionIllusionEligibility) { $clientArgs += '--action-illusion-eligibility-smoke' }
    if ($ActionIllusionMana) { $clientArgs += '--action-illusion-mana-smoke' }
    if ($MapRecall) {
        $clientArgs += @('--movement-restriction-recall-smoke',
            '--action-recall-destination-observer-account-file',$failureActor,
            '--action-recall-distant-observer-account-file',$distantObserver)
    } elseif ($ActionRecall) {
        $clientArgs += @('--action-origin-recall-smoke',
            '--action-recall-destination-observer-account-file',$failureActor,
            '--action-recall-distant-observer-account-file',$distantObserver)
    }
    try {
        $ErrorActionPreference = 'Continue' # Preserve native stderr too, then check its exit explicitly.
        & dotnet $clientDll @clientArgs 2>&1 | Tee-Object -FilePath (Join-Path $runRoot 'client.log')
        $e2eExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = 'Stop' }
    Stop-OwnedSmokeHost
    if ($e2eExit -ne 0) { throw 'Autotrade network assertions failed; evidence retained.' }
    $crackLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=10, type=17, total=' })
    if ($ActionRecall -or $MapRecall) {
        if ($MapRecall) {
            $recallLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Movement recalled: account=.*?, reason=NewbieZone, from=\(2063,2100\), to=\(\d+,\d+\)\.$' })
            $expectedMarker = '^CLIENT_E2E_PASS movement-map-recall from=\(2063,2100\) to=\(\d+,\d+\) actor-origin-destination-distant$'
            if ($recallLines.Count -ne 1) { throw 'Movement map-gate recall host log is absent, duplicated, or has the wrong source position/reason.' }
            if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' }).Count -ne 0) { throw 'Movement map restriction recall incorrectly incremented the crack-error counter.' }
            if (@(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match $expectedMarker }).Count -ne 1) { throw 'Movement restriction recall E2E pass marker is missing.' }
            'MOVEMENT_MAP_RECALL_NETWORK_PASS'
        } else {
        $recallLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Action origin recalled: account=.*?, from=\(1664,1920\), to=\(\d+,\d+\)\.$' })
        if ($recallLines.Count -ne 1) { throw 'Origin recall host log is absent, duplicated, or has the wrong source sector.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' }).Count -ne 0) { throw 'Origin recall incorrectly incremented the crack-error counter.' }
        if (@(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^CLIENT_E2E_PASS action-origin-recall from=\(1664,1920\) to=\(\d+,\d+\) actor-origin-destination-distant$' }).Count -ne 1) { throw 'Action origin recall E2E pass marker is missing.' }
        'ACTION_ORIGIN_RECALL_NETWORK_PASS'
        }
    } elseif ($ActionIllusionMana) {
        $illusionTickLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=1, type=104, total=' })
        $quitLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=10, type=17, total=' })
        $allCrackLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' })
        if ($illusionTickLines.Count -ne 1 -or $illusionTickLines[0] -notmatch 'total=1\.$' -or
            $quitLines.Count -ne 2 -or $quitLines[0] -notmatch 'total=10\.$' -or $quitLines[1] -notmatch 'total=20\.$' -or
            $allCrackLines.Count -ne 3) { throw 'Action3 mana-before-tick counter mismatch.' }
        $actorMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ILLUSION_MANA_ACTOR_ID=\d+$' })
        if ($actorMarkers.Count -ne 1) { throw 'Action3 mana actor identity missing.' }
        $actorId = [int]($actorMarkers[0] -replace '^ILLUSION_MANA_ACTOR_ID=', '')
        if ($illusionTickLines[0] -notmatch ('connection=' + $actorId + ', ')) { throw 'Action3 tick counter belongs to another session.' }
        if (@(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^CLIENT_E2E_PASS illusion-mana fixed-skill73 cost-before-tick reqmp-unchanged insufficient-sync private-sync persisted-relogin$' }).Count -ne 1) { throw 'Action3 mana E2E pass marker missing.' }
        'ACTION_ILLUSION_MANA_NETWORK_PASS'
    } elseif ($ActionIllusionEligibility) {
        $illusionEligibilityLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=10, type=28, total=' })
        $allCrackLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' })
        if ($illusionEligibilityLines.Count -ne 1 -or $illusionEligibilityLines[0] -notmatch 'total=10\.$' -or
            $crackLines.Count -ne 3 -or $crackLines[0] -notmatch 'total=10\.$' -or $crackLines[1] -notmatch 'total=20\.$' -or $crackLines[2] -notmatch 'total=20\.$' -or
            $allCrackLines.Count -ne 4) { throw 'Action3 eligibility/shared-counter totals mismatch.' }
        $actorMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ILLUSION_ELIGIBILITY_ACTOR_ID=\d+$' })
        if ($actorMarkers.Count -ne 1) { throw 'Action3 eligibility actor identity missing.' }
        $actorId = [int]($actorMarkers[0] -replace '^ILLUSION_ELIGIBILITY_ACTOR_ID=', '')
        if ($illusionEligibilityLines[0] -notmatch ('connection=' + $actorId + ', ') -or $crackLines[2] -notmatch ('connection=' + $actorId + ', ')) { throw 'Action3 eligibility counter belongs to another session.' }
        if (@(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^CLIENT_E2E_PASS illusion-eligibility type28 fallthrough-relay shared-counter hp-carry-cargo-gold$' }).Count -ne 1) { throw 'Action3 eligibility E2E pass marker missing.' }
        'ACTION_ILLUSION_ELIGIBILITY_NETWORK_PASS'
    } elseif ($Action) {
        $totals = @(10, 20, $(if ($ActionRange) { 77 } elseif ($ActionNegativeHp) { 10 } else { 25 }), 10)
        $tickLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=1, type=102, total=' })
        $illusionTickLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=1, type=104, total=' })
        $rangeLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=1, type=5, total=' })
        $speedLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=5, type=4, total=' })
        $stopLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=1, type=101, total=1\.' })
        $tickCount = if ($ActionRange) { 2 } else { 0 }
        $illusionTickCount = if ($ActionRange) { 2 } else { 0 }
        $rangeCount = if ($ActionRange) { 43 } else { 0 }
        $speedCount = if ($ActionRange) { 4 } else { 0 }
        $stopCount = if ($ActionRange) { 0 } else { 1 }
        if ($crackLines.Count -ne 4 -or $tickLines.Count -ne $tickCount -or $illusionTickLines.Count -ne $illusionTickCount -or $rangeLines.Count -ne $rangeCount -or $speedLines.Count -ne $speedCount -or
            $stopLines.Count -ne $stopCount -or
            @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' }).Count -ne (4 + $tickCount + $illusionTickCount + $rangeCount + $speedCount + $stopCount)) { throw 'Action suppressed/shared counters mismatch.' }
        $actorMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ACTION_ACTOR_ID=\d+$' })
        $peerMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ACTION_PEER_ID=\d+$' })
        $reloginMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ACTION_RELOGIN_ID=\d+$' })
        if ($actorMarkers.Count -ne 1 -or $reloginMarkers.Count -ne 1 -or ($stopCount -eq 1 -and $peerMarkers.Count -ne 1)) { throw 'Action session identities missing.' }
        $actorId = [int]($actorMarkers[0] -replace '^ACTION_ACTOR_ID=', '')
        $reloginId = [int]($reloginMarkers[0] -replace '^ACTION_RELOGIN_ID=', '')
        if ($stopCount -eq 1) {
            $peerId = [int]($peerMarkers[0] -replace '^ACTION_PEER_ID=', '')
            if ($stopLines[0] -notmatch ('connection=' + $peerId + ', ') -or $stopLines[0] -notmatch 'total=1\.$') { throw 'Consecutive Action2 did not add exactly 1/type101 to the peer session.' }
        }
        for ($i = 0; $i -lt $tickCount; $i++) {
            if ($tickLines[$i] -notmatch ('connection=' + $actorId + ', ') -or $tickLines[$i] -notmatch ('total=' + ($i + 1) + '\.$')) { throw 'Action tick rejection did not add exactly 1/type102 to the actor session.' }
        }
        for ($i = 0; $i -lt $illusionTickCount; $i++) {
            if ($illusionTickLines[$i] -notmatch ('connection=' + $actorId + ', ') -or $illusionTickLines[$i] -notmatch ('total=' + ($tickCount + $i + 1) + '\.$')) { throw 'Action3 tick rejection did not add exactly 1/type104 to the actor session.' }
        }
        for ($i = 0; $i -lt $rangeCount; $i++) {
            if ($rangeLines[$i] -notmatch ('connection=' + $actorId + ', ') -or $rangeLines[$i] -notmatch ('total=' + (24 + $i + 1) + '\.$')) { throw 'Action range counter total or identity mismatch.' }
        }
        $expectedSpeedTotals = if ($ActionRange) { @(9, 14, 19, 24) } else { @(5, 10, 15, 20) }
        for ($i = 0; $i -lt $speedCount; $i++) {
            if ($speedLines[$i] -notmatch ('connection=' + $actorId + ', ') -or $speedLines[$i] -notmatch ('total=' + $expectedSpeedTotals[$i] + '\.$')) { throw 'Action speed counter total or identity mismatch.' }
        }
        for ($i = 0; $i -lt 4; $i++) {
            $expectedId = if ($i -lt 3) { $actorId } else { $reloginId }
            if ($crackLines[$i] -notmatch ('connection=' + $expectedId + ', ') -or $crackLines[$i] -notmatch ('total=' + $totals[$i] + '\.$')) { throw 'Action counter total or session identity mismatch.' }
        }
        'ACTION_NETWORK_PASS'
    } elseif ($TradeList) {
        $listLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=10, type=87, total=' })
        $listCount = if ($TradeListNegativeHp) { 2 } else { 4 }
        $quitTotal = if ($TradeListNegativeHp) { 10 } else { 30 }
        if ($listLines.Count -ne $listCount -or $listLines[0] -notmatch 'total=10\.$' -or $listLines[1] -notmatch 'total=20\.$' -or
            (!$TradeListNegativeHp -and ($listLines[2] -notmatch 'total=10\.$' -or $listLines[3] -notmatch 'total=20\.$')) -or
            $crackLines.Count -ne 1 -or $crackLines[0] -notmatch ('total=' + $quitTotal + '\.$')) { throw 'ReqTradeList/shared QuitTrade counters mismatch.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' }).Count -ne ($listCount + 1)) { throw 'Accepted ReqTradeList gained crack points.' }
        $actorMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^LIST_ACTOR_ID=\d+$' })
        if ($actorMarkers.Count -ne 1) { throw 'ReqTradeList actor identity missing.' }
        $actorId = [int]($actorMarkers[0] -replace '^LIST_ACTOR_ID=', '')
        foreach ($line in @($listLines) + @($crackLines)) {
            if ($line -notmatch ('connection=' + $actorId + ', ')) { throw 'Crack increment belongs to another session.' }
        }
        'TRADE_LIST_NETWORK_PASS'
    } elseif ($Motion) {
        $motionLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=4, type=6, total=' })
        $motionCount = if ($MotionNegativeHp) { 2 } else { 3 }
        $quitTotal = if ($MotionNegativeHp) { 10 } else { 14 }
        if ($motionLines.Count -ne $motionCount -or $motionLines[0] -notmatch 'total=4\.$' -or $motionLines[1] -notmatch 'total=8\.$' -or
            (!$MotionNegativeHp -and $motionLines[2] -notmatch 'total=4\.$') -or
            $crackLines.Count -ne 1 -or $crackLines[0] -notmatch ('total=' + $quitTotal + '\.$')) { throw 'Motion/shared QuitTrade counters mismatch.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' }).Count -ne ($motionCount + 1)) { throw 'Unexpected crack increment in accepted Motion.' }
        'MOTION_NETWORK_PASS'
    } elseif ($AttackDeadGate) {
        $attackLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error: connection=\d+, points=10, type=17, total=' })
        $actorMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ATTACK_DEAD_ACTOR_ID=\d+$' })
        if ($attackLines.Count -ne 1 -or $attackLines[0] -notmatch 'total=11\.$' -or $crackLines.Count -ne 1 -or $actorMarkers.Count -ne 1) {
            throw 'Dead Attack type-8 increment/shared counter mismatch; expected suppressed +1 followed by type17 total11.'
        }
        $actorId = [int]($actorMarkers[0] -replace '^ATTACK_DEAD_ACTOR_ID=', '')
        if ($attackLines[0] -notmatch ('connection=' + $actorId + ', ')) { throw 'Dead Attack shared counter belongs to another session.' }
        if (@(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-dead-gate hp-zero-private-hpmode-no-relay-type8-shared-counter$' }).Count -ne 1) {
            throw 'Dead Attack private wire/no-relay E2E pass marker is missing.'
        }
        'ATTACK_DEAD_GATE_NETWORK_PASS'
    } elseif ($AttackRvrGate) {
        $actorMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ATTACK_RVR_ACTOR_ID=\d+$' })
        if ($crackLines.Count -ne 0 -or $actorMarkers.Count -ne 1) { throw 'RVR Attack gate must not increase the shared crack counter and must report one actor.' }
        if (@(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-rvr-private-notice-before-dead-gate-no-relay$' }).Count -ne 1) {
            throw 'RVR Attack private notice/no-relay E2E pass marker is missing.'
        }
        'ATTACK_RVR_GATE_NETWORK_PASS'
    } elseif ($AttackTimingGate) {
        $allCrackLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' })
        $timingLines = @($allCrackLines | Where-Object { $_ -match 'Crack error: connection=\d+, points=1, type=107, total=' })
        $backstepLines = @($allCrackLines | Where-Object { $_ -match 'Crack error: connection=\d+, points=4, type=7, total=6\.$' })
        $rejectedTooSoon = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Rejected attack: account=.*?, skill=-1, reason=TooSoon\.$' })
        $rejectedOutside = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Rejected attack: account=.*?, skill=-1, reason=OutsideServerWindow\.$' })
        $rejectedInvalidSkill = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Rejected attack: account=.*?, skill=-1, reason=InvalidSkillId\.$' })
        $actorMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ATTACK_TIMING_ACTOR_ID=\d+$' })
        $peerMarkers = @(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^ATTACK_TIMING_PEER_ID=\d+$' })
        if ($timingLines.Count -ne 3 -or $backstepLines.Count -ne 1 -or $allCrackLines.Count -ne 4 -or
            $rejectedTooSoon.Count -ne 1 -or $rejectedOutside.Count -ne 2 -or $rejectedInvalidSkill.Count -ne 2 -or
            $actorMarkers.Count -ne 1 -or $peerMarkers.Count -ne 1) {
            throw 'Attack timing must reject two invalid skills, one TooSoon, two OutsideServerWindow packets, and record one wrap backstep type7.'
        }
        $actorId = [int]($actorMarkers[0] -replace '^ATTACK_TIMING_ACTOR_ID=', '')
        $peerId = [int]($peerMarkers[0] -replace '^ATTACK_TIMING_PEER_ID=', '')
        $actorIncrements = @($timingLines | Where-Object { $_ -match ('Crack error: connection=' + $actorId + ', points=1, type=107, total=') })
        $peerIncrements = @($timingLines | Where-Object { $_ -match ('Crack error: connection=' + $peerId + ', points=1, type=107, total=') })
        if ($actorId -eq $peerId -or $actorIncrements.Count -ne 2 -or $peerIncrements.Count -ne 1 -or
            $actorIncrements[0] -notmatch 'total=1\.$' -or $actorIncrements[1] -notmatch 'total=2\.$' -or
            $peerIncrements[0] -notmatch 'total=1\.$' -or
            $backstepLines[0] -notmatch ('connection=' + $actorId + ', ')) {
            throw 'Attack timing 1/type107 and 4/type7 increments do not match the actor/peer counters.'
        }
        if (@(Get-Content -LiteralPath (Join-Path $runRoot 'client.log') | Where-Object { $_ -match '^CLIENT_E2E_READY attack-timing-too-soon-outside-window-and-wrap-backstep$' }).Count -ne 1) {
            throw 'Attack timing wire smoke completion marker is missing.'
        }
        'ATTACK_TIMING_GATE_NETWORK_PASS'
    } elseif ($DeadQuit) {
        if ($crackLines.Count -ne 1 -or $crackLines[0] -notmatch 'total=10\.$') { throw 'Dead QuitTrade crack increment missing or duplicated.' }
    } else {
        if ($crackLines.Count -ne 2 -or $crackLines[0] -notmatch 'total=10\.$' -or $crackLines[1] -notmatch 'total=20\.$') { throw 'Nonplaying QuitTrade increments missing, or accepted QuitTrade gained points.' }
    }
    $assertionsPassed = $true
    'CRACK_NETWORK_PASS'
    'AUTOTRADE_NETWORK_PASS'
    }
}
finally {
    $manifest = [pscustomobject]@{ Version=1; Mode=$(if ($MapRecall) { 'movement-map-recall' } elseif ($ActionRecall) { 'action-origin-recall' } elseif ($ActionAnalysis) { 'action-account-analysis' } elseif ($ActionIllusionMana) { 'action-illusion-mana' } elseif ($ActionIllusionEligibility) { 'action-illusion-eligibility' } elseif ($ActionRange) { 'action-range' } elseif ($ActionNegativeHp) { 'action-negative-hp' } elseif ($Action) { 'action' } elseif ($TradeListNegativeHp) { 'trade-list-negative-hp' } elseif ($TradeList) { 'trade-list' } elseif ($MotionNegativeHp) { 'motion-negative-hp' } elseif ($Motion) { 'motion' } elseif ($AttackTimingGate) { 'attack-timing-gate' } elseif ($AttackRvrGate) { 'attack-rvr-gate' } elseif ($AttackDeadGate) { 'attack-dead-gate' } elseif ($DeadQuit) { 'dead-quit' } else { 'autotrade-close' }); ExitCode=$e2eExit; AssertionsPassed=$assertionsPassed; Artifacts=$artifacts; OriginalFixtureHashes=$hashes; ServerLogs=@($serverLogs) }
    if ($AttackDeadGate) { $manifest.Mode = 'attack-dead-gate' }
    try {
        Complete-AutoTradeCloseEvidence -ManifestPath (Join-Path $runRoot 'evidence.json') -Manifest $manifest -Cleanup {
            if ($script:ownedHost) { Stop-OwnedSmokeHost }
            elseif ($process -and !$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit(10000) | Out-Null }
            for ($i = 0; $i -lt $originals.Count; $i++) {
                if ((Get-FileHash -LiteralPath $originals[$i] -Algorithm SHA256).Hash -ne $hashes[$i]) { throw 'Original account hash changed.' }
            }
            if (@(Get-TcpListenerProcessIds 18283).Count -gt 0) { throw 'Port18283 remains occupied after owned cleanup.' }
            'ORIGINAL_HASHES_INTACT PORT18283_RELEASED'
        }
    } finally { "EVIDENCE=$runRoot" }
}
