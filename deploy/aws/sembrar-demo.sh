#!/usr/bin/env bash
# Carga los datos de demostración (dos operadores con configuración distinta) en la instancia de AWS y guarda las cuentas
# en un archivo local fuera del repositorio. Todo desde tu máquina, por SSM Run Command (sin SSH).
#
# Qué hace, en orden:
#   1. Enciende WEB_ENTORNO=Development en la instancia (recrea solo el contenedor web) y espera a que responda.
#   2. Llama UNA sola vez a POST /dev/datos-demo (no reintenta: un reintento crearía operadores duplicados), pasándole una
#      clave ALEATORIA generada en cada ejecución (la instancia es pública: no se usa la clave fija de desarrollo).
#   3. Apaga Development (también si algo falla o cortás con Ctrl-C) y verifica que /dev/datos-demo vuelve a dar 404.
#   4. Guarda las cuentas y esa clave en ~/demo-cuentas.txt con permisos 600; es el único lugar donde queda. No se imprime
#      por pantalla, no viaja en argumentos de comandos (va por stdin/variable de entorno) y el servidor no la devuelve.
#
# Mientras Development está encendido, /dev/* queda abierto a internet sin login: el script lo deja encendido el mínimo tiempo.
#
#   Uso (desde cualquier carpeta):  deploy/aws/sembrar-demo.sh
#   Opcional: CUENTAS_ARCHIVO=/ruta/fuera/del/repo.txt deploy/aws/sembrar-demo.sh
#   Requiere: terraform apply hecho en deploy/terraform/envs/dev, aws CLI (v1 o v2), jq, curl y openssl.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
ENV_DIR="$RAIZ/deploy/terraform/envs/dev"
ARCHIVO="${CUENTAS_ARCHIVO:-$HOME/demo-cuentas.txt}"
COMPOSE='docker compose --env-file .env -f compose.yaml -f compose.aws.yaml --profile app'

for comando in aws jq curl terraform openssl; do
  command -v "$comando" >/dev/null 2>&1 || { echo "ERROR: falta '$comando' en el PATH." >&2; exit 1; }
done

