-- Why: jsonb sorts the keys of an object, and an inform's points are a map whose written order is part of what the author wrote.
ALTER TABLE resource ALTER COLUMN body TYPE json USING body::json;
