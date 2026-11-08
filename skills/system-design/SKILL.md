---
name: system-design
description: "Use when asked to write a system design for a project or ticket: an engineering page (schema, PR order, risks), a shareable TDD and eng-review deck, a product and design page, diagrams, mockups and one running record of the owner's decisions. Writes a packet folder. Not for reviewing an existing design (archReview), one spec doc (officeHours) or a bug (rootCause)."
argument-hint: "[ticket-or-project] [--personas engineering,product-design,...]"
allowed-tools: Bash(git *), Bash(mkdir *), Bash(SHARED_DIR=*), Bash(source *), Bash(python3 *), Bash(node *), Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion, Agent
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

**Writing rules:** write every doc, spec and PR body this skill makes in the toolkit's plain style, `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/config/packs/style/plain/voice.md`: short sentences, one term per meaning, steps as numbered lists, and a tldraw diagram where a picture explains it better.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing. Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble-extended.md` (component inventory, pipeline overview).

# System Design

One run turns a project into a status doc (the source of truth), one page per persona, a shareable TDD and the deck that presents it, mockups, and diagrams. Pages are plain HTML so they can be shared without a build step.

**Prerequisites (checked in Diagrams and Style and build, not before):** `python3` (stdlib only); Node 20+; the tldraw offline app running (diagrams); Playwright installed in some directory's `node_modules` (page verification, passed as `--playwright-root`). If one is missing, say which step it blocks and **Ask the user**; do not skip the step. A missing `design-tokens.json` is not a prerequisite: the pages fall back to Sindri's tokens (Style).

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"          # sets REPO_SLUG, AW_DIR
S="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/system-design"
SD="$AW_DIR/system-design/<slug>"          # one folder per design = the packet (layout below)
mkdir -p "$SD"/{prs,diagrams,mockups,shots,share}
```

If the user names another deliverables folder (a notes vault, a docs dir), use it for `$SD` instead. Never write deliverables into the code repo unless asked.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Inputs

- **Subject:** a ticket or project from the project's tracker, plus any brief. Read the ticket's decisions and the owner's replies first; never re-ask what they already answered. Reach the tracker through whatever the project provides (an MCP server or CLI named in `AGENTS.md`); if there is none, ask the user to paste the ticket.
- **Code and surfaces:** Sindri context when the `sindri` MCP server is registered for the repo (`mcp: sindri/context_lenses`, `context_query`, `context_neighbors`, line runs) plus the repo's `AGENTS.md`. Sindri output is a lead, not a verdict: reconcile it against a hand map of readers, writers and screens, read from the remote default branch (`git fetch`, then `git show origin/<default>:<path>`), never a stale checkout.
- **Data:** read-only counts from whatever read access the project documents (`AGENTS.md` or its rules). Writes are out of scope. If none is documented, say so in the status doc and ask the user rather than guessing sizes.
- **Style:** the repo's `design-tokens.json` and `.impeccable.md` from `/design-language` (see `_shared/design-artifact-paths.md`), or the tokens file the repo's `AGENTS.md` points to. They layer on top of the default look, Sindri's dark tokens in `$S/assets/sindri-tokens.json`. With no tokens file in the repo, use the Sindri tokens and carry on; do not stop to run `/design-language`, and the "No design language found" stop in the design preamble does not apply to this skill.

## The packet

`$SD` is a fixed layout. `/system-design-video` reads it directly, so keep these names:

```
$SD/
  packet.json            manifest: slug, title, personas -> page, diagrams -> mmd/svg, sections in TDD order with anchors, prs
  doc.md                 the design-doc convention, generated from packet.json by write_packet.py (sections, Ph<n> per PR, REQ-<n> per EARS criterion, TP-<n> per PR test row)
  tdd.html               engineering page; PR cards + deep-dive dialog at its PR-order section
  share/story.json       the shareable TDD's story: the one source of tdd-share.html and deck.html
  tdd-share.html         shareable TDD: the pre-read engineers get as a link (build_share.py)
  deck.html              eng-review deck: the same story as slides, presented live (build_share.py)
  product-design.html    product + design page (or <persona>.html, one per extra persona)
  status.md              the one living doc
  diagrams/<key>.mmd     mermaid source, one per diagram (+ colors.json, optional)
  diagrams/<key>.svg     drawn by draw_diagrams.mjs from the .mmd
  prs/pr-NN.md           one deep dive per PR (shape: templates/pr-cards.md)
  mockups/ shots/        working files
