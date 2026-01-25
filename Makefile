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
GRAFANA_URL       ?= http://localhost:3000
PROM_URL          ?= http://localhost:9090

.PHONY: help up down restart ps logs build pull reset health urls \
        fmt test lint clean

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
	echo "== Feed Service ==";  curl -fsS $(FEED_SERVICE_URL)/health  && echo && echo OK || echo "NOT OK"

urls: ## Print useful local URLs
	@echo "Post Service:        $(POST_SERVICE_URL)"
	@echo "Social Graph Service: $(GRAPH_SERVICE_URL)"
	@echo "Feed Service:        $(FEED_SERVICE_URL)"
	@echo "Grafana:             $(GRAFANA_URL)"
	@echo "Prometheus:          $(PROM_URL)"

# Optional: formatting/testing placeholders (wire them once code exists)
fmt: ## Format code (placeholder - implement per language/tooling)
	@echo "No formatter configured yet. Add dotnet format / prettier / etc."

lint: ## Lint code (placeholder - implement per language/tooling)
	@echo "No linter configured yet. Add dotnet analyzers / eslint / etc."

test: ## Run tests (placeholder - implement once services exist)
	@echo "No tests configured yet. Add 'dotnet test' per solution when created."

clean: ## Local cleanup (non-destructive)
	@echo "Nothing to clean yet."
