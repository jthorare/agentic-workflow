# The schema lane (D7)

The owner's ruling, 2026-11-08: when a TDD shows the data model, a reader can open any table from the schema diagram
and see, for a new table, sample rows; for an existing table, sample rows, a clear indicator of how each column
changes, measured reads and writes, and the expected change to database load and system resources. An unmeasured value
reads "not measured". Nothing in this lane is ever invented.

## Files

```
<packet>/schema/<table>.json            one file per table the design adds, changes, or leans on
<packet>/diagrams/schema.hotspots.json  the schema diagram's boxes; a box's "table" names the file it opens
```

Start from `templates/schema.template.json` (every field annotated in its `_about` block; delete `_about` and every
EXAMPLE value in the copy). The compiler, `scripts/build_tdd.py`, reads every `schema/*.json` in name order, sets
`id` to `tbl-<table slug>` (for example `tbl-hold-ranking`), defines that id as a `table` anchor, and writes the
tables to `content.json` as `schema[]`. A box whose `table` has no file is a warning:
`hotspot schema/'…': table x has no schema/x.json`.

## Fields

| Field | Meaning | Rule |
|---|---|---|
| `table` | the real table name | defaults to the file name |
| `state` | `new`, `changed` or `existing` | defaults to `existing`; drives the header chip (good, warn, plain) |
| `summary` | two or three sentences: what a row is, what this design does to the table, which PR | from the design's own text |
| `columns[]` | `{name, type, change, was?, note?}` | `change` is `new`, `changed`, `dropped` or `unchanged` |
| `columns[].type` | the SQL type and nullability as the source states them | `""` when the source doesn't state it: the drawer prints "not stated" |
| `columns[].was` | the previous definition of a `changed` column | shown struck through under the new type |
| `sample_rows[]` | a few rows keyed by column name | synthetic and shaped like the DDL unless a reviewed, scrubbed export says otherwise; never personal data |
| `sample_source` | where the rows came from | e.g. "synthetic, shaped like the DDL; not production data" |
| `measured{key}` | `{value, unit, source, date, note?, label?}` | recorded facts only (below) |
| `expected{key}` | `{value?, unit?, note, label?}` | the design's own reasoning, with the basis quoted in `note` |
| `ddl` | the CREATE or ALTER statements | verbatim from the design; say in a comment when you had to render a stated change as SQL |
| `indexes[]` | keys, indexes and constraints | one string each |
| `notes[]` | short facts a reviewer needs | `` `backticks` `` render as code |

Set `change` on every column of a `changed` table. The compiler fills a missing `change` with `unchanged` only when
`state` is `existing`, and with `new` otherwise, so an unmarked column of a changed table would read as new.

## Where measurements come from

A `measured` entry is a number someone actually observed, with the place it was observed:

- a read-only production query or dashboard the design cites (for example the TDD's "How big this is" counts);
- `pg_stat_statements`, `pg_stat_user_tables` (`n_tup_ins`, `n_tup_upd`, `n_tup_del`, `seq_scan`, `idx_scan`),
  `pg_total_relation_size`, or the audit table (`audit.record_version` insert and delete counts), run read-only;
- APM or tracing percentiles (p50, p99) for the query or the service call;
- a research file in the packet (`research/*.md`) that records the query and its output.

`source` names that place precisely enough to rerun it ("TDD rev 4 · How big this is (#sizing)", "research/d6.md",
"Datadog: db.query p99, service api, 7 days"). `date` is the day it was measured; when the source gives no date, use
the source document's date and say so in `note`. Say in `note` what the number includes (all tenants or approval
units only, test tenants or not, 90 days or a peak hour). Leave the key out when nothing was measured. Never derive a
"measured" number by arithmetic on another one; put a derived figure under `expected` with its working in `note`.

An `expected` entry is the design's reasoning about load after the change: extra reads per request, writes per action,
rows per entity, a new index, audit rows. `note` quotes or cites the line it rests on. When the design gives a basis
but no figure, keep the key with only a `note`: the drawer prints "not estimated" and the basis.

## What the drawer shows

`app/src/components/SchemaDrawer.tsx` (styles in `app/src/schema.css`) is a right-side `<dialog class="drawer schema">`,
up to 720px wide and full height, full width on phones, opened by the bus event `table {id}`. The App emits it when a
diagram box with a table (`[data-hs][data-table]`) or a link to a `table` anchor is clicked; a `#tbl-…` hash opens it
too. Esc, the Close button and a click on the scrim close it.

1. Header: the table name in mono, the state chip, Close.
2. Summary.
3. Sample rows (first for a new table): a horizontal-scroll mono table, NULL in muted italics, with a caption giving
   the row and column count and `sample_source`.
4. Columns (first for a changed or existing table): a legend for the change kinds present, then one row per column.
   New rows are tinted good, changed rows warn with the old definition struck through, dropped rows danger with the
   name struck through; each with a 3px bar on the left and a chip. In a new table every column is new, so the legend
   says so and the per-row chips are left out. On phones each column becomes a stacked card.
5. Load: a Measured card beside an Expected card. Reads, writes, rows and storage always appear, then any other key in
   file order. A measured value shows `value unit`, then `source · date`, then the note; an absent one reads "not
   measured". An expected value shows `value unit` and its basis; an absent one reads "not estimated".
6. DDL, indexes and constraints, notes.
7. Where it appears: every anchored block on the page whose own content names the table, as `a.ref` links.

On the schema diagram, every box with a table carries a small "rows" badge in its top-right corner, visible without
hover, so a reader can see which boxes open the drawer.

## Proof and preview

```
T="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/tdd"                  # this skill's folder, as SKILL.md sets it
python3 -I "$T/scripts/build_tdd.py" <source.md> build      # "N schema tables", no "has no schema" warning
cd "$T/app"
bun harness/render.tsx <packet>/build <scratch>/schema-harness.html [--extra ../templates/schema.template.json]
node harness/shoot.mjs <scratch>/schema-harness.html <png dir> <table> [<table> ...]
```

The harness renders the packet's schema diagram and the real drawer (server-rendered closed, hydrated, opened by a
real click on the box), then writes `schema-drawer-<table>-<1440|390>-<light|dark>.png` at the drawer's full height
and prints checks as JSON: opened by click, Esc closes, scrim click closes, `#tbl-…` opens, drawer width, no
horizontal overflow, absent measures read as words, zero hydration and console errors. `--extra` previews a schema
file that is not in a packet yet.

The drawer's data preview, column statistics and the in-page chat are described in `references/data-view.md` (D14).
