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
  service            = each.key
  disable_on_destroy = false
}

# 2. Artifact Registry para imágenes Docker
resource "google_artifact_registry_repository" "repo" {
  location      = var.gcp_region
  repository_id = "laboratorio-concurrencia-repo"
  description   = "Repositorio Docker para la API .NET"
  format        = "DOCKER"
  depends_on    = [google_project_service.apis]
}

# 3. Service Account e IAM Roles
resource "google_service_account" "app_sa" {
  account_id   = "sa-backend-api"
  display_name = "Service Account para Cloud Run API"
}

resource "google_project_iam_member" "sa_roles" {
  for_each = toset([
    "roles/logging.logWriter",
    "roles/secretmanager.secretAccessor",
    "roles/cloudsql.client"
  ])
  project = var.gcp_project_id
  role    = each.key
  member  = "serviceAccount:${google_service_account.app_sa.email}"
}

# 4. Secret Manager (JWT Secret y DB Connection)
resource "google_secret_manager_secret" "jwt_secret" {
  secret_id = "jwt-secret"

  replication {
    user_managed {
      replicas {
        location = var.region
      }
    }
  }

  depends_on = [google_project_service.apis]
}

resource "google_secret_manager_secret_version" "jwt_secret_val" {
  secret      = google_secret_manager_secret.jwt_secret.id
  secret_data = var.jwt_secret
}

resource "google_secret_manager_secret" "db_url" {
  secret_id = "database-url"

  replication {
    user_managed {
      replicas {
        location = var.region
      }
    }
  }

  depends_on = [google_project_service.apis]
}

# 5. Cloud Run Service (.NET API)
resource "google_cloud_run_v2_service" "api_service" {
  name     = "api-backend-service"
  location = var.gcp_region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.app_sa.email

    containers {
      image = "${var.gcp_region}-docker.pkg.dev/${var.gcp_project_id}/${google_artifact_registry_repository.repo.name}/api:latest"

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

# Habilitar acceso público no autenticado a Cloud Run (para ser consumido por API Gateway / App)
resource "google_cloud_run_service_iam_member" "public_access" {
  location = google_cloud_run_v2_service.api_service.location
  service  = google_cloud_run_v2_service.api_service.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}