```

`packet.json` and `doc.md` are written by `scripts/write_packet.py` from what is on disk (step 12); never hand-edit them.

## Personas

One page per persona, each answering that persona's question. Default two; add more by asking who turns the feature on, acts on it, or is told about it, then merge pages that would say the same thing.

| Persona | Page | Question it answers | Shape |
|---|---|---|---|
| Engineering | `tdd.html` | Is this buildable and safe to ship in order? | Open questions; product decisions; design needed; formula or rules; schema with example rows; services (repo / facade, read -> plan -> publish where the project uses that split); architecture; blast radius; PR order; gotchas; scope; failure modes; rollout; risks. One card per PR opens a deep dive (code changes as `path:line`, Sindri graph context, screenshots of the pages it touches). Diagrams as linked figures. |
| Engineering, shared | `tdd-share.html` + `deck.html` | What is this design, and what do you need from my review? | Built from `share/story.json` (shape: `templates/share-story.md`). A cold reader's story: hero with stats, the problem today, five whys with `path:line` evidence, the fix (thesis, 2-4 pieces, not building / building instead), a step-through walkthrough, a clickable lifecycle, before/after, the data model (new tables beside the ones they join), diagrams, locked decisions, build slices with done-means and open questions, then the asks. Several parts become chapters of one page. **Current answer only**: no addenda, no superseded items, no change notes. The deck is the same story, ending on the asks. Required on every run. |
| Product + design | `product-design.html` | What are we deciding, and what will it look like? | Designs first (mockups), how the functions work with one numeric example, surfaces-to-design table + gallery of today's screens, product questions with options, decisions list. Self-contained: images inlined. |
| (add: ops, support, ...) | own page | that persona's question | start from `$S/templates/page-skeleton.html` |

## Process

Each step writes to `$SD/status.md`, the one living doc. Sections: surfaces, counts, decisions, scope, TDD notes, tracker. Never start a second status doc.

1. **Surfaces.** Sindri output plus the hand map, reconciled. Record which found what.
2. **Counts.** Read-only queries that size every claim (rows, tenants, cells). Keep the query text next to the doc.
3. **Decisions with the owner.** **Ask the user** one decision at a time: background, the gap, a concrete example with real numbers, options in words. Record the answer in their words under the next D-number (re-read the file for the max first; sessions add concurrently). Stop asking if they reject the question.
4. **Scope.** Acceptance criteria in EARS form. Mark "waiting on approval" until approved.
5. **TDD.** Engineering page from the approved scope; PR cards last. Write one `prs/pr-NN.md` per PR (summary, `path:line | change` table, Sindri graph context, tests; `templates/pr-cards.md`), put `<!-- PR-CARDS -->` in the PR-order section and run `python3 -I "$S/scripts/build_prs.py" "$SD/tdd.html" "$SD/prs" [--shots shots.json]`. It writes milestone-grouped cards (number, title, one-line change, rollback box, "Open the deep dive") and one shared dialog; click a card in the rendered page to confirm it opens.
6. **Shareable TDD and deck.** Once the TDD is approved, write `$SD/share/story.json` from `templates/share-story.md` (example: `templates/share-story.example.json`) and run `python3 -I "$S/scripts/build_share.py" "$SD"`. Distil, don't copy: the reader is an engineer outside the work, reading cold before an eng review. Fold every later round (review fixes, re-scopes, owner feedback) into the current answer; the story never says what changed or when, `status.md` does. A design with several parts is one story with a chapter per part. Put what the review must decide in `asks`, with your recommendation in its `notes`. `--check` validates it now; the pages are built in the style pass, once the diagrams and `doc-style.css` exist. Rebuild after any change to the TDD or the decisions, so the pages never lag the design.
7. **Product + design page.** Mockups first.
8. **Mockups.** Build only from the project's real components and tokens (**Invoke skill** `design-mockup-web` for the HTML). Render each beside a real screenshot of the same screen and compare before showing anyone. Reuse filters and creation flows that already exist; never re-mock them.
9. **Diagrams.** Next section.
10. **Style pass.** **Invoke skill** `design-refine` on each hand-written page (skip it when the repo has no tokens file or `.impeccable.md`; the Sindri default needs no refine pass), then re-apply the style and build the shareable TDD and deck (below). Their look lives in `templates/share-base.css` and `deck-base.css`; never hand-edit the generated files, they are rewritten on every build.
11. **Verify.** Every page rendered (the deck included: `verify_page.mjs` also fails a slide whose content overflows its 16:9 frame; open slides of each kind and read them), 0 broken images, no horizontal scroll at 390px, every screenshot opened and read by you. The script gives metrics; the metrics are not the review.
12. **Packet.** `python3 -I "$S/scripts/write_packet.py" "$SD" --slug <slug> --title "<title>" [--repo "$(gh repo view --json nameWithOwner -q .nameWithOwner)"]` after the last page and diagram change (omit `--repo` when that `gh` call fails); open `packet.json` and check every path exists. It also writes `doc.md`, the design-doc convention (`docs/conventions/design-docs.md`): generated from `packet.json`, so hand edits are lost on the next run and show as `SND-LIB-017`. EARS criteria are read from `tdd.html` (a list item or table cell that starts with When, While, Where, If or The and holds "shall"), test rows from each card's `## Tests` list or table. Its id is `<slug>-tdd`. Then run "The check" from `_shared/design-doc-front-matter.md` with `OUT="$SD/doc.md"`; an error there is fixed in the packet (or the card), then rerun this step.
13. **Tracker last.** Create issues only after the owner approves scope and pages, in whatever state the project's tracker uses for "not started".

