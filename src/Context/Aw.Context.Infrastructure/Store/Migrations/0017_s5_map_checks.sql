-- Map structure spec §4.3 (issue 1365): a map version records the structure reason its build was degraded for (numbers and limits only, notes joined by '; ',
-- for example 'structure 41% isolated, limit 30%'), so 'sindri context map status' and the app can show it after the build. NULL = built clean, or built before
-- the checks ran. A promote copies it with the rest of the row.
ALTER TABLE map_version ADD COLUMN checks TEXT;
