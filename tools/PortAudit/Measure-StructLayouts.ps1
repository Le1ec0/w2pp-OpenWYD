Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$referenceRoot = Join-Path $workspaceRoot 'Backup\Tools\ReferenceSources\TMProject2GlobalClient'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$probeSource = Join-Path $PSScriptRoot 'StructLayoutProbe.cpp'

if (-not (Test-Path -LiteralPath $vswhere)) {
    throw "vswhere.exe not found: $vswhere"
}
if (-not (Test-Path -LiteralPath $probeSource)) {
    throw "Probe source not found: $probeSource"
}

$installationPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($installationPath)) {
    throw 'Visual Studio C++ x86/x64 build tools were not found.'
}

$vcvars = Join-Path $installationPath 'VC\Auxiliary\Build\vcvarsall.bat'
if (-not (Test-Path -LiteralPath $vcvars)) {
    throw "vcvarsall.bat not found: $vcvars"
}

$profiles = @(
    [pscustomobject]@{
        Name = '7.69 client'; Define = 'PROBE_CLIENT'
        Include = Join-Path $referenceRoot 'Projects\TMProject'
    },
    [pscustomobject]@{
        Name = '7.69 DBSrv common header'; Define = 'PROBE_769_DBSRV'
        Include = Join-Path $referenceRoot 'Servidor\Source\Code'
    },
    [pscustomobject]@{
        Name = '7.69 TMSrv header'; Define = 'PROBE_769_TMSRV'
        # TMSrv's ProcessClientMessage.h includes ..\Basedef.h. Its adjacent TMSrv\Basedef.h
        # is a stale copy without SecLearnedSkill and is not the effective attack ABI.
        Include = Join-Path $referenceRoot 'Servidor\Source\Code'
    },
    [pscustomobject]@{
        Name = 'W2PP shared server header'; Define = 'PROBE_W2PP'
        Include = Join-Path $workspaceRoot 'Server\W2PP\Source\Code'
    }
)

foreach ($profile in $profiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $profile.Include 'Basedef.h'))) {
        throw "Basedef.h not found for profile '$($profile.Name)': $($profile.Include)"
    }
}

$tempDirectory = Join-Path $PSScriptRoot ('.struct-layout-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDirectory | Out-Null
$serverLayouts = @{}
$serverSoulOffsets = @{}
$w2ppRegenHpOffset = $null

try {
    foreach ($profile in $profiles) {
        $output = Join-Path $tempDirectory $profile.Define
        $objectFile = "$output.obj"
        $executable = "$output.exe"
        $compileAndRun = 'call "{0}" x86 >nul && cl.exe /nologo /EHsc /std:c++17 /W0 /D{1} /I"{2}" /Fo"{3}" /Fe"{4}" "{5}" && "{4}"' -f `
            $vcvars, $profile.Define, $profile.Include, $objectFile, $executable, $probeSource

        Write-Output "--- $($profile.Name) (Win32 ABI) ---"
        $probeOutput = @(& $env:ComSpec /d /s /c $compileAndRun)
        $probeExitCode = $LASTEXITCODE
        $probeOutput | ForEach-Object { Write-Output $_ }
        if ($probeExitCode -ne 0) {
            throw "Layout probe failed for '$($profile.Name)' with exit code $probeExitCode."
        }

        if ($profile.Define -in @('PROBE_769_DBSRV', 'PROBE_769_TMSRV', 'PROBE_W2PP')) {
            $layout = [ordered]@{}
            foreach ($name in @(
                'sizeof.MOBEXTRA',
                'offsetof.MOBEXTRA.SecLearnedSkill',
                    'sizeof.MOBEXTRA.SecLearnedSkill',
                    'offsetof.MOBEXTRA.Soul')) {
                $line = $probeOutput | Where-Object { $_ -match ('^{0}=(\d+)$' -f [regex]::Escape($name)) } | Select-Object -First 1
                if ($null -eq $line) {
                    throw "Profile '$($profile.Name)' omitted required ABI measurement '$name'."
                }
                $layout[$name] = [int](($line -split '=', 2)[1])
            }

            if ($layout['sizeof.MOBEXTRA'] -ne 552 -or $layout['sizeof.MOBEXTRA.SecLearnedSkill'] -ne 4) {
                throw "Profile '$($profile.Name)' reported unexpected MOBEXTRA size/field width: $($layout['sizeof.MOBEXTRA'])/$($layout['sizeof.MOBEXTRA.SecLearnedSkill'])."
            }
            $serverLayouts[$profile.Define] = '{0}:{1}:{2}' -f `
                $layout['sizeof.MOBEXTRA'],
                $layout['offsetof.MOBEXTRA.SecLearnedSkill'],
                $layout['sizeof.MOBEXTRA.SecLearnedSkill']
            $serverSoulOffsets[$profile.Define] = $layout['offsetof.MOBEXTRA.Soul']
        }

        if ($profile.Define -eq 'PROBE_W2PP') {
            $line = $probeOutput | Where-Object { $_ -match '^offsetof\.STRUCT_MOB\.RegenHP=(\d+)$' } | Select-Object -First 1
            if ($null -eq $line) {
                throw "Profile '$($profile.Name)' omitted required ABI measurement 'offsetof.STRUCT_MOB.RegenHP'."
            }
            $w2ppRegenHpOffset = [int](($line -split '=', 2)[1])
        }
    }

    $distinctServerLayouts = @($serverLayouts.Values | Select-Object -Unique)
    if ($distinctServerLayouts.Count -ne 1) {
        $details = ($serverLayouts.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value }) -join '; '
        throw "Effective 7.69/W2PP MOBEXTRA ABI measurements disagree: $details"
    }
    Write-Output "server.MOBEXTRA.abi=$($distinctServerLayouts[0])"

    $distinctSoulOffsets = @($serverSoulOffsets.Values | Select-Object -Unique)
    if ($distinctSoulOffsets.Count -ne 1) {
        $details = ($serverSoulOffsets.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value }) -join '; '
        throw "Effective 7.69/W2PP MOBEXTRA Soul offsets disagree: $details"
    }
    Write-Output "server.MOBEXTRA.soul-offset=$($distinctSoulOffsets[0])"
    if ($w2ppRegenHpOffset -ne 802) {
        throw "W2PP STRUCT_MOB.RegenHP offset changed from the account-file ABI: $w2ppRegenHpOffset"
    }
    Write-Output "server.W2PP.MOB.RegenHP-offset=$w2ppRegenHpOffset"
}
finally {
    $resolvedTempDirectory = [System.IO.Path]::GetFullPath($tempDirectory)
    $resolvedToolDirectory = [System.IO.Path]::GetFullPath($PSScriptRoot)
    $requiredPrefix = $resolvedToolDirectory.TrimEnd('\') + '\'
    $isOwnedTemporaryDirectory =
        $resolvedTempDirectory.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Path]::GetFileName($resolvedTempDirectory).StartsWith('.struct-layout-', [System.StringComparison]::Ordinal)

    if ($isOwnedTemporaryDirectory -and (Test-Path -LiteralPath $resolvedTempDirectory)) {
        Remove-Item -LiteralPath $resolvedTempDirectory -Recurse -Force
    }
}
