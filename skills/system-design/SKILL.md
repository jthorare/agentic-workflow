---
name: system-design
description: "Produce a system design for a project or ticket as persona pages - an engineering TDD and a shareable product + design page - written as a markdown source, built with the house doc design system and published as claude.ai HTML artifacts (the source of truth), backed by one living status doc, owner decisions recorded one at a time, mockups from the project's real components, and tldraw diagrams styled from the project's design tokens. Use for 'write a system design', 'write a TDD / design doc / spec', 'TDD plus product/design pages', adding a persona page, or re-drawing/re-styling the diagrams. Not a bug investigation or a PR review."
argument-hint: "[ticket-or-project] [--personas engineering,product-design,...]"
allowed-tools: Bash(S=*), Bash(SD=*), Bash(git *), Bash(mkdir *), Bash(source *), Bash(python3 *), Bash(node *), Bash(open *), Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion, Agent
---

<!-- Standalone adaptation of jthorare/agentic-workflow's /system-design: no toolkit install, hooks, prism-mcp or
     sibling skills required. Provenance and the upstream diff: the skills repository README -->

# System Design

One run turns a project into a status doc (the working record), one page per persona, mockups, and diagrams. Each page is a markdown source in the packet, built with the house doc design system (`templates/doc-system/`) and published as a claude.ai HTML artifact with the Artifact tool. **The published artifact is the source of truth and the deliverable** for every TDD and doc. A Claude Doc may mirror it; it never replaces it (a Doc is a fixed 672 px column with no side nav).

**Prerequisites (checked in Diagrams and Style and build, not before):** `python3` (stdlib only); Node 20+; the tldraw offline app running (diagrams); `playwright-core` with its Chromium in `$DESIGN_DEPS` (page verification); the Artifact tool (Publish). `"$S/scripts/doctor.sh"` reports the first four. If one is missing, say which step it blocks and **ask the user** (AskUserQuestion); do not skip the step.

```bash
S="$HOME/.claude/skills/system-design"
source "$S/scripts/env.sh"                 # sets REPO_SLUG, DESIGN_HOME, DESIGN_DEPS
SD="$DESIGN_HOME/system-design/<slug>"     # one folder per design = the packet (layout below)
mkdir -p "$SD"/{prs,diagrams,mockups,shots,video,media} && echo "packet: $SD"
"$S/scripts/doctor.sh"                     # what is ready; each missing piece names the step it blocks
```

**Shell variables do not persist between Bash calls.** Begin every later bash block with the same first three lines (`S=...`, `source ...`, `SD=...` with the slug filled in), or use the absolute packet path the block above printed.

`DESIGN_HOME` is `~/.system-design/<repo-slug>` unless `SYSTEM_DESIGN_OUT` is set. If the user names another deliverables folder (a notes vault, a docs dir), use it for `$SD` instead. Never write deliverables into the code repo unless asked.

## Design context (load before Inputs)

