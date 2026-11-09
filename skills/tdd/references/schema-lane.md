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
| `measured.rows`, `measured.writes` | production row count and write rate | the diagram card's rows tile shows the row count; writes is in the card's hover detail |
| `measured.cost` | optional: what the table costs in production (storage USD per month, its share of the database bill) | from a billing export or cost dashboard, with `source` and `date`; the card's cost tile shows it, `expected.cost` beside it |
| `measured.columns` | optional: the live table's column count, when the file lists only the columns the design names | the card's cols tile shows it; without it the tile counts the file's columns (dropped ones left out) |
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
too. Esc, the Close button and a click on the scrim close it; Close takes focus on open.

1. Head, one row (D26): the table name in mono and the state chip, the tabs Data preview, Columns and Details, then
   Expand (only when the grid is banded) and Close. On phones it wraps: name and Close, then the tabs.
2. Sample rows (Data preview): a mono grid with row numbers, NULL in muted italics, with a caption giving the row and
   column count and `sample_source`. When the columns do not fit the drawer's width the grid is drawn in stacked bands
   of whole columns (columns 1–k, then k+1–n, each band captioned "Columns a–b of n" and carrying the same row numbers
   in a sticky first column; the hovered row lights on every band). Phones keep the banded, full-width drawer.
3. Expand turns the same dialog into a landscape modal at 95% of the page's width and height holding only the tabs,
   the grid as one band that scrolls sideways under its sticky header, and the chat dock; the filter, sort, tab and
   chat carry over. Esc, a click outside and its Close button return to the drawer.
4. Columns: a legend for the change kinds present, then one row per column. New rows are tinted good, changed rows
   warn with the old definition struck through, dropped rows danger with the name struck through; each with a 3px bar
   on the left and a chip. In a new table every column is new, so the legend says so and the per-row chips are left
   out. On phones each column becomes a stacked card.
5. Details: the summary, then Load: a Measured card beside an Expected card. Reads, writes, rows and storage always
   appear, then any other key in file order. A measured value shows `value unit`, then `source · date`, then the note;
   an absent one reads "not measured". An expected value shows `value unit` and its basis; an absent one reads "not
   estimated". Then DDL, indexes and constraints, notes, and where it appears: every anchored block on the page whose
   own content names the table, as `a.ref` links.

## Table cards on the schema diagram (D26)

Every diagram box whose `table` has a schema file is drawn as that table's card (`app/src/components/TableNode.tsx`),
positioned exactly over the box (the SVG box stays underneath, covered) and server-rendered, so it reads the same with
JavaScript off. The card is the hotspot: hover shows the hotspot card, a click opens the drawer, Tab reaches it.
Its colours are the theme tokens, so it is light in the light theme and dark in the dark one; the box's tone (green,
grey, blue, amber, red, violet) tints its frame and head.

- Head: the table name, the state chip, and the text after " · " in the box's label.
- Columns: primary and foreign keys first and set larger, badged `PK` / `FK`, with `→ table` when the column's note
  (`→ name`), type (`REFERENCES name`) or an `indexes[]` line (`FOREIGN KEY … (cols) → name`, `PRIMARY KEY (cols)`)
  names one; then changed columns in their change colours with a `new` / `changed` / `dropped` tag (not in a new
  table, where every column is new); then the rest, as many as the box holds, in table order, and "+N more columns".
- Sizing strip (D28), on every card that has room: one row of four measures, rows, cols, writes and cost, about a
  third of the three-row tiles it replaced. Each is an icon, the production number from `measured.rows`,
  `measured.columns` (else the file's column count), `measured.writes`, `measured.cost` ("–" when not measured; a long
  figure shortened, 37,821 → 37.8k), and beside it the expected change after the design from `expected.rows`, the
  column change flags (`+new −dropped`), `expected.writes`, `expected.cost` as a pill (no pill when not estimated).
  The pill is red when the measure rises, green when it falls or stays, grey when the file states no direction: a
  leading `+` or `−`, a word (no change, none, grows, fewer…), a comparison with the measured number in the same unit,
  or, for a new table, any figure (a rise from nothing). No labels or prose in the row; never a zero for a missing
  value. When the four do not fit one row with their icons, the icons go (the tooltip still names each measure).
- Detail, by every input: pointing at a measure shows its tooltip (label, production value, after this design, source
  and date, basis); hovering or focusing the card shows the table's brief in its hover card (below), so the keyboard
  reads everything; on a touch screen a tap on a measure shows its tooltip and a second tap, or a tap elsewhere, hides
  it (a tap elsewhere on the card opens the drawer, whose Details tab starts with the same brief). The card's
  `aria-describedby` names a hidden text with every line, and without JavaScript each measure keeps a native `title`.
