variable "region" {
  type        = string
  default     = "us-east-2"
  description = "La SCP de la organizacion de la cuenta solo permite EC2 y ECR en us-east-2 (el resto de las regiones se probo y esta denegado)."
}

variable "ambiente" {
  type        = string
  default     = "dev"
  description = "Nombre del ambiente; forma parte de los nombres de los recursos y de la ruta en SSM."
}

variable "dominio" {
  type        = string
  default     = ""
  description = "Dominio propio (ej. ultimamilla.example.com); su registro A debe apuntar a la Elastic IP. Si queda vacio se usa <ip-con-guiones>.sslip.io, que resuelve solo a esa IP y permite sacar el certificado de Lets Encrypt sin comprar un dominio."
}

variable "correo_acme" {
  type        = string
  description = "Correo que Lets Encrypt usa para avisos de vencimiento."
}

variable "repositorio_github" {
  type        = string
  default     = "AnaKMorales/Taller-.Net"
  description = "Repositorio (organizacion/nombre) autorizado a desplegar por OIDC."
}

variable "rama_despliegue" {
  type    = string
  default = "main"
}

variable "tipo_instancia" {
  type        = string
  default     = "c7i-flex.large"
  description = "c7i-flex.large (2 vCPU, 4 GB) o m7i-flex.large (8 GB). Tipos x86_64: la AMI es amd64 (no sirven los t4g, que son ARM). El plan Free de la cuenta solo permite c7i-flex.large, m7i-flex.large, t3.micro, t3.small, t4g.*, t8i.micro y t8i.small."
}

variable "volumen_gb" {
  type    = number
  default = 30
}

variable "presupuesto_mensual_usd" {
  type    = number
  default = 40
}

variable "correo_presupuesto" {
  type        = string
  default     = ""
  description = "Destino de las alertas de costo; si queda vacio se usa correo_acme."
}

variable "habilitar_cicd" {
  type        = bool
  default     = true
  description = "false omite por completo el modulo cicd (proveedor OIDC de GitHub y rol del pipeline): no se crea ni se busca nada de OIDC. Para cuentas cuya SCP niega iam:CreateOpenIDConnectProvider / ListOpenIDConnectProviders. Sin cicd no hay despliegue desde GitHub: se usa deploy/aws/desplegar-manual.sh."
}

variable "crear_proveedor_oidc" {
  type        = bool
  default     = true
  description = "Solo si habilitar_cicd = true. false si la cuenta ya tiene el proveedor token.actions.githubusercontent.com (solo admite uno)."
}

variable "zona_route53_id" {
  type        = string
  default     = ""
  description = "Si el dominio esta en Route 53, id de la zona: crea el registro A hacia la Elastic IP."
}

variable "cidr_vpc" {
  type    = string
  default = "10.20.0.0/16"
}

variable "cidr_subred" {
  type    = string
  default = "10.20.1.0/24"
}
