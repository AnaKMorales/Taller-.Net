# Alerta de costos: avisa por correo al 80 % del gasto real y cuando se pronostica pasar el 100 %.
variable "nombre" { type = string }
variable "limite_usd" { type = number }
variable "correo" { type = string }

resource "aws_budgets_budget" "mensual" {
  name         = "${var.nombre}-mensual"
  budget_type  = "COST"
  limit_amount = tostring(var.limite_usd)
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.correo]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = [var.correo]
  }
}
