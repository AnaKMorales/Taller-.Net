output "ip_publica" {
  description = "Elastic IP: el registro A del dominio debe apuntar aqui."
  value       = module.instancia.ip_publica
}

output "github_variables" {
  description = "Cargar en GitHub: Settings > Secrets and variables > Actions > Variables (no son secretos)."
  value = {
    AWS_REGION      = var.region
    AWS_ROLE_ARN    = module.cicd.rol_arn
    AWS_INSTANCE_ID = module.instancia.instance_id
    DEPLOY_BUCKET   = module.artefactos.nombre
    ECR_REGISTRY    = module.ecr.registro
  }
}

output "repositorios_ecr" {
  value = module.ecr.urls
}

output "ruta_parametros_ssm" {
  value = module.parametros.ruta
}
