# Acceso de GitHub Actions a AWS por OIDC: credenciales temporales, sin claves guardadas en el repositorio.
# Solo puede asumir el rol el repositorio y la rama indicados.
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
  count          = var.crear_proveedor_oidc ? 1 : 0
  url            = local.url_oidc
  client_id_list = ["sts.amazonaws.com"]
}

data "aws_iam_openid_connect_provider" "github" {
  count = var.crear_proveedor_oidc ? 0 : 1
  url   = local.url_oidc
}

locals {
  arn_proveedor = var.crear_proveedor_oidc ? aws_iam_openid_connect_provider.github[0].arn : data.aws_iam_openid_connect_provider.github[0].arn
}

data "aws_iam_policy_document" "confianza" {
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
  name                 = "${var.nombre}-pipeline"
  assume_role_policy   = data.aws_iam_policy_document.confianza.json
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
  name   = "permisos-pipeline"
  role   = aws_iam_role.pipeline.id
  policy = data.aws_iam_policy_document.permisos.json
}

output "rol_arn" { value = aws_iam_role.pipeline.arn }
