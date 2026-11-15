#!/usr/bin/env bash
# Prueba el cifrado en reposo de la PWA (wwwroot/js/repartidor.js) en Chrome real, sin Docker ni base de datos.
# Uso, desde la raíz del repositorio:  tests/Navegador/probar-cifrado-pwa.sh
# Requiere google-chrome (o CHROME=/ruta/al/navegador) y python3. Ver docs/cifrado-pwa.md.
set -euo pipefail
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHROME="${CHROME:-google-chrome}"
TEMP="$(mktemp -d)"
trap 'kill "${SRV:-0}" 2>/dev/null || true; rm -rf "$TEMP"' EXIT
cd "$RAIZ"
RESULTADOS="$TEMP" python3 -I tests/Navegador/servidor_pruebas.py & SRV=$!
sleep 1
# Dos cargas con el mismo perfil temporal: la segunda comprueba que la clave persiste.
for marca in "" "#recarga"; do
  timeout 30 "$CHROME" --headless=new --no-first-run --no-default-browser-check --disable-gpu \
    --user-data-dir="$TEMP/perfil" "http://127.0.0.1:5395/tests/Navegador/cifrado-pwa.html$marca" >/dev/null 2>&1 || true
done
cat "$TEMP/resultado1.txt" "$TEMP/resultado2.txt" 2>/dev/null || { echo "La página no informó resultados." >&2; exit 1; }
echo
if grep -qE '^(FALLA|ERROR)' "$TEMP/resultado1.txt" "$TEMP/resultado2.txt"; then echo "HAY FALLAS" >&2; exit 1; fi
echo "Todas las verificaciones pasaron."
