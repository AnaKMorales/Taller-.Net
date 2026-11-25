#!/usr/bin/env bash
# Genera un par de claves VAPID (P-256) para los avisos push al repartidor (docs/push-repartidor.md).
# Imprime las dos líneas para pegar en deploy/.env (local) o cargar en SSM Parameter Store (AWS).
# La privada es secreta: nunca la subas a Git.  Requiere openssl y xxd.
set -euo pipefail

texto=$(openssl ecparam -name prime256v1 -genkey -noout | openssl ec -text -noout 2>/dev/null)
hex_privada=$(echo "$texto" | awk '/priv:/{f=1;next} /pub:/{f=0} f' | tr -d ' :\n')
hex_publica=$(echo "$texto" | awk '/pub:/{f=1;next} /ASN1 OID/{f=0} f' | tr -d ' :\n')
# openssl puede agregar un 00 adelante o comer ceros: se normaliza a 32 bytes.
hex_privada=$(printf '%064s' "${hex_privada: -64}" | tr ' ' 0)

b64url() { xxd -r -p | base64 -w0 | tr '+/' '-_' | tr -d '='; }
echo "WEBPUSH_CLAVE_PUBLICA=$(echo "$hex_publica" | b64url)"
echo "WEBPUSH_CLAVE_PRIVADA=$(echo "$hex_privada" | b64url)"
