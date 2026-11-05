---
aw-doc: 1
id: sindri-pr-review-page
kind: spec
title: "Sindri's PR review page, configured per project, mapped against the design docs"
status: draft
repos: [jthorare/agentic-workflow]
---

# Sindri's PR review page, configured per project, mapped against the design docs

**Status:** draft for the owner, revision 2 (after /autoplan round 1, the context and ideas section reviews, and the owner's answers of 2026-11-04) · **Date:** 2026-11-04 · **Lane:** ui (this spec), with context (§5, the library's store) and ideas (§4, the convention and the skills) · **Builds on:** the Projects page spec `2026-10-29-sindri-projects-page-design.md`, the owned index spec `2026-10-27-sindri-owned-index-design.md`, the PR babysitter spec `2026-10-15-sindri-pr-babysitter-docs-sync-design.md` with `docs/csharp/sindri-pr.md` and `docs/csharp/forge.md`, the pages and actions spec `2026-10-29-sindri-pages-actions-design.md` (profile entries saved with one-click approval), `docs/csharp/sindri-profile.md` · **Next:** /autoplan rounds 2 and 3, meta's UX check, the owner's read; then /specToProvenPR takes §12's phases.

Every file path below is one the author or a reviewing lane opened on main (94ff1714e). Every command, route, event, key and type is marked **existing** or **new**. This file is written in the convention §4 defines, so the first index run has a real document to itemize.

## The owner's steps: install -> works

Sindri runs on the owner's laptop, so every path below is a local one.

1. **Open Reviews** (new, the ninth top-bar tab, `G V`). The first time, it asks for your GitHub login (one field, saved as `review.login` with one-click approval). From then on it lists every open PR you authored and every open PR waiting on your review, in the GitHub repos Sindri already syncs (`context.sources.github.repos`, existing), grouped by project.
2. **Open a PR.** The left column shows the panels in §7.1's default order: required checks, design-doc map, test plan, CI, evidence, risk notes, conversation. The right pane is the diff, grouped by folder, with lockfiles and generated files folded.
3. **Review (release 2).** Comment on diff lines, then press **Comment**, **Approve** or **Request changes**. A confirm sheet shows exactly what will be posted and as which GitHub account (§8.3), then Sindri posts one review. In release 1 the same buttons open the PR on GitHub.
4. **Send as fix task** on any finding: it becomes a Work item, a PR thread, and a note to the agent session working on that PR's branch (§8.2).
5. **Point Sindri at your notes (once, optional).** Settings → Library → **Add a folder** → pick the Obsidian vault → **Save and approve** (2 clicks plus the picker). `~/.agentic-workflow` is read with no setup.

**What the doc map shows on day one.** Only docs with the convention's front matter are read at all, and the owner decided to ignore older ones (§14 Q4). So until the skills write the convention (Ph4a), the doc map shows the doc a PR's issue or branch points to ("suggested"), or nothing. Full claims start with PRs written after Ph4a, when specToProvenPR writes `Covers:`. Ph0 measures how many of this repo's last 20 merged PRs would have mapped.

To change a project's panels, groups or checks, use the project sheet's **Review settings** (Ph9). Until that ships, edit the profile's `review` block (§7).

## 0. Why

The owner reviews many PRs a day across several projects. Each one is written against a design doc: a system-design TDD, a spec, an officeHours plan, a specToProvenPR stage map, or a note in the vault. GitHub knows nothing about those docs, so every review starts with the same hand work:
- find the doc and the phase;
- check which requirements the PR claims, and that the test plan ran;
- check the repo's own rules ("a recipe change shows the Ui tests ran", "a new env var is in `.env.example`");
- skip past lockfile noise.

Sindri is the owner's personal app for all of it: PR reviews, workflows, agent monitoring, docs and connections. This spec adds three parts, built so later pages reuse them:

1. **The library**, a shared docs layer. The vault and `~/.agentic-workflow` are indexed, and docs written to a strict convention are itemized into sections, phases, requirements and test-plan rows (§4, §5). The review page is its first reader. A Library page, the Projects sheet and agent sessions read the same tables later.
2. **The review module**: a Reviews tab, and a review page whose panels, diff grouping and required checks come from per-project settings (§6, §7).
3. **Actions**: post a review to GitHub as the owner, and send a finding as a fix task (§8).

What exists today (checked on main 94ff1714e):

