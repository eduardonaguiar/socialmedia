# Event Contracts

## Event: PostCreated v1
Topic: `post.created.v1`

### Semantics
- Produced by Post Service after the post is committed (outbox).
- Consumers MUST assume at-least-once delivery (duplicates possible).
- Consumers MUST be idempotent.

### Payload (logical)
- event_id (uuid)
- occurred_at (utc)
- post_id
- author_id
- created_at (utc, ms precision)
- visibility (public | followers_only) [optional for lab; default public]
- schema_version = 1

### Processing guarantees
- Fanout Worker:
  - Deduplicate by `event_id` (TTL store) and/or `post_id`+`author_id`
  - ZSET insertion must be idempotent

## Event: FollowChanged v1 (optional)
Topic: `graph.follow.changed.v1`
Used only if we decide to propagate follow changes to caches/materializations beyond DB.
