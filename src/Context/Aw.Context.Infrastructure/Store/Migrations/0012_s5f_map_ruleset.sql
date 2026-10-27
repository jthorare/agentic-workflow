-- Issue 1041: a map version records the hash of the map rules it was built under (MapRuleset), so a tick or the doctor can tell a map
-- built under older rules from a current one. NULL = built before the rules were recorded, which reads as "older rules" and rebuilds once.
ALTER TABLE map_version ADD COLUMN ruleset TEXT;
