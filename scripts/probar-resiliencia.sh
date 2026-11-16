#!/usr/bin/env bash
# Pruebas de resiliencia (opcional 7.4): corta a propósito cada dependencia y compara lo esperado con lo observado.
#   1. Redis (caché)          2. RabbitMQ (cola)          3. PostgreSQL (base)
#   4. Receptor de webhooks (servicio externo)            5. Worker (consumidor)
# Requisitos: el stack completo de deploy/compose.yaml levantado con el perfil "app", WEB_ENTORNO=Development
# (usa /dev/datos-demo y /dev/simular) y WEBHOOKS_PERMITIR_DESTINOS_PRIVADOS=true (el receptor es privado).
#   docker compose --env-file deploy/.env -f deploy/compose.yaml --profile app up -d
#   ./scripts/probar-resiliencia.sh            # todos los escenarios (~6 min)
#   ./scripts/probar-resiliencia.sh 1 4        # solo algunos
# Deja el resumen en TestResults/resiliencia-<fecha>.md. Termina con código 1 si algún escenario no se comportó como se esperaba.
# Al salir (incluso con Ctrl+C) vuelve a levantar todo lo que detuvo.
set -uo pipefail

RAIZ=$(cd "$(dirname "$0")/.." && pwd)
ENV_FILE=${ENV_FILE:-$RAIZ/deploy/.env}
WEB=${WEB:-http://127.0.0.1:${WEB_PORT:-8080}}
RECEPTOR=${RECEPTOR:-http://127.0.0.1:${RECEPTOR_PORT:-58080}}
set -a; . "$ENV_FILE"; set +a
PGUSER_=${POSTGRES_USER:-ultimamilla}; PGDB_=${POSTGRES_DB:-ultimamilla}

ESCENARIOS=("$@"); (( ${#ESCENARIOS[@]} )) || ESCENARIOS=(1 2 3 4 5)
mkdir -p "$RAIZ/TestResults"
INFORME="$RAIZ/TestResults/resiliencia-$(date +%Y%m%d-%H%M%S).md"
JAR=$(mktemp)
FALLAS=0

dc() { docker compose --env-file "$ENV_FILE" -f "$RAIZ/deploy/compose.yaml" --profile app "$@"; }
sql() { dc exec -T postgres psql -U "$PGUSER_" -d "$PGDB_" -tAc "$1" 2>/dev/null | tr -d '[:space:]'; }
json() { python3 -c "import sys,json;print(json.load(sys.stdin)$1)"; }
# Código HTTP y segundos de una llamada (máximo 15 s).
medir() { curl -s -o /dev/null -m 15 -w '%{http_code} %{time_total}' "$@" || true; }
pendientes() { sql 'SELECT count(*) FROM "MensajesOutbox" WHERE "PublicadoEn" IS NULL'; }
# Avisos entregados con éxito a la suscripción /ok de la cuenta de prueba (en la base: el receptor pierde su historial al reiniciarse).
entregados_ok() { sql "SELECT count(*) FROM \"EntregasAviso\" WHERE \"SuscripcionId\"='$SUB_OK' AND \"Exitoso\""; }
# Eventos que el receptor recibió con 200 en /ok más de una vez (debe ser 0: idempotencia de punta a punta).
duplicados_ok() { curl -fsS -m 5 "$RECEPTOR/recibidos" 2>/dev/null | python3 -c "
import sys,json,collections
c=collections.Counter(x['eventoId'] for x in json.load(sys.stdin) if x['ruta']=='/ok' and x['estado']==200)
print(sum(1 for v in c.values() if v>1))" 2>/dev/null || echo "?"; }
ultimo_evento() { sql 'SELECT "EventoId" FROM "MensajesOutbox" ORDER BY "CreadoEn" DESC LIMIT 1'; }
entrega_ok() { sql "SELECT \"Exitoso\"||'/'||\"Intentos\" FROM \"EntregasAviso\" WHERE \"SuscripcionId\"='$SUB_OK' AND \"EventoId\"='$1'"; }
alta() { medir -b "$JAR" -X POST "$WEB/dev/simular/alta"; }
log() { echo "$*" | tee -a "$INFORME"; }
# Espera hasta N segundos a que el comando devuelva éxito; imprime los segundos que tardó o "timeout".
esperar() { local n=$1; shift; local t0=$SECONDS; while (( SECONDS - t0 < n )); do "$@" && { echo $((SECONDS - t0)); return 0; }; sleep 1; done; echo timeout; return 1; }
listo() { [[ $(curl -s -o /dev/null -m 3 -w '%{http_code}' "$WEB/ready") == 200 ]]; }
sin_pendientes() { [[ $(pendientes) == 0 ]]; }

resultado() { # escenario, esperado, observado, ok(0/1)
  local marca="✅"; [[ $4 == 0 ]] || { marca="❌"; FALLAS=$((FALLAS + 1)); }
  printf '| %s | %s | %s | %s |\n' "$1" "$2" "$3" "$marca" >> "$INFORME.tabla"
}

restaurar() {
  dc start postgres redis rabbitmq receptor worker web >/dev/null 2>&1 || true
  rm -f "$JAR"
}
trap restaurar EXIT

preparar() {
  listo || { echo "La Web no responde /ready en $WEB. Levantá el stack (ver encabezado)."; exit 2; }
  local demo email clave token
  demo=$(curl -fsS -X POST "$WEB/dev/datos-demo") || { echo "Falta WEB_ENTORNO=Development (no existe /dev/datos-demo)."; exit 2; }
  email=$(echo "$demo" | json "['operadores'][0]['emailAdministrador']")
  clave=$(echo "$demo" | json "['operadores'][0]['contrasena']")
  token=$(curl -fsS -c "$JAR" "$WEB/login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | sed 's/.*value="//;s/"$//')
  curl -fsS -b "$JAR" -c "$JAR" -o /dev/null -X POST "$WEB/login" \
    --data-urlencode "__RequestVerificationToken=$token" --data-urlencode "Email=$email" --data-urlencode "Contrasena=$clave"
  curl -fsS -b "$JAR" -o /dev/null -X POST "$WEB/dev/simular/webhooks?receptor=http://receptor:8080&eventos=Admitir"
  local inicial; inicial=$(alta)
  [[ $inicial == 204* ]] || { echo "POST /dev/simular/alta respondió $inicial con todo levantado: revisar el log de web."; exit 2; }
  SEGUIMIENTO="$WEB/api/seguimiento/$(sql 'SELECT "CodigoSeguimiento" FROM "Envios" ORDER BY "FechaAdmision" DESC LIMIT 1')"
  SUB_OK=$(sql "SELECT s.\"Id\" FROM \"SuscripcionesAviso\" s WHERE s.\"UrlDestino\" LIKE '%/ok' AND s.\"CuentaComercialId\"=(SELECT \"CuentaComercialId\" FROM \"Envios\" ORDER BY \"FechaAdmision\" DESC LIMIT 1)")
  [[ -n $SUB_OK ]] || { echo "No se encontró la suscripción /ok de la cuenta de prueba."; exit 2; }
  esperar 60 sin_pendientes >/dev/null
  log "Operador demo: $email. Seguimiento de prueba: $SEGUIMIENTO"
}

# 1. Redis caído: la caché es una optimización; el seguimiento y la tarifa deben seguir respondiendo desde la base.
escenario_1() {
  log ""; log "## 1. Redis (caché) detenido"
  log "- Con Redis: seguimiento $(medir "$SEGUIMIENTO") (código, segundos)"
  dc stop redis >/dev/null 2>&1
  local r1 r2 r3 rd
  r1=$(medir "$SEGUIMIENTO"); r2=$(medir "$SEGUIMIENTO"); r3=$(medir "$SEGUIMIENTO"); rd=$(medir "$WEB/ready")
  log "- Sin Redis: seguimiento 1.º $r1 · 2.º $r2 · 3.º $r3 · /ready $rd"
  dc start redis >/dev/null 2>&1
  log "- Redis vuelve; tras la pausa de 30 s de CacheSegura se vuelve a usar."
  sleep 32; medir "$SEGUIMIENTO" >/dev/null
  local r4; r4=$(medir "$SEGUIMIENTO"); log "- Con Redis otra vez: seguimiento $r4"
  local ok=1; [[ $r1 == 200* && $r2 == 200* && $r3 == 200* && $r4 == 200* ]] && ok=0
  resultado "1. Redis" "Seguimiento sigue en 200 leyendo de la base; solo la primera llamada paga el timeout" \
    "200 siempre; 1.ª llamada ${r1#* } s (timeout), siguientes ${r2#* } s y ${r3#* } s; vuelve a la caché" $ok
}

# 2. RabbitMQ caído: la Web sigue aceptando altas (el evento queda en el outbox) y se publica cuando vuelve.
escenario_2() {
  log ""; log "## 2. RabbitMQ (cola) detenido"
  local antes; antes=$(entregados_ok)
  dc stop rabbitmq >/dev/null 2>&1
  local a1 a2 p; a1=$(alta); a2=$(alta); sleep 3; p=$(pendientes)
  log "- Sin RabbitMQ: alta 1 $a1 · alta 2 $a2 · outbox pendiente $p · /ready $(medir "$WEB/ready")"
  dc start rabbitmq >/dev/null 2>&1
  local t; t=$(esperar 150 sin_pendientes)
  llegaron() { (( $(entregados_ok) >= antes + 2 )); }
  local t2; t2=$(esperar 120 llegaron); local despues; despues=$(entregados_ok)
  log "- RabbitMQ vuelve: outbox vacío en ${t} s · avisos /ok entregados $antes → $despues (${t2} s más)"
  local ok=1; [[ $a1 == 204* && $a2 == 204* && $p -ge 2 && $t != timeout && $despues == $((antes + 2)) ]] && ok=0
  resultado "2. RabbitMQ" "Altas aceptadas; eventos esperan en el outbox y salen al volver la cola, sin perder ni duplicar" \
    "Altas ${a1%% *}/${a2%% *}; $p en outbox; publicados $t s después de volver; avisos +$((despues - antes))" $ok
}

# 3. PostgreSQL caído: la Web sigue viva (/live) pero se declara no lista (/ready 503) y se recupera sola.
escenario_3() {
  log ""; log "## 3. PostgreSQL (base) detenido"
  medir "$SEGUIMIENTO" >/dev/null # deja el seguimiento en caché (vive pocos segundos: cambia seguido)
  dc stop postgres >/dev/null 2>&1
  local lv rd sg al; lv=$(medir "$WEB/live"); rd=$(medir "$WEB/ready"); sg=$(medir "$SEGUIMIENTO"); al=$(alta)
  log "- Sin base: /live $lv · /ready $rd · seguimiento (en caché) $sg · alta $al"
  dc start postgres >/dev/null 2>&1
  local t; t=$(esperar 90 listo)
  local al2; al2=$(alta); local t2; t2=$(esperar 60 sin_pendientes)
  log "- Base vuelve: /ready 200 en ${t} s · alta $al2 · outbox vacío en ${t2} s (el Worker también se reconectó)"
  local ok=1; [[ $lv == 200* && $rd == 503* && $al != 2* && $t != timeout && $al2 == 204* && $t2 != timeout ]] && ok=0
  resultado "3. PostgreSQL" "/live 200, /ready 503, escrituras rechazadas sin corromper nada; Web y Worker se reconectan solos" \
    "/live ${lv%% *}, /ready ${rd%% *}, seguimiento en caché ${sg%% *}, alta ${al%% *}; listo $t s después; Worker publica en $t2 s" $ok
}

# 4. Receptor del comercio caído (servicio externo): el aviso se reintenta con espera creciente y llega al volver.
escenario_4() {
  log ""; log "## 4. Receptor de webhooks (servicio externo) detenido"
  dc stop receptor >/dev/null 2>&1
  alta >/dev/null; sleep 2
  local evento; evento=$(ultimo_evento)
  fallo() { [[ $(entrega_ok "$evento") == false/* ]]; }
  esperar 40 fallo >/dev/null
  local fila; fila=$(entrega_ok "$evento")
  log "- Sin receptor: entrega /ok del evento $evento = $fila (exitoso/intentos)"
  sleep 5
  dc start receptor >/dev/null 2>&1
  log "- Receptor vuelve; el mensaje sigue en las colas de espera (5 s → 30 s → 2 m)."
  entregado() { [[ $(entrega_ok "$evento") == true/* ]]; }
  local t; t=$(esperar 180 entregado)
  local final; final=$(entrega_ok "$evento")
  log "- Entregado ${t} s después de reactivar el receptor: $final"
  local ok=1; [[ $fila == false/* && $t != timeout && $final == true/* ]] && ok=0
  resultado "4. Receptor externo" "Los intentos fallidos quedan registrados; el aviso llega en un reintento automático, sin intervención" \
    "Caído: $fila; al volver: $final ($t s después)" $ok
}

# 5. Worker detenido: los mensajes esperan; al volver se procesan una sola vez (idempotencia).
escenario_5() {
  log ""; log "## 5. Worker (consumidor) detenido"
  local antes; antes=$(entregados_ok)
  dc stop worker >/dev/null 2>&1
  local a1 a2 a3; a1=$(alta); a2=$(alta); a3=$(alta); sleep 3
  local p; p=$(pendientes)
  log "- Sin Worker: altas ${a1%% *} · ${a2%% *} · ${a3%% *} · outbox pendiente $p · Web /ready $(medir "$WEB/ready")"
  dc start worker >/dev/null 2>&1
  local t; t=$(esperar 120 sin_pendientes)
  llegaron() { (( $(entregados_ok) >= antes + 3 )); }
  esperar 120 llegaron >/dev/null; sleep 10
  local despues dup; despues=$(entregados_ok); dup=$(duplicados_ok)
  log "- Worker vuelve: outbox vacío en ${t} s · avisos /ok entregados $antes → $despues · eventos repetidos en el receptor: $dup"
  local ok=1; [[ $p -ge 3 && $t != timeout && $despues == $((antes + 3)) && $dup == 0 ]] && ok=0
  resultado "5. Worker" "La Web no se entera; los 3 eventos se procesan al volver, exactamente una vez cada uno" \
    "$p en outbox; publicados $t s después de volver; avisos +$((despues - antes)); repetidos $dup" $ok
}

{ echo "# Pruebas de resiliencia — $(date '+%d/%m/%Y %H:%M')"; echo; echo "Web: $WEB · Commit: $(git -C "$RAIZ" rev-parse --short HEAD)"; } > "$INFORME"
printf '| Escenario | Esperado | Observado | Resultado |\n|---|---|---|---|\n' > "$INFORME.tabla"
preparar
for e in "${ESCENARIOS[@]}"; do "escenario_$e"; done
{ echo; echo "## Resumen"; echo; cat "$INFORME.tabla"; } >> "$INFORME"; rm -f "$INFORME.tabla"
echo; sed -n '/## Resumen/,$p' "$INFORME"; echo; echo "Informe: $INFORME"
(( FALLAS == 0 ))
