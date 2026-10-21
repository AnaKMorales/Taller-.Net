$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$composePath = Join-Path $repoPath 'deploy/compose.postgres.yaml'
$settingsPath = Join-Path $repoPath 'deploy/.env.postgres'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Instala y abre Docker Desktop antes de ejecutar este script.'
}
& docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'El motor Docker no responde. Abre Docker Desktop.' }

if (-not (Test-Path -LiteralPath $settingsPath)) {
    $bytes = New-Object byte[] 24
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($bytes) } finally { $random.Dispose() }
    $password = [System.BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
    $settings = "POSTGRES_DB=ultimamilla`nPOSTGRES_USER=ultimamilla`nPOSTGRES_PORT=55432`nPOSTGRES_PASSWORD=$password`n"
    [System.IO.File]::WriteAllText($settingsPath, $settings, [System.Text.UTF8Encoding]::new($false))
    Write-Host 'Se creó deploy/.env.postgres con credenciales locales (excluidas de Git).'
}

& docker compose --env-file $settingsPath -f $composePath up -d --wait --wait-timeout 90 postgres
if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar PostgreSQL. Revisa el estado y los logs de Docker Compose.' }
Write-Host 'PostgreSQL está listo. Ejecuta scripts/Test-LocalPersistence.ps1 para migrar y probar.'