# Las cuentas llevan claves: el archivo tiene que quedar fuera del repositorio (para no commitearlo por error).
directorio="$(cd "$(dirname "$ARCHIVO")" 2>/dev/null && pwd -P)" || { echo "ERROR: no existe la carpeta de $ARCHIVO." >&2; exit 1; }
case "$directorio/" in
  "$RAIZ"/*) echo "ERROR: $ARCHIVO está dentro del repositorio; elegí una ruta fuera de él (CUENTAS_ARCHIVO)." >&2; exit 1 ;;
esac

# Clave aleatoria de esta ejecución: 20 caracteres alfanuméricos (~119 bits). Se arma antes de tocar la instancia.
clave=$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | cut -c1-20)
[ "${#clave}" -eq 20 ] || { echo "ERROR: no se pudo generar la clave aleatoria." >&2; exit 1; }

salida=$(terraform -chdir="$ENV_DIR" output -json github_variables)
REGION=$(jq -r .AWS_REGION <<<"$salida")
INSTANCIA=$(jq -r .AWS_INSTANCE_ID <<<"$salida")
BASE="https://$(terraform -chdir="$ENV_DIR" output -raw dominio)"
export AWS_DEFAULT_REGION="$REGION"

# Ejecuta un comando en la instancia y espera el resultado. Solo usa --output json/text (compatible con AWS CLI v1 y v2).
remoto() { # comentario, comando
  local id estado inicio=$SECONDS
  id=$(aws ssm send-command --instance-ids "$INSTANCIA" --document-name AWS-RunShellScript --comment "$1" \
    --parameters "$(jq -nc --arg c "$2" '{commands: [$c]}')" --query Command.CommandId --output text)
  while :; do
    estado=$(aws ssm get-command-invocation --command-id "$id" --instance-id "$INSTANCIA" \
      --query Status --output text 2>/dev/null || echo "Pending")
    case "$estado" in
      Pending | InProgress | Delayed) ;;
      *) break ;;
    esac
    if [ $((SECONDS - inicio)) -ge 300 ]; then estado="SinRespuesta"; break; fi
    sleep 5
  done
  if [ "$estado" != "Success" ]; then
    echo "ERROR: el comando remoto '$1' terminó con estado '$estado'." >&2
    aws ssm get-command-invocation --command-id "$id" --instance-id "$INSTANCIA" \
      --query '[StandardOutputContent, StandardErrorContent]' --output text >&2 || true
    return 1
  fi
}

# Espera hasta que GET <ruta> devuelva el código esperado.
esperar() { # ruta, código esperado, segundos máximos
  local codigo inicio=$SECONDS
  while :; do
    codigo=$(curl -s -o /dev/null -m 8 -w '%{http_code}' "$BASE$1" || true)
    [ "$codigo" = "$2" ] && return 0
    [ $((SECONDS - inicio)) -ge "$3" ] && return 1
    sleep 3
  done
}

encender() { remoto "Encender Development (datos demo)" "cd /opt/ultimamilla/deploy && sed -i '/^WEB_ENTORNO=/d' .env && echo WEB_ENTORNO=Development >> .env && $COMPOSE up -d --no-build web"; }
apagar() { remoto "Apagar Development (datos demo)" "cd /opt/ultimamilla/deploy && sed -i '/^WEB_ENTORNO=/d' .env && $COMPOSE up -d --no-build web"; }

DEV_ENCENDIDO=0
respuesta=""
limpiar() {
  if [ -n "$respuesta" ]; then rm -f "$respuesta"; fi
  if [ "$DEV_ENCENDIDO" = "1" ]; then
    echo "==> Apagando Development (limpieza)" >&2
    apagar || echo "ATENCIÓN: no se pudo apagar Development. Hacelo a mano: quitar WEB_ENTORNO de /opt/ultimamilla/deploy/.env y recrear web." >&2
  fi
}
trap limpiar EXIT

echo "==> Comprobando que la plataforma responde ($BASE)"
esperar /login 200 30 || { echo "ERROR: $BASE/login no responde 200." >&2; exit 1; }

echo "==> Encendiendo Development en la instancia $INSTANCIA"
DEV_ENCENDIDO=1
encender
echo "==> Esperando a que web responda en modo Development"
esperar /dev/cache/metricas 200 120 || { echo "ERROR: web no volvió en modo Development a tiempo." >&2; exit 1; }

echo "==> Creando los datos de demostración (una sola llamada)"
umask 077
respuesta=$(mktemp)
# La clave viaja en el cuerpo (por stdin), nunca en los argumentos de curl ni de jq, que se ven con "ps".
codigo=$(CLAVE="$clave" jq -nc '{contrasena: env.CLAVE}' |
  curl -sS -m 180 -o "$respuesta" -w '%{http_code}' -X POST -H 'Content-Type: application/json' --data-binary @- \
    "$BASE/dev/datos-demo") || codigo="000"
if [ "$codigo" != "200" ] || ! jq -e '.operadores | length == 2' "$respuesta" >/dev/null 2>&1; then
  echo "ERROR: /dev/datos-demo respondió '$codigo' y no devolvió los dos operadores." >&2
  echo "       No se reintenta para no duplicar operadores; revisá en el backoffice si se llegó a crear alguno." >&2
  if [ "$codigo" != "000" ]; then head -c 400 "$respuesta" >&2; echo >&2; fi
  exit 1
fi

# Se guardan primero las cuentas: si algo falla después, igual quedan registradas.
{
  echo "# Cuentas de demostración - $(date '+%Y-%m-%d %H:%M') - $BASE"
  CLAVE="$clave" jq -r --arg base "$BASE" '.operadores[] |
    "OPERADOR: \(.operador)",
    "  Entrar (administrador y comercio): \($base)/login",
    "  PWA del repartidor:                \($base)/repartidor/",
    "  Administrador: \(.emailAdministrador)",
    "  Comercio:      \(.emailComercio)",
    "  Repartidor:    \(.emailRepartidor)",
    "  Contraseña:    \(env.CLAVE)",
    "  Seguimiento público, visita pendiente: \($base)/seguimiento/\(.seguimientoPendiente)",
    "  Seguimiento público, entrega parcial:  \($base)/seguimiento/\(.seguimientoParcial)",
    "  Seguimiento público, no entregado:     \($base)/seguimiento/\(.seguimientoFallido)",
    "  Qué tiene de distinto:",
    (.configuracion[] | "    - \(.)"),
    ""' "$respuesta"
} >>"$ARCHIVO"
chmod 600 "$ARCHIVO"

echo "==> Apagando Development"
apagar
DEV_ENCENDIDO=0

echo "==> Verificando que /dev/datos-demo vuelve a dar 404"
# GET sobre una ruta que solo acepta POST: 404 si Development está apagado, 405 si sigue encendido. No crea datos.
if ! esperar /dev/datos-demo 404 120; then
  echo "ERROR: /dev/datos-demo no volvió a 404; Development podría seguir encendido. Revisá la instancia." >&2
  exit 1
fi

echo "==> Listo."
echo "    Operadores creados: $(jq -r '[.operadores[].operador] | join(", ")' "$respuesta")"
echo "    Cuentas guardadas en $ARCHIVO (permisos 600). Las claves no se muestran por pantalla."
