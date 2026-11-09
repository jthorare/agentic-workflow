# Data view: the schema drawer's Snowsight-style grid and its chat (D7, D14a, D14c, D15)

The drawer (`app/src/components/SchemaDrawer.tsx`) shows one `schema/<table>.json` as three tabs drawn by
`DataGrid.tsx`, with a docked chat from `DataChat.tsx`. This file records what was verified about Snowflake Snowsight,
what the house view takes from it, the `meta.dataSources` shape, and the capability declaration the integrator passes.

## What was verified (Snowflake docs, read 2026-11-08)
- Table page, https://docs.snowflake.com/en/user-guide/ui-snowsight-data-databases-table : tabs **Table Details**
  (database, schema, type, owner role, creation time, **row count**, size, the table's SQL definition, privileges),
  **Columns** (the table's columns, with a search box), **Data Preview** ("up to the first 100 rows"; a per-column
  menu sorts ascending/descending and sets decimal precision, thousands separators, percentages; options depend on the
  column's type; previewing needs a warehouse).
- Worksheet results, https://docs.snowflake.com/en/user-guide/ui-snowsight-query : a navigable grid where cells, rows
  and ranges can be selected and copied; selecting data opens an inspector with **automatic contextual statistics**
  (up to 1 million rows): **filled/empty meters** for every column, **histograms** for date, time and numeric
  columns, **frequency distributions** for repeating text, key distributions for JSON; the statistics filter the
  results. **Query Details**: duration, result row count, completion time, data scanned, role, warehouse.
  **Download results** as CSV or TSV.
- Workspaces results, https://docs.snowflake.com/en/user-guide/ui-snowsight/workspaces-working : **Show column stats**
  puts a mini graph in every column header; clicking one opens a popover with value frequencies and **null / filled
  percentages**, whose selections filter the grid (Apply). Double-click opens a cell inspector with its data type.
- Data profile, https://docs.snowflake.com/en/user-guide/data-quality-profile : row count, last updated, and per column
  NULL count, min and max, most common values. Distinct counts were not stated on any page read.
- Appearance, https://docs.snowflake.com/en/user-guide/ui-snowsight-profile : Light, Dark or System, per user.
- Not verified from a primary source: the exact header glyphs Snowsight draws per type (no page describes them), its
  colours, and its grid's sticky behaviour. The glyphs and look below are the house's reading, marked as such.

## What the house view takes (DataGrid.tsx)
| Snowsight | House drawer |
|---|---|
| Data Preview tab | **Data preview**: row numbers, sticky typed header (glyph + name + D7 change chip), mono cells, `NULL` muted, numbers right-aligned under right-aligned headers (D30), long JSON or text cut at 40 characters with click to expand, click a header to sort, a filter box, **Copy TSV** (visible rows), caption `N sample rows · synthetic` (the first clause of `sample_source`). A grid wider than the drawer is cut into stacked bands of whole columns with the same row numbers (D26); **Expand** shows it as one band in a 95% landscape modal |
| Show column stats (header mini graphs) | one small stats badge beside each header's name (D30), no stats row and no toggle: muted when the column has no NULL in the sample, with a warning dot when it has some. Hover or focus shows a popover under the header: the column and its type, "x% null · n distinct", min and max where the kind has them (shortest and longest for text, ids and JSON), one small distribution bar (a bin per number, date or time range, else the most frequent values) and "From the N sample rows, not the database." Click, Enter or Space opens the same content as a dropdown that stays until Esc, a click outside or a second click; one is open at a time; touch uses the tap. The badge carries `aria-expanded` and `aria-controls`; Esc closes the dropdown first and leaves the drawer open. Headers and cells keep one column grid. The Columns tab keeps its meter and histogram |
| Columns tab + inspector statistics | **Columns**: name, D7 chip, note, type (or "not stated"), struck-through `was`, nulls %, distinct count, min/max (shortest/longest for text and JSON), the same mini graph |
| Table Details + Query Details | **Details**: the table's brief (D28: the summary as its purpose, a measures table with "not measured" / "not estimated" cells and each row's basis, trade-offs, place and impact), then the table facts, DDL, indexes, notes, where the table appears on the page |
| Table Details row count and size | the schema diagram's **table cards** (D26, D28): one row of rows, cols, writes and cost measures from `measured.rows`, `measured.columns`, `measured.writes`, `measured.cost`, each with the expected change after the design as a pill (red rises, green falls or stays); the card's hover card and the top of Details show the table's brief (purpose, measures table, trade-offs, a mini diagram of its place); details in schema-lane.md |
| Download results | not offered: the page contract forbids download links; Copy TSV covers small sample sets |
Every statistic is computed at render from `sample_rows`, deterministically, and labelled "from sample rows"; none is a
measurement of the real table. Glyphs (house choice): `#` number, `Aa` text, `{}` JSON, `[]` array, `id` uuid,
`01` boolean, a calendar for date and time, `?` when the JSON states no type. Light and dark come from the tdd.css
tokens only. The tabs share the drawer's head row with the table name, Expand and Close (D26). At phone width the head
wraps (name and Close, then the tabs), the grid is drawn in bands (a single column wider than the drawer still scrolls
sideways inside its band), Expand is not offered, and the Columns tab stacks each column as a card.

## Sizing keys the cards read (D26)
`measured.rows`, `measured.writes` and the optional `measured.cost` (what the table costs in production: storage USD
per month or its share of the database bill, from a billing export or cost dashboard) and `measured.columns` (the live
table's column count when the file lists only the columns the design names) each take `{value, unit, source, date,
note?}`; `expected.rows`, `expected.writes`, `expected.cost` take `{value?, unit?, note}`. The drawer's Details tab
lists them in the brief's measures table; the diagram's table card turns rows, columns, writes and cost into its one-row
strip. Neither ever computes a
measurement: an absent key reads "not measured" or "not estimated".

## The chat (DataChat.tsx)
- Docked at the drawer's foot, labelled "Ask about this table". Nothing is called on load. `claude.use("sample")` is
  awaited once when the drawer first opens; `null` (the SSR page, the harness, any copy outside claude.ai) renders
  "Chat is available when this page runs on claude.ai with the sample capability." and nothing else.
- Each send is one `sample(input, {tools, onText, signal, cache: false})` call, default model tier, with a new
  `AbortController` (Stop button). `sample` is memoryless, so `input` is rebuilt every time: a leading user turn with
  the rules plus this table's JSON (columns, sample rows, sample_source, measured, expected, DDL, indexes, notes,
  `chat_context` when present), then the last 6 turns the page keeps, then the question.
- Page tools (offered only when `sample.limits()` reports `tools`): `get_table({name})` any table in
  `content.schema`; `get_defs({query})` up to 12 matching anchors from `content.defs`; `get_section({id})` the plain
  text of a section (or any anchored block) from the content island, cut at 12 KB. Each shows progress in the reply.
- Errors branch on `code`: `not_granted`, `sampling_disabled`, `not_declared`, `capability_disabled`,
  `capability_removed` hide the chat for the view; `rate_limited`, `session_expired`, `refused`, `empty_completion`,
  `prompt_too_large`, `tools_unavailable` say what to do; `cancelled` keeps the partial; anything else keeps the
  partial marked interrupted. The page never retries by itself.

## Connector hook: `meta.dataSources` (optional)
```json
"dataSources": [
  {"name": "seed", "server": "host:postgres_seed", "tool": "query", "arg": "sql", "kind": "seed",
   "note": "local seed database; synthetic health-system rows"}
]
```
| Field | Meaning |
|---|---|
| `name` | what the chat calls the source (`query_source` takes it) |
| `server` | a claude.ai connector's display name, or `host:<name>` for an MCP server on the viewer's Mac (answers only in the Claude desktop app) |
| `tool` | the connector tool to call, its upstream name |
| `arg` | optional: the tool's input member that takes the SQL text (read it from the tool's schema; never guess). Without it the model must pass an object of arguments |
| `kind` | `prod`, `local`, `render` or `seed` (D14c: work = hosted prod, local, or the home seed generator; personal = Render or local) |
| `note` | one line the chat is told about the source |
Only when `sample.limits()` reports tools does the page ask `claude.use("mcp")`; when that resolves and
`mcp.listTools()` lists a source's server (a `host:` server lists only inside the Claude desktop app; `needs_reauth`
shows "Reconnect <server> in claude.ai Settings → Connectors"), the chat gains `query_source({name, sql_or_args})`, which calls
`(await mcp.server(server))[tool](args)`, refuses SQL that writes (INSERT, UPDATE, DELETE, MERGE, DROP, ALTER, TRUNCATE,
CREATE, GRANT, REVOKE, COPY, CALL and similar), cuts results at 30 KB, turns connector errors into per-code copy
(reauth, add the connector, approval, policy), and the dock reads "connected via <server>". Without it the dock reads "Answers come
from the sample rows on this page only." The page never embeds production data and holds no credentials; a connector
answers with the viewer's own credentials. Compiler request: `build_tdd.py` must copy `meta.data_sources` or
`meta.dataSources` into `content.meta.dataSources` (today it drops unknown meta keys).

## Publish-time capabilities (the integrator passes these to the Artifact tool)
- Reference packet (no data sources): `capabilities: {"sample": {}}`
- With data sources: one `servers[]` entry per distinct `server`, `tools` = the union of their `tool` values:
```json
{"sample": {}, "mcp": {"servers": [{"server": "host:postgres_seed", "tools": ["query"]}]}}
```
`tools` must be non-empty. Declaring `mcp` gives viewers outside the owner's reach `null` (they get the sample-only chat);
host servers answer only for the artifact's owner in the Claude app.

## Harness
`bun harness/render.tsx <build> <out.html> [--stub-claude] [--data-sources <json>]` then
`node harness/shoot.mjs <out.html> <png dir> <table> ... [--prefix datagrid] [--tabs preview,columns,details] [--chat]`.
`--stub-claude` installs a fake `window.claude` (canned streamed reply, real tool executes) to shoot the chat UI; its
replies say "harness stub" and are never Claude's.
