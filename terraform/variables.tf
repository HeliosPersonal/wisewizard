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
