$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$composePath = Join-Path $repoPath 'deploy/compose.redis.yaml'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Instala y abre Docker Desktop antes de ejecutar este script.'
}
& docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'El motor Docker no responde. Abre Docker Desktop.' }

# Docker escribe el progreso en stderr; en Windows PowerShell 5.1 eso cortaría el script con 'Stop'.
$ErrorActionPreference = 'Continue'
& docker compose -f $composePath up -d --wait --wait-timeout 60 redis
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar Redis. Revisa el estado y los logs de Docker Compose.' }
Write-Host 'Redis está listo en 127.0.0.1:56379.'
