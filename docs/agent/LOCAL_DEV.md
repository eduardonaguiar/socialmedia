# Local Development

## Prerequisites
- Docker + Docker Compose v2
- Make (optional but recommended)

## Commands
- `make up`        -> start full stack
- `make down`      -> stop stack
- `make logs`      -> tail logs
- `make ps`        -> list containers
- `make reset`     -> wipe volumes (destructive)
- `make deps-health` -> verify dependency readiness
- `make obs-health`  -> verify observability readiness

## Health checks
- Post Service:    http://localhost:8081/health
- Graph Service:   http://localhost:8082/health
- Feed Service:    http://localhost:8083/health
- Grafana:         http://localhost:3000
- Prometheus:      http://localhost:9090
- Loki:            http://localhost:3100/ready
- Tempo:           http://localhost:3200/ready
- OTEL Collector:  http://localhost:13133/healthz

## Dependencies (Docker Compose base)
The base stack starts data dependencies (Postgres, Redis, Redpanda) plus the observability stack
on the `case1-net` network. This matches the local architecture described in PT-BR in
`docs/study/04-ARCHITECTURE.md`.

### Start dependencies
```bash
make up
```

## Post Service (authoritative)
The Post Service listens on `http://localhost:8081` and expects the `X-User-Id` header.
Content is limited to 280 characters. See `docs/study/05-DATA_MODEL.md`.

### Create a post
```bash
curl -fsS -X POST http://localhost:8081/posts \
  -H "Content-Type: application/json" \
  -H "X-User-Id: user-123" \
  -d '{"content":"hello from post-service"}'
```

Swagger (dev only):
```bash
open http://localhost:8081/swagger
```

### Get a post by id
```bash
curl -fsS http://localhost:8081/posts/<post_id>
```

### Verify dependencies are ready
```bash
make deps-health
```

### Verify observability is ready
```bash
make obs-health
```

## Social Graph Service (authoritative)
The Graph Service listens on `http://localhost:8082` and expects the `X-User-Id` header for
follow/unfollow operations. It supports cursor pagination for following/followers.

### Follow a user
```bash
curl -fsS -X POST http://localhost:8082/follow/user-456 \
  -H "X-User-Id: user-123"
```

### Unfollow a user
```bash
curl -fsS -X DELETE http://localhost:8082/follow/user-456 \
  -H "X-User-Id: user-123"
```

### List following (first page)
```bash
curl -fsS "http://localhost:8082/users/user-123/following?limit=2"
```

### List following (next page)
```bash
curl -fsS "http://localhost:8082/users/user-123/following?cursor=<cursor>&limit=2"
```

### List followers
```bash
curl -fsS "http://localhost:8082/users/user-456/followers?limit=2"
```

Swagger (dev only):
```bash
open http://localhost:8082/swagger
```

Manual checks:
```bash
curl -fsS http://localhost:9090/-/ready
curl -fsS http://localhost:3000/api/health
curl -fsS http://localhost:3100/ready
curl -fsS http://localhost:3200/ready
curl -fsS http://localhost:13133/healthz
```

Prometheus targets page:
```bash
open http://localhost:9090/targets
```

Grafana login:
```bash
# Use .env values for GRAFANA_ADMIN_USER / GRAFANA_ADMIN_PASSWORD
open http://localhost:3000
```

Equivalent direct checks:
```bash
docker compose exec -T postgres pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"
docker compose exec -T redis redis-cli ping
docker compose exec -T redpanda curl -fsS http://localhost:9644/v1/status/ready
```

### Troubleshooting
```bash
docker compose ps
docker compose logs -f --tail=200
docker compose logs -f --tail=200 otel-collector prometheus grafana loki tempo
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"
```

## Default ports (reserved)
- 8081 Post Service
- 8082 Social Graph Service
- 8083 Feed Service
- 8084 Fanout Worker (optional metrics endpoint)
- 5432 Postgres
- 6379 Redis
- 9092 Redpanda (Kafka API)
- 9644 Redpanda Admin/Health
- 3000 Grafana
- 9090 Prometheus
- 4317 OTLP gRPC (Collector)
- 4318 OTLP HTTP (Collector)
- 8888 OTEL Collector internal metrics
- 8889 OTEL Collector Prometheus exporter
- 3100 Loki
- 3200 Tempo
