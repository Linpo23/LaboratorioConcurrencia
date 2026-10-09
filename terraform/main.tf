# 1. Habilitar APIs necesarias
resource "google_project_service" "apis" {
  for_each = toset([
    "run.googleapis.com",
    "artifactregistry.googleapis.com",
    "secretmanager.googleapis.com",
    "sqladmin.googleapis.com",
    "apigateway.googleapis.com",
    "cloudbuild.googleapis.com",
    "logging.googleapis.com"
  ])
  project            = "laboratorio-concurrencia"
  service            = each.key
  disable_on_destroy = false
}

# 2. Artifact Registry para imágenes Docker
resource "google_artifact_registry_repository" "repo" {
  project       = "laboratorio-concurrencia"
  location      = var.gcp_region
  repository_id = "laboratorio-concurrencia-repo"
  description   = "Repositorio Docker para la API .NET"
  format        = "DOCKER"
  depends_on    = [google_project_service.apis]
}

# 3. Service Account e IAM Roles
resource "google_service_account" "app_sa" {
  project      = "laboratorio-concurrencia"
  account_id   = "sa-backend-api"
  display_name = "Service Account para Cloud Run API"
}

resource "google_project_iam_member" "sa_roles" {
  for_each = toset([
    "roles/logging.logWriter",
    "roles/secretmanager.secretAccessor",
    "roles/cloudsql.client"
  ])
  project = "laboratorio-concurrencia"
  role    = each.key
  member  = "serviceAccount:${google_service_account.app_sa.email}"
}

# 4. Secret Manager (JWT Secret y DB Connection)
resource "google_secret_manager_secret" "jwt_secret" {
  project   = "laboratorio-concurrencia"
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
  project   = "laboratorio-concurrencia"
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
  project  = "laboratorio-concurrencia"
  name     = "api-backend-service"
  location = var.gcp_region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.app_sa.email

    containers {
      image = "${var.gcp_region}-docker.pkg.dev/laboratorio-concurrencia/${google_artifact_registry_repository.repo.name}/api:latest"

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
    google_secret_manager_secret_version.jwt_secret_val,
    google_secret_manager_secret_version.db_url_val
  ]
}

# Habilitar acceso público no autenticado a Cloud Run
resource "google_cloud_run_service_iam_member" "public_access" {
  project  = "laboratorio-concurrencia"
  location = google_cloud_run_v2_service.api_service.location
  service  = google_cloud_run_v2_service.api_service.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
# Importar Artifact Registry existente
import {
  to = google_artifact_registry_repository.repo
  id = "projects/laboratorio-concurrencia/locations/${var.gcp_region}/repositories/laboratorio-concurrencia-repo"
}

# Importar Service Account existente
import {
  to = google_service_account.app_sa
  id = "projects/laboratorio-concurrencia/serviceAccounts/sa-backend-api@laboratorio-concurrencia.iam.gserviceaccount.com"
}