| Need | Today | Where |
|---|---|---|
| A PR's changed files | **None.** `IPullRequests` has `Get`, `CheckLog`, `Push`, `RerunChecks`, `Self`, `Threads`, `Merge`, `Reply`, `React`, `PushBranch`, `Find`, `Open`, `Update`, `Retarget`, `DefaultBranch`, `Digest`. `IForge` has the forge-side equivalents. Neither lists files | `Aw.Sindri.Core/Forge/IPullRequests.cs:18-118`, `src/Forge/Aw.Forge.Core/Ports/IForge.cs` |
| Submit a review | **None.** `Reply` posts one comment per marker. `PullSnapshot.ReviewDecision` reads GitHub's decision, and nothing writes one | `IPullRequests.cs:61`, `Aw.Forge.Core/Pulls/PullSnapshot.cs:15` |
| Per-check results | **Partial.** `PullChecks` is `{State, Sha, Failing[] (names), Passed (count), Pending (count)}`, and `Sha` can be older than the head | `Aw.Sindri.Core/Forge/PullChecks.cs:4-9` |
| Open PRs per repo | Stored: `github` feed records of kind `pr`, state open or draft, with the body in the record. No requested reviewers are stored | `Aw.Sindri.Host/IndexSources/GitHubRecordFeeds.cs:33` |
| Local markdown outside a repo | **None.** `repo-docs` reads each profile repo's doc globs from git at HEAD | `Aw.Context.Infrastructure/Docs/RepoDocsFeed.cs:15-23,110-111` |
| Items inside a doc | **None.** 7 record kinds (doc, issue, comment, project, message, code, pr) and 12 link kinds (parent, blocks, duplicates, relates, closes, attachment, review-of, replies-to, in-project, touches, led-by, known-as) | `Aw.Context.Core/Sources/RecordKind.cs`, `Aw.Context.Core/Records/RecordLinkKind.cs` |
| Per-project settings | **None.** A project row has no repo and no settings | `Aw.Sindri.Core/Projects/ProjectAttachedRow.cs:19-31` |
| Sending work to an agent | An item plus a recipe instance, or the babysitter's comment loop. The `sindri-nudge` SessionStart hook prints one line from `sindri repo status --nudge`. Nothing else reaches a running session | `Features/CreateItem/CreateItemRequest.cs:11`, `config/hooks/sindri-nudge.sh`, `docs/csharp/sindri.md:933` |
| Doc ids and validators | officeHours has `REQ-U1`/`REQ-E1`..., `AC-1`, `TR-1`, `TASK-1`. system-design numbers PR cards `pr-NN`. specToProvenPR has none. None of the three has a validator | `skills/officeHours/SKILL.md:200-252,421-448`, `skills/system-design/SKILL.md:43-58`, `skills/specToProvenPR/SKILL.md:42-65` |
| A PR saying what it covers | **None** in `_shared/pr-body.md` | `skills/_shared/pr-body.md:7-26` |
| Top bar | Eight tabs, chords `G H/P/W/Q/R/C/A/.`. `V` is free | `Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.cs:112-122`, `ShellViewModel.Chords.cs:111-121` |

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **The library is its own tables in the owned index** (`design_doc`, `design_doc_item`, `design_doc_ref`, `design_doc_claim`), filled from the doc records of either doc feed. Items are **not** records | Items as records would be chunked and embedded twice, get one decision-pass model call each (`RecordDecisionRun.cs:107`) and become map nodes (`RecordPass.cs:46`). The context lane's call |
| D2 | **Opt-in is front matter.** A markdown file is a design doc when its front matter has `aw-doc: 1`. In the `aw` root and the vaults, a file without it is **not indexed at all** | Owner, 2026-11-04 (§14 Q4). A vault of unrelated notes never enters the store |
| D3 | **Items come from ids at the start of a heading, a list item or a table row's first cell**, outside code fences and quotes (§4.2). No model call | Deterministic. `sindri library validate` and the indexer run the same parser |
| D4 | **A PR claims items with a `Covers:` line.** An issue or a branch name gives suggestions, always marked "suggested". A claim is checked when the PR is written (`library validate --pr-body`) and is a required-check kind (§7.3) | The claim is the author's, and a broken one is caught before review |
| D5 | **Review settings live in the profile** (`review` block): named settings, plus which repos belong to which project. Saved from the app with one-click approval | Required checks guard what the owner approves, and a session can't change an approved profile |
| D6 | **Settings are picked by the PR's repo.** A doc's project only breaks a tie between projects that share a repo | The PR author writes `Covers:`, so it must not choose which checks apply (security round 1) |
| D7 | **Posting is the owner's, as the owner.** Approve, request changes and comment reviews are human-only and use the owner's own GitHub credential (`review.credential`), never the agent App token the babysitter uses | A review posted by a bot isn't the owner's review, and an App can't approve PRs it opened (arch round 1) |
| D8 | **Drafts are local state, not ledger events.** One file per PR under `$AW_SINDRI_HOME/reviews/`. Only `review.posted` and `review.fix-sent` go to the ledger | The ledger is append-only and sessions can read it, so a debounced draft would leave copies of unposted comments forever (arch round 1) |
| D9 | **The diff, checks and threads are read live when the page opens**, pinned to one head and cached by head sha with a cap | A review must be of the current head |
| D10 | **Two releases.** Release 1 is read-only: the list, the panels, required checks, the doc map, the diff and fix tasks; its review buttons open GitHub. Release 2 adds drafts and posting | Release 1 is useful alone and carries the parts GitHub can't do. Posting is the biggest build and the most like GitHub (product round 1) |
| D11 | **Required checks are decided from data Sindri already holds**: a CI check's state on the head, files changed together, a PR body line (shown as "claimed in body"), a resolvable `Covers:`, or a manual tick. No PR code runs | Running PR code on the laptop outside the sandbox is the one thing this page must not do |
| D12 | **How Sindri reaches the vault: decided 2026-11-04.** Sindri runs on the work laptop, so the vault is a local path in the profile (`context.sources.library.vaults`, §5.1). No sync | Owner, 2026-11-04 |
| D13 | **Library text reaches no model unless the owner turns it on per root.** The decision pass and the labeling pass skip the `library` source (the `FeedDecisions.cs:30` skip). `models: true` on a root opts it in | Work notes stay on the laptop by default (security and context round 1) |
| D14 | **Reviews is the ninth top-bar tab, third after Projects**, and the review page is a full page | Owner, 2026-11-04 (§14 Q1) |
| D15 | **A fix task goes to the lane that owns the PR, never through `/meta`** (§8.2) | Owner, 2026-11-04 (§14 Q2) |
| D16 | **Fixtures are made up** ("Harbor ferry booking"). No work-app names, paths or text | Owner rule |

## 2. Goals and non-goals

**Goals**

- **REQ-1** Reviews lists the open PRs the owner authored and the open PRs requesting the owner's review, in the synced GitHub repos. They are grouped by project, then by repo, newest activity first, with "Waiting on you" first.
- **REQ-2** A review page shows the panels its settings name, in that order, in the left column, with the diff in the right pane.
- **REQ-3** The diff is grouped by the settings' groups. Files matching the collapse globs fold with a count, and risky paths are marked with their reason.
- **REQ-4** Each required check shows pass, fail, running, claimed in body, not applicable or needs a tick, with the reason in words.
- **REQ-5** (release 2) The owner posts one review (line comments, a body, a verdict) to GitHub as themselves, after a confirm sheet.
- **REQ-6** The owner can send any finding as a fix task. It reaches the session working on the PR's branch, and the page says where it went, including a half-done send.
- **REQ-7** Front-matter docs in `~/.agentic-workflow` and each configured vault are indexed and itemized into sections, phases, requirements with acceptance criteria, test-plan rows and tasks.
- **REQ-8** The doc map shows this PR's claims and suggestions, other PRs' claims with their PR, and requirements nobody claimed.
- **REQ-9** `sindri library validate` checks a file, a folder or a PR body against the convention, using the indexer's parser.
- **REQ-10** officeHours, system-design and specToProvenPR write conformant docs every time, and specToProvenPR's PR bodies carry `Covers:`.
- **REQ-11** Agent sessions read the same data and may suggest comments. They never tick, post or approve (§8.4).
- **REQ-12** Broken docs are visible: `sindri library list --errors`, a doctor row, and a count on the Reviews header.
- **REQ-13** The page works by keyboard alone and with a screen reader (§6.4).

**Success measure.**
- **Baseline (Ph0).** Of the last 20 merged PRs in this repo, how many a doc would have mapped by issue or branch, and how long the owner's reviews take today by their own estimate.
- **Trial.** For two weeks after release 1, the owner reviews this repo's PRs from Reviews.
- **Target.** For PRs with `Covers:`, the owner never opens the design doc by hand to find the phase (their word at the end, against the baseline). At least 80% of required checks are decided by Sindri, not by a manual tick.

**Non-goals**
- Merging from the page. The babysitter's `merge` verb stays where it is.
- Editing docs.
- Model-written review comments.
- Docs outside the roots and the repos' doc globs.
- Indexing docs without front matter in the `aw` root or a vault.
- Running check commands (D11).
- A Library page in v1.

## 3. Where it sits in the app

Sindri's nav today is Home, Projects, Workflows, Work, Recipes, Context, Agents and System. Health lives under System, and the workflow views are Workflows, Work and Recipes.

| Module | Reads | Writes | Reached from |
|---|---|---|---|
| **Reviews** (new tab, `G V`, `sindri://reviews`) | PR records (stored), the library, review settings | `review.login` (once) | the top bar; a Projects card's "N open PRs"; a Work item linked to a PR |
| **Review page** (`sindri://review/<owner>/<repo>/<number>`) | the forge (live, cached by head), the library, the babysitter's facts when an instance watches the PR, evidence on disk, the draft | the draft (a local file), a posted review (forge), fix tasks (item, forge, session nudge) | a Reviews row; a babysitter instance ("Review"); a doc item's "claimed by #N" |
| **Library** (no page in v1) | front-matter docs in the vaults and `~/.agentic-workflow` (new `library` feed), repo docs (existing feed) | the library tables | the review page; Settings → Library; later a Library tab, the Projects sheet's "Design docs" and Context's Why view |
| **Projects** (existing) | + open PR counts per project | + Review settings (Ph9) | the top bar |

