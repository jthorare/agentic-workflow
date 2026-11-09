# Evidence rules

A TDD is only as good as the claims a reviewer can check. Every claim on the page has a source the reader can open: a line of code at a recorded commit, a query with the date it ran, or the owner's words with the date they said them. Anything without one is labelled as a proposal, a default or "not measured". Never fabricate a screenshot, a count, a measurement, a test result, a date or a quote.

## 1 · The commit: sha and date, recorded once

`meta.json` names the commit every code claim was read at:

| Field | What it holds |
|---|---|
| `sha` | 10 hex characters. `new_packet.py` records `origin/HEAD` when that ref exists, else `HEAD`, without fetching. |
| `sha_date` | that commit's date, `git show -s --format=%cs <sha>`. The masthead shows "Evidence at `<sha>` (`<sha_date>`)". |
| `repo_url` | `https://github.com/<owner>/<repo>` (GitHub or GitLab origins only). With `sha`, every citation links to `<repo_url>/blob/<sha>/<path>#L<a>-L<b>`. |
| `repo_path` | where the repo is checked out here; the linter checks citations in it. |

- Read code at the recorded commit, from the remote default branch (system-design's rule): `git -C <repo> fetch`, then `git -C <repo> show <sha>:<path>`, never a stale or dirty checkout. Uncommitted local edits are not evidence.
- If you read at a different commit (after a fetch, or a later revision of the TDD), change `sha` and `sha_date` together, re-check the line numbers you cite, and say so in `status.md`.
- Say the commit once in prose as well, in the Engineering reference group ("Paths are at `<sha>` (`<sha_date>`)"); the template fills it in.

## 2 · Code: `path:line`

Write a citation as inline code: `` `packages/services/src/Holds/Requests.ts:38-103` ``. The compiler (`build_tdd.py`, `CITE`) recognises a path with one of these extensions: ts, tsx, js, mjs, cjs, py, sql, json, md, cs, yaml, yml, toml, rb, go, rs, sh, css, html, txt, graphql, proto; then an optional `:line` or `:from-to`. Each one is recorded in `content.json` `cites` with its section, and linked when `repo_url`, `sha` and a line are all present.

- **Aliases.** Long prefixes get a 2 to 5 capital-letter alias in `meta.json` `path_aliases`: `{"SVC": "packages/services/src"}` lets `` `SVC/Holds/Requests.ts:38` `` resolve to the full path. Name every alias once in prose (the Engineering reference group's summary line is the place).
- **Always a line.** A file without a line or range is a weak citation (`CITES/no-line`, a warning). Cite the narrowest range that shows the claim.
- **Continuations** such as `(:562-791)` right after a citation are plain text: the reader resolves them against the previous path, the linter does not check them.
- **Files the design adds** do not exist at the sha: name them in prose or as a folder (`SVC/HoldRanking/`), not as a `path.ext` citation, which the linter would look for (`CITES/path-unknown`).
- **Short paths** such as `` `Requests.ts:38` `` are dead links: write the full path or an alias (`CITES/short-path`).
- Steps, PR deep dives and the schema reference carry the most citations; the redesign and the front end carry almost none.

## 3 · Counts: dated, with their query

A count is a measured fact: the number, what it counts, the query that produced it, where it ran and the date.

- Save each query as `queries/<name>.sql` (or `.ts`, `.sh`) in the packet. Run it read-only, through whatever read access the project documents (its `AGENTS.md` or rules); writes are out of scope. If no read access is documented, say so in `status.md` and ask the owner rather than guessing a size.
- Record it in `status.md` (Counts): `273 branches with hold approval in production · queries/branches.sql · prod read replica · 2026-11-05`.
- On the page, a count sits in a `cards {stat}` card (the number is the card title; the body says what it counts, the query and the date) or in a table cell whose row names the date. A window ("last 90 days") is part of the count.
- Round only when you say so ("about 18 ms, p50"). `~` needs a basis.
- **Expected** values (projected load, future rows) are a different thing from measured ones: label them expected and give the basis (the measured rate times the new writes per request, say). In the schema lane they go under `expected`, never `measured` (`references/schema-lane.md`).

## 4 · "not measured"

When nobody measured it, the page says **not measured**, in those words. The schema drawer renders an absent measurement the same way; the template's sizing cards start as "not measured". Never fill a gap with a plausible number, a number from memory, or a number from another system.

## 5 · Words and pictures

- **Decisions** quote the owner's words, with the date and who said them (`D2 (2026-11-05, <owner>): "..."`). Only the owner's answers get a D-number; your defaults are lead defaults; open questions are Q-numbers.
- **Requirements** in Rule coverage name their source (PRD section, ticket, thread, contract clause) and the date it was read. Second-hand sources say so ("article numbers are second-hand: no contract text was readable").
- **Screenshots** of today's screens come from the running app; **mocks** are built from the project's real components and captioned as the state they show (`references/mock-conventions.md`). Never pass a mock off as production, or edit a screenshot.
- **Test results** are quoted from the run that produced them, with the command and the date; "not run" when it was not run.

## 6 · Reference tokens must link

Every reference token on the page links to its definition (D5): D-numbers, Q-numbers, rule rows (R1, OS-01), engineering confirmations (E1), facts (F3.2), PRs (PR 7), and every glossary abbreviation. The compiler links a token when the page defines it: a `decision` card (`D4`, `Q1`), a table row whose first cell is the token (`R2`), a PR card (`PR 7`), a glossary term, or a family in `meta.json` `refs` (`{"OS": "#coverage"}`). What it cannot link it lists in `content.json` `refs.unlinked` and prints as a warning; the linter turns that into an error.

## 7 · What the linter checks

`python3 -I "$T/scripts/lint_packet.py" <packet>` (`T` as SKILL.md sets it) reads the source, `meta.json`, `prs/`, `diagrams/`, `mockups/`, `schema/`, the build (`build/content.json`, `content.files.json`) and the repo through read-only git. `references/validators.md` (the lint worker's) is the authority on each rule, its severity and its fix; the ids below are the ones `lint_packet.py` declared on 2026-11-08.

| Rule | Evidence it guards | Checks (id: severity) |
|---|---|---|
| `CITES` | code citations | `no-repo`, `repo-url`, `sha-date`, `sha-unknown`, `no-line` (warn); `short-path`, `path-unknown`, `line-range` (error or warn): each citation resolves in `repo_path` at `meta.sha`, its lines inside the file, `sha_date` matches the commit |
| `REFS` | reference tokens | `unlinked`, `missed-link`, `broken-anchor`, `family`, `hotspot-anchor` (error); `title-token` (warn) |
| `DNUM` | decisions | `bad-id`, `duplicate`, `gap`, `no-status`, `done-no-date` (error): D-numbers unique and gapless from D1, a status on each, a date on each decided one; `none`, `outside-section` (warn) |
| `SCHEMA` | measurements | `measured` (source and date on every measurement), `expected` (a basis on every expectation), `sample-rows`, `column`, `state`, `shape`, `required` (error or warn); `unused` (warn) |
| `META` | the header | `field` (title, an Author, ISO `updated`, integer `rev`, status), `missing`; `stale-build`, `rev-not-moved`, `artifact-url` (warn) |
| `DIAG`, `FILES` | figures and files | every diagram and image exists, hotspots match their SVG, nothing over 15 MB, page under 16 MB |
| `STRUCT`, `AGENTS`, `XDOC` | the page shape, the agents lane, glossary terms across sibling packets | see `references/validators.md` |

The compiler's own warnings (`build_tdd.py`, on stderr) are the first line of the same checks: a missing diagram or image, an unlinked reference token, a hotspot whose anchor or table does not exist, a `refs` family pointing nowhere. `--strict` turns any of them into exit 1.
