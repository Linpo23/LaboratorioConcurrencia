terraform {
  required_version = ">= 1.6"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }

  # RECOMENDADO: descomenta esto cuando crees el bucket de state.
  # Una vez que el primer apply con backend funcione, puedes borrar
  # todos los bloques "import" de este archivo.
  #
  # backend "gcs" {
  #   bucket = "NOMBRE-DE-TU-BUCKET-TFSTATE"
  #   prefix = "laboratorio-concurrencia"
  # }
}

locals {
  project_id = "laboratorio-concurrencia"
}

# Configuración del Proveedor GCP
provider "google" {
  project = local.project_id
  region  = var.gcp_region
}

# 1. Habilitar APIs necesarias
resource "google_project_service" "apis" {
  for_each = toset([
    "run.googleapis.com",
    "artifactregistry.googleapis.com",
    "secretmanager.googleapis.com",
    "sqladmin.googleapis.com",
    "apigateway.googleapis.com",
    "cloudbuild.googleapis.com",
    "logging.googleapis.com",
    "pubsub.googleapis.com",
    "iam.googleapis.com"
  ])
  project            = local.project_id
  service            = each.key
  disable_on_destroy = false
}

# 2. Artifact Registry para imágenes Docker
resource "google_artifact_registry_repository" "repo" {
  project       = local.project_id
  location      = var.gcp_region
  repository_id = "laboratorio-concurrencia-repo"
  description   = "Repositorio Docker para la API .NET"
  format        = "DOCKER"
  depends_on    = [google_project_service.apis]
}

# 3. Service Account e IAM Roles
resource "google_service_account" "app_sa" {
  project      = local.project_id
  account_id   = "sa-backend-api"
  display_name = "Service Account para Cloud Run API"
}

resource "google_project_iam_member" "sa_roles" {
  for_each = toset([
    "roles/logging.logWriter",
    "roles/secretmanager.secretAccessor",
    "roles/cloudsql.client",
    "roles/pubsub.publisher" # la API publica las notificaciones en Pub/Sub
  ])
  project = local.project_id
  role    = each.key
  member  = "serviceAccount:${google_service_account.app_sa.email}"
}

# 4. Secret Manager (JWT Secret y DB Connection)
resource "google_secret_manager_secret" "jwt_secret" {
  project   = local.project_id
  secret_id = "jwt-secret"

  replication {
    auto {}
  }

  depends_on = [google_project_service.apis]
}

resource "google_secret_manager_secret_version" "jwt_secret_val" {
  secret      = google_secret_manager_secret.jwt_secret.id
  secret_data = var.jwt_secret
}

resource "google_secret_manager_secret" "db_url" {
  project   = local.project_id
  secret_id = "database-url"

  replication {
    auto {}
  }

  depends_on = [google_project_service.apis]
}

resource "google_secret_manager_secret_version" "db_url_val" {
  secret      = google_secret_manager_secret.db_url.id
  secret_data = var.database_url
}

# 5. Cloud Run Service (.NET API)
resource "google_cloud_run_v2_service" "api_service" {
  project  = local.project_id
  name     = "api-backend-service"
  location = var.gcp_region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.app_sa.email

    containers {
      # CORREGIDO: repository_id en lugar de name (name devuelve la ruta completa)
      image = "${var.gcp_region}-docker.pkg.dev/${local.project_id}/${google_artifact_registry_repository.repo.repository_id}/api:${var.api_image_tag}"

      ports {
        container_port = 5030
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name = "ConnectionStrings__DefaultConnection"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_url.secret_id
            version = "latest"
          }
        }
      }

      env {
        name = "Jwt__Key"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.jwt_secret.secret_id
            version = "latest"
          }
        }
      }
    }
  }

  depends_on = [
    google_project_service.apis,
    google_project_iam_member.sa_roles,
    google_secret_manager_secret_version.jwt_secret_val,
    google_secret_manager_secret_version.db_url_val
  ]
}

