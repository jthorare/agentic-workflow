# tdd/app contracts (read before writing anything here)

The page is React 19.3.1, server-rendered by `build.ts` under bun into the claude.ai artifact page, hydrated by one inline production IIFE. Type-checks with TypeScript 7 (`typescript@7.0.2`). Content comes from `build/content.json`, written by `../scripts/build_tdd.py` (read its docstring and `Compiler` class for the exact node shapes; `src/types.ts` mirrors them).

## Page assembly (build.ts owns this)
```
<title>{tabTitle}</title>
<meta name="claude:viewport-fit-sides" content="cover">
<style>{templates/tdd-system/tdd.css}{src/schema.css}</style>      @import of Google Fonts stays first inside the style
<div id="tdd-root">{renderToString(<App content/>)}</div>
<script type="application/json" id="tdd-data">{content JSON, "<" escaped as <}</script>
<script>{client IIFE, "</script" escaped}</script>
```
No doctype/html/head/body (the host wraps the file). External scripts: none (React is inline). Stylesheets: only the Google Fonts @import already in tdd.css. No iframes, alert, print, download links. 16 MB limit. Must read correctly with JavaScript off: every block is real HTML, `<details>` are native and uncontrolled (never pass a changing `open` prop), the rail is plain `<a href="#id">`.

## Files and owners
| Path | Owner | Notes | Eval duty (D18): what it scores, when |
|---|---|---|---|
| `build.ts`, `check_hydrate.ts`, `package.json`, `tsconfig.json`, `src/App.tsx`, `src/client.tsx`, `src/components/{Blocks,Diagram,Shot,Prs,Rail,Masthead,Overlays}.tsx`, `src/lib/*` | app-core worker | overlays = RefPop, RefPanel, HsPop, TipPop, Lightbox; delegation lives in App. Diagram.tsx draws a TableNode over each hotspot whose table has a schema card (D26). HsPop shows the table brief (TableBrief, D28) when the hotspot names a schema table (`.hs-pop.has-brief`), so a focused or hovered table card reads its purpose, measures, trade-offs and place; TipPop shows one `[data-tip]` element's lines under the pointer, or on a tap on touch screens (`.tn-tip`) | output 1 (`scripts/eval/ev_artifact.py`): `page_exists`, `page_size`, `check_hydrate`, `artifact_check`, `refs_linked`, `diagram_hotspots`; after every change, before returning |
| `src/components/{SchemaDrawer,DataGrid,TableNode,TableBrief}.tsx`, `src/schema.css`, `harness/**` | schema worker (D7, D26, D28, D30) | the drawer (SchemaDrawer: one-row head, Expand view); DataGrid: the tabs, the grid drawn as bands of columns with a stats badge per header (D30), Columns and Details; TableNode: the table card on a schema-diagram box (keys, change marks, the one-row rows/cols/writes/cost strip with a `[data-tip]` per measure, a compact card for small boxes); TableBrief: the table brief (purpose, measures table, trade-offs, mini diagram) shared by the hover card and the Details tab; harness renders the drawer standalone (`render.tsx`, `shoot.mjs`) and checks a built page (`shoot-d26.mjs`) | `schema_ddl` (output 1), `schema_md` (output 2, `ev_packet.py`), the table and column questions in `probe_answerable` (`ev_probe.py`); after every drawer or harness change |
| `../templates/tdd-system/tdd.css` | app-core worker | small additive fixes only; the generated FONTS/TOKENS blocks are never hand-edited | `artifact_check` (its 1440/390 light/dark renders) and `check_hydrate`; after every stylesheet change |
| `../scripts/write_agents.py`, `../scripts/strip_page.py`, `../references/agents-lane.md`, `../templates/agents/**` | agents-lane worker (D6, D8) | reads content.json and the built page; writes `agents/` | output 2 (`ev_packet.py`): `agents_present`, `agents_current`, `readme_bytes`, `largest_slice`, `anchors_sliced_once`, `sql_extracted`, `map_edges_resolve`, `images_indexed`, `prs_copied`, `probe_answerable`; `lane_size` and `build_sizes` (output 6, `ev_tokens.py`); after every generator or template change |
| `../scripts/lint_packet.py`, `../references/validators.md` | lint worker | reads content.json, content.files.json, the packet on disk, git | `lint` (output 1; the eval writes `build/eval/lint.json`), and `refs_linked` or `cites_href` when a rule about them changes; after every rule change |
| `SKILL.md`, `../templates/tdd-template.md`, `../templates/meta.template.json`, `../scripts/new_packet.py`, `../references/{evidence-rules,diagram-conventions,mock-conventions,publish-and-mirror}.md`, `../templates/tdd-system/README.md` | skill-docs worker | documents the CLIs as declared here and in each script's docstring | `sections_house`, `decisions_status`, `refs_linked`, `cites_href` (output 1) when a template changes; runs the eval exactly as SKILL.md step 5 writes it, so the documented command is the one that ran; after every doc or template change |
| `../scripts/eval_packet.py`, `../scripts/eval/**`, `../references/eval-loop.md`, `../templates/eval/**` | eval worker (D10, D12) | scores are measurements, not gates; `--push` is gated (below) | all six outputs, before and after its change; no scorer turns `not run` or `error` without a stated reason; never `--push` |
Nobody edits another owner's files; a needed change in another scope is written into the return as a request.

