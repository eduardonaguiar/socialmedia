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

## Health checks
- Post Service:    http://localhost:8081/health
- Graph Service:   http://localhost:8082/health
- Feed Service:    http://localhost:8083/health
- Grafana:         http://localhost:3000
- Prometheus:      http://localhost:9090

## Default ports (reserved)
- 8081 Post Service
- 8082 Social Graph Service
- 8083 Feed Service
- 8084 Fanout Worker (optional metrics endpoint)
- 5432 Postgres
- 6379 Redis
- 9092 Redpanda (Kafka API)
- 3000 Grafana
- 9090 Prometheus
- 4317 OTLP gRPC (Collector)
