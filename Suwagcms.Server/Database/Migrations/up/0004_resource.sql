CREATE TABLE resource (
    id uuid PRIMARY KEY,
    bot_id text NOT NULL REFERENCES bot (id),
    kind text NOT NULL,
    public_id text NOT NULL,
    revision int NOT NULL,
    body jsonb NOT NULL,
    updated_at timestamptz NOT NULL,
    UNIQUE (bot_id, kind, public_id)
);
