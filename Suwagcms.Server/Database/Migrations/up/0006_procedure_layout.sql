CREATE TABLE procedure_layout (
    resource_id uuid PRIMARY KEY REFERENCES resource (id) ON DELETE CASCADE,
    positions jsonb NOT NULL
);
