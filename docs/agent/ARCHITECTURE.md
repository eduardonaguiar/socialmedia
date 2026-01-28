# Architecture Overview

## Services
### 1) Post Service (authoritative)
- Stores post content and metadata
- Emits `PostCreated` event (transactional outbox + background publisher)
- API: create/get + author timeline (cursor)
- Outbox: `outbox_messages` with locking to avoid concurrent publish
- Resilience: DB/Kafka retries with jitter + circuit breakers

Reference (PT-BR): `docs/study/05-DATA_MODEL.md`, `docs/study/08-CONSISTENCY.md`.

### 2) Social Graph Service (authoritative)
- Stores follow edges
- Maintains two materializations:
  - `following_by_user` (out edges)
  - `followers_by_user` (in edges)
- Maintains `user_stats` with `followers_count`
- API: follow/unfollow + list following/followers + stats + celebrity-following
- Deterministic ordering: `followed_at_utc DESC, <id> DESC` for stable cursors
- Resilience: DB retries with jitter + circuit breaker

Reference (PT-BR): `docs/study/04-ARCHITECTURE.md`, `docs/study/05-DATA_MODEL.md`, `docs/study/10-SCALING.md`.

### 3) Feed Service (derived read model)
- Serves feed via Redis ZSET hot window (`case1:feed:{user_id}`)
- Cursor pagination using `(score, tie-breaker)` with deterministic ordering
- Hybrid read path: merge push ZSET with pull timelines for celebrity authors
- Returns post references only (no hydration)
- Failure mode: Redis unavailable -> 503 to keep cache state explicit
- Resilience: retries with jitter, circuit breakers for Graph/Post, partial feed fallbacks

Reference (PT-BR): `docs/study/06-FEED_STRATEGY.md`, `docs/study/07-CACHING.md`, `docs/study/08-CONSISTENCY.md`, `docs/study/09-FAILURES.md`, `docs/study/10-SCALING.md`.

### 4) Fanout Worker (derived builder)
- Consumes `PostCreated` (Kafka/Redpanda)
- Classifies author via Graph Service stats
- Resolves followers via Graph Service (cursor pagination) only for normal authors
- Updates Redis ZSET hot window (`case1:feed:{user_id}`) idempotently
- Deduplicates by `event_id` with TTL (Redis NX) and relies on ZSET uniqueness
- Trims feed to hot window size after writes
- Skips fan-out for celebrity authors (hybrid)
- Backpressure: bounded concurrency, rate limiting, and lag-aware consumption

Reference (PT-BR): `docs/study/06-FEED_STRATEGY.md`, `docs/study/08-CONSISTENCY.md`, `docs/study/09-FAILURES.md`, `docs/study/10-SCALING.md`.

## Data classification
- Authoritative:
  - Post Service DB tables
  - Social Graph DB tables
- Derived:
  - Redis feed ZSETs
  - Counters / cached lists
  - Anything computed from events

## Core patterns
- Transactional outbox (PostCreated)
- At-least-once processing + dedup/idempotency
- Skew-aware: celebrity threshold and selective fan-out
- Graceful degradation:
  - Redis down → explicit 503 today (fallback path later)
  - Graph/Post down → partial feed (push-only)
  - Circuit breakers + retries guard p99 latency
