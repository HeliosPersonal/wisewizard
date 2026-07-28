# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# IBKR Client Portal API Gateway
# Downloads and packages the IBKR Client Portal Gateway (Java).
# After deployment, complete the daily 2FA via kubectl port-forward (ADR-0006).
# ---------------------------------------------------------------------------
FROM eclipse-temurin:21-jre-alpine AS runtime

RUN apk add --no-cache curl unzip bash

WORKDIR /gateway

# Download the IBKR Client Portal Gateway zip from the public IBKR distribution.
# Pin to a specific version URL when IBKR releases updates.
RUN curl -fL -o clientportal.gw.zip \
        "https://download2.interactivebrokers.com/portal/clientportal.gw.zip" && \
    unzip -q clientportal.gw.zip && \
    rm clientportal.gw.zip && \
    chmod +x bin/run.sh

# Override the default config with our container-friendly version.
COPY conf.yaml root/conf.yaml

# Gateway REST API and web UI (HTTPS, self-signed cert).
EXPOSE 5000

ENTRYPOINT ["bin/run.sh", "root/conf.yaml"]
