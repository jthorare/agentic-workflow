-- Concept pins (Q9): a map version records the hash of each concept pin's rule line (MapRuleset.PinHashes, a JSON object by pin id), so the pins read can tell which pin the map
-- was built without or under another version of. NULL = built before this, which reads as "unknown", never as "created".
ALTER TABLE map_version ADD COLUMN pin_hashes TEXT;
