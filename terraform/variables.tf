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
variable "smtp_user" {
  type        = string
  description = "Usuario SMTP para el envío de correos"
}

variable "smtp_password" {
  type        = string
  description = "Contraseña de la cuenta SMTP"
  sensitive   = true
}

variable "notification_image" {
  type        = string
  description = "Imagen Docker para notification-cloud-run"
  default     = "gcr.io/google-samples/hello-app:1.0"
}