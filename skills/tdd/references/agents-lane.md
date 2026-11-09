# The agents/ lane (decisions D6 and D8)

The page is for people. The `agents/` lane is the same design cut into files an agent can load one at a time: a small
front door, one slice per task, and machine indexes that link the slices by id. Everything in it is generated from
`build/content.json` (and the packet's `prs/`, `diagrams/*.hotspots.json`, `schema/`), so it cannot drift from the
page. Nobody edits it by hand; edit the TDD source and rebuild.

## When it is written
After every build, in this order:
```
python3 -I scripts/build_tdd.py <slug>.md build          # content.json
bun run app/build.ts build                               # tdd.html
python3 -I scripts/write_agents.py build                 # agents/ (also strips tdd.html into agents/page/)
python3 -I scripts/write_agents.py build --check         # exit 1 when agents/ is stale
```
`write_agents.py <build-dir> [--packet <dir>] [--out <dir>] [--budget-tokens N] [--link-images] [--force] [--quiet]`
writes the lane. `--packet` defaults to the build dir's parent when it holds content.json's source file. `--out`
defaults to `<build-dir>/agents`. `--link-images` points at the build's images instead of byte-copying them. `--force`
overwrites an `--out` that is not an agents lane. A rerun removes only the files the previous run listed in
`tokens.json`; anything else in the folder is left alone and reported by `--check`.

`strip_page.py <tdd.html> [--out-dir D]` is the D8 tool on its own: it strips any built page (a house TDD page, an older
system-design artifact, a claude.ai export) into `plain.html`, `plain.txt` and `outline.json` (default
`<dir>/<name>.stripped/`, so a standalone run never edits the lane). `write_agents.py` calls its functions to write `agents/page/`, and builds a reduced lane from the
page alone when content.json is missing.

`--check` (and the library call `check(build_dir, out=None) -> list[str]`, used by `lint_packet.py`) reports the lane
stale when: README.md is missing; its header's `content <timestamp>` differs from content.json's `generated`; its
`page <sha>` differs from tdd.html's sha256 (or tdd.html appeared since); the generator version changed; a file listed in
`tokens.json` is missing or its sha256 changed (a hand edit); a file appeared that the generator did not write.

## Layout
Paths are relative to `agents/`; every path inside a JSON file is relative to `agents/` too.

| Path | What it holds | Budget |
| --- | --- | --- |
| `README.md` | front door: what the doc is, status, rev, sha, the rules, where the eval scores are and the duty to leave your own (Scores), which file for which task, section and decision index, counts, build warnings | <= 8 KB (hard) |
| `llms.txt` | the same index in llmstxt.org shape: H1, blockquote summary, link lists with one-line notes, `## Optional` | small |
| `tdd.md` | the whole document as clean markdown: `{#id}` on headings and anchored blocks, tables, lists, code fences, decisions in place, citations as `path:line` | exempt (whole doc) |
| `tdd.txt` | the whole document as plain text, one paragraph per line, `#` heading markers, table rows as `a \| b` | exempt |
| `sections/NN-<id>.md` | one per section; a section over the budget splits into `NN-<id>.md`, `NN-<id>-part-2.md`, ... at fold-group boundaries (then folds, then table rows or list items) | <= budget |
| `decisions/D-N.md` | one per decision box (D7 -> `D-7.md`, R2 -> `R-2.md`), MADR-style front matter | <= budget |
| `decisions/decisions.json` | id, anchor, question, status (verbatim), state (done/open/held), date if the status states one, section, slice, referenced_from | exempt |
| `citations.json` | every `path:line` on the page and in the PR deep dives: path (aliases expanded), from, to, sha, href, section, nearest anchor, slice | exempt |
| `citations.md` | the same, grouped by repo file | <= budget |
| `refs.json` | every anchor (kind, title, definition text, section, slice), every reference token (anchor, slice, uses), every reference link, unlinked tokens, families, glossary | exempt |
| `sql/NN-<section>.sql` | every ```sql block, verbatim, with a header naming section, anchor and slice; then `NN-schema-<table>.sql` for each schema table's DDL | <= budget |
| `code/NN-<section>.<ext>` | every other fenced block, by language (json becomes `.jsonc` so it can carry the header) | <= budget |
| `schema/<table>.json`, `schema/schema.md` | the schema lane (D7) as compiled: columns with change flags, measured (value, unit, source, date) and expected; "not measured" when absent; table anchors `{#tbl-...}` live here | <= budget |
| `prs/pr-NN.md`, `prs/prs.md`, `prs/prs.json` | the packet's deep dives (copied; `](#id)` page links rewritten into `sections/`), the PR order with milestone, title, change, rollback | <= budget |
| `images/diagrams/*.svg`, `images/mockups/*` | byte copies of every pictured file, never re-encoded | binary |
| `images/index.json` | per image: path, kind (diagram or mock), in_today_block, title, caption, alt, section, anchor, width, height, bytes, sha256, legend, hotspots `[{label, href, table}]`, box count and labels, sidecar | exempt |
| `images/diagrams/<key>.md` | every box of a diagram (from `<key>.hotspots.json`): label, kind, tone, full text, box in percent, link | <= budget |
| `page/plain.html`, `page/outline.json` | tdd.html with scripts, styles, the JSON island, the rail, hotspots, overlays and buttons removed; every `<details>` open; `a.ref` as `text (→ #id)` | exempt |
| `map.json` | entities (section, anchor, decision, pr, table, image, citation, repo_path, sql, code, token, file) and edges (contains, in_file, anchored_at, in_section, documented_by, appears_under, hotspot, cites, has_cite, extracted_from, defined_by, references) | exempt (for programs) |
| `tokens.json` | bytes, sha256 and a token estimate per file; budget, over-budget list, largest slice | exempt |

## Which agent reads what
| Task | Give it |
| --- | --- |
| Build a PR | `README.md`, `prs/prs.md`, `prs/pr-NN.md`, the `decisions/D-N.md` the PR names, the `sql/` and `code/` files its section points to. Not `tdd.md`. |
| Iterate on one section | `sections/NN-<id>.md` (all parts if split). Its front matter lists the anchors it holds; its "Defined elsewhere" footer gives a one-line definition and a link for every reference it uses, so it stands alone. |
| Semantic search, embeddings | `tdd.txt` (one paragraph per line, chunk by line) or `sections/*.md` (chunk by file; ids in front matter). |
| Find a fact | `grep -rn "<term>" agents/sections` (anchors are written `{#id}`, so `grep -rn "{#d3}"` finds where D3 is defined). |
| SQL review | `sql/*.sql` and `schema/schema.md`. The page's DDL (`NN-<section>.sql`) and the schema lane's DDL (`NN-schema-<table>.sql`) come from different source files; a diff between them is a finding. |
| Image review | `images/index.json` first (what each picture holds without opening it), then `images/diagrams/<key>.md` (every box's text), then the image itself. |
| Check citations | `citations.md` (by file) or `citations.json`; re-read each file at the sha. |
| Follow a reference (D3, E1, F3.2, PR 7) | `refs.json` -> anchor -> slice. |
| Tooling over the design | `map.json` with jq; `decisions.json`, `prs.json`, `images/index.json` for typed lists. |

**Scores (D18).** The eval lane writes beside this one, in `build/eval/` (`../eval/` from `agents/`):
`output.{artifact,packet,implementation,qa,bugs,tokens}.json`, `probe.json`, `lint.json` and `history.jsonl`
(`scripts/eval_packet.py`; `references/eval-loop.md`). The README's Scores paragraph tells a subagent that opens the lane
that they exist, and that when it changes the packet it rebuilds, reruns `write_agents.py` and `eval_packet.py`, and
leaves its own scores: its line in `history.jsonl`, and the summary line and moved scorers in its return. A reviewer
returns the score diff (eval-loop.md, Who runs it and when). The lane holds no scores itself, so `--check` stays a
check of the lane.

## Conventions
- **Header.** Every text file names its source in one line: `agents lane · from <slug>.md · rev N · sha <sha> · content <generated> · page <sha256-12> · write_agents/1 · generated, do not edit`. As `> ...` on line 1 of markdown without front matter, as `generated:` in front matter, as a comment in sql and code, as `_generated` (first key) in JSON, as a YAML comment on line 2 of the PR copies.
- **Anchors.** The page's ids, written `{#id}` (headings, decision and fold titles, figures, the first cell of an anchored table row). Every section-flow anchor is defined in exactly one `sections/` file; table anchors are defined in `schema/schema.md`. `tdd.md` repeats them all (it is the whole document); `decisions/` and `prs/` do not.
- **Links** are relative markdown links into the slice that defines the anchor; a token with no definition stays plain text (and is listed as unlinked in `refs.json`).
- **Citations** are written out in full (`packages/contracts/src/QueueStages/Domain.ts:862-875`), not with the page's aliases (`CON/...`); the aliases are listed in README.md and citations.json.
- **Code** up to 60 lines is pasted in section slices with a pointer to its `sql/` or `code/` file; a longer block is replaced by the pointer. `tdd.md` pastes everything.
- **PR deep dives** are linked from section slices, never pasted there; `tdd.md` pastes them.
- **Tokens** are `ceil(chars / 4)`: an estimate, not a tokenizer. Slices are packed to 90% of the budget so links and footers fit.

## Budgets and their basis
- README.md <= 8 KB: well under the 32 KiB that Codex reads from an AGENTS.md before it silently drops the rest (research/agent-files-2026.md 2.2), small enough to read on every task.
- Slice <= 8k tokens (`--budget-tokens`): a heuristic. The research marks the 2k-8k range unverified; the Skills guidance (body under about 5k tokens, bundled files free until read) is the nearest primary source.
- The research suggests a task bundle (README + PR file + its decisions + its sql/code) under about 20k tokens; also a heuristic, and not computed per PR yet (PR files do not name their decisions structurally).

## Anti-patterns (research/agent-files-2026.md section 4) and how the lane avoids them
1. One giant `llms-full.txt` as the only agent file: `tdd.md`/`tdd.txt` exist but sit under `## Optional`; slices are the default.
2. Agents scraping `tdd.html`: the lane comes from content.json; `page/plain.html` is the stripped page for when the DOM itself matters.
3. Slices that say "see above": every slice repeats the section heading, a continuation line and breadcrumb when split, and a "Defined elsewhere" footer.
4. Duplicated truth hand-maintained in several places: duplication here is generated from one source and checked by sha in `tokens.json`; long code is linked, not pasted.
5. Images with no caption, alt or source: `images/index.json` carries title, caption, alt, size and box labels; diagrams ship as SVG (text inside) with a box-text sidecar; no re-encoding.
6. Rulings buried in prose: every decision has `state` (done/open/held) in front matter and in `decisions.json`; the verbatim status is kept beside it.
7. `path:line` without a sha: every citation carries the packet's sha (and the README says to re-read at it). A packet with no `sha` in meta.json produces `sha: null`, which the linter should flag.
8. A front door over 32 KiB or an inlined index: README.md is capped at 8 KB and falls back to a compact index when over.
9. Tool-specific forks that drift: one README and one llms.txt from one template pair (`templates/agents/`).
10. Brittle if-else prescriptions: the README gives the rules (re-read at sha, stop at open decisions) and the task table, not scripts.
11. Hand-edited generated files and unvalidated mappings: `--check` fails on any edited or unlisted file; `map.json` warns on edges whose ends have no entity.

## Templates
`templates/agents/README.template.md` and `templates/agents/llms.template.txt` are filled with `{{name}}` placeholders:
header, head_raw, title, summary, status, rev, rev_note, updated, repo, sha, sha_date, source, generated, budget,
sections, decisions, files, counts, aliases, notes, unlinked, llm_sections, llm_decisions, llm_prs, n_sql, n_code,
tdd_md_tokens, tdd_txt_tokens. Without the files, the same text built into `write_agents.py` is used.

## Fallback (page only)
With no content.json but a `tdd.html`, `write_agents.py` builds README.md, llms.txt, tdd.md, tdd.txt, `sections/` (one per
`<section id>`), `sql/` and `code/` (languages guessed when the page does not say, and marked "guessed"), `decisions/`
(from `.dec` cards), `images/index.json` (linked, not copied), `refs.json` (every id and its slice), `page/` and
`tokens.json`. No citation index, no map, no schema lane. Rebuild content.json for the full lane.

## Unverified or open
- Token counts are chars/4 estimates; no tokenizer was run.
- The 8k slice budget and the 20k bundle figure are heuristics (research marks them unverified).
- Image vision-token cost is not estimated; the index gives pixel sizes and bytes only.