**Mockup first.** Ph6 runs `/design-mockup` for the Reviews list, the review page and Settings → Library. The review page is mocked at the minimum width and at 1,440 px, in each §6.3 state. The owner picks before any view is built.

## 4. The docs convention (ideas lane, with context)

One page, `docs/conventions/design-docs.md` (new, Ph0), and one schema of the parsed result, `schemas/library/design-doc.schema.json` (new).

### 4.1 Roots and what is read

| Root | Path | Read |
|---|---|---|
| `aw` | `context.sources.library.stateDir` (default `~/.agentic-workflow`) | Only `*.md` files with `aw-doc: 1`, under `<repo-slug>/plans/**`, `<repo-slug>/proof/*/` or `<repo-slug>/system-design/*/`. Tool state (`bin*/`, `cache/`, `skill-evals/`, `digests/`, `meta/`, logs) is never opened. system-design's `prs/pr-NN.md` cards have no front matter, so they aren't read |
| `<vault name>` | each `context.sources.library.vaults[].path` | Every `*.md` with `aw-doc: 1`, minus `.obsidian/`, `.trash/`, any dot folder, and the vault's `deny` globs |
| `repo:<name>` | each profile repo, through the existing `repo-docs` feed | Its doc globs, unchanged (`docs/**/*.md` covers `docs/superpowers/specs/`). Front-matter docs among them are itemized too |

- A file's first 4 KiB decide whether it's read: no `aw-doc: 1` front matter means it is skipped before any text is stored.
- Symlinks are not followed.
- A front-matter file over 1 MiB is stored as a doc with the error "too large" and no items.

### 4.2 Front matter and item ids

```yaml
---
aw-doc: 1                         # required: convention version; opts the file in (D2)
id: harbor-booking-tdd            # required: lowercase letters, digits, '-', 3-64
kind: tdd                         # required: tdd | spec | plan | stages | product | engineering | design | tasks | verification | note
title: "Harbor ferry booking"     # required
status: draft                     # required: draft | review | approved | building | done | superseded
project: prj_01J...               # optional: a Sindri project id, or linear:project/<id>
repos: [acme/harbor-web]          # optional: owner/name; exactly one decides who may read it (§5.4)
issues: [412, 415]                # optional: issue numbers (plain numbers, so Obsidian's Properties editor keeps them)
supersedes: harbor-booking-v1     # optional: a doc id in the same scope
parent: harbor-booking-plan       # optional: the doc this one belongs to
generated: write_packet.py        # only on generated files, with source-sha256 (below)
source-sha256: 3f2a...
---
```

**Doc ids are unique per scope.** In the `aw` root the scope is the root plus the repo-slug (`doc:aw/<slug>/<id>`). In a vault or a repo it's the root. A second doc with a taken id is stored with `SND-LIB-012` and no items. The doc at the lowest path keeps the id, so a replay gives the same answer.

**Where items are read.** Only from:
1. headings `##` to `####`;
2. list items (`-`, `*`, `1.`);
3. a table row's **first cell**.

A bold lead-in (`**REQ-3**`) is allowed inside those, never on a bare paragraph. Nothing inside a ``` or ~~~ fence, or a `>` quote or callout, is read. An item's text is its line plus the lines under it, up to the next item or heading of the same or higher level.

**Each id kind is defined in one set of doc kinds.** A matching line in any other doc kind is a reference, not a definition:

| Item | Id | Defined in kinds | Elsewhere |
|---|---|---|---|
| Section | A heading with no id. Its id is its anchor (Obsidian's and GitHub's slug) | all | — |
| Phase | `Ph<n>` with an optional letter or `-<n>` (`Ph0`, `Ph3b`, `Ph2-1`). No leading zeros; a ref to `Ph01` matches `Ph1` | `spec`, `tdd`, `stages` | ref |
| Requirement | `REQ-<n>` or `REQ-<U\|E\|S\|O\|W><n>` | `spec`, `tdd`, `product` | ref (`relates`) |
| Acceptance criterion | `AC-<n>`, a child of the nearest requirement above it | `spec`, `tdd`, `product` | ref |
| Test-plan row | `TP-<n>`. The row's other cells that hold ids become `verifies` refs | `spec`, `tdd`, `verification` | ref |
| Task | `TASK-<n>`. Its `reqs:` and `depends:` become `relates` and `blocks` refs | `tasks` | ref |

officeHours' `TR-<n>` and `ADR-<n>` stay plain text on purpose: nothing claims or verifies them.

The full ref form is `doc:<root>[/<slug>]/<doc id>#<item id>`. A ref inside the same scope may be just `<doc id>#<item id>`. This is ref syntax only, never a stored key.

**Errors.** Each has its own code (`SND-LIB-010`..`019`, §9):
- a missing required key or a bad id;
- a duplicate doc id, or a duplicate item id in one doc;
- an `AC` with no requirement above it;
- a ref to an item the scope doesn't have;
- an unknown `parent` or `supersedes`;
- a hand-edited generated file (its `source-sha256` no longer matches its source);
- "too large", and "blocked" (the scrubber hit).

A doc with errors is stored with them and without items, so it shows as broken instead of vanishing.

### 4.3 What each skill writes (Ph4a)

