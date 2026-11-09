---
name: tdd
description: "Write a technical design document (TDD) in the house template: a markdown source compiled to content.json and rendered by a React app into one claude.ai HTML artifact (the source of truth), with mocks from the project's real components, tldraw diagrams with hover hotspots, a schema lane with sample rows and measured load, an agents/ lane for subagents, a linter, one living status doc, and owner decisions recorded one at a time. Use for 'write a TDD', 'technical design doc', 'design doc for <ticket>', 'rebuild this TDD in the house template'. Not a bug investigation or a PR review."
argument-hint: "<slug> [--title T] [--ticket P-XXX-NNN] [--repo <path>]"
allowed-tools: Bash(T=*), Bash(S=*), Bash(SD=*), Bash(SHARED_DIR=*), Bash(source *), Bash(git *), Bash(python3 *), Bash(node *), Bash(bun *), Bash(cd *), Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion, Agent
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

# TDD (house template)

One run turns a ticket into a **packet**: `<slug>.md` (the editable source), `meta.json`, diagrams, mocks, PR deep dives, schema cards, one `status.md`, and the built page `build/tdd.html`, published as a private claude.ai artifact. **The published artifact is the source of truth**; Notion mirrors it. This skill sits beside `system-design` (D1 of the tdd-system packet) and uses system-design's scripts for drawing, checking and the repo slug, from its own copy in `vendor/system-design/`; it never edits `skills/system-design`.

```bash
T="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/tdd"   # this skill's folder
S="${SYSTEM_DESIGN_SKILL:-$T/vendor/system-design}"              # the system-design scripts the skill resolves (see sd_root in scripts/build_tdd.py)
SD="<the packet path new_packet.py printed>"     # shell state does not persist: repeat these lines in every block
```

`T` is this skill's folder. If that path does not exist (the skill was linked into a provider's skills dir without the toolkit, for example a symlink at `~/.claude/skills/tdd`), set `T` to the folder that holds this SKILL.md. `S` is the system-design scripts the skill resolves (see `sd_root` in `scripts/build_tdd.py`): `$SYSTEM_DESIGN_SKILL` when it is set, else the copy in `vendor/system-design/` (its README says where it came from). The repo's own `skills/system-design` is a different variant (no `build_doc.py`, `artifact_check.mjs`, `doctor.sh` or `svg_to_png.mjs`, and its `draw_diagrams.mjs` draws dark by default), so never point `S` at it.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Rules

- **Deliverables are files** in the packet; chat carries the artifact URL, the packet paths and a short summary, never the page or a long diff.
- **Never fabricate** a screenshot, a count, a measurement, a test result, a date or a quote. A number has a source and a date or reads "not measured" (`references/evidence-rules.md`).
- **Every reference token links.** D2, Q1, R3, E1, F3.2, PR 7, OS-01, and every glossary abbreviation, link to their definition on the page; the compiler links what it can find, and the linter fails an unlinked token or a link to a missing anchor.
- **Only the owner's words get a D-number.** Your defaults are labelled lead defaults; open questions are Q-numbers until answered.
- **Build from source.** Edit `<slug>.md`, `meta.json`, `diagrams/`, `prs/`, `schema/`, `mockups/`; rebuild; never hand-edit `build/`.
- **One status doc** (`status.md`), no handoff copies; deliverables never go into the code repo unless the owner asks.

## The packet

```
$SD/  <slug>.md  meta.json  status.md
      diagrams/<key>.mmd .svg .hotspots.json   mockups/*.png|jpg (+ their .html sources)   prs/pr-NN.md
      schema/<table>.json   queries/<name>.sql   shots/ (working renders)
      build/   content.json  content.files.json  tdd.html (generated; never edited)
      agents/  the agents lane write_agents.py generates (D6, D8; where and what: references/agents-lane.md)
```

## The run

### 0 · Environment and packet

```bash
python3 -I "$T/scripts/new_packet.py" <slug> --title "<the finding, not the topic>" --ticket P-XXX-NNN --repo <repo path>
"$S/scripts/doctor.sh"      # python3, Node 20+, tldraw offline, playwright-core + Chromium: each missing piece names the step it blocks
```

