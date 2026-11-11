# Validators: `scripts/lint_packet.py`

The linter checks a TDD packet against the house rules: the markdown source, `meta.json`, `prs/`, `diagrams/`,
`mockups/`, `schema/`, and the build that `build_tdd.py` wrote (`build/content.json` and `build/content.files.json`).
It runs on Python's standard library under `python3 -I`, and calls git read-only through subprocess. It never writes
into the packet.

```
python3 -I "$T/scripts/lint_packet.py" <packet-dir> [--build <dir>] [--strict] [--json <out>] [--skip <ids>] [--compile]
```

| Option | Effect |
|---|---|
| `--build <dir>` | The build folder to read. The default is `<packet>/build`. |
| `--compile` | Compile the source into a temporary folder (`build_tdd.py --no-copy`) and lint that instead of the build. The linter also does this on its own when the build has no `content.json`, for example in an older system-design packet. The page-size, stale-build and agents checks need a real build, so they are skipped. |
| `--strict` | Exit 1 on any warning too. |
| `--json <out>` | Also write `{packet, build, compiled, findings[], notes[], counts{errors, warnings, by_rule}}`. Each finding is `{rule, check, severity, file, line, message}`. `file` is relative to the packet, and `line` is 1-based or null. |
| `--skip <ids>` | Leave out whole rules (`CITES`) or single checks (`CITES/no-line`). Separate ids with commas. |

Output groups the findings by rule, one line each: `severity CHECK-ID file:line: message`. Errors come first within
each rule. Notes follow (things skipped and why, citation counts), then the summary line `N errors, M warnings`.
Exit codes: 0 when the lint is clean or has only warnings; 1 on any error, or on any warning with `--strict`; 2 on bad
usage.

Any rule that reads a JSON file can also report `<RULE>/bad-json` (error) when the file does not parse.

## Where it runs
1. **After every build.** Run `build_tdd.py`, then the app build, then `lint_packet.py <packet>`. Fix the errors
   before you look at the page.
2. **Before publish.** Run `lint_packet.py <packet> --strict`. Publish only when it exits 0. A warning you decide to
   keep goes into `status.md` with the reason, and then that check is skipped by id on the publish run
   (`--skip CITES/no-line`), never by skipping the whole rule.
3. **In the skill's close-out.** Paste the summary line and any kept warnings into the close-out return. Pass
   `--json build/lint.json` so reviewers and the agents lane can read the same findings.

## Severity policy
- **error**: the published page is wrong. A link is dead or points at nothing, a token is unlinked (D5), a decision
  record is malformed, a measurement has no source (D7), a file is missing or too big, or the build does not match
  the source.
- **warn**: the page works but is weaker or not verifiable. Something is stale, ambiguous, orphaned, or out of order.
  This includes everything the linter could not verify because some context is missing; those warnings are marked
  `NEEDS_CONTEXT`.
- A finding the author cannot fix in the packet (a compiler gap) still counts. Its message says "compiler fix" or
  names the converter. The fix goes to the compiler owner, and the check is skipped by id until it lands.

## STRUCT: page structure
| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `STRUCT/no-source` | error | The packet has a source markdown, `<slug>.md`, or the `source` that `content.json` names. | Name the source after the packet folder. |
| `STRUCT/no-content` | error | A `content.json` could be read or compiled. The message carries the compiler's last error line. | Fix the compile error, then rebuild. |
| `STRUCT/missing-section` | error | The six house sections exist, matched by id or by title: the redesign or overview (`redesign`, `overview`); front end (`frontend`); rule coverage or requirements (`coverage`, `requirements`); end-to-end flow (`flow`, or a title with "end-to-end"); decisions and open questions (`questions`, `decisions`); for agents (`more`, `agents`, or a title with "for agents"). | Add the section, or give the existing one the house id. |
| `STRUCT/section-order` | warn | The matched sections appear in the house order. Only the sections outside the longest run already in order are reported. | Move the section. |
| `STRUCT/numbering` | error | Sections are numbered 1..n in order. The `##` titles in the source match the sections in `content.json`, one for one. | Rebuild. A mismatch means the build is stale. |
| `STRUCT/heading-id` | error | Every `##` carries an explicit `{#id}`. | Add `{#id}`. Without one, the anchor is the title's slug, and every link to the section breaks when the title changes. |
| `STRUCT/id-collision` | warn | Each explicit `{#id}` in the source is set once and reaches the page. A second use is silently renamed `id-2`, and a block type that ignores ids drops it. | Rename one of them, or move the id to a block that takes ids. |
| `STRUCT/raw-markdown` | error | No literal markdown is left in page text outside code: `![..](..)`, `[..](#..)` or `[..](http..)`, `**..**`, `[diagram:` markers, `:::` fences. | Two images on adjacent lines become one paragraph, so put a blank line between them. A `[text](#anchor)` link in a PR body is a converter gap: `build_prs.py` converts only `https` links. |

