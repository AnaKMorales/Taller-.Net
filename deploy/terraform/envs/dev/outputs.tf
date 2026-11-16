output "ip_publica" {
  description = "Elastic IP: el registro A del dominio debe apuntar aqui."
  value       = module.instancia.ip_publica
}

output "dominio" {
  description = "Direccion de la plataforma (https://<dominio>)."
  value       = local.dominio
}

output "github_variables" {
  description = "Cargar en GitHub: Settings > Secrets and variables > Actions > Variables (no son secretos)."
  # AWS_ROLE_ARN solo existe con modo_cicd = "oidc". Con "claves" el pipeline usa los secrets AWS_ACCESS_KEY_ID y
  # AWS_SECRET_ACCESS_KEY (clave del usuario de la salida usuario_pipeline).
  value = merge(
    {
      AWS_REGION      = var.region
      AWS_INSTANCE_ID = module.instancia.instance_id
      DEPLOY_BUCKET   = module.artefactos.nombre
      ECR_REGISTRY    = module.ecr.registro
    },
    var.habilitar_cicd && var.modo_cicd == "oidc" ? { AWS_ROLE_ARN = module.cicd[0].rol_arn } : {}
  )
}

output "usuario_pipeline" {
  description = "Solo con modo_cicd = \"claves\": usuario IAM del pipeline. Su clave se crea con aws iam create-access-key y va a los secrets de GitHub."
  value       = var.habilitar_cicd && var.modo_cicd == "claves" ? module.cicd[0].usuario : ""
}

output "repositorios_ecr" {
  value = module.ecr.urls
}

output "ruta_parametros_ssm" {
  value = module.parametros.ruta
}
