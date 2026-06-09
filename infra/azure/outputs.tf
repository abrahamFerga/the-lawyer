output "resource_group_name" {
  value       = azurerm_resource_group.main.name
  description = "The resource group that holds every TheLawyer resource for this environment."
}

output "postgres_fqdn" {
  value       = azurerm_postgresql_flexible_server.main.fqdn
  description = "FQDN of the Postgres flexible server. Connection string composed by the app via managed identity."
}

output "redis_hostname" {
  value       = azurerm_redis_cache.main.hostname
  description = "Hostname of the Redis cache."
}

output "documents_storage_account_name" {
  value       = azurerm_storage_account.documents.name
  description = "Blob storage account holding matter documents."
}

output "key_vault_uri" {
  value       = azurerm_key_vault.main.vault_uri
  description = "Key Vault URI. The Api container app reads connector tokens and the Claude API key from here via managed identity."
}

output "container_app_environment_id" {
  value       = azurerm_container_app_environment.main.id
  description = "Container Apps environment. The Aspire deployment manifest provisions the Api/Web/Hangfire-dashboard apps into this env."
}
