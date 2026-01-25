CREATE TABLE IF NOT EXISTS posts (
    post_id uuid PRIMARY KEY,
    author_id text NOT NULL,
    content text NOT NULL,
    created_at_utc timestamptz NOT NULL,
    CONSTRAINT posts_content_length CHECK (char_length(content) <= 280)
);

CREATE TABLE IF NOT EXISTS outbox_messages (
    outbox_id uuid PRIMARY KEY,
    event_type text NOT NULL,
    schema_version int NOT NULL,
    payload_json jsonb NOT NULL,
    occurred_at_utc timestamptz NOT NULL,
    published_at_utc timestamptz NULL,
    publish_attempts int NOT NULL DEFAULT 0,
    last_error text NULL,
    lock_id uuid NULL,
    locked_at_utc timestamptz NULL
);

CREATE INDEX IF NOT EXISTS idx_outbox_unpublished
    ON outbox_messages (published_at_utc, locked_at_utc, occurred_at_utc);

CREATE INDEX IF NOT EXISTS idx_posts_author_created
    ON posts (author_id, created_at_utc DESC);