## DNUM: decision records
| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `DNUM/duplicate` | error | Each decision id is defined once. | Renumber the later one. |
| `DNUM/gap` | error | The D family runs from D1 with no gap. Other decision families, such as R2, are checked only for duplicates. | Fill the gap. A withdrawn number stays on the page as a decision with a `held` or `withdrawn` status. |
| `DNUM/bad-id` | error | A `::: decision` title starts with a reference token (`D7 · the question`). | Rewrite the title. |
| `DNUM/no-status` | error | Every decision has a `{status: ...}`. | Add the status. |
| `DNUM/done-no-date` | error | A decision whose state is done has a `YYYY-MM-DD` date in its status. | Add the date of the owner's ruling. |
| `DNUM/outside-section` | warn | Every decision sits in the decisions section. | Move the box, and link to it from where it was. |
| `DNUM/none` | warn | The page has no decision boxes but refers to D-numbers. | Write each ruled decision as a `::: decision` box. Until then, every D-token is also a `REFS/unlinked` error. |

## REFS: reference tokens and anchors (D5)
| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `REFS/unlinked` | error | Every reference token on the page (D7, E3, F3.2, R2, PR 7, OS-01...) has a definition: `content.refs.unlinked` is empty. The token is located in the source or in `prs/`. | Define it (a table row whose first cell is the token, a decision, or an explicit `{#id}`), add a `refs` family in `meta.json`, or reword. |
| `REFS/missed-link` | error | The token sits in prose the compiler means to link, but it was not linked because of where it sits. Today this is the paragraphs inside a persona box: `link_tree` does not descend into `persona.text`. | A compiler fix. |
| `REFS/title-token` | warn | The token sits where the compiler does not link by design: section, fold, persona, card and diagram titles, legend labels, decision questions, PR card titles, PR milestone names and notes, and headings or summaries inside HTML. | Reword, or define the token and ask the compiler owner to link that position. The message says "no definition either" when the token is also undefined. |
| `REFS/broken-anchor` | error | Every `href="#..."` in the page's inline HTML points at an id on the page. The page's ids are defs, anchors, every node id, every `id="..."`, and the schema table ids. | Fix the anchor, or add the id. |
| `REFS/hotspot-anchor` | error | Every diagram hotspot `href` resolves, and every hotspot `table` is a schema table. | Fix the hotspots json, or add the anchor or table. |
| `REFS/family` | error | Every `meta.json` `refs` family (`"OS": "#coverage"`) points at an anchor on the page. | Fix the target. |

## CITES: path:line citations
The linter checks each distinct citation the compiler recorded (``` `SVC/X.ts:12-40` ```, with aliases expanded from
`meta.path_aliases`) in `meta.repo_path` at `meta.sha`. It lists the tree with `git ls-tree -r <sha>` and counts a
file's lines with `git show <sha>:<path>`. A path that exists in the packet or the build, such as
`evidence-snapshot.json`, counts as resolved. When the sha is missing from the clone, or `meta.json` has no sha,
the linter checks against HEAD instead, and every finding it would have raised as an error becomes a warning tagged
`[HEAD fallback]`. The one exception is `CITES/short-path`, which stays an error: a short path's line link is dead at
any commit.

| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `CITES/no-repo` | warn | `repo_path` is missing, or is not a git checkout. The message says `NEEDS_CONTEXT` and how many citations went unchecked. | Add `repo_path`, `repo_url` and `sha` to `meta.json`. |
| `CITES/sha-unknown` | warn | `meta.sha` is a commit in the clone (`git cat-file -t`). | Run `git -C <repo_path> fetch origin`, or correct the sha. |
| `CITES/sha-date` | warn | `sha_date` equals `git show -s --format=%cs <sha>`. | Correct `sha_date`. |
| `CITES/repo-url` | warn | `repo_url` is the clone's `origin`. | Point `repo_path` at a clone of the linked repo. |
| `CITES/short-path` | error with a line, warn without | The path is not in the tree, but real files end with it (`Domain.ts:845-848`). With a line number, the compiler links it to `blob/<sha>/<short path>`, which is dead. | Write the full path, or an alias from `path_aliases`. The message names the file it found, or how many files match. |
| `CITES/path-unknown` | error (warn at HEAD) | No file in the tree has this path, or ends with it. | Fix the path. If the design adds this file, name it in prose rather than citing it. |
| `CITES/line-range` | error (warn at HEAD) | `from >= 1`, `to >= from`, and `to` is within the file's line count at the sha. | Re-cite against the sha. |
| `CITES/no-line` | warn | A code file (`ts tsx js mjs cjs py sql cs rb go rs sh css html graphql proto`) is cited without a line or range. Citations to `md`, `json`, `yaml` and `txt` files are exempt. | Add `:line` or `:from-to`. |

## DIAG: diagrams and hotspots (D4)
| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `DIAG/missing` | error | Every `[diagram: key]` has `diagrams/key.svg` or `diagrams/key.png`. | Draw or export it. |
| `DIAG/duplicate` | warn | Each diagram is embedded once. | Embed it once and link to its figure id elsewhere. |
| `DIAG/stale-svg` | warn | A `diagrams/key.mmd` beside the svg is not newer than the svg. | Redraw the svg. |
| `DIAG/viewbox` | error | The `viewBox` in `key.hotspots.json` equals the svg's. | The svg was redrawn: rerun `diagram_hotspots.py diagrams/key.svg --write`. |
| `DIAG/hotspot-label` | error | Every annotated hotspot box (one with an href, image, note or table) still names a box in the svg. The labels come from `diagram_hotspots.py`'s own `Shapes` extractor. | Rerun `--write`, then move the annotation to the right box. |
| `DIAG/hotspot-image` | error | A hotspot's `image` file exists. | Add the image, or fix the path. |
| `DIAG/hotspot-table` | error | A hotspot's `table` has `schema/<table>.json`. | Add the table card (D7), or fix the name. |
| `DIAG/legend-tone` | warn | Each legend item resolves to known tone classes, without repeats. For example, `= orange dashed` gives `orange dashed dashed`, because `orange` already means dashed. | Write `= orange`. |

## SCHEMA: table cards (D7)
| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `SCHEMA/shape` | error | Each `schema/*.json` is a JSON object. | Rewrite it as one object per table. |
| `SCHEMA/required` | error | Each `schema/*.json` has `table`, `state` and `columns[]`. | Add the field. |
| `SCHEMA/state` | error | `state` is `new`, `changed` or `existing`. | Use one of the three. |
| `SCHEMA/column` | error | Every column has a name. `change` is one of `new`, `changed`, `dropped` or `unchanged`. A new table has only new columns. | Fix the column. |
| `SCHEMA/column` | warn | A column has neither a type nor a note saying why there is none. A `changed` column has no `was`. A `changed` table has no column that changes. | Write the type, or note that the source does not state it (never guess a type). |
| `SCHEMA/sample-rows` | error | Every sample row's keys are column names. | Rename the keys, or add the columns. |
| `SCHEMA/sample-rows` | warn | Sample rows have a `sample_source`. | Say where the rows came from, or that they are synthetic. |
| `SCHEMA/measured` | error | Every measured value has a `source` and a `date` (`YYYY-MM-DD`). | Add both. Without a source, leave the measure out, and the drawer reads "not measured". Measurements are never invented. |
| `SCHEMA/expected` | error | Every numeric expected value has a `note` giving its basis. | Say how it was estimated. |
| `SCHEMA/unused` | warn | The table is neither a hotspot table on a diagram nor named on the page. | Wire it to the schema diagram, or drop it. |

