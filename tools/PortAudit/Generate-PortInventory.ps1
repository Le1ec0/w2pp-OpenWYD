[CmdletBinding()]
param(
    [string]$ServerRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ServerRoot)) {
    $ServerRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

$sourceRoots = @(
    (Join-Path $ServerRoot 'src'),
    (Join-Path $ServerRoot 'tests')
)

$files = foreach ($root in $sourceRoots) {
    if (Test-Path -LiteralPath $root) {
        Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs'
    }
}

$classPattern = '\b(record\s+struct|record|class|interface|enum)\s+([A-Za-z_]\w*)'
$methodPattern = '^\s*(?:(?:public|private|protected|internal|static|async|virtual|override|sealed|new|partial|unsafe|extern|file|readonly)\s+)*(?:[A-Za-z_]\w*(?:<[^>]+>)?|void|Task|ValueTask|bool|int|long|short|byte|uint|ushort|ulong|decimal|string|object|[A-Za-z_]\w*\[\])\s+([A-Za-z_]\w*)\s*\('
$handlerPattern = 'TryParse\(|IsValid\(|TryApply|TryGet|TryBuild|TryPrepare|TryProcess|TrySpawn|TryRegister|TryPurchase|TryConsume|HandleAsync'
$rootUri = [Uri]::new(($ServerRoot.TrimEnd('\') + '\'))

Write-Output '# Inventario mecanico do port'
Write-Output ''
Write-Output ('Gerado em: {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Write-Output ('Raiz: `{0}`' -f $ServerRoot)
Write-Output ''
Write-Output '| Arquivo | Linha | Tipo | Nome | Classificacao |'
Write-Output '|---|---:|---|---|---|'

foreach ($file in $files | Sort-Object FullName) {
    $relative = $rootUri.MakeRelativeUri([Uri]::new($file.FullName)).ToString().Replace('\', '/')
    $currentType = ''
    $lineNumber = 0

    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++

        $classMatch = [regex]::Match($line, $classPattern)
        if ($classMatch.Success) {
            $currentType = $classMatch.Groups[2].Value
            Write-Output ('| `{0}` | {1} | {2} | `{3}` | type |' -f $relative, $lineNumber, $classMatch.Groups[1].Value, $currentType)
        }

        $methodMatch = [regex]::Match($line, $methodPattern)
        if ($methodMatch.Success -and $line -match '\)\s*(?:\{|=>|:)') {
            $name = $methodMatch.Groups[1].Value
            $classification = if ($line -match $handlerPattern -or $name -match '^(Try|Handle|Parse|ToFrame|Read|Write|Save|Load)') { 'port/entry candidate' } else { 'method' }
            $qualifiedName = if ([string]::IsNullOrWhiteSpace($currentType)) { $name } else { '{0}.{1}' -f $currentType, $name }
            Write-Output ('| `{0}` | {1} | method | `{2}` | {3} |' -f $relative, $lineNumber, $qualifiedName, $classification)
        }
    }
}
