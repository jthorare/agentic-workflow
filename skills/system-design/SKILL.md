---
name: system-design
description: "Produce a system design for a project or ticket as persona pages - an engineering TDD and a shareable product + design page - backed by one living status doc, owner decisions recorded one at a time, mockups from the project's real components, and tldraw diagrams styled from the project's design tokens. Use for 'write a system design', 'TDD plus product/design pages', adding a persona page, or re-drawing/re-styling the diagrams. Not a single spec doc (/officeHours), a bug (/rootCause), or a PR plan review (/autoplan)."
argument-hint: "[ticket-or-project] [--personas engineering,product-design,...]"
allowed-tools: Bash(git *), Bash(mkdir *), Bash(SHARED_DIR=*), Bash(source *), Bash(python3 *), Bash(node *), Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion, Agent
---

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `$HOME/.agentic-workflow/toolkit/skills/_design-preamble.md` and follow it before continuing.

# System Design

One run turns a project into a status doc (the source of truth), one page per persona, mockups, and diagrams. Pages are plain HTML so they can be shared without a build step.

**Prerequisites (checked in Diagrams and Style and build, not before):** `python3` (stdlib only); Node 20+; the tldraw offline app running (diagrams); Playwright installed in some directory's `node_modules` (page verification, passed as `--playwright-root`). If one is missing, say which step it blocks and **Ask the user**; do not skip the step.

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"          # sets REPO_SLUG, AW_DIR
S="$HOME/.agentic-workflow/toolkit/skills/system-design"
SD="$AW_DIR/system-design/<slug>"          # one folder per design = the packet (layout below)
mkdir -p "$SD"/{prs,diagrams,mockups,shots}
```

If the user names another deliverables folder (a notes vault, a docs dir), use it for `$SD` instead. Never write deliverables into the code repo unless asked.

## Inputs

- **Subject:** a ticket or project from the project's tracker, plus any brief. Read the ticket's decisions and the owner's replies first; never re-ask what they already answered. Reach the tracker through whatever the project provides (an MCP server or CLI named in `AGENTS.md`); if there is none, ask the user to paste the ticket.
- **Code and surfaces:** Sindri context when the `sindri` MCP server is registered for the repo (`mcp: sindri/context_lenses`, `context_query`, `context_neighbors`, line runs) plus the repo's `AGENTS.md`. Sindri output is a lead, not a verdict: reconcile it against a hand map of readers, writers and screens, read from the remote default branch (`git fetch`, then `git show origin/<default>:<path>`), never a stale checkout.
- **Data:** read-only counts from whatever read access the project documents (`AGENTS.md` or its rules). Writes are out of scope. If none is documented, say so in the status doc and ask the user rather than guessing sizes.
- **Style:** `design-tokens.json` and `.impeccable.md` from `/design-language` (see `_shared/design-artifact-paths.md`), or the tokens file the repo's `AGENTS.md` points to. If neither exists, run **Invoke skill** `design-language` first.

## The packet

`$SD` is a fixed layout. `/system-design-video` reads it directly, so keep these names:

```
$SD/
  packet.json            manifest: slug, title, personas -> page, diagrams -> mmd/svg, sections in TDD order with anchors, prs
  tdd.html               engineering page; PR cards + deep-dive dialog at its PR-order section
  product-design.html    product + design page (or <persona>.html, one per extra persona)
  status.md              the one living doc
  diagrams/<key>.mmd     mermaid source, one per diagram (+ colors.json, optional)
  diagrams/<key>.svg     drawn by draw_diagrams.mjs from the .mmd
  prs/pr-NN.md           one deep dive per PR (shape: templates/pr-cards.md)
  mockups/ shots/        working files