`new_packet.py` derives the repo slug from `--repo`'s origin exactly as system-design's `env.sh` does, creates `${SYSTEM_DESIGN_OUT:-~/.system-design}/<repo-slug>/system-design/<slug>/` (`--root` overrides), copies `templates/tdd-template.md` and `templates/meta.template.json` with the title, ticket, author, date, repo URL and evidence sha filled in, writes the `status.md` skeleton, and refuses to overwrite an existing packet. It also copies the placeholder images in `templates/packet/` (each marked "PLACEHOLDER · REPLACE ME"), so a fresh packet builds and renders but lints with `FILES/placeholder` and `FILES/placeholder-text` errors, one per placeholder image and one per line that still carries a template prompt: those errors are expected until you replace them, and they list what is left to write (`references/validators.md`). It records `origin/HEAD` (else `HEAD`) without fetching: `git fetch` first if the remote must be current. Rebuilding an existing TDD in the house template: make the packet the same way, then move the old content into the six sections. If a tool is missing, say which step it blocks and ask the owner; never skip the step.

Read the ticket, the PRD and the owner's replies first; never re-ask what they answered. Load the project's design context the way system-design's SKILL.md (Design context) says: tokens, component library, primitives.

### 1 · Evidence first

Before writing a claim, have its source. Code claims are `path:line` at the sha in `meta.json` (re-record `sha` and `sha_date` if you read another commit); counts are read-only queries saved in `queries/`, run once, recorded with their date in `status.md` (Counts) and on the page; anything not measured says so. Details, alias rules and what the linter checks: `references/evidence-rules.md`.

### 2 · Write the six sections, in this order

| # | Section | Answers | Containers it usually holds |
|---|---|---|---|
| 1 | The redesign | What changes, for whom, what stays? | lead, whole-system `[diagram]`, `cards {four}` |
| 2 | Front end | What does each person see, and how is that different from today? | `persona` boxes with shots and a `today` fold, `duo` |
| 3 | Rule coverage | Is every requirement in the PRD met, open or out? | a table whose first column is R-tokens, Status chips |
| 4 | End-to-end flow | How does a request move through the system, step by step? | flow and schema diagrams, `fold` > `cols` > `steps` with `[new]` |
| 5 | Decisions and open questions | What is still open, and what did the owner decide? | `decisions` grid, `decision` cards, `options` |
| 6 | For agents and additional context | What does a reviewer or an implementer need beyond that? | `group`s of `fold`s: scope, sizing (`cards {stat}`), rollout (`callout`), SQL, PR order (`<!-- PR-CARDS -->`), `glossary` |

`templates/tdd-template.md` shows every container once with guidance comments; the exact syntax (attributes such as `{chip: ...; #id; lead: no}`, `{small}`, `[chip:tone text]`, nesting with longer `::::` fences) is the docstring of `scripts/build_tdd.py`. Guidance comments must stay on one line each (a multi-line `<!-- -->` renders as text). The lead of each section is the finding, not the topic. A complete real example: `~/.system-design/<org>-web-app/system-design/<slug>/<slug>.md`.

PR deep dives are `prs/pr-NN.md` in system-design's shape (`$S/templates/pr-cards.md`); the `<!-- PR-CARDS -->` line in section 6 becomes the cards.

### 3 · Diagrams and hotspots

```bash
node "$S/scripts/draw_diagrams.mjs" --spec "$SD/diagrams" --out "$SD/diagrams" --name "<slug>-diagrams"   # .mmd -> .svg via tldraw offline
node "$S/scripts/svg_to_png.mjs" "$SD/diagrams/<key>.svg" "$SD/shots/diagram-<key>.png"                   # then Read the PNG
python3 -I "$T/scripts/diagram_hotspots.py" "$SD/diagrams/<key>.svg" --write                              # then fill href / image / note / table
```

Organisation, language and layout follow the system-design diagrams (D4); the boxes become hover hotspots. Tones, legend words, which boxes get a hotspot and the hotspots file shape: `references/diagram-conventions.md`. Mocks for section 2 are built from the project's real components: `references/mock-conventions.md`.

### 4 · Schema lane

One `schema/<table>.json` per new, changed or touched table (D7): columns with their change, sample rows with their source, measured reads and writes with source and date, expected load with its basis. The schema diagram's table boxes carry `"table": "<name>"` in `diagrams/schema.hotspots.json`, so a click opens the drawer. Start from `templates/schema.template.json`; shape and rules: `references/schema-lane.md, references/data-view.md (the Snowsight-style data preview and chat, D14)`. An unmeasured value is left out and renders "not measured".

### 5 · Build and check

