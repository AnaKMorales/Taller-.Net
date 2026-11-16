# Acceso de GitHub Actions a AWS con permisos mínimos (ECR, archivos de despliegue en S3 y SSM a esta instancia).
#   modo = "oidc":   rol asumido por OIDC; credenciales temporales y solo para el repositorio y la rama indicados.
#   modo = "claves": usuario IAM con esos mismos permisos, para cuentas cuya SCP niega OIDC. Terraform crea el
#                    usuario y su política, no la clave (así no queda en el estado): se crea con la CLI y se guarda
#                    como secret de GitHub (ver deploy/terraform/README.md).
variable "modo" {
  type = string
  validation {
    condition     = contains(["oidc", "claves"], var.modo)
    error_message = "modo debe ser \"oidc\" o \"claves\"."
  }
}
variable "nombre" { type = string }
variable "region" { type = string }
variable "repositorio" { type = string }
variable "rama" { type = string }
variable "crear_proveedor_oidc" { type = bool }
variable "arns_ecr" { type = list(string) }
variable "bucket_arn" { type = string }
variable "instancia_arn" { type = string }

data "aws_caller_identity" "actual" {}

locals {
  url_oidc = "https://token.actions.githubusercontent.com"
}

# Una cuenta solo admite un proveedor por URL: si ya existe (otro proyecto), crear_proveedor_oidc = false.
resource "aws_iam_openid_connect_provider" "github" {
  count          = var.modo == "oidc" && var.crear_proveedor_oidc ? 1 : 0
  url            = local.url_oidc
  client_id_list = ["sts.amazonaws.com"]
}

data "aws_iam_openid_connect_provider" "github" {
  count = var.modo == "oidc" && !var.crear_proveedor_oidc ? 1 : 0
  url   = local.url_oidc
}

locals {
  arn_proveedor = var.modo != "oidc" ? "" : (var.crear_proveedor_oidc ? aws_iam_openid_connect_provider.github[0].arn : data.aws_iam_openid_connect_provider.github[0].arn)
}

data "aws_iam_policy_document" "confianza" {
  count = var.modo == "oidc" ? 1 : 0
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    principals {
      type        = "Federated"
      identifiers = [local.arn_proveedor]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.repositorio}:ref:refs/heads/${var.rama}"]
    }
  }
}

resource "aws_iam_role" "pipeline" {
  count                = var.modo == "oidc" ? 1 : 0
  name                 = "${var.nombre}-pipeline"
  assume_role_policy   = data.aws_iam_policy_document.confianza[0].json
  max_session_duration = 3600
}

data "aws_iam_policy_document" "permisos" {
  statement {
    sid       = "EcrLogin"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }
  statement {
    sid = "EcrSubirImagenes"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:BatchGetImage",
      "ecr:CompleteLayerUpload",
      "ecr:DescribeImages",
      "ecr:GetDownloadUrlForLayer",
      "ecr:InitiateLayerUpload",
      "ecr:PutImage",
      "ecr:UploadLayerPart",
    ]
    resources = var.arns_ecr
  }
  statement {
    sid       = "SubirArchivosDeDespliegue"
    actions   = ["s3:PutObject"]
    resources = ["${var.bucket_arn}/deploy/*"]
  }
  statement {
    sid     = "OrdenarElDespliegue"
    actions = ["ssm:SendCommand"]
    resources = [
      "arn:aws:ssm:${var.region}::document/AWS-RunShellScript",
      var.instancia_arn,
    ]
  }
  statement {
    sid       = "VerElResultadoDelComando"
    actions   = ["ssm:GetCommandInvocation", "ssm:ListCommandInvocations", "ssm:ListCommands"]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "pipeline" {
  count  = var.modo == "oidc" ? 1 : 0
  name   = "permisos-pipeline"
  role   = aws_iam_role.pipeline[0].id
  policy = data.aws_iam_policy_document.permisos.json
}

# Modo "claves": mismo conjunto de permisos, en un usuario que solo usa el pipeline (sin consola ni otros permisos).
resource "aws_iam_user" "pipeline" {
  count = var.modo == "claves" ? 1 : 0
  name  = "${var.nombre}-pipeline"
  tags  = { Uso = "GitHub Actions: imagenes a ECR y despliegue por SSM" }
}

resource "aws_iam_user_policy" "pipeline" {
  count  = var.modo == "claves" ? 1 : 0
  name   = "permisos-pipeline"
  user   = aws_iam_user.pipeline[0].name
  policy = data.aws_iam_policy_document.permisos.json
}

output "rol_arn" { value = var.modo == "oidc" ? aws_iam_role.pipeline[0].arn : "" }
output "usuario" { value = var.modo == "claves" ? aws_iam_user.pipeline[0].name : "" }
