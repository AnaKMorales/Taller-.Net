#!/usr/bin/env bash
# Primer despliegue (o uno de emergencia) desde tu máquina, sin pasar por GitHub: hace lo mismo que el job "desplegar"
# del pipeline. Requiere: terraform apply hecho en deploy/terraform/envs/dev, docker, aws CLI, jq y credenciales de AWS.
#   Uso (desde la raíz del repositorio):  deploy/aws/desplegar-manual.sh
set -euo pipefail

# Las etiquetas de ECR son inmutables y la etiqueta es el SHA del commit (igual que en el pipeline): si hubiera cambios
# sin commitear se subiría código distinto bajo ese SHA, y esa etiqueta ya no se podría corregir. Se exige un árbol limpio.
if ! git diff --quiet || ! git diff --cached --quiet || [ -n "$(git ls-files --others --exclude-standard)" ]; then
  echo "ERROR: hay cambios sin commitear; no se despliega." >&2
  echo "       Las etiquetas de ECR son inmutables: la imagen quedaría etiquetada con un commit que no la contiene." >&2
  echo "       Commiteá los cambios (o guardalos con 'git stash -u') y volvé a ejecutar." >&2
  echo >&2
  git status --short >&2
  exit 1
fi

ENV_DIR="deploy/terraform/envs/dev"
salida=$(terraform -chdir="$ENV_DIR" output -json github_variables)
dato() { jq -r ".$1" <<<"$salida"; }
REGION=$(dato AWS_REGION); INSTANCIA=$(dato AWS_INSTANCE_ID); BUCKET=$(dato DEPLOY_BUCKET); REGISTRO=$(dato ECR_REGISTRY)
export AWS_DEFAULT_REGION="$REGION"

TAG="$(git rev-parse HEAD)"
echo "==> Etiqueta de las imágenes (SHA del commit): $TAG"

echo "==> Login en ECR ($REGISTRO)"
aws ecr get-login-password | docker login --username AWS --password-stdin "$REGISTRO"

construir() { # nombre contexto dockerfile
  if aws ecr describe-images --repository-name "ultimamilla-$1" --image-ids imageTag="$TAG" >/dev/null 2>&1; then
    echo "==> ultimamilla-$1:$TAG ya está en ECR; no se vuelve a subir"
    return
  fi
  echo "==> Construyendo y subiendo ultimamilla-$1"
  docker build -f "$3" -t "$REGISTRO/ultimamilla-$1:$TAG" "$2"
  docker push "$REGISTRO/ultimamilla-$1:$TAG"
}
construir web . src/UltimaMilla.Web/Dockerfile
construir worker . src/UltimaMilla.Worker/Dockerfile
construir migrador . deploy/migrador/Dockerfile
construir receptor tools/UltimaMilla.ReceptorWebhooks tools/UltimaMilla.ReceptorWebhooks/Dockerfile

echo "==> Subiendo archivos de despliegue a s3://$BUCKET/deploy/"
aws s3 cp deploy/compose.yaml "s3://$BUCKET/deploy/compose.yaml"
aws s3 cp deploy/compose.aws.yaml "s3://$BUCKET/deploy/compose.aws.yaml"
aws s3 cp deploy/aws/desplegar.sh "s3://$BUCKET/deploy/desplegar.sh"
aws s3 cp deploy/traefik "s3://$BUCKET/deploy/traefik" --recursive

echo "==> Esperando a que la instancia esté registrada en SSM"
for _ in $(seq 1 40); do
  estado=$(aws ssm describe-instance-information --filters "Key=InstanceIds,Values=$INSTANCIA" \
    --query 'InstanceInformationList[0].PingStatus' --output text 2>/dev/null || true)
  [ "$estado" = "Online" ] && break
  sleep 15
done
[ "$estado" = "Online" ] || { echo "La instancia no aparece Online en SSM (¿terminó el arranque? ver README)"; exit 1; }

echo "==> Ordenando el despliegue por SSM"
parametros=$(jq -nc --arg bucket "$BUCKET" --arg tag "$TAG" \
  '{commands: ["cloud-init status --wait || true", "/usr/local/bin/aws s3 sync s3://\($bucket)/deploy/ /opt/ultimamilla/deploy/ --only-show-errors", "bash /opt/ultimamilla/deploy/desplegar.sh \($tag)"]}')
id=$(aws ssm send-command --instance-ids "$INSTANCIA" --document-name AWS-RunShellScript \
  --comment "Despliegue manual ${TAG:0:7}" --parameters "$parametros" --timeout-seconds 1200 \
  --query Command.CommandId --output text)
echo "    Comando $id (puede tardar varios minutos la primera vez: descarga de imágenes y migraciones)"

# Se sondea el estado en vez de usar "aws ssm wait command-executed": ese espera como máximo ~100 s y el primer despliegue
# (descarga de imágenes y migraciones) tarda más. Compatible con la AWS CLI v1 y v2: solo usa --output json/text.
echo "==> Esperando el resultado (hasta 21 min)"
inicio=$SECONDS
estado="Pending"
while :; do
  estado=$(aws ssm get-command-invocation --command-id "$id" --instance-id "$INSTANCIA" \
    --query Status --output text 2>/dev/null || echo "Pending")
  case "$estado" in
    Pending | InProgress | Delayed) ;;
    *) break ;;
  esac
  transcurrido=$((SECONDS - inicio))
  if [ "$transcurrido" -ge 1260 ]; then
    estado="SinRespuesta"
    break
  fi
  printf '    ... %s (%ss)\n' "$estado" "$transcurrido"
  sleep 15
done

# Estado final y log completo del comando, tanto si salió bien como si falló.
invocacion=$(aws ssm get-command-invocation --command-id "$id" --instance-id "$INSTANCIA" --output json 2>/dev/null || echo '{}')
echo
echo "==> Estado final: $(jq -r --arg e "$estado" '.Status // $e' <<<"$invocacion")"
echo "----- Salida del comando (StandardOutputContent) -----"
jq -r '.StandardOutputContent // ""' <<<"$invocacion"
echo "----- Errores del comando (StandardErrorContent) -----"
jq -r '.StandardErrorContent // ""' <<<"$invocacion"
echo "-----"

if [ "$estado" != "Success" ]; then
  echo "ERROR: el despliegue terminó con estado '$estado' (comando $id). El log completo está arriba." >&2
  exit 1
fi

dominio=$(terraform -chdir="$ENV_DIR" output -raw dominio)
echo "==> Listo: https://$dominio"
