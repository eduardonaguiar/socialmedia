CREATE TABLE IF NOT EXISTS user_stats (
    user_id text PRIMARY KEY,
    followers_count bigint NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_user_stats_followers_count
    ON user_stats (followers_count DESC);
