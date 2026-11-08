#!/usr/bin/env bash
# Despliega en la EC2: baja la configuración de SSM, las imágenes de ECR y reinicia los contenedores con docker compose.
# Lo ejecuta el pipeline por SSM Run Command (como root, con el rol de la instancia): sin SSH ni contraseñas.
#   Uso: desplegar.sh <tag>      (el tag es el SHA del commit con el que el pipeline etiquetó las imágenes)
set -euo pipefail
export PATH="$PATH:/usr/local/bin"

TAG="${1:?Uso: desplegar.sh <tag-de-imagen>}"

# Datos de esta instancia (los escribe el user-data de Terraform): región, bucket, registro de ECR y ruta de SSM.
set -a
# shellcheck disable=SC1091
. /etc/ultimamilla.env
set +a

cd /opt/ultimamilla/deploy

echo "==> Configuración desde SSM (${RUTA_SSM})"
umask 077 # el .env contiene secretos: solo root lo lee
{
  aws ssm get-parameters-by-path --path "$RUTA_SSM" --recursive --with-decryption \
    --query 'Parameters[].[Name,Value]' --output text |
    while IFS=$'\t' read -r nombre valor; do echo "${nombre##*/}=${valor}"; done
  echo "WEB_IMAGE=${REGISTRO_ECR}/ultimamilla-web:${TAG}"
  echo "WORKER_IMAGE=${REGISTRO_ECR}/ultimamilla-worker:${TAG}"
  echo "RECEPTOR_IMAGE=${REGISTRO_ECR}/ultimamilla-receptor:${TAG}"
  echo "MIGRADOR_IMAGE=${REGISTRO_ECR}/ultimamilla-migrador:${TAG}"
} > .env.nuevo
mv .env.nuevo .env

echo "==> Login en ECR y descarga de imágenes (${TAG})"
aws ecr get-login-password | docker login --username AWS --password-stdin "$REGISTRO_ECR"

compose=(docker compose --env-file .env -f compose.yaml -f compose.aws.yaml --profile app)
"${compose[@]}" pull

echo "==> Levantando contenedores (el migrador corre primero; web y worker esperan a que termine bien)"
"${compose[@]}" up -d --no-build --remove-orphans

echo "==> Estado"
"${compose[@]}" ps

# /health todavía no existe: se comprueba que web y worker estén corriendo.
for servicio in web worker traefik; do
  if [ "$("${compose[@]}" ps --status running --quiet "$servicio" | wc -l)" -eq 0 ]; then
    echo "ERROR: el servicio $servicio no está corriendo" >&2
    "${compose[@]}" logs --tail 50 "$servicio" >&2 || true
    exit 1
  fi
done

docker image prune -f >/dev/null
echo "$TAG" > /opt/ultimamilla/tag-actual
echo "==> Listo: ${TAG}"
