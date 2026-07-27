# ====================================================================================
# POSTGRESQL - CREATE DATABASE
# ====================================================================================
# Creates the wisewizard database inside the shared postgres instance managed by
# infrastructure-helios. Idempotent: only creates if the database does not exist.
# The connection string (including password) is stored in Infisical, not here.
# ====================================================================================

resource "null_resource" "create_postgres_database" {
  triggers = {
    database        = local.pg_database
    host            = local.postgres_host
    pw_hash         = sha256(var.pg_password)
    kubeconfig_path = var.kubeconfig_path
  }

  provisioner "local-exec" {
    command = <<-SH
      set -e
      export KUBECONFIG='${var.kubeconfig_path}'

      PG_POD=$(kubectl get pod -n ${local.namespace_infra} \
        -l app.kubernetes.io/name=postgresql \
        -o jsonpath='{.items[0].metadata.name}')

      echo ">>> Using postgres pod: $PG_POD"
      echo ">>> Ensuring database: ${local.pg_database}"

      kubectl exec -n ${local.namespace_infra} "$PG_POD" \
        -- bash -c "PGPASSWORD='${var.pg_password}' psql -U postgres -tc \
          \"SELECT 1 FROM pg_database WHERE datname='${local.pg_database}'\" \
          | grep -q 1 || PGPASSWORD='${var.pg_password}' psql -U postgres \
          -c \"CREATE DATABASE \\\"${local.pg_database}\\\"\""

      echo ">>> Done."
    SH
  }
}
