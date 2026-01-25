# Event Contracts

## Event: PostCreated v1
Topic: `post.created.v1`

### Semantics
- Produced by Post Service after the post is committed (outbox).
- Consumers MUST assume at-least-once delivery (duplicates possible).
- Consumers MUST be idempotent.
- Message key: `author_id` (ordering per author).

### Payload (logical)
- event_id (uuid)
- occurred_at (utc)
- post_id
- author_id (string)
- created_at (utc)
- schema_version = 1

See also: `docs/study/05-DATA_MODEL.md` and `docs/study/08-CONSISTENCY.md`.

### Processing guarantees
- Fanout Worker:
  - Deduplicate by `event_id` (TTL store) and/or `post_id`+`author_id`
  - ZSET insertion must be idempotent

## Event: FollowChanged v1 (optional)
Topic: `graph.follow.changed.v1`
Used only if we decide to propagate follow changes to caches/materializations beyond DB.
