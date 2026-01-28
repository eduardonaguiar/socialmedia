# Decisions (ADR-style Summary)

This log summarizes decisions for Case 1 (Following Feed). Each decision is backed
by the PT-BR study docs.

## D-01 — Separate authoritative vs derived data
**Decision:** Post/Graph remain authoritative; Redis and materializations are derived.
**Why:** Derived data is rebuildable and cheaper to serve; authority remains correct.
**Refs (PT-BR):** `docs/study/05-DATA_MODEL.md`, `docs/study/07-CACHING.md`.

## D-02 — At-least-once with idempotent fanout
**Decision:** Accept duplicates and enforce idempotency in the fanout pipeline.
**Why:** Kafka delivery is at-least-once; correctness must survive retries.
**Refs (PT-BR):** `docs/study/08-CONSISTENCY.md`.

## D-03 — Hybrid feed (push for normal, pull for celebrity)
**Decision:** Skip fanout for celebrity authors; merge at read time.
**Why:** Controls write amplification caused by skew.
**Refs (PT-BR):** `docs/study/06-FEED_STRATEGY.md`, `docs/study/10-SCALING.md`.

## D-04 — Redis ZSET hot window (bounded)
**Decision:** Keep a fixed-size hot window in Redis per user.
**Why:** Fast reads with bounded memory; rebuildable derived store.
**Refs (PT-BR):** `docs/study/07-CACHING.md`.

## D-05 — Backpressure and circuit breakers for latency control
**Decision:** Use bounded concurrency, backoff, retries, and breakers.
**Why:** Protect Graph/Post/Redis and stabilize p95/p99.
**Refs (PT-BR):** `docs/study/09-FAILURES.md`.

## D-06 — Scale evolution is metric-driven (1× → 10× → 100×)
**Decision:** Changes are triggered by lag, memory pressure, and latency, not by
feature scope.
**Why:** Prevents premature optimization and ties evolution to observable signals.
**Refs (PT-BR):** `docs/study/11-SCALE_EVOLUTION.md`.

## D-07 — Cost control is “do less work”
**Decision:** Prefer higher thresholds, smaller windows, and cache tuning over
expensive infra changes.
**Why:** Major cost drivers are write amplification and Redis memory.
**Refs (PT-BR):** `docs/study/12-COST_MODEL.md`.

## D-08 — Interview-grade narrative and cheat sheet
**Decision:** Provide a concise review and mental model for senior/staff interviews.
**Why:** The case is a study lab; the final output must be explorable quickly.
**Refs (PT-BR):** `docs/study/13-INTERVIEW_REVIEW.md`.
