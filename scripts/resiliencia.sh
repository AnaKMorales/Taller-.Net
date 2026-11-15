#!/usr/bin/env bash
# Pruebas de resiliencia del entorno LOCAL (deploy/compose.yaml). Apaga de a un servicio por vez
# (redis, rabbitmq, postgres, receptor), hace una acción de prueba, mira los logs de web y worker,
# lo vuelve a prender y verifica que el sistema se recupere. Los resultados se cargan en docs/resiliencia.md.
#
# Solo usa "docker compose stop/start": no borra contenedores ni volúmenes, nunca hace "down".
# NO es para AWS: se niega a correr si DOCKER_HOST apunta a otra máquina.
#
# Requisitos: el entorno completo levantado con la Web en Development y los webhooks a destinos privados:
#   cd deploy && WEB_ENTORNO=Development WEBHOOKS_PERMITIR_DESTINOS_PRIVADOS=true \
#     docker compose --env-file .env --profile app up -d --build
# Herramientas: docker (con compose), curl y python3.
#
# Variables opcionales:
#   WEB (http://127.0.0.1:8080)   RECEPTOR (http://127.0.0.1:58080)   ESPERA (15 s con el servicio apagado)
#   SALIDA (archivo de salida; por defecto en $HOME, fuera del repo)
set -uo pipefail

