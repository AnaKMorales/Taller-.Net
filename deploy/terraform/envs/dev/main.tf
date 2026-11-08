locals {
  nombre   = "ultimamilla-${var.ambiente}"
  ruta_ssm = "/ultimamilla/${var.ambiente}"
  # Una imagen propia por contenedor; los repositorios se llaman ultimamilla-<imagen>.
  imagenes = ["web", "worker", "receptor", "migrador"]
}

module "red" {
  source      = "../../modules/red"
  nombre      = local.nombre
  cidr_vpc    = var.cidr_vpc
  cidr_subred = var.cidr_subred
}

module "ecr" {
  source   = "../../modules/ecr"
  prefijo  = "ultimamilla"
  imagenes = local.imagenes
}

module "artefactos" {
  source = "../../modules/artefactos"
  nombre = local.nombre
}

module "parametros" {
  source      = "../../modules/parametros"
  ruta        = local.ruta_ssm
  dominio     = var.dominio
  correo_acme = var.correo_acme
}

module "instancia" {
  source            = "../../modules/instancia"
  nombre            = local.nombre
  ambiente          = var.ambiente
  region            = var.region
  tipo_instancia    = var.tipo_instancia
  volumen_gb        = var.volumen_gb
  subred_id         = module.red.subred_id
  security_group_id = module.red.security_group_id
  registro_ecr      = module.ecr.registro
  arns_ecr          = module.ecr.arns
  bucket_nombre     = module.artefactos.nombre
  bucket_arn        = module.artefactos.arn
  ruta_ssm          = module.parametros.ruta
}

module "cicd" {
  source               = "../../modules/cicd"
  nombre               = local.nombre
  region               = var.region
  repositorio          = var.repositorio_github
  rama                 = var.rama_despliegue
  crear_proveedor_oidc = var.crear_proveedor_oidc
  arns_ecr             = module.ecr.arns
  bucket_arn           = module.artefactos.arn
  instancia_arn        = module.instancia.arn
}

module "presupuesto" {
  source     = "../../modules/presupuesto"
  nombre     = local.nombre
  limite_usd = var.presupuesto_mensual_usd
  correo     = var.correo_presupuesto != "" ? var.correo_presupuesto : var.correo_acme
}

# Opcional: registro A en Route 53 hacia la Elastic IP.
resource "aws_route53_record" "dominio" {
  count   = var.zona_route53_id != "" ? 1 : 0
  zone_id = var.zona_route53_id
  name    = var.dominio
  type    = "A"
  ttl     = 300
  records = [module.instancia.ip_publica]
}
