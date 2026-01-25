# Conventions

## API style
- REST + JSON
- Cursor-based pagination for lists and feed
- Error format: `{ "error": { "code": "...", "message": "...", "details": {...} } }`

## Identity in dev
- Use header `X-User-Id` for user context
- Do not implement full auth in this lab

## Folder layout (planned)
- `services/post-service`
- `services/graph-service`
- `services/feed-service`
- `workers/fanout-worker`
- `contracts/events` (event schemas)
- `docs/agent` and `docs/study`

## Code standards
- .NET 8, nullable enabled
- English comments, English commit messages
- Prefer small modules + clear naming
- Observability: OpenTelemetry tracing + metrics (minimal)

## OpenTelemetry conventions
### Resource attributes (required)
- `service.name`: use scoped names like `post-service`, `graph-service`, `feed-service`, `fanout-worker`
- `deployment.environment=dev`

### Trace propagation
- W3C Trace Context (`traceparent` header)

### Metrics (minimum)
- Request duration histogram (e.g., `http.server.duration`)
- Request count and error count (e.g., `http.server.request_count`, `http.server.error_count`)

### Log correlation
- Include `trace_id` in structured logs when possible

PT-BR reference: `docs/study/03-NFR.md` (Observabilidade — convenções mínimas).

## Data modeling principles
- Authoritative tables normalized enough for clarity
- Derived views optimized for reads (Redis ZSET hot window)
- Never query derived data to make authoritative decisions

## Docker Compose conventions
- Use a single bridge network: `case1-net`
- Dependency service names are fixed: `postgres`, `redis`, `redpanda`
- Named volumes: `pg_data`, `redpanda_data`, `redis_data`
- Local compose design rationale (PT-BR): see `docs/study/04-ARCHITECTURE.md`
