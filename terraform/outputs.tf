output "cloud_run_url" {
  value       = google_cloud_run_v2_service.api_service.uri
  description = "URL publica generada por Cloud Run"
}

output "artifact_registry_url" {
  value       = "${var.gcp_region}-docker.pkg.dev/${var.gcp_project_id}/${google_artifact_registry_repository.repo.name}"
  description = "Ruta del repositorio Artifact Registry"
}