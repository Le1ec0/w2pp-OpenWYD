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
        Include = Join-Path $referenceRoot 'Servidor\Source\Code\TMSrv'
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

try {
    foreach ($profile in $profiles) {
        $output = Join-Path $tempDirectory $profile.Define
        $objectFile = "$output.obj"
        $executable = "$output.exe"
        $compileAndRun = 'call "{0}" x86 >nul && cl.exe /nologo /EHsc /std:c++17 /W0 /D{1} /I"{2}" /Fo"{3}" /Fe"{4}" "{5}" && "{4}"' -f `
            $vcvars, $profile.Define, $profile.Include, $objectFile, $executable, $probeSource

        Write-Output "--- $($profile.Name) (Win32 ABI) ---"
        & $env:ComSpec /d /s /c $compileAndRun
        if ($LASTEXITCODE -ne 0) {
            throw "Layout probe failed for '$($profile.Name)' with exit code $LASTEXITCODE."
        }
    }
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
