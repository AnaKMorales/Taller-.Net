# Un repositorio por imagen propia. Las etiquetas son el SHA del commit y no se pueden pisar (IMMUTABLE).
variable "prefijo" { type = string }
variable "imagenes" { type = list(string) }
variable "conservar" {
  type    = number
  default = 10
}
variable "forzar_borrado" {
  type        = bool
  default     = true
  description = "Permite que terraform destroy borre repositorios con imagenes."
}

resource "aws_ecr_repository" "this" {
  for_each             = toset(var.imagenes)
  name                 = "${var.prefijo}-${each.key}"
  image_tag_mutability = "IMMUTABLE"
  force_delete         = var.forzar_borrado

  image_scanning_configuration {
    scan_on_push = true
  }
  encryption_configuration {
    encryption_type = "AES256"
  }
}

resource "aws_ecr_lifecycle_policy" "this" {
  for_each   = aws_ecr_repository.this
  repository = each.value.name
  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Conservar las ultimas ${var.conservar} imagenes"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = var.conservar
      }
      action = { type = "expire" }
    }]
  })
}

output "arns" { value = [for r in aws_ecr_repository.this : r.arn] }
output "urls" { value = { for k, r in aws_ecr_repository.this : k => r.repository_url } }
output "registro" { value = split("/", values(aws_ecr_repository.this)[0].repository_url)[0] }
