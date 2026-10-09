variable "gcp_project_id" {
  type        = string
  description = "ID del Proyecto de Google Cloud"
  default ="laboratorio-concurrencia"
}

variable "gcp_region" {
  type        = string
  default     = "us-central1"
  description = "Región de despliegue en GCP"
}

variable "database_url" {
  type        = string
  sensitive   = true
  description = "Cadena de conexión a la base de datos PostgreSQL"
}

variable "jwt_secret" {
  type        = string
  sensitive   = true
  description = "Clave secreta para la firma de JWT"
}