terraform {
  required_version = ">= 1.5"

  backend "azurerm" {
    resource_group_name  = "rg-helios-tfstate"
    storage_account_name = "stheliosinfrastate"
    container_name       = "tfstate"
    key                  = "wisewizard.tfstate"
    use_azuread_auth     = true
    use_cli              = false
  }

  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.32"
    }
    null = {
      source  = "hashicorp/null"
      version = "~> 3.2"
    }
  }
}

provider "kubernetes" {
  config_path = var.kubeconfig_path
}
