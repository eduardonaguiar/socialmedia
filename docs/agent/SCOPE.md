# Scope (FR/NFR) — Social Feed (Following)

## Functional Requirements (FR)
FR-01 Create post (text + timestamp + author_id)
FR-02 Get post by id
FR-03 Follow user / unfollow user
FR-04 Get following list (paginated)
FR-05 Get followers list (paginated)
FR-06 Get feed (paginated, cursor-based)
FR-07 Fan-out pipeline: PostCreated → feed updates (normal users)
FR-08 Hybrid feed: celebrity posts merged at read time

## Non-Functional Requirements (NFR)
NFR-01 Low latency feed reads (p95 target < 400ms local dev baseline)
NFR-02 High availability via graceful degradation
NFR-03 At-least-once events assumed (duplicates possible)
NFR-04 Eventual consistency acceptable for propagation (seconds)
NFR-05 Idempotent processing required
NFR-06 Operability: metrics/traces/logs available in local stack

## Explicit Non-goals
- Recommendation ranking, personalization ML
- Full auth, user accounts lifecycle
- Strong global consistency
- Deleting/Editing posts with strict propagation guarantees
