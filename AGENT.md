# Agent Instructions (Repository Context)

You are operating inside a learning lab repository: **System Design Case #1 — Social Feed**.

This repo is **documentation-first** and **printable-study oriented**.
Your job is to implement the case **incrementally** while producing clear docs that explain trade-offs and operational behavior.

## Non-negotiables
- Do not add features outside the case scope.
- Keep changes small and reviewable.
- Every technical decision must be reflected in `docs/study/` (PT-BR) and referenced from `docs/agent/` (EN).
- Prefer correctness + clarity over cleverness.
- Always implement idempotency and failure-safe behavior in pipelines (at-least-once assumption).

## Default Tech Choices (unless overridden by an ADR)
- Services: .NET 8 (Minimal APIs)
- Authoritative stores: PostgreSQL (learning-friendly)
- Cache / Feed hot window: Redis (ZSET)
- Event bus: Kafka API via Redpanda (docker-compose friendly)
- Observability: OpenTelemetry Collector + Prometheus + Grafana + Loki + Tempo

If any of the above must change, create an ADR first.

## Output expectations (per task)
- Code + configs + docker-compose changes (if required)
- Docs updated (both agent + study)
- A runnable local path (`make up`, `make down`, `make test`)
- A validation checklist with concrete commands to run

## Golden rules
- Cache is never a source of truth.
- Derived data can be rebuilt; authoritative data must remain correct.
- Assume duplicates and retries will happen.
- Design for skew and celebrity behavior (power-law).
- Provide graceful degradation paths for Redis and broker failures.
