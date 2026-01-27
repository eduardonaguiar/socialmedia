CREATE INDEX IF NOT EXISTS idx_posts_author_created_post
    ON posts (author_id, created_at_utc DESC, post_id DESC);