## FILES: publish manifest and the scaffold's placeholders
| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `FILES/no-manifest` | error | `content.files.json` sits beside `content.json`. | Rebuild. |
| `FILES/missing` | error | Nothing is listed under `missing`, and every source in `files` exists. | Add the file, or fix the reference. |
| `FILES/too-big` | error | No published file is over 15 MB. Neither is any media file under `mockups/` or `diagrams/`. | Compress, crop or split the file. |
| `FILES/page-size` | error | The built page (`page`, usually `tdd.html`) and `page_bytes`, when present, are at most 16 MB. When the app has not built the page yet, this is a note, not a finding. | Move images out of the page. |
| `FILES/orphan` | warn | Every media file (`svg png jpg jpeg gif webp avif mp4 webm mov`) under `mockups/` and `diagrams/` is published by some page of the packet: `content.files.json`, or any `*.files.json` at the packet root, or a hotspot image. Source files (`.mmd`, `.tldraw`, `.json`, `.html`, `.css`) are not checked. When the markdown names the orphan, the message gives that line: the page did not pick the file up. | Embed it, fix the markup that dropped it, or delete it. |
| `FILES/pr-unloaded` | warn | Every `prs/*.md` is named `pr-NN.md`. The PR loader skips any other name. | Rename it, for example `pr-02b.md` to a free number. |
| `FILES/placeholder` | error | No file the page publishes (a source in the manifest's `files`: the build's copy, or with `--compile` the packet's own file) contains the scaffold's marker "PLACEHOLDER · REPLACE ME". The location is the first marker's line in that file. | Draw the diagram (`draw_diagrams.mjs` writes over the placeholder) or render the shot, point the page at it, delete the placeholder, and rebuild. |
| `FILES/placeholder-text` | error | No line of the source markdown or of `status.md` still carries a scaffold placeholder: the marker "PLACEHOLDER · REPLACE ME", the date `YYYY-MM-DD`, or one of the template's `<angle-bracket>` prompts (the list below). One finding per line, naming the placeholders on it. Fenced code counts. One-line comments do not (the compiler drops them), nor does a prompt right after a letter, digit or `_` (`useState<State>`). The check runs without a manifest too. | Write the real text, or delete the line. In `status.md`, replace the Decisions guide line with the first decision (or `- none yet`). |

**The scaffold's placeholders.** `new_packet.py` and `tdd new` write a packet that builds and renders but is not
finished: seven placeholder images copied from `templates/packet/` (each says "PLACEHOLDER · REPLACE ME"), and a
source and `status.md` full of prompts. A fresh packet therefore lints with errors: one `FILES/placeholder` per
placeholder image and one `FILES/placeholder-text` per line that still carries a placeholder (about 70 in all). They
are expected while the author writes, they list what is left to replace, and the page is ready to publish only when
they are gone. They do not stop `tdd rev`: its lint step records the findings in `build/lint.json` (and the database)
and fails only when the linter cannot run, so the draft page still builds; the publish run (`--strict`, below) is what
they block. To lint a draft you know still has placeholders, and see only the other findings, pass
`--skip FILES/placeholder,FILES/placeholder-text`. Never skip them on the publish run. The tokens are matched exactly, case and spacing included (`lint_packet.PLACEHOLDER_MARK`,
`PLACEHOLDER_DATE` and `PLACEHOLDER_PROMPTS`, mirrored in `cli/src/lint/patterns.ts`):
- the marker `PLACEHOLDER · REPLACE ME`, and the date `YYYY-MM-DD`;
- every `<angle-bracket>` prompt of `templates/tdd-template.md` outside its one-line comments, plus the title a
  scaffold writes when `--title` is left out (`<Title: the finding, not the topic>`) and the `status.md` guide's
  `<the owner's words>`: 70 prompts today, such as `<What is new>`, `<Role>`, `<Moment>`, `<State>`, `<Caption>`,
  `<Owner>`, `<ALIAS>`, `<new_table>`, `<new_column>`, `<YYYY-MM-DD>` and the sentence-long ones.

An all-lower-case one-word prompt (`<name>`, `<path>`, `<n>`, `<owner>`) is ordinary command-line notation, so it is
not matched: a design that documents `git show <sha>:<path>` lints clean. Every template line that carries one also
carries a matched prompt. So do the `status.md` guide lines that stay true while a list is empty (Counts, Open
questions): they hold only that notation and are not flagged. To spell a date format in a design, write "ISO date" or
`yyyy-mm-dd`. `cli/test/lint/placeholders.test.ts` keeps the list equal to what a fresh scaffold writes.

## META: header fields and revisions
| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `META/missing` | error | The packet has a `meta.json`. | Write one from `templates/meta.template.json`. |
| `META/field` | error | `meta.json` has a `title`, a `people` entry with role Author, `updated` as an ISO date, `rev` as an integer, and a `status`. | Fill the field. |
| `META/stale-build` | warn | No source, `meta.json`, PR, schema, diagram or mockup file is newer than `build/content.json`. | Rebuild before publishing. The lint you just ran read the old build. |
| `META/rev-not-moved` | warn | When markdown changed after the last build, `rev` or `updated` in `meta.json` differs from the build's. | Bump `rev`, and set `updated` and `rev_note`. |
| `META/artifact-url` | warn | A status that is not Draft has an `artifact_url`, in `meta.json`, in `status.md` (`artifact_url: https://...`), or in `packet.json`. | Record the URL after you publish. |

## AGENTS: the agents lane (D6, D8)
When `build/agents/` exists, the linter imports `scripts/write_agents.py` and calls `check(build_dir)`. The function
belongs to the agents-lane worker. It may return a list of strings (each becomes an `AGENTS/problem` error), or a
list of dicts `{severity, check, message, file, line}`. It may also return a dict holding such a list under
`problems` or `findings`. If the module or its `check` is absent, the linter skips with a note. If the call raises,
the finding is `AGENTS/check-failed` (error).

## XDOC: consistency across packets
The linter compares the `::: glossary` terms of this packet with every sibling under
`~/.system-design/<slug>/system-design/*/`. It reads `- **Term** — definition` items from each packet's root
markdown, and compares definitions ignoring case, punctuation and markup.

| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `XDOC/glossary-conflict` | warn | The same term has two different definitions in sibling packets. | Agree one definition and use it in both. |

The same D-number with different question text in two packets is **not** a finding. D-numbers are per document:
D3 in one TDD and D3 in another are different decisions, so the linter does not compare them.

## PARITY: the TypeScript compiler (D19, the overlap period)
While the bun port runs beside the Python (PR 11 to the cutover), the linter compiles the source a second time with
`cli/bin/build-tdd.ts` (`bun --no-install`, `--no-copy`, into a temporary folder, with `TDD_GENERATED` set to the
build's `generated`) and compares it with the build. When the build or its manifest differs, or the TypeScript compile
fails, it also compiles the source with `build_tdd.py` the same way, and only a difference from that fresh compile is
a finding: a stale build, files found only under the build folder, or another spelling of the packet path are notes,
and when `build_tdd.py` fails on the source too, the rule notes that and leaves the failure to the other rules. Nothing
is written into the packet. When `bun` is not on PATH, or `cli/` or its `node_modules` is absent, the rule is a note and a
skip, never a finding. `parity/gate.ts` (run by the writer and the reviewer of record) compares more: every copied
file and the dashed SVGs, stdout and stderr.

| Check | Severity | What it checks | How to fix |
|---|---|---|---|
| `PARITY/content` | error | `content.json` from the TypeScript compiler is byte-identical to the build's (or to a fresh `build_tdd.py` compile when they differ), and the TypeScript compiler does not fail where the Python succeeds. | Fix `cli/src/compile`: `build_tdd.py` is the reference until the cutover. A change to `build_tdd.py` lands with the same change in TypeScript (the twin rule). |
| `PARITY/manifest` | warn | `content.files.json` against a fresh `build_tdd.py` compile's: the same `files` paths, `missing` list and `warnings`, in order. | As above. |

## The TypeScript linter (`tdd lint`, D19, the overlap period)
`cli/bin/lint-packet.ts` (also `bun cli/bin/tdd.ts lint`) is this linter in TypeScript, with Effect (`cli/src/lint`).
It takes the same arguments, prints the same report, writes the same `--json` file byte for byte and exits with the
same codes. The Python stays the documented path until the cutover.

```
bun "$T/cli/bin/tdd.ts" lint <packet-dir> [--build <dir>] [--strict] [--json <out>] [--skip <ids>] [--compile]
```

Three things differ by design:
- **The compile runs in-process.** A packet with no build, or `--compile`, is compiled by the TypeScript compiler
  (`cli/src/compile`) into a temporary folder. The Python linter runs `build_tdd.py` there. Each linter reports its own
  compiler's failure, and the two compilers fail with the same exception text.
- **PARITY is reversed.** The TypeScript compiler runs in-process. `python3` with `build_tdd.py` is the external
  compiler. As in the Python rule, the build is the first reference, and a fresh `build_tdd.py` compile decides whose
  difference it is whenever the build and the TypeScript compile differ. On a compiled lint that fresh compile is the
  reference. Every finding and note is the Python rule's text, so `lint.json` stays identical. Only the skip note
  differs: `python3 is not on PATH` or `build_tdd.py is absent`, where the Python says `bun`, `cli/` or `node_modules`.
- **A linter defect does not end the run.** An exception that is a defect of the TypeScript linter becomes
  `<RULE>/check-failed` (error), and the other rules still run. `--skip <RULE>/check-failed` does not hide it, so a
  broken rule always exits 1, as the Python's traceback does; `--skip <RULE>` leaves the rule out instead. The Python
  linter stops with a traceback there, so this id never appears in its output. `AGENTS/check-failed` (an exception
  raised by `write_agents.check`) is the same in both linters, and `--skip` hides it in both.

An exception the Python linter raises too ends the TypeScript run the same way. This covers a JSON file whose root or
records have the wrong type: `content.json`, `content.files.json`, `packet.json`, `meta.json`, a sibling
`*.files.json` or a hotspots file. There is no report and no `lint.json`, and the exit code is 1. The traceback's last
line (for example `AttributeError: 'list' object has no attribute 'get'`) goes to stderr. Neither linter passes a
corrupt file.

`cli/test/lint` proves the parity. It runs both linters on the packets in `$TDD_PARITY_PACKETS` (on the build and with
`--compile`). It also runs a mutation corpus: a small neutral packet with a git repository, mutated 118 ways plus 7
usage cases, so that every check id this file lists fires at least once in both linters. The PARITY cases run both
linters on a copy of the skill whose `build_tdd.py` differs on purpose. The crash cases compare the exit code, the
missing report and `lint.json`, and the traceback's last line.

## Notes, not findings
- The source was compiled to a temporary folder because the packet had no build.
- PARITY was skipped (no `bun`, no `cli/`, no `cli/node_modules`), passed, found the build stale, or found that `build_tdd.py` fails on the source too.
- The app has not built the page yet.
- `build/agents/` or `write_agents.check` is absent.
- How many citations resolved, and at which ref.
- XDOC had no glossary to compare.

## Proof fixture
`lint_packet.py` was proven on four sets of packets:
- The reference packet (`<slug>`).
- The older-format `<older-slug>` packet, which has no build, so the linter compiled it.
- A mutated copy of the reference packet in a scratch folder.
- Three tiny packets: an empty folder; a build with no manifest, plus a bad schema file and a `write_agents.check`
  that raises; a broken `meta.json`.

Between them, every check id in this file fired at least once. The fixtures are not part of the skill. Rebuild them
from these mutations when the rules change: a `##` without an id; duplicate and gapped D-numbers; a decision without
a status; a done decision without a date; a `#nowhere` link; a bad `refs` family; a shifted hotspot `viewBox` and a
renamed label; a missing hotspot image and table; a bad schema card; a 16 MB mock; a 17 MB page; `rev` as a string; a
stray `prs/pr-xx.md`; a sibling glossary; `sha` set to a commit the clone has; swapped sections in `content.json`.
