param([Parameter(Mandatory = $true)][string]$ResultsDirectory)

$ErrorActionPreference = 'Stop'
$reports = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter '*.trx' -Recurse)
if ($reports.Count -eq 0) { throw 'No se encontraron resultados TRX.' }
$total = 0
$omitted = @()
foreach ($file in $reports) {
    [xml]$report = Get-Content -LiteralPath $file.FullName -Raw
    $results = @($report.SelectNodes("//*[local-name()='UnitTestResult']"))
    $total += $results.Count
    $omitted += $results | Where-Object { $_.outcome -ne 'Passed' -and $_.outcome -ne 'Failed' }
}
if ($total -eq 0) { throw 'Los reportes no contienen pruebas ejecutadas.' }
if ($omitted.Count -gt 0) {
    $omitted | ForEach-Object { Write-Output "$($_.outcome): $($_.testName)" }
    throw "Hay $($omitted.Count) pruebas omitidas o incompletas. Revisar servicios y variables del runner."
}
Write-Output "Verificados $total resultados sin pruebas omitidas. Los fallos los informa dotnet test."
