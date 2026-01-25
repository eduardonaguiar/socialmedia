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

## Data modeling principles
- Authoritative tables normalized enough for clarity
- Derived views optimized for reads (Redis ZSET hot window)
- Never query derived data to make authoritative decisions

## Docker Compose conventions
- Use a single bridge network: `case1-net`
- Dependency service names are fixed: `postgres`, `redis`, `redpanda`
- Named volumes: `pg_data`, `redpanda_data`, `redis_data`
- Local compose design rationale (PT-BR): see `docs/study/04-ARCHITECTURE.md`
