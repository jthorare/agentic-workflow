# Sindri's Home page: one inbox, ranked by what it unblocks

Status: draft (ideas lane, 2026-11-12). Serves #2978 (one app that replaces the others; Home is the one inbox) and builds the "Home" rows of the prior-art ledger (`docs/sindri/prior-art/home.md`, merged in #3011).
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

## 0. Why

The owner's words on #2978: "think about how much or little information density with good design we have in sindri and how we can make that easier or offload it to the home app." Home is where that density goes.

What Home does on main today (read from `origin/main` a0658aacd4, issues linked; the app was not driven for this spec):

- It is panel cards. `Views/Shell/HomePage.axaml` stacks a setup line, a Todo panel, a Needs you list (left column, `3*`), a Running / With you / Queued column (right, `2*`) and a health strip. `ViewModels/HomeFold.cs` caps the Needs you list at 5 rows and gives the strip a share of 3, so 8 rows sit above the fold. Design's frame on #2978 shows 22 rows.
- Needs you holds only what Sindri itself raised: decisions, parked runs, service failures, profile drift, setup steps, connection errors, workflow notes and questions (`Core/NeedsYou/NeedsYouKind.cs`). It holds no review request, no assigned issue, no mention, no PR ready to merge and no lane.
- Its order is urgency, then age, then id (`Core/NeedsYou/NeedsYouOrder.cs`). Nothing counts what a row blocks.
- The Agents page said "0 asking you" while two lanes sat at their recycle mark and two PRs were ready in the merge queue (walk on #3018, gap #3082). Only `aw-meta status` said so. Home would have said the same, because no Home source reads lanes.
- Rows lead with raw ids (#2782), the PR count shows twice (#2827), and a failed read leaves the loading skeleton on screen (#2921).

The job, from #2978: **one list of the things that need the owner, from every source, ranked by what each one unblocks, each actionable in place. When the list is empty, the owner is done.**

## 1. The bar

"Ledger N" is problem N in `docs/sindri/prior-art/home.md`. "Take N" is item N of its closing list "What we take". The ledger's "Not solid" section is not used: no Superhuman key or behaviour claim appears below, and the Tab key for splits (a Superhuman snippet) is replaced in §7.4.

| Step of the daily loop | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| See what needs me from many sources in one list | 1 | Universal Inbox, Gitify, Huly | splits as tabs on one list, a source glyph on every row; linked CI and thread facts on the row | §3, §7.1 |
| Rank by what unblocks the most | 2 | GitHub `reason`, Linear Priority tab | the reason chip and a readable rule; the score itself is ours | §4 |
| Act without leaving | 3 | Raycast Action Panel | one primary action per row kind, `.` panel with keys shown, optimistic write with Undo | §5 |
| Know when I am done | 4 | GitHub Done, Octobox revive | one Done verb; a done row returns only on activity addressed to me | §5.4 |
| Snooze | 5 | Linear `H` | typed date, presets, early return on new activity | §5.3 |
| Peek | 6 | Linear Space peek | Space pane, richer contents (linked PR, CI, thread) | §7.2 |
| One key map | 7 | Linear, Raycast | the grammar keys on #2978, keys shown in the panel | §7.4 |
| Density | 8 | Gmail steps, Linear rows | two steps, compact default | §7.1 |
| Since you last looked | 9 | Slack recaps | a 5 to 10 line brief, read lines drop | §6.1 |
| No spam | 10 | GitHub unsubscribe re-entry | Priority-only toast, one per row, mute with a re-entry rule | §6.3 |
| Palette for every action | 11 | Raycast | every Home action has a palette entry | §7.4 |
| Agents as ordinary list entries | 12 | Zed threads | sessions and lanes are rows | §3.2 |
| Small view later | 13 | GitHub mobile | skipped; the row stays narrow-safe | §11 |

Nobody has rank by blocked count, rows that carry a lane's context size, or one snooze and mute across Slack, Linear, PRs and agent runs. Those three are ours and have no prior art to lean on.

**The bar in one line:** Home's first row names the thing that unblocks the most, in 1 step from opening the app; a row moves only when the owner acts; every action lands in its source within 2 s; and an empty Home means nothing is waiting.

Done-checks. Each is a test or a measured run in the real app (aw-ui-driver), and each names its owner lane in §15.

| # | Check | Measured as |
|---|---|---|
| D1 | **The ux walk.** On the #3082 box state (11 lanes; meta at 205k and ideas at 210k, both at the recycle mark; 2 PRs ready in the merge queue), row 1 of Home names the session that needs the owner. | 1 step: open Home. Today it takes 5 (Agents tab, read 11 rows, open qa, Details, give up for the terminal, comment on #3018). |
| D2 | First paint under 100 ms from the local store. | Window open to first row painted, 5,000 rows in the store, serve not yet answering, p95 over 20 runs. |
| D3 | Rows per screen at compact. | 1440x900, peek closed, brief at its default 3 lines, the setup line hidden, the collapsed Todo line shown (§12), the New band empty: at least 17 Needs you rows visible. At comfortable: at least 14. With peek open: the same rows, 3 facts each. |
| D4 | Key to visible change under 50 ms; source write lands within 2 s. | Keydown to repaint of the optimistic row. Act request received to the source confirming, p95 over 50 writes per source on the fake adapters. |
| D5 | A failed write is visible and retryable. | For each action kind: the fake source refuses, the row shows the reason and Retry within 1 frame, and Retry lands the write once. |
| D6 | Stable order. | A property test over 10,000 random event streams: between two settle points (§4.3) no row changes index except by removal, and every new row appears only in the New band. |
| D7 | Ranking is deterministic and explained. | The §4.2 worked example is a golden test. `sindri home rank --explain <row>` prints blocks, weight, age and why the row sits above the next one. |
| D8 | Re-rank cost. | 10,000 rows scored in under 16 ms (ledger 2). |
| D9 | Snooze. | Wake within 1 s of the due time with the app in the tray; early return within 5 s of a reply addressed to the owner; Undo within 5 s. |
| D10 | Notifications. | Over 50 injected events: exactly one toast per notifying row per episode, zero for non-notifying kinds, tray count equals notifying rows not yet seen. |
| D11 | Done means done. | A seeded store with no Needs you rows and every source fresh shows "You're done." and the brief; with one source stale it shows "Nothing waiting in what I could read" and the stale line (§5.5); one new addressed event brings exactly one row back. |
| D12 | Brief. | At most 10 lines; every link resolves to a row or object that exists; plain-fact lines paint under 100 ms; with the writer off or model-limited the section says why (§6.1). |
| D13 | Agent parity. | A test enumerates the `home.*` action ids; each has a CLI verb and an MCP tool, and each tool's result fences repo text. |
| D14 | **Day without the other apps.** ux runs a scripted day with GitHub notifications, Linear's inbox and the lane terminal closed. Each time the owner opens one to find out what is waiting is an issue, `Part of #2978`. Slack joins when Messages (#2979) ships. | Count of such opens: 0 for the shipped sources. |

## 2. Decisions

1. **Home is one list with three groups and splits as tabs.** Groups: Needs you, Moving, Since you last looked (#2978). Splits are saved filters on the same list: All, Reviews, Messages, Work, Agents (ledger 1). A source page is for working; Home is for deciding what to work on.
2. **Rank by "blocks N".** N counts the open objects that cannot move until this row is done (§4.1). Ties go to a reason weight, then age, then id. The rule is readable and the weights are profile keys (ledger 2, take 6). The score is ours; the ledger found no prior art for it.
3. **Order holds still.** Rows move only at a settle point (§4.3). New rows wait in a marked "New" band at the top. This is the Agents walk's expected behaviour 3 (#3082), applied to all of Home.
4. **Sessions and lanes are rows.** A lane at its recycle mark, a session asking a question and a PR ready in the merge queue are Needs you rows with the same grammar as a review request (ledger 12; #3082).
5. **Acting follows the grammar's write rule.** Meta's rule on #2978: a bare letter never commits a write, and may differ per surface only when it opens something cancellable. So reply (`r`), snooze (`h`) and hand to agent (`t`) open a composer or a picker; send, approve, merge and assign go through the `Ctrl+Enter` menu or the `.` panel. This replaces ledger 3's bare `a`. The owner's boundary (§7.4): a bare key never writes to a source and never starts work; it may change Sindri's own view state when Undo shows for 5 s (`e` Done, `h` snooze).
6. **One Done, one snooze, one mute, for every row kind** (ledger 4, 5, 10). They are Sindri-local state; they never write to Slack, Linear or GitHub. Source writes (reply, approve, merge, state change) are separate actions with write-through (§5.2).
7. **Priority-only notifications.** A toast and the tray count come from a short kind list, once per row (§6.3). Moving and the brief never notify.
8. **The brief is facts first.** Plain-fact lines come from the ledger with no model. A model writes the summary line and the order, and a failed or limited model costs only that (§6.1).
9. **Density is 32 px (compact, default) and 40 px (comfortable).** These are design's grammar numbers on #2978, drawn and measured at 1440x900 (22 rows). The ledger's 28 / 36 came before that drawing and are replaced. `Tokens.axaml:229-233` carries 28 / 32 / 36 / 40 today; design's token PR (P0) sets the names.
10. **Splits use `[` and `]`, and Tab stays focus traversal.** A list that traps Tab strands keyboard users. The ledger's Tab binding is a Superhuman snippet and is in "Not solid".
11. **Done keeps 7 days.** #3031 sets 7. The ledger's 90 days copies GitHub's retention, and a 7-day list is enough to undo a mistake without growing a second inbox.
12. **Home reads one local store.** The first page of `GET /sindri/home` and a cached copy of it paint first; nothing on the first-paint path calls a source.
13. **Agents get the same verbs.** Every action on the page has a CLI verb and an MCP tool; a session reads Home but never presses owner buttons (§9).
14. **The old page stays until U4.** The profile key `home.page` (`rows` | `panels`) picks the page. U1 to U3 ship with `panels` as the default and `rows` for the owner to try. Trying it is one click: U1 adds a Settings row "Home: rows (new) / panels" that sets the same key; `sindri profile set home.page rows` does the same from a terminal. U3 flips the default to `rows`. U4 deletes the panel views after one week with no miss of D1, D4 or D11; any miss flips the default back. After U4 the profile still accepts `home.page` and ignores it for one release, so a profile that sets it does not fail validation.

## 3. The model

### 3.1 The row

A Home row is one object that needs, or may soon need, the owner. Core type `HomeRow` in `Aw.Sindri.Core.Home` (the existing `ISindriHome` in `Core/Control` is the state directory and is not related). Fields:

| Field | What it holds |
|---|---|
| `id` | `<source>:<kind>:<source id>`. Examples: `gh:pr:jthorare/agentic-workflow#3120`, `session:claude-code/ab12cd34`, `lane:meta/ideas`, `linear:issue:ENG-412`. A needs-you row keeps its `NeedsYouRowId` as the source id. |
| `version` | starts at 1, +1 per change; every write names the version it read (§10, code 003) |
| `kind`, `group` | the kind (§3.2); `needs-you` or `moving` |
| `title` | the owner's words for it ("ideas: at 210k, recycle from a digest"), never a raw id (#2782) |
| `facts[3]` | typed, most important first: the **why** chip, the **object** ref (mono), the **from** person or session |
| `waitsOn` | who has the next move: `you`, a person, `reviewer`, `CI`, `agent` |
| `since` | when it started to need the owner (the source's time, not Sindri's poll time) |
| `rank` | `{blocks, weight, sinceRank, position}` as computed by `HomeRank` (§4) |
| `blocksList[]` | the objects behind `blocks`, each `{id, title, via}`, capped at 20 |
| `links[]` | cross-links to the other objects: PR, issue, thread, run, session, scope (#2978 item 3) |
| `state` | `open`, `snoozed` (with `until`), `done` (with `at`), `muted` |
| `actions[]` | action ids that apply (§5.1); the first is the primary |
| `source` | `{name, readAt, connection}`; `readAt` feeds the stale-source line (§7.5) |

The row text comes from the sources and is untrusted: scrubbed by `Aw.Redaction` before it enters the ledger and fenced in agent results.

Home rows are a view. Ledger events (`home.row.raised`, `.changed` (on a change of blocks, why chip or state only; age is worked out at read), `.cleared`, `.seen`, `.snoozed`, `.woke`, `.done`, `.revived`, `.muted`; `home.act.sent`, `.landed`, `.failed`; `home.brief.written`; `home.cursor.moved`) fill a `home_rows` table in `views.db`. `sindri views rebuild` replays them. A collector per source raises and clears rows; none writes a row directly.

### 3.2 Needs you: the sources and their exact triggers

A row is in Needs you while its trigger holds, and leaves when the trigger stops holding or the owner does Done or Snooze. "The owner" is the profile's `review.login` for GitHub and the connected Linear user for Linear.

| Kind | Source | Trigger | Exists on main? |
|---|---|---|---|
| `decision` | Sindri | a pending decision (`NeedsYouKind.Decision`) | yes |
| `question` | Sindri | a graph `ask_owner` node asked and is unanswered (`NeedsYouKind.Question`) | yes |
| `run-failed` | Sindri | a workflow run the owner started, or one whose workflow notifies the owner, ended `failed` and has not been seen | partly: service failures are rows; run failures are not |
| `system` | Sindri | setup step open, profile drifted, connection token stale, graph note posted, a run parked or a recipe needing the owner (`Setup`, `Profile`, `Connection`, `WorkflowNotify`, `Failure`, `Park`, `Recipe`) | yes |
| `agent-asking` | session collector | `GET /sindri/sessions` row with `state: asking` (`SessionAskingView` gives reason, tool, since) | yes |
| `agent-limited` | aw-meta, Sindri | a subject in `aw-meta usage --json` with `state: Limited` (its `Stop` has the kind and `ResetsAt`), or a Sindri agent run paused on a model limit | no |
| `lane-recycle` | aw-meta | `aw-meta lanes-context` gives a lane the verdict `recycle`: context at or over `context.recycleAt` (the meta lane included) | no |
| `pr-ready-merge` | forge, aw-meta | an open, non-draft PR of the owner with required checks passing and nothing blocking its merge, **or** a PR in `aw-meta status --json` `mergeQueue` (a lane sent `ready #N <sha>`; the entry leaves when the PR merges, closes or is dropped). One row per PR even when both hold. | queue: no; Reviews list has "Yours": partly |
| `review-requested` | forge | `ReviewWaitingKind.Requested`: a review was requested from the owner and none is submitted | yes (Reviews list "Waiting on you") |
| `review-new-commits` | forge | `ReviewWaitingKind.NewCommits`: the owner reviewed and the PR has newer commits | yes |
| `mention` | Linear, forge, Messages | a comment names the owner and the owner wrote nothing after it in that thread | Linear: `LinearSinceItem` kind `mentioned`; GitHub and Slack: no |
| `dm` | Messages (#2979) | the last message in a direct message thread is not the owner's and is unanswered | no: waits on Messages |
| `thread-reply` | Messages (#2979) | a new message in a DM, or a reply in a thread the owner wrote in, after the owner's last message, that asks the owner nothing and names no one (Messages class 1: Pat's "thanks!" in a DM, Kim's "ok, 90 s holds then" in the owner's thread) | no: waits on Messages |
| `keyword` | Messages (#2979) | a message matches one of the owner's `messages.notify.rules` keywords and the owner wrote nothing after it in that thread (Messages class 1) | no: waits on Messages |
| `issue-assigned` | Linear | the owner became assignee at or after 00:00 local today and the issue is still unstarted (Linear state type backlog, unstarted or triage) | yes: `LinearSinceItem` kind `assigned`; the "today" cut is new |
| `todo-due` | Todo | a todo due today or overdue and open | yes: Todo store |

Reason words on the why chip: "asking", "limited", "at 210k", "ready", "review requested", "new commits", "mention", "DM", "reply", "keyword", "failed", "assigned", "due", "setup".

`thread-reply` and `keyword` are the two non-notifying Messages kinds: they show in Needs you and in the Messages split, but never toast and never count in the tray (§6.3). Until they exist, the Messages collector raises no row for a class 1 thread, so Slack's "thanks!" stays on the Messages page. *(agent default (Messages spec, #3188))*

Not on Home: "idle with work owed" and "slot wait over 30 min" from #3082's Agents ranking. Home shows only facts that have a source and never guesses what is owed, and both of these are guesses. They stay on the Agents page.

Not on Home: the Queue's `queued` group. Its `running` and `withYou` groups go to Moving (§6.2).

### 3.3 Where the data comes from

| Source | Read through | New work |
|---|---|---|
| decisions, questions, system | `GET /sindri/needs-you` (projection `NeedsYouProjection`) | none; the projection becomes one row producer |
| sessions | `GET /sindri/sessions` (`ListSessionsEndpoint`) | none for asking; context size is not on `SessionRowView` today |
| lanes and merge queue | `aw-meta lanes-context` and `aw-meta status`, read-only, on a service tick | a `home-lanes` service in the Sindri host, with the fields below |
| reviews, PR checks | `GET /sindri/reviews` (`ListReviewsResponse`, `ReviewWaitingKind`), `Core/Forge` (`PullChecks`, `PullMerge`) | the ready-to-merge trigger |
| Linear | `GET /sindri/linear/since` (`LinearSinceResponse`) | the "today" cut; mention-awaiting-reply |
| runs | `IWorkflowRunStore` in process (the route `GET /sindri/workflows/{slug}/runs` lists one workflow's runs of every status, newest first, 50 per page, `More` for older, so it can't answer "failed anywhere, unseen") | the failed-and-unseen trigger needs a new `IWorkflowRunStore` query across workflows by status and a "seen" flag on a run (E1 adds both; today the store has `Recent(workflow)`, `Running()` and `InFlight(workflow)` only); no new public route |
| todos | `GET /sindri/todos` | none |
| Slack, DMs | Messages store (#2979) | waits on that epic; the row kinds are defined here so it only has to raise them |

Fields the lanes collector reads, checked by running the commands on 2026-11-12 (box with 11 lanes):

- `aw-meta status --json`: `mergeQueue[]` is `{pr, from, gate, github}` (`from` is the lane's tmux session name, for example `sindri-v3-ui`; `github` is null unless the PR sits in GitHub's merge queue, then `queued <position>`). `context[]` is `{lane, session, transcript, tokens, lastActivity, threshold, verdict}`; `verdict` is PascalCase (`"verdict":"Ok"`), so the recycle verdict is `Recycle`. `lanes.entries[]` is `{lane, sessionName, session, source}`. The status has no usage key.
- `aw-meta lanes-context --json`: `{recycleAt, lanes[], attention}`, the same lane objects. On the owner's box `"recycleAt":200000` comes from the team config; the code default is 300000.
- `aw-meta usage --json`: `subjects[]` is `{subject, state, agent, signalNote}`; `state` is `Ok`, `Limited` or `Unknown`. A limited subject also has a stop with a kind (`session`, `weekly`, `other`) and a reset time; its exact JSON key was not on screen because no lane was limited at the check.
- `aw-meta merge-check <pr>` prints `PASS` / `FAIL` lines per check; the row's Merge runs `merge-run`, which runs the same checks first.

## 4. Ranking

### 4.1 Blocks N, exactly

A row is about one **object** O (a session, PR, issue, run, thread, lane). N is the count of **dependents** of O:

1. A dependent is an open object that cannot move until the owner acts on O. Merged, closed, cancelled and done objects do not count. A snoozed object does count.
2. The edges, the closed list that make one object wait on another:
   - `item-of`: the objects a lane owns wait on its session. A session S has as dependents the PRs in `aw-meta status` `mergeQueue` whose `from` is its lane (the source: the entry's `from` field). The meta lane also has every PR in the merge queue, because it merges them. Other lane work (open issues and authored PRs) has no named source yet: E1 finds one on a real box (§3.3). Until then those lanes get blocks 0 from `item-of`, and the §4.2 numbers for ideas assume that source exists.
   - `closes`: an issue waits on a PR whose body says `Fixes` or `Closes` for it. "Part of" is not an edge. Only PRs by the owner, the owner's lanes or the profile's trusted authors count (a stranger's PR body can't lift its own row). Same for `stacked`. `blocksList` names the edge's source.
   - `stacked`: a PR whose base branch is another PR's head waits on that PR.
   - `merge-order`: a queue entry waits on the entry the team's merge order puts before it.
   - `child-run`: a workflow run waits on a failed or parked child run, and the item the run serves waits on the run.
   - `blocks`: an issue waits on an issue that Linear marks `blocks` it.
   - `thread-about`: an object marked `waitsOn: you` waits on a thread linked to it that asks the owner something. (Waits on Messages; counts 0 until #2979.)
3. The walk starts at O and follows edges toward dependents, up to 3 hops. An object reached by two paths counts once. O does not count itself.
4. N is capped at 99 on screen. `blocksList[]` keeps up to 20 for the peek.

The edges come from the cross-links in the object model (#2978 item 3). The PR-to-issue links already exist as the Projects "relates" links; the lane work sets come from the lanes collector (§3.3).

### 4.2 Order, tie-break and a worked example

Sort key, in this order: `blocks` high first, then reason weight high first, then `since` old first, then `id` (ordinal, as `NeedsYouOrder` does today).

Reason weights, the default of the profile keys `home.rank.weights.<reason>` (integer 0 to 9): asking, question, decision 6; review requested, new commits, ready to merge 5; mention, DM, failed, limited, at the recycle mark 4; assigned, due 3; system 2. The owner reads them on the row's why chip ("blocks 3") and in `sindri home rank --explain`, and edits them with `sindri profile set`.

Worked example. All numbers are made up for this spec. It is the order at 09:10.

Objects and edges:
- meta (205k) and ideas (210k) are past the recycle mark (200000 on the owner's box, from the team config; the code default is 300000). The merge queue holds #3099 (since 08:02) and #3101 (since 08:40). #3101 waits on #3099 (`merge-order`) and closes issue #3040.
- ideas authored PR #3078 and holds issue #3014 (this assumes the lane work source E1 must find).
- A review of PR #3120 is requested of the owner (since 07:30). #3120 closes issue #3055. PR #3125 is stacked on it.
- Sam's DM waits (since 09:02) and is linked to no object.

| Row | Dependents counted | Blocks | Weight | Since |
|---|---|---|---|---|
| meta at 205k | #3099, #3101 (`item-of`), then #3040 (`closes`, hop 2) | 3 | 4 | 08:55 |
| review #3120 | #3055 (`closes`), #3125 (`stacked`) | 2 | 5 | 07:30 |
| ideas at 210k | #3078, #3014 (`item-of`) | 2 | 4 | 08:50 |
| ready #3099 | #3101 (`merge-order`), then #3040 (`closes`, hop 2) | 2 | 5 | 08:02 |
| ready #3101 | #3040 (`closes`) | 1 | 5 | 08:40 |
| DM from Sam | none | 0 | 4 | 09:02 |

Order: meta (3 blocks). Then three rows with 2 blocks: review #3120 (weight 5, 07:30), ready #3099 (weight 5, 08:02) and ideas (weight 4). Then ready #3101 (1), then the DM (0). Row 1 names a session, which is D1.

At 09:12 the qa session asks to run `rm -rf build/` (blocks 1 through its issue #3102, weight 6). It lands in the New band, not in the list (§4.3). The owner copies the digest request on row 1 at 09:14 (the lane row's primary action, "Copy digest request"). That act settles row 1 only (§4.3): meta's row stays until meta recycles, and qa's row stays in the New band. At 09:16 the owner presses `R`. qa's row takes its place at blocks 1, weight 6, which is below ideas (2 blocks) and above #3101 (weight 5), and the list reads meta, review #3120, #3099, ideas, qa, #3101, DM. Between 09:10 and 09:16 no row changed index.

### 4.3 Stable order and the New band

The server always returns the current ranked order. The app holds its own order and changes it only at a **settle point**:

1. The owner presses `R` (re-rank now).
2. Home opens, or the split tab changes.

An act on a row (Done, Snooze, an action, Hand to agent, Mute) settles that row only: it is removed or updated in place, and no other row moves and the New band keeps its rows. A run of `e` presses therefore never re-sorts the list between presses.

Between settle points:
- A row that leaves (the source cleared it, the owner finished it elsewhere) is removed in place. Rows below move up one; the cursor stays on its row, or on the next row when its own row left.
- A row whose blocks, why chip or age changes updates that text in place and keeps its position.
- A new row appears in the **New band**: a 28 px group header "New (2)" at the top of Needs you with the new rows beneath it in their own rank order. New rows are marked with the word "new" in the age column, in ink, never by colour alone. The band is above the cursor, so the app scrolls to keep the cursor row at the same screen y, and the "New (n)" header sticks to the top of the list while the band has rows.
- A row that returns (it wakes from Snooze, revives from Done, or comes back early) enters through the New band like a new row, marked "back" in the age column.
- At the next settle point the New rows take their ranked place and the mark goes.

The bad case this stops: a lane's row jumps to the top as the owner presses Enter, and the Enter opens the wrong row. This is #3082's expected behaviour 3 ("the order holds still between refreshes; a row moves only when its group changes"), applied to every group.

## 5. Acting in place

### 5.1 Actions per row kind

Keys follow §7.4. `Ctrl+Enter` opens the row's submit menu with the primary action preselected; `Enter` there commits it. The row's buttons (2 to 3, replacing the right end of the row, per design's grammar) show on hover and on the cursor row, and commit on click.

| Kind | Primary (first in the menu) | Also | Writes to |
|---|---|---|---|
| `decision`, `question` | Answer with the shown option | Decline, Copy command, Open | Sindri (`POST /sindri/decisions/{id}`) |
| `agent-asking` | Reply (text to the session, #3022) | Jump to terminal `o` (#3023), Hand to agent n/a | the session |
| `agent-limited` | Open session | Resume (when the pause is Sindri's) | Sindri, aw-meta |
| `lane-recycle` | Copy digest request (copies the standard line and jumps to the lane's terminal, `o`) | Open session | nothing: `aw-meta lane-send` refuses the meta lane and every Claude Code lane (AW-META-017, "use SendMessage"), so Home sends no digest request; the row clears when the lane's verdict is no longer `Recycle` |
| `review-requested`, `review-new-commits` | Open review (Enter, full) | Approve, Comment, Request changes in the `Ctrl+Enter` menu | GitHub, through `Core/Forge` (`PullReview`) |
| `pr-ready-merge` | Merge (in a meta team through `aw-meta merge-run`, which runs the same `merge-check` meta runs; otherwise the repo's merge method through the forge; `merge-run` takes no sha, so the adapter compares the PR head with the row's `headSha` just before the call and refuses with 016 on a mismatch, then reads the PR state after, since exit 0 covers both merged and queued) | Open, Hand to agent | GitHub, aw-meta |
| `mention`, `dm`, `thread-reply`, `keyword` | Reply (composer in the peek). For a Slack row, until Messages ships its send route (Messages M2c), the primary is "Open in Slack" and there is no Reply. *(agent default (Messages spec, #3188))* | Hand to agent (an agent writes a draft into the composer; the owner sends) | the source of the thread |
| `run-failed` | Retry the run | Open run, Hand to agent | Sindri (`workflow_run`) |
| `issue-assigned` | Start (state to Started) | Change state or priority, Hand to agent, Comment | Linear (`ChangeLinearIssue`, `CommentLinearIssue`) |
| `todo-due` | Check off | Change due date | Sindri (todo) |
| `system` | the row's existing primary action | Open | Sindri |

While meta is already merging a PR (its `mergeQueue` entry has `github: queued <position>`, or a `merge.queued` record), the row's waits-on says "meta is merging (queue position 2)" and Merge is not offered, so there is no second merge. The act also reads the PR state first (§5.2 step 3) and, when the PR is already merged, lands as a no-op and clears the row.

On every kind: Snooze `h`, Done `e`, Hand to agent `t` (except where noted), Mute (in `.`), Open `Enter`.

Hand to agent opens a small picker, as the Context page's Hand to agent does: a new session on a provider, or one of the repo's running sessions. The picker has a filter field, so it shows no letter or digit chips and `Enter` picks; a session that is asking the owner shows disabled with "asking you". It shows what goes along ("PR #3120, 2 linked issues, the thread, about 4k tokens") and leaves a "Handed to <session>" line with a link. The row stays in Needs you until its trigger stops holding. The brief to the agent holds the row's links and its peek text, fenced as untrusted.

### 5.2 Write-through

Every action that writes to a source of record (a reply, an approval, a merge, a state change) follows one pipeline. This is contract item 2 on #2978.

1. **Key to paint, under 50 ms.** The row changes in place (reply: the composer closes and the message shows as "sending"; merge: the glyph becomes the merged disc, waits-on says "merging"). The app has written nothing yet.
2. **Request.** `POST /sindri/home/rows/{id}/act` with `{action, version, actId, args}`. `actId` is a client ulid (`act_<ulid>`). A PR row stores the `headSha` the owner saw, and Merge and Approve send it in `args`: the adapter passes it to the forge (merge `sha`, review `commit_id`) and to `aw-meta merge-run`. When the PR head moved, nothing is written, the row refreshes and the code is 016. `args` per action is in S0's JSON schema (§8): `reply` takes `{body}`, `snooze` `{until}` or `{preset}`, with an optional `unlessReplied`, `hand` `{target, provider?}`, `merge` `{headSha, method?}`, `approve` and `request-changes` `{headSha, body?}`.
3. **Source write.** The server calls the source adapter and records `home.act.sent`. The adapter checks first whether the write already landed (a merge reads the PR state; a reply looks for the owner's last comment with the same body hash), so a replay is safe. It records `home.act.landed` when the source confirms.
4. **Within 2 s** the row shows the source's answer (D4). After 2 s without one, the row says "still sending" in the waits-on column and stays as it is. It does not roll back.
5. **Failure** (the source refuses, or 15 s pass): the row returns to its old state and shows "failed: <reason>" in the waits-on column in the attention colour, with **Retry** (`.` then `r`, or the button) and **Discard**. Retry sends the same `actId`. Discard drops the pending change. The reason comes from the adapter ("GitHub refused: required check `build` is red", "Linear: token expired, reconnect") and a Connect button replaces Retry when the connection is stale.
6. **Undo** exists only for Sindri-local state (Done, Snooze, Mute): 5 s. A source write has no Undo; the confirm is the Ctrl+Enter menu.

A second action on a row that is still sending is refused with "wait for the first to land" (code 004).

### 5.3 Snooze

`h` opens a picker (cancellable; when it closes, focus returns to the row). Choices: the keys `1` to `4` pick a preset while the date field is empty and unfocused. `5` or a click focuses the "Pick a date or time" field, and from then on digits type into it (so `14:00` types). The footer keys while it is open are in §7.4.

1. Later today: 17:00 local, or 3 hours from now when it is after 14:00.
2. Tomorrow 9:00.
3. Monday 9:00: the next Monday after today.
4. Next week: the same weekday next week, 9:00.
5. Pick a date or time (the field): `fri 14:00`, `nov 20`, `in 2h`.

Rules:
- The row leaves Home and shows `back Mon 9:00` under the "Snoozed (n)" filter (off by default, a toggle in the header).
- **It returns early** when something new is addressed to the owner on that object: a reply in the thread, a review request, CI red on the PR, the session asking again. The row says "back early: new reply" for its first view.
- The wake is a local timer in the app (within 1 s of due, D9). When the app was not running at the due time, every due row wakes at the next start, and one toast says how many.
- Snooze works on any kind, including a todo and an agent that is asking. It shares one store with Messages' "follow up if no reply" (#3020).
- **Follow up if no reply.** A snooze with `unlessReplied: true` (Messages' snooze picker checkbox; Home's own picker does not show it) wakes at its time only when nobody replied in the thread since the snooze. When someone did reply, the row clears at the due time instead of waking, and a `home.row.cleared` event records it. The early return above still applies. *(agent default (Messages spec, #3188))*
- Longest snooze: 365 days (code 008). Undo lasts 5 s.
- Taken from the Linear `H` flow (ledger 5, take 3). The presets are #3030's, not the ledger's.

### 5.4 Done

`e` marks the row done, whatever its kind (ledger 4, take 4; #3031). The row leaves Home in one frame. Undo lasts 5 s.

- A done row **returns on its own** only when new activity is addressed to the owner on that object: a mention, a review request, a reply in the owner's thread, CI red on the owner's PR, the session asking again. Other activity changes nothing. The row says "back: new reply". A source clear (the lane recycled, the session ended, the PR merged) ends the episode and forgets the Done, so the next recycle mark or ask is a new row.
- Done never writes to a source. A merged PR's row clears itself; a replied thread's row clears when the reply lands.
- The **Done list**, reached from the clickable footer text "Done list", from the `?` sheet and from the palette id `home.done-list` (it has no bare key), holds the last 7 days of done rows (`home.done.keepDays`, whole days 1 to 90, default 7). #3031 sets 7; the ledger's 90 days is GitHub's number.
- **Mark all seen** stays as the bulk form of "seen" (the row stops looking unread and the tray count drops); it does not hide rows and has the existing Undo.
- `e` stays a bare key by the owner's decision (§7.4 states the boundary). Done writes only Sindri's own state and shows Undo for 5 s.
- **Undo has a key:** `Ctrl+Z` on Home undoes the last Sindri-local change for 5 s, and the same step stays on the Done list after that. A polite live region announces "Marked done. Undo with Ctrl+Z." and the same for a failed or still-sending write (§5.2).

### 5.5 Definition of done: the empty Home

Home is empty when Needs you holds no row in state `open` **and** no stale line is showing, so every source was read in the last 5 minutes with no `degraded[]` entry about a source. Snoozed, done and muted rows do not count; a row whose write failed does count. When Needs you is empty but a source is stale or disconnected, the body says "Nothing waiting in what I could read" with the stale line and **Refresh**, never "You're done."

An empty Home shows, in the body: "You're done." (20 px, ink), one line under it ("3 snoozed, the next back Mon 9:00" when there are snoozed rows), and the brief if it has lines. Moving is not drawn (ledger 4: only the brief remains). The tray count is 0.

## 6. The brief, Moving and notifications

### 6.1 Since you last looked

The cursor is a ledger event number, `home.cursor.moved`. It moves to the ledger head when Home closes after it was visible for at least 5 s, so a glance does not erase the brief. Lines the owner has seen on screen drop at the next refresh (Slack recap rule, ledger 9).

Two writers:
1. **Facts, by Sindri, no model.** A job reads the ledger from the cursor and writes one line per change that matters: a PR merged, CI went red or green on a PR of the owner's, a run ended, a lane recycled, an issue changed state. A line is a sentence plus at most 2 links (deep links to a row or an object). Example: "PR #3078 merged; CI red on main since 09:10." Painted at first paint, under 100 ms (D12).
2. **Summary, by a model.** A Sindri job (kind `HomeBrief`) runs when Home opens with events since the cursor and the last brief is older than 15 min, or when the owner presses `.` then `u`. It reads only typed facts (kind, title, state change, ids) after `Aw.Redaction`, never message bodies. It writes the order of the lines and one summary line at the top. Every link in its output must resolve to a row or object id from its input; a line with a link that does not resolve falls back to its plain-fact form.

The default view is a summary line plus 2 fact lines, then "b N more lines" (`b` shows the rest, §7.4). The cap is 10 lines. When there are more than 10 facts, the first 9 by (blocks, recency) show, then "and 14 more: see Moving, Work". The minimum is 0 lines; with 0 the section is the one word "Nothing changed." Each line is an ordinary row: it can be peeked, done and snoozed.

Who sees what when the model is limited or off:

| Case | Section shows |
|---|---|
| `home.brief: auto` (default), writer works | summary line plus fact lines |
| model limit hit (the model-limit pause) | fact lines only; a quiet header line "Facts only: writer paused at its model limit" and a **Resume** button |
| no agent provider signed in | fact lines only; "Facts only: no writer signed in" and a **Sign in** button that opens the sandbox login |
| the job failed | fact lines only; "Summary failed: <reason> (SND-…)" and **Retry** |
| `home.brief: facts` | fact lines only, no header line |
| `home.brief: off` | the section is not drawn |

### 6.2 Moving

One line per activity the owner started or owns that is in flight and needs nothing from them: an agent session working in one of their lanes or launches (`state: running`), a workflow run in progress, CI running on the owner's PR (`ReviewCi`), a Queue item in the `running` or `withYou` group. A line reads "design: building RowList (12 min)" or "CI on #3120: 4 of 7 checks done".

Every group header (New, Needs you, Moving, Since you last looked) has a chevron (right folded, down open) and is a click target: a click anywhere on the header bar toggles the fold, and the chevron's hit area is at least 28 x 28 px. A folded header keeps its count ("Needs you 8"). Moving is **collapsed** by default (Left on its header folds it and Right unfolds it, §7.4): a 28 px header "Moving: 4 agents, 2 CI runs" with a chevron. Expanded it shows up to 8 lines, oldest start first, then "Show all N in Agents". The expanded state is remembered per user. A line has no actions, only peek and open. When its state changes to something that needs the owner (CI red, a session asking), the object leaves Moving and a row enters Needs you through the New band.

### 6.3 Notifications

What notifies: a row **entering** Needs you with one of these kinds: `agent-asking`, `decision`, `question`, `review-requested`, `run-failed`, `mention`, `dm`. One OS toast per row per episode (an episode ends at Done, a clear by the source, or a snooze that wakes). Clicking the toast opens Home with that row's peek open (as #3026 does for agents).

What never notifies: `thread-reply`, `keyword`, `pr-ready-merge`, `review-new-commits`, `issue-assigned`, `todo-due`, `lane-recycle`, `agent-limited`, `system` (a stale connection is the one exception: one toast, since every row of that source goes stale), Moving, the brief, a row that comes back from Done (it notifies only if its kind is in the list above), a row inside a muted object.

The tray count is the number of open rows of the notifying kinds. It is the one count in the app: #3026's agent badge is the `agent-asking` part of it, not a second badge. A mute applies to the thread, the PR and the issue about it together (ledger 10) and ends only on a direct ping: a mention naming the owner or a review request to the owner.

A toast shows the row's title (for Messages rows, sender and place only: "Sam, DM"). A row holds no message text, so a toast with a text preview (Messages' `messages.notify.preview: on`, Messages Release 2) needs a body from the kind's owner: the toast function takes an optional body that the owner kind's provider returns in process on the Home host, never from the row or the ledger. Without a provider, or with the setting off, there is no body. *(agent default (Messages spec, #3188))*

Toast and tray run in the Sindri app, from local state, with no push service (ledger 10). When the app is closed to the tray, they work; when it is not running, nothing notifies.

## 7. The page

### 7.1 Layout

Design's frame on #2978 (comment 6817965291) is the reference; every number is `grammar.css` / `Tokens.axaml`. At 1440x900:

```
┌ top bar 40 ───────────────────────────────────────────────────────────────────────────────┐
│ Home   [All 9] [Reviews 3] [Messages 2] [Work 1] [Agents 3]      / filter                 │ page header 48
├ New 1 ────────────────────────────────────────────────────────────────────────────────────┤ group header 28
│ ◇ qa is asking to run rm -rf build/                asking     session qa    new           │ row 32
├ Needs you 8 ──────────────────────────────────────────────────────────────────────────────┤
│ ◇ meta: at 205k, recycle from a digest              blocks 3   lane meta    15m           │
│ ◎ #3120  Review requested: System grid              blocks 2   repo/ui      1h40          │
│ ◎ #3099  Ready to merge: spacing scale              blocks 2   repo/ui      68m           │
│ ◇ ideas: at 210k, recycle from a digest             blocks 2   lane ideas   20m           │
│ ...                                                                                       │
├ ▸ Moving: 4 agents, 2 CI runs ───────────────────────────────────────────────────────────┤
├ Since you last looked 6 ──────────────────────────────────────────────────────────────────┤
│ PR #3078 merged; CI red on main since 09:10.                                              │
│ (2 fact lines)                                                                            │
│ b 3 more lines                                                                            │
│ ○ Todo, 4 open                                               Enter opens the panel        │ 28
├ j/k move · Space peek · Enter open · h snooze · t hand to agent · e done · R re-rank  Done list ┤ footer 28
└───────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Frame:** top bar 40, page header 48, body 784, footer 28. Group headers 28, sentence case, 12/500, the row count only in ink-3 (never "blocks N"; blocks N stays per row). Side inset 24.
- **Row (32 px compact, 40 comfortable):** `[glyph 16] [mono id] [title, flex, min 200] [why chip 104] [object 104] [from 136] [age 44, right-aligned] [waits on 112]`, 12 px gap. The why chip carries "blocks 3" or the reason word. The mono id sits left of the title, as the #2978 Row rule says (`#3120`, `r-0412`, `ENG-412`). A lane, session or DM row has no id: its title starts in the id column, and the object column names it (`lane meta`, `session qa`, `dm sam.ortiz`). The object fact column says the kind and place only: lane, repo or dm. The from fact is a person or session name. Title is the only 13 px ink text; unread weighs 500, read is ink-muted. The waits-on column shows only where its value varies within a group (Moving). Needs you rows show no waits-on `you`, because the group header says it. The id adds to the fixed part; the title keeps its 200 minimum.
- **Width check:** the fixed part is 636 px, so the title gets `list width - 636`: 364 px at 1000, 804 at 1440 with the peek closed. Under 840 px of list width the third fact drops; under 688 the second drops. The title minimum of 200 never shrinks (design's rule on #2978: facts end in an ellipsis, never a mid-word clip).
- **Rows per screen (D3):** body 784 minus 8 padding minus 3 group headers (84) minus the brief's 3 lines (96) leaves 596 px, which is 18 rows at 32 px and 14 at 40. The Todo line (a 28 px non-group line with an empty-circle glyph, "Todo, N open" and "Enter opens the panel" at the right, §12) takes one compact row, so D3 asks for 17 and 14.
- **Splits** are the saved filters: All; Reviews (`review-requested`, `review-new-commits`, `pr-ready-merge`); Messages (`mention`, `dm`, `thread-reply`, `keyword`); Work (`issue-assigned`, `todo-due`, `run-failed`, `decision`, `question`); Agents (`agent-asking`, `agent-limited`, `lane-recycle`, plus Moving's agent lines). Each tab shows its open count, computed from row kinds: open rows of its kinds, the New band included (the frame's All 9 is New 1 plus Needs you 8; All = New + Needs you). A split filters New, Needs you and Moving to its kind. The brief ("Since you last looked") and the Todo line show on All only. A tab with no rows says why: Messages reads "Messages arrive with #2979" until that epic ships. There is no "Other" tab: a row that is not a Needs you kind is not on Home (Linear's Priority / Other split is covered by Needs you versus the source pages).
- **Header right:** the `/` filter box. There is no health chip: the shell's top-bar pill is the one status mark, and a stale or failed source shows as its §7.3 state line.
- **Footer:** the grammar keys, then Home's. At 1440 the page keys carry their words (`h snooze · t hand to agent · e done · R re-rank`), with "Done list" at the right. At 1000 they shrink to `h snooze · t hand · e done · ? all keys`.
- **Text scale 1.3** scales text and row heights only (row 40, group header 36). Column widths, insets and the 440 px peek stay in px, so long facts ellipsize. A truncated title shows a plain tooltip with the full title.
- **Glyphs** come from design's set (diamond for needs you, x for failed, arc for an agent running, ring with dot for a PR, bubble for a message, dot for a mention); lane-recycle rows use the needs-you diamond and ready-to-merge rows the PR-open ring, so no new glyph is drawn. The grammar adds Down and Stale glyph aliases (`.g.fail`, `.g.parked`) for §7.3.

### 7.2 Peek (Space) and full (Enter)

Peek is a 440 px pane on the right (the list narrows to 1000 at 1440). `j/k` moves the peek with the cursor. It paints within 50 ms of `Space`, from the local store, with no spinner. Header 48 (title, ref, open-full, close), fields as two columns of 28 px rows, a body, 2 to 3 actions at the bottom. Fields on every kind: Source, Waiting since, Blocks (up to 5 dependents as links; "and 4 more"), From, Linked (the other objects).

| Kind | Body |
|---|---|
| `review-*`, `pr-ready-merge` | the diff summary (files, +/-), CI checks with state, the linked Slack thread's last 3 messages when Messages exists, the closing issues |
| `mention`, `dm`, `thread-reply`, `keyword` | the last 5 messages, an inline reply composer. For a Slack row before Messages M2c: "Open in Slack" in the composer's place. From M2c on, the composer is the Messages composer, with its rule: `Ctrl+Enter` sends, `Enter` is a new line. *(agent default (Messages spec, #3188))* |
| `agent-asking` | the last ~30 terminal lines, the pending question, a reply box (#3022); a session Sindri cannot write to says why ("started outside Sindri: copy the reply") |
| `lane-recycle` | context size and threshold, when the last digest was written, what the lane owns (the `item-of` list) |
| `decision`, `question` | the question and its options |
| `issue-assigned` | Linear fields and linked PRs |

On a PR row, `Ctrl+Enter` in the peek opens the submit menu with Merge preselected (Merge via `aw-meta merge-run`, Approve, Comment…, Request changes…); `Enter` is Open.

**Full** (`Enter`) goes to the working surface: the review for a PR, the Agents sheet for a session, `DecisionSheet` for a decision, the Linear issue, the run page. `Esc` returns to Home with the cursor on the same row.

### 7.3 States

Every error row has a button that fixes it.

| State | What shows |
|---|---|
| first open, no store yet | the page header and footer paint at once; the body shows 6 skeleton rows for at most 400 ms, then the rows or the empty state |
| loading from a slow serve | the cached first page paints first (D2); a quiet header line "Updating…" is a polite live region; no spinner over rows |
| serve not running | the cached rows paint and a header line says "Sindri isn't running · showing 09:02" with **Start Sindri**; actions are disabled with the same reason |
| a source is stale (not read in 5 min) | one 32 px state line (ui's `FailedReadLine` shape, #3056): glyph, one sentence ("GitHub last read 12 min ago"), an outlined primary **Refresh** and an optional `Details ▸`; rows from the source get a muted title and age |
| a source is not connected | no rows from it; one `system` row "Connect GitHub to see reviews" with **Connect** |
| a failed read of Home | the same state-line shape: "Couldn't read Home" with **Retry** and the log path; never the empty "You're done." (fixes #2921's skeleton) |
| a row action failed | the row shows "failed: <reason>" with **Retry** and **Discard** (§5.2) |
| connection stale on an action | the row's Retry becomes **Connect** |
| write still sending | "still sending" in waits-on after 2 s |
| a row's version changed under an edit (003) | the edit is not applied; the row shows "Changed while you were editing" and **Reload** (keeps the cursor on the row) |
| New band has rows | the "New (n)" header; the page title shows "Home (n new)" in the window title for screen readers |
| a snoozed row wakes | the age cell says "back"; the sub-line gives the reason ("back early: new reply" or "snoozed until 9:00") until first seen |
| Needs you empty | §5.5 |
| a split tab with no rows, the Done list empty, Moving with 0 lines, the hand-to-agent picker with no session | one plain line each ("Nothing in Reviews"; the Messages tab says "Slack messages arrive with #2979" only while it has no rows and Messages is not shipped, since Linear and GitHub mentions fill it before then; "Nothing done in the last 7 days", "Nothing in flight", "No session to hand to" with **Start one**) |
| the peek body can't load | the fields from the local store and "Couldn't load the thread" with **Retry** |
| brief states | §6.1 table |
| writer paused or off | §6.1 |
| model for the brief is limited | **Resume**, §6.1 |

### 7.4 Keyboard and focus

The grammar keys are the same on every list (#2978): `j/k` and arrows move, `Home`/`End`, `/` filters, `Space` peeks, `Enter` opens, `Esc` goes back one layer, `.` opens the row's action panel (each action shows its key), `?` shows the keys of this surface, `Ctrl+K` is the palette. **A bare key never writes to a source (Slack, GitHub, Linear, notes) and never starts work; a bare key may change Sindri's own view state when Undo shows for 5 s (`e` Done, `h` snooze).** (The owner wrote this boundary with `s` for snooze; the spec uses `h` because #3030 and Linear's flow use `h`, and `s` already means state on Projects and suggest on Reviews.) Every bare key below obeys it: `h`, `t` and `r` only open a picker or a composer, and the write or the start happens on the picker's Send or on `Ctrl+Enter`; `e` is the Undo case; `R`, `[`, `]`, `1` to `5`, `b` and the fold arrows change the view only. `c` (create) and `x` (select) are not Home keys in v1: Home has no create verb and no bulk select (§5), and `x` is Stop on the Agents page. The footer does not show them, and the U1 build drops the two hints design's frames carry. Home adds these, after the divider in the footer:

| Key | Does | Writes? |
|---|---|---|
| `h` | opens the snooze picker (§5.3) | no, until a choice is made; then Sindri-local |
| `t` | opens the hand-to-agent picker ("tell an agent") | no, until Send |
| `r` | opens the reply composer in the peek (kinds with a thread). Not on a Slack row before Messages M2c: there it does nothing and the footer drops it. From M2c on it opens the Messages composer. *(agent default (Messages spec, #3188))* | no, until `Ctrl+Enter` sends |
| `e` | Done, with Undo | Sindri-local |
| `Ctrl+Enter` | opens the submit menu with the primary preselected; `Enter` commits | the source, on commit |
| `R` | re-rank now (a settle point) | no |
| `b` | show the brief's lines beyond the default summary line plus 2 fact lines ("b N more lines") | no |
| `[` / `]`, `1` to `5` | previous / next split; jump to a split | no |
| Left / Right | the tree convention, on every group (Needs you, Moving, the brief). On a group header, Left folds the group and Right unfolds it. On a row, Left moves the cursor to its group header (so Left Left folds the group the cursor is in) and Right does nothing. Bare `g` is not a Home key: the shell's G-chord (`G` then a page key (`H`, `N`, `D`, `P`, `Q`, `C`, `A`, `.`, `S`, `T` and the others `SecondKey` maps) within 1 s; `ShellViewModel.Chords.cs`, `PressKey`) owns it on every page, from a header as from a row. | no |

Fold rules (the table row above gives the keys):

- A group header is a `j/k` stop, so Left Left lands the cursor there. Enter and Space on a header toggle its fold and open no peek; Right on an already-open header moves to the first row, Left on a folded header does nothing, and Left or Right on the Todo line does nothing (it is not a group).
- Only Moving remembers its fold per user. Needs you, the brief and the New band open unfolded every time, and the cursor starts on row 1. The New band is never folded when it holds a row. A new or returning row, a toast click, `Esc` back from Full, a `/` match and a split jump all unfold the group of their target row first.
- Folded rows still count in the tab numbers. "You're done." (§5.5) reads the data, not the fold.
- Fold arrows are caret keys while a text field has focus, the same guard as the letters. On Home's split strip, Left and Right switch splits when focus is on the strip, as on Health and Queue, and fold when focus is in the list. A pending G-chord takes its second key before Home's list sees it, so `G` then `H` goes Home and never opens a snooze picker; the top bar keeps its "G ..." hint for the 1 s wait (U3 tests both).
- On Home, `G` `D` and `G` `T` (which set `HomeFilter.Decisions` and open the Todo panel on main) retire with the old views: `G D` selects the Work split (the Done list has no chord) and `G T` opens the collapsed Todo line's panel (§12).
- Fold and unfold have palette ids `home.fold` and `home.unfold`, the `?` sheet lists the arrows, and the footer shows "Left fold · Right open" only while the cursor is on a header. While a menu or picker is open, the page footer swaps to its keys: "↑/↓ move · Enter pick · Esc cancel"; menus carry no key strip of their own. A screen reader reads a header as "Needs you, 8 rows, expanded" or "collapsed, 8 hidden".

Tab keeps its normal job: it moves focus between the filter box, the list, the peek and the footer. Splits do not use Tab because Tab has to stay a focus key (the ledger's Tab binding is a Superhuman snippet and is in "Not solid"). Key clashes were checked against the Context page (`c e / j k g n s`, and `m w r` inside its `.` menu), Projects (`s` state, `a` assign, `h` hide, `e` notes, `o` open, `c`), Reviews (`n p` files, `c`, `s` suggest, `v`, `r` reply) and Agents (`o` jump, `x` stop). `t` and `R` clash with none. Two letters differ by surface, as meta's rule allows because each opens something cancellable or is Sindri-local with Undo: `h` (snooze here, hide on Projects) and `e` (Done here, edit on Context and Projects). Letters work only while focus is in the list or the peek and not in a text field, as `RowKeys` will guard once #2952 merges (`Views/Controls/RowKeys.cs`; on main today the keys sit in `ShellWindow.axaml.cs:143`).

Every action also has a menu item and a palette entry under the same id: `home.open`, `home.peek`, `home.reply`, `home.send`, `home.approve`, `home.comment`, `home.request-changes`, `home.merge`, `home.start`, `home.retry-run`, `home.answer`, `home.digest`, `home.snooze`, `home.unsnooze`, `home.done`, `home.undo`, `home.mute`, `home.hand`, `home.retry`, `home.discard`, `home.rank`, `home.brief`, `home.density`. A button with no palette entry is a bug (ledger 11).

Screen readers: the list is a list of groups of rows; a row reads "needs you, meta, at 205k, recycle from a digest, blocks 3, waits on you, 2 hours". The New band is announced once as "1 new row". Reduced motion turns the toast slide and the row removal into an instant change.

### 7.5 Visual bar

One accent. Status colour only on status marks (the glyph, the failed reason). Every text 4.5:1 on its fill, every meaningful mark 3:1, held by a contrast check in the Frames suite over every row state (rest, hover, cursor, selected, new, failed, snoozed). Type scale and spacing from the tokens. Nothing in a row wraps. Inspora is the reference (#2978, owner 2026-11-11).

## 8. API and engine requirements

Routes, all under `/sindri/home`, named by S0 (§15). The three `GET /sindri/home*` routes apply the §9 session filter too: a session sees only its repos' rows and gets no body for repo-less rows (`dm`, `mention`, `thread-reply`, `keyword`); a test reads a `dm` row as a session:

| Route | Does |
|---|---|
| `GET /sindri/home?split=&after=&limit=&snoozed=` | the rows by group, current ranked order, with `counts`, `cursor`, `degraded[]` and the first page (default 60) |
| `GET /sindri/home/rows/{id}` | one row with its peek body and `blocksList` |
| `POST /sindri/home/rows/{id}/act` | `{action, version, actId, args}`; the §5.2 pipeline |
| `POST /sindri/home/rows/{id}/snooze` | `{until}` or `{preset}`, plus an optional `unlessReplied` (bool, default false; §5.3) |
| `POST /sindri/home/rows/{id}/done`, `/undo`, `/mute` | Sindri-local state |
| `GET /sindri/home/done` | the Done list |
| `POST /sindri/home/brief/refresh` | starts the `HomeBrief` job |
| `GET /sindri/home/rank/{id}` | the `--explain` data |

Requirements by area:

- **Ranking (api, Core).** `HomeRank` is a pure function in `Aw.Sindri.Core.Home`: rows, edges and weights in, ordered rows out. The §4.2 example is its golden test. Edges live in a `home_links` table of the view, filled by the collectors.
- **Collectors (api, engine).** One per source in §3.3. Each raises and clears rows by ledger event and records `source.readAt`. The lanes collector is a service in the Sindri host that runs `aw-meta lanes-context` and `aw-meta status` read-only; it never calls `merge-run`, `gate-run` or `lane-recycle` (an action does that, §5.1). `lanes-context` and `usage` exit 1 exactly when something needs attention and still print valid JSON, so the collector reads stdout; only a missing binary, exit 2 or JSON that does not parse raises `lanes-unavailable`.
- **Write-through adapters (api).** Forge (`PullMerge`, `PullReview`, `PullReply`), Linear (change and comment), decisions, the session reply route (#3022), aw-meta (`merge-run`, `lane-send`). Each takes the `actId`, checks for an earlier landing, and returns the source's reason on refusal.
- **Snooze, done, mute stores (api).** Ledger events, projected; `home.row.woke` and `.revived` come from the collectors when addressed activity arrives. One store serves Messages (#3020).
- **First paint (ui, api).** `GET /sindri/home` returns 60 rows and the counts in one read. The app writes the last answer to its cache file in `$AW_SINDRI_HOME/ui/` and paints from it before the read returns (D2).
- **Brief (engine).** The `HomeBrief` job: facts, writer, link check, limit states (§6.1). It reports its token spend like other jobs.
- **Profile keys (api).** `home.density` (`compact` | `comfortable`, default `compact`), `home.rank.weights.<reason>` (integers 0 to 9), `home.done.keepDays` (1 to 90, default 7), `home.brief` (`auto` | `facts` | `off`, default `auto`), `home.notify` (`priority` | `off`, default `priority`). `home.page` (`rows` | `panels`, default `panels` until U3, §2 decision 14). An unknown or out-of-range value is SND-HOME-001 with the key path. They take rows in `docs/csharp/sindri-profile.md`, and `sindri profile set` accepts them.
- **Routes and text safety (api).** `GET /sindri/ledger` and `GET /sindri/events` leave `home.*` events out for a session, since those events carry row titles, facts and brief lines (today only `todo.*` text is fenced there, `ReadLedgerEndpoint.cs`); the owner gets them all. Every `/sindri/home` POST needs the per-start human token, like profile writes (SND-AUTH-002 for a session), and rejects a browser `Origin`. Collectors and adapters call `aw-meta` and other CLIs with an argument list, never a shell string; lane names come from `aw-meta` output and PR numbers are integers. A reply sent to a session's terminal drops control characters except newline and goes as one literal paste with no trailing Enter. `args` pass through `Aw.Redaction` before `home.act.sent`, and the event keeps a body hash only. The UI cache file is mode 0600 in a 0700 folder and holds the first page only. Row ids are percent-encoded in paths; S0's golden has one example.
- **Observability (engine).** `sindri doctor` gets a row per collector and for the brief job (last read, last error); the same change updates `src/Sindri/aot-smoke/expected.txt`.
- **Events and migration (api).** The §3.1 events join the event catalog; `home_rows` and `home_links` are view tables, so they take the next free number in `Infrastructure/Views/Migrations/` (latest `0016_items_state.sql` on main; the projector rebuilds them from the ledger) and move no `ledger schema` line. Only the runs "seen" flag (§3.3, beside `workflow_seen`) is a ledger migration: next free in `Infrastructure/Ledger/Migrations/` (latest `0019`), no gaps (AW-DATA-005), and it bumps `ledger schema N/N` in `aot-smoke/expected.txt`. Both numbers are taken at final merge. Why a new table and not `view_needs_you` (`key`, `row_json`, `seq`): Home rows come from six sources with their own id, version, state (open, snoozed, done, muted) and rank fields, which `sindri needs-you` rows (§12) do not have; `home_links` is many-to-many. (agent default; names keep the `home_` prefix as the `home.*` events do.)

## 9. Agents

Everything the page does, an agent can do through one verb. `sindri home <verb>` on the CLI, `home_<verb>` over MCP at `/sindri/mcp`.

| Verb | Arguments | Does | Refused with |
|---|---|---|---|
| `list` | `[--split S] [--group G] [--snoozed] [--max N] [--json]` | rows in ranked order with `rank`, `blocks`, `degraded[]` | none |
| `show` | `<id> [--json]` | one row with its peek body and `blocksList` | 002 |
| `rank` | `<id> --explain [--json]` | blocks, the dependents walked, weight, age, and why the row is above the next | 002 |
| `act` | `<id> --action A [--args JSON] --version N` | an owner action | 002, 003, 004, 005, 010, and SND-AUTH-002 for a session |
| `snooze`, `done`, `undo`, `mute` | `<id> ...` | Sindri-local state | the same |
| `draft` | `<id> --text T` | puts a draft reply into the row's composer; sends nothing | 002, 013 |
| `brief` | `[--refresh] [--json]` | the brief's lines and its writer state | none |
| `done-list` | `[--json]` | the Done list | none |

Rules, as for the other Sindri tools:
- A session reads the rows of the repos its `X-Sindri-Repo` header names. For repo-less rows (`dm`, `mention`, `thread-reply`, `keyword`, `agent-limited`, `system`) it gets the id, kind and counts, and the body and peek only go to the owner. Repo and source text in every result is fenced as untrusted, over MCP and in `--json`.
- A session never presses the owner's buttons. `act`, `snooze`, `done`, `undo` and `mute` from a session are refused with SND-AUTH-002, the way profile writes are. A session may `draft`: the owner reads the draft and sends it.
- Ids print as `<source>:<kind>:<source id>`; `--json` carries keys, text carries words.
- Exit codes follow the context family: `0` ok, `1` attention (a refusal in the last column, Sindri not running), `2` a tool or usage error.

## 10. Errors

Reserved now: SND-HOME-001 to 029 (no `SND-HOME-` code exists on main).

| Code | Meaning | HTTP | CLI exit |
|---|---|---|---|
| 001 | a `home.*` profile key is unknown or out of range; carries the key path | n/a | 2 |
| 002 | row not found | 404 | 1 |
| 003 | write names an old row version; carries the current one | 409 | 1 |
| 004 | the action does not apply to this row kind, or an earlier action on the row is still sending | 400 | 1 |
| 005 | the source refused the write; carries `source` and `reason` | 502 | 1 |
| 006 | the source did not answer in 15 s; the write may still land (the row says "still sending" until it is known) | 504 | 1 |
| 007 | a typed snooze time can't be read; the fix lists three examples | 400 | 2 |
| 008 | snooze time is in the past or over 365 days away | 400 | 1 |
| 009 | Undo came after its 5 s | 409 | 1 |
| 010 | the source is not connected; the fix names the connect step | 409 | 1 |
| 011 | `--version` missing on a write | 400 | 2 |
| 012 | hand-to-agent target is gone or not one of this repo's sessions (for a repo-less `dm` or `mention`, the picker lists the owner's sessions and the "what goes along" line names the target's repo) | 404 | 1 |
| 013 | a reply or draft is empty or over 8,000 characters | 400 | 1 |
| 014 | an `actId` was replayed with a different body | 409 | 1 |
| 015 | the brief job failed; carries the cause | 500 | 1 |
| 016 | the PR head moved since the row was read; carries the new `headSha` | 409 | 1 |

Each has a sentence for the app and a `fix:` for the CLI, written in S0. The profile keys' errors reuse the context family's shape.

`degraded[]` entries on `GET /sindri/home` are data, not errors: `{code, detail}` with `source-stale` (detail: source and minutes), `source-disconnected`, `brief-model-limited`, `brief-no-writer`, `brief-failed`, `lanes-unavailable` (no aw-meta team on this box), `behind` (the projector is behind the ledger, with the event count). The page prints a label for each; agents branch on `code`.

## 11. Goals and non-goals

Goals: the job in §0, the done-checks in §1, the page in §7, the agent surface in §9.

Non-goals for this spec:
- Chained actions ("approve the PR, close the Linear issue, reply in the thread" as one press). Ledger 3 names them as where one app wins. Ship 1 does one action per press; chains wait for the workflow graph.
- A mobile Home. The ledger skips it for now (13). The row stays narrow-safe: three facts drop first.
- Quiet hours, per-kind notification settings, email or push.
- Editing the ranking rule beyond the integer weights.
- Writing to Slack. It waits on Messages (#2979); Home only defines the row kinds `dm` and `mention`.
- A second inbox for agents. The Agents page keeps its own idle and slot-wait ranking.

## 12. What happens to today's views

| Today | After |
|---|---|
| `Views/Shell/HomePage.axaml`, `HomePage.axaml.cs`, `HomePage.NeedsYou.cs`, `HomePage.Regions.cs` (panel cards; focus regions for Todo and Needs you) | rewritten on `RowList` (P0): groups, the New band, peek. `HomePage.Regions.cs` is deleted |
| `ViewModels/HomeFold.cs` (the 5 / 3 row caps and the fold of 8) | deleted; the list is virtualized |
| `ViewModels/NeedsYou/HomeCounts.cs` (running, withYou, queued totals) | deleted; the header tab counts replace it |
| `ViewModels/NeedsYou/NeedsYouFeed.cs`, `NeedsYouGrouping.cs`, `NeedsYouGroup*.cs`, `NeedsYouInboxGroup.cs`, `NeedsYouRowViewModel*.cs`, `NeedsYouKey.cs`, `NeedsYouViewModel.Keys.cs` | replaced by `HomeRowViewModel` over `RowKeys`; deleted in U4 once nothing else uses them |
| `ViewModels/NeedsYou/SeenTracker*.cs`, `NeedsYouSeenViewModel.cs` | kept: "seen" is local presentation state and Mark all seen stays |
| `Views/NeedsYou/DecisionSheet.axaml`, `NeedsYouSheet.axaml` | kept as the Full view of a decision or question row |
| `Core/NeedsYou/*` and `GET /sindri/needs-you` | kept: `sindri needs-you` and the MCP tool keep working; the projection is one row producer. `NeedsYouOrder.cs` stays for rows of one kind; cross-kind order moves to `HomeRank` |
| `Views/Todo/TodoPanel.axaml`, `ViewModels/Shell/ShellViewModel.Todo.cs`, `ShellWindow.TodoKeys.cs` | kept in ship 1 as a collapsed "Todo, 4 open" line under the list that opens the panel as its Full view; only due todos become `todo-due` rows |
| Running / With you / Queued column (`GET /sindri/queue`) | `running` and `withYou` become Moving lines; `queued` stays on the Queue page |
| the health strip | none: the shell's top-bar pill is the one status mark; a stale source shows as its state line (§7.3) |
| the setup line (`SetupViewModel.IsHomeLineShown`) | unchanged, above the list |
| `ViewModels/Labels.Home*.cs`, `Labels.NeedsYou*.cs` | strings rewritten for the new rows |
| `Views/Palette/CommandPalette.axaml` | gains the `home.*` ids of §7.4 |

## 13. Gaps found

From a read of `origin/main` a4d03ace1c and the issues (no build, no app run for this spec; D1 and the §15 proof walk drive the real app):

| What | Number | Issue | Spec part that fixes it |
|---|---|---|---|
| The Agents page counts only `state: asking`; a lane at its recycle mark is no state, and `SessionRowView` has no context size | "0 asking you" with 2 lanes at the mark | #3082 | §3.2 `lane-recycle`, §3.3 |
| Raw ids lead Home rows and the decision line is engine-speak | n/a | #2782 | §3.1 `title` |
| The PR-waiting count shows twice | 2 places | #2827 | §7.1 one count per tab |
| Needs you stays on its skeleton when its read fails | n/a | #2921 | §7.3 "a failed read" |
| Linear "since you last looked" is a wall of links with no titles | n/a | #2536 | §6.1 |
| #2952 adds `RowKeys.cs`; until it merges, the keys live at `ShellWindow.axaml.cs:143` (a private method) | 1 file not on main | #2951, #2952 | §15 P0 reuses it |
| `Tokens.axaml:229-233` names `Row.Height.Compact` 28 and `Default` 32; the grammar says compact is 32 and comfortable 40 | 28 vs 32 | #2978 | §2 decision 9, P0 |
| No `RowList`, `Row` or `PeekPane` exists, and no open PR builds them (search of open PRs, 2026-11-12) | 0 PRs | #2978 | §15 P0 |
| No snooze, no per-row done, no revive rule, no peek on Home | n/a | #3030, #3031 | §5.3, §5.4, §7.2 |

## 14. Proof

- The D1 to D14 numbers, before and after each phase, as a comment on #2978.
- Screenshots of every §7.3 state at 1280 and 1440 wide, the page at 1000 wide, the peek open at 1440 (3 facts), comfortable and compact, the New band, a failed row, a snoozed row, the empty state.
- The aw-ui-driver walk of D1 on the #3082 box state, a screenshot of row 1 naming the session, the step count.
- A property test for D6 and a golden test for the §4.2 example.
- A fake source per kind for D4 and D5: the 2 s landing, the refusal, the timeout.
- A test plants a token in a row title and in a Linear comment: the row, the peek, the brief and the toast show it scrubbed, and agent output fences it.
- ux runs D14, the day without the other apps, and files each open as an issue.

## 15. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Lane | Starts | Needs |
|---|---|---|---|---|
| P0 | the shared controls: row tokens (32 / 40, density), `RowList`, `Row`, `PeekPane`, with Frames tests. Design builds them: no PR exists (2026-11-12). Design reuses `RowKeys` from #2952 and gives `RowList` no key handler of its own | design | now | #2952 merged; until then design codes against the keys at `ShellWindow.axaml.cs:143` |
| D1 | the Home frame redrawn with this spec's states: New band, failed, snoozed, empty, brief states; the lane and merge-queue glyphs; the §7.1 width numbers checked | design | now | none |
| S0 | reserves its migration number at start; adds a `home` mode to `scripts/sindri-ui-fixture.sh` that serves the fixture to the page; the wire shape: JSON schema and API golden for `/sindri/home` (scenario row, Ui `GoldenContract` slug), `tests/fixtures/home/rows.json` (the §4.2 example as rows), the views migration for `home_rows` and `home_links`, and the ledger migration for the runs "seen" flag (with `ledger schema N/N` in `src/Sindri/aot-smoke/expected.txt`), the `home.*` events (`EventCatalogTests`), `PublicAPI` files, `home_*` in aot-smoke's tools/list, the profile keys and their rows in `docs/csharp/sindri-profile.md` | api | now | the §3.1 fields |
| A1 | first a throwaway script that prints blocks N for each row of the #3082 box state (if under half the rows get N above 0, simplify the rule before building); then `HomeRank` (pure, golden on §4.2, D8), the edge table, `docs/sindri/home.md` (how ranking works; `--explain` points to it), `GET /sindri/home` over the existing sources: needs-you rows, sessions asking, reviews waiting, Linear assigned and mentioned, todos due | api | now, against S0's fixture | S0 |
| A2 | act, snooze, done, undo, mute routes; write-through adapters for forge, Linear, decisions, sessions; early return and revive rules | api | after A1's first read | A1 |
| E1 | the `home-lanes` collector (`lane-recycle`, `agent-limited`, queue `pr-ready-merge`), then the failed-run trigger | engine | now (reads aw-meta; needs no other phase) | the §3.3 fields |
| E2 | the `HomeBrief` job: facts, writer, link check, limit states | engine | after A1's cursor event | A1 |
| U1 | the page on `RowList`: tabs, groups, the New band and held order (D6), peek, keys, states, density setting, the Settings row "Home: rows (new) / panels" (decision 14), all on S0's fixture | ui | now, on the fixture | P0 (stub `RowList` until it lands), S0 |
| U2 | act in place: composers, pickers (snooze, hand to agent), the `Ctrl+Enter` menu, Retry and Discard, Done list | ui | after U1 and A2's routes (fake first) | U1, A2 |
| U3 | brief and Moving sections, empty state, tray count and toasts, first-paint cache (D2) | ui | after U1 | U1, E2 |
| U4 | delete the old views in §12 | ui | after U3, one week of the new page default | U3 |
| F1 | perf: D2, D3, D8 on the 5,000-row store in the real app | perf | after U1 | U1 |
| X1 | the day-without script (D14) and the D1 walk | ux | script now; walk after U1 + E1 | U1, E1 |
| Q1 | qa: D4, D5, D9, D10, D11, D13 | qa | after U2 + A2 | U2, A2 |

P0, D1, S0, A1, E1 and the X1 script start the same day, so six lanes are busy from day one. U1 starts the same day on S0's fixture and a stub `RowList`, so ui is busy too. Nothing waits on a source that does not exist: Messages (#2979) is the one external dependency, and Home ships without `dm` rows.

Each phase updates its docs: `docs/sindri/README.md` (or a new `docs/sindri/home.md`), `docs/csharp/sindri-profile.md`, and the CLAUDE.md Commands entries for the new `sindri home` verbs.

## Open questions

None. The owner settled the four earlier questions on 2026-11-12 and the lanes-collector fields are in §3.3. One thing stays unchecked until a lane is limited: the JSON key of a limited subject's stop in `aw-meta usage --json`. E1 reads it from a real case first. The verdict casing is checked (`Recycle`, PascalCase).

### Review round 1 (autoplan, 5 lenses)

Verdicts: product NEEDS_WORK, architecture NEEDS_WORK, design NEEDS_WORK, devex NEEDS_WORK, security NEEDS_WORK. Nothing CRITICAL. Applied: the §4.2 order fix (#3099 blocks 2), the lane `item-of` source, the false "You're done." on a stale source, the PR head sha on Merge and Approve (016), the rollout key `home.page`, `Park` and `Recipe` rows, the run-store query, `act` argument shapes, D3 and the frame numbers, and the code facts. Left, one line each:

- Product C1 (ship in two cuts): the phases already run in parallel and the owner asked for the whole page; a cut line stays a lane-lead call at U2.
- Product C3 (baseline of opens of other apps): X1 records the baseline in its first week, before U3 flips the default.
- Product C4 (a silent lane row): the top row of a day-start is seen on open, and the lane's toast would fire on every recycle mark; kept silent on purpose.
- Product C5 (defer the model brief): E2 ships facts first and the writer second; the table in §6.1 already covers the facts-only case.
- Arch F5 (`t` in the example): fixed in the example; the lane row's primary is "Copy digest request" (round 2).
- Arch, `aw-meta` key names: unchanged, E1 reads them from a real case first.
- Arch, `Tokens.axaml:229-233`: kept; line 229 is the comment above the four tokens.
- Design F9 (hover buttons on the cursor row), F10 (`b` twice): hover buttons show on hover and on the cursor row; the `.` panel key for the brief is `u`, not `b`.
- Devex, the new `Aw.Sindri.Core.Home` namespace: the gate list per phase is the lane lead's, from the team memory rules.
- Devex, migration number: kept "next free at final merge"; S0 reserves a number at start and renumbers on conflict.
- Security L3 (mute): mute stops the toast and the early return only; the row stays in Needs you.

### Review round 2 (autoplan, 5 lenses)

Verdicts: product PASS (SHIP), architecture NEEDS_WORK, design NEEDS_WORK, devex NEEDS_WORK, security NEEDS_WORK. Nothing CRITICAL. Code facts checked at `origin/main`. Applied: "Ask for a digest" replaced by "Copy digest request" (`lane-send` refuses meta and Claude lanes, AW-META-017), the `g` key against the shell's G-chord, an act settles its own row only (so `e` runs don't re-sort) with a sticky New header, returning rows enter through the New band, `home.*` events left out of session ledger reads, the `merge-run` head-sha check, `home.page` kept for one release after U4, the lanes collector's exit-1 rule, and a source clear ends a Done episode. Left, one line each:

- Product C1 (add D14 to the U4 gate): X1 already records D14; the U4 lane lead may add it, the one-week rule stays.
- Product C2 (digest asked twice): the copy sends nothing, so a second press only copies again.
- Product C3 (rank stats): D7's `home rank --explain` is the first read; a stats verb waits on real use.
- Product C4 (flaky CI revives Done): kept, since the owner's rule is "addressed activity returns"; a revive cap is a first-week tuning call.
- Product C5 (brief cursor past unseen lines): the cursor moves only after 5 s visible, and unpainted lines are a U3 test.
- Arch, lanes collector gaps (team key, `since` for queue entries, `from == sessionName` join, tick interval, `deps.yaml` edge): E1 writes them down from the real box, as §3.3 already says.
- Arch, `closes` edge needs a GitHub issue read and a trusted-author key: A1's throwaway script finds out first.
- Arch/devex, retried sends deliver twice: only the session reply has no read-back; its retry shows "may have landed" and asks.
- Arch, two owners for the snooze timer: the app times the wake, the store holds the due time.
- Design, reply drafts, mute look and unmute, Snoozed toggle and Done-list place, Done-list Restore, Ctrl+Z in a text field: the U2 draw (D1) settles each; Ctrl+Z is text undo while a field has focus.
- Design, wake toast against D10: the start-up wake toast is one toast per start and D10 counts it as one.
- Devex, parity holes (Retry, Discard, unsnooze, unmute, Restore), error 004 split, exit codes of 008 and 013, soak owner, usage text owner: S0's schema lists the verbs and codes; X1 owns the soak.
- Security R2-M1 to M3 (draft scope and label, repo-less definition, peek markdown and bidi): S0 and U2 write them down; a draft shows "Draft from <session>".
- Nits (stale shas, line range, frame counts, snooze edge presets, tray total): written down, not applied.

### Review round 3 (autoplan, 5 lenses)

Verdicts: product PASS (SHIP), architecture NEEDS_WORK, design NEEDS_WORK, devex PASS, security PASS. Nothing CRITICAL. Code facts checked at `origin/main`. Lead decision: `g` is no longer a Home key, because the shell's G-chord owns bare `g` on every page and a key that folds on a header but starts the chord on a row blocks g-chords from a header; fold is Left and Right (tree convention). Applied: fold chevrons on every header and header click (design MUST), screen-reader state and palette ids, the fold edge rules (§7.4), the views versus ledger migration trail and the table reasons, the session filter on the three GET routes, the repo-less hand-to-agent target, `G D` and `G T`, and chord-before-list. Left, one line each:

- Arch F3 (tag about 40 numbers `owner decision` or `agent default`): the numbers carry their source in the prose where the owner set them; a full tag pass would add about 30 lines for no behavior change, so U1 tags the profile-key defaults in `docs/csharp/sindri-profile.md` as it writes them.
- Arch F3, five items to move to open questions (CI-red revive, notify list, rank weights, brief cap of 9, the one-week U4 gate): all five are agent defaults tunable by profile key or first-week tuning (round 2 answers); none blocks the build.
- Design D8 (cursor row leaves, next row in another group): the cursor stays in its group while that group has rows; a U3 test.
- Security R3-N1 (head-sha check and `merge-run` are two steps): accepted; a push in between merges the new head, as a manual merge would.
- Nits (stale header sha and "3 rounds" line, token line range 230-233, header contrast states, `doctor` last-error through `Aw.Redaction`, footer chord hint wording, "bare `g`" in the Context page's key list): written down, not applied.

### Frames review (design, comment 6820038112)

The lead decided design's 22 frame guesses. Where the spec and the #2978 grammar disagreed, the grammar won. One line each:

1. The mono id moves left of the title; the object column says lane, repo or dm (§7.1 frame and text).
2. Needs you rows show no waits-on "you"; the column shows only where it varies (Moving).
3. Group headers show the row count only, never "blocks N".
4. The brief is a summary line plus 2 fact lines, then "b N more lines" (§6.1).
5. The Todo line is a 28 px non-group line with an empty-circle glyph, no chevron; Left and Right do nothing on it.
6. Snooze: digits 1 to 4 pick presets while the field is empty and unfocused; 5 or a click focuses the field, then digits type (§5.3).
7. The hand-to-agent picker has a filter field, no chips, `Enter` picks; an asking session is disabled with "asking you".
8. While a menu or picker is open the footer swaps to "↑/↓ move · Enter pick · Esc cancel" (§7.4).
9. The footer shrinks at 1000 px to `h snooze · t hand · e done · ? all keys`; at 1440 it is `h snooze · t hand to agent · e done · R re-rank` plus "Done list".
10. The Done list opens from the footer text, the `?` sheet and `home.done-list`; no bare key.
11. A split filters New, Needs you and Moving to its kind; the brief and the Todo line show on All only.
12. Lane-recycle rows use the needs-you diamond, ready-to-merge rows the PR-open ring.
13. No "Health ok" chip in the page header; the shell pill is the one status mark.
14. A stale or error line is one 32 px `FailedReadLine`-shaped state line; stale rows get a muted title and age.
15. "You're done." means no stale line is showing.
16. Text scale 1.3 scales text and row heights only; column widths, insets and the peek stay in px.
17. A truncated title shows a plain tooltip with the full title.
18. A row back from snooze says "back" in the age cell; the reason goes in the sub-line.
19. Tab counts are computed from row kinds; All = New + Needs you.
20. In a PR peek, `Ctrl+Enter` opens the submit menu with Merge preselected; `Enter` is Open.
21. Guesses 21 and 22 need no spec change: the grammar adds Down and Stale glyph aliases (`.g.fail`, `.g.parked`).

### Reshoot review (design, comment 6820192936)

The lead checked the reshoot (assets 743f55d31a) against "Frames review". It matches. The spec now follows four details the frames got right:

1. A lane, session or DM row has no mono id. Its title starts in the id column, and the object column names it (§7.1).
2. Folded, the Moving header names its kinds ("Moving: 4 agents, 2 CI runs"), as the fold rule already said. The §7.1 frame said "Moving 6" and is fixed.
3. At 1440 the footer keys carry their words. The short form is for 1000 px only.
4. PR rows use the PR-open ring in the frame, as decision 12 says.

Design's three calls stand: shortened refs, CI Moving titles without the PR number, and no right-hand text on a folded Needs you header. One copy fix for the UI build: the stale empty state says "cannot", not "can not".

### UX pass (meta, at dcf296fdeb)

PASS after four text fixes, all applied: `c` and `x` are not Home keys and leave the footer (§7.4); the §7.1 frame's tab counts match its rows; the Messages tab's "arrive with #2979" line shows only while it is empty and Messages is not shipped (§7.3); the orphan `b` row is in the key table; trying the new page is one Settings row in U1 (decision 14, §15).