## Diagrams

Uses the tldraw offline app (`./setup.sh` installs it; the app's Develop -> Install Agent Skills step adds the `tldraw-offline` skill). The app must be running, so its `server.json` exists. `draw_diagrams.mjs` looks for it at `~/Library/Application Support/tldraw/server.json` on macOS and `${XDG_CONFIG_HOME:-~/.config}/tldraw/server.json` elsewhere; set `TLDRAW_SERVER_JSON` if yours is somewhere else.

1. One mermaid file per diagram in `$SD/diagrams/` (`flowchart TB|LR`, `stateDiagram-v2`). Prefer top-to-bottom when a graph is wide. Optional `colors.json`: `{ "<diagram>": [["node text substring", "blue"], ...] }`, first match wins, default grey. Roles: blue = new or primary, violet = flagged or secondary, green = outcome, red = alert, grey = existing context.
2. Draw:

```bash
node "$S/scripts/draw_diagrams.mjs" --spec "$SD/diagrams" --out "$SD/diagrams" --name "<slug>-diagrams" [--only key] [--theme dark|light]
```

Diagrams are exported dark by default (`--theme light` for the old look): the SVG comes from tldraw's `darkMode` export option, and the colors tldraw reports for each role are swapped for Sindri's, so they sit on the dark pages. It creates your own tldraw doc (`POST /api/docs/create`; never edit another session's doc), one page per diagram, clears and redraws on rerun, forces sans font, solid strokes, semi fill and a transparent background, runs the canvas lints, saves, and writes `<key>.svg`. Fix reported lints (overlapping text) by shortening labels or splitting the diagram, then rerun.
3. Embed as `<figure class="shot diagram"><img src="diagrams/<key>.svg" alt="..."></figure>`: linked in the TDD, inlined in the shareable page.
4. Render each SVG to an image and read it. A diagram nobody looked at is not done.

## Style and build

```bash
TOKENS=design-tokens.json; [ -f "$TOKENS" ] || TOKENS="$S/assets/sindri-tokens.json"   # no repo tokens: the Sindri default
python3 -I "$S/scripts/tokens_to_css.py" "$TOKENS" "$SD/doc-style.css"   # prints tokens it could not map
python3 -I "$S/scripts/apply_doc_style.py" "$SD/tdd.html" "$SD/doc-style.css"          # rerunnable
python3 -I "$S/scripts/build_page.py" "$SD/product-design.src.html" "$SD/product-design.html" "$SD/doc-style.css"
python3 -I "$S/scripts/build_share.py" "$SD"                    # tdd-share.html + deck.html from share/story.json; --check validates only
node "$S/scripts/verify_page.mjs" "$SD/tdd-share.html" "$SD/shots" --playwright-root <dir whose node_modules has playwright>
node "$S/scripts/verify_page.mjs" "$SD/deck.html" "$SD/shots" --playwright-root <dir whose node_modules has playwright>
node "$S/scripts/verify_page.mjs" "$SD/product-design.html" "$SD/shots" --playwright-root <dir whose node_modules has playwright>
```

- The default look is Sindri's: near-black canvas, Inter and JetBrains Mono, a 12px panel radius, violet for flagged, green for outcome, orange for needs-attention, red for alert. `assets/sindri-tokens.json` holds the values, copied from the Sindri app's theme; the base CSS carries the same values as its fallbacks, so a page looks right even with no tokens step. Pass `$S/assets/sindri-tokens.json` as the tokens file when the repo has none. A repo's own tokens replace Sindri's value by value, so a repo that defines only an accent keeps Sindri's dark canvas.
- `tokens_to_css.py` maps `color.*`, `font.*`, `radius.*` tokens onto the semantic variables in `templates/doc-base.css`. For any it lists as unmapped, the page keeps Sindri's value; ask the user only if that value would clash with their brand. Do not invent brand colors.
- Exit codes: the Python scripts exit 0 on success and 1 with a message on bad arguments, a missing image, a missing style marker or unmatched markers; `draw_diagrams.mjs` exits 0 when every diagram is drawn, 2 on bad arguments, 1 on an API error; `verify_page.mjs` exits 0 clean, 1 on a broken image or horizontal scroll, 2 on bad arguments.
- `build_share.py` exits 0 when both pages are written and 1 with every problem listed: a missing field, a list or text over its limit, a history marker, a missing image. It layers the project tokens from `$SD/doc-style.css` (or `--style`) on `templates/share-base.css` (+ `deck-base.css` for the deck), so run `tokens_to_css.py` first. The deck: arrow keys or space to move, `N` speaker notes, `P` presenter window (current, next, notes, timer), `F` full screen, `#N` deep-links a slide, print gives one slide per page.
- `build_page.py` inlines every `<img>` as a data URI and fails on a missing image. `verify_page.mjs` exits 1 on a broken image or horizontal scroll at 1280 or 390px.
- Start new pages from `templates/page-skeleton.html`. A generic sketch of a full run is in `references/example-run.md`.

## Rules

- Block on product questions; never carry on with a guessed default. Only the owner's words get a D-number; your defaults and Sindri's picks are labeled as such.
- Ask only what changes the build. Answer speculative edge cases yourself from the code and the TDD's risks.
- Schema: foreign-key columns with CHECK constraints over join tables or synced subject keys; show example rows.
- Reuse existing filters, creation flows and screens; showing a design is not new UI.
- Deliverables live in `$SD`; one status doc; no `-handoff-N` copies.
- The shareable TDD and deck carry the current design only. A later round rewrites the story; it never appends an addendum to it.
- No deadline or estimate questions; size stages by dependency only.
- Don't design recovery for states the work itself makes impossible.

## Next steps

- `/autoplan` on the approved scope, for the product / architecture / design / devex / security lenses in parallel.
- `/system-design-video <packet dir>` for the whiteboard film of this packet: its boards are laid out from `diagrams/*.mmd`, its beats cite `packet.json` sections.
- `/specToProvenPR` to turn the approved TDD's PR order into proven PRs.
- `/design-verify` once a mockup has become a real screen.

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/system-design-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`system-design`), `artifacts` (absolute paths: the packet under `$AW_DIR/system-design/<slug>/` (TDD, `tdd-share.html`, `deck.html`, persona pages, `packet.json`)), `summary` (one line),
plus `slug` (design slug); `packet_dir` (absolute path of the packet directory).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
