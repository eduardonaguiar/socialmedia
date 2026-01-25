# Agent Instructions (Root)

This repository is a **documentation-first learning lab** for **System Design Case #1 — Social Feed**.

## Primary directives
- Keep changes small, readable, and aligned with the case scope (Following Feed only).
- Do **not** introduce features outside the defined scope (no recommendation, media, or auth).
- Every technical decision must be reflected in `docs/study/` (PT-BR) and referenced from `docs/agent/` (EN).
- Assume **at-least-once** delivery and enforce **idempotency** in derived pipelines.
- Derived data is rebuildable; authoritative data remains correct.

## Default tech choices (unless an ADR changes them)
- .NET 8 (Minimal APIs)
- PostgreSQL (authoritative)
- Redis ZSET (feed hot window)
- Kafka API via Redpanda (event bus)
- OpenTelemetry + Prometheus + Grafana + Loki + Tempo

## If uncertain
If a decision cannot be derived from existing context, stop and create an ADR stub using
`docs/agent/ADR_TEMPLATE.md`.

> Note: `AGENT.md` contains the canonical repository context and expanded guidance.