REPO=$(cd "$(dirname "$0")/.." && pwd)
DEPLOY="$REPO/deploy"
WEB=${WEB:-http://127.0.0.1:${WEB_PORT:-8080}}
RECEPTOR=${RECEPTOR:-http://127.0.0.1:${RECEPTOR_PORT:-58080}}
RECEPTOR_INTERNO=${RECEPTOR_INTERNO:-http://receptor:8080}
ESPERA=${ESPERA:-15}
# Operador del escenario demo (0 = A, 1 = B). Por defecto B, el de los resultados de docs/resiliencia.md;
# desde el arreglo de DatosDemo.SimularAltaAsync el alta funciona con los dos.
OPERADOR=${OPERADOR:-1}
SERVICIOS=(redis rabbitmq postgres receptor)
SALIDA=${SALIDA:-$HOME/ultimamilla-resiliencia-$(date +%Y%m%d-%H%M%S).log}

# ---------- Controles previos (antes de tocar nada) ----------

if [[ -n "${DOCKER_HOST:-}" && "${DOCKER_HOST}" != unix://* ]]; then
  echo "DOCKER_HOST=$DOCKER_HOST apunta a otra máquina. Este script es solo para el entorno local." >&2; exit 1
fi
for herramienta in docker curl python3; do
  command -v "$herramienta" >/dev/null || { echo "Falta $herramienta." >&2; exit 1; }
done
[[ -f "$DEPLOY/.env" ]] || { echo "Falta deploy/.env (ver deploy/.env.example)." >&2; exit 1; }

dir_salida=$(cd "$(dirname "$SALIDA")" 2>/dev/null && pwd) || { echo "No existe la carpeta de $SALIDA." >&2; exit 1; }
if [[ "$dir_salida/" == "$REPO/"* ]]; then
  echo "La salida ($SALIDA) tiene que quedar fuera del repositorio." >&2; exit 1
fi

dc() { docker compose --project-directory "$DEPLOY" -f "$DEPLOY/compose.yaml" --env-file "$DEPLOY/.env" --profile app "$@"; }

for servicio in "${SERVICIOS[@]}" web worker; do
  if [[ -z "$(dc ps -q "$servicio" 2>/dev/null)" ]]; then
    echo "El servicio $servicio no está corriendo. Levantá el entorno completo primero (ver el encabezado)." >&2; exit 1
  fi
done

cat <<AVISO
Se van a apagar y volver a prender, de a uno, estos servicios LOCALES: ${SERVICIOS[*]}.
Mientras tanto la Web y el Worker van a mostrar errores (es lo que se quiere observar).
También se crean datos demo nuevos (POST /dev/datos-demo).
No se borran contenedores ni volúmenes. La salida se guarda en: $SALIDA
AVISO
read -r -p "¿Seguir? [s/N] " respuesta
[[ "$respuesta" =~ ^[sS]$ ]] || { echo "Cancelado."; exit 0; }

# Desde acá, todo lo que se muestra también va al archivo.
exec > >(tee -a "$SALIDA") 2>&1

# ---------- Restauración garantizada ----------

JAR=$(mktemp)
restaurado=0
restaurar() {
  (( restaurado )) && return
  restaurado=1
  echo
  echo "== Restaurando servicios (${SERVICIOS[*]}) =="
  dc start "${SERVICIOS[@]}" || echo "ATENCIÓN: no se pudo prender algún servicio; revisá con: docker compose ps"
  rm -f "$JAR"
}
trap restaurar EXIT
trap 'echo; echo "Interrumpido."; exit 130' INT TERM

# ---------- Utilidades ----------

json() { python3 -c "import sys,json;print(json.load(sys.stdin)$1)"; }
ahora_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }
codigo() { curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$@"; }

salud() {
  local id; id=$(dc ps -q "$1" 2>/dev/null)
  [[ -z "$id" ]] && { echo detenido; return; }
  docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$id"
}

esperar_sano() {
  local servicio=$1 limite=${2:-120} inicio=$SECONDS estado
  while (( SECONDS - inicio < limite )); do
    estado=$(salud "$servicio")
    [[ "$estado" == healthy || ( "$servicio" == receptor && "$estado" == running ) ]] && {
      echo "   $servicio: $estado ($((SECONDS - inicio)) s)"; return 0; }
    sleep 2
  done
  echo "   $servicio: sigue '$estado' después de $limite s"; return 1
}

RESUMEN=()
chequear() {  # chequear "descripción" "esperado" "obtenido"
  local marca=OK; [[ "$3" == $2 ]] || marca=DISTINTO
  printf '   [%-8s] %s: esperado %s, obtenido %s\n' "$marca" "$1" "$2" "$3"
  RESUMEN+=("$marca|$SERVICIO_ACTUAL|$1|esperado $2|obtenido $3")
}

logs_desde() {
  echo "   -- Logs de web y worker desde $1 (warnings/errores, últimas 25 líneas) --"
  dc logs --no-color --since "$1" web worker 2>&1 \
    | grep -iE 'warn|fail|error|excep|redis|rabbit|npgsql|postgres|reintent|espera|dlq' | tail -n 25 | sed 's/^/   | /'
  echo "   -- fin de logs --"
}

ready() { codigo "$WEB/ready"; }
seguimiento() { codigo "$WEB/api/seguimiento/$SEGUIMIENTO"; }
alta() { codigo -b "$JAR" -X POST "$WEB/dev/simular/alta"; }
limpiar_receptor() { curl -fsS -X DELETE "$RECEPTOR/recibidos" >/dev/null 2>&1 || true; }
metrica_cache() {  # Imprime "aciertos errores" de la caché de seguimiento.
  curl -fsS "$WEB/dev/cache/metricas" 2>/dev/null | python3 -c "
import sys,json
d=json.load(sys.stdin); d=d if isinstance(d,list) else [d]
c=[x for x in d if 'seguim' in str(x.get('cache','')).lower()]
print(sum(x.get('aciertos',0) for x in c), sum(x.get('errores',0) for x in c))" 2>/dev/null || echo "? ?"
}
subio() {  # subio antes despues -> "si (a -> b)" o "no (a -> b)"
  if [[ "$1" =~ ^[0-9]+$ && "$2" =~ ^[0-9]+$ && $2 -gt $1 ]]; then echo "si ($1 -> $2)"; else echo "no ($1 -> $2)"; fi
}
esperar_webhook_ok() {  # Espera que el receptor anote un POST /ok respondido con 2xx.
  local limite=$1 inicio=$SECONDS
  while (( SECONDS - inicio < limite )); do
    if curl -fsS --max-time 5 "$RECEPTOR/recibidos" 2>/dev/null | python3 -c "
import sys,json
sys.exit(0 if any(x['ruta']=='/ok' and 200<=x['estado']<300 for x in json.load(sys.stdin)) else 1)" 2>/dev/null; then
      echo "si ($((SECONDS - inicio)) s)"; return 0
    fi
    sleep 3
  done
  echo "no (en $limite s)"; return 1
}

# ---------- Preparación: datos demo, login y suscripción al receptor ----------

SERVICIO_ACTUAL=preparacion
echo "== Preparación ($(ahora_utc)) =="
DEMO=$(curl -fsS -X POST "$WEB/dev/datos-demo") || {
  echo "No respondió POST /dev/datos-demo. ¿La Web está en Development (WEB_ENTORNO=Development)?"; exit 1; }
EMAIL=$(echo "$DEMO" | json "['operadores'][$OPERADOR]['emailAdministrador']")
CLAVE=$(echo "$DEMO" | json "['operadores'][$OPERADOR]['contrasena']")
SEGUIMIENTO=$(echo "$DEMO" | json "['operadores'][$OPERADOR]['seguimientoPendiente']")
TOKEN=$(curl -fsS -c "$JAR" "$WEB/login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | sed 's/.*value="//;s/"$//')
curl -fsS -b "$JAR" -c "$JAR" -o /dev/null -X POST "$WEB/login" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" --data-urlencode "Email=$EMAIL" --data-urlencode "Contrasena=$CLAVE" \
  || { echo "No se pudo iniciar sesión como $EMAIL."; exit 1; }
curl -fsS -b "$JAR" -o /dev/null -X POST "$WEB/dev/simular/webhooks?receptor=$RECEPTOR_INTERNO&eventos=Admitir" \
  || { echo "No se pudo suscribir el receptor."; exit 1; }
echo "   Operador demo: $EMAIL. Esperando 8 s a que el Worker drene los eventos iniciales..."
sleep 8

echo "   Línea de base:"
chequear "/ready con todo prendido" "200" "$(ready)"
chequear "seguimiento con todo prendido" "200" "$(seguimiento)"
limpiar_receptor
chequear "alta de envío con todo prendido" "204" "$(alta)"
chequear "webhook /ok con todo prendido" "si*" "$(esperar_webhook_ok 60)"

# ---------- 1. Redis ----------

SERVICIO_ACTUAL=redis
echo; echo "== 1. Redis ($(ahora_utc)) =="
desde=$(ahora_utc); read -r _ errores_antes <<<"$(metrica_cache)"
dc stop redis
chequear "seguimiento sin Redis (lee de la base)" "200" "$(seguimiento)"
chequear "segunda consulta sin Redis (caché salteada 30 s)" "200" "$(seguimiento)"
chequear "/ready sin Redis (no depende de Redis)" "200" "$(ready)"
read -r _ errores_despues <<<"$(metrica_cache)"
chequear "la caché cuenta errores (ultimamilla.cache.errores)" "si*" "$(subio "$errores_antes" "$errores_despues")"
sleep "$ESPERA"; logs_desde "$desde"
dc start redis; esperar_sano redis
echo "   Esperando 35 s (pausa de 30 s de la caché tras una falla)..."; sleep 35
read -r aciertos_antes _ <<<"$(metrica_cache)"
seguimiento >/dev/null; seguimiento >/dev/null
read -r aciertos_despues _ <<<"$(metrica_cache)"
chequear "seguimiento tras volver Redis" "200" "$(seguimiento)"
chequear "la caché vuelve a acertar" "si*" "$(subio "$aciertos_antes" "$aciertos_despues")"

# ---------- 2. RabbitMQ ----------

SERVICIO_ACTUAL=rabbitmq
echo; echo "== 2. RabbitMQ ($(ahora_utc)) =="
desde=$(ahora_utc); limpiar_receptor
dc stop rabbitmq
chequear "alta de envío sin RabbitMQ (se guarda con su outbox)" "204" "$(alta)"
chequear "/ready sin RabbitMQ" "200" "$(ready)"
sleep "$ESPERA"
chequear "webhook mientras RabbitMQ está apagado" "no*" "$(esperar_webhook_ok 5)"
logs_desde "$desde"
dc start rabbitmq; esperar_sano rabbitmq 180
chequear "el webhook pendiente llega tras volver RabbitMQ" "si*" "$(esperar_webhook_ok 120)"
logs_desde "$desde"

# ---------- 3. PostgreSQL ----------

SERVICIO_ACTUAL=postgres
echo; echo "== 3. PostgreSQL ($(ahora_utc)) =="
echo "   Esperando 31 s para que venza la caché de seguimiento (30 s) y la consulta vaya a la base..."; sleep 31
desde=$(ahora_utc); limpiar_receptor
dc stop postgres
chequear "/live sin base (el proceso sigue vivo)" "200" "$(codigo "$WEB/live")"
chequear "/ready sin base" "503" "$(ready)"
chequear "seguimiento sin base" "5*" "$(seguimiento)"
chequear "alta de envío sin base" "5*" "$(alta)"
sleep "$ESPERA"; logs_desde "$desde"
dc start postgres; esperar_sano postgres
sleep 5
chequear "/ready tras volver la base" "200" "$(ready)"
chequear "seguimiento tras volver la base" "200" "$(seguimiento)"
chequear "alta tras volver la base (misma sesión)" "204" "$(alta)"
chequear "webhook del alta nueva" "si*" "$(esperar_webhook_ok 90)"

# ---------- 4. Receptor de webhooks ----------

SERVICIO_ACTUAL=receptor
echo; echo "== 4. Receptor de webhooks ($(ahora_utc)) =="
desde=$(ahora_utc)
dc stop receptor
chequear "alta de envío sin receptor" "204" "$(alta)"
sleep "$ESPERA"; logs_desde "$desde"
# Prendido antes de que venza la espera de 30 s: el aviso tiene que llegar en un reintento, no ir a la DLQ.
dc start receptor; esperar_sano receptor 60
chequear "el webhook /ok llega en un reintento" "si*" "$(esperar_webhook_ok 90)"
echo "   Llamadas que anotó el receptor después de volver:"
curl -fsS "$RECEPTOR/recibidos" | python3 -c "
import sys,json
for x in json.load(sys.stdin)[::-1]: print('   ', x['cuando'][11:19], x['ruta'], 'intento', x['intento'], x['firma'], x['estado'])" 2>/dev/null || true
logs_desde "$desde"

# ---------- Resumen ----------

SERVICIO_ACTUAL=final
restaurar
echo; echo "== Estado final =="
for servicio in "${SERVICIOS[@]}"; do esperar_sano "$servicio" 180 || true; done
echo; echo "== Resumen ($(ahora_utc)) =="
distintos=0
for fila in "${RESUMEN[@]}"; do
  IFS='|' read -r marca servicio descripcion esperado obtenido <<<"$fila"
  printf '%-8s %-11s %-55s %-22s %s\n' "$marca" "$servicio" "$descripcion" "$esperado" "$obtenido"
  [[ "$marca" == OK ]] || distintos=$((distintos + 1))
done
echo; echo "Chequeos distintos de lo esperado: $distintos. Salida completa: $SALIDA"
echo "Cargá los resultados en la columna 'Qué pasó' de docs/resiliencia.md."
(( distintos == 0 ))
