CREATE TABLE bot_version (
    bot_id text NOT NULL REFERENCES bot (id),
    number int NOT NULL,
    format_version int NOT NULL,
    document text NOT NULL,
    sha256 text NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (bot_id, number)
);
