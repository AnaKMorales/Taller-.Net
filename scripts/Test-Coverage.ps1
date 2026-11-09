param(
    [string]$ResultsDirectory = (Join-Path $PSScriptRoot '../coverage'),
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runDirectory = Join-Path $ResultsDirectory ([Guid]::NewGuid().ToString('N'))
$summary = @('| Capa | Líneas cubiertas | Cobertura |', '| --- | ---: | ---: |')
$belowThreshold = @()

foreach ($layer in @('Domain', 'Application')) {
    $assembly = "UltimaMilla.$layer"
    $project = Join-Path $repo "tests/Unit/$assembly.Tests/$assembly.Tests.csproj"
    $layerDirectory = Join-Path $runDirectory $layer
    $testArgs = @('test', $project, '-c', 'Release', '--collect', 'XPlat Code Coverage',
        '--results-directory', $layerDirectory)
    if ($NoBuild) { $testArgs += '--no-build' }

    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { throw "Fallaron las pruebas de $layer." }

    $reports = @(Get-ChildItem -LiteralPath $layerDirectory -Filter 'coverage.cobertura.xml' -Recurse)
    if ($reports.Count -ne 1) { throw "Se esperaba un reporte de cobertura para $layer." }

    [xml]$report = Get-Content -LiteralPath $reports[0].FullName -Raw
    $lines = @($report.SelectNodes("/coverage/packages/package[@name='$assembly']/classes/class/lines/line"))
    if ($lines.Count -eq 0) { throw "El reporte de $layer no contiene líneas medibles." }
    $covered = @($lines | Where-Object { [int]$_.hits -gt 0 }).Count
    $percentage = 100.0 * $covered / $lines.Count
    $summary += '| {0} | {1}/{2} | {3:F2} % |' -f $layer, $covered, $lines.Count, $percentage

    # Compara los conteos exactos para exigir más de 70 %, incluso si el valor redondeado es 70,00.
    if (100 * $covered -le 70 * $lines.Count) { $belowThreshold += $layer }
}

$summary | Write-Output
if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value (@('### Cobertura de líneas', '') + $summary)
}
if ($belowThreshold.Count -gt 0) {
    throw "La cobertura debe superar el 70 % en cada capa. Por debajo del requisito: $($belowThreshold -join ', ')."
}
