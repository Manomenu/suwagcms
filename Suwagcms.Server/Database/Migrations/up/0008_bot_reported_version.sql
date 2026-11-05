ALTER TABLE bot
    ADD COLUMN reported_version int,
    ADD COLUMN reported_at timestamptz;
