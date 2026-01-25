# Observability (Local Stack)

This lab ships a local observability stack via Docker Compose. The PT-BR rationale and
stack diagram context live in `docs/study/04-ARCHITECTURE.md` (see “Observabilidade (stack local)”).

## Components
- **OpenTelemetry Collector**: single OTLP entrypoint, routes to metrics/logs/traces backends.
- **Prometheus**: metrics store (scrapes Collector internal + OTLP metrics endpoint).
- **Grafana**: dashboards + unified view.
- **Loki**: logs aggregation.
- **Tempo**: traces storage.

## OTLP contract
- OTLP gRPC: `http://otel-collector:4317`
- OTLP HTTP: `http://otel-collector:4318`

Pipelines:
- traces → Tempo
- metrics → Prometheus (exporter on `:8889`)
- logs → Loki

## Local URLs
- Grafana: http://localhost:3000
- Prometheus: http://localhost:9090
- Loki: http://localhost:3100
- Tempo: http://localhost:3200

See `docs/agent/LOCAL_DEV.md` for verification steps.