| Skill | Change |
|---|---|
| officeHours | Front matter on `plan.md` (kind `plan`) and on the domain docs (`product`, `engineering`, `design`, `tasks`, each with `parent`). Ids unchanged. The doc id is the folder slug. **A rerun on the same slug** writes the new folder's docs with `supersedes: <old id>` and a numeric suffix on the id (`harbor-booking-2`) |
| system-design | `write_packet.py` also writes `doc.md`. Its front matter has `generated: write_packet.py`, the `source-sha256` of `packet.json`, and an id from `packet.json`'s slug (never the title). Its items are generated from the packet: sections, a `Ph<n>` per PR card (no leading zeros), a `REQ-<n>` per EARS criterion, a `TP-<n>` per card test row |
| specToProvenPR | `stages.md` gets front matter (kind `stages`, `parent` = the spec's doc id) and a `Ph<n>` heading per stage. The verification plan gets front matter, and its journey rows become `TP-<n>` with a `verifies` cell. Before `gh pr create`, it runs `sindri library validate --pr-body <body.md>` and fixes what it finds |
| `_shared/pr-body.md` | A `Covers:` line under Summary, e.g. `Covers: harbor-booking-tdd#Ph2, harbor-booking-tdd#REQ-E3` |

Each skill runs `sindri library validate "$OUT" --json` as its last step when `sindri` is on PATH.

**Evals (Ph4b).**
- **A new grader.** `type: command`, host-side in `scripts/skill-evals.sh` (the api lane's). It runs after the case, on the staged output, with the published `sindri library validate --json`, and passes on exit 0. It gets a selftest pass row and fail row.
- **Why a host-side grader.** The eval box has no `sindri` on PATH (#1937), so the skill's own validate step skips inside a run, and the grader does the check instead.
- **officeHours** pre-fills the interview answers in `case.yaml`.
- Paid runs go through the spend line.

### 4.4 Mapping a PR to items

1. **Claimed**: the `Covers:` refs, resolved. A ref that doesn't resolve is a broken claim (`SND-LIB-021`). On a merged PR whose doc changed later, it shows as "stale claim", not as an error.
2. **Suggested by issue**: a `Part of #N` or `Fixes #N` in the body matches the `issues:` of a doc whose `repos:` names the PR's repo. That doc's phases are offered, none pre-picked.
3. **Suggested by branch**: the head branch contains a doc id.
4. **None**: "This PR names no design doc", plus the docs of the PR's project.

Claims are parsed when a PR record is written (the `github` feed already stores the body) into `design_doc_claim` (§5.3). So the doc map's "claimed by #N" is one indexed read, not a scan of every PR body.

## 5. The library's store (context lane)

### 5.1 Profile keys (new)

The keys sit under `context.sources` and the engine checks them at validate and approve, like the other source keys. They're documented in `docs/csharp/sindri-profile.md`, so `check-profile-doc.mjs` covers them. They're named `library` to stay clear of the existing `context.docs.*` keys and of `sindri docs check`.

```yaml
context:
  sources:
    library:
      stateDir: ~/.agentic-workflow      # default: $AW_STATE_DIR when set, else ~/.agentic-workflow; 'off' turns the root off
      models: false                       # default false: aw docs reach no model call (D13)
      vaults:
        - name: work                      # lowercase letters, digits, '-', 1-32; not 'aw', not starting 'repo'
          path: ~/Documents/WorkVault
          deny: ["Private/**", "Journal/**"]   # optional, denyPaths rules
          models: false                   # default false
```

- **Paths** must be absolute with no `..`, with a leading `~/` expanded to the owner's home first. The rule is the same as for `sandbox.fs.denyExtra` (`SandboxRules.cs:128-129`, made shared). A path that doesn't exist or can't be read (an unmounted drive) is not a validation error: it's a degraded `library` doctor row with the path and the reason.
- **Limits**: at most 10 vaults, no name twice, and no root inside another.
- **Approval**: every change is a re-approval. Settings → Library writes these keys, and approves in one click when it's the only waiting change.

### 5.2 The feed (new, `library` source)

A `RecordFeed` (`Aw.Context.Core/Sync/RecordFeed.cs:12`) with source id `library`, in `ContextRecordFeedCatalog` beside `repo-docs`. Today `ContextRecordFeedCatalog.For` returns no feeds when the profile has no repos (`ContextRecordFeedCatalog.cs:19-22`). Ph2 changes that, so the library still runs.

- **What it reads**: the working tree, only the files §4.1 admits, minus the deny globs and the built-in secret globs.
- **Records**: one `doc` record per file, keyed `doc:<root>/<path>`, whose version is the file's sha256. A file that's gone, or that lost its front matter, is in `RecordPage.Removed`.
- **Cursor**: a fingerprint of every candidate file's path, size and mtime, **plus the parser version**. An unchanged tree is an empty page, and a new parser re-reads everything.
- **Text** goes through the same scrub and chunking as `repo-docs`.
- **Timing**: it ticks with the other feeds, so a change shows within one tick (5 minutes, accepted by the owner 2026-11-04). `sindri library sync` (new) runs the tick now.

### 5.3 Itemizing and tables (new, migration 0030)

`DesignDocParser` (new, `Aw.Context.Core`) is pure, with no store dependency, so `library validate` can run it outside serve. It turns a doc's cleaned text into `{front matter, items[], refs[], errors[]}`, the shape the schema defines. It runs in `RecordBatches.From`, the one place both doc feeds' text is cleaned. `RecordWrite` carries the parsed doc, and `IRecordStore.Commit` replaces the doc's rows in the same transaction:

| Table | Columns |
|---|---|
| `design_doc` | `doc_key` PK (FK to `source_record`, cascade), `root`, `scope`, `doc_id`, `kind`, `status`, `project`, `repos` json, `issues` json, `parser_version`, `errors` json; UNIQUE(`scope`, `doc_id`) |
| `design_doc_item` | `doc_key` (cascade), `item_id`, `item_kind`, `parent_id`, `line_start`, `line_end`, `text` (capped at 4,000 chars, taken from the doc's scrubbed text); PK(`doc_key`, `item_id`); at most 2,000 per doc |
| `design_doc_ref` | `doc_key`, `from_item`, `to_ref`, `kind` (`verifies`, `relates`, `blocks`) |
| `design_doc_claim` | `pr_key` (the PR record, cascade), `ref`, `resolved_doc_key`, `resolved_item_id` (null when broken) |

- **Blocked docs.** A doc the scrubber blocks gets no items and the error "blocked".
- **Backfill.** `RepoDocsFeed` re-reads a doc only when its blob or the ruleset changed (`RepoDocsFeed.cs:110-111`). The parser version joins that check, so every repo spec that already has `aw-doc: 1` (this one included) is itemized on the first tick after upgrade.
- **ERD.** `planning/ERD.md` gets the four tables.

### 5.4 Who may read it

A record holds one audience key, and a null key means owner-only (`AudienceScope.cs:19`, `ProfileAudiencePolicy.cs:20`).
- **One repo**: a library doc whose `repos:` has **exactly one** entry, matching a `context.sources.github.repos` entry, gets that repo's audience (`ProfileAudiencePolicy.OwnRepo`, as `repo-docs` does).
- **Any other library doc** is owner-only.
- **Stated v1 limit**: a doc meant for two repos is invisible to sessions in both.

### 5.5 Read path (new)

`IContextLibrary` on the context facade, following the `IContextProjects` pattern:
- `ListDocs(root?, project?, repo?, errorsOnly?)`, `GetDoc(key)`, `Items(docKey)`;
- `Resolve(refs[])`, `Claims(prKey)`, `ClaimsOn(docKey)`.

All of them read the store only. Sindri exposes them through:
- the routes `GET /library`, `GET /library/{doc}` and `GET /library/{doc}/items`;
- the CLI `sindri library list [--errors]|show|items|validate|sync`;
- the MCP tool `sindri_library`.

Doc text in results is fenced as untrusted.

**`sindri library validate`.**
- Usage: `sindri library validate <path|dir> [--root <name>] [--pr-body <file> --repo <owner/name>] [--json]`.
- Files and folders are checked locally, with no serve.
- `--pr-body` reads the store to resolve refs, so it needs serve, and says so when serve is down.
- Exit 0 no findings, 1 findings, 2 usage or a tool error.
- `--json` prints `{ findings: [{ rule, file, line, message, fix }], passed }`.

### 5.6 PR records gain author and requested reviewers (context lane, Ph2b)

The Reviews list needs, per open PR, its author login and requested reviewer logins (§6.1). The `github` feed's PR mapping (`Host/IndexSources/ConnectorFeed.cs`, `GitHubPullDetail.cs`) adds both fields, from data the PR read already returns. Until Ph2b lands, the list shows only the PRs the owner authored, with a line saying review requests come later.

## 6. The review page

### 6.1 The Reviews list

Rows are stored PR records (kind `pr`, open or draft) of the repos in `context.sources.github.repos`.
- **Which PRs**: the author is `review.login`, or `review.login` is among the requested reviewers (owner, 2026-11-04, §14 Q3).
- **Order**: two groups, **Waiting on you** (review requested) first, then **Yours**. Inside each, rows are grouped by the project whose `review.projects[]` names the repo, else by the repo's name, newest activity first.
- **A row** shows the title, number, author, age, CI state, open required checks, and whether the PR claims a doc.
- **The header** shows the library's state, e.g. "Library: 41 docs, 3 with errors"; the count opens a sheet listing the broken docs and their errors.

### 6.2 The page model (`GET /reviews/{owner}/{repo}/{number}`, new)

- **Who it serves**: only repos in `context.sources.github.repos`, on the forge host in `workflow.forge.hosts` (existing). Anything else is `SND-REVIEW-004` before any call.
- **Paths**: path segments are checked against GitHub's name rules before they build a cache path.

| Panel | Source | Live? |
|---|---|---|
| `requiredChecks` | §7.3, on this head | from the rest |
| `docMap` | §4.4: the claimed doc's tree with this PR's items highlighted, other PRs' claims as "claimed by #N", unclaimed requirements marked | stored |
| `testPlan` | the `TP` rows that `verify` a claimed item. The owner ticks rows while reviewing (kept in the draft) | stored |
| `ci` | per-check runs on the head (§8.3's addition to `Get`), plus the babysitter's `fact:prState` outcome and `babysit.jsonl` rows when an instance watches the PR | live |
| `evidence` | ui-evidence runs whose `summary.json` names the head sha, verification packs, a babysitter instance's `proof-pack/`, the PR body's Evidence section. Ph5 measures which of these carry a head sha; the others match by PR number | stored on disk |
| `risk` | risky-path hits (§7.1), the body's `## Risks`, the handoff note's `risks` | stored |
| `conversation` | `IPullRequests.Threads` | live |

**The diff** is always the right pane, not a panel. It comes from `IPullRequests.Files` (new), grouped and folded by §7.1. A file whose patch the forge leaves out says so, with a link to the file on GitHub.

**One head.** The page reads `Get` first, then `Files` and `Threads`, then `Get` again. If the head moved in between, it reads once more. After a second move it shows the newest reads with "The PR is moving; reload when it settles."

**Cache.** Under `$AW_SINDRI_HOME/reviews/<owner>/<repo>/<number>/<sha>/`, in owner-only files.
- The last 3 heads per PR are kept.
- A PR's cache is dropped 7 days after it merges or closes.
- The whole cache is capped at 500 MB, dropping the oldest PR first.

### 6.3 States

| Where | Loading | Empty | Error |
|---|---|---|---|
| Reviews list | skeleton rows | "Nothing waiting on you and no open PRs of yours." plus where repos are set (Settings → Context sources). With no `review.login`: the one-field login prompt | "PR list is from <time>: <source> failed: <reason>", above the last rows |
| Each panel | a one-line placeholder in the panel's place, so the column doesn't jump | per panel: "No required checks for this project" / "This PR names no design doc" / "No test plan rows for the claimed items" / "No evidence found for <sha7>" / "No risky paths touched" | the panel says which source failed; the other panels stay |
| Diff | "Reading 42 files…" | "This PR changes no files" | "Couldn't read the diff: <forge reason>". Offline with a cached head: the cached diff, "as of <time>, the PR may have moved" |

### 6.4 Keyboard and screen readers

- **Focus order**: the panel column top to bottom, then the diff, then the action bar. `F6` moves between the three regions.
- **Diff keys**: `j`/`k` next and previous file, `n`/`p` next and previous hunk, `c` comment on the focused line, `o` fold or unfold a group, `Enter` on a folded group opens it. `?` lists the keys.
- **Names**: every control has an `AutomationProperties.Name`. A diff line reads as "line 42, added: <text>". A required check reads its state and reason ("Recipe change shows Ui tests ran: fail, ui-tests failed on a1b2c3d").
- **Never color alone**: added and removed lines carry `+` and `-`, and check states carry an icon and a word. Colors are the existing tokens (`Brush.Success`, `Brush.Attention`, `Brush.Error`), whose contrast the UI spec already checks.
- **Tests**: Ph7's UI tests cover the focus order, the keys and the names.

## 7. Review settings (the profile's new `review` block)

### 7.1 Named settings

```yaml
review:
  login: joi-example                        # the owner's GitHub login: which PRs are "yours" and "waiting on you"
  credential: env:AW_REVIEW_GITHUB_TOKEN    # the owner's own GitHub token for posting (D7); env:, file:, keychain:, op:
  settings:
    sindri:
      extends: default                      # optional; every key not set is taken from it
      panels: [requiredChecks, docMap, testPlan, ci, evidence, risk, conversation]
      groups:                               # first match wins; unmatched files go to "Other"
        - { name: Core,           globs: ["src/*/*.Core/**"] }
        - { name: Features,       globs: ["src/*/*.Features/**"] }
        - { name: Infrastructure, globs: ["src/*/*.Infrastructure/**"] }
        - { name: Host,           globs: ["src/*/*.Host/**", "src/*/*.App/**"] }
        - { name: UI,             globs: ["src/Sindri/Aw.Sindri.Ui*/**"] }
        - { name: Tests,          globs: ["tests/**"] }
        - { name: Recipes,        globs: ["recipes/**"] }
        - { name: Docs,           globs: ["docs/**", "**/*.md"] }
      collapse: ["**/packages.lock.json", "**/*.g.cs", "**/PublicAPI.*.txt"]
      risky:
        - { globs: [".github/workflows/**"], why: "CI definition: runs with repo secrets" }
        - { globs: ["config/hooks/**"],      why: "Safety hooks: every session runs them" }
      checks: [ ... ]                        # §7.3
```

- **The built-in `default`**, the one place the default order is written:
  - panels: `requiredChecks, docMap, testPlan, ci, evidence, risk, conversation`;
  - groups: by the first two path segments;
  - collapse: `package-lock.json`, `yarn.lock`, `pnpm-lock.yaml`, `**/packages.lock.json`, `Cargo.lock`, `poetry.lock`, `go.sum`, `*.min.js`, `*.g.cs`, `**/generated/**`;
  - no risky paths and no checks.
- **`extends`** names `default` or another settings entry, with no cycles. Lists replace the inherited list rather than merging with it. A `settings:` name that doesn't exist is `SND-REVIEW-001`.
- **Globs** follow the `denyPaths` rules. `panels` names each panel at most once, and leaving one out hides it.
- **`review.login`** is a GitHub login (GitHub's name rules). It's set by the Reviews tab's first-run field. When `review.credential` is set, `sindri doctor` warns if `Self` answers a different login.
- **`review.credential`** is a secret pointer like the other tokens, never a value, and it's used only to post (§8.3). Without it, release 2's post buttons open GitHub instead.

### 7.2 Projects and repos

```yaml
review:
  projects:
    - project: prj_01JABCDEF...          # or linear:project/<id>
      repos: [jthorare/agentic-workflow]
      settings: sindri
```

A PR gets its settings by **repo** (D6):
1. The one entry naming its repo.
2. With several, the entry whose project the claimed doc names breaks the tie; failing that, the page asks the owner to pick, and the pick is kept in the draft.
3. With none, `default`.

A project not on the Projects page is `SND-REVIEW-002`. A repo not in `context.sources.github.repos` is `SND-REVIEW-005`, since no PR records would come from it.

### 7.3 Required checks

```yaml
      checks:
        - id: recipe-ui-tests
          title: "A recipe change shows the Ui tests ran"
          when: ["recipes/**/*.yaml"]
          need: { ci: "ui-tests" }
        - id: env-example
          title: "A new env var is in .env.example"
          when: ["src/**/*.cs", "scripts/**/*.sh"]
          need: { ci: "check-env-example" }
        - id: locks-with-csproj
          title: "A csproj change commits its lock files"
          when: ["**/*.csproj"]
          need: { changedWith: ["**/packages.lock.json"] }
        - id: covers
          title: "Names the design doc it builds"
          need: { covers: true }
        - id: ideas-review
          title: "Ideas reviewed it"
          need: { body: "^reviewed by ideas at [0-9a-f]{7,40}$" }
        - id: owner-ran-it
          title: "I ran it myself"
          need: { manual: true }
```

`when` is a list of globs. A check applies when any changed file matches any of its globs, and a check with no `when` always applies.

| `need` | Shows | Pass when |
|---|---|---|
| `ci: <name>` | pass / fail / running | The check run with exactly that name (case-sensitive) succeeded **on the head sha**. A run on an older sha shows "running" until the head's run arrives |
| `changedWith: [globs]` | pass / fail | Some changed file matches each glob |
| `covers: true` | pass / fail | The body has a `Covers:` line and every ref resolves |
| `body: <regex>` | **claimed in body** / fail | A body line matches (.NET regex, 1 s timeout, ≤ 512 chars). Anyone who can edit the body can write that line, so it never shows as "pass" |
| `manual: true` | needs a tick / ticked | The owner ticked it on this head. A new head unticks it |

A `command:` need is refused (`SND-REVIEW-003`) until the sandbox can run it.

## 8. Actions

### 8.1 Drafts (release 2)

The draft holds the line comments, the body, the test-plan ticks, the manual ticks and the settings pick.
- **Storage**: one owner-only file per PR, `$AW_SINDRI_HOME/reviews/<owner>/<repo>/<number>/draft.json`, written atomically as the owner types (debounced), keyed by head sha and versioned. It is not in the ledger (D8).
- **When the head moves**: comments on lines the new diff no longer has are kept as "outdated", and listed for the owner to move or drop.
- **Discarding** deletes the file.

### 8.2 Send as fix task (release 1)

**Send as fix task** is on every line comment, failed required check and unclaimed requirement. It goes to the lane that owns the PR, never through `/meta` (owner, 2026-11-04, D15). Pressing it:

1. **Creates an item** (`POST /items`, existing):
   - title `Fix: <first line>`, and the PR's repo;
   - body: the finding (fenced as quoted text), the file and line, the head sha and the PR link;
   - `link` = the PR record key.
2. **Posts the finding as a PR thread** with Sindri's marker, through `SubmitReview` with verdict `comment` and one line comment (§8.3). It uses the forge credential the babysitter uses, because it's Sindri's note, not the owner's review.
3. **Reaches the owning lane.** The owner of a PR is found in this order:
   - **a babysitter instance watching the PR**: its comment loop picks up the thread, fixes it, pushes and replies. The item closes when the thread's mark is `fixed`.
   - **otherwise, the live agent session whose worktree is on the PR's head branch** (`sindri agents`, existing, which reads each session's working directory): the item's owner is set to that session, and the session is told at its next prompt. A new `UserPromptSubmit` hook, `sindri-tasks.sh`, follows the `sindri-nudge` pattern: it runs `sindri review tasks --session <id> --nudge` (new) and prints one line per open fix task for that session's branch, e.g. "Fix task 01J… on #2071: <first line>. `sindri review tasks` shows it." It stays silent when there are none, exits 0 always, and is bounded at 500 ms. The lane fixes the finding and closes the item with `sindri item close`.
   - **no session on that branch**: the item stays open in Work as "Waiting on <branch>", and the hook shows it to the first session that opens a worktree on that branch.
4. **Records `review.fix-sent`**: the item, the PR, the thread link and the session, or the failure.

**Half-done sends.** If the item is created but the post fails, the page says "Item <id> created in Work; posting to #2071 failed: <reason>", with **Retry post**. The retry reuses the item and the same marker, so it can't post twice. If the item can't be created, nothing is posted.

### 8.3 Forge additions (Ph3)

New members on `IForge` and `IPullRequests`, with `ForgeContract` and `PullRequestsContract` extended:

| Member | GitHub | File adapter, `FakeForge` |
|---|---|---|
| `Files(link)` → `PullFile[]` `{path, previousPath, status, additions, deletions, patch?}` | `GET .../pulls/{n}/files`, 100 per page, up to GitHub's 3,000-file cap (the page says when it's hit) | seeded (`aw-forge-fake seed files`, new) |
| `Get` gains `Checks.Runs[]` `{name, state, sha}` | check runs and commit statuses for the head sha | seeded by `seed checks --runs` |
| `SubmitReview(link, expectedHead, verdict, body, comments[], marker, credential)` → `Posted` \| `AlreadyDelivered` \| `HeadMoved` | Reads the head first, and returns `HeadMoved` when it isn't `expectedHead`. Then `POST .../pulls/{n}/reviews` with `commit_id`, `event`, the body with the marker, and `comments` | stored in the world |

It's unproven whether GitHub itself refuses a review on an old `commit_id`. The pre-check doesn't depend on the answer. The owner's heavy write contract (`forge.md`, "The owner's heavy-lane GitHub check") gains a review on a scratch PR that records it.

**Posting (release 2).** `POST /reviews/{...}/post` (new) takes the draft version, the verdict and the confirm token from the confirm sheet.
- **The confirm sheet** shows the verdict, the body, every line comment with its file and line, and "Posting as @<login> (from `review.credential`)".
- **The post** is one `SubmitReview` with marker `sindri.review:<draft id>:<draft version>`, so a retry can't post twice. Text is scrubbed before it's sent.
- **After**: Sindri records `review.posted` (verdict, sha, comment count and review link; no comment text) and deletes the draft.
- **Human-only**: a session's post gets `SND-AUTH-002`.
- **`HeadMoved`** keeps the draft: "The PR moved while you reviewed. Reload to see the new commits; your comments are kept."

### 8.4 What agent sessions can do

- **Read**: `sindri review list|show --json` and `sindri_review` return everything the page shows.
- **Suggest**: `sindri review suggest <pr> --file F --line N --text T` (new) adds a **suggested comment** to the page. It's shown apart from the owner's draft, under "Suggested by <session>", for the owner to accept into the draft or dismiss.
- **Their own fix tasks**: `sindri review tasks` lists them, and `sindri item close` closes them.
- **Refused** (`SND-AUTH-002`): writing the draft, ticking anything, posting, and sending fix tasks.

**The owner's CLI.**
- `sindri review list [--project P]`, `show <pr-link>`, `fix <pr-link> --text T [--file F --line N]`.
- `post <pr-link> --verdict comment|approve|request-changes` (release 2): prints the confirm text and asks for `y` on a terminal.

## 9. Errors (new)

| Code | When | Text |
|---|---|---|
| `SND-REVIEW-001` | a bad `review` key: a panel, glob, regex or login, a duplicate id, an unknown `settings` or `extends`, a cycle | names the key path |
| `SND-REVIEW-002` | a `review.projects[].project` not on the Projects page | "No project <id>. `sindri projects` lists them." |
| `SND-REVIEW-003` | a `command:` need | "Command checks wait for the sandbox. Use `ci:` with the CI job that runs it." |
| `SND-REVIEW-004` | a review route for a host or repo Sindri doesn't sync | "Sindri doesn't sync <owner/repo>. Add it to `context.sources.github.repos`." |
| `SND-REVIEW-005` | a `review.projects[].repos` entry not in `context.sources.github.repos` | names the key path |
| `SND-REVIEW-010` | posting a draft whose version changed since the sheet opened | "This review changed in another window. Reload it." |
| `SND-REVIEW-011` | posting when the head moved | §8.3's text |
| `SND-REVIEW-012` | posting with no `review.credential` | "Set `review.credential` to your own GitHub token to post from Sindri." |
| `SND-LIB-001` | a bad `context.sources.library` key | names the key path |
| `SND-LIB-010`..`019` | §4.2's convention errors, one code each | file, line and the fix |
| `SND-LIB-021` | a `Covers:` ref that doesn't resolve | "#2071 claims harbor-booking-tdd#Ph9, and that doc has no Ph9." |

Registered in `docs/csharp/sindri.md`'s error table.

## 10. Security

- **Doc and forge text is untrusted.** It's fenced in API and MCP results, scrubbed before it's stored, and shown as plain text, never markup that loads anything. In fix tasks the babysitter and lanes read, it's fenced as quoted text.
- **Work notes stay on the laptop.** Notes without front matter are never read into the store (D2). Library text reaches no model call unless the owner sets `models: true` on a root (D13), and no library feature calls a model.
- **Who sees library docs**: §5.4. They're owner-only unless a doc names exactly one synced repo.
- **Posting is the owner's** (D7): human-only, with the owner's own credential, after a confirm sheet that shows the account and every comment.
- **The PR author can't steer required checks** (D6), and `body:` checks never show as pass.
- **No PR code runs** (D11).
- **Limits**: symlinks aren't followed, and files over 1 MiB and docs over 2,000 items are stored with an error and no items (§4.1, §5.3).
- **Routes accept only synced repos** on allowed hosts, with path segments checked (§6.2).
- **Drafts aren't in the ledger** (D8), and `review.posted` holds no comment text.
- **The fix-task hook** prints only the item id, the PR number and the finding's first line, scrubbed, and exits 0 on any failure.
- **Fixtures are made up** (D16).

## 11. Docs to update

- **Profile and CLI**: `docs/csharp/sindri-profile.md` (`context.sources.library.*`, `review.*`); `docs/csharp/sindri.md` (the CLI table, the error table, the new hook's cli edge).
- **UI**: `docs/csharp/sindri-ui.md` (the ninth tab, the minimum width, the review page).
- **Forge and PR line**: `docs/csharp/forge.md` (`Files`, `Checks.Runs`, `SubmitReview`, `seed files`, the heavy contract's review); `docs/csharp/sindri-pr.md` (fix tasks taken by the babysitter).
- **Index and data**: the owned index docs (the `library` feed, the four tables, the PR record's new fields); `planning/ERD.md` (migration 0030).
- **Hooks**: `.agents/rules/hooks.md` and the provider installers (`sindri-tasks.sh`).
- **Skills and conventions**: `skills/_shared/pr-body.md`; the three skills; `docs/conventions/design-docs.md` (new).
- **Repo files**: AGENTS.md's Commands (`sindri library …`, `sindri review …`); `deps.yaml` (the hook's edge); `.env.example` (`AW_REVIEW_GITHUB_TOKEN`, the documented example pointer); `src/Sindri/aot-smoke/expected.txt` (the `library` doctor row).

## 12. Phases and parallel plan (for /specToProvenPR)

| Phase | Release | Lane | What | Depends on | Proof |
|---|---|---|---|---|---|
| **Ph0** | — | ideas | The convention page and `schemas/library/design-doc.schema.json`. Made-up fixtures in `tests/fixtures/library/`: a TDD; an officeHours plan in its real shape, with REQ refs in its engineering and tasks docs; a stage map and verification plan; a doc with a fenced example; `Ph01` vs `Ph1`; a rerun pair; two repo-slugs sharing an id; one broken doc per `SND-LIB-01x`; a vault with `.obsidian/`, a private folder, a symlink and a note without front matter. Also the baseline count of this repo's last 20 merged PRs (§2) | — | a schema test over the fixtures: the good ones pass, and each broken one fails with its rule |
| **Ph1** | 1 | context | `DesignDocParser`; itemizing in `RecordBatches.From`; migration 0030 and the `IRecordStore.Commit` rows; claims from PR records; the parser version in the re-read check; `IContextLibrary`; the ERD | Ph0's draft schema | unit: the parser over every fixture. integration: **an existing `aw-doc` repo spec (this file) itemizes on the first tick after upgrade**, and a PR body's claims land |
| **Ph2** | 1 | context | The `library` feed (front-matter files only); `context.sources.library` keys and rules; the zero-repo catalog change; the doctor row; `library sync` | Ph1 | unit: keys, path expansion, deny, symlinks, caps, skipping a file without front matter. integration: a tick over the fixture vault, a removed file, an unreadable root. aot-smoke `expected.txt` |
| **Ph2b** | 1 | context | PR records gain the author and requested reviewers (§5.6) | — | unit: the mapping. integration: a GitHub fake PR with two reviewers |
| **Ph3** | 1 | api | `Files`, `Checks.Runs` and `SubmitReview` (GitHub, file, fake); `seed files` and `seed checks --runs`; the `IPullRequests` members; the heavy contract's review | — | contract tests over the fake and file adapters; stubbed-HTTP unit tests |
| **Ph4a** | 1 | ideas | officeHours, system-design (`doc.md`), specToProvenPR and `pr-body.md` write the convention and `Covers:` | Ph0 | the system-design suites; fixtures from real runs pass the schema |
| **Ph4b** | 1 | api, then ideas | The `type: command` grader in `scripts/skill-evals.sh` (api), with selftest rows. Then one eval case per skill (ideas), officeHours with pre-filled answers. Runs go through the spend line | Ph4a, Ph5's `library validate` | the selftest; the three cases pass |
| **Ph5** | 1 | api | `sindri library …`, `GET /library*`, `sindri_library`. The `review` block and its rules. `GET /reviews` and `GET /reviews/{pr}`: the page model, one-head reads, the cache, check evaluation, and the evidence lookup measured and wired. Fix tasks with the session lookup; `sindri review list|show|fix|suggest|tasks` and `sindri_review`; the `sindri-tasks.sh` hook and its installer rows; catalog rows | Ph1, Ph2b, Ph3 | unit per check kind, mapping rule and fix-task branch (including half-done and no session); API goldens; `SND-AUTH-002` for a session's fix; the hook test in `config/hooks/tests/` (silent, one line, exit 0, bounded) |
| **Ph6** | — | ui | Mockups: the Reviews list, the review page (every §6.3 state, both widths), the confirm sheet, Settings → Library. Then the owner's pick | — | the pick, recorded here |
| **Ph7** | 1 | ui | The Reviews tab (`G V`, nine tabs, the minimum width measured again) with the login field; the review page (release 1: the review buttons open GitHub); Settings → Library; the library count on the header | Ph5's goldens (hand-written first), Ph6 | UI tests: nine tabs at the minimum width, the chord, deep links, panel order from settings, folded groups, §6.4's keys, focus and names, every §6.3 state, the fix-task flow on the fake forge. A screenshot run on the fixture |
| **Ph8** | 2 | api, ui | Drafts (the file); suggested comments; `POST .../post` with the confirm token; `review.credential`; the confirm sheet; `sindri review post` | release 1 | unit: draft versions, outdated comments, `HeadMoved`, a retry with one marker. UI tests for the sheet. A session's post is `SND-AUTH-002` |
| **Ph9** | 2 | ui | The project sheet's Review settings editor, with one-click approval | Ph7 | UI tests; an edit round-trips through `profile approve` |
| **Ph10** | — | — | The owner proof (§13) and the two-week measure | each release | §13 |

**Parallel plan.**
- **Day one**: ideas on Ph0, then Ph4a; context on Ph1 (from Ph0's draft schema) and Ph2b; api on Ph3; ui on Ph6's mockups.
- **Then**:
  - context: Ph1 → Ph2;
  - api: Ph3 → Ph5 (once Ph1's read path and Ph2b land) → Ph4b's grader → Ph8;
  - ui: Ph6 → Ph7 on Ph5's hand-written goldens → Ph9 and Ph8's sheet.
- **Release 1 ships** when Ph1, Ph2, Ph2b, Ph3, Ph5 and Ph7 are in.

**Gates per phase.**
- every phase: the `-warnaserror` build, the touched projects' unit and integration lanes, scoped format;
- `Aw.Architecture.Tests` (new folders and usings): Ph1, Ph3, Ph5, Ph7, Ph8;
- `check-profile-doc.mjs`: Ph2, Ph5, Ph8;
- `check-env-example.sh`: Ph8;
- the ApiGoldens classes: Ph5, Ph8;
- `Aw.Sindri.Ui.Tests`: Ph5's catalog rows, Ph7–Ph9;
- `bash src/Sindri/aot-smoke/run.sh`: Ph2;
- the hook tests and `setup.sh --dry-run`: Ph5;
- the skill-evals selftest: Ph4b.

## 13. Proof

| Row | Check |
|---|---|
| **TP-1** | The fixture vault and `aw` tree on a scratch Sindri home: every good doc's items match the schema's expected list, and each broken doc is stored with its one error and no items. Absent from the store: `.obsidian/`, the denied folder, the symlink target and the note without front matter. A fenced example makes no items, and officeHours' REQ refs in its engineering and tasks docs are refs, not duplicates. Verifies REQ-7, REQ-12 |
| **TP-2** | `sindri library validate` gives the index's verdicts file by file, and `--pr-body` flags a broken `Covers:` ref. Verifies REQ-9 |
| **TP-3** | One eval case per skill passes the `command` grader, and specToProvenPR's PR body's `Covers:` resolves. Verifies REQ-10 |
| **TP-4** | On the file forge, a PR with `Covers: harbor-booking-tdd#Ph2` shows Ph2 highlighted, its `TP` rows, a second PR's claim on Ph1 as "claimed by #N", and `REQ-E5` unclaimed. A PR with only `Part of #412` shows the suggestion. Verifies REQ-2, REQ-8 |
| **TP-5** | The same PR touching `packages.lock.json` and `.github/workflows/ci.yml` shows the lock folded with a count, and the workflow file marked with its reason. Verifies REQ-3 |
| **TP-6** | Each `need` kind, applicable and not: pass, fail, running (a run on an older sha), claimed in body, and a manual tick reset by a new head. A PR claiming a doc of another project still gets its repo's checks. Verifies REQ-4 |
| **TP-7** | A fix task with a babysitter watching: a thread, which the comment loop takes, and the item closes on `fixed`. With a live session on the branch: the hook prints the one line at its next prompt, and the session closes the item. With neither: a thread and an open item. A post failing after the item: the half-done message, and Retry posts once. Verifies REQ-6 |
| **TP-8** | The list holds exactly the owner's PRs and the PRs requesting the owner's review, with "Waiting on you" first. Verifies REQ-1 |
| **TP-9** | Keyboard only: open Reviews, open a PR, walk files and hunks, open a folded group, send a fix task. Screen reader names on checks and diff lines. Verifies REQ-13 |
| **TP-10** | Release 2 on the file forge: Approve with two line comments shows the confirm sheet with the account, and posts one review with the marker. A retry posts nothing, and a moved head keeps the draft. A session's post and a session's tick are `SND-AUTH-002`. A session's `suggest` shows under "Suggested by". Verifies REQ-5, REQ-11 |
| **TP-11** | On the owner's laptop: the real vault and `~/.agentic-workflow` are indexed with no model call made (the ledger shows none for the `library` source). Reviews shows this repo's PRs, one review is posted to a scratch PR as the owner, and with the network off the page opens from the cache with "as of". Verifies REQ-1, REQ-5 |

## 14. Questions for the owner

Decided (owner, 2026-11-04):
1. **Where Reviews goes**: the ninth top-bar tab (D14).
2. **Fix tasks for lane PRs**: they go to the owning lane only, never through `/meta` (D15, §8.2).
3. **Which PRs the list shows**: PRs the owner authored, plus PRs requesting the owner's review (§6.1).
4. **Old docs without front matter**: ignored, so they're not indexed or itemized (D2).
5. **Vault pickup within 5 minutes**: fine (§5.2).
6. **How Sindri reaches the vault**: Sindri runs on the work laptop, so the vault is a local path in the profile (D12, §5.1). No sync.

Open (raised by review round 1):
1. **Posting as you.** Release 2 posts with your own GitHub token (`review.credential`), not the agent App token, so the review is yours. That needs a personal token with pull-request write on these repos, stored as a pointer (env, keychain or 1Password). I'd go with that. The other way is to post as the App with "on behalf of" text, but an App can't approve PRs it opened. Your own token?
2. **Release 1 without posting.** Release 1 shows everything and sends fix tasks, but its review buttons open GitHub, and posting from Sindri comes in release 2. I'd ship in that order, because release 1 carries what GitHub can't do. OK?
3. **The fix-task hook.** A lane hears about a fix task through a new `UserPromptSubmit` hook (one line, only when the lane's branch has an open task). It's one more hook every session runs; the other way is the SessionStart hook only, which a long-running lane wouldn't see until it restarts. The new hook?
