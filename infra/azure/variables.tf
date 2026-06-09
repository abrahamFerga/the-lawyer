variable "environment" {
  description = "Deployment environment (dev | staging | prod). Drives sku sizing and HA toggles."
  type        = string
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be one of: dev, staging, prod."
  }
}

variable "location" {
  description = "Azure region. Per ARCH.md ADR-0005 we are single-region for v1."
  type        = string
  default     = "eastus2"
}

variable "tags" {
  description = "Common tags applied to every resource (cost-center, owner, etc.)."
  type        = map(string)
  default = {
    project = "thelawyer"
    iac     = "terraform"
  }
}

variable "postgres_admin_login" {
  description = "Admin login for the managed Postgres flexible server."
  type        = string
}

variable "postgres_admin_password" {
  description = "Admin password. Provide via TF_VAR_postgres_admin_password from Key Vault in pipelines."
  type        = string
  sensitive   = true
}
