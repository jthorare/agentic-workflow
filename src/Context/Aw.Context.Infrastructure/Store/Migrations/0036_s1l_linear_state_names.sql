-- The strip said "moved from  to" when a team's state list was missing or lacked a state (#2536). A state change now keeps the names Linear sent with the issue: the issue stores its current state's name,
-- and a change logs the old and new name beside the ids. Rows written before this migration have no names (null); the strip falls back to the team's state list for them.
ALTER TABLE linear_issue ADD COLUMN state_name TEXT NULL;
ALTER TABLE linear_issue_change ADD COLUMN old_name TEXT NULL;
ALTER TABLE linear_issue_change ADD COLUMN new_name TEXT NULL;
