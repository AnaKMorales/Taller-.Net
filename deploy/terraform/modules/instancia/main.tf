# Una EC2 Ubuntu con Docker. Ejecuta todos los contenedores (docker compose). Se administra por SSM, sin SSH.
variable "nombre" { type = string }
variable "ambiente" { type = string }
variable "region" { type = string }
variable "tipo_instancia" { type = string }
variable "volumen_gb" { type = number }
variable "subred_id" { type = string }
variable "security_group_id" { type = string }
variable "registro_ecr" { type = string }
variable "arns_ecr" { type = list(string) }
variable "bucket_nombre" { type = string }
variable "bucket_arn" { type = string }
variable "ruta_ssm" { type = string }

data "aws_caller_identity" "actual" {}

# AMI oficial de Canonical (Ubuntu 24.04 LTS, x86_64). Ya trae el agente de SSM.
data "aws_ssm_parameter" "ami" {
  name = "/aws/service/canonical/ubuntu/server/24.04/stable/current/amd64/hvm/ebs-gp3/ami-id"
}

data "aws_iam_policy_document" "asumir" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "this" {
  name               = "${var.nombre}-instancia"
  assume_role_policy = data.aws_iam_policy_document.asumir.json
}

# Permite que SSM Run Command ejecute el despliegue en la instancia.
resource "aws_iam_role_policy_attachment" "ssm" {
  role       = aws_iam_role.this.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

data "aws_iam_policy_document" "permisos" {
  statement {
    sid       = "EcrLogin"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }
  statement {
    sid = "EcrBajarImagenes"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:BatchGetImage",
      "ecr:GetDownloadUrlForLayer",
    ]
    resources = var.arns_ecr
  }
  statement {
    sid     = "ParametrosDeLaAplicacion"
    actions = ["ssm:GetParameter", "ssm:GetParameters", "ssm:GetParametersByPath"]
    resources = [
      "arn:aws:ssm:${var.region}:${data.aws_caller_identity.actual.account_id}:parameter${var.ruta_ssm}",
      "arn:aws:ssm:${var.region}:${data.aws_caller_identity.actual.account_id}:parameter${var.ruta_ssm}/*",
    ]
  }
  statement {
    sid       = "ArchivosDeDespliegue"
    actions   = ["s3:GetObject"]
    resources = ["${var.bucket_arn}/deploy/*"]
  }
  statement {
    sid       = "ListarArchivosDeDespliegue"
    actions   = ["s3:ListBucket"]
    resources = [var.bucket_arn]
  }
}

resource "aws_iam_role_policy" "permisos" {
  name   = "permisos-aplicacion"
  role   = aws_iam_role.this.id
  policy = data.aws_iam_policy_document.permisos.json
}

resource "aws_iam_instance_profile" "this" {
  name = "${var.nombre}-instancia"
  role = aws_iam_role.this.name
}

resource "aws_instance" "this" {
  ami                    = nonsensitive(data.aws_ssm_parameter.ami.value)
  instance_type          = var.tipo_instancia
  subnet_id              = var.subred_id
  vpc_security_group_ids = [var.security_group_id]
  iam_instance_profile   = aws_iam_instance_profile.this.name

  user_data = templatefile("${path.module}/user-data.sh.tftpl", {
    region   = var.region
    ambiente = var.ambiente
    bucket   = var.bucket_nombre
    registro = var.registro_ecr
    ruta_ssm = var.ruta_ssm
  })

  # IMDSv2 obligatorio y un solo salto: los contenedores no alcanzan las credenciales del rol de la instancia
  # (solo las usa la AWS CLI del anfitrion, en desplegar.sh).
  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 1
  }

  root_block_device {
    volume_type           = "gp3"
    volume_size           = var.volumen_gb
    encrypted             = true
    delete_on_termination = true
  }

  tags = { Name = var.nombre }

  # Una AMI nueva o un cambio del script de arranque no deben recrear la instancia (se perderian los datos).
  lifecycle {
    ignore_changes = [ami, user_data]
  }
}

# Direccion fija: el dominio no cambia al reiniciar la instancia.
resource "aws_eip" "this" {
  domain   = "vpc"
  instance = aws_instance.this.id
  tags     = { Name = var.nombre }
}

output "instance_id" { value = aws_instance.this.id }
output "arn" { value = aws_instance.this.arn }
output "ip_publica" { value = aws_eip.this.public_ip }
