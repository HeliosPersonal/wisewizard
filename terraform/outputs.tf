# Connection string template to store in Infisical as CONNECTIONSTRINGS__WISEWIZARD.
# Append ;Password=<pg_password> when adding it to Infisical.
output "connection_string_template" {
  value       = "${local.postgres_connection_string};Database=${local.pg_database}"
  description = "Npgsql connection string (no password). Store in Infisical as CONNECTIONSTRINGS__WISEWIZARD with ;Password=<pg_password> appended."
  sensitive   = false
}

output "ibkr_gateway_url" {
  value       = "https://${local.ibkr_host}"
  description = "URL to open in browser for daily IBKR 2FA re-authentication."
  sensitive   = false
}
