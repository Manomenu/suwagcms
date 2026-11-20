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
