---
aw-doc: 1
id: sindri-pr-review-page
kind: spec
title: "Sindri's PR review page, configured per project, mapped against the design docs"
status: draft
repos: [jthorare/agentic-workflow]
---

# Sindri's PR review page, configured per project, mapped against the design docs

**Status:** first draft for the owner, revision 1 · **Date:** 2026-11-04 · **Lane:** ui (this spec), with context (§5, the docs layer's store) and ideas (§4, the convention and the skills) · **Builds on:** the Projects page spec `2026-10-29-sindri-projects-page-design.md` (projects, the card's sheet, desk-class commands), the owned index spec `2026-10-27-sindri-owned-index-design.md` (record store, feeds, "no live calls at query time" §6.1), the PR babysitter spec `2026-10-15-sindri-pr-babysitter-docs-sync-design.md` with `docs/csharp/sindri-pr.md` and `docs/csharp/forge.md` (the forge port), the pages and actions spec `2026-10-29-sindri-pages-actions-design.md` (profile entries saved with one-click approval), `docs/csharp/sindri-profile.md` · **Next:** 3 /autoplan rounds, meta's UX check, the owner's read; then /specToProvenPR takes §12's phases.

Every file path below is one the author opened on main (94ff1714e). Every command, route, event, key and type is marked **existing** or **new**.

This file is itself written in the convention §4 defines (front matter, `REQ-`, `Ph`, `TP-` ids), so the first index run has a real document to itemize.

## The owner's steps: install -> works

Zero lines of YAML for the default. Sindri runs on the owner's laptop, so every path below is a local one.

1. **Nothing to set up for `~/.agentic-workflow`.** The docs layer reads it by default (`context.sources.docs.stateDir`, §5.1). Every plan, TDD and stage map the skills write there shows up within one sync tick.
2. **Add the vault once.** Settings → Docs → **Add a folder** → pick the Obsidian vault folder → **Save and approve**. That's 2 clicks plus the folder picker, the pins pattern (pages and actions spec §3.6). The same key can be written by hand in `profile.yaml` (§5.1).
3. **Open Reviews** (new top-bar tab, `G V`). It lists the open pull requests of the repos your projects name, the ones waiting on your review first.
4. **Open a PR.** The page shows, in the project's panel order: the design-doc map (which phase and requirements this PR claims, what is still uncovered), the test plan rows for them, the diff grouped by layer with lockfiles and generated files collapsed, CI and gate results, evidence, risk notes, and the project's required checks as a ticked or unticked checklist.
5. **Review.** Write comments on diff lines, then press **Comment**, **Approve** or **Request changes**. Sindri posts one review to GitHub through the forge port. Or, on any finding, press **Send as fix task**, and the finding goes to whoever owns the PR (§8.2).

A project with no review settings gets the built-in default (§7.1). Changing a project's panels, groups or checks is the project sheet's **Review settings** (Ph8), or the profile's `review` block (§7).

## 0. Why

The owner reviews many PRs a day across several projects. Each one is written against a design doc: a system-design TDD, a spec, an officeHours plan, a specToProvenPR stage map, or a note in the vault. Today the review happens on GitHub, which knows nothing about those docs. So every review starts with the same hand work: find the doc, find the phase, check which requirements the PR claims, check that the test plan rows ran, check the repo's own rules ("a recipe change shows the Ui tests ran", "a new env var is in `.env.example`"), and skip past lockfile noise.

Sindri is the owner's personal app for all of it (PR reviews, workflows, agent monitoring, docs and connections), running on their laptop. This spec adds three things, built so later pages can reuse them:

1. **A shared docs layer**: the vault and `~/.agentic-workflow`, indexed by strict naming and folder conventions and itemized into sections, phases, requirements and test-plan rows (§4, §5). The review page is its first reader. A Docs page, the Projects sheet and agent sessions read the same records later.
2. **The review module**: a Reviews tab and a per-PR review page whose panels, diff grouping and required checks come from the project's settings (§6, §7).
3. **Actions**: post a review to GitHub, and send a finding as a fix task (§8).

What exists today (checked on main 94ff1714e):

