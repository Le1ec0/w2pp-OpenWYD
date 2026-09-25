Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$includeRoot = Join-Path $workspaceRoot 'Backup\Tools\ReferenceSources\TMProject2GlobalClient\Projects\TMProject'
$programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
$probeSource = Join-Path $PSScriptRoot 'V769UpdateEquipGoldenProbe.cpp'
$fixturePath = Join-Path $workspaceRoot 'Server\tests\WydCdk.Engine.Tests\Fixtures\V769\update-equip.golden.hex'

if (-not (Test-Path -LiteralPath $vswhere)) { throw "vswhere.exe not found: $vswhere" }
if (-not (Test-Path -LiteralPath (Join-Path $includeRoot 'Basedef.h'))) { throw "7.69 client header not found: $includeRoot" }

$installationPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($installationPath)) { throw 'Visual Studio C++ x86/x64 build tools were not found.' }

$vcvars = Join-Path $installationPath 'VC\Auxiliary\Build\vcvarsall.bat'
$tempDirectory = Join-Path $PSScriptRoot ('.v769-update-equip-golden-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDirectory | Out-Null
try {
    $objectFile = Join-Path $tempDirectory 'probe.obj'
    $executable = Join-Path $tempDirectory 'probe.exe'
    $compileAndRun = 'call "{0}" x86 >nul && cl.exe /nologo /EHsc /std:c++17 /W0 /I"{1}" /Fo"{2}" /Fe"{3}" "{4}" >nul && "{3}"' -f $vcvars, $includeRoot, $objectFile, $executable, $probeSource
    & $env:ComSpec /d /s /c $compileAndRun
    if ($LASTEXITCODE -ne 0) { throw "7.69 UpdateEquip golden probe failed with exit code $LASTEXITCODE." }

    $hex = (& $executable | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "7.69 UpdateEquip golden probe failed with exit code $LASTEXITCODE." }
    if ($hex -notmatch '^[0-9A-Fa-f]{112}$') { throw "Unexpected 7.69 UpdateEquip payload: expected 112 hex characters, received $($hex.Length)." }

    $fixtureDirectory = Split-Path -Parent $fixturePath
    New-Item -ItemType Directory -Path $fixtureDirectory -Force | Out-Null
    [System.IO.File]::WriteAllText($fixturePath, $hex + [Environment]::NewLine, [System.Text.Encoding]::ASCII)
    Write-Output "Generated 7.69 UpdateEquip payload fixture: $fixturePath"
}
finally {
    $resolvedTemp = [System.IO.Path]::GetFullPath($tempDirectory)
    $resolvedToolRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
    if ($resolvedTemp.StartsWith($resolvedToolRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemp).StartsWith('.v769-update-equip-golden-', [System.StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
