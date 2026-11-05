ALTER TABLE bot
    ADD COLUMN declaration text,
    ADD COLUMN declared_at timestamptz;
