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
