# Parametros en SSM Parameter Store bajo /<prefijo>/<ambiente>/. desplegar.sh los baja a un .env en la instancia.
# Las dos claves internas se generan al azar (nadie las necesita conocer); quedan en el estado de Terraform, que vive
# en un bucket S3 cifrado y privado. Si se recrean, hay que recrear tambien los volumenes de Postgres/RabbitMQ.
variable "ruta" { type = string }
variable "dominio" { type = string }
variable "correo_acme" { type = string }

resource "random_password" "postgres" {
  length  = 32
  special = false
}

resource "random_password" "rabbitmq" {
  length  = 32
  special = false
}

resource "aws_ssm_parameter" "secreto" {
  for_each = {
    POSTGRES_PASSWORD = random_password.postgres.result
    RABBITMQ_PASSWORD = random_password.rabbitmq.result
  }
  name  = "${var.ruta}/${each.key}"
  type  = "SecureString"
  value = each.value
}

resource "aws_ssm_parameter" "config" {
  for_each = {
    DOMINIO    = var.dominio
    ACME_EMAIL = var.correo_acme
  }
  name  = "${var.ruta}/${each.key}"
  type  = "String"
  value = each.value
}

output "ruta" { value = var.ruta }
