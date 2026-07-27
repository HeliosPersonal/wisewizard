data "terraform_remote_state" "infra" {
  backend = "azurerm"

  config = {
    resource_group_name  = "rg-helios-tfstate"
    storage_account_name = "stheliosinfrastate"
    container_name       = "tfstate"
    key                  = "infrastructure-helios.tfstate"
    use_azuread_auth     = true
    use_cli              = false
  }
}

locals {
  namespace_infra      = data.terraform_remote_state.infra.outputs.namespace_infra_production  # "infra-production"
  namespace_production = data.terraform_remote_state.infra.outputs.namespace_apps_production   # "apps-production"

  postgres_host              = data.terraform_remote_state.infra.outputs.postgres_host              # postgres.infra-production.svc.cluster.local
  postgres_port              = data.terraform_remote_state.infra.outputs.postgres_port              # 5432
  postgres_connection_string = data.terraform_remote_state.infra.outputs.postgres_connection_string # Host=...;Port=5432;Username=postgres

  pg_database = "wisewizard"
}