| Need | Today | Where |
|---|---|---|
| A PR's changed files or diff | **None.** `IPullRequests` has `Get`, `CheckLog`, `Push`, `RerunChecks`, `Self`, `Threads`, `Merge`, `Reply`, `React`, `PushBranch`, `Find`, `Open`, `Update`, `Retarget`, `DefaultBranch`, `Digest`, and no files member. `IForge` has the same set. The nearest is scoping's `GitChangeSource` (merge-base diff of a local checkout, 2 MiB cap) | `src/Sindri/Aw.Sindri.Core/Forge/IPullRequests.cs:18-118`, `src/Forge/Aw.Forge.Core/Ports/IForge.cs`, `Aw.Sindri.Core/Scoping/IChangeSource.cs` |
| Submit a review (approve, request changes) | **None.** `Reply` posts one comment per marker. `PullSnapshot.ReviewDecision` reads GitHub's decision but nothing writes one | `IPullRequests.cs:61`, `Aw.Forge.Core/Pulls/PullSnapshot.cs:15` |
| Open PRs per repo | Stored: `github` feed records of kind `pr`, `state` open or draft, by repo. Body text is in the record | `Aw.Sindri.Host/IndexSources/GitHubRecordFeeds.cs:33`, `RecordKind.cs` |
| Local markdown outside a repo | **None.** The only doc feed is `repo-docs`, which reads each profile repo's docs from git at HEAD (globs `docs/**/*.md`, `**/PRD*.md`, `**/TDD*.md`, plus `AGENTS.md`, `README.md`, `CONTEXT.md` anywhere) | `Aw.Context.Infrastructure/Docs/RepoDocsFeed.cs:15-23`, `Aw.Context.Core/Engine/ContextSettings.cs:13-32` |
| Items inside a doc (phase, requirement, test row) | **None.** Record kinds are doc, issue, comment, project, message, code, pr. Link kinds are parent, blocks, duplicates, relates, closes, attachment, review-of, replies-to, in-project, touches | `Aw.Context.Core/Sources/RecordKind.cs`, `Aw.Context.Core/Records/RecordLinkKind.cs` |
| Per-project settings | **None.** A project row has id, Linear key, name, state, links, notes, target, version and times. No repo, no settings. Per-repo settings are the profile's `repos[]` | `Aw.Sindri.Core/Projects/ProjectAttachedRow.cs:19-31`, `Aw.Context.Core/Engine/ProfileRepoEntry.cs:9` |
| Sending work to an agent | An item plus a recipe instance (`POST /items`, `POST /workflow/instances`), the babysitter's comment loop, `ILauncher.Launch`. Nothing pushes free text into a running session. `sindri_request` runs from session to human only | `Features/CreateItem/CreateItemRequest.cs:11`, `Host/Mcp/SindriTools.cs:16-18`, `Core/Launch/ILauncher.cs:19` |
| Doc id schemes | officeHours has them: `REQ-U1`, `REQ-E1`... (EARS), `AC-1`, `TR-1`, `TASK-1` with `reqs:`. system-design numbers PR cards `pr-NN` and section anchors only. specToProvenPR has stage slugs and `#` journey rows, no ids. No validator for any of the three | `skills/officeHours/SKILL.md:200-252,421-448`, `skills/system-design/SKILL.md:43-58`, `skills/specToProvenPR/SKILL.md:42-65` |
| A PR saying which requirements it covers | **None.** `_shared/pr-body.md` sections are Summary, Key decisions, Evidence, Tests, Coverage and Review resolution | `skills/_shared/pr-body.md:7-26` |
| Top bar | Eight tabs: Home, Projects, Workflows, Work, Recipes, Context, Agents, System. Chords `G H/P/W/Q/R/C/A/.`; `J` Jobs and `U` Settings are pages, not tabs; `V` is free | `Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.cs:112-122`, `ShellViewModel.Chords.cs:111-121` |

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **The docs layer is records in the owned index, and itemizing runs on every doc record that opts in**, whichever feed brought it. A repo spec with the front matter (like this file) is itemized exactly like a vault note | One store and one parser. A later Docs page, the Projects sheet and agent sessions read the same records. No second index |
| D2 | **Opt-in is front matter.** A markdown file is a design doc when its front matter has `aw-doc: 1`. Everything else stays a plain doc record, searchable as today, with no items | Strict and checkable. A vault full of unrelated notes stays out of the review page without a deny list |
| D3 | **Items come from ids at the start of a line**: a heading, list item or table row whose first token is an item id (§4.2). No model call, no fuzzy matching | Deterministic. The validator and the indexer run the same parser, so "it passed the check" means "it indexes" |
| D4 | **A PR claims items with a `Covers:` line in its body.** Sindri also suggests items from the issue numbers and branch name (§4.4), always marked "suggested" | The claim is the author's, checkable at PR time. A suggestion is never shown as a claim |
| D5 | **Review settings live in the profile** under a new `review` block: named settings, and per project which repos and which settings (§7). Saved from the app with one-click approval | Required checks guard what the owner approves, so an agent session must not loosen them. Profile changes are approved, and a session's write is refused (`SND-AUTH-002`). Same home as pins and named pages |
| D6 | **Diff from the forge, read live when the page opens**, cached by head sha. Everything else on the page reads stored data | Unlike the Projects page (P1 there), a review must be of the current head. A cache keyed by sha makes reopening free and offline-safe for a head already seen |
| D7 | **Approve and request changes are human-only.** A session can read the review page's data (`sindri review show --json`, `sindri_review`) and draft comments, never post a review verdict | Same rule as `effect.pr.merge` (W23) |
| D8 | **A fix task is a Sindri item linked to the PR, plus a delivery** that depends on who owns the PR (§8.2) | Items are how work reaches agents today. The owner sees every fix task in Work whether or not it was delivered |
| D9 | **Reviews is a ninth top-bar tab, third after Projects** (open question 1), and the review page is a full page, not a sheet | A diff needs the width. The Projects card links into it ("3 to review") |
| D10 | **Required checks in v1 are decided from data Sindri already holds**: a CI check's state, files changed together, a PR body line, or a manual tick (§7.3). Running a command against the PR's code waits for the sandbox | Running PR code on the laptop outside the sandbox is the one thing this page must not do |
| D11 | **Fixtures are made up.** The vault fixture is a fictional project ("Harbor ferry booking"). No work-app names, paths or text anywhere in the repo | Owner rule (app-agnostic toolkit) |
| D12 | **How Sindri reaches the vault: decided 2026-11-04.** Sindri runs on the work laptop, so the vault is a local path set in the profile (`context.sources.docs.vaults`, §5.1). No sync | Owner, 2026-11-04 |

## 2. Goals and non-goals

**Goals**

- **REQ-1** The app has a Reviews tab listing the open PRs of every repo a project names, grouped by project, with the ones requesting the owner's review first.
- **REQ-2** A PR's review page shows the panels its project's settings name, in that order: doc map, test plan, diff, checks (CI and gate), evidence, risk notes, required checks, conversation.
- **REQ-3** The diff is grouped by the project's layer rules. Files matching collapse globs (lockfiles, generated files) are folded with a count, and risky paths are marked with their reason.
- **REQ-4** Each required check shows pass, fail, not applicable or needs a tick, with the reason in words.
- **REQ-5** The owner can post one review (comments on lines, a body, and Comment, Approve or Request changes) to GitHub through the forge port.
- **REQ-6** The owner can send any finding as a fix task, and the page says where it went.
- **REQ-7** `~/.agentic-workflow` and each configured vault are indexed. Docs with the front matter are itemized into sections, phases, requirements (with acceptance criteria) and test-plan rows.
- **REQ-8** The doc map shows the claimed items, the items other PRs claimed (with their PR), and requirements nobody has claimed yet.
- **REQ-9** `sindri doc validate <path>` validates a file or folder against the convention, with the same parser the indexer uses.
- **REQ-10** system-design, specToProvenPR and officeHours write convention-conformant docs every time and run `sindri doc validate` before they finish. PR bodies carry `Covers:`.
- **REQ-11** An agent session can read the same data: `sindri review show <pr> --json`, `sindri doc items <doc> --json`, and the MCP tools `sindri_review` and `sindri_docs`, with doc text fenced as untrusted.

