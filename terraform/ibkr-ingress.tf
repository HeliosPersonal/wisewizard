# ====================================================================================
# IBKR GATEWAY INGRESS
# ====================================================================================
# Exposes the IBKR Client Portal Gateway login page at ibkr.<base_domain> so the
# Owner can authenticate (daily 2FA) from any browser without kubectl port-forward.
#
# Security:
#   - Basic Auth (username/password) in front of the gateway login page
#   - HTTPS terminated by Cloudflare; nginx ↔ gateway over HTTPS (self-signed cert)
#   - The gateway itself is ClusterIP-only; only nginx can reach it
#
# After apply: open https://ibkr.<base_domain>, enter basic auth credentials,
# then log in with your IBKR account (username + 2FA).
# ====================================================================================

locals {
  ibkr_host = "ibkr.${data.terraform_remote_state.infra.outputs.base_domain}"
}

# htpasswd-format credentials stored as a K8s Secret.
# Generate the hash with: htpasswd -nb <username> <password>
# Pass it via TF_VAR_ibkr_basic_auth_credentials or terraform.secret.tfvars.
resource "kubernetes_secret_v1" "ibkr_basic_auth" {
  metadata {
    name      = "ibkr-basic-auth"
    namespace = local.namespace_production
  }

  type = "Opaque"

  data = {
    auth = var.ibkr_basic_auth_credentials
  }
}

# Ingress: ibkr.<base_domain> → ibkr-gateway service (HTTPS backend, self-signed cert)
resource "kubernetes_ingress_v1" "ibkr_gateway" {
  metadata {
    name      = "ibkr-gateway"
    namespace = local.namespace_production

    annotations = {
      # Basic Auth guard
      "nginx.ingress.kubernetes.io/auth-type"   = "basic"
      "nginx.ingress.kubernetes.io/auth-secret" = kubernetes_secret_v1.ibkr_basic_auth.metadata[0].name
      "nginx.ingress.kubernetes.io/auth-realm"  = "IBKR Gateway — Owner only"

      # Gateway uses self-signed HTTPS; tell nginx to use HTTPS backend and skip cert verify
      "nginx.ingress.kubernetes.io/backend-protocol"             = "HTTPS"
      "nginx.ingress.kubernetes.io/proxy-ssl-verify"             = "off"

      # Increase timeouts — gateway login can be slow
      "nginx.ingress.kubernetes.io/proxy-read-timeout" = "300"
      "nginx.ingress.kubernetes.io/proxy-send-timeout" = "300"

      # Pass the original host header so the gateway redirects correctly
      "nginx.ingress.kubernetes.io/upstream-vhost" = local.ibkr_host
    }
  }

  spec {
    ingress_class_name = "nginx"

    tls {
      # Reuse the shared Cloudflare origin cert from infra-production namespace.
      # This secret lives in infra-production; we reference it by copying to apps-production.
      secret_name = kubernetes_secret_v1.ibkr_tls.metadata[0].name
      hosts       = [local.ibkr_host]
    }

    rule {
      host = local.ibkr_host

      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "ibkr-gateway"
              port {
                number = 5000
              }
            }
          }
        }
      }
    }
  }

  depends_on = [kubernetes_secret_v1.ibkr_basic_auth, kubernetes_secret_v1.ibkr_tls]
}

# Copy the shared Cloudflare origin TLS cert into apps-production so the Ingress can reference it.
# (K8s Ingress TLS secrets must be in the same namespace as the Ingress.)
data "kubernetes_secret_v1" "cloudflare_origin" {
  metadata {
    name      = "cloudflare-origin"
    namespace = local.namespace_infra
  }
}

resource "kubernetes_secret_v1" "ibkr_tls" {
  metadata {
    name      = "cloudflare-origin"
    namespace = local.namespace_production
  }

  type        = "kubernetes.io/tls"
  binary_data = data.kubernetes_secret_v1.cloudflare_origin.binary_data

  lifecycle {
    ignore_changes = [binary_data]
  }
}
