$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $repoPath 'deploy/.env.postgres'
if (-not (Test-Path -LiteralPath $settingsPath)) {
    throw 'Primero ejecuta scripts/Start-LocalPostgres.ps1.'
}

# La contraseña no va en appsettings: se toma del .env local (excluido de Git) solo para esta ejecución.
$settings = ConvertFrom-StringData ([System.IO.File]::ReadAllText($settingsPath))
function Quote-ConnectionValue([string] $Value) { '"' + $Value.Replace('"', '""') + '"' }
$connectionString = 'Host=127.0.0.1;Port={0};Database={1};Username={2};Password={3}' -f [int]$settings.POSTGRES_PORT,
    (Quote-ConnectionValue $settings.POSTGRES_DB), (Quote-ConnectionValue $settings.POSTGRES_USER),
    (Quote-ConnectionValue $settings.POSTGRES_PASSWORD)
$previousConnection = $env:ConnectionStrings__UltimaMilla

Push-Location $repoPath
try {
    $env:ConnectionStrings__UltimaMilla = $connectionString
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo restaurar dotnet-ef.' }
    & dotnet ef database update --project src/UltimaMilla.Infrastructure
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron aplicar las migraciones.' }
    & dotnet run --project src/UltimaMilla.Web --launch-profile http
}
finally {
    $env:ConnectionStrings__UltimaMilla = $previousConnection
    Pop-Location
}
