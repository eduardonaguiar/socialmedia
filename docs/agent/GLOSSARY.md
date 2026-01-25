# Glossary (Case 1)

- Authoritative: source of truth, owns invariants.
- Derived: computed/materialized view, rebuildable, may be stale.
- Skew: uneven distribution (power-law); few heavy hitters dominate.
- Hot key: partition/key with disproportionate load.
- Fan-out: propagate 1 event to N recipients.
- Push: fan-out on write (materialize feed on post).
- Pull: fan-out on read (compose feed at read time).
- Hot window: last N items kept in fast store/cache.
- Backpressure: flow control when downstream is overloaded.
- Idempotency: repeated processing yields same end state.
- Tie-breaker: deterministic ordering secondary key (timestamp + id).
- Threshold: cutoff value (e.g., celebrity threshold).
