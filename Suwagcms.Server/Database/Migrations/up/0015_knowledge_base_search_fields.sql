-- Why: embedding and results became required in format 3, and a stored base without them could not be read; default and 5 are what every base had in effect.
UPDATE resource
SET body = (body::jsonb || '{"embedding": "default", "results": 5}'::jsonb)::json
WHERE kind = 'knowledgeBase';
