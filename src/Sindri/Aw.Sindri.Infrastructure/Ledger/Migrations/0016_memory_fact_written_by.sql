-- Memory fact writer (#2460): the actor of the call that wrote the fact, taken from the caller and never the request.
-- NULL for a fact written before this column existed.
ALTER TABLE memory_fact ADD COLUMN written_by TEXT;
