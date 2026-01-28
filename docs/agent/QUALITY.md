# Quality Gates

## Required
- Unit tests for core domain logic (minimal but meaningful)
- Integration test for event → fanout → feed visible (happy path)
- Contract checks for event schema versioning

## Validation checklist (per PR)
- `docker compose up` works on clean machine
- Services expose `/health`
- Feed read works end-to-end
- Duplicate event does not duplicate feed item
- Basic metrics visible in Prometheus/Grafana
- Backpressure metrics emit when fanout is throttled (`fanout_backpressure_applied_total`)
- Circuit breaker opens and feed returns partial data when Graph/Post fail
- Retry exhaustion visible via `retry_exhausted_total`
- Kafka lag visible (`fanout_kafka_lag`) and decreases after recovery
- k6 suite covers scale/read/write and resilience checks (see `docs/study/10-SCALING.md`)
