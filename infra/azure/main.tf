################################################################################
# TheLawyer — Azure infrastructure (skeleton)
#
# Foundations epic ships the scaffolding: resource group, vnet, key vault,
# Postgres + pgvector, Redis, Blob storage, Container Apps environment.
# Later epics flesh out specific resources (B2C tenant config, App Insights
# workbooks, alert rules, the Container Apps for the Api + Hangfire dashboard +
# Web).
#
# Per ARCH.md ADR-0005 (single region) and ADR-0004 (pgvector in same Postgres).
################################################################################

locals {
  prefix = "thelawyer-${var.environment}"
  tags   = merge(var.tags, { environment = var.environment })
}

resource "random_string" "suffix" {
  length  = 6
  upper   = false
  special = false
}

# --- Resource group --------------------------------------------------------

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.prefix}-${random_string.suffix.result}"
  location = var.location
  tags     = local.tags
}

# --- Networking ------------------------------------------------------------

resource "azurerm_virtual_network" "main" {
  name                = "vnet-${local.prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  address_space       = ["10.10.0.0/16"]
  tags                = local.tags
}

resource "azurerm_subnet" "container_apps" {
  name                 = "snet-container-apps"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.10.1.0/23"]
}

resource "azurerm_subnet" "private_endpoints" {
  name                              = "snet-private-endpoints"
  resource_group_name               = azurerm_resource_group.main.name
  virtual_network_name              = azurerm_virtual_network.main.name
  address_prefixes                  = ["10.10.4.0/24"]
  private_endpoint_network_policies = "Disabled"
}

# --- Key Vault -------------------------------------------------------------

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "main" {
  name                       = "kv-${local.prefix}-${random_string.suffix.result}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  enable_rbac_authorization  = true
  purge_protection_enabled   = var.environment == "prod"
  soft_delete_retention_days = var.environment == "prod" ? 90 : 7
  tags                       = local.tags
}

# --- Postgres + pgvector ---------------------------------------------------

resource "azurerm_postgresql_flexible_server" "main" {
  name                          = "pg-${local.prefix}-${random_string.suffix.result}"
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  version                       = "16"
  sku_name                      = var.environment == "prod" ? "GP_Standard_D2ds_v5" : "B_Standard_B1ms"
  storage_mb                    = 32768
  administrator_login           = var.postgres_admin_login
  administrator_password        = var.postgres_admin_password
  zone                          = "1"
  high_availability_enabled     = var.environment == "prod"
  geo_redundant_backup_enabled  = var.environment == "prod"
  public_network_access_enabled = var.environment == "dev"
  tags                          = local.tags
}

resource "azurerm_postgresql_flexible_server_database" "thelawyer" {
  name      = "thelawyerdb"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_configuration" "pgvector" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "pgvector"
}

# --- Redis -----------------------------------------------------------------

resource "azurerm_redis_cache" "main" {
  name                          = "redis-${local.prefix}-${random_string.suffix.result}"
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  capacity                      = 0
  family                        = "C"
  sku_name                      = var.environment == "prod" ? "Standard" : "Basic"
  non_ssl_port_enabled          = false
  minimum_tls_version           = "1.2"
  public_network_access_enabled = var.environment == "dev"
  tags                          = local.tags
}

# --- Blob storage (per-matter SAS, ADR-0005) -------------------------------

resource "azurerm_storage_account" "documents" {
  name                            = "stdocs${replace(local.prefix, "-", "")}${random_string.suffix.result}"
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = var.environment == "prod" ? "GRS" : "LRS"
  account_kind                    = "StorageV2"
  https_traffic_only_enabled      = true
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  public_network_access_enabled   = var.environment == "dev"
  tags                            = local.tags
}

resource "azurerm_storage_container" "matters" {
  name                  = "matter-documents"
  storage_account_id    = azurerm_storage_account.documents.id
  container_access_type = "private"
}

# --- Container Apps environment --------------------------------------------

resource "azurerm_log_analytics_workspace" "main" {
  name                = "law-${local.prefix}-${random_string.suffix.result}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${local.prefix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  infrastructure_subnet_id   = azurerm_subnet.container_apps.id
  tags                       = local.tags
}

# Container Apps themselves (Api, Web, Hangfire-dashboard) are added by the
# build/deploy pipeline using the Aspire-generated manifest. Skeleton ends here.
