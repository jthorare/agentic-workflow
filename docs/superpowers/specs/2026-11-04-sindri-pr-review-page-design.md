---
aw-doc: 1
id: sindri-pr-review-page
kind: spec
title: "Sindri's PR review page, configured per project, mapped against the design docs"
status: draft
repos: [jthorare/agentic-workflow]
---

# Sindri's PR review page, configured per project, mapped against the design docs

**Status:** draft for the owner, revision 5 (after /autoplan rounds 1 to 3, two rounds of context and ideas section reviews, meta's UX check, and the owner's answers of 2026-11-04 and 2026-11-05) · **Date:** 2026-11-04 · **Lane:** ui (this spec), with context (§5, the library's store) and ideas (§4, the convention and the skills) · **Builds on:** the Projects page spec `2026-10-29-sindri-projects-page-design.md`, the owned index spec `2026-10-27-sindri-owned-index-design.md`, the PR babysitter spec `2026-10-15-sindri-pr-babysitter-docs-sync-design.md` with `docs/csharp/sindri-pr.md` and `docs/csharp/forge.md`, the pages and actions spec `2026-10-29-sindri-pages-actions-design.md` (profile entries saved with one-click approval), `docs/csharp/sindri-profile.md` · **Next:** the owner's read of the whole spec; then /specToProvenPR takes §12's phases.

Every file path below is one the author or a reviewing lane opened on main (94ff1714e, rechecked at 89a4d4b). Every command, route, event, key and type is marked **existing** or **new**. This file is written in the convention §4 defines, so the first index run has a real document to itemize.

## The owner's steps: install -> works

Sindri runs on the owner's laptop, so every path below is a local one.

0. **Install.** `./setup.sh` (existing) installs the new fix-task hook with the other hooks. On Codex, open `codex` and run `/hooks` once to trust it, as for every `aw:*` hook.
1. **Open Reviews** (new, the ninth top-bar tab, `G V`). Home shows "N PRs waiting on you" and the tab carries the same count. The first time, it asks for your GitHub login, pre-filled from the forge credential's own login (one field, saved as `review.login` with one-click approval). From then on it lists every open PR you authored and every open PR waiting on your review, in the GitHub repos Sindri already syncs (`context.sources.github.repos`, existing), grouped by project.
2. **Open a PR.** The left column shows the panels in §7.1's default order: required checks, design-doc map, test plan, CI, evidence, risk notes, conversation, under a one-line summary strip. The right pane is the diff, grouped by folder, with lockfiles and generated files folded.
3. **Send as fix task** on a failed check, an unclaimed requirement, or a diff line you select (`c`): it becomes a Work item, a PR thread, and a note to the agent session working on that PR's branch (§8.2).
4. **Review.** In release 1 the **Comment**, **Approve** and **Request changes** buttons open the PR on GitHub. In release 2 you comment on diff lines in Sindri, press one of them, and a confirm sheet shows exactly what will be posted and as which GitHub account (§8.3); then Sindri posts one review.
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
| Per-check results | **Partial.** `PullChecks` is `{State, Sha, Failing[] (names), Passed (count), Pending (count)}`, and `Sha` can be older than the head. `skipped` and `neutral` runs count as passed, and the check-runs read takes one page of 100 | `Aw.Sindri.Core/Forge/PullChecks.cs:4-9` |
| Open PRs per repo | Stored: `github` feed records of kind `pr`, state open or draft, with the body and the **author** in the record (`GitHubIssue.Author` → `ConnectorItem.Author` → `RecordItem.Author`, `ConnectorFeed.cs:75`). No requested reviewers are stored | `Aw.Sindri.Host/IndexSources/GitHubRecordFeeds.cs:33` |
| Local markdown outside a repo | **None.** `repo-docs` reads each profile repo's doc globs from git at HEAD | `Aw.Context.Infrastructure/Docs/RepoDocsFeed.cs:15-23,110-111` |
| Items inside a doc | **None.** 7 record kinds (doc, issue, comment, project, message, code, pr) and 12 link kinds (parent, blocks, duplicates, relates, closes, attachment, review-of, replies-to, in-project, touches, led-by, known-as) | `Aw.Context.Core/Sources/RecordKind.cs`, `Aw.Context.Core/Records/RecordLinkKind.cs` |
| Per-project settings | **None.** A project row has no repo and no settings | `Aw.Sindri.Core/Projects/ProjectAttachedRow.cs:19-31` |
| Sending work to an agent | An item plus a recipe instance, or the babysitter's comment loop. The `sindri-nudge` SessionStart hook prints one line from `sindri repo status --nudge`. Nothing else reaches a running session. The babysitter's comment queue drops every thread by Sindri's own login or with a marker starting `sindri`, so it would never take a thread Sindri posted. An item's `Owner` is reserved and always null | `Features/CreateItem/CreateItemRequest.cs:11`, `config/hooks/sindri-nudge.sh`, `docs/csharp/sindri.md:933`, `Features/Pr/PrCommentQueue.cs:140-143`, `Core/Items/ItemSummary.cs:15` |
| Who may read a record | One audience key per record (`repo:`, `linear:`, `slack:`). A record with none is visible to **no one**, the owner included (default deny). A human's scope is the repo and Linear keys plus the profile's Slack channels | `Aw.Context.Core/Audience/AudienceScope.cs:22`, `ProfileAudiencePolicy.cs` |
| Credentials kept from agent sessions | A launched Step's environment empties `GH_TOKEN`, `GITHUB_TOKEN`, `GH_ENTERPRISE_TOKEN`, `AW_FORGE_TOKEN`. Forge credential pointers are `env:` or `command:` | `Aw.Sindri.Core/Launch/LaunchEnvironment.cs:10`, `Aw.Forge.Core/Credentials/ForgeCredentialSpec.cs` |
| Doc ids and validators | officeHours has `REQ-U1`/`REQ-E1`..., `AC-1`, `TR-1`, `TASK-1`. system-design numbers PR cards `pr-NN`. specToProvenPR has none. None of the three has a validator | `skills/officeHours/SKILL.md:200-252,421-448`, `skills/system-design/SKILL.md:43-58`, `skills/specToProvenPR/SKILL.md:42-65` |
| A PR saying what it covers | **None** in `_shared/pr-body.md` | `skills/_shared/pr-body.md:7-26` |
| Top bar | Eight tabs, chords `G H/P/W/Q/R/C/A/.`. `V` is free | `Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.cs:112-122`, `ShellViewModel.Chords.cs:111-121` |

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **The library is its own tables in the owned index** (`design_doc`, `design_doc_item`, `design_doc_ref`, `design_doc_claim`), filled from the doc records of either doc feed. Items are **not** records | Items as records would be chunked and embedded twice, get one decision-pass model call each (`RecordDecisionRun.cs:107`) and become map nodes (`RecordPass.cs:46`). The context lane's call |
| D2 | **Opt-in is front matter.** A markdown file is a design doc when its front matter has `aw-doc: 1`. In the `aw` root and the vaults, a file without it is **not indexed at all** | Owner, 2026-11-04 (§14 Q4). A vault of unrelated notes never enters the store |
| D3 | **Items come from ids at the start of a heading, a list item or a table row's first cell**, outside code fences and quotes (§4.2). Inside one doc the first definition wins, and a later repeat is a reference. No model call | Deterministic. `sindri library validate` and the indexer run the same parser. officeHours lists each REQ and then repeats it in its Traceability table (ideas round 2) |
| D4 | **A PR claims items with a `Covers:` line.** An issue or a branch name gives suggestions, always marked "suggested". A claim is checked when the PR is written (`library validate --pr-body`) and is a required-check kind (§7.3) | The claim is the author's, and a broken one is caught before review |
| D5 | **Review settings live in the profile** (`review` block): named settings, plus which repos belong to which project. Saved from the app with one-click approval | Required checks guard what the owner approves, and a session can't change an approved profile |
| D6 | **Settings are picked by the PR's repo.** A doc's project only breaks a tie between projects that share a repo | The PR author writes `Covers:`, so it must not choose which checks apply (security round 1) |
| D7 | **Posting is the owner's, as the owner.** Approve, request changes and comment reviews are human-only and use the owner's own GitHub credential (`review.credential`), never the agent App token the babysitter uses. Its variable is emptied in every launched agent Step, like `GH_TOKEN` | A review posted by a bot isn't the owner's review, and an App can't approve PRs it opened (arch round 1). A session that inherited the token could post as the owner (security round 2) |
| D8 | **Drafts are local state, not ledger events.** One file per PR under `$AW_SINDRI_HOME/reviews/`. Only `review.posted` and `review.fix-sent` go to the ledger | The ledger is append-only and sessions can read it, so a debounced draft would leave copies of unposted comments forever (arch round 1) |
| D9 | **The diff, checks and threads are read live when the page opens**, pinned to one head and cached by head sha with a cap | A review must be of the current head |
| D10 | **Two releases.** Release 1 is read-only: the list, the panels, required checks, the doc map, the diff and fix tasks; its review buttons open GitHub. Release 2 adds drafts and posting | Release 1 is useful alone and carries the parts GitHub can't do. Posting is the biggest build and the most like GitHub (product round 1) |
| D11 | **Required checks are decided from data Sindri already holds**: a CI check's state on the head, files changed together, a PR body line (shown as "claimed in body"), a resolvable `Covers:`, or a manual tick. No PR code runs | Running PR code on the laptop outside the sandbox is the one thing this page must not do |
| D12 | **How Sindri reaches the vault: decided 2026-11-04.** Sindri runs on the work laptop, so the vault is a local path in the profile (`context.sources.library.vaults`, §5.1). No sync | Owner, 2026-11-04 |
| D13 | **Library text reaches no model unless the owner turns it on per root.** The decision pass skips the `library` source the way it skips projects (`FeedDecisions.cs:44`, `feed.Source == _projects`), the map pass already skips doc records (`RecordPass.cs:46`), and the reranker leaves library titles out of its prompt (`QueryContext/RankerStage.cs`). `models: true` on a root opts it in | Work notes stay on the laptop by default (security and context round 1; the reranker path, arch round 2) |
| D14 | **Reviews is the ninth top-bar tab, third after Projects**, and the review page is a full page | Owner, 2026-11-04 (§14 Q1) |
| D15 | **A fix task goes to the lane that owns the PR, never through `/meta`** (§8.2) | Owner, 2026-11-04 (§14 Q2) |
| D16 | **Fixtures are made up** ("Harbor ferry booking"). No work-app names, paths or text | Owner rule |
| D17 | **Owner-only is its own audience key, `owner:library`.** The human's scope always holds it; no session scope ever does, and the record-link rule never lets an `owner:` record through | A null audience is visible to no one, the owner included (context and arch round 2) |
| D18 | **Posting uses the owner's own GitHub token** (`review.credential`), never the agent App token (§8.3) | Owner, 2026-11-05 (§14 Q7) |
| D19 | **Release 1 is read-only; posting comes in release 2** (D10) | Owner, 2026-11-05 (§14 Q8) |
| D20 | **Lanes hear about fix tasks through the per-prompt `sindri-tasks.sh` hook** (§8.2) | Owner, 2026-11-05 (§14 Q9) |
| D21 | **A PR the owner reviewed comes back to "Waiting on you" when new commits land** (§5.6, §6.1) | Owner, 2026-11-05 (§14 Q10) |

## 2. Goals and non-goals

**Goals**

- **REQ-1** Reviews lists the open PRs the owner authored and the open PRs requesting the owner's review, in the synced GitHub repos. They are grouped by project, then by repo, newest activity first, with "Waiting on you" first. Home and the tab show how many are waiting on you.
- **REQ-2** A review page shows the panels its settings name, in that order, in the left column, with the diff in the right pane.
- **REQ-3** The diff is grouped by the settings' groups. Files matching the collapse globs fold with a count, and risky paths are marked with their reason.
- **REQ-4** Each required check shows pass, fail, running, claimed in body, not applicable or needs a tick, with the reason in words.
- **REQ-5** (release 2) The owner posts one review (line comments, a body, a verdict) to GitHub as themselves, after a confirm sheet.
- **REQ-6** The owner can send any finding as a fix task. It reaches the session working on the PR's branch, and the page says where it went, including a half-done send.
- **REQ-7** Front-matter docs in `~/.agentic-workflow` and each configured vault are indexed and itemized into sections, phases, requirements with acceptance criteria, test-plan rows and tasks.
- **REQ-8** The doc map shows this PR's claims and suggestions, other PRs' claims with their PR, and requirements nobody claimed.
- **REQ-9** `sindri library validate` checks a file, a folder or a PR body against the convention, using the indexer's parser.
- **REQ-10** officeHours, system-design and specToProvenPR write conformant docs every time, and specToProvenPR's PR bodies carry `Covers:`.
- **REQ-11** Agent sessions read the same data, scoped to what their repo may see, and (release 2) may suggest comments. They never tick, post or approve (§8.4).
- **REQ-12** Broken docs are visible: `sindri library list --errors`, a doctor row, and a count on the Reviews header.
- **REQ-13** The page works by keyboard alone and with a screen reader (§6.4).

**Success measure.**
- **Baseline (Ph0).** Of the last 20 merged PRs in this repo, how many a doc would have mapped by issue or branch, counted twice: against today's docs, and as if their docs carried front matter.
- **Go/no-go on the baseline.** If fewer than 5 of 20 would map even with front matter, Ph4a and the `Covers:` flow ship before Ph7, and the trial waits for them.
- **Counts the app keeps** (a `review.opened` event per page open, plus the existing `review.fix-sent` and `review.posted`): PRs opened in Reviews against PRs reviewed in the window, the share of opened PRs with a resolved claim, fix tasks sent and closed, reviews posted (release 2). `sindri review stats [--since D]` (new) prints them.
- **Trial.** Two weeks, starting once 5 open PRs in this repo carry `Covers:` (not on the release date), with the owner reviewing this repo's PRs from Reviews.
- **Target.** For PRs with `Covers:`, the owner never opens the design doc by hand to find the phase (their word at the end of the trial). At least 80% of required checks are decided by Sindri, not by a manual tick, and the settings must hold at least one `covers:` or `ci:` check besides `changedWith:`.
- **Stop line.** The denominator is the PRs on the owner's list (authored or requested) that merged or got a review in the trial, from stored PR records; the numerator is those opened in Reviews (`review.opened`). `review stats` prints both. If fewer than half were opened in Reviews, Ph8 and Ph9 wait, and the owner decides what changes.

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
| **Reviews** (new tab, `G V`, `sindri://reviews`) | PR records (stored), the library, review settings | `review.login` (once) | the top bar (with the waiting count); Home's "N PRs waiting on you" line; a Projects card's "N open PRs"; a Work item linked to a PR |
| **Review page** (`sindri://review/<owner>/<repo>/<number>`) | the forge (live, cached by head), the library, the babysitter's facts when an instance watches the PR, evidence on disk, the draft | the draft (a local file), a posted review (forge), fix tasks (item, forge, session nudge) | a Reviews row; a babysitter instance ("Review"); a doc item's "claimed by #N" |
| **Library** (no page in v1) | front-matter docs in the vaults and `~/.agentic-workflow` (new `library` feed), repo docs (existing feed) | the library tables | the review page; Settings → Library; later a Library tab, the Projects sheet's "Design docs" and Context's Why view |
| **Projects** (existing) | + open PR counts per project | + Review settings (Ph9) | the top bar |

**Mockup first.** Ph6 runs `/design-mockup` for the Reviews list, the review page, the fix-task sheet and its three outcomes, the head-moved banner, the library errors sheet, the confirm sheet, the comment composer and Settings → Library. The review page is mocked at the 1,000 px minimum and at 1,440 px, in each §6.3 state. No new tokens or components outside the existing Theme. The owner picks before any view is built.

**Nine tabs at the minimum width.** Ph6 measures the bar first. If nine labels don't fit at 1,000 px, the labels shorten below 1,100 px (the width where sheets start covering the page), never into an overflow menu, and the mockups show it.

## 4. The docs convention (ideas lane, with context)

One page, `docs/conventions/design-docs.md` (new, Ph0), and one schema of the parsed result, `schemas/library/design-doc.schema.json` (new).

### 4.1 Roots and what is read

| Root | Path | Read |
|---|---|---|
| `aw` | `context.sources.library.stateDir` (default `~/.agentic-workflow`) | Only `*.md` files with `aw-doc: 1`, under `<repo-slug>/plans/**`, `<repo-slug>/proof/*/` or `<repo-slug>/system-design/*/`. Tool state (`bin*/`, `cache/`, `skill-evals/`, `digests/`, `meta/`, logs) is never opened. system-design's `prs/pr-NN.md` cards have no front matter, so they aren't read |
| `<vault name>` | each `context.sources.library.vaults[].path` | Every `*.md` with `aw-doc: 1`, minus `.obsidian/`, `.trash/`, any dot folder, and the vault's `deny` globs |
| `repo:<name>` | each profile repo, through the existing `repo-docs` feed | Its doc globs, unchanged (`docs/**/*.md` covers `docs/superpowers/specs/`). Front-matter docs among them are itemized too |

- A file's first 4 KiB decide whether it's read: no `aw-doc: 1` front matter means it is skipped before any text is stored.
- **Near misses are counted, not stored.** A file whose first 4 KiB hold front matter with an `aw-doc:` key whose value isn't a whole number (`"1"`, `1.0`, `yes`), or `aw-doc: 1` after a blank first line, is skipped the same way, but its path goes on the `library` doctor row ("3 files look like design docs but aren't read") and in `sindri library list --near-misses`. No text from it is stored. `sindri library validate` reports it as `SND-LIB-019` with the fix. A whole number other than 1 (`aw-doc: 2`) is not a near miss: it is a later convention version, stored with `SND-LIB-010` and no items (§9).
- Symlinks are not followed.
- A front-matter file over 1 MiB is stored as a doc with the error "too large" and no items.

### 4.2 Front matter and item ids

```yaml
---
aw-doc: 1                         # required: convention version; opts the file in (D2)
id: harbor-booking-tdd            # required: lowercase letters, digits, '-', 3-64; a skill that makes it from a title cuts it at 64 on a word boundary
kind: tdd                         # required: tdd | spec | plan | stages | product | engineering | design | tasks | verification | note
title: "Harbor ferry booking"     # required
status: draft                     # required: draft | review | approved | building | done | superseded
project: prj_01J...               # optional: a Sindri project id, or linear:project/<id>
repos: [acme/harbor-web]          # optional: owner/name; exactly one decides who may read it (§5.4)
issues: [412, 415, ENG-12]       # optional: GitHub issue numbers (plain numbers, so Obsidian's Properties editor keeps them) or Linear keys (^[A-Z][A-Z0-9]{0,9}-\d+$); anything else is SND-LIB-010
supersedes: harbor-booking-v1     # optional: a doc id in the same scope, or a full ref (doc:<root>[/<slug>]/<id>) for another scope
parent: harbor-booking-plan       # optional: the doc this one belongs to; same rule (a stage map names its repo spec as doc:repo:<name>/<id>)
generated: write_packet.py        # only on generated files, with source-sha256 (below)
source-sha256: 3f2a...
---
```

**Doc ids are unique per scope.** In the `aw` root the scope is the root plus the repo-slug (`doc:aw/<slug>/<id>`). In a vault or a repo it's the root. Every doc is stored with its items; who owns a shared id is decided **on read**: the live doc at the lowest path keeps the id, and the others show `SND-LIB-012`. A doc removed later hands the id to the next one with no rewrite, and a replay gives the same answer.

**Where items are read.** Only from:
1. headings `##` to `####`;
2. list items (`-`, `*`, `1.`);
3. a table row's **first cell**.

A bold lead-in (`**REQ-3**`) is allowed inside those, never on a bare paragraph. Nothing inside a ``` or ~~~ fence, or a `>` quote or callout, is read. An item's text is its line plus the lines under it, up to the next item or heading of the same or higher level.

**The first definition in a doc wins.** A later list item or table row whose first cell repeats an id already defined above it is a reference, and that row's other cells that hold ids become links between the two (officeHours' Traceability row `| REQ-U1 | AC-1 | … |` ties `REQ-U1` to `AC-1`). Two headings, or two list items, defining the same id is `SND-LIB-013`.

**Each id kind is defined in one set of doc kinds.** A matching line in any other doc kind is a reference, not a definition:

| Item | Id | Defined in kinds | Elsewhere |
|---|---|---|---|
| Section | A heading with no id. Its id is its anchor, by GitHub's slug rule (lowercase, spaces to `-`, punctuation dropped, `-1`, `-2` on repeats) | all | — |
| Phase | `Ph<n>` with an optional letter or `-<n>` (`Ph0`, `Ph3b`, `Ph2-1`). No leading zeros; a ref to `Ph01` matches `Ph1` | `spec`, `tdd`, `stages` | ref |
| Requirement | `REQ-<n>` or `REQ-<U\|E\|S\|O\|W><n>` | `spec`, `tdd`, `product` | ref (`relates`) |
| Acceptance criterion | `AC-<n>`. Its parent is the requirement a table row pairs it with (officeHours writes every AC in its own section, after all the REQs). With no such row, the nearest requirement above it **in the same section**. With neither, `SND-LIB-014` | `spec`, `tdd`, `product` | ref |
| Test-plan row | `TP-<n>`. The row's other cells that hold ids become `verifies` refs | `spec`, `tdd`, `verification` | ref |
| Task | `TASK-<n>`. List items `- reqs: REQ-U1` and `- depends: TASK-1, TASK-2` under its heading become `relates` and `blocks` refs (Ph4a moves officeHours' per-task `---` key blocks into these list items: a `key: value` line followed by `---` reads as a heading in CommonMark) | `tasks` | ref |

officeHours' `TR-<n>` and `ADR-<n>` stay plain text on purpose: nothing claims or verifies them.

The full ref form is `doc:<root>[/<slug>]/<doc id>#<item id>`. A ref inside the same scope may be just `<doc id>#<item id>`. The item may be a section's anchor (`harbor-booking-tdd#data-model`). This is ref syntax only, never a stored key.

**Errors.** One code each; the text names the file, the line and the fix:

| Code | Error |
|---|---|
| `SND-LIB-010` | a missing or bad front-matter key (id shape, kind, status, `issues:` entry) |
| `SND-LIB-011` | an item id of a bad shape (`Ph01` is accepted and reads as `Ph1`; `REQ-` with no number is not) |
| `SND-LIB-012` | a doc id another doc in the scope also has (§ below) |
| `SND-LIB-013` | two headings, or two list items, defining one item id in a doc |
| `SND-LIB-014` | an `AC` no row pairs and no requirement precedes in its section |
| `SND-LIB-015` | a ref to an item the scope doesn't have |
| `SND-LIB-016` | an unknown `parent` or `supersedes` |
| `SND-LIB-017` | a hand-edited generated file (its `source-sha256` no longer matches its source) |
| `SND-LIB-018` | "too large" (over 1 MiB or 2,000 items), or "blocked" (the scrubber hit) |
| `SND-LIB-019` | a near miss: `aw-doc:` present but not the number 1 (§4.1); only `validate` and the doctor row show it |

A doc with errors other than `012`, `015` and `016` is stored with them and without items, so it shows as broken instead of vanishing. Those three are decided against the rest of the scope when read (a shared id, a broken ref, an unknown `parent` or `supersedes`): they are listed on the doc, and its items stay.

### 4.3 What each skill writes (Ph4a)

| Skill | Change |
|---|---|
| officeHours | Front matter on `plan.md` (kind `plan`) and on the domain docs (`product`, `engineering`, `design`, `tasks`, each with `parent`). Ids unchanged. The doc id is the folder slug. **A rerun on the same slug** writes the new folder's docs with `supersedes: <old id>` and a numeric suffix on the id (`harbor-booking-2`); it never writes into the old folder, and the old doc reads as superseded through that link. `TASKS.md`'s per-task `---` key blocks become `- reqs:` / `- depends:` list items under each `## TASK-n` heading (archReview, planDevexReview, productReview and dark-factory's check 7 read TASKS.md as prose, so the move is safe; dark-factory's wording is updated in the same PR) |
| system-design | `write_packet.py` also writes `doc.md`. Its front matter has `generated: write_packet.py`, the `source-sha256` of `packet.json`, and an id from `packet.json`'s slug (never the title). Its items are generated from the packet: sections, a `Ph<n>` per PR card (no leading zeros), a `REQ-<n>` per EARS criterion, a `TP-<n>` per card test row |
| specToProvenPR | `stages.md` gets front matter (kind `stages`, `parent: doc:repo:<name>/<spec id>`, the full ref, since the spec lives in the repo and the stage map under `~/.agentic-workflow`) and a `Ph<n>` heading per stage. The verification plan gets front matter, and its journey rows become `TP-<n>` with a `verifies` cell. Before `gh pr create`, it runs `sindri library validate --pr-body <body.md>` and fixes what it finds |
| `_shared/pr-body.md` | A `Covers:` line under Summary, e.g. `Covers: harbor-booking-tdd#Ph2, harbor-booking-tdd#REQ-E3` |

Each skill runs `sindri library validate "$OUT" --json` as its last step. When `sindri` isn't on PATH, or has no `library` verb yet (before Ph5), it prints one line, "Skipped the design-doc check: sindri library isn't installed", and goes on. Front matter at the top of a plan file is new to the skills that read plans (archReview, planDesignReview, planDevexReview, productReview, autoplan); Ph4a runs each once on a fixture plan with front matter and checks its review still names the plan's sections.

**Evals (Ph4b).**
- **A new grader.** `type: command`, host-side in `scripts/skill-evals.sh` (the api lane's). In a case's `checks:` it's `{ id, type: command, run: "sindri library validate {out} --json", pass: exit-0 }`, where `{out}` is the staged output folder. It runs after the case with the published `sindri`, and its stdout is kept in the run's result. It gets a selftest pass row and fail row.
- **Why a host-side grader.** The eval box has no `sindri` on PATH (#1937), so the skill's own validate step skips inside a run, and the grader does the check instead.
- **officeHours** pre-fills the interview answers in `case.yaml`.
- Paid runs go through the spend line.

### 4.4 Mapping a PR to items

1. **Claimed**: the `Covers:` refs, resolved **when read**, so a PR synced before its doc, or a doc that renames an item, is right on the next read.
   - **Where a short ref looks** (`<doc id>#<item>` in a PR body): the PR repo's own root (`repo:<name>`), then `aw` and vault docs whose `repos:` names the PR's repo (matched by front matter, never by the state folder's name, which can differ from the repo's: `joi-fairshare-agentic-workflow` vs `jthorare/agentic-workflow`). The skills write `repos:` from the repo they run in. Two matches at the same step is `SND-LIB-022` "ambiguous; use the full ref".
   - **Grammar**: one line `Covers: <ref>, <ref>…` (case-insensitive key, refs may sit in backticks, nothing after the last ref), or `Covers: none (<reason>)`, which passes `covers: true` as "claims no design doc: <reason>". A second `Covers:` line is `SND-LIB-021`.
   - **A doc this PR adds or edits**: when the PR's changed files hold a front-matter `.md` with that doc id, the ref is checked against that file at the head, read through `FileText` (§8.3), and shows "in this PR". A file that can't be read shows "in this PR; not checked".
   - A ref that resolves nowhere is a broken claim (`SND-LIB-021`). On a merged PR whose item was later removed, it shows as "stale claim", not as an error.
2. **Suggested by issue**: a `Part of #N` or `Fixes #N` in the body matches the `issues:` of a doc whose `repos:` names the PR's repo. That doc's phases are offered, none pre-picked.
3. **Suggested by branch**: the head branch contains a doc id.
4. **None**: "This PR names no design doc", plus the docs of the PR's project.

Claims are parsed when a PR record is written (the `github` feed already stores the body) into `design_doc_claim` (§5.3), as refs only. So the doc map's "claimed by #N" is one indexed join, not a scan of every PR body. It lists open and merged PRs, each labeled ("claimed by #2071, merged"); a closed unmerged PR's claim isn't shown.

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

- **Paths**: a leading `~/` is expanded to the owner's home **before** the check, then the path must be absolute with no `..`: the rule `sandbox.fs.denyExtra` uses (`SandboxRules.AbsolutePathProblem`, `SandboxRules.cs:128`, made shared). A root may not be `/`, the home folder itself, or a folder that holds `$AW_SINDRI_HOME`. Roots are compared by their real path. A path that doesn't exist or can't be read (an unmounted drive) is not a validation error: it's a degraded `library` doctor row with the path and the reason.
- **Limits**: at most 10 vaults, no name twice, and no root inside another.
- **Built-in deny**, on every root before its own `deny`: `.env*`, `*.pem`, `*.key`, `id_*`, `*.p12`, `**/secrets/**`, `**/.git/**`.
- **Approval**: every change is a re-approval. Settings → Library writes these keys, and approves in one click when it's the only waiting change.

### 5.2 The feed (new, `library` source)

A `RecordFeed` (`Aw.Context.Core/Sync/RecordFeed.cs:12`) with source id `library`, in `ContextRecordFeedCatalog` beside `repo-docs`. Today `ContextRecordFeedCatalog.For` returns no feeds when the profile has no repos (`ContextRecordFeedCatalog.cs:19-22`). Ph2 changes that, so the library still runs.

- **What it reads**: the working tree, only the files §4.1 admits, minus the built-in deny and the root's deny globs.
- **Records**: one `doc` record per file, keyed `doc:<root>/<path>`, whose version is the file's sha256. A file that's gone, or that lost its front matter, is in `RecordPage.Removed`.
- **A root it can't list** (unmounted, permission denied) is a failed tick for that root, with the reason on the doctor row. Its stored docs stay as they were; nothing is removed until a later tick lists the root again.
- **Cursor**: per root, a fingerprint of the `*.md` files' path, size and mtime, **plus the parser version**. An unchanged root is an empty page, and a new parser re-reads everything. An Obsidian autosave changes the fingerprint, so the tick re-reads only changed files' first 4 KiB, and only files whose sha256 changed are written.
- **Text** goes through the same scrub and chunking as `repo-docs`.
- **Timing**: it ticks with the other feeds, so a change shows within one tick (5 minutes, accepted by the owner 2026-11-04). `sindri library sync` (new) runs the tick now.

### 5.3 Itemizing and tables (new, migration 0031)

`DesignDocParser` (new, `Aw.Context.Core`) is pure, with no store dependency, so `library validate` can run it outside serve. It turns a doc's cleaned text into `{front matter, items[], refs[], errors[]}`, the shape the schema defines. It runs in `RecordBatches.From`, the one place both doc feeds' text is cleaned. `RecordWrite` carries the parsed doc, and `IRecordStore.Commit` replaces the doc's rows in the same transaction:

| Table | Columns |
|---|---|
| `design_doc` | `doc_key` PK (FK to `source_record`), `root`, `scope`, `doc_id`, `kind`, `status`, `project`, `repos` json, `issues` json, `parser_version`, `errors` json; INDEX(`scope`, `doc_id`) (not unique: who owns a shared id is decided on read, §4.2) |
| `design_doc_item` | `doc_key`, `item_id`, `item_kind`, `parent_id`, `line_start`, `line_end`, `text` (capped at 4,000 chars, taken from the doc's scrubbed text); PK(`doc_key`, `item_id`); at most 2,000 per doc (`SND-LIB-018` past that) |
| `design_doc_ref` | `doc_key`, `from_item`, `to_ref`, `kind` (`verifies`, `relates`, `blocks`); INDEX(`to_ref`) |
| `design_doc_claim` | `pr_key` (the PR record), `ref`; INDEX(`ref`). No resolved columns: claims resolve on read (§4.4) |

- **Which records are itemized**: only kind `doc` from the `repo-docs` and `library` sources.
- **Removal.** Records are soft-deleted (`removed_at`), so a foreign-key cascade never fires. `IRecordStore.Commit` deletes a record's `design_doc*` rows in the same transaction that sets `removed_at`, and a PR's claim rows when the PR record is removed.

- **Blocked docs.** A doc the scrubber blocks gets no items and the error "blocked".
- **Backfill.** `RepoDocsFeed` returns an empty page when the repo-head fingerprint equals its cursor (`RepoDocsFeed.cs:47-50`), and re-reads a doc only when its blob or the ruleset changed (`:110-111`). The parser version joins **both**: the cursor fingerprint and the per-doc check. So every repo spec that already has `aw-doc: 1` (this one included) is itemized on the first tick after upgrade, with no new commit.
- **ERD.** `planning/ERD.md` gets the four tables.

### 5.4 Who may read it

A record holds one audience key. A null key is visible to **no one**, the owner included (`AudienceScope.cs:22`), so owner-only needs its own key (D17).
- **New key kind `owner:`.** `AudienceKey`'s shape gains `owner:[a-z][a-z-]{0,31}` beside `repo:`, `linear:` and `slack:`, with its doc comment and the owned index spec §4.4's key list updated. The library uses `owner:library`; a later owner-only source reuses the prefix.
- **One repo**: a library doc whose `repos:` has **exactly one** entry, matching a `context.sources.github.repos` entry, gets that repo's audience (`ProfileAudiencePolicy.OwnRepo`, as `repo-docs` does).
- **Any other library doc** gets `owner:library`.
- **Who holds it**: the human's scope always adds `owner:library` (with `HumanExtras`). No session scope ever holds an `owner:` key, and rule (c), a record a session's scoped map links to, never lets an `owner:` record through (`AudienceScope.Allows(audience, record)` checks the prefix first).
- **Items, refs and claims** are read through their parent record's audience: `IContextLibrary` takes the caller's `AudienceScope` and joins `design_doc_item`, `design_doc_ref` and `design_doc_claim` to `source_record.audience`. A claim is filtered by the PR's audience **and** the doc's, so a session sees neither side's owner-only half.
- **What sessions read**: a vault note with `repos: [x]` is readable by every session in repo `x`, the same as a doc committed to `x`. The Settings → Library sheet says so next to the vault ("Docs naming one synced repo are visible to that repo's agent sessions").
- **Stated v1 limit**: a doc meant for two repos is invisible to sessions in both.
- **Tests**: a session whose `Records` set holds an `owner:library` record still can't read it; no session scope built by `ProfileAudiencePolicy` holds an `owner:` key; the owner reads an `owner:library` doc.

### 5.5 Read path (new)

`IContextLibrary` on the context facade, following the `IContextProjects` pattern. Every read takes the caller's `AudienceScope` (§5.4):
- `ListDocs(root?, project?, repo?, errorsOnly?)`, `GetDoc(key)`, `Items(docKey)`;
- `Resolve(refs[])`, `Claims(prKey)`, `ClaimsOn(docKey)`.

All of them read the store only. Sindri exposes them through:
- the routes `GET /library` and `GET /library/{doc}` (release 1; the review page reads items through its own page model), and `GET /library/{doc}/items` (release 2);
- the CLI `sindri library list [--errors|--near-misses]|show|validate|sync` (release 1) and `items` (release 2);
- the MCP tool `sindri_library` (release 2: nothing in release 1 needs a session to browse the library).

Doc text in results is fenced as untrusted.

**`sindri library validate`.**
- Usage: `sindri library validate <path|dir> [--root <name>] [--pr-body <file> --repo <owner/name>] [--json]`.
- Files and folders are checked locally, with no serve. The scope for the cross-doc rules (`012`, `015`, `016`) is the folder given; a ref to a doc outside it is listed as "not checked here" (not a finding) unless serve is up, in which case it's resolved against the store.
- `--pr-body` reads the store to resolve refs, so it needs serve. With serve down it exits 2 with `SND-API-001`, as `scoping check` does, never 1, so a skill doesn't loop editing a body that's fine. `--docs <dir>` adds a folder of docs the PR itself adds (§4.4).
- Exit 0 no findings, 1 findings, 2 usage or a tool error.
- `--json` prints `{ findings: [{ rule, file, line, message, fix }], passed }`.

### 5.6 PR records gain requested reviewers (context lane, Ph2b)

The Reviews list needs, per open PR, its author login and requested reviewer logins (§6.1). The author is already on the record (`StoredRecord.Author`, from `ConnectorFeed.cs:75`). Ph2b adds `RequestedReviewers` to `GitHubPullDetail` (`Aw.Connectors.Core/GitHub/GitHubPullDetail.cs`), read by `GitHubWire.ReadPull`, the single-PR read that already gives `Merged`, `Draft`, `MergedAt` and `Paths`, and stores it on the PR record (the context lane picks the column, in migration 0030 or its own).
- **Team requests** land in GitHub's `requested_teams`, not `requested_reviewers`, so a review asked of a team the owner is on isn't "Waiting on you" in v1. The list's empty state says so.
- **A PR the owner already reviewed** drops out of `requested_reviewers` (D21, owner 2026-11-05). Ph2b also reads `GET /repos/{owner}/{repo}/pulls/{n}/reviews` for an open PR the owner didn't author, only when its `updated_at` or head sha changed since the last read, and stores the `commit_id` of `review.login`'s latest review. When that differs from the head, it reads `GET .../compare/{commit_id}...{head}` once and stores `ahead_by` as the new-commit count. A force-push that dropped the reviewed commit (compare answers 404) stores the count as unknown. Until Ph2b's review read lands, the waiting count undercounts, and the list's header says so.
- Until Ph2b lands, the list shows only the PRs the owner authored, with the line "Review requests show here once Sindri reads requested reviewers." in its header.

## 6. The review page

### 6.1 The Reviews list

Rows are stored PR records (kind `pr`, open or draft) of the repos in `context.sources.github.repos`.
- **Which PRs**: the author is `review.login`, or `review.login` is among the requested reviewers (owner, 2026-11-04, §14 Q3).
- **Order**: two groups, **Waiting on you** first, then **Yours**. A PR waits on the owner when `review.login` is a requested reviewer, or when the owner's latest review is on an older commit than the head (§5.6); that row reads "reviewed, N new commits" ("reviewed, new commits" when the count is unknown). Inside each, rows are grouped by the project whose `review.projects[]` names the repo, else by the repo's name, newest activity first.
- **A row** shows the title, number, author, age, CI state, open required checks, and whether the PR claims a doc.
  The row's groups, waiting state and doc claim come from the stored PR records; its **CI state and open required checks come from the per-head summary** in the review cache (§6.2 "Cache"): the list reads the summary of the head it holds, builds and stores one for a head that has none (at most 4 heads at once), and on a forge failure or rate limit keeps the PR's newest stored summary, or shows CI "none" and no required count.
- **The header** shows the library's state, e.g. "Library: 41 docs, 3 with errors"; the count opens a sheet listing the broken docs and their errors, and the near misses (§4.1).
- **The waiting count** ("N PRs waiting on you") is the "Waiting on you" group's size, read from the same stored records. It shows on Home and as the Reviews tab's badge, with the badge's accessible name "Reviews, N waiting on you".

### 6.2 The page model (`GET /reviews/{owner}/{repo}/{number}`, new)

**The header** shows the PR's title, repo, number, author, head sha, and an issue chip for each Linear issue the PR's record links to (the `linear` feed already stores those links). The chip opens `sindri://linear/<KEY>`, a sheet the Linear hub spec adds; until that sheet exists it's plain text.

**The summary strip**, one line under the header: open required checks, the claim state ("Covers Ph2, REQ-E3" / "no doc named"), risky files touched, failing CI. Each part jumps to its panel. Each panel can be collapsed, and the collapsed set is kept per settings name.

**Layout by width.** At 1,280 px and wider: the panel column (420 px) beside the diff. Below that: the panels stack above the diff in one scroll, collapsed except required checks. Sheets (fix task, confirm, library errors) follow the app's rule: a side sheet at 1,100 px and wider, covering the page below it.

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

**Cache.** Under `$AW_SINDRI_HOME/reviews/cache/<owner>/<repo>/<number>/<sha>/`, in owner-only files. Drafts live apart (§8.1), so pruning the cache never touches one. A patch over 1 MiB is held as "too large to show here", with the GitHub link.
- The last 3 heads per PR are kept.
- A PR's cache is dropped 7 days after it merges or closes.
- The whole cache is capped at 500 MB, dropping the oldest PR first.

### 6.3 States

| Where | Loading | Empty | Error |
|---|---|---|---|
| Reviews list | skeleton rows | "Nothing waiting on you and no open PRs of yours." plus where repos are set (Settings → Connections), and "Reviews asked of a team don't show here yet." With no `review.login`: the one-field login prompt. Before Ph2b: §5.6's line | "PR list is from <time>: <source> failed: <reason>", above the last rows |
| Library count and errors sheet | "Library: reading…" | "Library: no design docs yet" and a link to the convention page | "Library: <root> couldn't be read: <reason>" per root |
| `requiredChecks` | a one-line placeholder in the panel's place, so the column doesn't jump (the same for every panel) | "No required checks for this project" | "Couldn't decide checks: <source> failed: <reason>" |
| `docMap` | placeholder | "This PR names no design doc", plus the project's docs | "Couldn't read the library: <reason>" |
| `testPlan` | placeholder | "No test plan rows for the claimed items" | as `docMap` |
| `ci` | placeholder | "No CI runs on <sha7> yet" | "Couldn't read CI: <forge reason>" |
| `evidence` | placeholder | "No evidence found for <sha7>" | "Couldn't read <path>: <reason>" |
| `risk` | placeholder | "No risky paths touched" | — (stored data only) |
| `conversation` | placeholder | "No threads yet" | "Couldn't read threads: <forge reason>" |
| Diff | "Reading 42 files…" | "This PR changes no files" | "Couldn't read the diff: <forge reason>". Offline with a cached head: the cached diff, "as of <time>, the PR may have moved". At GitHub's 3,000-file cap: "GitHub lists the first 3,000 files; open the rest on GitHub" |
| Fix-task sheet | after 300 ms "Sending…", and a second press does nothing (the app's Sending pattern, `sindri-ui.md:147`) | — | §8.2's three outcomes and the half-done text, in the sheet, with the item link |
| Post (release 2) | "Posting…" after 300 ms, button disabled | — | `SND-REVIEW-010`..`013`'s text in the sheet; the draft stays |
| Head moved | — | — | the banner: "The PR moved to <sha7>. Reload to see the new commits. Kept: your N comments. Reset: M ticks (<names>)." |
| Settings → Library | "Reading the folder…" | "No folders yet. Add a folder to index its design docs." | per root: the path, "scanned <time>, N docs, M errors", or the reason it can't be read |

### 6.4 Keyboard and screen readers

- **Focus order**: the summary strip, the panel column top to bottom, then the diff, then the action bar. `F6` moves between the regions.
- **Diff keys** work only when focus is in the diff and no text box has it (the composer, the login field, search): `j`/`k` next and previous file, `n`/`p` next and previous hunk, `c` on the focused line opens the fix-task sheet (release 1) or the comment composer (release 2), `o` folds or unfolds a group, `Enter` on a folded group opens it. They're listed in the app's existing shortcuts sheet under "Review page", not a sheet of their own. Diff lines are list items, not text elements, so a screen reader's own quick-nav letters still reach the reader first.
- **Names**: every control has an `AutomationProperties.Name`. A diff line reads "line 42, added: <text>", "line 40, removed: <text>", or "line 41: <text>"; a hunk header reads "lines 40 to 48"; a folded group's name is "Lockfiles, collapsed, 14 files". A required check reads its state and reason ("Recipe change shows Ui tests ran: fail, ui-tests failed on a1b2c3d").
- **Focus after**:

| Action | Focus goes to |
|---|---|
| Open the fix-task sheet or composer | its text box |
| `Esc`, Cancel or Save in either | the diff line or panel row it came from |
| Send (fix task) | the sheet's outcome text, which is focusable; closing returns to the source row |
| Fold or unfold a group | the group header |
| Open the confirm sheet | its heading; `Tab` stays inside the sheet; `Esc` returns to the button that opened it (`sindri-ui.md:137`) |
| Post done | the outcome line in the action bar |
| Reload after a head move | the summary strip |

- **Announcements** are polite live regions, built by the app's recipe (`sindri-ui.md:244-253`: a dedicated text block whose name is its text, in the tree before it changes): "Reading 42 files", "The PR moved; reload", "Fix task sent to <where>", "Posting failed: <reason>", "Review posted", a tick's new state. Orca on Linux may speak none of them (`sindri-ui.md:268`), so every outcome is also in a focusable element, and focus moves there.
- **Never color alone**: added and removed lines carry `+` and `-`, and check states carry an icon and a word. Colors are the existing tokens (`Brush.Success`, `Brush.Attention`, `Brush.Error`). The diff's text on the added and removed row tints, in both themes, is checked at 4.5:1 by a new `Aw.Sindri.Ui.Rendering.Tests` row, beside the focus-ring one.
- **Tests**: Ph7's UI tests cover the focus order, the key scope (keys do nothing in a text box), the focus-after table and the names; the rendering row covers contrast.

## 7. Review settings (the profile's new `review` block)

### 7.1 Named settings

```yaml
review:
  login: joi-example                        # the owner's GitHub login: which PRs are "yours" and "waiting on you"
  credential: env:AW_REVIEW_GITHUB_TOKEN    # the owner's own GitHub token for posting (D7); env:NAME or command:<argv>, as workflow.forge.credential
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
- **`sindri review settings <owner/repo>`** (new) prints the settings that repo's PRs get, with `extends` resolved and where each value came from, so a hand edit can be checked before the editor exists (Ph9).
- **Globs** follow the `denyPaths` rules. `panels` names each panel at most once, and leaving one out hides it.
- **`review.login`** is a GitHub login (GitHub's name rules). It's set by the Reviews tab's first-run field, pre-filled with the login the forge credential answers (`Self`), which the owner can change.
- **`review.credential`** is a secret pointer, `env:NAME` or `command:<argv>` (`ForgeCredentialSpec`, the same parser as `workflow.forge.credential`), never a value, and it's used only to post (§8.3). Its variable (`AW_REVIEW_GITHUB_TOKEN` by default, or whatever `env:` names) is added to the list a launched Step's environment empties (`LaunchEnvironment.Credentials`), with a test. That covers sessions Sindri launches only: a lane the owner starts by hand inherits whatever the owner's shell exports. So the documented pointer is `command:` (e.g. a keychain or `gh auth token` call), which puts nothing in any environment; `env:` stays for CI and is documented with that caveat. When it's set, `sindri doctor` asks GitHub who the credential is (`GET /user` with it) and warns when that isn't `review.login`. Without it, release 2's post buttons open GitHub instead.

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

`when` is a list of globs. A check applies when any changed file matches any of its globs, and a check with no `when` always applies; `when: []` is refused (`SND-REVIEW-001`), since it reads as "never". `need` holds exactly one key.

| `need` | Shows | Pass when |
|---|---|---|
| `ci: <name>` (optional `app: <slug>`) | pass / fail / running / skipped | The check run with exactly that name (case-sensitive), from that GitHub App when `app:` is set, concluded `success` **on the head sha**. `skipped` and `neutral` show "skipped" and don't pass. A run on an older sha shows "running" until the head's run arrives. All pages of check runs are read |
| `changedWith: [globs]` | pass / fail | Some changed file matches each glob |
| `covers: true` | pass / fail | The body has a `Covers:` line and every ref resolves |
| `body: <regex>` | **claimed in body** / fail | A body line matches (.NET regex, multiline so `^` and `$` are per line, 1 s timeout, ≤ 512 chars). Anyone who can edit the body can write that line, so it never shows as "pass" |
| `manual: true` | needs a tick / ticked | The owner ticked it on this head. A new head unticks it |

A `command:` need is refused (`SND-REVIEW-003`) until the sandbox can run it.

## 8. Actions

### 8.1 Drafts (release 2)

The draft holds the line comments, the body, the test-plan ticks, the manual ticks and the settings pick. In release 1, ticks and the settings pick live in the same file, so they survive a restart; it just has no comments yet.
- **Storage**: one owner-only file per PR, `$AW_SINDRI_HOME/reviews/drafts/<owner>/<repo>/<number>.json`, apart from the cache so pruning never deletes one. Written atomically as the owner types (debounced), keyed by head sha and versioned, capped at 1 MiB. It is not in the ledger (D8).
- **The composer** (release 2): `c` on a diff line, or a drag across lines for a range, opens it under the line. It holds Markdown text and an optional GitHub suggestion block. A draft comment can be edited or deleted from its line or from the action bar's list.
- **When the head moves**: comments on lines the new diff no longer has are kept as "outdated", and listed for the owner to move or drop. Manual ticks reset (§7.3), and the head-moved banner names them (§6.3).
- **Discarding** deletes the file.

### 8.2 Send as fix task (release 1)

**Send as fix task** is on every failed required check and unclaimed requirement, and on any diff line or range the owner selects (`c`, or the line's menu). It goes to the lane that owns the PR, never through `/meta` (owner, 2026-11-04, D15). It opens a sheet with a text box (up to 2,000 chars, pre-filled for a check or a requirement) and, below it, exactly what will be posted and where. Pressing **Send**:

1. **Creates an item** (`POST /items`, existing):
   - title `Fix: <first line>`, and the PR's repo;
   - body: the finding (fenced as quoted text), the file and line, the head sha, the PR link, and the head as `branch: <head owner/repo>:<branch>`;
   - `link` = the PR record key.
2. **Posts the finding as a PR thread**, only when the PR's author is `review.login` or the forge credential's own login (an agent PR). It goes through `SubmitReview` with verdict `comment` and one line comment (§8.3), with the marker `sindri.fix:<item id>`, using the forge credential the babysitter uses, because it's Sindri's note, not the owner's review. On anyone else's PR (a contributor's) the sheet says "Item only: Sindri won't post on <author>'s PR" and skips this step.
3. **Reaches the owning lane.** In this order:
   - **a babysitter instance watching the PR**: its comment loop takes the thread. `PrCommentQueue` today drops every thread by Sindri's login or with a `sindri` marker (`PrCommentQueue.cs:140-143`); Ph5 changes that rule, and the same one in `PrSnapshotStep` (`PrSnapshotStep.cs:69,294-295`), so a `sindri.fix:` thread is work; Sindri's own login is trusted by `Classify` for those threads only. The marker goes on the **line comment** (the review body is empty), so one thread is one work item. When the loop marks it `fixed`, it closes the item (`CloseItem`, by the id in the marker).
   - **otherwise, a live agent session on the head**: `sindri agents` (existing) gives each session's working directory; a session matches when that checkout's `origin` is the PR's head repo **and** its branch is the head branch. Matching on the branch name alone would send a fork's `main` to an unrelated session.
   - **no session**: the item stays open in Work as "Waiting on <branch>", and the hook shows it to the first session on that head.
4. **Records `review.fix-sent`**: the item, the PR, the thread link, and where it went, or the failure.

**The hook, `sindri-tasks.sh`** (new, per prompt). Sindri keeps one file per head, `$AW_SINDRI_HOME/fix-tasks/<sha256 of "head-owner/head-repo:branch">.json`, listing the open fix tasks' item ids and PR numbers. The item service rewrites it whenever a fix-task item opens or closes, by any path. The hook starts no .NET process:
- it reads the session id from the hook's input, the checkout's `origin` and branch (`git`, bounded), and that one file;
- it prints one line per task **not yet shown to this session**: "Fix task 01J… on #2071 is waiting. `sindri review tasks` shows it." Ids and numbers only, never finding text, which can come from a PR author;
- it records what it showed in `$AW_SINDRI_HOME/fix-tasks/seen/<session id>` (a session id that isn't letters, digits and `-` is ignored), so each task is said once per session; seen files older than 7 days are pruned by the next run;
- it is silent in judge child sessions (`AW_JUDGE_CHILD`), when there's nothing new, or on any error; it exits 0 always and is bounded at 500 ms. Sessions Sindri launches (`AW_SINDRI_CHILD`) aren't matched in step 3: a launched Step gets its work through its recipe, so the hook stays silent there too;
- providers: Claude Code (`UserPromptSubmit`) and Codex (through `config/hooks/adapters/codex.sh`). Cursor's `beforeSubmitPrompt` can't add context (`config/hooks/adapters/README.md:65`), so a Cursor session sees fix tasks through `sindri review tasks` and the Work item; `.agents/rules/hooks.md` lists the three rows.
The lane fixes the finding and closes the item with `sindri item close`.

**Half-done sends.** If the item is created but the post fails, the sheet says "Item <id> created in Work; posting to #2071 failed: <reason>", with **Retry post**. The retry reuses the item and the same marker, so it can't post twice. If the item can't be created, nothing is posted.

### 8.3 Forge additions (Ph3)

New members on `IForge` and `IPullRequests`, with `ForgeContract` and `PullRequestsContract` extended:

| Member | GitHub | File adapter, `FakeForge` |
|---|---|---|
| `Files(link)` → `PullFile[]` `{path, previousPath, status, additions, deletions, patch?}` | `GET .../pulls/{n}/files`, 100 per page, up to GitHub's 3,000-file cap (the page says when it's hit) | seeded (`aw-forge-fake seed files`, new) |
| `FileText(link, path, sha)` → text, capped at 1 MiB | `GET .../contents/{path}?ref={sha}` | seeded with `seed files` |
| `Get` gains `Checks.Runs[]` `{name, app, conclusion, sha}` | check runs (every page) and commit statuses for the head sha | seeded by `seed checks --runs` |
| `SubmitReview(link, expectedHead, verdict, body, comments[], marker)` → `Posted` \| `AlreadyDelivered` \| `HeadMoved` \| `LineRejected` | Reads the head first, and returns `HeadMoved` when it isn't `expectedHead`. Then `POST .../pulls/{n}/reviews` with `commit_id`, `event` (`COMMENT`, `APPROVE`, `REQUEST_CHANGES`), the body with the marker in an HTML comment (so it doesn't render), and `comments`. A 422 on a comment's line is `LineRejected` | stored in the world |

**Two forge clients, one credential each.** The forge port keeps one credential per client. Sindri builds a second `IForge` from `review.credential` for the owner's posts; the babysitter's client is unchanged. `SubmitReview` takes no credential argument.

It's unproven whether GitHub itself refuses a review on an old `commit_id`. The pre-check doesn't depend on the answer. The owner's heavy write contract (`forge.md`, "The owner's heavy-lane GitHub check") gains a review on a scratch PR that records it (release 2).

**Posting (release 2).** `POST /reviews/{...}/post` (new) takes the draft version, the verdict and the confirm token.
- **The confirm sheet** shows the verdict, the body, every line comment with its file and line, and "Posting as @<login>", where the login is what GitHub answers for `review.credential` (`GET /user`), not what the profile says. Accepted suggestions still say "Suggested by <session>".
- **Checks again at post time.** Opening the sheet re-reads the head's checks. A required check that went from pass to fail since the page loaded is listed at the top of the sheet.
- **The confirm token** is issued when the sheet opens, through the app's own channel, used once, bound to the draft version, the head sha and the verdict, and gone after 10 minutes.
- **Approve** isn't offered on the owner's own PR (GitHub refuses it).
- **The post** is one `SubmitReview` with marker `owner-review:<draft id>:<draft version>`, so a retry can't post twice. The marker doesn't start with `sindri`, so a babysitter on the PR still takes the owner's comments as work. Text is scrubbed before it's sent.
- **After**: Sindri records `review.posted` (verdict, sha, comment count and review link; no comment text) and deletes the draft.
- **Human-only**: a session's post gets `SND-AUTH-002`.
- **`HeadMoved`** keeps the draft (§6.3's banner). **`LineRejected`** keeps the draft and marks the comment GitHub refused (`SND-REVIEW-013`).

### 8.4 What agent sessions can do

- **Read**: `sindri review list|show --json` and `sindri_review` return what the page shows, scoped to the session's audience (§5.4): a doc the session may not read is left out of `docMap` and `testPlan`, and the panel says "1 doc not visible to this session".
- **Suggest** (release 2): `sindri review suggest <pr> --file F --line N --text T` (new) adds a **suggested comment** to the page. It's shown apart from the owner's draft, under "Suggested by <session>", for the owner to accept into the draft or dismiss.
- **Their own fix tasks**: `sindri review tasks` lists them, and `sindri item close` closes them.
- **Refused** (`SND-AUTH-002`): writing the draft, ticking anything, posting, and sending fix tasks.

**The owner's CLI.**
- `sindri review list [--project P]`, `show <pr-link>`, `fix <pr-link> --text T [--file F --line N]`, `settings <owner/repo>`, `stats [--since D]`.
- `post <pr-link> --verdict comment|approve|request-changes` (release 2; `request-changes` is GitHub's `REQUEST_CHANGES`): prints the confirm text and asks for `y` on a terminal.

## 9. Errors (new)

| Code | When | Text |
|---|---|---|
| `SND-REVIEW-001` | a bad `review` key: a panel, glob, regex or login, a duplicate id, an unknown `settings` or `extends`, a cycle, `when: []`, a `need` with other than one key | names the key path and the allowed values |
| `SND-REVIEW-002` | a `review.projects[].project` not on the Projects page | "No project <id>. `sindri projects` lists them." |
| `SND-REVIEW-003` | a `command:` need | "Command checks wait for the sandbox. Use `ci:` with the CI job that runs it." |
| `SND-REVIEW-004` | a review route for a host or repo Sindri doesn't sync | "Sindri doesn't sync <owner/repo>. Add it to `context.sources.github.repos`." |
| `SND-REVIEW-005` | a `review.projects[].repos` entry not in `context.sources.github.repos` | names the key path |
| `SND-REVIEW-010` | posting a draft whose version changed since the sheet opened | "This review changed in another window. Reload it." |
| `SND-REVIEW-011` | posting when the head moved | the head-moved banner's text (§6.3) |
| `SND-REVIEW-012` | posting with no `review.credential` | "Set `review.credential` to your own GitHub token to post from Sindri." Shown when the confirm sheet opens, with Post disabled (§16) |
| `SND-REVIEW-013` | GitHub refused a line comment (the line isn't in the diff it has) | "GitHub refused the comment on <file>:<line>. Move it to a line in the diff, or drop it." |
| `SND-LIB-001` | a bad `context.sources.library` key, including a root at `/`, the home folder, or holding `$AW_SINDRI_HOME` | names the key path and the allowed values |
| `SND-LIB-010`..`019` | §4.2's table, one code each | file, line and the fix |
| `SND-LIB-021` | a `Covers:` ref that doesn't resolve, or a second `Covers:` line | "#2071 claims harbor-booking-tdd#Ph9, and that doc has no Ph9." |
| `SND-LIB-022` | a short `Covers:` ref two docs match | "harbor-booking-tdd is in two places; write doc:<root>/harbor-booking-tdd#Ph2." |
| `SND-LIB-404` | `GET /library/{doc}` (and `sindri library show`) names a doc the library doesn't have, or one the caller may not read | "No design doc 'harbor-booking-tdd'; `sindri library list` names them." |
| `SND-REVIEW-006` | a fix task's item was created but its thread wasn't posted | §8.2's half-done text |
| `SND-REVIEW-007` | a review route or verb before `review.login` is set | "Set your GitHub login on the Reviews tab first." |
| `SND-REVIEW-404` | a review route for a PR the forge does not have | "PR not found" |
| `SND-REVIEW-014` | a confirm token used twice or older than 10 minutes | "This confirmation expired. Open the sheet again." |
| `SND-REVIEW-503` | offline, and the PR is not in the cache (ProblemDetails, HTTP 503) | "Offline, and this PR isn't cached yet" |

Codes 008, 009 and `SND-LIB-020` are left free on purpose, for the next errors of the same kinds.

Registered in `docs/csharp/sindri.md`'s error table; `SND-LIB-01x` are also listed on `docs/conventions/design-docs.md`. An older Sindri refuses a profile with `context.sources.library` or `review` keys as unknown, so downgrading means removing them first (`sindri-profile.md` says so). A doc with `aw-doc: 2` is stored by a v1 reader with `SND-LIB-010` "convention version 2 needs a newer Sindri" and no items.

## 10. Security

- **Doc and forge text is untrusted.** It's fenced in API and MCP results, scrubbed before it's stored, and shown as plain text, never markup that loads anything. In fix tasks the babysitter and lanes read, it's fenced as quoted text.
- **Work notes stay on the laptop.** Notes without front matter are never read into the store (D2). Library text reaches no model call through Sindri's own passes or the reranker unless the owner sets `models: true` on a root (D13). **But** a doc naming one synced repo is readable by that repo's agent sessions (§5.4), and a session is a model: put `repos:` on a vault note only when that repo's sessions may read it.
- **Who sees library docs**: §5.4. They're owner-only (`owner:library`) unless a doc names exactly one synced repo.
- **The owner's posting token** is emptied in every launched Step's environment (D7), and the confirm sheet names the account GitHub reports for it.
- **Posting is the owner's** (D7): human-only, with the owner's own credential, after a confirm sheet that shows the account and every comment.
- **The PR author can't steer required checks** (D6), and `body:` checks never show as pass.
- **No PR code runs** (D11).
- **Limits**: symlinks aren't followed, and files over 1 MiB and docs over 2,000 items are stored with an error and no items (§4.1, §5.3).
- **Routes accept only synced repos** on allowed hosts, with path segments checked (§6.2).
- **Drafts aren't in the ledger** (D8), and `review.posted` holds no comment text.
- **Fix tasks**: the hook prints only item ids and PR numbers, never finding text, and stays silent in child sessions (§8.2). A task reaches a session only when both the head repo and the branch match. Sindri posts a thread only on PRs by the owner or its own agents, and the sheet shows the exact text first. The babysitter and lanes read the finding fenced as quoted text.
- **Fixtures are made up** (D16).

## 11. Docs to update

- **Profile and CLI**: `docs/csharp/sindri-profile.md` (`context.sources.library.*`, `review.*`); `docs/csharp/sindri.md` (the CLI table, the error table, the new hook's cli edge).
- **UI**: `docs/csharp/sindri-ui.md` (the ninth tab, the minimum width, the review page).
- **Forge and PR line**: `docs/csharp/forge.md` (`Files`, `Checks.Runs`, `SubmitReview`, `seed files`, the heavy contract's review); `docs/csharp/sindri-pr.md` (`sindri.fix:` threads are work for the comment loop, and closing the item on `fixed`; the owner's `owner-review:` marker).
- **Index and data**: the owned index docs (the `library` feed, the four tables, the PR record's requested reviewers, the `owner:` audience key in the owned index spec §4.4's key list); `planning/ERD.md` (migration 0031; Ph2b took 0030 for the PR record's requested reviewers and reviews; at Ph1's start, check no open PR has taken 0031).
- **Hooks**: `.agents/rules/hooks.md` and the provider installers (`sindri-tasks.sh`, which providers get it).
- **Launch**: `LaunchEnvironment.Credentials` gains the review token's variable; the line spec's LN-LCH1 list says so.
- **Skills and conventions**: `skills/_shared/pr-body.md`; the three skills; `docs/conventions/design-docs.md` (new).
- **Repo files**: AGENTS.md's Commands (`sindri library …`, `sindri review …`); `deps.yaml` (the hook's edge); `.env.example` (`AW_REVIEW_GITHUB_TOKEN`, the documented example pointer); `src/Sindri/aot-smoke/expected.txt` (the `library` doctor row).

## 12. Phases and parallel plan (for /specToProvenPR)

| Phase | Release | Lane | What | Depends on | Proof |
|---|---|---|---|---|---|
| **Ph0** | — | ideas | The convention page (with a copy-paste first doc) and `schemas/library/design-doc.schema.json`. Made-up fixtures in `tests/fixtures/library/`: a TDD; an officeHours plan in its real shape, with REQ refs in its engineering and tasks docs, a Traceability table repeating each REQ, ACs in their own section, and TASKS.md's `- reqs:` / `- depends:` lines; near misses (`aw-doc: "1"`, `1.0`, a blank first line); a stage map whose `parent` is a repo spec's full ref; a stage map and verification plan; a doc with a fenced example; `Ph01` vs `Ph1`; a rerun pair; two repo-slugs sharing an id; one broken doc per `SND-LIB-01x`; a vault with `.obsidian/`, a private folder, a symlink and a note without front matter. Also the baseline count of this repo's last 20 merged PRs, both ways, and the go/no-go (§2) | — | a schema test over the fixtures: the good ones pass, and each broken one fails with its rule |
| **Ph1** | 1 | context | `DesignDocParser`; itemizing in `RecordBatches.From` (doc records of `repo-docs` and `library` only); migration 0031 and the `IRecordStore.Commit` rows, deleted with the record's `removed_at`; claims from PR records, resolved on read; the parser version in the cursor fingerprint and the re-read check; the `owner:` audience key and its rules (§5.4); `IContextLibrary` with the caller's scope; the ERD | Ph0's draft schema | unit: the parser over every fixture; the three audience tests (§5.4). integration: **an existing `aw-doc` repo spec (this file) itemizes on the first tick after upgrade with no new commit**; a PR body's claims land, and one written before its doc resolves once the doc lands; a removed doc's rows go and its id passes to the next doc |
| **Ph2** | 1 | context | The `library` feed (front-matter files only, near misses counted); `context.sources.library` keys and rules; the zero-repo catalog change; the doctor row; `library sync`; the decision-pass and reranker skips (D13) | Ph1 | unit: keys, `~/` expanded before the check, refused roots, built-in deny, symlinks, caps, skipping a file without front matter, near misses. integration: a tick over the fixture vault, a removed file, an unreadable root that removes nothing; no model call for the `library` source with `models: false`. aot-smoke `expected.txt` |
| **Ph2b** | 1 | context | PR records gain requested reviewers (§5.6; the author is already stored), and the owner's latest reviewed commit with the new-commit count (D21) | — | unit: the mapping; the reviews read skipped when `updated_at` and head are unchanged; a 404 compare stores the count as unknown. integration: a GitHub fake PR with two reviewers; a PR reviewed by the owner then pushed to shows as "reviewed, 2 new commits" |
| **Ph3** | 1 | api | `Files`, `Checks.Runs` (every page; conclusion and app) and `SubmitReview` (GitHub, file, fake; release 1 uses only verdict `comment`); `seed files` and `seed checks --runs`; the `IPullRequests` members | — | contract tests over the fake and file adapters; stubbed-HTTP unit tests, including a 422 line and a second page of check runs |
| **Ph4a** | before 1 | ideas | officeHours (front matter, TASKS.md list-item keys, reruns), system-design (`doc.md`), specToProvenPR and `pr-body.md` write the convention and `Covers:`; the one-line skip when `sindri library` is missing; dark-factory's TASKS.md wording | Ph0's draft schema (the same draft Ph1 starts from) | the system-design suites; fixtures from real runs pass the schema; each plan-reading skill still names a fixture plan's sections with front matter on it |
| **Ph4b** | after 1 | api, then ideas | The `type: command` grader in `scripts/skill-evals.sh` (api), with selftest rows. Then one eval case per skill (ideas), officeHours with pre-filled answers. Runs go through the spend line | Ph4a, Ph5's `library validate` | the selftest; the three cases pass |
| **Ph5** | 1 | api (the `PrCommentQueue` change with the lane that owns the babysitter) | `sindri library list|show|validate|sync`, `GET /library` and `GET /library/{doc}`. The `review` block and its rules; `review.credential` in `LaunchEnvironment.Credentials`. `GET /reviews` and `GET /reviews/{pr}`: the page model, one-head reads, the cache, check evaluation, `Covers:` from the PR's own files, the evidence lookup measured and wired, audience-scoped reads for sessions. Fix tasks: item, thread (owner's and agent PRs only), the repo-and-branch session match, the per-head task files; `PrCommentQueue` takes `sindri.fix:` threads and closes the item on `fixed`. `sindri review list|show|fix|tasks|settings|stats` and `sindri_review`; the `review.opened` event; the `sindri-tasks.sh` hook and its installer rows; catalog rows | Ph1, Ph2b, Ph3 | unit per check kind (`skipped` doesn't pass), mapping rule and fix-task branch (half-done, no session, contributor's PR, a fork's `main` matching nothing); the scrub test for the review token; API goldens; `SND-AUTH-002` for a session's fix; the hook test in `config/hooks/tests/` (silent, once per task per session, ids only, silent under `AW_SINDRI_CHILD`, exit 0, under 500 ms with no `sindri` on PATH) |
| **Ph6** | — | ui | Mockups: the Reviews list, the review page (every §6.3 state, both widths), the confirm sheet, Settings → Library. Then the owner's pick | — | the pick, recorded here |
| **Ph7** | 1 | ui | The Reviews tab (`G V`, nine tabs, the waiting-count badge) and Home's waiting line; the login field; the review page (release 1: the summary strip, collapsible panels, both layouts, the fix-task sheet from a check, a requirement or a diff line; the review buttons open GitHub); Settings → Library; the library count and errors sheet | Ph5's goldens (hand-written first), Ph6 | UI tests: nine tabs at the minimum width, the chord, deep links, panel order from settings, folded groups, §6.4's key scope, focus-after table and names, every §6.3 state, the fix-task flow on the fake forge. The contrast row in `Aw.Sindri.Ui.Rendering.Tests`. A screenshot run on the fixture |
| **Ph8** | 2 | api, ui | Drafts (the file) and the composer; suggested comments (`review suggest`); `POST .../post` with the confirm token; the owner's second forge client; the confirm sheet with the checks re-read; `sindri review post`; `GET /library/{doc}/items`, `library items` and `sindri_library`; the heavy contract's review | release 1, and the stop line (§2) | unit: draft versions, outdated comments, `HeadMoved`, `LineRejected`, a retry with one marker, the token's one use. UI tests for the composer and the sheet. A session's post is `SND-AUTH-002` |
| **Ph9** | 2 | ui | The project sheet's Review settings editor, with one-click approval | Ph7 | UI tests; an edit round-trips through `profile approve` |
| **Ph10** | — | — | The owner proof (§13) and the two-week measure | each release | §13 |

**Parallel plan.**
- **Day one**: ideas on Ph0's draft schema, then Ph4a at once, with Ph0's fixtures and baseline count after it (meta's UX check: the doc map is mostly empty until the skills write the convention, so Ph4a goes first); context on Ph1 (from Ph0's draft schema) and Ph2b; api on Ph3; ui on Ph6's mockups.
- **Then**:
  - context: Ph1 → Ph2;
  - api: Ph3 → Ph5 (once Ph1's read path and Ph2b land) → Ph4b's grader → Ph8;
  - ui: Ph6 → Ph7 on Ph5's hand-written goldens → Ph9 and Ph8's sheet.
- **Release 1 ships** when Ph1, Ph2, Ph2b, Ph3, Ph5 and Ph7 are in. Ph4a merges as soon as it's ready, ahead of release 1, so PRs written while release 1 is built already carry `Covers:`; Ph4b follows.
- **The spend line** is the team's rule for paid eval runs: the lane sends its cost estimate to meta and starts only after meta's OK.

**Gates per phase.**
- every phase: the `-warnaserror` build, the touched projects' unit and integration lanes, scoped format;
- `Aw.Architecture.Tests` (new folders and usings): Ph1, Ph3, Ph5, Ph7, Ph8;
- `check-profile-doc.mjs`: Ph2, Ph5, Ph8;
- `check-env-example.sh`: Ph5, Ph8;
- the ApiGoldens classes: Ph5, Ph8;
- `Aw.Sindri.Ui.Tests`: Ph5's catalog rows, Ph7–Ph9; `Aw.Sindri.Ui.Rendering.Tests`: Ph7;
- `bash src/Sindri/aot-smoke/run.sh`: Ph2;
- the hook tests and `setup.sh --dry-run`: Ph5;
- the skill-evals selftest: Ph4b.

## 13. Proof

| Row | Check |
|---|---|
| **TP-1** | The fixture vault and `aw` tree on a scratch Sindri home: every good doc's items match the schema's expected list, and each broken doc is stored with its one error and no items. Absent from the store: `.obsidian/`, the denied folder, the symlink target and the note without front matter. A fenced example makes no items, and officeHours' REQ refs in its engineering and tasks docs are refs, not duplicates. In the officeHours fixture, each REQ is one item, each AC's parent is its Traceability row's REQ, and TASK `reqs:` / `depends:` are refs. The near misses are on the doctor row and stored nowhere. Verifies REQ-7, REQ-12 |
| **TP-2** | `sindri library validate` gives the index's verdicts file by file, and `--pr-body` flags a broken `Covers:` ref. Verifies REQ-9 |
| **TP-3** | One eval case per skill passes the `command` grader, and specToProvenPR's PR body's `Covers:` resolves. Verifies REQ-10 |
| **TP-4** | On the file forge, a PR with `Covers: harbor-booking-tdd#Ph2` shows Ph2 highlighted, its `TP` rows, a second PR's claim on Ph1 as "claimed by #N", and `REQ-E5` unclaimed. A PR with only `Part of #412` shows the suggestion. A PR that adds its own spec and claims it shows "in this PR". A PR synced before its doc resolves once the doc lands. Verifies REQ-2, REQ-8 |
| **TP-5** | The same PR touching `packages.lock.json` and `.github/workflows/ci.yml` shows the lock folded with a count, and the workflow file marked with its reason. Verifies REQ-3 |
| **TP-6** | Each `need` kind, applicable and not: pass, fail, running (a run on an older sha), claimed in body, and a manual tick reset by a new head. A PR claiming a doc of another project still gets its repo's checks. Verifies REQ-4 |
| **TP-7** | A fix task from a diff line with a babysitter watching: a `sindri.fix:` thread, which the comment loop takes, and the item closes on `fixed`. With a live session on the head repo and branch: the hook prints the one line at its next prompt, once, and the session closes the item; a session on another repo's `main` hears nothing. With neither: a thread and an open item. On a contributor's PR: an item and no thread. A post failing after the item: the half-done message, and Retry posts once. Verifies REQ-6 |
| **TP-8** | The list holds exactly the owner's PRs and the PRs requesting the owner's review, with "Waiting on you" first, and Home and the tab badge show the same waiting count. Verifies REQ-1 |
| **TP-9** | Keyboard only: open Reviews, open a PR, walk files and hunks, open a folded group, send a fix task from a diff line, and land where §6.4's table says after each step. Typing `j` in the fix-task text box types a `j`. Screen reader names on checks, diff lines and folded groups; the fix-task outcome is focusable. Verifies REQ-13 |
| **TP-12** | Audience: an `owner:library` vault doc is shown to the owner and absent from `sindri review show --json` and `sindri_review` in a session of the PR's repo, even when the session's map links to it; a vault doc with `repos: [<that repo>]` is shown to that session. The review token's variable is empty inside a launched Step. Verifies REQ-11 |
| **TP-10** | Release 2 on the file forge: Approve with two line comments shows the confirm sheet with the account, and posts one review with the marker. A retry posts nothing, and a moved head keeps the draft. A session's post and a session's tick are `SND-AUTH-002`. A session's `suggest` shows under "Suggested by". Verifies REQ-5, REQ-11 |
| **TP-11** | On the owner's laptop: the real vault and `~/.agentic-workflow` are indexed with no model call made (the ledger shows none for the `library` source). Reviews shows this repo's PRs, one review is posted to a scratch PR as the owner (release 2), and with the network off the page opens from the cache with "as of". `sindri review stats` prints the trial's counts. Verifies REQ-1, REQ-5 |

## 14. Questions for the owner

Decided (owner, 2026-11-04):
1. **Where Reviews goes**: the ninth top-bar tab (D14).
2. **Fix tasks for lane PRs**: they go to the owning lane only, never through `/meta` (D15, §8.2).
3. **Which PRs the list shows**: PRs the owner authored, plus PRs requesting the owner's review (§6.1).
4. **Old docs without front matter**: ignored, so they're not indexed or itemized (D2).
5. **Vault pickup within 5 minutes**: fine (§5.2).
6. **How Sindri reaches the vault**: Sindri runs on the work laptop, so the vault is a local path in the profile (D12, §5.1). No sync.

Decided (owner, 2026-11-05; raised by the reviews):
7. **Posting as you**: release 2 posts with your own GitHub token (`review.credential`, a pointer, never a value), not the agent App token (D18, §8.3).
8. **Release 1 without posting**: release 1 is read-only and its review buttons open GitHub; posting from Sindri comes in release 2 (D10, D19).
9. **The fix-task hook**: the new per-prompt hook, `sindri-tasks.sh` (D20, §8.2).
10. **PRs you already reviewed**: back in "Waiting on you" as "reviewed, N new commits" when new commits land (D21, §5.6, §6.1, Ph2b).

Decided at Ph6 (owner, 2026-11-06): the layout, a check's fix task, and four smaller calls; §16.

Open: none.

## 15. Left for the phases (autoplan round 3)

Round 3 passed in all five lenses (no critical or high findings; `plans/pr-review-page-r3/consolidated-review.md`). These medium findings are settled in the phase that builds the part, and each phase's plan names how:
- **Ph1**: store a claim's parsed parts (doc id, item id) with an index on them, so "claimed by #N" doesn't scan every claim.
- **Ph3**: the owner's posts go through a narrow port over the second client (`Self`, `SubmitReview`), and `aw-forge-fake` gains a second acting login, so TP-10 can tell the owner from the App.
- **Ph5**: split into Ph5a (library CLI and routes), Ph5b (review block, page model, checks) and Ph5c (fix tasks, `PrCommentQueue`, the hook), each with its own proof. Every new verb follows `sindri`'s CLI rules (`--json`, exit 0/1/2, reads never start serve), written in `docs/csharp/sindri-review.md` and `sindri-library.md` (new).
- **Ph6** (mockups): the page's own width decides the layout, including when a side sheet is open; a whole-page state row (merged or closed, not found, not served, offline with no cache) and a composer row; the action bar's names ("Approve on GitHub" in release 1) and focus; a file list with viewed marks, and the binary, renamed and deleted texts; the Reviews list's row names and `J`/`K`/`Enter`; the summary strip's buttons, panel toggles and the tick's `Space`; ranges by `Shift+j`/`Shift+k` or a hunk header.
- **Ph7**: whether Settings → Library waits for release 2, with release 1 keeping the header count and `sindri library list --errors`; the owner's pick at Ph6.
- **Owner steps**: the final text adds restarting a running serve after `./setup.sh`, `sindri mcp register` for `sindri_review`, the first tick's up-to-5-minute wait, and a "worked" check (`sindri doctor`'s library row, `sindri review list`).

## 16. Ph6 results (owner, 2026-11-06)

The mockups are in `2026-11-04-sindri-pr-review-page-design/mockups/` (`README.md` lists every screen). Ph7 builds what they show.

**The owner's picks.**
1. **Layout A**: the panel column beside the diff, as §6.2 has it (`screens/review-a-1440.png`), not the diff-first tabbed rail.
2. **A fix task from a failing check or a requirement** has no diff line, so its `sindri.fix:` thread is a comment in the review body. A fix task from a diff line stays a line comment (§8.2).
3. **No `review.credential`**: the confirm sheet opens with Post disabled and `SND-REVIEW-012`'s text, never failing after the press.
4. **The owner's own PR**: Approve and Request changes are shown disabled with "You can't approve or request changes on your own PR: GitHub takes a comment only", and Comment is the verdict (GitHub refuses both on the author's own PR; §8.3's "not offered").
5. **Home's line** "N PRs waiting on you" sits under the Needs-you rows and isn't counted in them; it opens Reviews.
6. **Near misses** show in the library errors sheet as well as in `library validate` and the doctor row (§4.2 changes to match §6.1).

**Measured.** Measured in Ph7: nine full labels don't fit at 1,000 px with 130% text (the pill overran by 30 px), so below 1,100 px "Reviews" shows as "PRs" and "Workflows" as "Flows"; their screen-reader names stay full (owner, 2026-11-06).

**Wording the mockups set** (Ph7 uses it; §6.3 and §8.2 point here):
- Fix-task outcomes: the babysitter took it, a live session on the branch took it, or "Waiting on <branch>": the item waits in Work. `SND-REVIEW-006` is the half-done text (§8.2), with Retry.
- "PR not found"; "Offline, and this PR isn't cached yet"; before `review.login` is set, the whole page shows the list's login prompt ("Set your GitHub login", one field, Save; `SND-REVIEW-007`).
- The library header when a folder can't be read: "Library: 33 docs, 1 folder can't be read".
- Files: "Renamed from <old path>, 91% the same"; "Deleted file: 61 lines removed. Contents hidden." with Show; "Binary file, not shown here." with Open on GitHub.

**Spec fixes found while drawing.**
- The summary strip has its own states: "Reading checks…", "No doc named", "Couldn't decide checks". Focus after Reload, and after a fix task whose source row is gone, lands on the strip's first segment.
- `risk` reads stored data only, so it has no error state.
- Release 1's head-moved banner drops "Kept: your N comments" (no comments until release 2); release 2's counts outdated comments with the kept ones.
- §7.1 `groups[]` gains `folded: true|false` (default false), the flag behind "Lockfiles, collapsed, 14 files".
- At GitHub's 3,000-file cap the header says "3,000 files" and the file list is what GitHub gave.
- A "claimed in body" check counts as open in the strip's count; "Send as fix task" shows on failed checks and unclaimed requirements only.
- The list: zero open required checks reads "none open"; project groups come before repo-name groups; the hint line shows `J`/`K`/`Enter` and `G V`.
- Settings → Library is a section of the Settings page (titled cards, no left nav), beside Connections. Repo roots are listed read-only ("set by the repo list"); the `aw` root has Turn off, not Remove.

## 17. Ph0 baseline (ideas, 2026-11-06)

The last 20 merged PRs in this repo (#2101 to #2133, 2026-11-05 and 06), counted the §2 way: would a doc map the PR by issue (`Part of` / `Fixes #N` in the body against a doc's `issues:`) or by branch (the head branch holds a doc id)?

| Count | Today | As if every doc had front matter |
|---|---|---|
| By issue | 0: no doc has `issues:` yet | 3: #2125 and #2126 (`Part of #2076`, this spec) and #2127 (`Part of #2077`, the Linear hub spec), with each spec's `issues:` holding the number lanes track it by |
| By branch | 0 | 0: branches are named by phase (`feat/review-ph2b-reviewers`, `feat/linear-hub-ph0`), never by the doc id (`sindri-pr-review-page`) |
| By `Covers:` (not part of the §2 count) | 2: #2124 and #2132, both written after Ph4a's convention | 2 |
| Never | 13 bug fixes with no design doc behind them: "This PR names no design doc" is the right answer | 13 |

Two more are one hop away: #2114 says `Part of #2112`, an issue under the model routing spec's #1567, and #2104 fixes #2095, a follow-up of the model check spec. They map only if a spec's `issues:` also lists the follow-up issues filed under it.

**Go/no-go.** 3 of 20 map by issue or branch even with front matter, under §2's 5. So Ph4a and the `Covers:` flow ship before Ph7 and the trial waits for them. Ph4a merged (#2132), so the `Covers:` line is in every specToProvenPR PR from here, and the trial's own start rule (5 open PRs with `Covers:`) is the wait. Issue and branch suggestions stay a fallback, not the main path.

**The owner's review time today**: not measured (owner, 2026-11-06).