**Success measure.** For two weeks after Ph7, the owner reviews this repo's PRs from the Reviews page. For the PRs whose body has `Covers:`, the owner never has to open the design doc by hand to find the phase (their word at the end). Every required check on those PRs is decided by Sindri, not by a manual tick, except the checks configured as manual.

**Non-goals**

- Merging from the review page. The babysitter's `merge` verb stays where it is.
- Editing docs. The layer reads them. Obsidian and the skills write them.
- Model-written review comments. A later phase could add a judge pass. v1 is the owner's review.
- Docs outside the configured roots and the repos' doc globs.
- Running a check command against the PR's code (D10).

## 3. Where it sits in the app

Sindri's nav today is Home, Projects, Workflows, Work, Recipes, Context, Agents, System (`ShellViewModel.cs:112-122`). Health lives under System, and the workflow views are Workflows (instances), Work (the queue) and Recipes.

| Module | Reads | Writes | Reached from |
|---|---|---|---|
| **Reviews** (new tab, `G V`, deep link `sindri://reviews`) | PR records (stored), the docs layer, review settings | nothing | top bar; a Projects card's "N to review"; a Work item linked to a PR |
| **Review page** (`sindri://review/<host>/<owner>/<repo>/<number>`) | the forge (diff, threads, checks: live, cached by head sha), the docs layer, the babysitter's facts when an instance watches the PR, evidence on disk | review drafts (ledger), a posted review (forge), fix tasks (items) | a Reviews row; a babysitter instance's page ("Review"); a doc item's "claimed by #N" |
| **Docs layer** (no page in v1) | the vault and `~/.agentic-workflow` (new feed), repo docs (existing feed) | records | the review page's doc map; later a Docs tab, the Projects sheet's "Design docs" section, Context's Why view |
| **Projects** (existing) | + the projects' review counts | + Review settings (Ph8) | top bar |

The review page is laid out like a two-pane code review: the panel column on the left (doc map, test plan, required checks, risk notes, evidence, CI) and the diff on the right. At the minimum width the panels stack above the diff. **Mockup first:** Ph7 starts with `/design-mockup` for the Reviews list and the review page at the minimum width and at 1,440 px, and the owner picks before any view is built (the F3 pattern).

## 4. The docs convention (ideas lane, with context)

One page, `docs/conventions/design-docs.md` (new, Ph0), and one JSON schema of the parsed result, `schemas/docs/design-doc.schema.json` (new). The rules:

### 4.1 Where and what

| Root | Path | What lives there |
|---|---|---|
| `aw` | `context.sources.docs.stateDir` (default `~/.agentic-workflow`) | `<repo-slug>/plans/<timestamp>-<slug>/` (officeHours: `plan.md`, `product.md`, `engineering.md`, `design-brief.md`, `TASKS.md`), `<repo-slug>/plans/<epic>/stages.md` (specToProvenPR), `<repo-slug>/proof/<stage>/verification-plan.md`, `<repo-slug>/system-design/<slug>/` (system-design) |
| `<vault name>` | each `context.sources.docs.vaults[].path` | Obsidian notes, any folder layout |
| `repo:<name>` | each profile repo, through the existing `repo-docs` feed | `docs/superpowers/specs/*.md` and other doc globs |

Only `*.md` files are read. Inside the `aw` root only the folder patterns above are read, so tool state (`bin/`, `cache/`, `skill-evals/`, `digests/`, `meta/`, logs) is never indexed. In a vault, `.obsidian/`, `.trash/` and any folder starting with `.` are skipped.

### 4.2 Front matter and item ids

```yaml
---
aw-doc: 1                         # required: the convention version; opts the file in (D2)
id: harbor-booking-tdd            # required: lowercase letters, digits, '-', 3-64; unique within its root
kind: tdd                         # required: tdd | spec | plan | stages | product | engineering | tasks | verification | note
title: "Harbor ferry booking"     # required
status: draft                     # required: draft | review | approved | building | done | superseded
project: prj_01J...               # optional: a Sindri project id, or linear:project/<id>
repos: [acme/harbor-web]          # optional: owner/name
issues: ["#412"]                  # optional: issue refs the PRs will cite ("Part of #412")
supersedes: harbor-booking-v1     # optional: another doc id in the same root
parent: harbor-booking-plan       # optional: the doc this one belongs to (a plan's product.md -> plan.md)
---
```

An **item** is a line whose first token (after `#`s, `- `, `| ` or `**`) matches one of these. Its text is the rest of the line, plus the lines under it up to the next item or heading of the same or higher level.

| Item | Id pattern | Example | Parent |
|---|---|---|---|
| Section | any `##` to `####` heading without an item id; id = the heading's anchor (Obsidian's and GitHub's slug) | `## Booking flow` → `booking-flow` | the heading above it |
| Phase | `Ph<n>` with an optional letter or `-<n>` (`Ph0`, `Ph3b`, `Ph2-1`) | `\| **Ph2** \| ... \|` or `## Ph2 Seat map` | the doc |
| Requirement | `REQ-<n>` or officeHours' `REQ-<U\|E\|S\|O\|W><n>` | `- **REQ-E3** When a sailing is full, ...` | its section |
| Acceptance criterion | `AC-<n>` | `- AC-2 Given ...` | the nearest requirement above, or the one its row names |
| Test-plan row | `TP-<n>` | `\| TP-4 \| Book the last seat \| REQ-E3 \| Ph2 \|` | its section. A row's other cells that hold item ids become `verifies` links (`REQ-E3`, `Ph2`) |
| Task | officeHours' `TASK-<n>` | `## TASK-3: Seat map` | the doc. `reqs:` and `depends:` become links |

