# VPC con una sola subred pública (sin NAT Gateway, que tiene costo propio). La protección es el Security Group:
# solo entran los puertos 80 y 443. No se abre el 22: el acceso administrativo es por SSM.
variable "nombre" { type = string }
variable "cidr_vpc" { type = string }
variable "cidr_subred" { type = string }

data "aws_availability_zones" "disponibles" {
  state = "available"
}

resource "aws_vpc" "this" {
  cidr_block           = var.cidr_vpc
  enable_dns_support   = true
  enable_dns_hostnames = true
  tags                 = { Name = var.nombre }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id
  tags   = { Name = var.nombre }
}

resource "aws_subnet" "publica" {
  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.cidr_subred
  availability_zone       = data.aws_availability_zones.disponibles.names[0]
  map_public_ip_on_launch = true
  tags                    = { Name = "${var.nombre}-publica" }
}

resource "aws_route_table" "publica" {
  vpc_id = aws_vpc.this.id
  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.this.id
  }
  tags = { Name = "${var.nombre}-publica" }
}

resource "aws_route_table_association" "publica" {
  subnet_id      = aws_subnet.publica.id
  route_table_id = aws_route_table.publica.id
}

resource "aws_security_group" "web" {
  name        = "${var.nombre}-web"
  description = "Entrada solo por HTTP y HTTPS (Traefik); sin SSH"
  vpc_id      = aws_vpc.this.id
  tags        = { Name = "${var.nombre}-web" }
}

resource "aws_vpc_security_group_ingress_rule" "http" {
  security_group_id = aws_security_group.web.id
  description       = "HTTP (desafio de Lets Encrypt y redireccion a HTTPS)"
  ip_protocol       = "tcp"
  from_port         = 80
  to_port           = 80
  cidr_ipv4         = "0.0.0.0/0"
}

resource "aws_vpc_security_group_ingress_rule" "https" {
  security_group_id = aws_security_group.web.id
  description       = "HTTPS"
  ip_protocol       = "tcp"
  from_port         = 443
  to_port           = 443
  cidr_ipv4         = "0.0.0.0/0"
}

# Salida libre: la instancia baja imagenes de ECR, habla con SSM y con Lets Encrypt.
resource "aws_vpc_security_group_egress_rule" "todo" {
  security_group_id = aws_security_group.web.id
  ip_protocol       = "-1"
  cidr_ipv4         = "0.0.0.0/0"
}

output "subred_id" { value = aws_subnet.publica.id }
output "security_group_id" { value = aws_security_group.web.id }
