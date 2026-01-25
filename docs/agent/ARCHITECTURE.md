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
- API: follow/unfollow + list following/followers

### 3) Feed Service (derived read model)
- Serves feed via Redis ZSET (hot window)
- Cursor pagination using (score, tie-breaker)
- Hybrid merge: derived feed + celebrity pull

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
  - Redis down → fallback path
  - Kafka lag → partial pull merge
