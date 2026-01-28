# Case 1 — Social Feed — k6 Test Suite (Functional + E2E)

This directory contains a production-shaped k6 test suite for the **Case 1: Social Feed** system.
It validates correctness, eventual consistency, idempotency, hybrid celebrity pull, resilience,
backpressure signals, and observability-oriented metrics.

> **Scope:** Following Feed only. No recommendation, media, or auth.

---

## ✅ What this suite covers

| Scenario | File | Architectural concern |
| --- | --- | --- |
| Post Service | `scenarios/01-post-service.js` | Authoritative writes + outbox-safe persistence |
| Graph Service | `scenarios/02-graph-service.js` | Follow edges, idempotency, cursor stability |
| Fanout Worker | `scenarios/03-fanout-worker.js` | E2E: follow → post → feed update + dedupe |
| Feed Service | `scenarios/04-feed-service.js` | Feed ordering, cursor pagination, Redis availability |
| Hybrid Celebrity | `scenarios/05-hybrid-celebrity.js` | Push vs pull merge + duplicate avoidance |
| Resilience | `scenarios/06-resilience.js` | Partial feed under dependency stress |
| Duplication | `scenarios/07-duplication.js` | Idempotency under retries / reprocessing |
| Scale Read | `scenarios/08-scale-read.js` | Hot key + concurrent feed reads |
| Scale Write | `scenarios/09-scale-write.js` | Burst writes + fanout pressure |

---

## 🧪 How to run

### 1) Start the stack

From the repo root:

```bash
make up
```

Ensure `POST_SERVICE_URL`, `GRAPH_SERVICE_URL`, and `FEED_SERVICE_URL` are reachable. The
`env.local.js` defaults are:

- `http://localhost:8081` (post service)
- `http://localhost:8082` (graph service)
- `http://localhost:8083` (feed service)

### 2) Run all scenarios

```bash
k6 run tests/k6/main.js
```

Or via Makefile:

```bash
make k6
```

### 3) Run a single scenario group

```bash
SCENARIO=feed k6 run tests/k6/main.js
SCENARIO=fanout k6 run tests/k6/main.js
SCENARIO=hybrid k6 run tests/k6/main.js
SCENARIO=resilience k6 run tests/k6/main.js
```

**Group mapping:**
- `feed`: feed correctness + scale-read
- `fanout`: fanout, duplication, scale-write
- `hybrid`: hybrid celebrity merge only
- `resilience`: resilience scenario only
- `all`: everything

### 4) Override environment config

```bash
POST_SERVICE_URL=http://localhost:8081 \
GRAPH_SERVICE_URL=http://localhost:8082 \
FEED_SERVICE_URL=http://localhost:8083 \
LATENCY_P95_MS=800 \
EVENTUAL_CONSISTENCY_TIMEOUT_MS=15000 \
SCENARIO=all \
k6 run tests/k6/main.js
```

---

## ✅ Expected system state before running

- Docker Compose stack is up (`make up`).
- Post Service, Graph Service, Feed Service, Redis, and Kafka are running.
- Database migrations are complete.

**Hybrid celebrity tests require one of these preparations:**

1. **Lower celebrity threshold**: set `CELEBRITY_FOLLOWER_THRESHOLD` to a small value
   in your container environment so a single follow marks an author as celebrity.
2. **Pre-seed followers**: create enough followers so the celebrity author surpasses
   the threshold.

---

## 📈 Metrics & thresholds

Custom k6 metrics include:

- `feed_items_returned`
- `duplicate_items_detected`
- `partial_feed_responses`
- `fanout_latency_ms`
- `eventual_consistency_delay_ms`

Thresholds live in `config/thresholds.js` and enforce:

- Error rate < 1%
- p95 / p99 latency sanity
- **Zero duplicates**

---

## 🔍 How to interpret failures

### ✅ **Architectural issues (must fail loudly)**

- Duplicate posts in feed (idempotency failure)
- Ordering violations (score/time ordering)
- Missing feed entries beyond the consistency timeout
- Feed returns 5xx when dependencies fail instead of partial results

### ⚠️ **Potential test flakiness**

- Propagation delay exceeds timeout (increase `EVENTUAL_CONSISTENCY_TIMEOUT_MS`)
- Redis/Kafka not fully ready when tests start
- Celebrity threshold too high for local data

---

## 📌 Notes on resilience and fault injection

Some resilience checks depend on **manual fault injection**:

- To validate Redis-down behavior, set `EXPECT_REDIS_DOWN=true` and temporarily
  stop Redis in the docker-compose stack.
- To validate partial responses, use `RESILIENCE_EXPECT_PARTIAL=true` and
  temporarily stop the Graph or Post service.

These are **intentional**: the suite does not mock services and does not
assume strong consistency.

---

## ✅ Validation checklist (self-check)

- [x] Every E2E scenario is implemented
- [x] Each scenario maps to an architectural concern
- [x] Eventual consistency is explicit and bounded
- [x] Duplicates are asserted as failures
- [x] README explains why each test exists
