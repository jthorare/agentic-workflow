-- Memory fact refresher (#2505): the actor of the call that last refreshed the fact, taken from the caller and never the request.
-- NULL for a fact never refreshed, and for one refreshed before this column existed.
ALTER TABLE memory_fact ADD COLUMN refreshed_by TEXT;
