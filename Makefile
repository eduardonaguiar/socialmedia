# Makefile — Case 1: Social Feed (docker-compose based)
# Usage:
#   make up
#   make down
#   make logs
#   make ps
#   make restart
#   make build
#   make pull
#   make reset
#   make health
#   make urls
#   make fmt
#   make test

SHELL := /bin/bash

# Prefer docker compose v2 (plugin)
DC ?= docker compose
PROJECT ?= case1-feed
ENV_FILE ?= .env

# Compose files (extend later if needed)
COMPOSE_FILE ?= docker-compose.yml

# Default services/ports (keep consistent with docs/agent/LOCAL_DEV.md)
POST_SERVICE_URL  ?= http://localhost:8081
GRAPH_SERVICE_URL ?= http://localhost:8082
FEED_SERVICE_URL  ?= http://localhost:8083
FANOUT_WORKER_URL ?= http://localhost:8084
GRAFANA_URL       ?= http://localhost:3000
PROM_URL          ?= http://localhost:9090
LOKI_URL          ?= http://localhost:3100
TEMPO_URL         ?= http://localhost:3200
OTEL_HEALTH_URL   ?= http://localhost:13133/healthz

.PHONY: help up down restart ps logs build pull reset health deps-health obs-health urls \
        fmt test lint clean k6

help: ## Show available commands
	@grep -E '^[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-18s\033[0m %s\n", $$1, $$2}'

up: ## Start the full stack (detached)
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) up -d

down: ## Stop the stack (keep volumes)
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) down

restart: ## Restart the stack
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) restart

ps: ## List containers
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) ps

logs: ## Tail logs (all services)
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) logs -f --tail=200

build: ## Build images (if any local Dockerfiles are used)
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) build

pull: ## Pull remote images
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) pull

reset: ## Destroy everything (including volumes) - DESTRUCTIVE
	@echo "⚠️  This will remove volumes and ALL persisted data for $(PROJECT)."
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) down -v --remove-orphans

health: ## Check service health endpoints (best-effort)
	@set -e; \
	echo "== Post Service ==";  curl -fsS $(POST_SERVICE_URL)/health  && echo && echo OK || echo "NOT OK"; \
	echo "== Graph Service =="; curl -fsS $(GRAPH_SERVICE_URL)/health && echo && echo OK || echo "NOT OK"; \
	echo "== Feed Service ==";  curl -fsS $(FEED_SERVICE_URL)/health  && echo && echo OK || echo "NOT OK"; \
	echo "== Fanout Worker =="; curl -fsS $(FANOUT_WORKER_URL)/health && echo && echo OK || echo "NOT OK"

deps-health: ## Check dependency readiness in containers
	@set -e; \
	echo "== Postgres =="; \
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) exec -T postgres sh -c 'pg_isready -U "$$POSTGRES_USER" -d "$$POSTGRES_DB"' && echo OK; \
	echo "== Redis =="; \
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) exec -T redis redis-cli ping | grep -q PONG && echo OK; \
	echo "== Redpanda =="; \
	$(DC) --project-name $(PROJECT) --env-file $(ENV_FILE) -f $(COMPOSE_FILE) exec -T redpanda curl -fsS http://localhost:9644/v1/status/ready && echo && echo OK

obs-health: ## Check observability endpoints (best-effort)
	@set -e; \
	echo "== OTEL Collector =="; curl -fsS $(OTEL_HEALTH_URL) && echo && echo OK || echo "NOT OK"; \
	echo "== Prometheus =="; curl -fsS $(PROM_URL)/-/ready && echo && echo OK || echo "NOT OK"; \
	echo "== Grafana =="; curl -fsS $(GRAFANA_URL)/api/health && echo && echo OK || echo "NOT OK"; \
	echo "== Loki =="; curl -fsS $(LOKI_URL)/ready && echo && echo OK || echo "NOT OK"; \
	echo "== Tempo =="; curl -fsS $(TEMPO_URL)/ready && echo && echo OK || echo "NOT OK"

urls: ## Print useful local URLs
	@echo "Post Service:        $(POST_SERVICE_URL)"
	@echo "Social Graph Service: $(GRAPH_SERVICE_URL)"
	@echo "Feed Service:        $(FEED_SERVICE_URL)"
	@echo "Fanout Worker:       $(FANOUT_WORKER_URL)"
	@echo "Grafana:             $(GRAFANA_URL)"
	@echo "Prometheus:          $(PROM_URL)"
	@echo "Loki:                $(LOKI_URL)"
	@echo "Tempo:               $(TEMPO_URL)"
	@echo "OTEL Collector:      $(OTEL_HEALTH_URL)"

# Optional: formatting/testing placeholders (wire them once code exists)
fmt: ## Format code (placeholder - implement per language/tooling)
	@echo "No formatter configured yet. Add dotnet format / prettier / etc."

lint: ## Lint code (placeholder - implement per language/tooling)
	@echo "No linter configured yet. Add dotnet analyzers / eslint / etc."

test: ## Run tests (placeholder - implement once services exist)
	@dotnet test services/post-service/Tests/PostService.Tests.csproj
	@dotnet test services/graph-service/Tests/GraphService.Tests.csproj
	@dotnet test services/feed-service/Tests/FeedService.Tests.csproj
	@dotnet test workers/fanout-worker/Tests/FanoutWorker.Tests.csproj

k6: ## Run k6 E2E/functional scenarios (requires k6 CLI installed)
	@POST_SERVICE_URL=$(POST_SERVICE_URL) \
	GRAPH_SERVICE_URL=$(GRAPH_SERVICE_URL) \
	FEED_SERVICE_URL=$(FEED_SERVICE_URL) \
	k6 run tests/k6/main.js

clean: ## Local cleanup (non-destructive)
	@echo "Nothing to clean yet."
