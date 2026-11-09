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
| Path | Owner | Notes |
|---|---|---|
| `build.ts`, `check_hydrate.ts`, `package.json`, `tsconfig.json`, `src/App.tsx`, `src/client.tsx`, `src/components/{Blocks,Diagram,Shot,Prs,Rail,Masthead,Overlays}.tsx`, `src/lib/*` | app-core worker | overlays = RefPop, RefPanel, HsPop, Lightbox; delegation lives in App |
| `src/components/SchemaDrawer.tsx`, `src/schema.css`, `harness/**` | schema worker (D7) | the drawer; harness renders it standalone for screenshots |
| `../templates/tdd-system/tdd.css` | app-core worker | small additive fixes only; the generated FONTS/TOKENS blocks are never hand-edited |
| `../scripts/write_agents.py`, `../scripts/strip_page.py`, `../references/agents-lane.md`, `../templates/agents/**` | agents-lane worker (D6, D8) | reads content.json and the built page; writes `agents/` |
| `../scripts/lint_packet.py`, `../references/validators.md` | lint worker | reads content.json, content.files.json, the packet on disk, git |
| `SKILL.md`, `../templates/tdd-template.md`, `../templates/meta.template.json`, `../scripts/new_packet.py`, `../references/{evidence-rules,diagram-conventions,mock-conventions,publish-and-mirror}.md`, `../templates/tdd-system/README.md` | skill-docs worker | documents the CLIs as declared here and in each script's docstring |
Nobody edits another owner's files; a needed change in another scope is written into the return as a request.

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
```
Screenshots: playwright-core is installed under `~/.local/share/system-design-skills/node_modules`; the helper `<scratchpad>/shoot.mjs (a playwright-core screenshot helper; any equivalent works) <file.html> <out-prefix> [width]` writes `<prefix>-top.png` and `<prefix>-full.png`. Reference screenshots to match: `~/.system-design/house/system-design/tdd-system/shots/reference-1440-*.png`.

## Rules for every worker
- Commit nothing, push nothing, switch no branch. `~/.claude/skills` has other sessions' uncommitted edits: do not touch files outside your scope.
- Never invent a measurement, a screenshot, a count or a test result. Unverified claims are marked unverified in the return.
- Typed return at the end: `DONE | DONE_WITH_CONCERNS | NEEDS_CONTEXT | BLOCKED | NEEDS_HUMAN`, with the files written, the proof output (counts, not prose), and open requests for other owners.
