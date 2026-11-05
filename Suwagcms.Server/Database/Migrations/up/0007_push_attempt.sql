CREATE TABLE push_attempt (
    id uuid PRIMARY KEY,
    bot_id text NOT NULL REFERENCES bot (id),
    version_number int NOT NULL,
    status text NOT NULL,
    error text,
    at timestamptz NOT NULL
);

CREATE INDEX push_attempt_bot_at ON push_attempt (bot_id, at DESC);