The full key is `doc:<root>/<doc id>#<item id>`, e.g. `doc:aw/harbor-booking-tdd#REQ-E3`. Inside one root a doc id is unique. Inside one doc an item id is unique.

**Errors are the validator's and the indexer's alike** (§5.3): a missing required key, a bad id, a duplicate doc id in a root, a duplicate item id in a doc, an `AC` with no requirement above it, a `verifies` cell naming an item the doc doesn't have, a `supersedes` or `parent` naming a doc not in the root. The indexer still stores the doc record with its errors attached and no items, so a broken doc shows up as broken instead of vanishing.

### 4.3 What each skill writes (Ph4)

| Skill | Today | Change |
|---|---|---|
| officeHours | `plan.md` and four domain docs with `REQ-`, `AC-`, `TR-`, `TASK-` ids, no front matter (`SKILL.md:523-564`) | Front matter on all five files (`plan.md` kind `plan`, the others with `parent: <plan id>`). Ids unchanged. The plan's doc id is its folder name without the timestamp |
| system-design | HTML packet: `tdd.html`, `packet.json` from `scripts/write_packet.py`, PR cards `prs/pr-NN.md` (`SKILL.md:43-58`) | `write_packet.py` also writes `doc.md`: front matter (kind `tdd`) and the item lines generated from the packet. Sections from `packet.json` sections, `Ph<NN>` from each PR card, `REQ-<n>` from each EARS acceptance criterion, `TP-<n>` from each card's test rows. `doc.md` is generated (never hand-edited), like `packet.json` |
| specToProvenPR | `plans/<epic>/stages.md` per stage: goal, signal, files, size, status. `proof/<stage>/verification-plan.md` with `#` rows (`SKILL.md:42-65`, `_shared/verification-plan-template.md:9-21`) | `stages.md` gets front matter (kind `stages`, `parent` = the spec's doc id) and each stage a `Ph<n>` heading. The verification plan's journey rows become `TP-<n>` with a `verifies` cell. Every PR body gets `Covers:` |
| `_shared/pr-body.md` | no coverage line | A `Covers:` line right under Summary: `Covers: harbor-booking-tdd#Ph2, harbor-booking-tdd#REQ-E3` (or the full `doc:` key when the root is ambiguous) |

Each of the three skills runs `sindri doc validate "$OUT" --json` as its last step and fixes every finding before it reports done. The skill evals (`skill-evals`) gain one case per skill: the output passes `sindri doc validate`.

### 4.4 Mapping a PR to items

In order. The first that gives anything wins, and the doc map says which one did it.

1. **Claimed**: the PR body's `Covers:` line, every ref resolved against the index. A ref that doesn't resolve is shown as a broken claim (`SND-DOC-021`), never dropped.
2. **Suggested by issue**: the body's `Part of #N` or `Fixes #N` matches a doc's `issues:`. That doc's phases are offered, none pre-picked.
3. **Suggested by branch**: the head branch contains a doc id (`feat/harbor-booking-tdd-ph2` → that doc, `Ph2`).
4. **None**: the doc map panel says "This PR names no design doc" and offers the docs of the PR's project.

The claim is computed at read time from the stored PR record's body. No `covers` link is written, so a body edit shows on the next PR sync and nothing has to be rebuilt.

## 5. The docs layer's store (context lane)

### 5.1 Profile keys (new)

Under the existing `context.sources` block, checked by the engine at validate and approve like the other source keys. Documented in `docs/csharp/sindri-profile.md` (so `scripts/check-profile-doc.mjs` covers them):

```yaml
context:
  sources:
    docs:
      stateDir: ~/.agentic-workflow      # default: $AW_STATE_DIR when set, else ~/.agentic-workflow
      vaults:
        - name: work                      # the root's name: lowercase letters, digits, '-', 1-32; not 'aw', not 'repo'
          path: ~/Documents/WorkVault
          deny: ["Private/**", "Journal/**"]   # optional: vault-relative globs never read (denyPaths rules)
```

- **Paths** follow the profile's path rule: absolute with no `..` (`SandboxRules.AbsolutePathProblem`, `Aw.Sindri.Core/Profile/SandboxRules.cs:128-129`), with a leading `~/` expanded to the owner's home first. A path that doesn't exist, isn't a directory or can't be read is not a validation error (a vault on an unmounted drive): it's a degraded `docs` doctor row and a degraded source on the page, with the path and the reason.
- At most 10 vaults, no name twice, no two roots where one contains the other.
- `stateDir: off` turns the `aw` root off.
- Every change is a re-approval, as for every profile key. The Settings → Docs sheet writes these keys and approves in one click when this is the only waiting change.

### 5.2 The feed (new, `docs` source)

A new `RecordFeed` (`Aw.Context.Core/Sync/RecordFeed.cs:12`) with source id `docs`, in `ContextRecordFeedCatalog` beside `repo-docs` (`ContextRecordFeedCatalog.cs:24-30`):

- Reads the working tree (these roots aren't git repos), the `*.md` files §4.1 allows, minus deny globs and the built-in secret globs.
- One `doc` record per file, keyed `doc:<root>/<relative path>`, version = the file's sha256. A file that's gone is in `RecordPage.Removed`. The cursor is a fingerprint of every file's path, size and mtime, so an unchanged tree is an empty page.
- Text goes through the same scrub and chunking as `repo-docs` (`RecordBatches`).
- Ticks on the scheduler like the other feeds. There's no file watcher in v1, so a change shows within one tick (5 minutes). Open question 5 asks whether that's fast enough.

### 5.3 Itemizing (new, every doc record)

A pure parser in `Aw.Context.Core` (`DesignDocParser`, new) turns a doc's text into `{ front matter, items[], errors[] }`, with the shape `schemas/docs/design-doc.schema.json` defines. The sync driver runs it on every doc record it writes, from either feed (D1). For a doc with `aw-doc: 1`:

- The record gains the front matter's fields, and its key gains an alias `doc:<root>/<doc id>` (the id the convention uses). For repo docs the root is `repo:<repo name>`.
- Each item becomes a record of a **new kind, `item`** (proposed; context decides the name), keyed `<doc key>#<item id>`, with a `parent` link to its parent item or the doc, and **new link kind `verifies`** from a test row to the items its cells name. `TASK` `reqs:` and `depends:` become `relates` and `blocks`.
- Items are rewritten whenever the doc's version changes, and removed with it.

`sindri doc validate <path|dir> [--root <name>] [--json]` (new, local, no serve) runs the same parser on files on disk and the cross-doc rules for the root the path sits in (duplicate ids, `parent`, `supersedes`). The verb is in the Sindri host, which already references Context. Exit 0 no findings, 1 findings, 2 usage or a tool error. Output is like `sindri docs check`'s: one line per finding with its fix, `--json` is `{ findings: [{ rule, file, line, message, fix }], passed }`.

### 5.4 Read path (new)

`IContextDocs` on the context facade (the `IContextProjects` pattern, Projects page spec §6.1): `ListDocs(root?, project?, repo?)`, `GetDoc(key)`, `Items(docKey)`, `Resolve(refs[])` (the `Covers:` refs to items). All of them read the store only. Sindri exposes them as `GET /docs`, `GET /docs/{doc}`, `GET /docs/{doc}/items`, the CLI `sindri doc list|show|items` and the MCP tool `sindri_docs`. Doc and item text in a result is fenced as untrusted, like every record text.

## 6. The review page

### 6.1 The Reviews list

Rows come from stored PR records (`kind = pr`, state `open` or `draft`) in the repos of every `review.projects[]` entry (§7.2), grouped by project. A row shows the title, number, author, age, CI state, how many required checks are open, and whether the PR claims a doc. The order: review requested from the owner, then the rest, newest activity first. PRs by the owner are folded under "Yours". PRs in a repo no project names are under "Other repos", with the built-in settings.

### 6.2 The page model (`GET /reviews/{host}/{owner}/{repo}/{number}`, new)

One response holds every panel's data, built from:

| Panel | Source | Live? |
|---|---|---|
| Doc map | §4.4 mapping. The claimed doc's tree with this PR's items highlighted, other open or merged PRs' claims as "claimed by #N" (from their stored bodies), requirements with no claim marked uncovered | stored |
| Test plan | the `TP` rows that `verify` a claimed item, with the item they verify. Each row has a tick the owner sets while reviewing (kept in the draft, §8.1) | stored |
| Diff | `IPullRequests.Files` (new, §8.3): path, status, additions, deletions, patch. Grouped and folded by §7.1's rules. A file whose patch the forge leaves out (too big) says so, with a link to the file on GitHub | live, cached by head sha |
| CI and gates | `PullView.Checks` (state, failing, passed, pending) and, when a babysitter instance watches the PR, its `fact:prState` outcome and `babysit.jsonl` rows (`docs/csharp/sindri-pr.md`) | live, cached by head sha |
| Evidence | ui-evidence runs whose `summary.json` names the head sha (`$AW_STATE_DIR/ui-evidence/runs/<id>`), verification packs (`$AW_DIR/verification/<run>/pack.json`), a babysitter instance's `proof-pack/`, and the PR body's Evidence section. Ph5 measures which of these carry a head sha today, and those that don't are matched by PR number | stored on disk |
| Risk notes | risky-path hits (§7.1), the PR body's `## Risks` section, the handoff note's `risks` (`schemas/pr/handoff-note.json`) when the line opened the PR | stored |
| Required checks | §7.3, evaluated on this head | from the above |
| Conversation | `IPullRequests.Threads` | live, cached by head sha |

The cache lives under `$AW_SINDRI_HOME/reviews/<host>/<owner>/<repo>/<number>/<sha>/` (owner-only files). A new head drops the old one after the next successful read. With the forge unreachable, the page shows the last cached head with "as of <time>, the PR may have moved", and the post and fix-task buttons stay on (they fail with the forge's code if it's still down).

## 7. Review settings (the profile's new `review` block)

### 7.1 Named settings

```yaml
review:
  settings:
    sindri:                               # a name, used by projects below
      panels: [docMap, requiredChecks, diff, testPlan, ci, evidence, risk, conversation]
      groups:                             # first match wins; unmatched files go to "Other"
        - { name: Core,           globs: ["src/*/*.Core/**"] }
        - { name: Features,       globs: ["src/*/*.Features/**"] }
        - { name: Infrastructure, globs: ["src/*/*.Infrastructure/**"] }
        - { name: Host,           globs: ["src/*/*.Host/**", "src/*/*.App/**"] }
        - { name: UI,             globs: ["src/Sindri/Aw.Sindri.Ui*/**"] }
        - { name: Tests,          globs: ["tests/**"] }
        - { name: Recipes,        globs: ["recipes/**"] }
        - { name: Docs,           globs: ["docs/**", "**/*.md"] }
      collapse: ["**/packages.lock.json", "**/*.g.cs", "**/PublicAPI.*.txt", "tests/**/goldens/**"]
      risky:
        - { globs: [".github/workflows/**"], why: "CI definition: runs with repo secrets" }
        - { globs: ["config/hooks/**"],      why: "Safety hooks: every session runs them" }
      checks: [ ... ]                      # §7.3
```

- `panels` names any of the eight panels, each at most once. Leaving one out hides it.
- Globs follow the `denyPaths` rules (repo-relative, `/` separators, no `..`).
- **The built-in `default`**, used when a project names no settings: all eight panels in the order above, groups by the first two path segments, collapse = common lockfiles (`package-lock.json`, `yarn.lock`, `pnpm-lock.yaml`, `**/packages.lock.json`, `Cargo.lock`, `poetry.lock`, `go.sum`) and `*.min.js`, `*.g.cs`, `**/generated/**`, no risky paths, no checks.

### 7.2 Projects and repos

```yaml
review:
  projects:
    - project: prj_01JABCDEF...          # or linear:project/<id>
      repos: [jthorare/agentic-workflow]
      settings: sindri
```

Which settings a PR gets: (1) the project of the doc the PR claims (`project:` front matter), when that project has an entry. (2) Otherwise the one entry whose `repos` has the PR's repo. (3) With several entries for the repo, the PR is listed under each, and the page asks the owner to pick once per PR (kept in the draft). (4) Otherwise `default`. A project id that isn't on the Projects page is refused at validate (`SND-REVIEW-002`).

### 7.3 Required checks

```yaml
      checks:
        - id: recipe-ui-tests
          title: "A recipe change shows the Ui tests ran"
          when: ["recipes/**/*.yaml"]
          need: { ci: "ui-tests" }                          # a CI check by name, passing on this head
        - id: env-example
          title: "A new env var is in .env.example"
          when: ["src/**/*.cs", "scripts/**/*.sh"]
          need: { ci: "check-env-example" }
        - id: locks-with-csproj
          title: "A csproj change commits its lock files"
          when: ["**/*.csproj"]
          need: { changedWith: ["**/packages.lock.json"] }
        - id: ideas-review
          title: "Ideas reviewed it"
          need: { body: "^reviewed by ideas at [0-9a-f]{7,40}$" }   # a line in the PR body
        - id: owner-ran-it
          title: "I ran it myself"
          need: { manual: true }
```

| `need` | Pass when | Not applicable when |
|---|---|---|
| `ci: <name>` | a check with that name is passing on the head sha | `when` matches no changed file |
| `changedWith: [globs]` | some changed file matches each glob | `when` matches no changed file |
| `body: <regex>` | a line of the PR body matches (.NET regex, 1 s timeout, at most 512 characters) | never. Leave `when` off to always require it |
| `manual: true` | the owner ticked it on this head (a new head unticks it) | `when` matches no changed file |

A `ci` check that's still pending is "running", not failed. The env-var example is `ci` on purpose: deciding "a new env var" is what `scripts/check-env-example.sh` already does, and running it here would run PR code (D10). A `command:` need is reserved: it's refused at validate (`SND-REVIEW-003`) until the sandbox's run-area exec can host it.

## 8. Actions

### 8.1 Drafts

Line comments, the body, test-plan ticks, manual-check ticks and the settings pick are a **draft**, saved as ledger events as the owner types (debounced), so a restart keeps them. New events: `review.draft.changed` (the whole draft, versioned) and `review.draft.discarded`. Both are scrubbed by `Aw.Redaction` like every event text. A draft is keyed by PR and head sha. When the head moves, line comments on lines the new diff no longer has are kept as "outdated" and listed for the owner to move or drop.

### 8.2 Send as fix task

On a line comment, a failed required check or an uncovered requirement: **Send as fix task**. Sindri:

1. Creates an item (`POST /items`, existing): title `Fix: <first line of the finding>`, repo = the PR's repo, body = the finding, the file and line, the head sha and the PR link, `link` = the PR record's key. It shows in Work.
2. Delivers it, by the first of these that holds:
   - **A babysitter instance watches the PR**: the finding is posted as a PR review thread with Sindri's marker. The babysitter's existing comment loop (`comments` outcome, `effect.pr.reply`) then classifies it, fixes it, pushes and replies. The item is closed when the thread's mark is `fixed`.
   - **Otherwise**: the finding is posted as a PR review thread (the durable record every owner of a PR reads), and the item stays open in Work with "Waiting on <author>". Open question 2 asks whether Sindri should also tell `/meta` for lane-owned PRs.
3. The page shows where it went ("Posted on #2071 and given to the babysitter", "Posted on #2071, item 01J... in Work").

### 8.3 Post the review (forge additions, Ph3)

Two new members on `IForge` and Sindri's `IPullRequests`, with the existing contract tests extended (`ForgeContract`, `PullRequestsContract`):

| Member | GitHub | File adapter, `FakeForge` |
|---|---|---|
| `Files(link)` → `PullFile[]` `{ path, previousPath, status, additions, deletions, patch? }` | `GET /repos/{o}/{r}/pulls/{n}/files`, paged by 100, GitHub's cap of 3,000 files. Over the cap, the page says so. `patch` is absent when GitHub omits it | a seeded file list (`seed files`, new `aw-forge-fake` verb) |
| `SubmitReview(link, headSha, verdict, body, comments[], marker)` → `Posted` \| `AlreadyDelivered` \| `HeadMoved`. `verdict` is `comment`, `approve` or `requestChanges`. A comment is `{ path, line, side, body }` | `POST /repos/{o}/{r}/pulls/{n}/reviews` with `commit_id` = `headSha`, `event`, `body` (marker appended with `ForgeMarker.Append`) and `comments`. A 422 for a stale commit is `HeadMoved` | stored in the world. `AlreadyDelivered` for the same marker |

`POST /reviews/{...}/post` (new) takes the draft version and the verdict, sends one `SubmitReview` with marker `sindri.review:<draft id>:<draft version>` (a retry can't post twice), then records `review.posted` (verdict, sha, comment count, the forge's review link) and clears the draft. Its catalog row is human-only (D7): a session gets `SND-AUTH-002`. Text is scrubbed before it's sent. `HeadMoved` keeps the draft and says "The PR moved while you reviewed. Reload to see the new commits; your comments are kept."

CLI: `sindri review list [--project P] [--json]`, `sindri review show <pr-link> [--json]`, `sindri review post <pr-link> --verdict comment|approve|request-changes` (human only), `sindri review fix <pr-link> --text T [--file F --line N]`.

## 9. Errors (new)

| Code | When | Text |
|---|---|---|
| `SND-REVIEW-001` | `review` settings with a bad panel, glob, regex or duplicate id | names the key path |
| `SND-REVIEW-002` | `review.projects[].project` isn't on the Projects page | "No project <id>. `sindri projects` lists them." |
| `SND-REVIEW-003` | a `command:` need | "Command checks wait for the sandbox. Use `ci:` with the CI job that runs it." |
| `SND-REVIEW-010` | posting a draft whose version changed since the page read it | "This review changed in another window. Reload it." |
| `SND-REVIEW-011` | posting when the head moved | §8.3's text |
| `SND-DOC-001` | a `context.sources.docs` key is wrong | names the key path (the engine's `SND-CONTEXT-001` pattern) |
| `SND-DOC-010`..`019` | §4.2's convention errors, one code each (missing key, bad id, duplicate doc id, duplicate item id, orphan `AC`, unknown `verifies` ref, unknown `parent` or `supersedes`) | names the file, line and the fix |
| `SND-DOC-021` | a `Covers:` ref that doesn't resolve | "#2071 claims harbor-booking-tdd#Ph9, and that doc has no Ph9." |

Registered in `docs/csharp/sindri.md`'s error table.

## 10. Security

- **Doc text is untrusted** everywhere it leaves the store: fenced in API and MCP results, never run as instructions, scrubbed before indexing. A vault note can hold anything.
- **Work data stays on the laptop.** The docs layer is the local store. No model call reads doc text in v1 (D3). The existing labeling and decision passes (`context.labeling.enabled`) read doc records like any other record, so a vault's notes reach those model calls once indexed. **The Settings → Docs sheet says so next to Add a folder**, and the vault's `deny` globs keep folders out.
- **Posting is human-only** (D7). Agent sessions read and draft only.
- **No PR code runs** on the laptop for a check (D10).
- **Forge text** (diffs, threads) is untrusted and shown as text, never rendered as markup that loads anything.
- **Fixtures are made up** (D11).

## 11. Docs to update

`docs/csharp/sindri-profile.md` (`context.sources.docs.*`, the `review` block), `docs/csharp/sindri.md` (CLI and error tables), `docs/csharp/sindri-ui.md` (the ninth tab, the minimum width, the review page), `docs/csharp/forge.md` (`Files`, `SubmitReview`, `seed files`), `docs/csharp/sindri-pr.md` (fix tasks given to the babysitter), the owned index docs' feed list (`docs`, itemizing), `planning/ERD.md` (the `item` kind and `verifies` link if they need a migration), `skills/_shared/pr-body.md`, the three skills, `docs/conventions/design-docs.md` (new), AGENTS.md's Commands (`sindri doc …`, `sindri review …`), `src/Sindri/aot-smoke/expected.txt` (the `docs` doctor row).

## 12. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Packages | Depends on | Proof |
|---|---|---|---|---|---|
| **Ph0** | ideas | The convention page, `schemas/docs/design-doc.schema.json`, the made-up fixture vault and `aw` tree (`tests/fixtures/docs/`: a TDD, a plan with domain docs, a stage map with a verification plan, one broken doc per `SND-DOC-01x`, a vault with `.obsidian/` and a private folder) | docs, schemas, fixtures | — | `check-line-schemas`-style schema test over the fixtures (good ones pass, each broken one fails with its rule) |
| **Ph1** | context | `DesignDocParser`, itemizing in the sync driver, the `item` kind and `verifies` link (migration if needed), `IContextDocs` | `Aw.Context.Core`, `.Features`, `.Infrastructure` | Ph0's schema (can start on the draft) | unit: the parser over every fixture, matching the schema's verdicts; integration: a repo-docs sync of a fixture repo itemizes |
| **Ph2** | context | The `docs` feed, `context.sources.docs` keys and rules, the doctor row | `Aw.Context.*`, Sindri profile doc | Ph1 | unit: key rules, path expansion, deny; integration: a tick over the fixture vault, a removed file, an unreadable root |
| **Ph3** | api | `IForge.Files` / `SubmitReview` (GitHub, file, fake), `aw-forge-fake seed files`, `IPullRequests` members | `Aw.Forge.*`, `Aw.Sindri.Core`, `.Host/Forge` | — | contract tests over fake and file; GitHub stubbed-HTTP unit tests; the owner's heavy write contract gains a review |
| **Ph4** | ideas | officeHours, system-design (`doc.md` from `write_packet.py`), specToProvenPR and `_shared/pr-body.md` emit the convention and `Covers:`, each skill runs `sindri doc validate` | `skills/**` | Ph0; Ph5's `doc validate` verb for the skills' last step (a skill can land first with the step behind `command -v`) | skill evals: one case per skill whose output passes `sindri doc validate`; the system-design suites |
| **Ph5** | api | `sindri doc validate|list|show|items`, `GET /docs*`, `sindri_docs`; the `review` profile block and its rules; `GET /reviews`, `GET /reviews/{pr}` (the page model, the cache), checks evaluation, the evidence lookup measured and wired; drafts, `POST .../post`, fix tasks, `sindri review …`, `sindri_review`; catalog rows | `Aw.Sindri.Core`, `.Features`, `.Host` | Ph1 (read path), Ph3 | unit per check kind, per mapping rule, per delivery branch; API goldens; `SND-AUTH-002` for a session's post |
| **Ph6** | ui | Mockups (`/design-mockup`: Reviews list, the review page at the minimum width and 1,440 px, the Docs sheet) and the owner's pick | `docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design/mockups/` | — | the owner's pick, recorded here |
| **Ph7** | ui | The Reviews tab (`G V`, the ninth tab, the window's minimum width measured again), the review page, Settings → Docs, the Projects card's "N to review" | `Aw.Sindri.Ui` | Ph5's goldens (hand-written first), Ph6 | UI tests (nine tabs at the minimum width, chord, deep links, panel order from settings, folded groups, the post and fix-task flows on the fake forge), `GoldenContractTests`, a screenshot run on the fixture |
| **Ph8** | ui | The project sheet's Review settings editor (panels, groups, collapse, risky, checks) writing the profile with one-click approval | `Aw.Sindri.Ui`, profile writer | Ph7 | UI tests; an edit round-trips through `profile approve` |
| **Ph9** | — | The owner proof (§13) and the two-week measure | — | each phase | §13 |

**Parallel plan.** From day one, four lanes are busy: ideas on Ph0 (small, then straight into Ph4), context on Ph1 (it starts from Ph0's draft schema the same day), api on Ph3, ui on Ph6's mockups. Then context goes Ph1 → Ph2, api goes Ph3 → Ph5 once Ph1's read path lands, and ui goes Ph6 → Ph7 against Ph5's hand-written goldens, then Ph8. Ph4's `doc validate` step waits on the Ph5 verb, so Ph4 lands its writers first and adds the check step after.

**Gates per phase.** `-warnaserror` build; the touched projects' unit and integration lanes; `Aw.Architecture.Tests` (new folders and usings in Ph1, Ph3, Ph5, Ph7); scoped format; `check-profile-doc.mjs` (Ph2, Ph5); `check-env-example.sh` (any new variable); the ApiGoldens classes (Ph5); `Aw.Sindri.Ui.Tests` (Ph5 catalog rows, Ph7, Ph8); `bash src/Sindri/aot-smoke/run.sh` with `expected.txt` (Ph2's doctor row); the skill evals for the touched skills (Ph4).

## 13. Proof

| Row | Check |
|---|---|
| **TP-1** | The fixture vault and `aw` tree, indexed on a scratch Sindri home: every good doc's items match the schema's expected list. Each broken doc is stored with its one error and no items. `.obsidian/` and the denied folder are absent. Verifies REQ-7 |
| **TP-2** | `sindri doc validate` over the fixtures gives the same verdicts as the index, file by file. Verifies REQ-9 |
| **TP-3** | A real officeHours, system-design and specToProvenPR run (one eval case each) passes `sindri doc validate`, and the specToProvenPR PR body's `Covers:` resolves. Verifies REQ-10 |
| **TP-4** | On the file forge: a PR with `Covers: harbor-booking-tdd#Ph2` opens on the page with Ph2 highlighted, its `TP` rows listed, a second PR's claim on Ph1 shown as "claimed by #N", and `REQ-E5` marked uncovered. Verifies REQ-2, REQ-8 |
| **TP-5** | The same PR touching `packages.lock.json` and a `.github/workflows` file shows the lock folded with a count and the workflow file marked with its reason. Verifies REQ-3 |
| **TP-6** | Each `need` kind, applicable and not: pass, fail, running, not applicable, manual tick reset by a new head. Verifies REQ-4 |
| **TP-7** | Post Approve with two line comments: one review on the file forge, the marker present. A retry posts nothing. A moved head keeps the draft. An agent session's post is `SND-AUTH-002`. Verifies REQ-5 |
| **TP-8** | Send as fix task with a babysitter instance watching: a thread appears, the babysitter's comment loop takes it, and the item closes on `fixed`. Without one: a thread plus an open item. Verifies REQ-6 |
| **TP-9** | On the owner's laptop: the real vault and `~/.agentic-workflow` indexed, the Reviews tab on this repo's open PRs, one real review posted to a scratch PR. Network off: the page opens from the cache with "as of". Verifies REQ-1, REQ-11 |

## 14. Open questions for the owner

1. **Where Reviews goes.** I'd make it a ninth top-bar tab after Projects (`G V`), and grow the window's minimum width again (the Projects page did the same for eight). The other way is a Reviews section inside each Projects card's sheet, with no new tab. That keeps the bar at eight, but there's no one list of everything waiting on you. Ninth tab?
2. **Fix tasks for PRs a /meta lane owns.** Without a babysitter, a fix task is a PR thread plus a Work item. The lane only sees it when it looks at the PR. Should Sindri also hand it to `/meta` (a new `aw-meta` verb that drops it in meta's inbox for routing, so meta stays the router)? I'd say yes, as a follow-up phase after Ph5. It adds a Sindri → aw-meta CLI edge and only applies when `--with-meta` is installed.
3. **Which PRs the Reviews list shows.** I'd show every open PR in the projects' repos except your own, with review requested from you first. The other way is only PRs requesting your review, which hides lane PRs that never request a reviewer. Every open PR?
4. **Old docs without front matter.** The 74 existing plan folders under `~/.agentic-workflow` (24 with a `plan.md` or `stages.md`) predate the convention. I'd leave them as plain doc records (searchable, no items) and add front matter only to the ones you still review against, by hand or by asking the skill to rewrite them. The other way is a one-off `sindri doc adopt` that adds front matter to every officeHours plan (they already have the ids). Leave them?
5. **How fast a vault edit shows.** v1 picks up a vault change within one sync tick (5 minutes). A file watcher would make it seconds, at the cost of one more long-running piece. Is 5 minutes fine?

Decided: **how Sindri reaches the vault** (owner, 2026-11-04). Sindri runs on the work laptop, so the vault is a local path set in the profile (D12, §5.1). No sync.
