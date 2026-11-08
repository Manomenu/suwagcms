-- Why: format 4 has no knowledge on whenStalled, and a stored procedure must not keep words no screen shows and no version carries. The body is rebuilt key by key, never through jsonb, so the written order of an inform's points stays; the history keeps what was written then, as an entry is never changed.
UPDATE resource
SET body = (
    SELECT json_object_agg(
        procedure.key,
        CASE
            WHEN procedure.key = 'whenStalled' THEN (
                SELECT coalesce(json_object_agg(stalled.key, stalled.value ORDER BY stalled.position), '{}'::json)
                FROM json_each(procedure.value) WITH ORDINALITY AS stalled (key, value, position)
                WHERE stalled.key <> 'knowledge'
            )
            ELSE procedure.value
        END
        ORDER BY procedure.position
    )
    FROM json_each(body) WITH ORDINALITY AS procedure (key, value, position)
)
WHERE kind = 'procedure'
    AND json_typeof(body -> 'whenStalled') = 'object'
    AND body -> 'whenStalled' -> 'knowledge' IS NOT NULL;
