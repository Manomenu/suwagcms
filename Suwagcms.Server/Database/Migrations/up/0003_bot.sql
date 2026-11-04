CREATE TABLE bot (
    id text PRIMARY KEY,
    name text NOT NULL,
    address text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
