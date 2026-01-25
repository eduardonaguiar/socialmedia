# Project Context — Case 1: Social Feed

## Purpose
Build a runnable, local (docker-compose) implementation of a **Following Feed** system
(Twitter/Instagram/LinkedIn style), including:
- Authoritative services (posts, social graph)
- Derived feed view (Redis ZSET hot window)
- Event-driven fan-out pipeline
- Hybrid strategy for celebrities (push for normal users + pull for heavy hitters)
- Observability and resilience patterns
- Printable study documentation of the full interview-style reasoning

## What this is NOT
- No ML ranking / recommendation feed.
- No media upload/transcoding/CDN.
- No full auth system (assume a simple user-id header in dev).
- No ads, moderation, search, hashtags, trends.

## Learning goals
- Reason about reads≫writes systems.
- Understand skew, hot keys, fan-out cost explosion.
- Implement materialized views, cache strategy, cursor pagination.
- Practice idempotency, retries, dedup, backpressure, graceful degradation.
- Connect architecture decisions to measurable outcomes (p95/p99, lag, throughput).

## Target deliverables
- `docker-compose.yml` running full stack
- Minimal services + worker(s)
- Docs:
  - Agent context docs (`docs/agent/`)
  - Printable study docs in PT-BR (`docs/study/`)
- Load test scripts + scenarios (celebrity post, broker lag, redis down)
