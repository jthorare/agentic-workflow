# Sindri's Reviews page, v3: review a PR in Sindri, not on GitHub

Status: draft (ideas lane, 2026-11-12). Serves #2980 (Reviews: a GitHub PR review replacement with full parity) under #2978 (one app that replaces the others). It builds the Reviews rows of the prior-art ledger (`docs/sindri/prior-art/reviews.md`, #2989). It sits beside the Home spec (`docs/superpowers/specs/2026-11-12-sindri-home-inbox-design.md`) and the Agents spec (`docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md`), and reuses their row / peek / full grammar, keys and stable-order rule.
It supersedes `docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design.md` where §2 says so. Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

"Take N" means the numbered item in the ledger's closing list "What we take". "Ledger N" means problem N in the same file.

## 0. Why

The owner's words on #2980: the mega app replaces the existing apps without losing function, "same for PR reviewing". Today the owner still opens github.com for every verdict.

What the Reviews tab does on main (read from `origin/main` d211e1b163; issues linked; the app was not driven for this spec):

- It lists PRs in two groups, "Waiting on you" and "Yours". `ReviewListBuilder` (`Core/Reviews/ReviewListBuilder.cs`) puts a PR in the first when the owner is a requested reviewer or reviewed an older commit (`ReviewWaitingKind.Requested`, `NewCommits`). The row says no reason beyond that, and a team request never shows.
- It reads one PR page: `GET /sindri/reviews/{owner}/{repo}/{number}` returns `ReviewPageModel` with seven panels (`requiredChecks`, `docMap`, `testPlan`, `ci`, `evidence`, `risk`, `conversation`) and a diff grouped by folder. The diff is unified only (`ReviewPatch.Hunks`).
- It writes two things: `POST /sindri/reviews/login` and `POST /sindri/reviews/{owner}/{repo}/{number}/fix` (a fix task). Approve, request changes and comment open github.com (#3099).
- The forge port is ahead of the app. `IPullRequests` has `Files`, `FileText` and `SubmitReview` (`Core/Forge/IPullRequests.cs:124-138`), `Reply`, `React`, `Merge`, `RerunChecks`, `Update` and `Retarget`. `SubmitReview` returns `Posted`, `AlreadyDelivered`, `HeadMoved` or `LineRejected` (`Aw.Forge.Core/Pulls/ReviewOutcome.cs`). No page calls it except the fix task.
- It has no composer, no draft, no viewed mark, no resolve, no re-request, no merge button, no concept order, no since-last-review diff and no agent pre-review.
- Its open bugs: #2791, #2793, #2936, #3065, #3092, #3100, #3132. They are fixed in their own PRs and are not repeated here.

The job: **review a PR from the queue to the merged button without opening github.com. Read it in the order the change makes sense. Let an agent pre-read it, and verify each thing the agent says. Every action lands on GitHub.**

## 1. The bar

The rows below are the ledger problems this spec builds. Ledger 6 (structural diff), 10 (stacks) and part of 12 (merge queue, auto-merge) are in §4 as "later" with a reason.

| Step of a review | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Know which PRs wait on me, and why | 1 | Gerrit attention set | a reason string per row; Graphite's section list; bots never put a PR on the list | §3.1 |
| Rank by what unblocks someone | 2 | CodeRabbit Triage | priority and next action as two columns, never merged | §3.1 |
| Read in change order | 3 | CodeRabbit Change Stack | numbered layers with stored dependencies; Files mode one key away; ours comes from the code index, not a model guess | §3.2 |
| Mark files viewed, keep my place | 4 | GitHub Viewed, Reviewable | `v`, a "12/41 viewed" count, write-through, a mark survives an unchanged push | §3.3 |
| See only what changed since my look | 5 | Reviewable | the default view on a re-review; comments follow their line | §3.4 |
| Comment, suggest, batch, resolve | 7 | Graphite, octorus | `c`, `s`, Ctrl+Enter to batch; no dispositions | §3.5 |
| Verify an agent's findings | 8 | CodeRabbit findings, octorus (read-only) | one row per finding with accept, edit, drop; an evidence line | §3.6 |
| CI at the failing line | 9 | none found | ours to design: a gutter mark and a log excerpt on the hunk | §3.8 |
| Jump to context from a hunk | 11 | octorus symbols | `complete`, `partial` or `none` on every caller list | §3.2 |
| Approve and merge with confidence | 12 | CodeRabbit refuse-on-stale | refuse on a moved head; a ready line | §3.7 |
| Big PR, keyboard, search | 13 | octorus, Change Stack | virtualized diff, palette, `/` search | §6 |

**The bar in one line:** the owner reviews 10 real PRs (3 large) in Sindri only, faster than on github.com, and every write shows on GitHub within 2 s.

Done-checks. Each is a test or a measured run in the real app (aw-ui-driver) on a scratch home, never the owner's.

| # | Check | Measured as | Owner · phase |
|---|---|---|---|
| D1 | **The 10-PR day.** ux reviews 10 merged-then-reopened fixture PRs (3 over 100 files) in Sindri only. Each takes less time than the same review on github.com. Each action shows on GitHub. | ux times the same 10 PRs on github.com first, with the same script (§12 question 6). Count of times ux opens github.com in the Sindri run: 0. | ux · R14 |
| D2 | **Nothing posts without one click.** A pre-review run, a finding, an accepted finding and a draft write nothing to GitHub. Only a click on a verdict in the submit sheet writes. | A contract test over the fake forge: after a full pre-review and a full draft, the fake records 0 writes. After the click it records exactly one `SubmitReview`. | api · R3 |
| D3 | **A session cannot post.** `post`, `merge`, `viewed`, `resolve` and the other owner writes return `SND-AUTH-002` to a session caller. `suggestions` is the one write a session may make. | A test enumerates every write route and the caller class. | api · R3 |
| D4 | **Change order is stable.** The same head gives the same order, byte for byte, and the order is cached per head sha. | Run 20 times over each of the 10 fixture PRs. The p95 for a 50-file diff is under 2 s cold and under 100 ms warm. | context · R4 |
| D5 | **No false "no callers".** Every caller list carries `complete`, `partial` or `none`. An empty list with `partial` or `none` never reads as "no callers". | A Frames test over the three marks. A test greps the Labels for "No callers" without a mark. | ui · R10; context · R4 |
| D6 | **Refuse on stale.** Submit, merge and a suggestion apply carry the head the owner read. A moved head writes nothing and keeps the draft. | The fake forge moves the head between sheet open and click. 0 writes, the banner shows, the draft is intact. | api · R3 |
| D7 | **Viewed survives a push.** A file whose patch is unchanged after a push stays viewed. A changed file flips to "changed since viewed". The mark also shows on github.com. | Two-head fixture: mark 5 files, push a change to 2, check the 3 stay and the 2 flip. Read the fake's `viewed` list. | api · R2; ui · R10 |
| D8 | **First paint.** The queue paints in under 100 ms from the local store with 200 PRs. A 300-file PR paints its first file in under 200 ms from the cache. | aw-ui-driver timings over 20 runs, p95. | perf · R13 |
| D9 | **Smooth diff.** 10,000 diff lines scroll with frame time under 16.6 ms at p95. A viewed mark repaints in one frame. | The Avalonia frame recorder in the real app. | perf · R13 |
| D10 | **Every pre-review finding is checkable in one line.** Each finding shows its anchor, its why and its evidence line. A finding whose line is not in the head's diff cannot be accepted. | A test per finding kind. A stale finding shows `SND-REVIEW-016`. | engine · R6; ui · R11 |
| D11 | **Parity ledger green.** `docs/parity/github-review.md` has no row without `done`, `missing #N` or `dropped: why`, and every `done` row has a file path. | A script reads the file and fails on a bare row. | ideas · R0 |
| D12 | **Agent parity.** The CLI and MCP give every read and every owner-side op the page has, for the fields a session may see. | A test enumerates the routes and checks each has a verb and a tool (#3165 is the first gap). | api · R3 |

## 2. What changes from the 2026-11-04 spec

The 2026-11-04 spec (`2026-11-04-sindri-pr-review-page-design.md`) stays the base for the library, the doc map, review settings, required checks, fix tasks and the page's cache. This spec supersedes it in the places below. Anything not named here holds by reference.

| 2026-11-04 spec | v3 | Why |
|---|---|---|
| §0 table: no `Files`, no `SubmitReview`, no per-check runs | Both exist (`IPullRequests.cs:124-138`, `PullChecks`). The Ph3 forge additions are built. | Read the code at `d211e1b163`. |
| D10: release 1 read-only, release 2 posts | Release 1 shipped. v3 is release 2 and adds the rest of GitHub's review actions (§3, §4). The order of the phases is in §11. | The direction is full parity. |
| §6.2 and the owner's pick "Layout A": panel column (420 px) beside the diff | A three-pane walk: file tree left, diff middle, threads right. The seven panels move to an **Overview** step that opens first (§5.2). | Direction item "Review surface" on #2980; the tree needs the left rail. Question 1. |
| §6.1: two groups, "Waiting on you" and "Yours", newest first | Sections with a reason per row, and a rank (§3.1). The two groups stay as the first sections. | Take on ledger 1 and 2. |
| §8.1: drafts hold line comments only | Drafts also hold range, file-level and PR-level comments, suggestion blocks, accepted findings and replies (§3.5). The file path and the 1 MiB cap stay. | Parity. |
| §8.3: the confirm sheet follows the verdict button | One **submit sheet** opened by `Ctrl+Enter`. It shows the draft and holds the three verdict buttons. One click on a verdict posts. | Meta's rule: approve is in the `Ctrl+Enter` menu, never a bare letter. The owner rule: one click. |
| §8.4: `sindri review suggest` adds a suggested comment | The same idea, widened to a pre-review's findings with a kind, a severity and evidence. The route is `POST .../suggestions` (§8). | Take 3. |
| §4.4 and the CLI list: `sindri review list|show|fix|settings|stats|post` | None of these verbs exists on main (#3165). They ship in R3 with the same names. | Code check. |
| §14 Q10 and D21: "reviewed, N new commits" | Kept. The row's reason also names the cause (§3.1), and a re-review opens on "since your last review" (§3.4). | Take 4. |
| Prior-art take 6: the pending batch lives on GitHub as a real pending review | It lives in the local drafts file. | Meta's decision on #2980, 2026-11-12, question 2: "Nothing posts without you" stays true even if the app dies. |

What does not change: D7 and D18 (posting is the owner's, with the owner's own token, never the agent App token), D8 (drafts are local files, not ledger events), D9 (one head per read), D13 (library text reaches no model), D16 (made-up fixtures), the `sindri.fix:` marker rules, the fix-task hook, the cache layout, and `SND-REVIEW-001` to `-009`, `-404`, `-503`.

## 3. Decisions

Taken for the owner. Each says where it came from. "Agent default" means nobody ruled on it and the spec picked.

1. **Sindri is a better client, not a fork.** Every write goes to GitHub within 2 s, optimistic, with a visible failure and a retry. GitHub stays the record. (#2978 contract item 2.)
2. **The queue is sections, each row with a reason.** Sections: Needs your review, New commits since you reviewed, Returned to you, Waiting on others, Ready to merge, Drafts. The reason is one phrase: "asked of you", "3 new commits", "2 unreplied threads from @x", "CI red on a1b2c3d". (Take 2; ledger 1.)
3. **Priority and next action are two columns.** Priority orders rows inside a section. The next action says whose move it is. A bot's comment never puts a PR on the list by itself. (Ledger 2 take; ledger 1 take, Gerrit.)
4. **Review order follows the change.** Files are ordered by band (entry, core, data, tests), then by distance from the entry, from the code index. The order is stored per head sha. Files mode is always one key away. (Take 1; context plan on #2980.)
5. **A re-review opens on "since your last review".** One key shows the whole PR. (Take 4.)
6. **`v` marks a file viewed and writes it through to GitHub.** A file with the same patch after a push stays viewed. (Take 5.)
7. **The pending batch is the local drafts file.** A pre-review writes only there, as findings, never as GitHub comments. (Meta, #2980, 2026-11-12; supersedes Take 6's GitHub-side batch.)
8. **Suggestions: `s` writes a suggestion block, apply copies the patch.** Sindri commits a suggestion only on a branch the signed-in account owns. (Meta, #2980, 2026-11-12, question 3.)
9. **A bare letter never commits a write.** Approve, comment and request changes are the three buttons of the submit sheet. (Meta's key rule on #2978.)
10. **Posting is the owner's, with the owner's own token.** A session's post gets `SND-AUTH-002`. (2026-11-04 D7 and D18.)
11. **The pre-review agent is read-only.** It runs in the read-only sandbox, holds no GitHub token, and writes findings through `POST .../suggestions`. (Engine plan on #2980; 2026-11-04 §8.4; ledger 8 take.)
12. **Each finding has three keys: accept as draft comment, edit, drop.** It shows severity, anchor, one line of why and one line of evidence. (Take 3.)
13. **Submit, merge and apply refuse on a moved head.** The row shows a ready line that names what is missing. (Take 7; `HeadMoved` exists.)
14. **The queue is a list on the row / peek / full grammar; the review is a Full surface.** The queue uses `RowList`, `Row` and `PeekPane` on `RowKeys`, with no key handler of its own. (Meta's accepted ui plan on #2978.)
15. **Keys keep the shipped meaning of `j/k` and `n/p`.** `j/k` moves between files, `n/p` between hunks (`ReviewPageViewModel.Keys.cs:73-76`). Question 3 asks about the direction text, which says the reverse. (Agent default.)
16. **Pre-review runs on review request, up to 60 files, else on demand.** `review.preReview` is `auto` (default), `manual` or `off`. (Agent default; question 2.)
17. **Fixtures are made up** ("Harbor ferry booking"). No app-specific names. (2026-11-04 D16.)

## 4. The parity table

Every GitHub function the owner uses in a PR review, with its v3 answer. "Ship" names the phase in §11. "Later" gives the reason. "Exists" means built on main today. The file `docs/parity/github-review.md` (R0) repeats this table with the GitHub keystroke and the evidence path for each row.

### 4.1 Queue and PR page

| # | GitHub | v3 answer | Ship / later |
|---|---|---|---|
| 1 | Review requested, your PRs | Sections with reasons (decision 2). Both lists exist as two groups. | Exists; sections R7 |
| 2 | Requested of a team | Section row "asked of @team". Needs the team in the stored PR record and the `read:org` scope. | Ship R7 |
| 3 | Mentions | Home's `mention` kind. A mention in a PR thread shows on Home and in the PR's threads. | Later: Home owns it (Home spec §3.2) |
| 4 | Search and filters | `/` filters by text, `a:` author, `r:` repo, `s:` section. Saved sections as links. | Ship R9 |
| 5 | Notifications | A review request, new commits and CI red notify through Home (Home §6.3). No second badge. | Ship through Home |
| 6 | PR description and title | Peek and Overview show the rendered body. Edit title and body through `Update`. | Exists (`Update`); UI R10 |
| 7 | Commits list | Commit chips in the header. Picking one shows that commit's diff (§3.4). Needs a `Commits` read. | Ship R2 (read), R10 (UI) |
| 8 | Checks with logs | The `ci` panel lists runs. `CheckLog` text opens under the failing hunk (§3.8). | Exists; inline R12 |
| 9 | Linked issues | Linear chips exist. GitHub `closes #N` refs from the PR body add chips. | Exists; GitHub refs R10 |
| 10 | Labels, assignees | Read labels exist (`PullSnapshot.Labels`). Writes through `.` menu. | Ship R2 |
| 11 | Milestone | The owner reads it nowhere in Sindri. One small write after R2 if the ux day needs it. | Later: no Sindri surface reads it |
| 12 | Draft toggle | "Ready for review" and "Convert to draft" in `.`. Needs GraphQL `markPullRequestReadyForReview` and `convertPullRequestToDraft`. | Ship R2 |
| 13 | Conflicts state | `PullSnapshot.Mergeable` exists. The header shows "conflicts with <base>". The fix is "Start babysitter" (§3.9), not an in-app merge editor. | State R10; editor later: needs a checkout and a merge tool |
| 14 | Update branch | `.` menu, REST `update-branch`. | Ship R2 |
| 15 | CODEOWNERS | A file-header chip "owner: @x" read with `FileText` at the base. | Ship R10 |
| 16 | Close, reopen | `.` menu. | Ship R2 |
| 17 | Retarget base | Exists as `Retarget`. | Exists; `.` menu R10 |
| 18 | Open on GitHub, copy link | `.` menu entry on every row and page. | Ship R9 |

### 4.2 Diff

| # | GitHub | v3 answer | Ship / later |
|---|---|---|---|
| 19 | Unified diff | Kept. | Exists |
| 20 | Split diff | `u` toggles unified and split. | Ship R10 |
| 21 | Whitespace toggle | `w` hides whitespace-only lines, computed from the patch. | Ship R10 |
| 22 | Word-level highlight | Always on, inside changed lines (ledger 6, layer 1). | Ship R10 |
| 23 | Expand context | A fold row loads more lines with `FileText`. | Ship R10 |
| 24 | File tree | Grouped by concept, sorted by band (§5.2). Files mode is the folder tree. | Ship R10 |
| 25 | Change order | Decision 4. GitHub has none. | Ship R4, R10 |
| 26 | Viewed marks | `v`, count in the header, write-through. | Ship R2 (write), R10 (UI) |
| 27 | Rename detection | `PullFile.PreviousPath` exists. Text "Renamed from <old>, 91% the same" is set. | Exists |
| 28 | Large, deleted, binary files | Held back with one key to load them (2026-11-04 §16). | Exists |
| 29 | Since last review, per commit range | Decision 5 and `GET .../range`. | Ship R5 |
| 30 | Search in the diff | `/` with scope file, diff or comment. | Ship R10 |
| 31 | Go to definition, callers | `C` shows callers and callees with a mark (§3.2). GitHub's symbol panel is partial. | Ship R4, R10 |
| 32 | Structural diff, moved blocks | Layer 2 of ledger 6: per language, with a "text diff (no parser)" label. | Later: needs tree-sitter grammars in the C# port; layer 1 ships first |
| 33 | Stacked PRs | Graphite's strip with `[` and `]` between PRs. | Later: needs stack detection from base and head refs; restack waits for the babysitter |

### 4.3 Comments and submit

| # | GitHub | v3 answer | Ship / later |
|---|---|---|---|
| 34 | Line comment | `c` opens the composer under the line. | Ship R3, R10 |
| 35 | Multi-line comment | Drag, or `Shift+Down` before `c`. Needs `StartLine` on `ReviewComment` (today only `Path` and `Line`). | Ship R1 |
| 36 | File-level and PR-level comment | File header `c`; PR-level uses `Reply` with key `"pull"` (exists). File-level needs a new form. | Ship R1 |
| 37 | Pending review batch | The local drafts file (decision 7), a counter in the header. | Ship R3 |
| 38 | Suggestion block | `s` prefills the selected lines. It is a fenced `suggestion` block in the body, so no forge member. | Ship R10 |
| 39 | Apply a suggestion | Copy the patch (any PR). Commit only on a branch the account owns (decision 8). | Copy R10; commit later: needs a checkout and push rights check |
| 40 | Reply to a thread | Exists as `Reply`; composer in the right pane. | Exists; UI R10 |
| 41 | Resolve, unresolve | `R`. Needs GraphQL `resolveReviewThread`. `ForgeThread.Resolved` is read today and nothing writes it. | Ship R1 |
| 42 | Outdated threads | `ForgeThread.Outdated` exists. The thread shows "outdated" and follows its line to the new head when it can. | Ship R5, R10 |
| 43 | Reactions | `React` exists. A small picker on a comment. | Exists; UI R10 |
| 44 | Edit, delete own comment | Needs `Edit` and `Delete` comment members. | Ship R1 |
| 45 | Submit: comment, approve, request changes | The submit sheet (§5.4). Approve is hidden on the owner's own PR (GitHub refuses it). | Ship R3, R10 |
| 46 | Re-request review | `a` opens the reviewer picker. REST `requested_reviewers`. | Ship R1 |
| 47 | Dismiss a review | Rare, needs write rights, no ledger row. | Later: no daily use found |
| 48 | Review by a bot | Shown with the bot flag (`ForgeThread.IsBot`). A bot never sets the queue. | Exists |

### 4.4 Merge

| # | GitHub | v3 answer | Ship / later |
|---|---|---|---|
| 49 | Merge, squash, rebase | `Merge` and `MergeMethod` exist. The ready line gates the button. | Exists; UI R12 |
| 50 | Required checks state | Exists (`RequiredChecks`). The ready line names the missing one. | Exists |
| 51 | Rerun checks | `RerunChecks` exists. Button on the `ci` panel. | Exists; UI R12 |
| 52 | Merge queue, auto-merge | `enablePullRequestAutoMerge` and the queue API need per-repo settings. Home's "merge when green" covers the common case. | Later: per-repo settings; Home covers green |
| 53 | Admin bypass of branch protection | The owner does not bypass. | Dropped: not a daily action |

### 4.5 Keyboard and speed

| # | GitHub | v3 answer | Ship / later |
|---|---|---|---|
| 54 | Keyboard set, `?` sheet | §6. One palette (`Ctrl+K`) that doubles as the key list. | Ship R10 |
| 55 | Large PRs | Virtualized diff, file panels on demand (D8, D9). | Ship R10, R13 |

Count: 55 rows. 48 ship (5 of them are built already and need no work), 6 are later (rows 3, 11, 32, 33, 47, 52) and 1 is dropped (row 53). Rows 13 and 39 ship in part and name what waits.

## 5. The page

### 5.1 The queue

Rows are the stored PR records the page reads today, with sections added. Layout is Home's (Home §7.1): 32 px rows, a peek on `Space`, the full surface on `Enter`.

```
┌ top bar 40 ───────────────────────────────────────────────────────────────────────────────────────────────┐
│ Reviews   4 need you · 2 ready to merge                       / s: r: a:                reviews as joi      │ page header 48
├ Needs your review 2 ──────────────────────────────────────────────────────────────────────────────────────┤ group header 28
│ ◎ #3120 Harbor ferry: seat holds        asked of you         +310 -42   CI green   ada     1h40           │ row 32
│ ◎ #3127 Harbor ferry: fare table        asked of @harbor-api +88 -9     CI red     lee     3h             │
├ New commits since you reviewed 1 ─────────────────────────────────────────────────────────────────────────┤
│ ◎ #3131 Quay lights: dim at night       3 new commits        +20 -4     CI green   sam     5h             │
├ Returned to you 1 ────────────────────────────────────────────────────────────────────────────────────────┤
│ ◐ #3118 Booking export                  2 unreplied threads  +120 -30   CI green   you     1d             │
├ Waiting on others 2 ▸ ────────────────────────────────────────────────────────────────────────────────────┤
├ Ready to merge 2 ▸ ───────────────────────────────────────────────────────────────────────────────────────┤
├ Drafts 1 ▸ ───────────────────────────────────────────────────────────────────────────────────────────────┤
├ footer 28: j/k move · Space peek · Enter review · / filter · . actions · a reviewer · ? all keys ──────────┤
└───────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Row (32 px compact):** `[glyph] [#number + title, flex] [reason chip 160] [size +/-] [CI] [author] [age]`. The reason chip is the Gerrit-style phrase. The chip comes first among facts, so it survives the peek's "first 3 facts" rule (#2978 grammar). A fact the source does not give is blank, never "-".
- **Peek (`Space`):** the description, the pre-review summary and risk areas when one exists (§5.5), the checks, and the sections of the next action. The peek never navigates. Take 2 and ledger 1.
- **Order inside a section:** priority, then the time the row entered the section, then the PR number. A poll never moves a row (Home §4.3). Priority is Home's `blocks N` from the ranking route when it ships; until then it is the stored order, newest activity first. Signals named in Take 2: how many people or agents wait on the PR, linked issue priority, size, age.
- **Needs your review** holds `ReviewWaitingKind.Requested` rows. **New commits since you reviewed** holds `NewCommits` rows. **Returned to you** holds the owner's PRs with changes requested or unreplied threads from people. **Waiting on others** holds the owner's PRs with a review pending. **Ready to merge** holds the owner's PRs with green required checks and no open thread. **Drafts** holds the owner's draft PRs.
- **Actions from the row** (`.` menu, each with its key): Review (Enter), Run pre-review, Request reviewer (`a`), Merge when ready, Start babysitter, Open on GitHub.

### 5.2 The review surface

The review opens on the **Overview** step. The walk then moves file by file in change order.

```
┌ header: Harbor ferry: seat holds #3120 · ada → main · head a1b2c3d · 14 files +310 -42 · 5/14 viewed ──────┐
│ Pre-review: 6 findings, 4 left   ·   CI green   ·   Required 2 of 2   ·   0 conflicts   [Order|Files] [All|Since your review]
├ tree 260 ─────────────────┬ diff (flex) ───────────────────────────────────┬ threads 360 ─────────────────┤
│ Overview                  │ Overview                                       │ Conversation 3               │
│ ▾ Booking: seat hold      │  Summary (agent): adds a 10-minute hold on     │ ● #1 BookingService.cs:41    │
│   entry                   │  a seat before payment. Touches 3 layers.      │   @lee: why a static clock?  │
│   ✓ HoldEndpoint.cs       │  Risks 2 · Questions 4                         │   [Reply] [Resolve]          │
│   core                    │  Required checks · Test plan · Doc map ...     │ ○ #2 outdated                │
│   ▸ HoldService.cs   +80  │                                                │                              │
│   ▸ HoldPolicy.cs    +22  │ HoldService.cs   core   viewed ☐   owner @ada  │ Findings 4                   │
│   data                    │ 38  public Hold Place(Seat s)                  │ ▲ high  HoldService.cs:41    │
│   ▸ 0042_holds.sql   +14  │ 41+ var until = Clock.Now.AddMinutes(10);      │   Static clock; tests can't  │
│   tests                   │      ▴ finding: static clock   [callers: partial]│   pin it. evidence: 2 hits   │
│   ▸ HoldServiceTests +96  │ 44+ if (until < Clock.Now) throw ...           │   [y accept] [e edit] [d drop]│
│ ▾ Unmapped                │                                                │                              │
│   ▸ docs/holds.md    +9   │                                                │                              │
│ ▸ Folded: 1 lockfile      │                                                │ Draft: 2 comments  Ctrl+Enter│
└───────────────────────────┴────────────────────────────────────────────────┴──────────────────────────────┘
```

- **Header:** title, number, author, head sha, files with +/-, the viewed count, the findings count, CI, required checks, conflicts. Two toggles: Order or Files (`t`), All or Since your review (`Shift+S`).
- **Tree (260 px):** in **Order** mode, files group by concept and sort by band inside each concept. The order of the groups follows the earliest band of their files. Each file shows its status mark (`✓` viewed, `▸` unread, `Δ` changed since viewed) and `+/-`. Files with no concept sit in one "Unmapped" group, never dropped. Folded files (lockfiles, generated) keep the 2026-11-04 §7.1 rules. In **Files** mode the tree is the plain folder tree, alphabetical, GitHub's order.
- **Diff (flex):** unified or split. The file header names its band, its CODEOWNERS owner, a viewed box and a "callers" link. Hunk headers name the changed symbol. A fold row loads more context.
- **Threads (360 px):** the conversation, then the findings (§5.5), then the draft counter. Threads on the focused file come first. At 1,100 to 1,439 px the pane becomes inline threads under their line, as GitHub does, and the tree drops to 240 px. Below 1,100 px the tree is a drop-down (`[`) and threads are inline.
- **Overview step:** the middle pane shows the seven 2026-11-04 panels (required checks, doc map, test plan, CI, evidence, risk, conversation) in the settings' order, under the pre-review summary. This replaces the 420 px panel column (question 1). `n` or `j` leaves Overview for the first file in order.

**Change order, defined once.** The order is the sequence in which the walk shows files. It puts a file after the files it depends on being understood from. Concretely:

1. Each changed file gets a **band** from the code index: `entry` (an endpoint, a command, a UI view), `core` (domain types and services), `data` (migrations, repositories, stored schemas) or `tests`.
2. Inside a band, files sort by **distance from the nearest entry**: the number of call or reference edges between them. Fewer edges first. A tie sorts by path.
3. Files with no band go to "Unmapped" after `tests`. Folded files go last.

Worked example, PR #3120 "Harbor ferry: seat holds", 8 files (paths trimmed). GitHub shows them in path order:

```
GitHub order                       Change order (band, distance)
 1 0042_holds.sql                   1 HoldEndpoint.cs          entry, 0
 2 HoldEndpoint.cs                  2 HoldRequest.cs           core, 1
 3 HoldPolicy.cs                    3 HoldService.cs           core, 1
 4 HoldRequest.cs                   4 HoldPolicy.cs            core, 2
 5 HoldService.cs                   5 0042_holds.sql           data, 3
 6 HoldServiceTests.cs              6 HoldServiceTests.cs      tests
 7 docs/holds.md                    7 docs/holds.md            unmapped
 8 packages.lock.json               8 packages.lock.json       folded
```

`HoldRequest.cs` and `HoldService.cs` both sit one edge from the endpoint, so the path breaks the tie. The owner reads the endpoint, then the request it takes, then the service that implements it, then the policy it calls, then the table, then the tests that pin them. That is the story. The path-order list opens on a SQL file with no context.

**Provenance.** The header says "Order built from the code index at a1b2c3d" and keeps the previous order when the head moves (ledger 3 take, Change Stack's snapshots). Files mode is always available. When `diff-scope` is not ready, or the code index is not built for the repo, the tree shows Files mode with "Order unavailable: <reason>". It never invents an order.

**Callers.** `C` on a hunk lists the callers and callees of the symbols whose bodies changed, ranked by distance. Each list carries one mark: `complete`, `partial (barrels)` or `none (no parser)`. The mark is part of the answer (D5). An empty list with `partial` reads "No callers found, list may be incomplete". `Ctrl+o` goes back.

### 5.3 Since your last review

When the owner has reviewed an earlier commit, the page opens on **Since your review**: the diff from the reviewed sha to the head, grouped and ordered the same way (context plan item 3). `Shift+S` shows the whole PR.

- Each of the owner's threads shows "your 6 comments: 4 changed lines near the thread, 2 untouched" in the threads pane.
- For each thread, the agent proposes **addressed** or **not addressed** as a pending reply, never posted (engine plan item 2). The owner accepts, edits or drops it like a finding.
- Hunks that only moved with a rebase draw in a muted colour. A force-push after a rebase is not a wall of red (ledger 5 take, Differential).
- Threads follow their line to the new head. A thread whose line is gone shows "outdated".

### 5.4 The submit sheet

`Ctrl+Enter` outside the composer opens a side sheet. Inside the composer, `Ctrl+Enter` adds the comment to the draft.

```
┌ Submit review on #3120 · head a1b2c3d ───────────────────────────────┐
│ Posting as @joi                                                      │
│ Body ┌───────────────────────────────────────────────────────────┐   │
│      │ Two small things: the static clock, the missing index.     │   │
│      └───────────────────────────────────────────────────────────┘   │
│ 2 line comments                                                       │
│  HoldService.cs:41   Static clock; tests can't pin it.                │
│  0042_holds.sql:9    No index on seat_id. (suggestion)                │
│ 2 findings not yet accepted or dropped                                │
│ Required checks: 2 of 2 pass                                          │
│ [ Comment ]   [ Approve ]   [ Request changes ]            [ Cancel ] │
└──────────────────────────────────────────────────────────────────────┘
```

- One click on a verdict posts one `SubmitReview` with the draft, `ExpectedHead` and the marker `owner-review:<draft id>:<draft version>` (2026-11-04 §8.3). No typed text, no second confirm.
- The sheet re-reads the head and the checks when it opens. A check that went from pass to fail since the page loaded is listed at the top.
- On the owner's own PR, Approve and Request changes are disabled with GitHub's reason (2026-11-04 §16 pick 4).
- Unresolved findings are shown as a count and never block the submit.
- On `Posted`, the draft is deleted, `review.posted` goes to the ledger (verdict, sha, comment count, link, no text) and focus lands on the outcome line. On `HeadMoved` the draft stays and the banner shows. On `LineRejected` the draft stays and the refused comment is marked (`SND-REVIEW-013`).

### 5.5 The agent pre-review

**What the agent produces**, for one head sha:

- `summary`: up to 8 lines in plain words. It names the layers the PR touches.
- `risks[]`: `{path, line, why, severity, evidence}`.
- `questions[]`: `{path, line, body, severity, evidence}`. The body is the draft comment text.
- `findings[]`: the union, one row each, with a stable id (hash of kind, path, line, why).

`severity` is `high`, `medium` or `low`, kept apart from effort (ledger 8 take). `evidence` is one line: a grep hit, a caller list with its mark, or a failing check name. The agent gets the diff, the `diff-scope` object (§5.2) and read-only access to the checkout. It gets no GitHub token and cannot post.

**How a run starts.** `github.pr.review_requested` starts the `pr-pre-review` workflow template (engine plan on #2980) when `review.preReview` is `auto` and the PR has 60 files or fewer. Run pre-review in the `.` menu starts it on demand, any size. `github.pr.updated` reruns it scoped to the commits after the owner's last review (§5.3). The workflow's `pending_review` block writes through `POST .../suggestions` (§8), a route a session may call. The run, its head and its cost show on the PR's peek.

**How the owner verifies a finding.**

1. The finding row shows severity, anchor `path:line`, one line of why, and one line of evidence.
2. `Enter` on the row scrolls the diff to the anchor in under 100 ms and highlights the line. The evidence opens beside it.
3. Three keys (decision 12): `y` accepts the finding as a draft comment (a risk becomes a comment whose text is its why; a question becomes its body); `e` accepts and opens the composer to edit; `d` drops it.
4. A dropped finding is remembered by id. If a later pre-review on the same PR repeats it, the row says "repeats one you dropped on <date>".
5. `Shift+J` and `Shift+K` step through findings. The header counts the ones left.
6. A finding whose line is not in the head's diff shows "stale" and cannot be accepted (`SND-REVIEW-016`). The owner can drop it.

An accepted finding is an ordinary draft comment. The submit sheet lists it with the rest. The posted text is the owner's text, with no agent marker. Nothing posts until one click on a verdict (D2). The acceptance rate (accepted over shown) is kept locally per template, as the quality number for the agent.

### 5.6 States

| Where | Loading | Empty | Error |
|---|---|---|---|
| Queue | skeleton rows; first paint from the store under 100 ms | "Nothing waiting on you and no open PRs of yours." plus where repos are set | "List is from <time>: <source> failed: <reason>", above the last rows. Retry button (#3065) |
| Overview | placeholders per panel (2026-11-04 §6.3) | "This PR changes no files" | per panel, as before |
| Tree, Order mode | "Ordering 14 files…" after 300 ms | — | "Order unavailable: <reason>", Files mode shown |
| Diff | "Reading 42 files…" | "This PR changes no files" | "Couldn't read the diff: <forge reason>"; offline with a cached head shows "as of <time>" |
| Since your review | "Comparing a1b2c3d with 9f8e7d6…" | "Nothing changed since your review" and the whole-PR key | "Couldn't compare: <reason>", whole PR shown |
| Callers | "Finding callers…" | the list with its mark (D5) | "Callers unavailable: <reason>" |
| Findings | "Pre-review running, started 2m ago" | "No pre-review for this head. Run one." | "Pre-review failed: <reason>" (`SND-REVIEW-017`) |
| Draft | "Saving…" only past 300 ms | "No draft" | "Couldn't save the draft: <reason>"; the text stays on screen |
| Submit sheet | "Posting…" after 300 ms, buttons disabled | — | `SND-REVIEW-010` to `-013`, `-018`, `-019` text in the sheet; the draft stays |
| Head moved | — | — | "The PR moved to <sha7>. Reload to see the new commits. Kept: N comments. Outdated: M. Viewed reset: K files (<names>)." |
| Offline | — | — | Cached head shows "as of <time>". Writes queue as "sending"; none is lost; a failed one shows Retry and Discard |

## 6. Keyboard and screen readers

Grammar keys are Home's: `j/k`, arrows, `Home`/`End`, `/`, `Space`, `Enter`, `Esc`, `.`, `?`, `Ctrl+K`. The queue uses them as they are. A bare letter below opens something cancellable or acts on local state only. None writes to GitHub.

| Key | Where | Does |
|---|---|---|
| `j` / `k` | review | next or previous file in the tree's order |
| `n` / `p` | review | next or previous hunk |
| `N` / `P` | review | next or previous thread |
| `Shift+J` / `Shift+K` | review | next or previous finding |
| `c` | diff line | composer under the line; `Shift+Down` or `Shift+Up` first extends the range |
| `s` | diff line | composer with a suggestion block of the selected lines |
| `r` | thread | reply (Home's reply key) |
| `R` | thread | resolve or unresolve (a write; opens no sheet, but the row shows "sending" and an undo for 5 s) |
| `v` | file | toggle viewed |
| `y` / `e` / `d` | finding | accept, edit, drop |
| `C` | hunk | callers; `Ctrl+o` back |
| `t` | review | Order or Files |
| `Shift+S` | review | Since your review or whole PR |
| `u` / `w` | diff | split or unified; hide whitespace |
| `a` | queue, review | reviewer picker (assign wherever a row has an owner; meta's rule) |
| `[` / `]` | review | collapse the tree or the threads pane |
| `Ctrl+Enter` | composer / elsewhere | add to draft / open the submit sheet |
| `F6` | review | cycle regions: tree, diff, threads |

`R` is the one letter that writes. It is a thread-state change on a comment the owner already read, repeatable, and it shows a 5 s undo. Question 4 asks whether to move it behind `.`.

- **Focus order:** header, tree, diff, threads, draft counter. After a composer closes, focus returns to the line. After the submit sheet closes, focus lands on the outcome line. After a finding is accepted, focus moves to the next finding.
- **Names:** every control has an `AutomationProperties.Name`. A finding reads "High finding, HoldService.cs line 41: static clock, evidence 2 hits, accept, edit or drop". A tree file reads "HoldService.cs, core, 80 lines added, unread". A caller list reads its mark first: "Callers, partial, 3 found".
- **Never color alone:** a viewed file has `✓`, a changed-since-viewed file has `Δ`, severity has a word. Colors are the existing tokens. Added and removed tints are checked at 4.5:1 in both themes by the rendering test of 2026-11-04 §6.4.
- **Announcements** are polite live regions built by the app's recipe (`sindri-ui.md`, the recipe the 2026-11-04 spec cites), and every outcome is also in a focusable element, because Orca may speak none of them.
- **Perf of keys:** a key press paints in one frame (16 ms) on `v`, `j/k`, `n/p`. The composer opens in one frame and writes the draft to disk as the owner types, so `kill -9` loses 0 characters (ledger 7 desktop gain).

## 7. Data and read path

All reads come from the local store, then refresh. Nothing blocks on the network (#2978 budget).

| Data | Source | Exists |
|---|---|---|
| Queue rows, sections | stored PR records; `ReviewListBuilder` | exists; sections and reasons new |
| PR page, files, threads, checks | `GET /sindri/reviews/{owner}/{repo}/{number}`; `IPullRequests.Files`, `Threads`, `Get`; cache by head sha | exists |
| Change order and band | `GET /sindri/context/diff-scope?repo&base&head` and MCP `context_diff_scope` (context lane, new) | new |
| Callers and callees | the same route, per changed symbol, with the `complete | partial | none` mark | new |
| Diff between two shas | `GET /sindri/reviews/{owner}/{repo}/{number}/range?from&to` (new), from `Files` on two refs and the cache | new |
| Reviewed sha, new commits | `ReviewedAt` (`Core/Reviews/ReviewedAt.cs`) | exists |
| Draft | `$AW_SINDRI_HOME/reviews/drafts/<owner>/<repo>/<number>.json` (2026-11-04 §8.1, not built) | new |
| Viewed marks | `$AW_SINDRI_HOME/reviews/viewed/<owner>/<repo>/<number>.json`: `{reviewedSha, files: {path: patchHash}}`, apart from the draft so a discard keeps them | new |
| Pre-review | `$AW_SINDRI_HOME/reviews/prereview/<owner>/<repo>/<number>/<headSha>.json` | new |

The order is built once per head sha and cached next to the PR's cache. Reopening is under 100 ms. The cache cap and pruning of the 2026-11-04 spec cover the new files.

Pre-review text and finding text come from a model and from PR text. Both are untrusted: fenced in API and MCP results, shown as plain text, scrubbed before they are stored.

## 8. Writes, routes and the CLI

Every write goes through the owner's forge client (`review.credential`), checks the head where it matters, and reconciles from GitHub's answer. A session caller gets `SND-AUTH-002` on every row except `suggestions`.

Routes. Each prefix is `/sindri/reviews/{owner}/{repo}/{number}`. "Exists" is checked on main. Home's `POST /sindri/home/rows/{id}/act` (Home §5.2) calls the same services; it adds no GitHub logic of its own.

| Route | Does | Forge call | Idempotency | Who |
|---|---|---|---|---|
| `GET /sindri/reviews` | queue; gains `sections[]` and `reason` | stored records | n/a | exists; fields new |
| `GET {prefix}` | page model; gains `order`, `viewed`, `prereview` | `Files`, `Threads`, `Get` | n/a | exists; fields new |
| `POST {prefix}/fix` | fix task | `SubmitReview` (line) | `sindri.fix:<item>` marker | exists |
| `GET {prefix}/range` | diff between two shas | `Files` ×2 | n/a | new |
| `GET {prefix}/callers?path&line` | callers of the symbol at a line | context | n/a | new |
| `GET/PUT/DELETE {prefix}/draft` | read, replace (with `version`), discard the draft | none | `version` | new, owner |
| `POST {prefix}/post` | submit the draft | `SubmitReview` | `owner-review:<draft id>:<version>` | new, owner |
| `PUT {prefix}/viewed` | `{path, viewed, headSha}` | `markFileAsViewed` (new) | repeatable | new, owner |
| `POST {prefix}/threads/{key}/reply` | reply | `Reply` (exists) | `clientId` marker | new route, owner |
| `POST {prefix}/threads/{key}/resolve` and `/unresolve` | thread state | `resolveReviewThread` (new) | repeatable | new, owner |
| `PUT/DELETE {prefix}/comments/{id}` | edit, delete own comment | new | repeatable | new, owner |
| `POST {prefix}/reactions` | reaction | `React` (exists) | repeatable | new route, owner |
| `POST {prefix}/rerequest` | re-request review | `requested_reviewers` (new) | repeatable | new, owner |
| `POST {prefix}/merge` | merge | `Merge`, `ExpectedHead` (exists) | head sha | new route, owner |
| `POST {prefix}/rerun` | rerun checks | `RerunChecks` (exists) | repeatable | new route, owner |
| `PUT {prefix}/ready` | `{draft: bool}` | `markPullRequestReadyForReview`, `convertPullRequestToDraft` (new) | repeatable | new, owner |
| `POST {prefix}/update-branch` | update branch | REST (new) | repeatable | new, owner |
| `PUT {prefix}/labels` and `/assignees` | set labels, assignees | new | repeatable | new, owner |
| `GET {prefix}/prereview` | latest pre-review for the head | none | n/a | new |
| `POST {prefix}/prereview/run` | start the pre-review | `wf run pr-pre-review` | one run per head | new, owner |
| `POST {prefix}/suggestions` | write findings into the pre-review file | none | finding id | new, **session allowed** |

Forge members that do not exist today, each with the file adapter and `FakeForge`, a contract test and red first (api's plan on #2980): resolve and unresolve a thread; re-request review; `StartLine` and a file-level form on `ReviewComment`; edit and delete a comment; mark a file viewed; update branch; draft toggle; labels, assignees and milestone; close and reopen; list commits. Member names are api's call in R1 and R2.

**Stale head.** For a PR row, the version is the head sha plus `updated_at`. `post`, `merge`, `viewed` and a suggestion copy carry the head the owner read. Thread replies and resolves use `updated_at` only (api's plan on #2980).

**The CLI and MCP** (D12). These verbs ship in R3 with the names the 2026-11-04 spec gave and #3165 reports missing. All take `--json`; exit 0 ok, 1 attention, 2 error; reads never start serve.

- `sindri review list [--section S] [--project P]`, `show <pr-link>`, `order <pr-link>`, `findings <pr-link>`.
- `sindri review suggest <pr-link> --file F --line N --text T [--severity S]`: a session may run it.
- `sindri review fix <pr-link> --text T [--file F --line N]`, `post <pr-link> --verdict comment|approve|request-changes`, `merge <pr-link>`: owner only. `post` prints the sheet's text and asks for `y` on a terminal, as the 2026-11-04 spec says; the app needs no typing.
- MCP: `sindri_review` reads and `suggest`. The owner-only verbs have no MCP tool.

## 9. Errors

Existing: `SND-REVIEW-001` to `-009`, `-404`, `-503` (`Core/SindriErrors.Review.cs`). `-010` to `-014` are reserved by the 2026-11-04 spec for posting and are not on main. New codes continue the same series.

| Code | Meaning | HTTP | CLI exit |
|---|---|---|---|
| `SND-REVIEW-010` | the draft changed in another window since the sheet opened (2026-11-04 §9) | 409 | 1 |
| `SND-REVIEW-011` | the head moved; carries the new sha (2026-11-04 §9) | 409 | 1 |
| `SND-REVIEW-012` | no `review.credential` (2026-11-04 §9) | 409 | 1 |
| `SND-REVIEW-013` | GitHub refused a line comment (2026-11-04 §9) | 422 | 1 |
| `SND-REVIEW-014` | the submit token was used twice or expired (2026-11-04 §9) | 409 | 1 |
| `SND-REVIEW-015` | apply refused: the branch is not one the account owns; the patch is offered to copy | 409 | 1 |
| `SND-REVIEW-016` | a finding's line is not in the head's diff (stale) | 409 | 1 |
| `SND-REVIEW-017` | the pre-review run failed or was refused; carries the run's reason | 502 | 1 |
| `SND-REVIEW-018` | merge refused: GitHub names a required check, a thread, a conflict or a protection rule; the text is GitHub's reason | 409 | 1 |
| `SND-REVIEW-019` | GitHub refused another write (resolve, viewed, labels, ready, update branch); carries its reason; the local state is kept | 502 | 1 |

Each has a sentence for the app and a `fix:` line for the CLI. `degraded[]` entries on the page model are data: `order-unavailable`, `callers-partial`, `prereview-stale`.

## 10. Security

- **Posting is human-only** (D3). Every write route checks the caller class. A session gets `SND-AUTH-002`, except `suggestions`.
- **The token stays out of sessions.** The owner's token variable is emptied in every launched Step (2026-11-04 D7). The pre-review run has no GitHub access of its own.
- **Findings are untrusted.** PR text can steer a model. A finding is plain text, never markup, and it becomes a GitHub comment only by the owner's accept and the owner's click. The submit sheet shows the final text.
- **The sandbox is read-only** for the pre-review: read of the checkout, write only through `suggestions`.
- **Viewed marks write only what the owner pressed.** No bulk "mark all viewed" ships.
- **No PR code runs** (2026-11-04 D11). Callers come from the index, not from building the PR.
- **Fixtures are made up.**

## 11. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Lane | Starts | Needs | Proves |
|---|---|---|---|---|---|
| R0 | `docs/parity/github-review.md` from §4 (five columns per `docs/parity/linear.md`); the script for D11 | ideas | now | none | D11 |
| R1 | Forge writes A: resolve, unresolve, re-request, `StartLine` and file-level comments, edit and delete comment. Contract tests, file adapter, `FakeForge`; red first | api | now | none | feeds D2, D3 |
| R2 | Forge writes B: viewed mark, draft toggle, update branch, labels, assignees, milestone, close, reopen, `Commits` | api | after R1's red commit | R1 | D7 (write half) |
| R3 | Drafts file and `draft` routes; the owner's second forge client; `post`, `suggestions`, `viewed`, `threads/*`, `merge`, `rerun`, `ready` routes; `SND-REVIEW-010` to `-014`, `-018`, `-019`; the `sindri review` verbs and `sindri_review` (#3165); the `ops` adapters Home's `act` calls | api | after R1 | R1, the outbox PR of #2978 | D2, D3, D6, D12 |
| R4 | `diff-scope` route and MCP tool; callers with the three marks; 10 merged-PR fixtures; p95 under 2 s for 50 files | context | fixtures now; routes after #2974 and #2983 | #2974, #2983 | D4, D5 |
| R5 | Range diff route; since-last-review grouped like R4; per-thread "addressed?" proposal; viewed carry-over by patch hash; outdated threads follow their line | api | after R2, R4 | R2, R4 | D7 (carry) |
| R6 | Engine: poll kinds `github.pr.opened`, `review_requested`, `updated`, `check.completed` with payloads and dedup (#2988, #2987); `pr-pre-review` template; the `pending_review` block writing to `suggestions`; `pr-babysitter` start from a row | engine, api | the kinds now; the template after R3's `suggestions` | #2988, R3 | D10 (data half) |
| R7 | Queue sections, reason strings, team reviews (team in the PR record, `read:org`), rank from the #2978 ranking route; saved sections | api | after the ranking route; reasons now | #2978 ranking | feeds D1 |
| R8 | Frames: queue with peek; three-pane surface at 1,440, 1,200 and 1,000 px; Overview; findings; submit sheet; composer; every §5.6 state | design | now | Home's tokens | the numbers in §5 |
| R9 | Queue on `RowList`, `Row` and `PeekPane`; sections; reasons; filter tokens; `.` menu. The open bugs #2791, #3100, #3092 land first | ui | now on the stub `RowList` | design P0 controls (#2978) | D8 (queue) |
| R10 | Review surface: tree (Order and Files), diff (split, whitespace, word-level, context fold), threads, composer, suggestions, viewed, callers, Since your review, submit sheet, keys | ui | after R8 and R3's fake routes | R3, R4, R5 | D5, D7, D9 |
| R11 | Findings panel and Overview summary; accept, edit, drop; the "repeats one you dropped" mark; the peek summary | ui | after R6's data and R10's threads pane | R6, R10 | D10 |
| R12 | CI log excerpt under the failing hunk (parse `path:line` from `CheckLog` text against the diff's files; measured on fixtures); ready line; merge, rerun, babysitter buttons | ui, api | after R10 | R10, R3 | D6 |
| R13 | Perf: D8 and D9 in the real app; baseline now, recheck after R10 | perf | baseline now | none | D8, D9 |
| R14 | ux: time the 10 PRs on github.com; run the day in Sindri; every github.com open is an issue, `Part of #2980` | ux | the baseline now; the run after R12 | R12 | D1 |

**Lanes busy from day one:** ideas (R0), api (R1), context (R4 fixtures), engine (R6 event kinds), design (R8), ui (R9 and the open bug fixes), perf (R13 baseline), ux (the github.com baseline of R14). Nothing waits on a source that does not exist: Order shows "unavailable" until R4, findings show "No pre-review" until R6, and the submit sheet's verdict buttons stay disabled until R3.

**Gates per phase.** Every phase: the `-warnaserror` build, the touched projects' lanes and scoped format. R1, R2: `Aw.Forge.Tests` contract lanes and `Aw.Forge.Testing.Tests`. R3: the ApiGoldens classes, `EndpointCqrsTests`, `check-env-example.sh`, `Aw.Architecture.Tests`. R4: the fixture timings. R6: the engine unit and integration lanes and the aot-smoke when a doctor row changes. R9 to R12: `Aw.Sindri.Ui.Tests`, `Aw.Sindri.Ui.Frames.Tests` integration, `Aw.Sindri.Ui.Rendering.Tests`. Each UI PR has aw-ui-driver before and after shots and an adversarial design review before OK (owner rule, 2026-11-10).

**Docs to update per phase:** `docs/csharp/forge.md` (new members, the heavy contract's review on a scratch PR), `docs/csharp/sindri.md` (CLI table, errors), `docs/csharp/sindri-ui.md` (the three-pane page, keys), `docs/csharp/sindri-profile.md` (`review.preReview`), `docs/sindri/context.md` (`diff-scope`), `docs/parity/github-review.md`, the Commands block of AGENTS.md (`sindri review …`), and `src/Sindri/aot-smoke/expected.txt` if a doctor row changes.

## 12. Questions for the owner

Each has my recommended pick. None blocks the phases that start now.

1. **Replace the panel column with the three-pane walk?** The owner picked Layout A (panels beside the diff) on 2026-11-06. The direction wants tree, diff, threads. Pick: yes. Panels move to the Overview step.
2. **When does the pre-review run?** Pick: automatically when you are asked to review and the PR has 60 files or fewer; on demand above that. It costs model usage per PR, so `review.preReview: manual` is one profile key away.
3. **Which keys move between files and hunks?** The direction text says `j/k` hunks and `n/p` files. The shipped page does the reverse. Pick: keep the shipped keys.
4. **Is `R` (resolve) allowed as a bare letter?** It writes to GitHub, though it is repeatable and has a 5 s undo. Pick: yes, since a thread state is not a verdict. The alternative is `.` then `r`.
5. **Do you want a draft visible on github.com?** The pending batch is a local file, so another device or github.com does not show it (meta's decision). Pick: no for v3; a "Save as GitHub pending review" button can come later.
6. **How do we time "faster than github.com"?** Nobody has a baseline. Pick: ux times the same 10 PRs on github.com first, with the same script, then in Sindri.
7. **Show reviews asked of your team?** This needs the `read:org` scope on the GitHub connection. Pick: yes, as its own reason "asked of @team".

### Review notes

Not run yet. Round 1 of /autoplan starts after this draft is read by meta. Facts to recheck before round 1:
- `PullSnapshot.Mergeable` is nullable (`bool?`). The header text for "unknown" is not yet written.
- The ledger says Sindri stores the failing file and line from CI logs. A search of `src/Sindri` found no such field, so R12 parses `CheckLog` text and measures it. It stays unverified until the fixtures run.
- `EventKind.cs` names `github.pr.opened` and `github.pr.review_requested`, yet #2988 says the poll produces only `github.pr.merged`. R6 settles it.
- The row-op route is `POST /sindri/rows/{id}/ops` in api's plan and `POST /sindri/home/rows/{id}/act` in the Home spec. This spec uses its own per-PR routes and lets either call them.
