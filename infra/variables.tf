variable "aws_region" {
  description = "Região AWS (decidida no ADR-010)"
  type        = string
  default     = "us-east-1"
}

variable "function_name" {
  description = "Nome da função Lambda"
  type        = string
  default     = "oficina-mecanica-auth"
}

variable "db_connection_string" {
  description = "Connection string do RDS (Host=...;Port=...;Database=...;Username=...;Password=...) — passada via TF_VAR, nunca hardcoded"
  type        = string
  sensitive   = true
}

variable "jwt_key" {
  description = "Chave HMAC-SHA256 do JWT — mesma usada em Tech-challenge, compartilhada via secret"
  type        = string
  sensitive   = true
}

variable "jwt_issuer" {
  description = "Issuer do JWT — deve bater com o esperado pelo middleware de auth do Atendimento"
  type        = string
  default     = "oficina-atendimento"
}

variable "jwt_audience" {
  description = "Audience do JWT — deve bater com o esperado pelo middleware de auth do Atendimento"
  type        = string
  default     = "oficina-atendimento-api"
}
