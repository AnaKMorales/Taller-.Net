#!/usr/bin/env bash
# Demostración de webhooks firmados: un aviso exitoso, uno con reintentos y uno que termina en fallidos.
# Requisitos: Postgres, RabbitMQ, worker y receptor corriendo (con WEBHOOKS_PERMITIR_DESTINOS_PRIVADOS=true; ver docs/webhooks-demo.md)
# y la Web en Development (/dev/datos-demo): la del compose con WEB_ENTORNO=Development, o scripts/Start-Web.ps1. Usa curl y python3.
set -euo pipefail

WEB=${WEB:-http://127.0.0.1:${WEB_PORT:-8080}}          # la Web (desde tu máquina): la del compose en 8080; con Start-Web.ps1, WEB=http://127.0.0.1:5243
RECEPTOR=${RECEPTOR:-http://127.0.0.1:58080}            # el receptor, para mirar /recibidos
RECEPTOR_INTERNO=${RECEPTOR_INTERNO:-http://receptor:8080}  # cómo lo ve el worker dentro de Docker
ESPERA_MAXIMA=${ESPERA_MAXIMA:-240}                     # segundos: 5 + 30 + 120 de esperas + margen
JAR=$(mktemp); trap 'rm -f "$JAR"' EXIT

json() { python3 -c "import sys,json;print(json.load(sys.stdin)$1)"; }

echo "1) Creando operador demo y entrando como administrador..."
DEMO=$(curl -fsS -X POST "$WEB/dev/datos-demo")
EMAIL=$(echo "$DEMO" | json "['emailAdministrador']")
CLAVE=$(echo "$DEMO" | json "['contrasena']")
TOKEN=$(curl -fsS -c "$JAR" "$WEB/login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | sed 's/.*value="//;s/"$//')
curl -fsS -b "$JAR" -c "$JAR" -o /dev/null -X POST "$WEB/login" \
  --data-urlencode "__RequestVerificationToken=$TOKEN" --data-urlencode "Email=$EMAIL" --data-urlencode "Contrasena=$CLAVE"

echo "   Esperando 8 s a que el Worker drene los eventos que dejó la creación del operador demo..."
sleep 8

echo "2) Suscribiendo la cuenta a $RECEPTOR_INTERNO: /ok, /flaky?n=2 y /fail..."
curl -fsS -b "$JAR" -X POST "$WEB/dev/simular/webhooks?receptor=$RECEPTOR_INTERNO&eventos=Admitir"; echo

echo "3) Limpiando el historial del receptor y generando un evento (alta de envío)..."
curl -fsS -X DELETE "$RECEPTOR/recibidos"
curl -fsS -b "$JAR" -o /dev/null -X POST "$WEB/dev/simular/alta"

echo "4) Siguiendo las llamadas que llegan al receptor (hasta $ESPERA_MAXIMA s):"
printf '   %-9s %-8s %-8s %-7s %s\n' HORA RUTA INTENTO FIRMA RESPUESTA
visto=0; inicio=$SECONDS
while (( SECONDS - inicio < ESPERA_MAXIMA )); do
  sleep 2
  filas=$(curl -fsS "$RECEPTOR/recibidos" | python3 -c "
import sys,json
r=json.load(sys.stdin)[::-1]
for x in r: print(x['cuando'][11:19], x['ruta'], x['intento'], x['firma'], x['estado'])")
  total=$(echo "$filas" | grep -c . || true)
  if (( total > visto )); then
    echo "$filas" | tail -n +$((visto + 1)) | while read -r h r i f e; do printf '   %-9s %-8s %-8s %-7s %s\n' "$h" "$r" "$i" "$f" "$e"; done
    visto=$total
  fi
  # /fail agotó sus 4 intentos (1 + 5 s + 30 s + 2 m): ya terminó.
  if echo "$filas" | grep -q '^[0-9:]* /fail 4 '; then break; fi
done

cat <<FIN

Listo. Mirá el resultado en el backoffice ($WEB/login, usuario $EMAIL, clave $CLAVE):
  $WEB/backoffice/fallidos
    - "Cola de fallidos (avisos.dlq)": el mensaje que agotó los reintentos (botón Reintentar).
    - "Webhooks a comercios": intentos por suscripción (/fail con 4 intentos, sin éxito).
FIN