# Acceso público no autenticado a Cloud Run
# CORREGIDO: recurso v2 (antes era google_cloud_run_service_iam_member, de v1)
resource "google_cloud_run_v2_service_iam_member" "public_access" {
  project  = local.project_id
  location = google_cloud_run_v2_service.api_service.location
  name     = google_cloud_run_v2_service.api_service.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}


# Tópico de Pub/Sub para notificaciones
resource "google_pubsub_topic" "notifications_topic" {
  project    = local.project_id
  name       = "notification-email-topic-v2"
  depends_on = [google_project_service.apis]
}

# Servicio Cloud Run para enviar las notificaciones
resource "google_cloud_run_v2_service" "notification_service" {
  project  = local.project_id
  name     = "notification-cloud-run-v2"
  location = var.gcp_region

  template {
    containers {
      image = var.notification_image

      env {
        name  = "SMTP_USER"
        value = var.smtp_user
      }
      env {
        name  = "SMTP_PASSWORD"
        value = var.smtp_password
      }
    }
  }

  depends_on = [google_project_service.apis]
}

# Service Account para permitir que Pub/Sub invoque Cloud Run
resource "google_service_account" "pubsub_invoker" {
  project      = local.project_id
  account_id   = "pubsub-cloudrun-invoker-v2"
  display_name = "PubSub Cloud Run Invoker SA"
}

resource "google_cloud_run_v2_service_iam_member" "invoker_role" {
  project  = local.project_id
  location = google_cloud_run_v2_service.notification_service.location
  name     = google_cloud_run_v2_service.notification_service.name
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.pubsub_invoker.email}"
}

# Suscripción Push de Pub/Sub hacia Cloud Run Notificaciones
resource "google_pubsub_subscription" "notification_sub" {
  project = local.project_id
  name    = "notification-email-sub-v2"
  topic   = google_pubsub_topic.notifications_topic.name

  push_config {
    push_endpoint = google_cloud_run_v2_service.notification_service.uri

    oidc_token {
      service_account_email = google_service_account.pubsub_invoker.email
    }
  }
}

# ---------------------------------------------------------------------------
# IMPORTS (temporales, mientras no haya backend remoto)
# Los ids usan la región literal: es lo más seguro en un bloque import.
# Si el recurso NO existe en GCP, borra su bloque import y deja que apply lo cree.
# ---------------------------------------------------------------------------

import {
  to = google_service_account.app_sa
  id = "projects/laboratorio-concurrencia/serviceAccounts/sa-backend-api@laboratorio-concurrencia.iam.gserviceaccount.com"
}

import {
  to = google_secret_manager_secret.jwt_secret
  id = "projects/laboratorio-concurrencia/secrets/jwt-secret"
}

import {
  to = google_secret_manager_secret.db_url
  id = "projects/laboratorio-concurrencia/secrets/database-url"
}

import {
  to = google_artifact_registry_repository.repo
  id = "projects/laboratorio-concurrencia/locations/us-central1/repositories/laboratorio-concurrencia-repo"
}

import {
  to = google_cloud_run_v2_service.api_service
  id = "projects/laboratorio-concurrencia/locations/us-central1/services/api-backend-service"
}

import {
  to = google_pubsub_topic.notifications_topic
  id = "projects/laboratorio-concurrencia/topics/notification-email-topic-v2"
}

# CORREGIDO: antes decía locations/var.gcp_region (texto literal)
import {
  to = google_cloud_run_v2_service.notification_service
  id = "projects/laboratorio-concurrencia/locations/us-central1/services/notification-cloud-run-v2"
}

import {
  to = google_service_account.pubsub_invoker
  id = "projects/laboratorio-concurrencia/serviceAccounts/pubsub-cloudrun-invoker-v2@laboratorio-concurrencia.iam.gserviceaccount.com"
}

# NUEVO: causa del error 409 de la suscripción
import {
  to = google_pubsub_subscription.notification_sub
  id = "projects/laboratorio-concurrencia/subscriptions/notification-email-sub-v2"
}
