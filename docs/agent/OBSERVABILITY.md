# Observability (Local Stack)

This lab ships a local observability stack via Docker Compose. The PT-BR rationale and
stack diagram context live in `docs/study/04-ARCHITECTURE.md` (see “Observabilidade (stack local)”,
“Instrumentação de banco (PostgreSQL)”, and “Instrumentação de cache (Redis)”).

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

## PostgreSQL tracing (manual)
The .NET OpenTelemetry ecosystem does **not** provide a stable
`OpenTelemetry.Instrumentation.Npgsql` package. To keep the lab realistic and stable, the services
use **manual spans** via `ActivitySource` around relevant SQL operations (feed/graph reads + writes,
outbox updates, and migrations). The spans set `db.system = postgresql`, `db.operation`, and `db.name`,
and only attach `db.statement` for static/sanitized statements to avoid high-cardinality tags.

## Redis tracing (manual)
The `OpenTelemetry.Instrumentation.StackExchangeRedis` package is not GA, so auto-instrumentation
is intentionally avoided. Redis access is instrumented **manually** at critical read/write paths
(feed hot window and dedup). Spans use `ActivityKind.Client` and the minimal tags
`db.system = redis` and `db.operation`, avoiding high-cardinality data such as full keys or payloads.

## Local URLs
- Grafana: http://localhost:3000
- Prometheus: http://localhost:9090
- Loki: http://localhost:3100
- Tempo: http://localhost:3200

See `docs/agent/LOCAL_DEV.md` for verification steps.
