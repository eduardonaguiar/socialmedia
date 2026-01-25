# Architecture Overview

## Services
### 1) Post Service (authoritative)
- Stores post content and metadata
- Emits `PostCreated` event (transactional outbox + background publisher)
- API: create/get
- Outbox: `outbox_messages` with locking to avoid concurrent publish

Reference (PT-BR): `docs/study/05-DATA_MODEL.md`, `docs/study/08-CONSISTENCY.md`.

### 2) Social Graph Service (authoritative)
- Stores follow edges
- Maintains two materializations:
  - `following_by_user` (out edges)
  - `followers_by_user` (in edges)
- API: follow/unfollow + list following/followers with cursor pagination
- Deterministic ordering: `followed_at_utc DESC, <id> DESC` for stable cursors

Reference (PT-BR): `docs/study/04-ARCHITECTURE.md`, `docs/study/05-DATA_MODEL.md`.

### 3) Feed Service (derived read model)
- Serves feed via Redis ZSET hot window (`case1:feed:{user_id}`)
- Cursor pagination using `(score, tie-breaker)` with deterministic ordering
- Read path only (no hydration); returns post references
- Failure mode: Redis unavailable -> 503 to keep cache state explicit

Reference (PT-BR): `docs/study/06-FEED_STRATEGY.md`, `docs/study/07-CACHING.md`, `docs/study/08-CONSISTENCY.md`, `docs/study/09-FAILURES.md`.

### 4) Fanout Worker (derived builder)
- Consumes `PostCreated`
- Resolves followers for author
- Updates `feed:{userId}` ZSET idempotently
- Applies selective fan-out + thresholds (celebrity)

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
  - Kafka lag → partial pull merge
