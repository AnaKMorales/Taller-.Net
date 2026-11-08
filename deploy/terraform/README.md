# Infraestructura en AWS (Terraform)

Una sola EC2 Ubuntu con Docker y docker compose, sin balanceador (diseño, sección 3). Todo el ambiente se
crea y se destruye con Terraform; el código de la aplicación lo despliega el pipeline.

```
deploy/terraform/
  bootstrap/        bucket S3 con el estado de Terraform (se crea una vez, a mano)
  modules/          red, ecr, parametros, artefactos, instancia, cicd, presupuesto
  envs/dev/         el ambiente: variables, backend y llamada a los módulos
```

| Módulo | Qué crea |
| --- | --- |
| `red` | VPC, subred pública, Internet Gateway, Security Group (**solo 80 y 443**, sin SSH) |
| `instancia` | EC2 Ubuntu 24.04 (c7i-flex.large: 2 vCPU, 4 GB), disco gp3 cifrado, Elastic IP, rol IAM (SSM, ECR de lectura, parámetros), IMDSv2 |
| `ecr` | Repositorios `ultimamilla-{web,worker,receptor,migrador}` con etiquetas inmutables y retención de 10 imágenes |
| `parametros` | SSM Parameter Store: claves de Postgres y RabbitMQ (generadas), `DOMINIO`, `ACME_EMAIL` |
| `artefactos` | Bucket S3 con `compose.yaml`, `compose.aws.yaml` y `desplegar.sh` (la instancia los baja) |
| `cicd` | Proveedor OIDC de GitHub y rol del pipeline (solo `main` del repositorio indicado) |
| `presupuesto` | AWS Budgets: alerta al 80 % real y al 100 % pronosticado |

## Antes de empezar

- Terraform ≥ 1.10, AWS CLI con credenciales, y un dominio propio.
- **La cuenta tiene una SCP que solo permite EC2 y ECR en `us-east-2`** (se probó: el resto de las regiones
  devuelve `explicit deny`). Por eso la región por defecto es `us-east-2`.
- **Tipo de instancia:** la cuenta (plan Free) solo permite `c7i-flex.large`, `m7i-flex.large`, `t3.micro`, `t3.small`,
  `t4g.*`, `t8i.micro` y `t8i.small`. El defecto es `c7i-flex.large` (variable `tipo_instancia`; debe ser x86_64, así que
  `t4g.*` no sirve con esta AMI). Los `t3.micro/small` (1 a 2 GB) no alcanzan para todos los contenedores.
- Se negó también `iam:ListOpenIDConnectProvider`. Si crear el proveedor OIDC fallara por la SCP, ver *Problemas conocidos*.

## 1. Estado remoto (una sola vez)

```bash
cd deploy/terraform/bootstrap
terraform init && terraform apply
terraform output -raw bucket_name
```

## 2. Crear el ambiente

```bash
cd deploy/terraform/envs/dev
cp backend.hcl.example backend.hcl            # bucket y región del estado (del paso anterior)
cp terraform.tfvars.example terraform.tfvars  # dominio y correo
terraform init -backend-config=backend.hcl
terraform plan                                 # qué se va a crear
terraform apply                                # crea el ambiente (~37 recursos)
```

Salidas importantes: `ip_publica` y `github_variables`.

## 3. Conectar GitHub y el dominio

1. **Variables de GitHub** (Settings → Secrets and variables → Actions → *Variables*; no son secretos):
   `AWS_REGION`, `AWS_ROLE_ARN`, `AWS_INSTANCE_ID`, `DEPLOY_BUCKET`, `ECR_REGISTRY` (valores en la salida `github_variables`).
   Mientras `AWS_ROLE_ARN` no exista, el pipeline solo compila y prueba.
2. **DNS:** registro A del dominio → `ip_publica` (o `zona_route53_id` para que Terraform lo cree).
   Let's Encrypt valida por HTTP-01: el DNS tiene que resolver **antes** del primer despliegue.
3. Hacer *push* a `main`: el pipeline compila, prueba, sube las 4 imágenes a ECR (etiqueta = SHA) y ejecuta
   `desplegar.sh` en la instancia por SSM Run Command. Sin SSH y sin claves de larga duración.

Para probar sin gastar cuota de Let's Encrypt: poner `ACME_CA_SERVER=https://acme-staging-v02.api.letsencrypt.org/directory`
(el navegador mostrará el certificado como no confiable).

## 4. Operar

```bash
terraform plan      # cambios pendientes
terraform apply     # aplicar cambios
terraform destroy   # elimina todo el ambiente (también las imágenes de ECR y el bucket de despliegue)
```

Destruir y reconstruir: `destroy` → `apply` → push a `main` (o *Re-run* del último despliegue).
Se pierden los datos de Postgres (viven en el disco de la instancia): no hay respaldos en este diseño.

Ver la instancia sin SSH: `aws ssm start-session --target <AWS_INSTANCE_ID>` (requiere el plugin de Session Manager).
Logs del arranque: `/var/log/cloud-init-output.log`.

## Problemas conocidos

- **`VcpuLimitExceeded` al crear la instancia:** la cuota de vCPU de la cuenta es menor a las que pide el tipo.
  Pedir un aumento en *Service Quotas → Amazon EC2* (cuota "Running On-Demand Standard (A, C, D, H, I, M, R, T, Z) instances",
  región `us-east-2`) y repetir `terraform apply`. (el mensaje de error indica qué cuota es; no verifiqué cuál aplica a los `*-flex`).

- **SCP de la organización:** si `apply` falla con `explicit deny in a service control policy`, hay que pedir
  al administrador de la cuenta que habilite esa acción. Con la SCP actual, EC2/ECR solo funcionan en `us-east-2`.
- **OIDC bloqueado:** sin proveedor OIDC no hay despliegue sin claves. Alternativa de último recurso (no cumple el
  diseño): usuario IAM con claves como *secrets* de GitHub.
- **El proveedor OIDC ya existe** en la cuenta (otro proyecto): `crear_proveedor_oidc = false`.
- **Cambiar las claves de Postgres/RabbitMQ** en SSM después del primer despliegue no cambia las ya inicializadas
  en los volúmenes de Docker.
- `lifecycle.ignore_changes` en la instancia: un cambio de `user_data` o de AMI no la recrea (se perderían los datos).
  Para aplicar cambios del arranque hay que recrearla a propósito: `terraform apply -replace=module.instancia.aws_instance.this`.
