-- An item-filtered ledger scan (LedgerSql.ScanAfter with an item, 'sindri ledger --item', the agent-session reads) had no index
-- on the item column, so SQLite walked the whole rowid range (34 ms at 200k events). This index serves 'item = $item ORDER BY seq'.
CREATE INDEX events_item_seq ON events (item, seq) WHERE item IS NOT NULL;
