Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$referenceRoot = Join-Path $workspaceRoot 'Backup\Tools\ReferenceSources\TMProject2GlobalClient'
$includeRoot = Join-Path $referenceRoot 'Projects\TMProject'
$programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
$probeSource = Join-Path $PSScriptRoot 'V769CharacterSelectionGoldenProbe.cpp'

if (-not (Test-Path -LiteralPath $vswhere)) { throw "vswhere.exe not found: $vswhere" }
if (-not (Test-Path -LiteralPath (Join-Path $includeRoot 'Basedef.h'))) { throw "7.69 client header not found: $includeRoot" }

$installationPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($installationPath)) { throw 'Visual Studio C++ x86/x64 build tools were not found.' }

$vcvars = Join-Path $installationPath 'VC\Auxiliary\Build\vcvarsall.bat'
$tempDirectory = Join-Path $PSScriptRoot ('.v769-selection-golden-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDirectory | Out-Null
try {
    $objectFile = Join-Path $tempDirectory 'probe.obj'
    $executable = Join-Path $tempDirectory 'probe.exe'
    $compileAndRun = 'call "{0}" x86 >nul && cl.exe /nologo /EHsc /std:c++17 /W0 /I"{1}" /Fo"{2}" /Fe"{3}" "{4}" >nul && "{3}"' -f $vcvars, $includeRoot, $objectFile, $executable, $probeSource
    & $env:ComSpec /d /s /c $compileAndRun
    if ($LASTEXITCODE -ne 0) { throw "7.69 selection golden probe failed with exit code $LASTEXITCODE." }
}
finally {
    $resolvedTemp = [System.IO.Path]::GetFullPath($tempDirectory)
    $resolvedToolRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
    if ($resolvedTemp.StartsWith($resolvedToolRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemp).StartsWith('.v769-selection-golden-', [System.StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
