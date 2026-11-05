CREATE TABLE change_entry (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    bot_id text NOT NULL REFERENCES bot (id),
    kind text NOT NULL,
    public_id text NOT NULL,
    action text NOT NULL,
    revision int,
    changes jsonb NOT NULL,
    batch uuid,
    at timestamptz NOT NULL
);

CREATE INDEX change_entry_of_bot ON change_entry (bot_id, id DESC);

CREATE INDEX change_entry_of_resource ON change_entry (bot_id, kind, public_id, id DESC);

CREATE TABLE history_start (
    started_at timestamptz NOT NULL
);

INSERT INTO history_start (started_at) VALUES (now());