```bash
cd "$SD"; (cd "$T/app" && bun install)                                 # bun install: once per machine
python3 -I "$T/scripts/build_tdd.py" <slug>.md build                 # -> build/content.json + content.files.json; warnings on stderr (--strict fails on any)
(cd "$T/app" && bun run build.ts "$SD/build")                        # -> build/tdd.html (React SSR + inline hydrate bundle)
(cd "$T/app" && bun run check_hydrate.ts "$SD/build/tdd.html")       # zero console errors on hydrate
python3 -I "$T/scripts/write_agents.py" build                        # -> build/agents/ (D6, D8; references/agents-lane.md); --check says whether it is current
python3 -I "$T/scripts/annotate_hotspots.py" "$SD" --apply           # proposes hover links/images for diagram boxes (D4); read the table before --apply
python3 -I "$T/scripts/lint_packet.py" "$SD"                         # validators: references/validators.md
node "$S/scripts/artifact_check.mjs" build/tdd.html shots            # contract + 1440/390, light/dark renders
~/.local/share/tdd-skill/venv/bin/python -I "$T/scripts/eval_packet.py" "$SD"   # eval lane -> build/eval/ (D10, D12, D18): every build and every review, never skipped; no --push here
```

**Also available: `tdd rev`.** During the bun port's overlap period the commands above stay the documented path. `bun "$T/cli/bin/tdd.ts" rev "$SD" [--no-shots] [--no-gh]` runs the same steps as one program (compile, app build, hydrate check, agents, lint to `build/lint.json`, `artifact_check`, then the eval on this run's screenshots: `tdd eval`, or `eval_packet.py` in the venv when a `--push` needs the `braintrust` package that `tdd eval` lacks), then `tdd load-db` into `$DATABASE_URL` (default `postgres://localhost/tdd_system`, the real database; skipped when nothing answers there) and the push only with `--push`. It journals each step under `build/.rev/`, so a stopped run resumes at the step it stopped on, or earlier when a file a finished step read or wrote has changed since (`--from <step>` and `--only <step>` override). Without `--push` the eval runs with `--skip linear,aws`, and no child process (git, bun, node, python) receives `BRAINTRUST_API_KEY`, `ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `LINEAR_API_KEY`, `GH_TOKEN`, `GITHUB_TOKEN`, an `AWS_*` variable or any other variable whose name ends in `_KEY`, `_TOKEN`, `_SECRET` or `_PASSWORD`; `DATABASE_URL` reaches `tdd load-db` only. With `--push` the eval alone also receives `BRAINTRUST_API_KEY`, `LINEAR_API_KEY`, the `AWS_*` variables and, unless `--no-gh`, `GH_TOKEN` and `GITHUB_TOKEN`. Usage and exit codes: `tdd rev --help`; how it works: `cli/README.md`.

**Eval duty (D18).** The eval is part of every build and every review, not a step someone runs later. The owner, 2026-11-09: "claude agents as they build / implement, review.  should be built into. both the agent description, implementation docs, and skills". The writer of a PR (the packet, the skill code, or code that implements the design) runs `eval_packet.py` for the packet after its last rebuild and puts the summary line (`scored N · metrics N · not run N · errors N`) and every scorer that moved in its return. The reviewer of record runs it again on the same tree and returns the score diff against the writer's run (the last two lines of `build/eval/history.jsonl`), not prose alone. A score that drops is fixed or explained; a scorer that turns `not run` is a missing tool or key, named as such. Only the integrating session passes `--push`, and only when `BRAINTRUST_API_KEY` is in the environment (never in a file) and the owner has said go. Who runs which scorers, and when: `references/eval-loop.md` (Who runs it and when).

The eval venv is created once with `python3 -m venv ~/.local/share/tdd-skill/venv && ~/.local/share/tdd-skill/venv/bin/pip install braintrust autoevals anthropic boto3`. Every PR, commit and Linear issue that implements the design carries the tagging convention from `references/eval-loop.md` (a `TDD: <slug> PR <n>` trailer; Linear label `tdd:<slug>`), which is what joins implementation, QA and bugs back to the document. Read every `build_tdd.py` warning: a missing diagram, image, PR folder, table or anchor is a gap in the page. Open the `artifact_check` screenshots and look at them; the metrics are not the review.

### 6 · Decisions with the owner

Ask with AskUserQuestion, **one decision at a time**: the background, the gap, a concrete example with real numbers, and the options in words (the Q card's `options`, recommendation marked `{rec}`). Record the answer in the owner's words as the next D-number (re-read `status.md` for the highest D first; other sessions add concurrently), with the date and who said it, in `status.md` (Decisions) and in section 5: rename `Q<n>` to `D<n>` everywhere in the source and move the card under Decided with `{status: <Owner>, YYYY-MM-DD}`. Never re-ask a recorded decision; stop if the owner rejects the question. Ask only what changes the build.

### 7 · Publish and mirror

Read `build/tdd.html` in full, then publish it with the Artifact tool as a private artifact: `file_path` the page, `files` the `files` map from `build/content.files.json`, `icon` on the first publish only, a one-sentence `description`. If the tool's quickstart suggests a document type, publish this HTML page anyway: the artifact is the source of truth and a Claude Doc is only ever a mirror. Write `artifact_url` into `status.md` (top) and `meta.json`, then mirror the row in the project's Notion Systems database. Call shapes, the files map, redeploys and the Notion row: `references/publish-and-mirror.md`.

### 8 · Close-out

Every published change bumps `rev` by one, sets `updated` to today and says what changed in `rev_note` (a line under Revisions in `status.md`, and the Notion row's last-change summary). Rebuild, lint green, `artifact_check` clean, eval run, republish, refresh the mirror. Finish with a typed return: `DONE | DONE_WITH_CONCERNS | NEEDS_CONTEXT | BLOCKED | NEEDS_HUMAN`, the artifact URL, the packet paths (`<slug>.md`, `status.md`, `build/tdd.html`, the agents lane), the eval summary line and the scorers that moved (a reviewer: the score diff), what is unverified, and the open questions waiting on the owner.

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes.
- `readonly`: work in the current directory. Make no branch, worktree, commit, push, PR, PR comment or review, and change no remote ref; that includes the `gh pr comment` and `gh pr edit` calls in the ops-loop templates (`templates/ops/`). Write, build, lint, check and eval the packet as the skill describes (the packet lives outside the code repo), and print each git or gh command you would have run under a `Would run:` heading.

## References

| File | What it holds |
|---|---|
| `templates/tdd-template.md` | the six-section skeleton, every container once, guidance as one-line comments |
| `templates/meta.template.json` | every header field the compiler reads, each explained in `_doc` |
| `templates/tdd-system/README.md` | the design system: tokens (light, dark), type scale, class vocabulary, states |
| `references/evidence-rules.md` | sha and date, `path:line` with aliases, dated counts, "not measured", what the linter checks |
| `references/diagram-conventions.md` | tldraw drawing, legend tones, hotspots: which boxes, the JSON shape |
| `references/mock-conventions.md` | mocks from real components, shot sizes, captions, `cols3`, the `today` fold |
| `references/publish-and-mirror.md` | the Artifact call, `artifact_url`, the Notion row, rev bump rules |
| `references/schema-lane.md`, `templates/schema.template.json` | `schema/<table>.json` and the drawer |
| `references/agents-lane.md` | the `agents/` files and their mappings |
| `references/eval-loop.md` | the eval lane: setup, flags, the six outputs and their scorers, who runs it and when, the push model |
| `references/validators.md` | lint rule ids, severities and fixes |
| `scripts/build_tdd.py` (docstring) | the source syntax: containers, markers, attributes, tokens |

## Trust model (read before letting anyone but the owner edit a source)

The compiler trusts the source author: markdown becomes inline HTML that the page renders as written. The compiler strips `<script>`, `<iframe>`, `on*=` handlers and `javascript:` URLs as a guard against accidents, but it is not a sanitizer for hostile input. A published artifact is private to its owner by default; before a source can be edited from outside (the ops loop's edit grant, a shared packet, a PR from a stranger), review the diff as you would code, and never let comment text or issue text flow into the source unreviewed.

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/tdd-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before the typed return of step 8.

Keys: `skill` (`tdd`), `artifacts` (absolute paths: the packet's `<slug>.md`, `meta.json`, `status.md`, `build/content.json`, `build/tdd.html` and the agents lane folder; `[]` when nothing was written), `summary` (one line),
plus `slug` (the packet slug); `packet_dir` (absolute path of the packet directory, or `null` when step 0 did not make one); `artifact_url` (the published artifact's URL, or `null` when it was not published); `status` (the typed return: `DONE`, `DONE_WITH_CONCERNS`, `NEEDS_CONTEXT`, `BLOCKED` or `NEEDS_HUMAN`); `eval_summary` (the `scored N · metrics N · not run N · errors N` line, or `null` when the eval did not run).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
