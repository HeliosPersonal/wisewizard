variable "kubeconfig_path" {
  type        = string
  default     = "~/.kube/config"
  description = "Path to kubeconfig for k3s cluster"
}

variable "pg_password" {
  type        = string
  sensitive   = true
  description = "PostgreSQL admin password (same as set in infrastructure-helios)"
}

variable "ibkr_basic_auth_credentials" {
  type        = string
  sensitive   = true
  description = "htpasswd-formatted credentials for the IBKR gateway basic-auth Ingress. Generate with: htpasswd -nb <username> <password>"
}
