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
  # AWS_ROLE_ARN solo existe con habilitar_cicd = true; sin esa variable el pipeline de GitHub no despliega.
  value = merge(
    {
      AWS_REGION      = var.region
      AWS_INSTANCE_ID = module.instancia.instance_id
      DEPLOY_BUCKET   = module.artefactos.nombre
      ECR_REGISTRY    = module.ecr.registro
    },
    var.habilitar_cicd ? { AWS_ROLE_ARN = module.cicd[0].rol_arn } : {}
  )
}

output "repositorios_ecr" {
  value = module.ecr.urls
}

output "ruta_parametros_ssm" {
  value = module.parametros.ruta
}
