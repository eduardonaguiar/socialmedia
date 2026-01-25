CREATE TABLE IF NOT EXISTS follow_edges (
    follower_id text NOT NULL,
    followed_id text NOT NULL,
    followed_at_utc timestamptz NOT NULL,
    CONSTRAINT pk_follow_edges PRIMARY KEY (follower_id, followed_id),
    CONSTRAINT follow_edges_no_self CHECK (follower_id <> followed_id)
);

CREATE INDEX IF NOT EXISTS idx_follow_edges_followed
    ON follow_edges (followed_id, follower_id);

CREATE TABLE IF NOT EXISTS following_by_user (
    user_id text NOT NULL,
    followed_id text NOT NULL,
    followed_at_utc timestamptz NOT NULL,
    CONSTRAINT pk_following_by_user PRIMARY KEY (user_id, followed_id)
);

CREATE INDEX IF NOT EXISTS idx_following_by_user_order
    ON following_by_user (user_id, followed_at_utc DESC, followed_id DESC);

CREATE TABLE IF NOT EXISTS followers_by_user (
    user_id text NOT NULL,
    follower_id text NOT NULL,
    followed_at_utc timestamptz NOT NULL,
    CONSTRAINT pk_followers_by_user PRIMARY KEY (user_id, follower_id)
);

CREATE INDEX IF NOT EXISTS idx_followers_by_user_order
    ON followers_by_user (user_id, followed_at_utc DESC, follower_id DESC);
