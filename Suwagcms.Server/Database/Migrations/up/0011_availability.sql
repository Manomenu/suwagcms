ALTER TABLE bot
    ADD COLUMN availability_number int NOT NULL DEFAULT 0;

CREATE TABLE availability (
    resource_id uuid PRIMARY KEY REFERENCES resource (id) ON DELETE CASCADE
);

CREATE TABLE resource_former_id (
    resource_id uuid NOT NULL REFERENCES resource (id) ON DELETE CASCADE,
    public_id text NOT NULL,
    up_to_version int NOT NULL,
    PRIMARY KEY (resource_id, public_id)
);