- Size: column names and strip numbers are about 11.5 to 12 px when the page fits the drawing to a 1440 px window
  (keys 1.15 times that, pills and labels at least 10 px), and grow with Actual size. A box that cannot hold a column
  line at that size may go down to 11 px; below that the card drops column lines (the count moves into the head as
  "+N cols"), then its strip, and keeps its size: a compact card shows the name, the chip, the key lines that fit and
  the count. Only a box too short for the head itself shrinks the type. The head and the strip need about 4.7 ems of
  box height, each column line about 1.45 (a key line 1.6). Draw table boxes tall enough for their keys and the strip
  (a box of about 210 drawing px in a 2400 px wide drawing shows three key lines and the strip); at phone width the
  whole drawing is scaled to the column, so read it with Actual size.

## The table brief (D28)

One shape for a table's detail, wherever it opens (`app/src/components/TableBrief.tsx`): the hover card of a table
card on the schema diagram (hover or keyboard focus) and the top of the drawer's Details tab.

1. **Purpose**: the first sentence of `summary` (the drawer shows the whole summary). Write that sentence to stand alone.
2. **Measures**: a small table, measure · production · after this design · delta · source (date), one row each for
   rows, columns, writes and cost, with "not measured" / "not estimated" cells rather than prose; the drawer adds a row
   for every other `measured` or `expected` key and lists each row's basis note under the table, the hover card counts
   them ("+N more measures … in the table's Details"). Columns: production is the live count (`measured.columns`) or
   the file's columns less the new ones ("none" for a new table), after is that count with the design's change.
3. **Trade-offs**: the optional `tradeoffs` field, two or three terse lines of what the design gains and costs for
   this table, taken from the TDD's own text, never invented: `[{ "gain": "…", "cost": "…" }]` or `["…"]` (a string
   starting with `+` or `gain:` is a gain, with `−` or `cost:` a cost). Shown as `+` and `−` bullets; hidden when absent.
4. **Place and impact**: an inline SVG drawn from the cards' keys, no library. The table sits in the middle; the tables
   it references (its foreign keys, from column types, notes and `indexes[]`) are on the right and the cards that
   reference it on the left, each side box naming the key (`post_id →`, `← request_id`); arrows point at the
   referenced table. Boxes take the table's tone on the page's diagrams, else new green, changed amber, existing
   grey; a referenced table with no card is dashed. A dotted "writes" arrow into the table carries `expected.writes`
   (else `measured.writes`) when the card states one. More than five tables on a side collapse into "+N more".

## Proof and preview

```
T="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/tdd"                  # this skill's folder, as SKILL.md sets it
python3 -I "$T/scripts/build_tdd.py" <source.md> build      # "N schema tables", no "has no schema" warning
cd "$T/app"
bun harness/render.tsx <packet>/build <scratch>/schema-harness.html [--extra ../templates/schema.template.json]
node harness/shoot.mjs <scratch>/schema-harness.html <png dir> <table> [<table> ...]
```

The harness renders the packet's schema diagram (with its table cards) and the real drawer (server-rendered closed,
hydrated, opened by a real click on the card), then writes `schema-drawer-<table>-<1440|390>-<light|dark>.png` at the
drawer's full height and prints checks as JSON: opened by click, Close focused, Esc closes, scrim click closes, `#tbl-…`
opens, drawer width, bands or a sideways-scrolling grid on phones, no horizontal overflow, absent measures read as
words, zero hydration and console errors. `--extra` previews a schema file that is not in a packet yet.

`node harness/shoot-d26.mjs <build>/tdd.html <png dir>` checks the built page itself: the one-row head, the bands of
the widest table, the Expand view (95% of the page, one band, Esc and Close return to the drawer), the column stats
badges (D30: no stats row or toggle, one labelled badge per column, headers in line with cells, hover shows the popover,
a click pins it and it stays when the pointer leaves, a second click, a click outside and Esc close it, Esc keeps the
drawer, Enter and Space open it, one open at a time, a tap opens it on a phone), the table brief in the Details tab
(D28: measures with "After this design", trade-offs where the card has them, the mini diagram with one edge per
neighbour box, at least one where foreign keys exist, and a writes arrow where the card states writes), the table
cards (D28: four measures in one row no taller than a third of the three-row tiles they replaced, or a compact card;
nothing clipped, no value, delta or name cut, focusable and described; at 1440 names and numbers of at least 11 px and
labels of at least 10 px; a measure's tooltip under the pointer; every focused card's hover card holds its own brief;
a tap on a measure toggles its tooltip on a touch screen without opening the drawer; a click opens the drawer), and
writes `d26-{drawer,bands,modal,diagram,diagram-tip,diagram-focus,diagram-touch}-…png`,
`d28-{strip,detail-pop,detail-drawer}-1440-…png` and `d30-stats-{hover,open}-1440-…png` plus one
`d30-stats-open-390-light.png`.

The drawer's data preview, column statistics and the in-page chat are described in `references/data-view.md` (D14).