```

`packet.json` is written by `scripts/write_packet.py` from what is on disk (step 11); never hand-edit it.

## Personas

One page per persona, each answering that persona's question. Default two; add more by asking who turns the feature on, acts on it, or is told about it, then merge pages that would say the same thing.

| Persona | Page | Question it answers | Shape |
|---|---|---|---|
| Engineering | `tdd.html` | Is this buildable and safe to ship in order? | Open questions; product decisions; design needed; formula or rules; schema with example rows; services (repo / facade, read -> plan -> publish where the project uses that split); architecture; blast radius; PR order; gotchas; scope; failure modes; rollout; risks. One card per PR opens a deep dive (code changes as `path:line`, Sindri graph context, screenshots of the pages it touches). Diagrams as linked figures. |
| Product + design | `product-design.html` | What are we deciding, and what will it look like? | Designs first (mockups), how the functions work with one numeric example, surfaces-to-design table + gallery of today's screens, product questions with options, decisions list. Self-contained: images inlined. |
| (add: ops, support, ...) | own page | that persona's question | start from `$S/templates/page-skeleton.html` |

## Process

Each step writes to `$SD/status.md`, the one living doc. Sections: surfaces, counts, decisions, scope, TDD notes, tracker. Never start a second status doc.

1. **Surfaces.** Sindri output plus the hand map, reconciled. Record which found what.
2. **Counts.** Read-only queries that size every claim (rows, tenants, cells). Keep the query text next to the doc.
3. **Decisions with the owner.** **Ask the user** one decision at a time: background, the gap, a concrete example with real numbers, options in words. Record the answer in their words under the next D-number (re-read the file for the max first; sessions add concurrently). Stop asking if they reject the question.
4. **Scope.** Acceptance criteria in EARS form. Mark "waiting on approval" until approved.
5. **TDD.** Engineering page from the approved scope; PR cards last. Write one `prs/pr-NN.md` per PR (summary, `path:line | change` table, Sindri graph context, tests; `templates/pr-cards.md`), put `<!-- PR-CARDS -->` in the PR-order section and run `python3 -I "$S/scripts/build_prs.py" "$SD/tdd.html" "$SD/prs" [--shots shots.json]`. It writes milestone-grouped cards (number, title, one-line change, rollback box, "Open the deep dive") and one shared dialog; click a card in the rendered page to confirm it opens.
6. **Product + design page.** Mockups first.
7. **Mockups.** Build only from the project's real components and tokens (**Invoke skill** `design-mockup-web` for the HTML). Render each beside a real screenshot of the same screen and compare before showing anyone. Reuse filters and creation flows that already exist; never re-mock them.
8. **Diagrams.** Next section.
9. **Style pass.** **Invoke skill** `design-refine` on each page, then re-apply the style (below).
10. **Verify.** Every page rendered, 0 broken images, no horizontal scroll at 390px, every screenshot opened and read by you. The script gives metrics; the metrics are not the review.
11. **Packet.** `python3 -I "$S/scripts/write_packet.py" "$SD" --slug <slug> --title "<title>"` after the last page and diagram change; open `packet.json` and check every path exists.
12. **Tracker last.** Create issues only after the owner approves scope and pages, in whatever state the project's tracker uses for "not started".

## Diagrams

Uses the tldraw offline app (`./setup.sh` installs it; the app's Develop -> Install Agent Skills step adds the `tldraw-offline` skill). The app must be running, so its `server.json` exists. `draw_diagrams.mjs` looks for it at `~/Library/Application Support/tldraw/server.json` on macOS and `${XDG_CONFIG_HOME:-~/.config}/tldraw/server.json` elsewhere; set `TLDRAW_SERVER_JSON` if yours is somewhere else.

1. One mermaid file per diagram in `$SD/diagrams/` (`flowchart TB|LR`, `stateDiagram-v2`). Prefer top-to-bottom when a graph is wide. Optional `colors.json`: `{ "<diagram>": [["node text substring", "blue"], ...] }`, first match wins, default grey. Roles: blue = new or primary, violet = flagged or secondary, green = outcome, red = alert, grey = existing context.
2. Draw:

```bash
node "$S/scripts/draw_diagrams.mjs" --spec "$SD/diagrams" --out "$SD/diagrams" --name "<slug>-diagrams" [--only key]
```

It creates your own tldraw doc (`POST /api/docs/create`; never edit another session's doc), one page per diagram, clears and redraws on rerun, forces sans font, solid strokes, semi fill and a transparent background, runs the canvas lints, saves, and writes `<key>.svg`. Fix reported lints (overlapping text) by shortening labels or splitting the diagram, then rerun.
3. Embed as `<figure class="shot diagram"><img src="diagrams/<key>.svg" alt="..."></figure>`: linked in the TDD, inlined in the shareable page.
4. Render each SVG to an image and read it. A diagram nobody looked at is not done.

## Style and build

```bash
python3 -I "$S/scripts/tokens_to_css.py" design-tokens.json "$SD/doc-style.css"   # prints tokens it could not map
python3 -I "$S/scripts/apply_doc_style.py" "$SD/tdd.html" "$SD/doc-style.css"          # rerunnable
python3 -I "$S/scripts/build_page.py" "$SD/product-design.src.html" "$SD/product-design.html" "$SD/doc-style.css"
node "$S/scripts/verify_page.mjs" "$SD/product-design.html" "$SD/shots" --playwright-root <dir whose node_modules has playwright>
```

- `tokens_to_css.py` maps `color.*`, `font.*`, `radius.*` tokens onto the semantic variables in `templates/doc-base.css`. For any it lists as unmapped, ask the user for the value or fix `design-tokens.json`; do not invent brand colors.
- Exit codes: the Python scripts exit 0 on success and 1 with a message on bad arguments, a missing image, a missing style marker or unmatched markers; `draw_diagrams.mjs` exits 0 when every diagram is drawn, 2 on bad arguments, 1 on an API error; `verify_page.mjs` exits 0 clean, 1 on a broken image or horizontal scroll, 2 on bad arguments.
- `build_page.py` inlines every `<img>` as a data URI and fails on a missing image. `verify_page.mjs` exits 1 on a broken image or horizontal scroll at 1280 or 390px.
- Start new pages from `templates/page-skeleton.html`. A generic sketch of a full run is in `references/example-run.md`.

## Rules

- Block on product questions; never carry on with a guessed default. Only the owner's words get a D-number; your defaults and Sindri's picks are labeled as such.
- Ask only what changes the build. Answer speculative edge cases yourself from the code and the TDD's risks.
- Schema: foreign-key columns with CHECK constraints over join tables or synced subject keys; show example rows.
- Reuse existing filters, creation flows and screens; showing a design is not new UI.
- Deliverables live in `$SD`; one status doc; no `-handoff-N` copies.
- No deadline or estimate questions; size stages by dependency only.
- Don't design recovery for states the work itself makes impossible.

## Next steps

- `/autoplan` on the approved scope, for the product / architecture / design / devex / security lenses in parallel.
- `/system-design-video <packet dir>` for the whiteboard film of this packet: its boards are laid out from `diagrams/*.mmd`, its beats cite `packet.json` sections.
- `/specToProvenPR` to turn the approved TDD's PR order into proven PRs.
- `/design-verify` once a mockup has become a real screen.
