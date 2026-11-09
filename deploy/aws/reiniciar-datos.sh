#!/usr/bin/env bash
# BORRA TODOS LOS DATOS de la instancia de AWS (volúmenes de Postgres y RabbitMQ) y la deja vacía y migrada.
# Sirve para empezar de cero antes de sembrar la demo (deploy/aws/sembrar-demo.sh). No toca las claves de Data Protection
# (las sesiones de cookies sobreviven) ni los secretos de SSM: Postgres se recrea con la misma contraseña.
#
# Qué hace en la instancia, por SSM:
#   1. detiene web y worker, elimina el contenedor de Postgres y borra los volúmenes de Postgres y RabbitMQ;
#   2. levanta todo de nuevo: el migrador crea las tablas desde cero y web y worker arrancan al terminar.
#
#   Uso:  deploy/aws/reiniciar-datos.sh          (pide escribir el dominio para confirmar)
#   Sin preguntar (ej. en un script):  CONFIRMAR=<dominio> deploy/aws/reiniciar-datos.sh
#   Requiere: terraform apply hecho en deploy/terraform/envs/dev, aws CLI (v1 o v2), jq y curl.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
ENV_DIR="$RAIZ/deploy/terraform/envs/dev"
COMPOSE='docker compose --env-file .env -f compose.yaml -f compose.aws.yaml --profile app'
# El proyecto de compose se llama "ultimamilla-local" (name: en compose.yaml): los volúmenes llevan ese prefijo.
VOLUMENES='ultimamilla-local_postgres_data ultimamilla-local_rabbitmq_data'

for comando in aws jq curl terraform; do
  command -v "$comando" >/dev/null 2>&1 || { echo "ERROR: falta '$comando' en el PATH." >&2; exit 1; }
done

salida=$(terraform -chdir="$ENV_DIR" output -json github_variables)
REGION=$(jq -r .AWS_REGION <<<"$salida")
INSTANCIA=$(jq -r .AWS_INSTANCE_ID <<<"$salida")
DOMINIO=$(terraform -chdir="$ENV_DIR" output -raw dominio)
export AWS_DEFAULT_REGION="$REGION"

echo "ATENCIÓN: esto borra TODOS los datos de https://$DOMINIO (operadores, usuarios, envíos, rutas, colas)."
echo "          Instancia: $INSTANCIA ($REGION). No se puede deshacer: no hay respaldos."
confirmacion="${CONFIRMAR:-}"
if [ -z "$confirmacion" ]; then
  read -r -p "Para continuar escribí el dominio exacto: " confirmacion
fi
if [ "$confirmacion" != "$DOMINIO" ]; then
  echo "Cancelado: la confirmación no coincide con '$DOMINIO'. No se tocó nada." >&2
  exit 1
fi

echo "==> Borrando datos y recreando los servicios en la instancia"
comando="cd /opt/ultimamilla/deploy && $COMPOSE stop web worker && $COMPOSE rm -sf postgres rabbitmq && docker volume rm $VOLUMENES && $COMPOSE up -d --no-build"
id=$(aws ssm send-command --instance-ids "$INSTANCIA" --document-name AWS-RunShellScript --comment "Reiniciar datos" \
  --parameters "$(jq -nc --arg c "$comando" '{commands: [$c]}')" --timeout-seconds 900 \
  --query Command.CommandId --output text)

inicio=$SECONDS
estado="Pending"
while :; do
  estado=$(aws ssm get-command-invocation --command-id "$id" --instance-id "$INSTANCIA" --query Status --output text 2>/dev/null || echo "Pending")
  case "$estado" in
    Pending | InProgress | Delayed) ;;
    *) break ;;
  esac
  if [ $((SECONDS - inicio)) -ge 960 ]; then estado="SinRespuesta"; break; fi
  sleep 10
done
echo "==> Estado: $estado"
aws ssm get-command-invocation --command-id "$id" --instance-id "$INSTANCIA" \
  --query '[StandardOutputContent, StandardErrorContent]' --output text || true
[ "$estado" = "Success" ] || { echo "ERROR: el reinicio no terminó bien (estado '$estado'). Revisá el log de arriba." >&2; exit 1; }

echo "==> Esperando a que la plataforma responda"
inicio=$SECONDS
until [ "$(curl -s -o /dev/null -m 8 -w '%{http_code}' "https://$DOMINIO/login" || true)" = "200" ]; do
  [ $((SECONDS - inicio)) -ge 180 ] && { echo "ERROR: https://$DOMINIO/login no volvió a responder 200." >&2; exit 1; }
  sleep 4
done
echo "==> Listo: base vacía y migrada. Siguiente paso: deploy/aws/sembrar-demo.sh"
