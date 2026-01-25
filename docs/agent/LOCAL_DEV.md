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

## Health checks
- Post Service:    http://localhost:8081/health
- Graph Service:   http://localhost:8082/health
- Feed Service:    http://localhost:8083/health
- Grafana:         http://localhost:3000
- Prometheus:      http://localhost:9090

## Dependencies (Docker Compose base)
The base stack starts only data dependencies (Postgres, Redis, Redpanda) on the `case1-net` network.
This matches the local architecture described in PT-BR in `docs/study/04-ARCHITECTURE.md`.

### Start dependencies
```bash
make up
```

### Verify dependencies are ready
```bash
make deps-health
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
