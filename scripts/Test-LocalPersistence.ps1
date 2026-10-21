$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $repoPath 'deploy/.env.postgres'
if (-not (Test-Path -LiteralPath $settingsPath)) {
    throw 'Primero ejecuta scripts/Start-LocalPostgres.ps1.'
}
$settings = ConvertFrom-StringData ([System.IO.File]::ReadAllText($settingsPath))
function Quote-ConnectionValue([string] $Value) { '"' + $Value.Replace('"', '""') + '"' }
$port = [int]$settings.POSTGRES_PORT
if ($port -lt 1 -or $port -gt 65535) { throw 'POSTGRES_PORT no es válido.' }
$connectionString = 'Host=127.0.0.1;Port={0};Database={1};Username={2};Password={3}' -f $port,
    (Quote-ConnectionValue $settings.POSTGRES_DB), (Quote-ConnectionValue $settings.POSTGRES_USER),
    (Quote-ConnectionValue $settings.POSTGRES_PASSWORD)
$previousConnection = $env:ConnectionStrings__UltimaMilla
$previousTestConnection = $env:ULTIMAMILLA_TEST_CONNECTION_STRING

Push-Location $repoPath
try {
    $env:ConnectionStrings__UltimaMilla = $connectionString
    $env:ULTIMAMILLA_TEST_CONNECTION_STRING = $connectionString
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo restaurar dotnet-ef.' }
    & dotnet ef database update --project src/UltimaMilla.Infrastructure
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo aplicar la migración a PostgreSQL local.' }
    & dotnet test tests/Integration/UltimaMilla.Infrastructure.IntegrationTests/UltimaMilla.Infrastructure.IntegrationTests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de integración.' }
}
finally {
    $env:ConnectionStrings__UltimaMilla = $previousConnection
    $env:ULTIMAMILLA_TEST_CONNECTION_STRING = $previousTestConnection
    Pop-Location
}
