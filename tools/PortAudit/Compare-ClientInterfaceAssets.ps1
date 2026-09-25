[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$ManifestPath,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
}
else {
    $ProjectRoot = (Resolve-Path $ProjectRoot).Path
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ProjectRoot 'Server\docs\CLIENT_UI_CANDIDATES.json'
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $ProjectRoot 'Client\Launcher\interface-manifest.json'
}

$manifest = Get-Content -LiteralPath (Resolve-Path $ManifestPath) -Raw | ConvertFrom-Json

function Normalize-AssetPath([string]$Path) {
    return (($Path.Replace('\', '/')) -replace '/+', '/').TrimStart('./')
}

function Get-CatalogNames([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path))
    $recordSize = 528
    $nameSize = 255
    $names = [System.Collections.Generic.List[string]]::new()

    for ($offset = 0; $offset + $recordSize -le $bytes.Length; $offset += $recordSize) {
        $end = $offset
        while ($end -lt $offset + $nameSize -and $bytes[$end] -ne 0) {
            $end++
        }

        if ($end -gt $offset) {
            $name = [Text.Encoding]::ASCII.GetString($bytes, $offset, $end - $offset)
            $names.Add((Normalize-AssetPath $name))
        }
    }

    return @($names | Sort-Object -Unique)
}

function Get-AssetPath([string]$Root, [string]$RelativePath) {
    return Join-Path $Root (Normalize-AssetPath $RelativePath).Replace('/', '\')
}

function Get-AssetFormat([string]$RelativePath) {
    switch ([IO.Path]::GetExtension($RelativePath).ToLowerInvariant()) {
        '.wys' { return 'ui-texture-dds' }
        '.wyt' { return 'ui-texture-tga-or-raw' }
        '.bin' { return 'binary-structural-or-data' }
        '.txt' { return 'text-structural-or-data' }
        '.dat' { return 'binary-data' }
        default { return 'other' }
    }
}

function Get-WytInfo([string]$Path) {
    if ([IO.Path]::GetExtension($Path).ToLowerInvariant() -ne '.wyt') {
        return $null
    }

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 22) {
        return [ordered]@{ magic = $null; tgaType = $null; width = $null; height = $null; bitsPerPixel = $null; trailingBytes = $null }
    }

    $header = [byte[]]$bytes[4..21]
    $width = [BitConverter]::ToUInt16($header, 12)
    $height = [BitConverter]::ToUInt16($header, 14)
    $bitsPerPixel = [int]$header[16]
    $expectedPixelBytes = [int64]$width * $height * ($bitsPerPixel / 8)
    $payloadBytes = $bytes.Length - 4 - 18

    return [ordered]@{
        magic = [Text.Encoding]::ASCII.GetString($bytes, 0, 4)
        tgaType = [int]$header[2]
        width = $width
        height = $height
        bitsPerPixel = $bitsPerPixel
        trailingBytes = $payloadBytes - $expectedPixelBytes
    }
}

function Test-BlockedByLauncherPolicy([string]$RelativePath) {
    $normalized = Normalize-AssetPath $RelativePath
    return $normalized -match '(?i)\.(bak|scc)$' -or
        $normalized -match '(?i)^UI/(UITextureListN\.bin|UITextureSetList\.txt|SelServerScene.*\.bin|EffectString\.txt|EffectSubString\.txt|GuildString\.txt)$'
}

$baseRoot = Join-Path $ProjectRoot 'Client\7670'
$baseCatalogPath = Join-Path $baseRoot 'UI\UITextureListN.bin'
$baseCatalog = @(Get-CatalogNames $baseCatalogPath)
$records = [System.Collections.Generic.List[object]]::new()

foreach ($profile in @('7559', '7600', '7662')) {
    $profileRoot = Join-Path $ProjectRoot "Client\$profile"
    $profileCatalog = @(Get-CatalogNames (Join-Path $profileRoot 'UI\UITextureListN.bin'))
    $manifestProfile = @($manifest.profiles | Where-Object { $_.name -eq $profile }) | Select-Object -First 1
    $manifestApprovals = if ($manifestProfile) { @($manifestProfile.overlayApprovedFiles) } else { @() }

    foreach ($directory in @('UI', 'NUI')) {
        $directoryRoot = Join-Path $profileRoot $directory
        if (-not (Test-Path -LiteralPath $directoryRoot)) {
            continue
        }

        foreach ($file in Get-ChildItem -LiteralPath $directoryRoot -Recurse -File) {
            $relative = Normalize-AssetPath $file.FullName.Substring($profileRoot.Length + 1)
            $baseFile = Get-AssetPath $baseRoot $relative
            $profileHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $baseExists = Test-Path -LiteralPath $baseFile
            $baseHash = if ($baseExists) { (Get-FileHash -LiteralPath $baseFile -Algorithm SHA256).Hash } else { $null }
            $wytInfo = Get-WytInfo $file.FullName

            if ($baseExists -and $profileHash -eq $baseHash) {
                continue
            }

            $profileListed = $profileCatalog -contains $relative
            $baseListed = $baseCatalog -contains $relative
            $catalogRelation = if ($profileListed -and $baseListed) { 'common' } elseif ($profileListed) { 'profile-only' } elseif ($baseListed) { 'base-only-file' } else { 'not-listed' }
            $blocked = Test-BlockedByLauncherPolicy $relative
            $unsupportedWytMagic = $null -ne $wytInfo -and $wytInfo.magic -ne 'WT10'
            $approval = @($manifestApprovals | Where-Object { (Normalize-AssetPath $_.path) -eq $relative }) | Select-Object -First 1
            $approvalHashMatches = $null -ne $approval -and $approval.sha256 -eq $profileHash
            $status = if ($blocked -or $unsupportedWytMagic) { 'blocked_non_runtime' } elseif ($approvalHashMatches) { 'approved_manifest' } elseif ($null -ne $approval) { 'approval_hash_mismatch' } else { 'pending_visual' }
            $reason = if ($blocked) { 'launcher_policy_exclusion' } elseif ($unsupportedWytMagic) { 'unsupported_wyt_magic' } elseif (-not $baseExists) { 'base_file_missing' } elseif ($approvalHashMatches) { 'manifest_approval' } elseif ($null -ne $approval) { 'manifest_approval_hash_mismatch' } else { 'sha256_differs_from_7670' }

            $records.Add([ordered]@{
                profile = $profile
                path = $relative
                extension = [IO.Path]::GetExtension($relative).ToLowerInvariant()
                format = Get-AssetFormat $relative
                wyt = $wytInfo
                length = $file.Length
                sha256 = $profileHash
                baseExists = $baseExists
                baseLength = if ($baseExists) { (Get-Item -LiteralPath $baseFile).Length } else { $null }
                baseSha256 = $baseHash
                profileCatalogListed = $profileListed
                baseCatalogListed = $baseListed
                catalogRelation = $catalogRelation
                sourceConsumer = if ($baseListed) { 'TextureManager.UITextureListN' } else { 'not-listed-in-7670-UITextureListN' }
                status = $status
                reason = $reason
                manifestApprovalPresent = $null -ne $approval
                manifestApprovalHashMatches = $approvalHashMatches
                approvalReason = if ($approval) { $approval.reason } else { $null }
                approved = $approvalHashMatches
            })
        }
    }
}

$summary = foreach ($profile in @('7559', '7600', '7662')) {
    $profileRecords = @($records | Where-Object { $_['profile'] -eq $profile })
    [ordered]@{
        profile = $profile
        candidates = $profileRecords.Count
        pendingVisual = @($profileRecords | Where-Object { $_['status'] -eq 'pending_visual' }).Count
        blockedNonRuntime = @($profileRecords | Where-Object { $_['status'] -eq 'blocked_non_runtime' }).Count
        approvedManifest = @($profileRecords | Where-Object { $_['status'] -eq 'approved_manifest' }).Count
        approvalHashMismatch = @($profileRecords | Where-Object { $_['status'] -eq 'approval_hash_mismatch' }).Count
    }
}

$document = [ordered]@{
    schemaVersion = 1
    clientVersion = 7670
    baseProfile = '7670'
    generatedAt = (Get-Date).ToString('o')
    source = 'Client interface trees and UITextureListN.bin'
    summary = @($summary)
    candidates = @($records | Sort-Object { $_['profile'] }, { $_['status'] }, { $_['path'] })
}

$parent = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$document | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Output=$OutputPath"
$summary | ForEach-Object { Write-Output ("Profile={0} Candidates={1} PendingVisual={2} BlockedNonRuntime={3} ApprovedManifest={4} ApprovalHashMismatch={5}" -f $_.profile, $_.candidates, $_.pendingVisual, $_.blockedNonRuntime, $_.approvedManifest, $_.approvalHashMismatch) }
