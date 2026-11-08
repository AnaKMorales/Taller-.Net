terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # El bucket lleva el id de la cuenta en el nombre y no admite variables aqui: se pasa al inicializar
  #   terraform init -backend-config=backend.hcl      (ver backend.hcl.example)
  backend "s3" {
    key          = "dev/terraform.tfstate"
    use_lockfile = true # bloqueo nativo de S3, sin tabla de DynamoDB
    encrypt      = true
  }
}

provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Proyecto  = "ultimamilla"
      Ambiente  = var.ambiente
      ManagedBy = "terraform"
    }
  }
}