## Eval duty (D18)
The eval runs as part of the work, not after it (`../SKILL.md`, step 5; `../references/eval-loop.md`, Who runs it and when).
- **Writer** (every row above, and any agent that changes a packet or implements its design): after the last rebuild,
  run the eval for the reference packet and for any packet the change rebuilt. Return the summary line
  (`scored N · metrics N · not run N · errors N`) and every scorer that moved since the previous `history.jsonl` line.
  A drop is fixed or explained; a new `not run` names the missing tool or key.
- **Reviewer of record** (fresh context): run the same command on the writer's tree and return a score diff, not just
  prose: per output and scorer, the writer's value, the reviewer's value, the delta; whether the input `digest` matched;
  any scorer that changed status. The jq for it is in eval-loop.md. A review without the diff is incomplete.
- **Integrator** (the session that merges): runs it once more on the merged tree. Only it passes `--push`, and only when
  `BRAINTRUST_API_KEY` is in the environment (never in a file) and the owner has said go.
- Never pass `--run-tests` unattended where the repo's test command loads secrets (eval-loop.md, output 4).

## Bus events (src/lib/bus.ts), emitted only in the browser
| Event | Payload | Who emits | Who listens |
|---|---|---|---|
| `ref` | `{ id, anchor: HTMLElement, pin?: boolean }` | App delegation on `a.ref` hover/focus (`data-ref`) | RefPop (hover card: kind, title, text, Open button) |
| `ref-panel` | `{ id }` | RefPop Open button; App delegation on `a.ref` click | RefPanel (`dialog.ref-panel`, mobile-sized; clones the anchor's element into `.ref-panel-body`, "Go to it" link) |
| `hs` | `{ el: HTMLElement \| null }` | App delegation on `[data-hs]` hover/focus/leave; TipPop emits `null` when a tile's tip opens | HsPop (`.hs-pop`: label, note, image, go link, and the `[data-tip]` lines inside `el`); `null` hides |
| `table` | `{ id }` | App delegation on `[data-hs][data-table]` click and on `a.ref` whose def.kind === "table" | SchemaDrawer (`dialog.drawer.schema`) |
| `shot` | `{ src, alt, caption }` | App delegation on `figure.shot img` click | Lightbox (`.shot-scrim` + `figure.shot.big`) |
| `reveal` | `{ id }` | hashchange, panel "Go to it" | App: opens ancestor `<details>`, flashes `tr.lit` |
`reveal(id)` in bus.ts does the DOM work; `bus.emit` is a no-op on the server.

## Class vocabulary
`../templates/tdd-system/tdd.css` is the contract; the summary of classes per component is in `../templates/tdd-system/README.md` (skill-docs worker writes it from the stylesheet). Components use these classes exactly; new classes go into tdd.css (app-core) or schema.css (schema worker).

schema.css (schema worker) adds these families; components use them exactly:
- **Drawer** (SchemaDrawer.tsx): `dialog.drawer.schema` (`.expanded` for the Expand view, 95% of the window), `.drawer-head`, `.drawer-name`, `.drawer-acts`, `.drawer-expand`, `.drawer-close`, `.drawer-body`, `.drawer-summary` (the brief's purpose in Details), `.drawer-sec`, `.drawer-notes`.
- **Grid** (DataGrid.tsx, `dg-*`): `.dg-tabs`, `.dg-tab`, `.dg-panel`, `.dg-preview`, `.dg-bar`, `.dg-filter`, `.dg-count`, `.dg-shown`, `.dg-actions`, `.dg-btn`, `.dg-toast`, `.dg-bands`, `.dg-band`, `.dg-band-cap`, `.dg-band-names`, `.dg-ghost-box`, `.dg-ghost` (the hidden table that measures column widths), `.dg-grid`, `.dg-head` (`th.num` right-aligned), `.dg-hc` (a header's sort button and badge), `.dg-h`, `.dg-glyph`, `.dg-sort`, `.dg-sb` (the stats badge: `.nulls` dot, `.on` while its popover shows; a span in the ghost grid), `.dg-spop` (the one stats popover: `.show`, `.open` when pinned) with `.dg-sp-h`, `.dg-sp-n`, `.dg-sp-r`, `.dg-sp-d`, `.dg-sp-note`, `.dg-mini`, `.dg-hist`, `.dg-freq`, `.dg-meter`, `.dg-range`, `.dg-empty`, `.dg-more`, `.dg-cap`, `.dg-cols`, `.dg-details`, `.dg-kv`.
- **Table card** (TableNode.tsx, `tn-*`) on `a.hs.has-table.tnode` with `tone-<green|grey|blue|amber|red|violet>`, `dashed`, `st-<state>` and `compact` (no strip): `.tn-in`, `.tn-head`, `.tn-name`, `.tn-chip`, `.tn-desc`, `.tn-hidden` (the "+N cols" count), `.tn-cols`, `.tn-col` (`.tn-key` for a PK or FK line, `ch-new|changed|dropped`), `.tn-k` (`.pk`, `.fk`), `.tn-cn`, `.tn-ref`, `.tn-ty`, `.tn-tag`, `.tn-more`, `.tn-strip` (one row, `.tight` drops the icons), `.tn-m` (`k-rows|cols|writes|cost`, carries `[data-tip]`), `.tn-ico`, `.tn-v` (`.none` for "–"), `.tn-d` (`.up` red, `.down` and `.same` green, `.unknown` grey; absent when not estimated), `.tn-dir-ico`. Never `.key` on a card line: tdd.css owns `.key` (the legend).
- **Table brief** (TableBrief.tsx, D28) in `.hs-pop.has-brief` (mode pop) and at the top of Details (mode drawer): `.tbrief` (`.pop`, `.drawer`), `.tb-title`, `.tb-chip`, `.tb-purpose`, `.tb-sec`, `.tb-measures` (`.none` cells, `.src`, `tr.extra`), `.tb-d` (the delta pill, same tones as `.tn-d`), `.tb-more`, `.tb-basis`, `.tb-trade` (`li.gain|cost|note`, `.tb-sign`), `.tb-map`, `.tb-map-box`, `.tb-map-key`; the mini diagram `svg.mm` with `.mm-node` (`.mid`, `t-<tone>`, `.ext` for a table without a card, `t-more`), `.mm-name`, `.mm-key`, `.mm-state`, `.mm-edge` (`.w` the writes arrow), `.mm-arrow`, `.mm-writes`, `.mm-wlabel`.
- **Overlays** styled here: `.tn-tip` (TipPop) and the line parts `.k`, `.up`, `.down`, `.same` inside it; `.hs-pop.has-brief` (width).
Colours come from the theme tokens (`--fg`, `--muted`, `--surface`, `--surface-2`, `--line`, `--line-strong`, `--accent`, `--good`, `--warn`, `--danger`, `--secondary`, `--shadow`) and `color-mix` of them, so the cards and grids follow the light and dark themes; no literal colour outside the token blocks.

## content.json essentials
- `sections[].blocks[]` node types: p, heading, list, table, code, callout, hr, placeholder, shot, legend, diagram, cards, persona, duo, today, fold, group, decisions, decision, options, steps, cols, col, glossary, prs, box. Inline HTML strings (`html`, `title`, `caption`, `reading`, `one`, `sub`, `bodyHtml`, `summary`, `status`, `change`, `rollback`) already carry `<a class="ref" data-ref>` links, `<span class="chip">`, `<a class="cite-link"><code class="cite">`, and are trusted.
- `defs[id] = {kind, title, text}` for every anchor: section, heading, persona, fold, decision, row, card, pr, diagram, glossary, paragraph, table.
- `schema[]` tables (D7): `{id, table, state new|changed|existing, summary, columns[{name,type,change new|changed|dropped|unchanged, note, was}], sample_rows[], sample_source, measured{reads,writes,rows,...:{value,unit,source,date,note}}, expected{...:{value,unit,note}}, ddl, indexes[], notes[]}`; an absent measure renders "not measured", never a guess.
- `diagram.hotspots[] = {label,x,y,w,h (percent), href, note, image, table (schema id), tone}`.
- `nav[]`, `decisions[]`, `cites[]`, `glossary{term:id}`, `refs{linked, unlinked[]}`, `stats{}`.

## Proof commands (every worker runs what applies, records output in its return)
```
T="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/tdd"; S="${SYSTEM_DESIGN_SKILL:-$T/vendor/system-design}"   # T and S as SKILL.md sets them
cd "$T/app" && bun install && bunx tsc -p . --noEmit                         # TypeScript 7
bun run build.ts ~/.system-design/<org>-web-app/system-design/<slug>/build
bun run check_hydrate.ts  <same build dir>/tdd.html                                # jsdom hydrate: zero console errors
node "$S/scripts/artifact_check.mjs" <build dir>/tdd.html                        # contract + 1440/390 light/dark renders
python3 -I "$T/scripts/build_tdd.py" <slug>.md build  # in the reference packet dir
~/.local/share/tdd-skill/venv/bin/python -I "$T/scripts/eval_packet.py" <packet dir>   # eval lane -> <packet>/build/eval/ (D18: every worker, every reviewer)
```
Screenshots: playwright-core is installed under `~/.local/share/system-design-skills/node_modules`; the helper `<scratchpad>/shoot.mjs (a playwright-core screenshot helper; any equivalent works) <file.html> <out-prefix> [width]` writes `<prefix>-top.png` and `<prefix>-full.png`. Reference screenshots to match: `~/.system-design/house/system-design/tdd-system/shots/reference-1440-*.png`.

## Rules for every worker
- Commit nothing, push nothing, switch no branch. The skill folder may hold other sessions' uncommitted edits: do not touch files outside your scope.
- Never invent a measurement, a screenshot, a count or a test result. Unverified claims are marked unverified in the return.
- Typed return at the end: `DONE | DONE_WITH_CONCERNS | NEEDS_CONTEXT | BLOCKED | NEEDS_HUMAN`, with the files written, the proof output (counts, not prose), the eval summary line and the scorers that moved (a reviewer: the score diff), and open requests for other owners.
