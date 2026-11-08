# Bucket con los archivos de despliegue (compose, desplegar.sh). El pipeline los sube y la instancia los baja:
# asi la instancia no necesita acceso al repositorio de GitHub ni claves.
variable "nombre" { type = string }

data "aws_caller_identity" "actual" {}

resource "aws_s3_bucket" "this" {
  bucket        = "${var.nombre}-despliegue-${data.aws_caller_identity.actual.account_id}"
  force_destroy = true
}

resource "aws_s3_bucket_versioning" "this" {
  bucket = aws_s3_bucket.this.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "this" {
  bucket = aws_s3_bucket.this.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "this" {
  bucket                  = aws_s3_bucket.this.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

output "nombre" { value = aws_s3_bucket.this.id }
output "arn" { value = aws_s3_bucket.this.arn }
