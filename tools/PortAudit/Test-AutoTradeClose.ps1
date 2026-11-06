param(
    [string]$Runtime = '',
    [switch]$SkipBuild,
    [int]$DoubleModeMultiplier = 1,
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
    [switch]$AttackSkillGate,
    [switch]$AttackPartySkillGate,
    [switch]$AttackGoldenShieldCoin,
    [switch]$AttackDoubleCritical,
    [switch]$AttackManaControl,
    [switch]$AttackManaGain,
    [switch]$AttackSkillHeal,
    [switch]$AttackSummonNotice,
    [switch]$AttackDivineBlessing,
    [switch]$AutoDoubleMagicTrumpet,
    [switch]$AttackMultiTarget,
    [switch]$AttackMixedSlots,
    [switch]$AttackType6Protection,
    [switch]$ActionRecall,
    [switch]$MapRecall,
    [switch]$ActionAnalysis
)
$ErrorActionPreference = 'Stop'
if ($AttackSummonNotice -and ($DeadQuit -or $Motion -or $MotionNegativeHp -or $TradeList -or $TradeListNegativeHp -or
    $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or
    $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $AttackSkillGate -or $AttackPartySkillGate -or
    $AttackGoldenShieldCoin -or $AttackDoubleCritical -or $AttackManaControl -or $AttackManaGain -or $AttackSkillHeal -or
    $AttackDivineBlessing -or $AutoDoubleMagicTrumpet -or $AttackMultiTarget -or $AttackMixedSlots -or $AttackType6Protection -or
    $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'InstanceType 9 summon notice is a separate Attack smoke mode.' }
if ($AttackDivineBlessing -and ($DeadQuit -or $Motion -or $MotionNegativeHp -or $TradeList -or $TradeListNegativeHp -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $AttackSkillGate -or $AttackPartySkillGate -or $AttackGoldenShieldCoin -or $AttackDoubleCritical -or $AttackManaControl -or $AttackManaGain -or $AttackSkillHeal -or $AttackMultiTarget -or $AttackMixedSlots -or $AttackType6Protection -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Divine Blessing is a separate Attack smoke mode.' }
if ($AttackMixedSlots -and ($DeadQuit -or $Motion -or $MotionNegativeHp -or $TradeList -or $TradeListNegativeHp -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $AttackSkillGate -or $AttackPartySkillGate -or $AttackGoldenShieldCoin -or $AttackDoubleCritical -or $AttackManaControl -or $AttackManaGain -or $AttackSkillHeal -or $AttackMultiTarget -or $AttackType6Protection -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Mixed-slot Attack is a separate smoke mode.' }
if ($MotionNegativeHp) { $Motion = $true }
if ($TradeListNegativeHp) { $TradeList = $true }
if ($ActionNegativeHp) { $Action = $true }
if ($ActionRange) { $Action = $true }
if ($ActionIllusionEligibility) { $Action = $true }
if ($ActionIllusionMana) { $Action = $true }
if ($AttackType6Protection -and ($DeadQuit -or $Motion -or $TradeList -or $Action -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $AttackSkillGate -or $AttackDoubleCritical -or $AttackManaControl -or $AttackManaGain -or $AttackSkillHeal -or $AttackMultiTarget -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'MSG_Attack Type 6 protection is a separate smoke mode.' }
if ($AttackSkillHeal -and ($AttackManaGain -or $AttackSkillGate -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $DeadQuit -or $Motion -or $TradeList -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack gates are separate smoke modes.' }
if ($AttackDoubleCritical -and ($AttackSkillHeal -or $AttackManaGain -or $AttackSkillGate -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $DeadQuit -or $Motion -or $TradeList -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack DoubleCritical is a separate smoke mode.' }
if ($AttackManaControl -and ($AttackDoubleCritical -or $AttackManaGain -or $AttackSkillHeal -or $AttackSkillGate -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $DeadQuit -or $Motion -or $TradeList -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack ManaControl is a separate smoke mode.' }
if ($AttackManaGain -and ($AttackSkillGate -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $DeadQuit -or $Motion -or $TradeList -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack gates are separate smoke modes.' }
if ($AttackSkillGate -and ($AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $DeadQuit -or $Motion -or $TradeList -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack gates are separate smoke modes.' }
if ($AttackPartySkillGate -and ($DeadQuit -or $Motion -or $TradeList -or $Action -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $AttackSkillGate -or $AttackGoldenShieldCoin -or $AttackDoubleCritical -or $AttackManaControl -or $AttackManaGain -or $AttackSkillHeal -or $AttackMultiTarget -or $AttackType6Protection -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack party-skill gate is a separate smoke mode.' }
if ($AttackGoldenShieldCoin -and ($DeadQuit -or $Motion -or $TradeList -or $Action -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $AttackSkillGate -or $AttackDoubleCritical -or $AttackManaControl -or $AttackManaGain -or $AttackSkillHeal -or $AttackMultiTarget -or $AttackType6Protection -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Golden Shield coin is a separate Attack smoke mode.' }
if ($AttackMultiTarget -and ($AttackSkillHeal -or $AttackManaGain -or $AttackManaControl -or $AttackDoubleCritical -or $AttackSkillGate -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $DeadQuit -or $Motion -or $TradeList -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack multi-target is a separate smoke mode.' }
if (($AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate) -and (($AttackDeadGate -and $AttackRvrGate) -or ($AttackDeadGate -and $AttackTimingGate) -or ($AttackRvrGate -and $AttackTimingGate) -or $DeadQuit -or $Motion -or $TradeList -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $ActionRecall -or $MapRecall -or $ActionAnalysis)) { throw 'Attack gates are separate smoke modes.' }
if (($ActionRecall -or $MapRecall) -and ($ActionRecall -and $MapRecall -or $Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $ActionAnalysis -or $TradeList -or $Motion -or $DeadQuit)) { throw 'Recall modes are separate smoke modes.' }
if ($ActionAnalysis -and ($Action -or $ActionNegativeHp -or $ActionRange -or $ActionIllusionEligibility -or $TradeList -or $Motion -or $DeadQuit)) { throw 'Action account Analysis is a separate smoke mode.' }
if ($ActionAnalysis) { $Action = $true }
if ($AutoDoubleMagicTrumpet) {
    $otherSmokeModes = @('DeadQuit','Motion','MotionNegativeHp','TradeList','TradeListNegativeHp','Action','ActionNegativeHp','ActionRange',
        'ActionIllusionEligibility','ActionIllusionMana','AttackDeadGate','AttackRvrGate','AttackTimingGate','AttackSkillGate','AttackPartySkillGate',
        'AttackGoldenShieldCoin','AttackDoubleCritical','AttackManaControl','AttackManaGain','AttackSkillHeal','AttackSummonNotice','AttackDivineBlessing',
        'AttackMultiTarget','AttackMixedSlots','AttackType6Protection','ActionRecall','MapRecall','ActionAnalysis')
    $selectedOtherModes = @($otherSmokeModes | Where-Object { [bool]$PSBoundParameters[$_] })
    if ($selectedOtherModes.Count -gt 0) { throw 'AutoDouble MagicTrumpet is a separate loopback smoke mode.' }
}
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
$partyNpcGenerationPath = Join-Path $runRoot 'party-skill-npc-generation.txt'
$partyNpcRoot = Join-Path $runRoot 'party-skill-npc-templates'
$partyNpcTemplatePath = Join-Path $partyNpcRoot 'ATTACK_PARTY_NPC'
$partySummonRoot = Join-Path $projectRoot 'Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/TMSrv/run/BaseSummon'
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
if ($AttackDoubleCritical -or $AttackMultiTarget -or $AttackType6Protection) {
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 16] = 1 # Keep the actor clanned in the isolated combat fixture.
    $bytes[216 + 20] = 3 # STRUCT_MOB.Class: class-3 passive DoubleCritical.
    [Array]::Copy([BitConverter]::GetBytes([int]0x200000), 0, $bytes, 216 + 780, 4)
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $bytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $bytes, 216 + 42, 2)
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]1), 0, $bytes, $score, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]0), 0, $bytes, $score + 4, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $bytes, $score + 8, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 16, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 24, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 28, 4)
        [Array]::Copy([BitConverter]::GetBytes([int16]1000), 0, $bytes, $score + 32, 2)
        [Array]::Copy([BitConverter]::GetBytes([int16]1000), 0, $bytes, $score + 46, 2)
    }
    [IO.File]::WriteAllBytes($actor, $bytes)

    $peerBytes = [IO.File]::ReadAllBytes($peer)
    $peerBytes[216 + 16] = 1
    [Array]::Copy([BitConverter]::GetBytes([int16]2005), 0, $peerBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2005), 0, $peerBytes, 216 + 42, 2)
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]1), 0, $peerBytes, $score, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]10), 0, $peerBytes, $score + 4, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]0), 0, $peerBytes, $score + 8, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]100000), 0, $peerBytes, $score + 16, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $peerBytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]100000), 0, $peerBytes, $score + 24, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $peerBytes, $score + 28, 4)
    }
    [IO.File]::WriteAllBytes($peer, $peerBytes)
    if ($AttackMultiTarget -or $AttackType6Protection) {
        # Keep this relay regression independent of the randomized 7.69 miss roll:
        # attacker Dex 1001 vs target Dex 1000 yields the literal 0% branch.
        $actorBytes = [IO.File]::ReadAllBytes($actor)
        [Array]::Clear($actorBytes, 216 + 22, 2) # STRUCT_MOB.Rsv precision bonuses.
        [Array]::Copy([BitConverter]::GetBytes([int16]1001), 0, $actorBytes, 216 + 92 + 36, 2)
        [IO.File]::WriteAllBytes($actor, $actorBytes)

        $peerBytes = [IO.File]::ReadAllBytes($peer)
        [Array]::Copy([BitConverter]::GetBytes([int16]1000), 0, $peerBytes, 216 + 92 + 36, 2)
        [IO.File]::WriteAllBytes($peer, $peerBytes)
    }
}
if ($AttackType6Protection) {
    $peerBytes = [IO.File]::ReadAllBytes($peer)
    $peerBytes[216 + 20] = 0 # Class 0 receives Divine Protection in MSG_Attack.
    [Array]::Clear($peerBytes, 4572, 256)
    $peerBytes[4572 + 31 * 8] = 6 # Place Type 6 in the last of 32 affect records.
    [IO.File]::WriteAllBytes($peer, $peerBytes)
}
if ($AttackManaControl) {
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 16] = 1 # Keep the actor clanned in the isolated combat fixture.
    $bytes[216 + 20] = 3 # Class-3 passive bit 4.
    [Array]::Copy([BitConverter]::GetBytes([int]0xA00000), 0, $bytes, 216 + 780, 4) # Passive DoubleCritical plus ManaControl divisor bit 23.
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $bytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $bytes, 216 + 42, 2)
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]1), 0, $bytes, $score, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]0), 0, $bytes, $score + 4, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $bytes, $score + 8, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 16, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 24, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 28, 4)
        [Array]::Copy([BitConverter]::GetBytes([int16]1000), 0, $bytes, $score + 32, 2)
        [Array]::Copy([BitConverter]::GetBytes([int16]1000), 0, $bytes, $score + 46, 2)
    }
    [Array]::Clear($bytes, 4572, 256) # Clear the isolated actor's effects.
    [IO.File]::WriteAllBytes($actor, $bytes)

    $peerBytes = [IO.File]::ReadAllBytes($peer)
    $peerBytes[216 + 16] = 2
    [Array]::Copy([BitConverter]::GetBytes([int16]2005), 0, $peerBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2005), 0, $peerBytes, 216 + 42, 2)
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]1), 0, $peerBytes, $score, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]10), 0, $peerBytes, $score + 4, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]0), 0, $peerBytes, $score + 8, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]100000), 0, $peerBytes, $score + 16, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]100000), 0, $peerBytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]100000), 0, $peerBytes, $score + 24, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]100000), 0, $peerBytes, $score + 28, 4)
    }
    [Array]::Clear($peerBytes, 4572, 256)
    $peerBytes[4572] = 18 # Target STRUCT_AFFECT.Type 18 activates physical ManaControl.
    [IO.File]::WriteAllBytes($peer, $peerBytes)
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
if ($AttackSkillGate) {
    # Class 0 (skill books 0..23), no learned skills, and enough MP for skills 10/96; slot 0 only.
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 20] = 0
    [Array]::Clear($bytes, 216 + 780, 4)
    $bytes[216 + 795] = 0x80 # Signed SaveMana -128 (7.69 char): skills cost 228%; unsigned 128 would make the cost negative.
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 28, 4)
        # Special[1]=200 (stage 1) and Special[2]=40 (stage 2): skill 10 must read Special[2].
        [Array]::Copy([BitConverter]::GetBytes([int16]200), 0, $bytes, $score + 42, 2)
        [Array]::Copy([BitConverter]::GetBytes([int16]40), 0, $bytes, $score + 44, 2)
    }
    [IO.File]::WriteAllBytes($actor, $bytes)
}
if ($AttackGoldenShieldCoin) {
    # Class 3, learned skill 85, Special[2]=3, Gold 600 and MP 121; each cast costs 300 Gold/121 MP.
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 16] = 1 # Avoid the clanless RvR gate regardless of fixture spawn.
    $bytes[216 + 20] = 3
    [Array]::Copy([BitConverter]::GetBytes([int]600), 0, $bytes, 216 + 28, 4)
    [Array]::Copy([BitConverter]::GetBytes([int](1 -shl 13)), 0, $bytes, 216 + 780, 4)
    $bytes[216 + 795] = 0 # SaveMana 0 keeps the CSV ManaSpent 120 at 121 with Special[2]=3.
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 16, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 24, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]121), 0, $bytes, $score + 28, 4)
        [Array]::Copy([BitConverter]::GetBytes([int16]3), 0, $bytes, $score + 44, 2) # STRUCT_SCORE.Special3 == Special[2].
    }
    [IO.File]::WriteAllBytes($actor, $bytes)
}
if ($AttackManaGain) {
    # Class 0 with skill 10 learned, SaveMana 120 (negative cost), MP 100 under MaxMp 107; slot 0 only.
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 20] = 0
    [Array]::Copy([BitConverter]::GetBytes([int](1 -shl 10)), 0, $bytes, 216 + 780, 4)
    $bytes[216 + 795] = 120
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]107), 0, $bytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]100), 0, $bytes, $score + 28, 4)
    }
    [IO.File]::WriteAllBytes($actor, $bytes)
}
if ($AttackSkillHeal -or $AttackMixedSlots) {
    # Mortal Foema with Cura (skill 27, learned bit 3) and Special[0..1]=300: 300*2+100 = 700, capped at 638.
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 20] = 1
    [Array]::Copy([BitConverter]::GetBytes([int](1 -shl 3)), 0, $bytes, 216 + 780, 4)
    $bytes[216 + 795] = 0
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 28, 4)
        [Array]::Copy([BitConverter]::GetBytes([int16]300), 0, $bytes, $score + 40, 2)
        [Array]::Copy([BitConverter]::GetBytes([int16]300), 0, $bytes, $score + 42, 2)
    }
    [Array]::Copy([BitConverter]::GetBytes([int16]2), 0, $bytes, 5600, 2) # ClassMaster MORTAL
    [IO.File]::WriteAllBytes($actor, $bytes)
    # Observer: HP 1000/5000, clan 0, no Equip[13] mount divisor.
    $peerBytes = [IO.File]::ReadAllBytes($peer)
    $peerBytes[216 + 16] = 0
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $peerBytes, $score + 16, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $peerBytes, $score + 24, 4)
    }
    [Array]::Clear($peerBytes, 216 + 140 + 13 * 8, 8)
    [IO.File]::WriteAllBytes($peer, $peerBytes)
}
if ($AttackSkillHeal) {
    # Stable non-city positions make the network smoke exercise authoritative FakeExp rather than only the town exclusion.
    $actorBytes = [IO.File]::ReadAllBytes($actor)
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $actorBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $actorBytes, 216 + 42, 2)
    [IO.File]::WriteAllBytes($actor, $actorBytes)
    $peerBytes = [IO.File]::ReadAllBytes($peer)
    [Array]::Copy([BitConverter]::GetBytes([int16]2005), 0, $peerBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $peerBytes, 216 + 42, 2)
    [IO.File]::WriteAllBytes($peer, $peerBytes)
}
if ($AttackSummonNotice) {
    # Foema skill 42 (Velocidade), whose 7.69 row is InstanceType 9 and bParty=1; bit 18 is skillId % 24.
    # A shared synthetic Guild lets the target pass the common bParty gate before the Type 9 zone branch.
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 20] = 1
    [Array]::Copy([BitConverter]::GetBytes([uint16]77), 0, $bytes, 216 + 18, 2)
    $learnedSkills = [BitConverter]::ToUInt32($bytes, 216 + 780) -bor [uint32](1 -shl 18)
    [Array]::Copy([BitConverter]::GetBytes([uint32]$learnedSkills), 0, $bytes, 216 + 780, 4)
    [IO.File]::WriteAllBytes($actor, $bytes)

    $peerBytes = [IO.File]::ReadAllBytes($peer)
    [Array]::Copy([BitConverter]::GetBytes([uint16]77), 0, $peerBytes, 216 + 18, 2)
    [IO.File]::WriteAllBytes($peer, $peerBytes)
}
if ($AttackDivineBlessing) {
    foreach ($path in @($actor, $failureActor)) {
        $bytes = [IO.File]::ReadAllBytes($path)
        $bytes[216 + 16] = 1 # Keep all combat participants in the same clan for this non-damaging support action.
        $bytes[216 + 20] = 3 # Divine Blessing's secondary bit is independent of the primary learned-skill tree.
        $bytes[216 + 795] = 0 # Deterministic SaveMana for the synthetic skill-29 debit.
        [Array]::Copy([BitConverter]::GetBytes([int16]2100), 0, $bytes, 216 + 40, 2)
        [Array]::Copy([BitConverter]::GetBytes([int16]2100), 0, $bytes, 216 + 42, 2)
        $mp = if ($path -eq $actor) { 500 } else { 10 }
        foreach ($score in @((216 + 44), (216 + 92))) {
            [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $bytes, $score + 16, 4) # MaxHp
            [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $bytes, $score + 20, 4) # MaxMp
            [Array]::Copy([BitConverter]::GetBytes([int]900), 0, $bytes, $score + 24, 4) # Current HP
            [Array]::Copy([BitConverter]::GetBytes([int]$mp), 0, $bytes, $score + 28, 4) # Current MP
        }
        $extraOffset = 5600 + 4 # Measured MOBEXTRA.SecLearnedSkill ABI offset.
        $secondarySkills = [BitConverter]::ToUInt32($bytes, $extraOffset) -bor [uint32]2
        [Array]::Copy([BitConverter]::GetBytes([uint32]$secondarySkills), 0, $bytes, $extraOffset, 4)
        [IO.File]::WriteAllBytes($path, $bytes)
    }
    foreach ($path in @($peer, $distantObserver)) {
        $bytes = [IO.File]::ReadAllBytes($path)
        $bytes[216 + 16] = 1
        [Array]::Copy([BitConverter]::GetBytes([int16]2101), 0, $bytes, 216 + 40, 2)
        [Array]::Copy([BitConverter]::GetBytes([int16]2100), 0, $bytes, 216 + 42, 2)
        $extraOffset = 5600 + 4
        $secondarySkills = [BitConverter]::ToUInt32($bytes, $extraOffset) -band ([uint32]::MaxValue -bxor [uint32]2)
        [Array]::Copy([BitConverter]::GetBytes([uint32]$secondarySkills), 0, $bytes, $extraOffset, 4)
        if ($path -eq $distantObserver) {
            # Mortal newcomers are intentionally placed within five tiles of Armia regardless of SPX/SPY.
            # Give the distant observer an ARCH fixture and Azran city selector so login resolves it outside the actor view.
            [Array]::Copy([BitConverter]::GetBytes([int16]1), 0, $bytes, 5600, 2) # MOBEXTRA.ClassMaster = ARCH
            $bytes[216 + 92 + 12] = ($bytes[216 + 92 + 12] -band 0x3F) -bor 0x40 # STRUCT_SCORE.Merchant selects Azran.
        }
        [IO.File]::WriteAllBytes($path, $bytes)
    }
    foreach ($identity in @(
        @{ Path = $failureActor; Name = 'TESTE3'; Password = 'SmokeA3!' },
        @{ Path = $distantObserver; Name = 'TESTE4'; Password = 'SmokeA4!' }
    )) {
        $identityBytes = [IO.File]::ReadAllBytes($identity.Path)
        [Array]::Clear($identityBytes, 0, 28)
        $nameBytes = [Text.Encoding]::ASCII.GetBytes($identity.Name)
        $passwordBytes = [Text.Encoding]::ASCII.GetBytes($identity.Password)
        [Array]::Copy($nameBytes, 0, $identityBytes, 0, $nameBytes.Length)
        [Array]::Copy($passwordBytes, 0, $identityBytes, 16, $passwordBytes.Length)
        [IO.File]::WriteAllBytes($identity.Path, $identityBytes)
    }
}
if ($AttackMixedSlots) {
    # Stable physical hit and close coordinates for the combined -2/-1 action.
    $actorBytes = [IO.File]::ReadAllBytes($actor)
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $actorBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2000), 0, $actorBytes, 216 + 42, 2)
    [Array]::Clear($actorBytes, 216 + 22, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]1001), 0, $actorBytes, 216 + 92 + 36, 2)
    [IO.File]::WriteAllBytes($actor, $actorBytes)
    $peerBytes = [IO.File]::ReadAllBytes($peer)
    [Array]::Copy([BitConverter]::GetBytes([int16]2005), 0, $peerBytes, 216 + 40, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]2005), 0, $peerBytes, 216 + 42, 2)
    [Array]::Copy([BitConverter]::GetBytes([int16]1000), 0, $peerBytes, 216 + 92 + 36, 2)
    [IO.File]::WriteAllBytes($peer, $peerBytes)

    # A third authenticated character lives outside the actor's physical range for the ordered abort case.
    $farBytes = [IO.File]::ReadAllBytes($failureActor)
    [Array]::Clear($farBytes, 0, 16)
    [Array]::Copy([Text.Encoding]::ASCII.GetBytes('TESTE3'), 0, $farBytes, 0, 6)
    [IO.File]::WriteAllBytes($failureActor, $farBytes)
}
if ($AttackPartySkillGate) {
    # Learned Foema skill 29 (Recuperar), bParty=1 in 7.69. Same nonzero Guild lets this smoke
    # exercise guildoff/guildon separately from the party fallback.
    $bytes = [IO.File]::ReadAllBytes($actor)
    $bytes[216 + 20] = 1
    [Array]::Copy([BitConverter]::GetBytes([int](1 -shl 5)), 0, $bytes, 216 + 780, 4)
    [Array]::Copy([BitConverter]::GetBytes([uint16]77), 0, $bytes, 216 + 18, 2)
    $bytes[216 + 795] = 0
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $bytes, $score + 28, 4)
    }
    [IO.File]::WriteAllBytes($actor, $bytes)

    $peerBytes = [IO.File]::ReadAllBytes($peer)
    $peerBytes[216 + 16] = 0 # Clanless; the two isolated copies share only the synthetic GuildId below.
    $peerBytes[216 + 20] = 2 # Skill 56 Evocar Condor belongs to class 2.
    [Array]::Clear($peerBytes, 216 + 780, 4)
    [Array]::Copy([BitConverter]::GetBytes([int](1 -shl 8)), 0, $peerBytes, 216 + 780, 4)
    [Array]::Copy([BitConverter]::GetBytes([uint16]77), 0, $peerBytes, 216 + 18, 2)
    foreach ($score in @((216 + 44), (216 + 92))) {
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $peerBytes, $score + 16, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]1000), 0, $peerBytes, $score + 24, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $peerBytes, $score + 20, 4)
        [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $peerBytes, $score + 28, 4)
        [Array]::Copy([BitConverter]::GetBytes([int16]40), 0, $peerBytes, $score + 44, 2) # Special3 requests one summon.
    }
    [Array]::Clear($peerBytes, 216 + 140 + 13 * 8, 8) # No mount divisor on the target.
    [IO.File]::WriteAllBytes($peer, $peerBytes)

    # Generate one deterministic static NPC at the actor's fixture position so
    # the TCP smoke can target an authoritative NPC without loading live data.
    New-Item -ItemType Directory -Path $partyNpcRoot | Out-Null
    $actorSnapshot = [IO.File]::ReadAllBytes($actor)
    $partyNpcX = [BitConverter]::ToInt16($actorSnapshot, 216 + 40)
    $partyNpcY = [BitConverter]::ToInt16($actorSnapshot, 216 + 42)
    $partyNpcMob = New-Object byte[] 816 # sizeof(STRUCT_MOB)
    [Array]::Copy([Text.Encoding]::ASCII.GetBytes('ATTACK_PARTY_NPC'), 0, $partyNpcMob, 0, 16)
    [Array]::Copy([BitConverter]::GetBytes([int]5000), 0, $partyNpcMob, 44 + 16, 4) # CurrentScore.MaxHp
    [IO.File]::WriteAllBytes($partyNpcTemplatePath, $partyNpcMob)
    $npcDefinition = "# [3427]`nMinuteGenerate: 0`nMaxNumMob: 1`nMinGroup: 1`nMaxGroup: 1`nLeader: ATTACK_PARTY_NPC`nStartX: $partyNpcX`nStartY: $partyNpcY`nStartRange: 0`n"
    [IO.File]::WriteAllText($partyNpcGenerationPath, $npcDefinition, [Text.UTF8Encoding]::new($false))
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
$autoDoubleConfigPath = Join-Path $runRoot 'eventAuto-smoke.json'
if ($AutoDoubleMagicTrumpet) {
    $scheduleNow = Get-Date
    $scheduleStart = $scheduleNow.AddMinutes(1)
    $scheduleEnd = $scheduleStart.AddMinutes(1)
    $autoDoubleConfig = [ordered]@{
        EVENT = [ordered]@{
            DOUBLE = [ordered]@{
                DayStart = [int]$scheduleStart.DayOfWeek
                HourStart = $scheduleStart.Hour
                MinStart = $scheduleStart.Minute
                MSGStart = 'Double EXP started'
                DayEnd = [int]$scheduleEnd.DayOfWeek
                HourEnd = $scheduleEnd.Hour
                MinEnd = $scheduleEnd.Minute
                MultEXP = 3
                MSGEnd = 'Double EXP ended'
            }
        }
    }
    [IO.File]::WriteAllText($autoDoubleConfigPath, ($autoDoubleConfig | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}
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
if ($ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $AttackSkillGate -or $AttackPartySkillGate -or $AttackGoldenShieldCoin -or $AttackDoubleCritical -or $AttackMultiTarget -or $AttackMixedSlots -or $AttackType6Protection -or $AttackManaControl -or $AttackManaGain -or $AttackSkillHeal -or $AttackSummonNotice -or $AttackDivineBlessing) {
    $artifactPaths += Join-Path $projectRoot 'Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv'
}
if ($AttackDoubleCritical) { $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackDoubleCriticalNetwork.cs' }
if ($AttackMultiTarget) { $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackMultiTargetNetwork.cs'; $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/Program.cs' }
if ($AttackMixedSlots) { $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackMixedSlotsNetwork.cs'; $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/Program.cs' }
if ($AttackType6Protection) { $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackType6ProtectionNetwork.cs'; $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/Program.cs' }
if ($AttackManaControl) { $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackManaControlNetwork.cs' }
if ($AttackSkillHeal) { $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackSkillHealNetwork.cs' }
if ($AttackSummonNotice) {
    $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackSkillSummonNoticeNetwork.cs'
    $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/Program.cs'
}
if ($AttackDivineBlessing) {
    $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackDivineBlessingNetwork.cs'
    $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/Program.cs'
}
if ($AttackPartySkillGate) {
    $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackPartySkillGateNetwork.cs'
    $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/Program.cs'
    $artifactPaths += @($partyNpcGenerationPath, $partyNpcTemplatePath)
}
if ($AttackGoldenShieldCoin) { $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AttackGoldenShieldCoinNetwork.cs'; $artifactPaths += Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/Program.cs' }
if ($AutoDoubleMagicTrumpet) {
    $artifactPaths += @($autoDoubleConfigPath,
        (Join-Path $projectRoot 'Server/src/WydCdk.Server/Program.cs'),
        (Join-Path $projectRoot 'Server/src/WydCdk.Protocol/MagicTrumpetConfirmation.cs'),
        (Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/AutoDoubleMagicTrumpetNetwork.cs'),
        (Join-Path $projectRoot 'Server/tests/WydCdk.Client.E2E/Program.cs'))
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
    if ($DoubleModeMultiplier -ne 1) { $arguments += @('--double-mode',[string]$DoubleModeMultiplier) }
    if ($AutoDoubleMagicTrumpet) { $arguments += @('--auto-double-config',$autoDoubleConfigPath) }
    if ($ActionRange -or $ActionIllusionEligibility -or $ActionIllusionMana -or $AttackDeadGate -or $AttackRvrGate -or $AttackTimingGate -or $AttackSkillGate -or $AttackPartySkillGate -or $AttackGoldenShieldCoin -or $AttackDoubleCritical -or $AttackMultiTarget -or $AttackMixedSlots -or $AttackType6Protection -or $AttackManaControl -or $AttackManaGain -or $AttackSkillHeal -or $AttackSummonNotice -or $AttackDivineBlessing) {
        $arguments += @('--skill-data',(Join-Path $projectRoot 'Backup/Tools/ReferenceSources/TMProject2GlobalClient/Servidor/Server/Common/skilldata.csv'))
    }
    if ($AttackPartySkillGate) {
        $arguments += @('--npc-generation',$partyNpcGenerationPath,'--npc-root',$partyNpcRoot,'--city-npcs')
        if (!(Test-Path -LiteralPath $partySummonRoot -PathType Container)) { throw 'The 7.69 BaseSummon reference directory is missing.' }
        $arguments += @('--base-summon',$partySummonRoot)
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
    if ($AutoDoubleMagicTrumpet) {
        $phase = 'auto-double-magictrumpet'
        Start-OwnedSmokeHost $phase -IncludeReviewRoot
        $clientArgs = @('--auto-double-magic-trumpet-smoke','--host','127.0.0.1','--port','18283',
            '--account-file',$actor,'--autotrade-close-peer-account-file',$peer,'--autotrade-state-file',$state,'--timeout-seconds','150')
        try {
            $ErrorActionPreference = 'Continue'
            & dotnet $clientDll @clientArgs 2>&1 | Tee-Object -FilePath (Join-Path $runRoot 'client.log')
            $e2eExit = $LASTEXITCODE
        } finally { $ErrorActionPreference = 'Stop' }
        $hostStdout = Join-Path $runRoot ($phase + '.stdout.log')
        Stop-OwnedSmokeHost
        $clientLog = Join-Path $runRoot 'client.log'
        if ($e2eExit -ne 0) { throw 'AutoDouble MagicTrumpet loopback delivery assertions failed; evidence retained.' }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS auto-double-start-end-messagepanel-and-magictrumpet-reached-two-playing-clients$' }).Count -ne 1) {
            throw 'AutoDouble two-player start/end notification marker is missing.'
        }
        if (@(Get-Content -LiteralPath $hostStdout | Where-Object { $_ -match 'AutoDouble transition: active=True, multiplier=3,' }).Count -ne 1) {
            throw 'AutoDouble start transition was not observed by the Host.'
        }
        'AUTO_DOUBLE_MAGIC_TRUMPET_NETWORK_PASS'
        $assertionsPassed = $true
    } elseif ($ActionAnalysis) {
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
    if ($AttackSkillGate) { $clientArgs += '--attack-skill-gate-smoke' }
    if ($AttackPartySkillGate) { $clientArgs += '--attack-party-skill-gate-smoke' }
    if ($AttackGoldenShieldCoin) { $clientArgs += '--attack-golden-shield-coin-smoke' }
    if ($AttackDoubleCritical) { $clientArgs += '--attack-double-critical-smoke' }
    if ($AttackMultiTarget) { $clientArgs += '--attack-multi-target-smoke' }
    if ($AttackMixedSlots) { $clientArgs += @('--attack-mixed-slots-smoke','--attack-mixed-out-of-range-account-file',$failureActor) }
    if ($AttackType6Protection) { $clientArgs += '--attack-type6-protection-smoke' }
    if ($AttackManaControl) { $clientArgs += '--attack-mana-control-smoke' }
    if ($AttackManaGain) { $clientArgs += '--attack-mana-gain-smoke' }
    if ($AttackSkillHeal) { $clientArgs += @('--attack-skill-heal-smoke','--expected-double-mode',[string]$DoubleModeMultiplier) }
    if ($AttackSummonNotice) { $clientArgs += '--attack-summon-notice-smoke' }
    if ($AttackDivineBlessing) {
        $clientArgs += @('--attack-divine-blessing-smoke', '--attack-divine-blessing-distant-observer-account-file', $distantObserver)
    }
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
    if ($AttackDivineBlessing -and $e2eExit -eq 0) {
        $lowManaArgs = @($clientArgs)
        $lowManaArgs[5] = $failureActor
        $lowManaFlag = [Array]::IndexOf($lowManaArgs, '--attack-divine-blessing-smoke')
        if ($lowManaFlag -lt 0) { throw 'Divine Blessing success flag is missing from its isolated client invocation.' }
        $lowManaArgs[$lowManaFlag] = '--attack-divine-blessing-insufficient-mana-smoke'
        try {
            $ErrorActionPreference = 'Continue'
            & dotnet $clientDll @lowManaArgs 2>&1 | Tee-Object -FilePath (Join-Path $runRoot 'client-divine-insufficient-mana.log')
            $e2eExit = $LASTEXITCODE
        } finally { $ErrorActionPreference = 'Stop' }
    }
    Stop-OwnedSmokeHost
    if ($e2eExit -ne 0) { throw 'Loopback network assertions failed; evidence retained.' }
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
        $rejectedTooSoon = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Rejected attack: account=.*?, skill=-2, reason=TooSoon\.$' })
        $rejectedOutside = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Rejected attack: account=.*?, skill=-2, reason=OutsideServerWindow\.$' })
        $rejectedInvalidSkill = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Rejected attack: account=.*?, skill=-2, reason=InvalidSkillId\.$' })
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
    } elseif ($AttackSkillGate) {
        $allCrackLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' })
        $clientLog = Join-Path $runRoot 'client.log'
        $actorMarkers = @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_SKILL_ACTOR_ID=\d+$' })
        if ($actorMarkers.Count -ne 1) { throw 'Attack skill gate actor identity missing.' }
        $actorId = [int]($actorMarkers[0] -replace '^ATTACK_SKILL_ACTOR_ID=', '')
        if ($allCrackLines.Count -ne 2 -or
            $allCrackLines[0] -notmatch ('^Crack error: connection=' + $actorId + ', points=8, type=10, total=8\.$') -or
            $allCrackLines[1] -notmatch ('^Crack error: connection=' + $actorId + ', points=208, type=1, total=216\.$')) {
            throw 'Unlearned skills must add exactly 8/type10 then 208/type1 to the actor without other crack points.'
        }
        foreach ($expected in @('skill=30, reason=WrongClass', 'skill=9, reason=PassiveSkill', 'skill=97, reason=MortarNotPorted', 'skill=-2, reason=InvalidSkillId')) {
            if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match ('^Rejected attack: account=.*?, ' + [regex]::Escape($expected) + '\.$') }).Count -ne 1) {
                throw "Attack skill gate rejection '$expected' is absent or duplicated."
            }
        }
        foreach ($skill in @(-1, 10, 96, 103)) {
            if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match ('^Attack mana accepted: account=.*?, skill=' + $skill + ', ') }).Count -ne 1) {
                throw "Accepted attack skill=$skill did not reach the relay exactly once."
            }
        }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Rejected attack:' }).Count -ne 4) { throw 'Unexpected additional attack rejection.' }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_SKILL_MP=5000,4946,4810,4810$' }).Count -ne 1) { throw 'Stage-2 Special[2]=40 with signed SaveMana -128 did not give 5000,4946,4810,4810.' }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-skill-gate physical-silent-rejects-nonterminal-learned-crack$' }).Count -ne 1) {
            throw 'Attack skill gate E2E pass marker is missing.'
        }
        'ATTACK_SKILL_GATE_NETWORK_PASS'
    } elseif ($AttackPartySkillGate) {
        $allCrackLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Crack error:' })
        $clientLog = Join-Path $runRoot 'client.log'
        $actorMarkers = @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_PARTY_ACTOR_ID=\d+$' })
        $peerMarkers = @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_PARTY_PEER_ID=\d+$' })
        $npcMarkers = @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_PARTY_NPC_ID=20000$' })
        $summonMarkers = @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_PARTY_SUMMON_ID=\d+$' })
        $blockedTargets = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Ignored party-ineligible skill target: account=.*?, skill=29, slot=0\.$' })
        $acceptedSummonTargets = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Skill attack produced no damage: account=.*?, skill=29, targetIndex=0, target=\d+, reason=UnsupportedSkillType\.$' })
        $summonGeneration = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Skill attack resolved: account=.*?, skill=56, targetIndex=0, target=\d+, .*summons=1, summonResult=Created\.$' })
        $acceptedInvites = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Party invite sent: leader=\d+, target=\d+\.$' })
        $acceptedParties = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Party accepted: leader=\d+, member=\d+, size=2\.$' })
        if ($actorMarkers.Count -ne 1 -or $peerMarkers.Count -ne 1 -or $npcMarkers.Count -ne 1 -or $summonMarkers.Count -ne 1 -or
            $summonGeneration.Count -ne 1 -or $allCrackLines.Count -ne 4 -or
            $allCrackLines[0] -notmatch '^Crack error: connection=\d+, points=10, type=27, total=10\.$' -or
            $allCrackLines[1] -notmatch '^Crack error: connection=\d+, points=10, type=27, total=20\.$' -or
            $allCrackLines[2] -notmatch '^Crack error: connection=\d+, points=10, type=27, total=30\.$' -or
            $allCrackLines[3] -notmatch '^Crack error: connection=\d+, points=10, type=27, total=40\.$' -or
            $blockedTargets.Count -ne 4 -or $acceptedSummonTargets.Count -ne 1 -or $acceptedInvites.Count -ne 1 -or $acceptedParties.Count -ne 1) {
            throw 'Party skill smoke expected NPC/player/summon outsider 10/type27 rejections, one unchanged eligible summon target, and one party formation.'
        }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-party-skill guilddisable-player-npc-summon-party-eligibility-without-summon-effect$' }).Count -ne 1) {
            throw 'Party skill wire smoke pass marker is missing.'
        }
        'ATTACK_PARTY_SKILL_GATE_NETWORK_PASS'
    } elseif ($AttackGoldenShieldCoin) {
        $clientLog = Join-Path $runRoot 'client.log'
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Crack error:' }).Count -ne 0) { throw 'Golden Shield fee smoke added crack points.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Attack mana accepted: account=.*?, skill=85, ' }).Count -ne 1) { throw 'The first Golden Shield attack did not reach the accepted relay.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Rejected attack: account=.*?, skill=85, reason=InsufficientMana\.$' }).Count -ne 1) { throw 'The second Golden Shield cast did not stop once at the mana gate.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Rejected attack:' }).Count -ne 1) { throw 'Golden Shield smoke had an unexpected attack rejection.' }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-golden-shield coin-update-before-mana private-relay-insufficient-coin-silent$' }).Count -ne 1) {
            throw 'Golden Shield coin/mana ordering network pass marker is missing.'
        }
        'ATTACK_GOLDEN_SHIELD_COIN_NETWORK_PASS'
    } elseif ($AttackDoubleCritical) {
        $clientLog = Join-Path $runRoot 'client.log'
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Rejected attack:' }).Count -ne 0) { throw 'DoubleCritical smoke had rejected physical attacks.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Physical attack resolved:' }).Count -ne 25) { throw 'DoubleCritical smoke did not resolve all 25 physical attack packets.' }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_DOUBLE_CRITICAL_PROCS=[1-9]\d*/25$' }).Count -ne 1 -or
            @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-double-critical authoritative-two-target-slot-and-secondary-damage$' }).Count -ne 1) {
            throw 'DoubleCritical wire smoke did not prove a server-generated second-slot proc.'
        }
        'ATTACK_DOUBLE_CRITICAL_NETWORK_PASS'
    } elseif ($AttackType6Protection) {
        $clientLog = Join-Path $runRoot 'client.log'
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Rejected attack:' }).Count -ne 0 -or
            @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Physical attack resolved:' }).Count -ne 2 -or
            @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-type6-class0-two-physical-slots-zero-damage-hp-unchanged$' }).Count -ne 1) {
            throw 'Type 6 protection smoke did not prove zero damage in both physical slots without a target HP mutation.'
        }
        'ATTACK_TYPE6_PROTECTION_NETWORK_PASS'
    } elseif ($AttackMixedSlots) {
        $clientLog = Join-Path $runRoot 'client.log'
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-mixed-physical-skill-one-batch-one-relay-order-and-invalid-skill-marker$' }).Count -ne 1) {
            throw 'Mixed-slot Attack E2E pass marker is missing.'
        }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_MIXED_SLOT_REVERSED=\d+,-638;\d+,\d+;skillSlot=0;physicalSlot=1;progress=321$' }).Count -ne 1) {
            throw 'Skill-first wire slot alignment or shared Progress marker is missing.'
        }
        $slotOutcomeLines = @(Get-Content -LiteralPath $stdout | Where-Object {
            $_ -match '^Physical attack resolved: .*slot=\d+,' -or $_ -match '^Skill attack resolved: .*targetIndex=\d+,'
        })
        $expectedSlotOrder = @(
            '^Physical attack resolved: .*slot=0,',
            '^Skill attack resolved: .*targetIndex=1,',
            '^Skill attack resolved: .*targetIndex=0,',
            '^Physical attack resolved: .*slot=1,',
            '^Physical attack resolved: .*slot=0,',
            '^Skill attack resolved: .*targetIndex=0,'
        )
        if ($slotOutcomeLines.Count -ne $expectedSlotOrder.Count) { throw 'Mixed-slot Host did not emit exactly six ordered physical/skill outcomes.' }
        for ($index = 0; $index -lt $expectedSlotOrder.Count; $index++) {
            if ($slotOutcomeLines[$index] -notmatch $expectedSlotOrder[$index]) {
                throw "Mixed-slot Host outcome order diverged at wire result $index."
            }
        }
        'ATTACK_MIXED_SLOTS_NETWORK_PASS'
    } elseif ($AttackMultiTarget) {
        $clientLog = Join-Path $runRoot 'client.log'
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-multi-target-13-slot-indexed-authoritative-relay$' }).Count -ne 1) {
            throw 'Multi-target Attack E2E pass marker is missing.'
        }
        'ATTACK_MULTI_TARGET_NETWORK_PASS'
    } elseif ($AttackManaControl) {
        $clientLog = Join-Path $runRoot 'client.log'
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Rejected attack:' }).Count -ne 0) { throw 'ManaControl smoke had rejected physical attacks.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Physical attack resolved:' }).Count -ne 25) { throw 'ManaControl smoke did not resolve all 25 physical attack packets.' }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_MANA_CONTROL_STATE=mp:\d+->\d+,hp:\d+->\d+,doublecritical:[1-9]\d*/25$' }).Count -ne 1 -or
            @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-mana-control-affect18-mp-drain-55-divisor-doublecritical-slot-override$' }).Count -ne 1) {
            throw 'ManaControl wire smoke did not prove MP drain, 55-divisor damage and the DoubleCritical slot override.'
        }
        'ATTACK_MANA_CONTROL_NETWORK_PASS'
    } elseif ($AttackManaGain) {
        $clientLog = Join-Path $runRoot 'client.log'
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'Crack error:' }).Count -ne 0) { throw 'Learned negative-cost skill gained crack points.' }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Rejected attack:' }).Count -ne 0) { throw 'Negative-cost skill attack was rejected.' }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_MANA_MP=104,107$' }).Count -ne 1 -or
            @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-mana-signed-savemana-gain-capped-at-maxmp$' }).Count -ne 1) {
            throw 'Negative ManaSpent gain/MaxMp cap markers are missing.'
        }
        'ATTACK_MANA_GAIN_NETWORK_PASS'
    } elseif ($AttackDivineBlessing) {
        $clientLog = Join-Path $runRoot 'client.log'
        $lowManaLog = Join-Path $runRoot 'client-divine-insufficient-mana.log'
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-divine-blessing-child-before-parent-captured-view-authoritative-hp-mp-exp$' }).Count -ne 1 -or
            @(Get-Content -LiteralPath $lowManaLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-divine-blessing-insufficient-mana-private-correction-before-parent-no-child-no-heal$' }).Count -ne 1) {
            throw 'Divine Blessing child/view/parent or insufficient-mana ordering assertion is missing.'
        }
        'ATTACK_DIVINE_BLESSING_NETWORK_PASS'
    } elseif ($AttackSkillHeal) {
        $clientLog = Join-Path $runRoot 'client.log'
        $expectedHealFakeExp = [Math]::Min(200, 79 * $DoubleModeMultiplier)
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_HEAL_TARGET_POS=2105,2170$' }).Count -ne 1 -or
            @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match ('^ATTACK_HEAL_FAKE_EXP=' + $expectedHealFakeExp + '$') }).Count -ne 1) {
            throw "Non-city heal smoke did not relay FakeExp=$expectedHealFakeExp at the stable target position."
        }
        $targetLimitLines = @(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Crack error: .*points=10, type=28, total=(10|20)\.$' })
        if ($targetLimitLines.Count -ne 2 -or $targetLimitLines[0] -notmatch 'total=10\.$' -or $targetLimitLines[1] -notmatch 'total=20\.$') {
            throw 'Excess skill slots did not add exactly two ordered 10/type28 increments.'
        }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Crack error:' -and $_ -notmatch 'points=10, type=28, total=(10|20)\.$' }).Count -ne 0) {
            throw 'The learned heal smoke added an unrelated crack error.'
        }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match '^Rejected attack:' }).Count -ne 0) { throw 'Heal attack was rejected.' }
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^ATTACK_HEAL_PEER_HP=1638$' }).Count -ne 1 -or
            @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-skill-heal type28-excess-slots-preserved-mortal-cap-638-relay-and-hp-sync$' }).Count -ne 1) {
            throw 'Mortal heal cap 638 markers are missing.'
        }
        'ATTACK_SKILL_HEAL_NETWORK_PASS'
    } elseif ($AttackSummonNotice) {
        $clientLog = Join-Path $runRoot 'client.log'
        if (@(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match '^CLIENT_E2E_PASS attack-summon-private-zone-notice-terminal-return-no-relay$' }).Count -ne 1) {
            throw 'InstanceType 9 private-notice smoke completion marker is missing.'
        }
        if (@(Get-Content -LiteralPath $stdout | Where-Object { $_ -match 'reason=SummonZoneBlocked' }).Count -ne 1) {
            throw 'Host did not record the blocked InstanceType 9 zone result.'
        }
        'ATTACK_SUMMON_NOTICE_NETWORK_PASS'
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
$mode = if ($AutoDoubleMagicTrumpet) { 'auto-double-magictrumpet' } elseif ($AttackDivineBlessing) { 'attack-divine-blessing' } elseif ($AttackSummonNotice) { 'attack-summon-notice' } elseif ($AttackMixedSlots) { 'attack-mixed-slots' } elseif ($MapRecall) { 'movement-map-recall' } elseif ($ActionRecall) { 'action-origin-recall' } elseif ($ActionAnalysis) { 'action-account-analysis' } elseif ($ActionIllusionMana) { 'action-illusion-mana' } elseif ($ActionIllusionEligibility) { 'action-illusion-eligibility' } elseif ($ActionRange) { 'action-range' } elseif ($ActionNegativeHp) { 'action-negative-hp' } elseif ($Action) { 'action' } elseif ($TradeListNegativeHp) { 'trade-list-negative-hp' } elseif ($TradeList) { 'trade-list' } elseif ($MotionNegativeHp) { 'motion-negative-hp' } elseif ($Motion) { 'motion' } elseif ($AttackPartySkillGate) { 'attack-party-skill-gate' } elseif ($AttackGoldenShieldCoin) { 'attack-golden-shield-coin' } elseif ($AttackType6Protection) { 'attack-type6-protection' } elseif ($AttackManaControl) { 'attack-mana-control' } elseif ($AttackDoubleCritical) { 'attack-double-critical' } elseif ($AttackTimingGate) { 'attack-timing-gate' } elseif ($AttackSkillGate) { 'attack-skill-gate' } elseif ($AttackManaGain) { 'attack-mana-gain' } elseif ($AttackSkillHeal) { 'attack-skill-heal' } elseif ($AttackRvrGate) { 'attack-rvr-gate' } elseif ($AttackDeadGate) { 'attack-dead-gate' } elseif ($DeadQuit) { 'dead-quit' } else { 'autotrade-close' }
$manifest = [pscustomobject]@{ Version=1; Mode=$mode; ExitCode=$e2eExit; AssertionsPassed=$assertionsPassed; Artifacts=$artifacts; OriginalFixtureHashes=$hashes; ServerLogs=@($serverLogs) }
    if ($AttackDeadGate) { $manifest.Mode = 'attack-dead-gate' }
    if ($AttackMultiTarget) { $manifest.Mode = 'attack-multi-target' }
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