1. Read `.impeccable.md` (brand personality, aesthetic direction, `## Sources`), `design-tokens.json` (W3C DTCG) and `planning/DESIGN_SYSTEM.md` from the repo root, or the tokens file the repo's `AGENTS.md` points to, if they exist. Also check `$DESIGN_HOME/design/design-tokens.json` (where this skill keeps tokens it derived on an earlier run).
2. Component inventory: read `package.json` `dependencies` + `devDependencies` for a component library (`@radix-ui/*`, `shadcn-ui`/`@shadcn/*`, `@headlessui/*`, `react-aria`, `@mui/*`, `@chakra-ui/*`, `@mantine/*`, `antd`, `@nextui-org/*`, `daisyui`; in a monorepo, the UI package's `package.json`), then glob `src/components/**`, `components/**`, `app/components/**`, `ui/src/components/**`, `packages/ui*/src/**` for primitive file names (`card-header.tsx` -> `CardHeader`). Note: "Library: ..., Primitives: ...". Mockups use these before inventing anything.
3. **No tokens file?** Derive one; never invent brand colors. Read the project's real theme (Tailwind `theme.extend.colors` / `fontFamily` / `borderRadius`, CSS custom properties in the global stylesheet, a theme or tokens module), and write a minimal DTCG `$DESIGN_HOME/design/design-tokens.json` with `color.background`, `color.surface`, `color.text`, `color.text-muted`, `color.border`, `color.primary`, `color.secondary`, `color.success`, `color.warning`, `color.danger`, `font.family-body`, `font.family-mono`, `radius.md`, as plain `#rrggbb` (convert `hsl(...)`/`oklch(...)` to sRGB hex) with a `$description` citing the source `path:line` of each. Values you cannot find stay out (the stylesheet keeps a neutral fallback for them). Show the user the mapping and ask them to confirm it before the style pass.
4. **Precedence.** Project tokens (`design-tokens.json`, `.impeccable.md`, or a Design System artifact the owner names) override the doc-system tokens. The doc system (`templates/doc-system/tokens.json`, `doc-system.css`, `README.md`: light + dark tokens, side nav, glance block, module cards, diagram and video figures, galleries, tables, callouts, chips, PR cards) is the fallback palette and the component vocabulary.

## Inputs

- **Subject:** a ticket or project from the project's tracker, plus any brief. Read the ticket's decisions and the owner's replies first; never re-ask what they already answered. Reach the tracker through whatever the project provides (an MCP server such as Linear, or a CLI named in `AGENTS.md`); if there is none, ask the user to paste the ticket.
- **Code and surfaces:** Sindri context when a `sindri` MCP server is registered for the repo (`context_lenses`, `context_query`, `context_neighbors`, line runs); otherwise Grep/Glob and reading the code, plus the repo's `AGENTS.md`. Tool output is a lead, not a verdict: reconcile it against a hand map of readers, writers and screens, read from the remote default branch (`git fetch`, then `git show origin/<default>:<path>`), never a stale checkout. For a wide sweep, an Explore/recon subagent can build the first map.
- **Data:** read-only counts from whatever read access the project documents (`AGENTS.md` or its rules). Writes are out of scope. If none is documented, say so in the status doc and ask the user rather than guessing sizes.
- **Style:** the tokens from Design context above.
- **Media:** game stills, concept and visual design images, recorded dev-proof clips (gameplay, mechanics, systems). Ingest them into the packet's media lane (The packet, below); never hotlink.

## The packet

`$SD` is a fixed layout. `/system-design-video` reads it directly, so keep these names:

```
$SD/
  packet.json            manifest: slug, title, personas -> page, diagrams -> mmd/svg, sections in TDD order with anchors, prs, artifact_url
  <slug>.md              markdown source of the TDD: the editable text of the source of truth
  product-design.md      source of the product + design page (one .md per persona page)
  modules.json           module list: id, title, summary, status, diagram key, clip
  media.json             media lane: stills, concept images, proof clips per module (ingest_media.py writes it)
  diagram-map.json video-map.json meta.json   build_doc.py inputs (shapes: templates/doc-system/README.md)
  tdd.html               engineering page, built from <slug>.md; PR cards + deep-dive dialog at its PR-order section
  product-design.html    product + design page (or <persona>.html, one per extra persona), built from its .md
  <slug>.files.json      publish manifest build_doc.py writes beside the page (<out>.files.json): published path -> source
  status.md              the one living working doc; carries artifact_url once published
  diagrams/<key>.mmd     mermaid source, one per diagram (+ colors.json, optional)
  diagrams/<key>.svg     drawn by draw_diagrams.mjs from the .mmd
  video/<name>.mp4       whiteboard clips the page plays, copied from the film (+ poster stills), each <= 15 MB
  media/                 ingested media files (media.json lists them)
  prs/pr-NN.md           one deep dive per PR (shape: templates/pr-cards.md)
  mockups/ shots/        working files
```

Pages are built from their source, never hand-edited: change the `.md`, a diagram, `modules.json`, `media.json` or a PR deep dive, then rebuild, verify and republish. Packets made before the doc system (a hand-written `tdd.html`, `product-design.src.html`) keep the legacy build in Style and build.

`packet.json` is written by `scripts/write_packet.py` from what is on disk (step 12); never hand-edit it.

**Media lane.** Add each file with `python3 -I "$HOME/.claude/skills/system-design-video/scripts/ingest_media.py" "$SD" --add <file> --kind still|concept|clip --module <section id> --caption "..." --source "..." --rights original|internal-proof|licensed-reference`; `--scan <repo>` lists candidates read-only and copies nothing. It writes `media/` and `media.json`. `licensed-reference` entries inform the design and are never published. Details: `~/.claude/skills/system-design-video/references/media-lane.md`.

## Personas

One page per persona, each answering that persona's question. Default two; add more by asking who turns the feature on, acts on it, or is told about it, then merge pages that would say the same thing.

| Persona | Page | Question it answers | Shape |
|---|---|---|---|
| Engineering | `tdd.html` | Is this buildable and safe to ship in order? | Open questions; product decisions; design needed; formula or rules; schema with example rows; services (repo / facade, read -> plan -> publish where the project uses that split); architecture; blast radius; PR order; gotchas; scope; failure modes; rollout; risks. One card per PR opens a deep dive (code changes as `path:line`, graph context, screenshots of the pages it touches). Opens in the house structure below. Every `##` in the source becomes a `<section id>` with an `<h2>` in the built page; `write_packet.py` reads them. |
| Product + design | `product-design.html` | What are we deciding, and what will it look like? | Designs first (mockups), how the functions work with one numeric example, surfaces-to-design table + gallery of today's screens, product questions with options, decisions list. Images ship with its artifact through `files` (legacy pages inline them). |
| (add: ops, support, ...) | own page | that persona's question | own `.md` source, built and published the same way (legacy: `$S/templates/page-skeleton.html`) |

## House structure (TDDs)

1. **System at a glance** opens the page: the whole-system diagram with every module inside its own boundary, then the whiteboard film's overview clip (if a film exists).
2. **Module grid** next: one card per `modules.json` entry (title, one-line summary, status chip), each linking to its section.
3. **One section per module**, in grid order, each in this order: its own system diagram (directly under the heading), that module's whiteboard clip (if a film exists), its stills and proof clips from `media.json` (a gallery), then prose.

The other engineering sections (the Shape column above) keep the source's section order. No `**Contents**` block: the page has its own side nav, and `build_doc.py` skips one. Source shape (exact glance and grid syntax: `templates/doc-system/README.md`):

```markdown
## Ledger service
[diagram: ledger · What the ledger owns and what it reads]
[video: ledger · The ledger, from write to settled balance]
Prose: the finding first, then the mechanism.
```

## Process

Each step writes to `$SD/status.md`, the one living doc. Sections: surfaces, counts, decisions, scope, TDD notes, tracker. Never start a second status doc.

1. **Surfaces.** Tool output plus the hand map, reconciled. Record which found what.
2. **Counts.** Read-only queries that size every claim (rows, tenants, cells). Keep the query text next to the doc.
3. **Decisions with the owner.** **Ask the user** one decision at a time: background, the gap, a concrete example with real numbers, options in words. Record the answer in their words under the next D-number (re-read the file for the max first; sessions add concurrently). Stop asking if they reject the question.
4. **Scope.** Acceptance criteria in EARS form. Mark "waiting on approval" until approved.
5. **TDD.** Write `$SD/<slug>.md` from the approved scope in the house structure, with `modules.json` beside it; PR cards last. Write one `prs/pr-NN.md` per PR (summary, `path:line | change` table, graph context, tests; `templates/pr-cards.md`), put `<!-- PR-CARDS -->` in the PR-order section of the source, build the page (Style and build), then run `python3 -I "$S/scripts/build_prs.py" "$SD/tdd.html" "$SD/prs" [--shots shots.json]`. It writes milestone-grouped cards (number, title, one-line change, rollback box, "Open the deep dive") and one shared dialog; click a card in the rendered page to confirm it opens.
6. **Product + design page.** Mockups first, in `product-design.md`.
7. **Mockups.** Build each as a static HTML file in `$SD/mockups/` from the project's real components and tokens: copy the real markup and class names of the components it uses (read their source), link or inline the project's compiled CSS where you can, and fall back to the doc-system tokens only for what the project does not style. Render each beside a real screenshot of the same screen and compare before showing anyone. Reuse filters and creation flows that already exist; never re-mock them.
8. **Diagrams.** Next section: the whole-system diagram, then one per module, then one per mechanism.
9. **Style pass.** Critique each page as its reader would: hierarchy (the finding is the heading, not the topic), scannability, spacing rhythm, contrast, and that every figure earns its place (the `design:design-critique` skill helps if it is installed). Fix what you find in the source, then rebuild (below).
10. **Verify.** `artifact_check.mjs` on every built page: 1440 and 390 px, light and dark; no horizontal scroll, no broken media, no invisible text, no console errors. Every screenshot opened and read by you. The script gives metrics; the metrics are not the review.
11. **Publish.** Load the `artifact-design` skill (or use a quickstart result), read the built page in full, then publish `tdd.html` with the Artifact tool: `files` from `<slug>.files.json` (`diagrams/*.svg`, images, `media/*`, `video/*.mp4`, each binary <= 15 MB), `icon` on the first publish only, `description` one sentence. Redeploy with the same `file_path` in this session or with `url` from a later one. Write `artifact_url: <url>` near the top of `status.md`. Publish each other persona page as its own artifact the same way and list its URL under that line. The artifact starts private; sharing it is the owner's call. How-to and checklist: `references/artifact-publish.md`.
12. **Packet.** `python3 -I "$S/scripts/write_packet.py" "$SD" --slug <slug> --title "<title>"` after the last page, diagram and publish change; it takes `artifact_url` from `status.md` (or a flag), if supported. If yours does not write `artifact_url` yet, add that one key to `packet.json` by hand and say so in `status.md`. Open `packet.json` and check every path exists.
13. **Tracker last.** Create issues only after the owner approves scope and pages, in whatever state the project's tracker uses for "not started".

## Diagrams

Uses the tldraw offline app (macOS: `brew install --cask tldraw`; launch it with `open -a "tldraw offline"`). Only the app has to be running so its `server.json` exists; its optional Develop -> Install Agent Skills step is **not** needed (it also installs a SubagentStart hook, so leave it off). `draw_diagrams.mjs` looks for `server.json` at `~/Library/Application Support/tldraw/server.json` on macOS and `${XDG_CONFIG_HOME:-~/.config}/tldraw/server.json` elsewhere; set `TLDRAW_SERVER_JSON` if yours is somewhere else.

1. One mermaid file per diagram in `$SD/diagrams/` (`flowchart TB|LR`, `stateDiagram-v2`). Prefer top-to-bottom when a graph is wide. Optional `colors.json`: `{ "<diagram>": [["node text substring", "blue"], ...] }`, first match wins, default grey. Roles: blue = new or primary, violet = flagged or secondary, green = outcome, red = alert, grey = existing context.
2. Draw:

```bash
node "$S/scripts/draw_diagrams.mjs" --spec "$SD/diagrams" --out "$SD/diagrams" --name "<slug>-diagrams" [--only key]
```

It creates your own tldraw doc (`POST /api/docs/create`; never edit another session's doc), one page per diagram, clears and redraws on rerun, forces sans font, solid strokes, semi fill and a transparent background, runs the canvas lints, saves, and writes `<key>.svg`. Fix reported lints (overlapping text) by shortening labels or splitting the diagram, then rerun. It reuses the doc only when its `.tldraw` file is still in `$SD/diagrams`; if that file was deleted while the app had the doc open (every exec would hang on save), it re-creates the doc at the same path, which re-attaches the window.
   Read each drawing before embedding it: `node "$S/scripts/svg_to_png.mjs" "$SD/diagrams/<key>.svg" "$SD/shots/diagram-<key>.png"`, then Read the PNG (the Read tool shows PNG, not SVG). Check that every node and label from the `.mmd` is there and that no arrow crosses a label.
3. Embed in the source as `[diagram: <key> · <caption>]` (a film clip: `[video: <name> · <caption>]`); `build_doc.py` renders the figure. The first diagram in a section renders directly under its heading. Legacy hand-written pages embed `<figure class="shot diagram"><img src="diagrams/<key>.svg" alt="..."></figure>`.
4. Render each SVG to an image and read it. A diagram nobody looked at is not done.

## Style and build

```bash
python3 -I "$S/scripts/build_doc.py" "$SD/<slug>.md" "$SD/tdd.html" --diagrams "$SD/diagrams" \
  --diagram-map "$SD/diagram-map.json" --video-map "$SD/video-map.json" --media "$SD/media.json" \
  --css "$S/templates/doc-system/doc-system.css" --meta "$SD/meta.json"   # also writes the .files.json manifest
python3 -I "$S/scripts/build_prs.py" "$SD/tdd.html" "$SD/prs" [--shots shots.json]
node "$S/scripts/artifact_check.mjs" "$SD/tdd.html" "$SD/shots"          # 1440/390 x light/dark render check
```

- `build_doc.py` writes page content for the Artifact tool (no `<html>`/`<head>`/`<body>`; `<title>` first) plus the manifest of every svg/png/mp4 it references. `--media` renders a `.gallery` under each module's diagram and whiteboard clip and leaves `licensed-reference` entries out. Build `product-design.html` from `product-design.md` the same way.
- Project tokens: `python3 -I "$S/scripts/tokens_to_css.py" <design-tokens.json> "$SD/doc-style.css"` prints any token it could not map; ask the user for that value or fix the tokens file; do not invent brand colors. Layer the result over `doc-system.css` the way `templates/doc-system/README.md` says, so project tokens win.
- `artifact_check.mjs` exits 1 on horizontal scroll, broken media, invisible text or a console error. Fix the source, rebuild, rerun.
- Legacy packets (hand-written `tdd.html`, `product-design.src.html`) keep the old build:

```bash
python3 -I "$S/scripts/apply_doc_style.py" "$SD/tdd.html" "$SD/doc-style.css"          # rerunnable
python3 -I "$S/scripts/build_page.py" "$SD/product-design.src.html" "$SD/product-design.html" "$SD/doc-style.css"
node "$S/scripts/verify_page.mjs" "$SD/product-design.html" "$SD/shots"              # playwright from $SYSTEM_DESIGN_DEPS
```

- `tokens_to_css.py` maps `color.*`, `font.*`, `radius.*` tokens onto the semantic variables in `templates/doc-base.css`.
- Exit codes: the Python scripts exit 0 on success and 1 with a message on bad arguments, a missing image, a missing style marker or unmatched markers; `draw_diagrams.mjs` exits 0 when every diagram is drawn, 2 on bad arguments, 1 on an API error; `verify_page.mjs` exits 0 clean, 1 on a broken image or horizontal scroll, 2 on bad arguments, no Playwright or a Chromium that does not launch (run `doctor.sh`).
- `build_page.py` inlines every `<img>` as a data URI and fails on a missing image (data URIs count toward the artifact's 16 MB page limit). `verify_page.mjs` exits 1 on a broken image or horizontal scroll at 1280 or 390px; it finds Playwright via `$PLAYWRIGHT_CORE`, then `--playwright-root <dir>`, else `$SYSTEM_DESIGN_DEPS` (default `~/.local/share/system-design-skills`, what `env.sh` exports as `$DESIGN_DEPS`), else the working directory.
- A generic sketch of a full run is in `references/example-run.md`; publishing is in `references/artifact-publish.md`.

## Claude Doc (optional mirror)

Only when the owner asks for one. A Claude Doc (Claude Docs connector) is a fixed 672 px column with no side nav, so it is never the source of truth. It mirrors `<slug>.md`, its first line links to the artifact, and it is refreshed from the source after each republish, never edited on its own. Record its URL in `status.md` under `artifact_url`. If a quickstart offers a document type for a TDD, publish the HTML artifact anyway; a doc is only the mirror.

## Rules

- Block on product questions; never carry on with a guessed default. Only the owner's words get a D-number; your defaults and tool picks are labeled as such.
- Ask only what changes the build. Answer speculative edge cases yourself from the code and the TDD's risks.
- Schema: foreign-key columns with CHECK constraints over join tables or synced subject keys; show example rows.
- Reuse existing filters, creation flows and screens; showing a design is not new UI.
- The published artifact is the source of truth. Edit `<slug>.md` (or its inputs), rebuild, republish; never hand-edit a built page.
- Deliverables live in `$SD` and in the artifact built from it; one status doc; no `-handoff-N` copies.
- No deadline or estimate questions; size stages by dependency only.
- Don't design recovery for states the work itself makes impossible.
- Finish by listing the artifact URL (and any other persona page's URL), then the packet's paths (`<slug>.md`, pages, `status.md`, `packet.json`) so the user can open them.

## Next steps

- `/system-design-video <packet dir>` for the whiteboard film of this packet: overview first, then one scene per module; its boards are laid out from `diagrams/*.mmd`, its beats cite `packet.json` sections. Copy its overview and per-module clips (and posters) into `video/`, rebuild, republish.
- `/engineering:architecture` to record a contested choice from the TDD as an ADR.
- `/code-review` on each PR as the TDD's PR order is built.
