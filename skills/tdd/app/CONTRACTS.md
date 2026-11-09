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
| `build.ts`, `check_hydrate.ts`, `package.json`, `tsconfig.json`, `src/App.tsx`, `src/client.tsx`, `src/components/{Blocks,Diagram,Shot,Prs,Rail,Masthead,Overlays}.tsx`, `src/lib/*` | app-core worker | overlays = RefPop, RefPanel, HsPop, Lightbox; delegation lives in App | output 1 (`scripts/eval/ev_artifact.py`): `page_exists`, `page_size`, `check_hydrate`, `artifact_check`, `refs_linked`, `diagram_hotspots`; after every change, before returning |
| `src/components/SchemaDrawer.tsx`, `src/schema.css`, `harness/**` | schema worker (D7) | the drawer; harness renders it standalone for screenshots | `schema_ddl` (output 1), `schema_md` (output 2, `ev_packet.py`), the table and column questions in `probe_answerable` (`ev_probe.py`); after every drawer or harness change |
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
| `hs` | `{ el: HTMLElement \| null }` | App delegation on `[data-hs]` hover/focus/leave | HsPop (`.hs-pop`: label, note, image, go link); `null` hides |
| `table` | `{ id }` | App delegation on `[data-hs][data-table]` click and on `a.ref` whose def.kind === "table" | SchemaDrawer (`dialog.drawer.schema`) |
| `shot` | `{ src, alt, caption }` | App delegation on `figure.shot img` click | Lightbox (`.shot-scrim` + `figure.shot.big`) |
| `reveal` | `{ id }` | hashchange, panel "Go to it" | App: opens ancestor `<details>`, flashes `tr.lit` |
`reveal(id)` in bus.ts does the DOM work; `bus.emit` is a no-op on the server.

## Class vocabulary
`../templates/tdd-system/tdd.css` is the contract; the summary of classes per component is in `../templates/tdd-system/README.md` (skill-docs worker writes it from the stylesheet). Components use these classes exactly; new classes go into tdd.css (app-core) or schema.css (schema worker).

## content.json essentials
- `sections[].blocks[]` node types: p, heading, list, table, code, callout, hr, placeholder, shot, legend, diagram, cards, persona, duo, today, fold, group, decisions, decision, options, steps, cols, col, glossary, prs, box. Inline HTML strings (`html`, `title`, `caption`, `reading`, `one`, `sub`, `bodyHtml`, `summary`, `status`, `change`, `rollback`) already carry `<a class="ref" data-ref>` links, `<span class="chip">`, `<a class="cite-link"><code class="cite">`, and are trusted.
- `defs[id] = {kind, title, text}` for every anchor: section, heading, persona, fold, decision, row, card, pr, diagram, glossary, paragraph, table.
- `schema[]` tables (D7): `{id, table, state new|changed|existing, summary, columns[{name,type,change new|changed|dropped|unchanged, note, was}], sample_rows[], sample_source, measured{reads,writes,rows,...:{value,unit,source,date,note}}, expected{...:{value,unit,note}}, ddl, indexes[], notes[]}`; an absent measure renders "not measured", never a guess.
- `diagram.hotspots[] = {label,x,y,w,h (percent), href, note, image, table (schema id), tone}`.
- `nav[]`, `decisions[]`, `cites[]`, `glossary{term:id}`, `refs{linked, unlinked[]}`, `stats{}`.

## Proof commands (every worker runs what applies, records output in its return)
```
cd ~/.claude/skills/tdd/app && bun install && bunx tsc -p . --noEmit            # TypeScript 7
bun run build.ts ~/.system-design/<org>-web-app/system-design/<slug>/build
bun run check_hydrate.ts  <same build dir>/tdd.html                                # jsdom hydrate: zero console errors
node ~/.claude/skills/system-design/scripts/artifact_check.mjs <build dir>/tdd.html  # contract + 1440/390 light/dark renders
python3 -I ~/.claude/skills/tdd/scripts/build_tdd.py <slug>.md build  # in the reference packet dir
~/.local/share/tdd-skill/venv/bin/python -I ~/.claude/skills/tdd/scripts/eval_packet.py <packet dir>   # eval lane -> <packet>/build/eval/ (D18: every worker, every reviewer)
```
Screenshots: playwright-core is installed under `~/.local/share/system-design-skills/node_modules`; the helper `<scratchpad>/shoot.mjs (a playwright-core screenshot helper; any equivalent works) <file.html> <out-prefix> [width]` writes `<prefix>-top.png` and `<prefix>-full.png`. Reference screenshots to match: `~/.system-design/house/system-design/tdd-system/shots/reference-1440-*.png`.

## Rules for every worker
- Commit nothing, push nothing, switch no branch. `~/.claude/skills` has other sessions' uncommitted edits: do not touch files outside your scope.
- Never invent a measurement, a screenshot, a count or a test result. Unverified claims are marked unverified in the return.
- Typed return at the end: `DONE | DONE_WITH_CONCERNS | NEEDS_CONTEXT | BLOCKED | NEEDS_HUMAN`, with the files written, the proof output (counts, not prose), the eval summary line and the scorers that moved (a reviewer: the score diff), and open requests for other owners.
