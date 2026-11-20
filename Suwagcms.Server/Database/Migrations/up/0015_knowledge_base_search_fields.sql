UPDATE resource
SET body = (body::jsonb || '{"embedding": "default", "results": 5}'::jsonb)::json
WHERE kind = 'knowledgeBase';
