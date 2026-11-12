# Sindri's Home page: one inbox, ranked by what it unblocks

Status: draft (ideas lane, 2026-11-12). Serves #2978 (one app that replaces the others; Home is the one inbox) and builds the "Home" rows of the prior-art ledger (`docs/sindri/prior-art/home.md`, merged in #3011).
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

## 0. Why

The owner's words on #2978: "think about how much or little information density with good design we have in sindri and how we can make that easier or offload it to the home app." Home is where that density goes.

What Home does on main today (read from `origin/main` a4d03ace1c, issues linked; the app was not driven for this spec):

- It is panel cards. `Views/Shell/HomePage.axaml` stacks a setup line, a Todo panel, a Needs you list (left column, `3*`), a Running / With you / Queued column (right, `2*`) and a health strip. `ViewModels/HomeFold.cs` caps the Needs you list at 5 rows and the strip at 3, so 8 rows sit above the fold. Design's frame on #2978 shows 22 rows.
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
| D3 | Rows per screen at compact. | 1440x900, peek closed, brief at its default 3 lines: at least 18 Needs you rows visible. At comfortable: at least 14. With peek open: the same rows, 3 facts each. |
| D4 | Key to visible change under 50 ms; source write lands within 2 s. | Keydown to repaint of the optimistic row. Act request received to the source confirming, p95 over 50 writes per source on the fake adapters. |
| D5 | A failed write is visible and retryable. | For each action kind: the fake source refuses, the row shows the reason and Retry within 1 frame, and Retry lands the write once. |
| D6 | Stable order. | A property test over 10,000 random event streams: between two settle points (§4.3) no row changes index except by removal, and every new row appears only in the New band. |
| D7 | Ranking is deterministic and explained. | The §4.2 worked example is a golden test. `sindri home rank --explain <row>` prints blocks, weight, age and why the row sits above the next one. |
| D8 | Re-rank cost. | 10,000 rows scored in under 16 ms (ledger 2). |
| D9 | Snooze. | Wake within 1 s of the due time with the app in the tray; early return within 5 s of a reply addressed to the owner; Undo within 5 s. |
| D10 | Notifications. | Over 50 injected events: exactly one toast per notifying row per episode, zero for non-notifying kinds, tray count equals notifying rows not yet seen. |
| D11 | Done means done. | A seeded store with no Needs you rows shows "You're done." and the brief; one new addressed event brings exactly one row back. |
| D12 | Brief. | At most 10 lines; every link resolves to a row or object that exists; plain-fact lines paint under 100 ms; with the writer off or model-limited the section says why (§6.1). |
| D13 | Agent parity. | A test enumerates the `home.*` action ids; each has a CLI verb and an MCP tool, and each tool's result fences repo text. |
| D14 | **Day without the other apps.** ux runs a scripted day with GitHub notifications, Linear's inbox and the lane terminal closed. Each time the owner opens one to find out what is waiting is an issue, `Part of #2978`. Slack joins when Messages (#2979) ships. | Count of such opens: 0 for the shipped sources. |

## 2. Decisions

1. **Home is one list with three groups and splits as tabs.** Groups: Needs you, Moving, Since you last looked (#2978). Splits are saved filters on the same list: All, Reviews, Messages, Work, Agents (ledger 1). A source page is for working; Home is for deciding what to work on.
2. **Rank by "blocks N".** N counts the open objects that cannot move until this row is done (§4.1). Ties go to a reason weight, then age, then id. The rule is readable and the weights are profile keys (ledger 2, take 6). The score is ours; the ledger found no prior art for it.
3. **Order holds still.** Rows move only at a settle point (§4.3). New rows wait in a marked "New" band at the top. This is the Agents walk's expected behaviour 3 (#3082), applied to all of Home.
4. **Sessions and lanes are rows.** A lane at its recycle mark, a session asking a question and a PR ready in the merge queue are Needs you rows with the same grammar as a review request (ledger 12; #3082).
5. **Acting follows the grammar's write rule.** Meta's rule on #2978: a bare letter never commits a write, and may differ per surface only when it opens something cancellable. So reply (`r`), snooze (`h`) and hand to agent (`g`) open a composer or a picker; send, approve, merge and assign go through the `Ctrl+Enter` menu or the `.` panel. This replaces ledger 3's bare `a`. One carve-out is open for the owner (§5.4, Q1): `e` Done writes only Sindri's own state and has a 5 s Undo.
6. **One Done, one snooze, one mute, for every row kind** (ledger 4, 5, 10). They are Sindri-local state; they never write to Slack, Linear or GitHub. Source writes (reply, approve, merge, state change) are separate actions with write-through (§5.2).
7. **Priority-only notifications.** A toast and the tray count come from a short kind list, once per row (§6.3). Moving and the brief never notify.
8. **The brief is facts first.** Plain-fact lines come from the ledger with no model. A model writes the summary line and the order, and a failed or limited model costs only that (§6.1).
9. **Density is 32 px (compact, default) and 40 px (comfortable).** These are design's grammar numbers on #2978. The ledger's 28 / 36 are replaced. `Tokens.axaml:229-233` carries 28 / 32 / 36 / 40 today; design's token PR (P0) sets the names.
10. **Home reads one local store.** The first page of `GET /sindri/home` and a cached copy of it paint first; nothing on the first-paint path calls a source.
11. **Agents get the same verbs.** Every action on the page has a CLI verb and an MCP tool; a session reads Home but never presses owner buttons (§9).

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

Home rows are a view. Ledger events (`home.row.raised`, `.changed`, `.cleared`, `.seen`, `.snoozed`, `.woke`, `.done`, `.revived`, `.muted`; `home.act.sent`, `.landed`, `.failed`; `home.brief.written`; `home.cursor.moved`) fill a `home_rows` table in `views.db`. `sindri views rebuild` replays them. A collector per source raises and clears rows; none writes a row directly.

### 3.2 Needs you: the sources and their exact triggers

A row is in Needs you while its trigger holds, and leaves when the trigger stops holding or the owner does Done or Snooze. "The owner" is the profile's `review.login` for GitHub and the connected Linear user for Linear.

| Kind | Source | Trigger | Exists on main? |
|---|---|---|---|
| `decision` | Sindri | a pending decision (`NeedsYouKind.Decision`) | yes |
| `question` | Sindri | a graph `ask_owner` node asked and is unanswered (`NeedsYouKind.Question`) | yes |
| `run-failed` | Sindri | a workflow run the owner started, or one whose workflow notifies the owner, ended `failed` and has not been seen | partly: service failures are rows; run failures are not |
| `system` | Sindri | setup step open, profile drifted, connection token stale, graph note posted (`Setup`, `Profile`, `Connection`, `WorkflowNotify`, `Failure`) | yes |
| `agent-asking` | session collector | `GET /sindri/sessions` row with `state: asking` (`SessionAskingView` gives reason, tool, since) | yes |
| `agent-limited` | aw-meta, Sindri | a lane that `aw-meta status` shows usage-limited, or a Sindri agent run paused on a model limit | no |
| `lane-recycle` | aw-meta | `aw-meta lanes-context` gives a lane the verdict `recycle`: context at or over `context.recycleAt` (the meta lane included) | no |
| `pr-ready-merge` | forge, aw-meta | an open, non-draft PR of the owner with required checks passing and nothing blocking its merge, **or** a PR in the aw-meta merge queue (a lane sent `ready #N <sha>`, not merged, ended or dropped). One row per PR even when both hold. | queue: no; Reviews list has "Yours": partly |
| `review-requested` | forge | `ReviewWaitingKind.Requested`: a review was requested from the owner and none is submitted | yes (Reviews list "Waiting on you") |
| `review-new-commits` | forge | `ReviewWaitingKind.NewCommits`: the owner reviewed and the PR has newer commits | yes |
| `mention` | Linear, forge, Messages | a comment names the owner and the owner wrote nothing after it in that thread | Linear: `LinearSinceItem` kind `mentioned`; GitHub and Slack: no |
| `dm` | Messages (#2979) | the last message in a direct message thread is not the owner's and is unanswered | no: waits on Messages |
| `issue-assigned` | Linear | the owner became assignee at or after 00:00 local today and the issue is still unstarted (Linear state type backlog, unstarted or triage) | yes: `LinearSinceItem` kind `assigned`; the "today" cut is new |
| `todo-due` | Todo | a todo due today or overdue and open | yes: Todo store |

Reason words on the why chip: "asking", "limited", "at 210k", "ready", "review requested", "new commits", "mention", "DM", "failed", "assigned", "due", "setup".

Not on Home: "idle with work owed" and "slot wait over 30 min" from #3082's Agents ranking. They are guesses about what is owed, and Home shows no guesses. They stay on the Agents page (Q3).

Not on Home: the Queue's `queued` group. Its `running` and `withYou` groups go to Moving (§6.2).

### 3.3 Where the data comes from

| Source | Read through | New work |
|---|---|---|
| decisions, questions, system | `GET /sindri/needs-you` (projection `NeedsYouProjection`) | none; the projection becomes one row producer |
| sessions | `GET /sindri/sessions` (`ListSessionsEndpoint`) | none for asking; context size is not on `SessionRowView` today |
| lanes and merge queue | `aw-meta lanes-context` and `aw-meta status`, read-only, on a service tick | a `home-lanes` service in the Sindri host; the exact JSON fields are unchecked (Q5) |
| reviews, PR checks | `GET /sindri/reviews` (`ListReviewsResponse`, `ReviewWaitingKind`), `Core/Forge` (`PullChecks`, `PullMerge`) | the ready-to-merge trigger |
| Linear | `GET /sindri/linear/since` (`LinearSinceResponse`) | the "today" cut; mention-awaiting-reply |
| runs | `GET /sindri/workflows/{slug}/runs` | the failed-and-unseen trigger |
| todos | `GET /sindri/todos` | none |
| Slack, DMs | Messages store (#2979) | waits on that epic; the row kinds are defined here so it only has to raise them |

## 4. Ranking

### 4.1 Blocks N, exactly

A row is about one **object** O (a session, PR, issue, run, thread, lane). N is the count of **dependents** of O:

1. A dependent is an open object that cannot move until the owner acts on O. Merged, closed, cancelled and done objects do not count. A snoozed object does count.
2. The edges, the closed list that make one object wait on another:
   - `item-of`: the objects a lane owns wait on its session. A session S has as dependents the open issues assigned to its lane and the open PRs it authored. The meta lane also has every PR in the merge queue, because it merges them.
   - `closes`: an issue waits on a PR whose body says `Fixes` or `Closes` for it. "Part of" is not an edge.
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
- meta (205k) and ideas (210k) are at the recycle mark. The merge queue holds #3099 (since 08:02) and #3101 (since 08:40). #3101 waits on #3099 (`merge-order`) and closes issue #3040.
- ideas authored PR #3078 and holds issue #3011.
- A review of PR #3120 is requested of the owner (since 07:30). #3120 closes issue #3055. PR #3125 is stacked on it.
- Sam's DM waits (since 09:02) and is linked to no object.

| Row | Dependents counted | Blocks | Weight | Since |
|---|---|---|---|---|
| meta at 205k | #3099, #3101 (`item-of`), then #3040 (`closes`, hop 2) | 3 | 4 | 08:55 |
| review #3120 | #3055 (`closes`), #3125 (`stacked`) | 2 | 5 | 07:30 |
| ideas at 210k | #3078, #3011 (`item-of`) | 2 | 4 | 08:50 |
| ready #3099 | #3101 (`merge-order`) | 1 | 5 | 08:02 |
| ready #3101 | #3040 (`closes`) | 1 | 5 | 08:40 |
| DM from Sam | none | 0 | 4 | 09:02 |

Order: meta (3 blocks), then review #3120 before ideas (both 2; weight 5 beats 4), then #3099 before #3101 (both 1 and weight 5; 08:02 is older), then the DM (0). Row 1 names a session, which is D1.

At 09:12 the qa session asks to run `rm -rf build/` (blocks 1, weight 6, its issue #3102). It lands in the New band, not in the list (§4.3). The owner types the digest request on row 1 at 09:14 (`g`, hand to agent). That is a settle point: meta's row is gone once meta recycles, qa's row joins the order at blocks 1, weight 6, which is above #3099, and the list reads review #3120, ideas, qa, #3099, #3101, DM. Between 09:10 and 09:14 no row changed index.

### 4.3 Stable order and the New band

The server always returns the current ranked order. The app holds its own order and changes it only at a **settle point**:

1. The owner acts on a row: Done, Snooze, an action, Hand to agent, Mute.
2. The owner presses `R` (re-rank now).
3. Home opens, or the split tab changes.

Between settle points:
- A row that leaves (the source cleared it, the owner finished it elsewhere) is removed in place. Rows below move up one; the cursor stays on its row, or on the next row when its own row left.
- A row whose blocks, why chip or age changes updates that text in place and keeps its position.
- A new row appears in the **New band**: a 28 px group header "New (2)" at the top of Needs you with the new rows beneath it in their own rank order. New rows are marked with the word "new" in the age column, in ink, never by colour alone. The band is above the cursor, so the app scrolls to keep the cursor row at the same screen y.
- At the next settle point the New rows take their ranked place and the mark goes.

The bad case this stops: a lane's row jumps to the top as the owner presses Enter, and the Enter opens the wrong row. This is #3082's expected behaviour 3 ("the order holds still between refreshes; a row moves only when its group changes"), applied to every group.

## 5. Acting in place

### 5.1 Actions per row kind

Keys follow §7.4. `Ctrl+Enter` opens the row's submit menu with the primary action preselected; `Enter` there commits it. The row's hover buttons (2 to 3, replacing the right end of the row, per design's grammar) commit on click.

| Kind | Primary (first in the menu) | Also | Writes to |
|---|---|---|---|
| `decision`, `question` | Answer with the shown option | Decline, Copy command, Open | Sindri (`POST /sindri/decisions/{id}`) |
| `agent-asking` | Reply (text to the session, #3022) | Jump to terminal `o` (#3023), Hand to agent n/a | the session |
| `agent-limited` | Open session | Resume (when the pause is Sindri's) | Sindri, aw-meta |
| `lane-recycle` | Ask for a digest (sends the standard line with `aw-meta lane-send`) | Jump to terminal | aw-meta |
| `review-requested`, `review-new-commits` | Open review (Enter, full) | Approve, Comment, Request changes in the `Ctrl+Enter` menu | GitHub, through `Core/Forge` (`PullReview`) |
| `pr-ready-merge` | Merge (merge method from the repo's setting; in a meta team, `aw-meta merge-run`, which runs `merge-check` first) | Open, Hand to agent | GitHub, aw-meta |
| `mention`, `dm` | Reply (composer in the peek) | Hand to agent (an agent writes a draft into the composer; the owner sends) | the source of the thread |
| `run-failed` | Retry the run | Open run, Hand to agent | Sindri (`workflow_run`) |
| `issue-assigned` | Start (state to Started) | Change state or priority, Hand to agent, Comment | Linear (`ChangeLinearIssue`, `CommentLinearIssue`) |
| `todo-due` | Check off | Change due date | Sindri (todo) |
| `system` | the row's existing primary action | Open | Sindri |

On every kind: Snooze `h`, Done `e`, Hand to agent `g` (except where noted), Mute (in `.`), Open `Enter`.

Hand to agent opens a small picker, as the Context page's Hand to agent does: a new session on a provider, or one of the repo's running sessions. It shows what goes along ("PR #3120, 2 linked issues, the thread, about 4k tokens") and leaves a "Handed to <session>" line with a link. The row stays in Needs you until its trigger stops holding. The brief to the agent holds the row's links and its peek text, fenced as untrusted.

### 5.2 Write-through

Every action that writes to a source of record (a reply, an approval, a merge, a state change) follows one pipeline. This is contract item 2 on #2978.

1. **Key to paint, under 50 ms.** The row changes in place (reply: the composer closes and the message shows as "sending"; merge: the glyph becomes the merged disc, waits-on says "merging"). The app has written nothing yet.
2. **Request.** `POST /sindri/home/rows/{id}/act` with `{action, version, actId, args}`. `actId` is a client ulid (`act_<ulid>`).
3. **Source write.** The server calls the source adapter and records `home.act.sent`. The adapter checks first whether the write already landed (a merge reads the PR state; a reply looks for the owner's last comment with the same body hash), so a replay is safe. It records `home.act.landed` when the source confirms.
4. **Within 2 s** the row shows the source's answer (D4). After 2 s without one, the row says "still sending" in the waits-on column and stays as it is. It does not roll back.
5. **Failure** (the source refuses, or 15 s pass): the row returns to its old state and shows "failed: <reason>" in the waits-on column in the attention colour, with **Retry** (`.` then `r`, or the button) and **Discard**. Retry sends the same `actId`. Discard drops the pending change. The reason comes from the adapter ("GitHub refused: required check `build` is red", "Linear: token expired, reconnect") and a Connect button replaces Retry when the connection is stale.
6. **Undo** exists only for Sindri-local state (Done, Snooze, Mute): 5 s. A source write has no Undo; the confirm is the Ctrl+Enter menu.

A second action on a row that is still sending is refused with "wait for the first to land" (code 004).

### 5.3 Snooze

`h` opens a picker (cancellable). Choices, with the keys `1` to `5` while it is open:

1. Later today: 17:00 local, or 3 hours from now when it is after 14:00.
2. Tomorrow 9:00.
3. Monday 9:00: the next Monday after today.
4. Next week: the same weekday next week, 9:00.
5. A typed date or time: `fri 14:00`, `nov 20`, `in 2h`.

Rules:
- The row leaves Home and shows `back Mon 9:00` under the "Snoozed (n)" filter (off by default, a toggle in the header).
- **It returns early** when something new is addressed to the owner on that object: a reply in the thread, a review request, CI red on the PR, the session asking again. The row says "back early: new reply" for its first view.
- The wake is a local timer in the app (within 1 s of due, D9). When the app was not running at the due time, every due row wakes at the next start, and one toast says how many.
- Snooze works on any kind, including a todo and an agent that is asking. It shares one store with Messages' "follow up if no reply" (#3020).
- Longest snooze: 365 days (code 008). Undo lasts 5 s.
- Taken from the Linear `H` flow (ledger 5, take 3). The presets are #3030's, not the ledger's.

### 5.4 Done

`e` marks the row done, whatever its kind (ledger 4, take 4; #3031). The row leaves Home in one frame. Undo lasts 5 s.

- A done row **returns on its own** only when new activity is addressed to the owner on that object: a mention, a review request, a reply in the owner's thread, CI red on the owner's PR, the session asking again. Other activity changes nothing. The row says "back: new reply".
- Done never writes to a source. A merged PR's row clears itself; a replied thread's row clears when the reply lands.
- The **Done list**, reached from the footer, holds the last 7 days of done rows (`home.done.keepDays`, whole days 1 to 90, default 7). #3031 sets 7; the ledger's 90 days is GitHub's number.
- **Mark all seen** stays as the bulk form of "seen" (the row stops looking unread and the tray count drops); it does not hide rows and has the existing Undo.
- Open (Q1): meta's rule is that a bare letter never commits a write. Done writes only Sindri's own state and has an Undo, so this spec keeps `e` bare. If the owner reads the rule strictly, Done moves to `.` then `e`; the action id `home.done` and every other part of this spec are unchanged.

### 5.5 Definition of done: the empty Home

Home is empty when Needs you holds no row in state `open`. Snoozed, done and muted rows do not count; a row whose write failed does count.

An empty Home shows, in the body: "You're done." (20 px, ink), one line under it ("3 snoozed, the next back Mon 9:00" when there are snoozed rows), and the brief if it has lines. Moving is not drawn (ledger 4: only the brief remains). The tray count is 0.

## 6. The brief, Moving and notifications

### 6.1 Since you last looked

The cursor is a ledger event number, `home.cursor.moved`. It moves to the ledger head when Home closes after it was visible for at least 5 s, so a glance does not erase the brief. Lines the owner has seen on screen drop at the next refresh (Slack recap rule, ledger 9).

Two writers:
1. **Facts, by Sindri, no model.** A job reads the ledger from the cursor and writes one line per change that matters: a PR merged, CI went red or green on a PR of the owner's, a run ended, a lane recycled, an issue changed state. A line is a sentence plus at most 2 links (deep links to a row or an object). Example: "PR #3078 merged; CI red on main since 09:10." Painted at first paint, under 100 ms (D12).
2. **Summary, by a model.** A Sindri job (kind `HomeBrief`) runs when Home opens with events since the cursor and the last brief is older than 15 min, or when the owner presses `.` then `b`. It reads only typed facts (kind, title, state change, ids) after `Aw.Redaction`, never message bodies. It writes the order of the lines and one summary line at the top. Every link in its output must resolve to a row or object id from its input; a line with a link that does not resolve falls back to its plain-fact form.

The cap is 10 lines. When there are more than 10 facts, the first 9 by (blocks, recency) show, then "and 14 more: see Moving, Work". The minimum is 0 lines; with 0 the section is the one word "Nothing changed." Each line is an ordinary row: it can be peeked, done and snoozed.

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

Moving is **collapsed** by default: a 28 px header "Moving: 4 agents, 2 CI runs" with a chevron. Expanded it shows up to 8 lines, oldest start first, then "Show all N in Agents". The expanded state is remembered per user. A line has no actions, only peek and open. When its state changes to something that needs the owner (CI red, a session asking), the object leaves Moving and a row enters Needs you through the New band.

### 6.3 Notifications

What notifies: a row **entering** Needs you with one of these kinds: `agent-asking`, `decision`, `question`, `review-requested`, `run-failed`, `mention`, `dm`. One OS toast per row per episode (an episode ends at Done, a clear by the source, or a snooze that wakes). Clicking the toast opens Home with that row's peek open (as #3026 does for agents).

What never notifies: `pr-ready-merge`, `review-new-commits`, `issue-assigned`, `todo-due`, `lane-recycle`, `agent-limited`, `system` (a stale connection is the one exception: one toast, since every row of that source goes stale), Moving, the brief, a row that comes back from Done (it notifies only if its kind is in the list above), a row inside a muted object.

The tray count is the number of open rows of the notifying kinds. It is the one count in the app: #3026's agent badge is the `agent-asking` part of it, not a second badge. A mute applies to the thread, the PR and the issue about it together (ledger 10) and ends only on a direct ping: a mention naming the owner or a review request to the owner.

Toast and tray run in the Sindri app, from local state, with no push service (ledger 10). When the app is closed to the tray, they work; when it is not running, nothing notifies.

## 7. The page

### 7.1 Layout

Design's frame on #2978 (comment 6817965291) is the reference; every number is `grammar.css` / `Tokens.axaml`. At 1440x900:

```
┌ top bar 40 ───────────────────────────────────────────────────────────────────────────────┐
│ Home   [All 9] [Reviews 3] [Messages 0] [Work 4] [Agents 2]      / filter      Health ok  │ page header 48
├ New (1) ──────────────────────────────────────────────────────────────────────────────────┤ group header 28
│ ◇ qa is asking: run rm -rf build/     asking     session  qa          new   you            │ row 32
├ Needs you 6 ──────────────────────────────────────────────────────────────────────────────┤
│ ◇ meta: at 205k, recycle from a digest   blocks 3   lane  meta        2h    you           │
│ ◇ Review requested: System grid           blocks 2   PR #3120 design  1h40  you           │
│ ◇ ideas: at 210k, recycle from a digest   blocks 2   lane  ideas      20m   you           │
│ ...                                                                                       │
├ Moving: 4 agents, 2 CI runs ▸ ────────────────────────────────────────────────────────────┤
├ Since you last looked 6 ──────────────────────────────────────────────────────────────────┤
│ PR #3078 merged; CI red on main since 09:10.                                              │
├ j/k move · Space peek · Enter open · h snooze · g hand to agent · e done · . actions · ? ──┤ footer 28
└───────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Frame:** top bar 40, page header 48, body 784, footer 28. Group headers 28, sentence case, 12/500, count in ink-3. Side inset 24.
- **Row (32 px compact, 40 comfortable):** `[glyph 16] [title, flex, min 200] [why chip 104] [object 104] [from 136] [age 44, right-aligned] [waits on 112]`, 12 px gap. The why chip carries "blocks 3" or the reason word. The object fact is the mono ref (`PR #3120`, `ENG-412`, `lane meta`). The from fact is a person or session name. Title is the only 13 px ink text; unread weighs 500, read is ink-muted. Waits on `you` is bold ink, the one loud fact.
- **Width check:** the fixed part is 636 px, so the title gets `list width - 636`: 364 px at 1000, 804 at 1440 with the peek closed. Under 840 px of list width the third fact drops; under 688 the second drops. The title minimum of 200 never shrinks (design's rule on #2978: facts end in an ellipsis, never a mid-word clip).
- **Rows per screen (D3):** body 784 minus 8 padding minus 3 group headers (84) minus the brief's 3 lines (96) leaves 596 px, which is 18 rows at 32 px and 14 at 40.
- **Splits** are the saved filters: All; Reviews (`review-requested`, `review-new-commits`, `pr-ready-merge`); Messages (`mention`, `dm`); Work (`issue-assigned`, `todo-due`, `run-failed`, `decision`, `question`); Agents (`agent-asking`, `agent-limited`, `lane-recycle`, plus Moving's agent lines). Each tab shows its open count. There is no "Other" tab: a row that is not a Needs you kind is not on Home (Linear's Priority / Other split is covered by Needs you versus the source pages).
- **Header right:** `/` filter box and one health chip that replaces today's health strip and opens the Health page.
- **Glyphs** come from design's set (diamond for needs you, x for failed, arc for an agent running, ring with dot for a PR, bubble for a message, dot for a mention); lane and merge-queue glyphs are new and drawn in D1 (§15).

### 7.2 Peek (Space) and full (Enter)

Peek is a 440 px pane on the right (the list narrows to 1000 at 1440). `j/k` moves the peek with the cursor. It paints within 50 ms of `Space`, from the local store, with no spinner. Header 48 (title, ref, open-full, close), fields as two columns of 28 px rows, a body, 2 to 3 actions at the bottom. Fields on every kind: Source, Waiting since, Blocks (up to 5 dependents as links; "and 4 more"), From, Linked (the other objects).

| Kind | Body |
|---|---|
| `review-*`, `pr-ready-merge` | the diff summary (files, +/-), CI checks with state, the linked Slack thread's last 3 messages when Messages exists, the closing issues |
| `mention`, `dm` | the last 5 messages, an inline reply composer |
| `agent-asking` | the last ~30 terminal lines, the pending question, a reply box (#3022); a session Sindri cannot write to says why ("started outside Sindri: copy the reply") |
| `lane-recycle` | context size and threshold, when the last digest was written, what the lane owns (the `item-of` list) |
| `decision`, `question` | the question and its options |
| `issue-assigned` | Linear fields and linked PRs |

**Full** (`Enter`) goes to the working surface: the review for a PR, the Agents sheet for a session, `DecisionSheet` for a decision, the Linear issue, the run page. `Esc` returns to Home with the cursor on the same row.

### 7.3 States

Every error row has a button that fixes it.

| State | What shows |
|---|---|
| first open, no store yet | the page header and footer paint at once; the body shows 6 skeleton rows for at most 400 ms, then the rows or the empty state |
| loading from a slow serve | the cached first page paints first (D2); a quiet header line "Updating…" is a polite live region; no spinner over rows |
| serve not running | the cached rows paint and a header line says "Sindri isn't running · showing 09:02" with **Start Sindri**; actions are disabled with the same reason |
| a source is stale (not read in 5 min) | the header names it ("GitHub last read 12 min ago") with **Refresh**; rows from it carry a muted age |
| a source is not connected | no rows from it; one `system` row "Connect GitHub to see reviews" with **Connect** |
| a failed read of Home | "Couldn't read Home" with **Retry** and the log path; never the empty "You're done." (fixes #2921's skeleton) |
| a row action failed | the row shows "failed: <reason>" with **Retry** and **Discard** (§5.2) |
| connection stale on an action | the row's Retry becomes **Connect** |
| write still sending | "still sending" in waits-on after 2 s |
| a row's version changed under an edit (003) | the edit is not applied; the row shows "Changed while you were editing" and **Reload** (keeps the cursor on the row) |
| New band has rows | the "New (n)" header; the page title shows "Home (n new)" in the window title for screen readers |
| a snoozed row wakes early | the row shows "back early: new reply" until first seen |
| Needs you empty | §5.5 |
| brief states | §6.1 table |
| writer paused or off | §6.1 |
| model for the brief is limited | **Resume**, §6.1 |

### 7.4 Keyboard and focus

The grammar keys are the same on every list (#2978): `j/k` and arrows move, `Home`/`End`, `/` filters, `Space` peeks, `Enter` opens, `Esc` goes back one layer, `.` opens the row's action panel (each action shows its key), `?` shows the keys of this surface, `Ctrl+K` is the palette. Home adds these, after the divider in the footer:

| Key | Does | Writes? |
|---|---|---|
| `h` | opens the snooze picker (§5.3) | no, until a choice is made; then Sindri-local |
| `g` | opens the hand-to-agent picker | no, until Send |
| `r` | opens the reply composer in the peek (kinds with a thread) | no, until `Ctrl+Enter` sends |
| `e` | Done, with Undo (Q1) | Sindri-local |
| `Ctrl+Enter` | opens the submit menu with the primary preselected; `Enter` commits | the source, on commit |
| `R` | re-rank now (a settle point) | no |
| `[` / `]`, `1` to `5` | previous / next split; jump to a split | no |
| `m` | expand or collapse Moving | no |
| `b` | expand the brief to all its lines | no |

Tab keeps its normal job: it moves focus between the filter box, the list, the peek and the footer. Splits do not use Tab because Tab has to stay a focus key (the ledger's Tab binding is a Superhuman snippet and is in "Not solid"). Letters work only while focus is in the list or the peek and not in a text field, as `RowKeys` already guards (`Views/Controls/RowKeys.cs`, #2952).

Every action also has a menu item and a palette entry under the same id: `home.open`, `home.peek`, `home.reply`, `home.send`, `home.approve`, `home.comment`, `home.request-changes`, `home.merge`, `home.start`, `home.retry-run`, `home.answer`, `home.digest`, `home.snooze`, `home.unsnooze`, `home.done`, `home.undo`, `home.mute`, `home.hand`, `home.retry`, `home.discard`, `home.rank`, `home.brief`, `home.density`. A button with no palette entry is a bug (ledger 11).

Screen readers: the list is a list of groups of rows; a row reads "needs you, meta, at 205k, recycle from a digest, blocks 3, waits on you, 2 hours". The New band is announced once as "1 new row". Reduced motion turns the toast slide and the row removal into an instant change.

### 7.5 Visual bar

One accent. Status colour only on status marks (the glyph, the failed reason). Every text 4.5:1 on its fill, every meaningful mark 3:1, held by a contrast check in the Frames suite over every row state (rest, hover, cursor, selected, new, failed, snoozed). Type scale and spacing from the tokens. Nothing in a row wraps. Inspora is the reference (#2978, owner 2026-11-11).

## 8. API and engine requirements

Routes, all under `/sindri/home`, named by S0 (§15):

| Route | Does |
|---|---|
| `GET /sindri/home?split=&after=&limit=&snoozed=` | the rows by group, current ranked order, with `counts`, `cursor`, `degraded[]` and the first page (default 60) |
| `GET /sindri/home/rows/{id}` | one row with its peek body and `blocksList` |
| `POST /sindri/home/rows/{id}/act` | `{action, version, actId, args}`; the §5.2 pipeline |
| `POST /sindri/home/rows/{id}/snooze` | `{until}` or `{preset}` |
| `POST /sindri/home/rows/{id}/done`, `/undo`, `/mute` | Sindri-local state |
| `GET /sindri/home/done` | the Done list |
| `POST /sindri/home/brief/refresh` | starts the `HomeBrief` job |
| `GET /sindri/home/rank/{id}` | the `--explain` data |

Requirements by area:

- **Ranking (api, Core).** `HomeRank` is a pure function in `Aw.Sindri.Core.Home`: rows, edges and weights in, ordered rows out. The §4.2 example is its golden test. Edges live in a `home_links` table of the view, filled by the collectors.
- **Collectors (api, engine).** One per source in §3.3. Each raises and clears rows by ledger event and records `source.readAt`. The lanes collector is a service in the Sindri host that runs `aw-meta lanes-context` and `aw-meta status` read-only; it never calls `merge-run`, `gate-run` or `lane-recycle` (an action does that, §5.1).
- **Write-through adapters (api).** Forge (`PullMerge`, `PullReview`, `PullReply`), Linear (change and comment), decisions, the session reply route (#3022), aw-meta (`merge-run`, `lane-send`). Each takes the `actId`, checks for an earlier landing, and returns the source's reason on refusal.
- **Snooze, done, mute stores (api).** Ledger events, projected; `home.row.woke` and `.revived` come from the collectors when addressed activity arrives. One store serves Messages (#3020).
- **First paint (ui, api).** `GET /sindri/home` returns 60 rows and the counts in one read. The app writes the last answer to its cache file in `$AW_SINDRI_HOME/ui/` and paints from it before the read returns (D2).
- **Brief (engine).** The `HomeBrief` job: facts, writer, link check, limit states (§6.1). It reports its token spend like other jobs.
- **Profile keys (api).** `home.density` (`compact` | `comfortable`, default `compact`), `home.rank.weights.<reason>` (integers 0 to 9), `home.done.keepDays` (1 to 90, default 7), `home.brief` (`auto` | `facts` | `off`, default `auto`), `home.notify` (`priority` | `off`, default `priority`). An unknown or out-of-range value is SND-HOME-001 with the key path. They take rows in `docs/csharp/sindri-profile.md`, and `sindri profile set` accepts them.
- **Events and migration (api).** The §3.1 events join the event catalog; the view takes the next free ledger migration number at final merge (no gaps, AW-DATA-005).

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
- A session reads the rows of the repos its `X-Sindri-Repo` header names, plus the repo-less rows. Repo and source text in every result is fenced as untrusted, over MCP and in `--json`.
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
| 012 | hand-to-agent target is gone or not one of this repo's sessions | 404 | 1 |
| 013 | a reply or draft is empty or over 8,000 characters | 400 | 1 |
| 014 | an `actId` was replayed with a different body | 409 | 1 |
| 015 | the brief job failed; carries the cause | 500 | 1 |

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
- A second inbox for agents. The Agents page keeps its own idle and slot-wait ranking (Q3).

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
| `Views/Todo/TodoPanel.axaml`, `ViewModels/Shell/ShellViewModel.Todo.cs`, `ShellWindow.TodoKeys.cs` | kept in ship 1 as a collapsed "Todo, 4 open" line under the list that opens the panel as its Full view; due todos become `todo-due` rows (Q2) |
| Running / With you / Queued column (`GET /sindri/queue`) | `running` and `withYou` become Moving lines; `queued` stays on the Queue page |
| the health strip | one health chip in the page header |
| the setup line (`Setup.IsHomeLineShown`) | unchanged, above the list |
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
| `RowKeys` is a private method in `ShellWindow.axaml.cs:143` on main; the shared `Views/Controls/RowKeys.cs` is in open PR #2952 only | 1 file not on main | #2951, #2952 | §15 P0 needs it |
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
| P0 | the shared controls: row tokens (32 / 40, density), `RowList`, `Row`, `PeekPane` on `RowKeys`, with Frames tests. Design builds them: no PR exists (2026-11-12) | design | now | #2952 (`RowKeys`) merged; until then P0 uses the private method's behaviour |
| D1 | the Home frame redrawn with this spec's states: New band, failed, snoozed, empty, brief states; the lane and merge-queue glyphs; the §7.1 width numbers checked | design | now | none |
| S0 | the wire shape: JSON schema and API golden for `/sindri/home` (scenario row, Ui `GoldenContract` slug), `tests/fixtures/home/rows.json` (the §4.2 example as rows), the ledger migration (and `ledger schema N/N` in `src/Sindri/aot-smoke/expected.txt`), the `home.*` events (`EventCatalogTests`), `PublicAPI` files, `home_*` in aot-smoke's tools/list, the profile keys and their rows in `docs/csharp/sindri-profile.md` | api | now | the §3.1 fields |
| A1 | `HomeRank` (pure, golden on §4.2, D8), the edge table, `GET /sindri/home` over the existing sources: needs-you rows, sessions asking, reviews waiting, Linear assigned and mentioned, todos due | api | now, against S0's fixture | S0 |
| A2 | act, snooze, done, undo, mute routes; write-through adapters for forge, Linear, decisions, sessions; early return and revive rules | api | after A1's first read | A1 |
| E1 | the `home-lanes` collector (`lane-recycle`, `agent-limited`, queue `pr-ready-merge`), then the failed-run trigger | engine | now (reads aw-meta; needs no other phase) | Q5 answered by running the commands |
| E2 | the `HomeBrief` job: facts, writer, link check, limit states | engine | after A1's cursor event | A1 |
| U1 | the page on `RowList`: tabs, groups, the New band and held order (D6), peek, keys, states, density setting, all on S0's fixture | ui | now, on the fixture | P0 (stub `RowList` until it lands), S0 |
| U2 | act in place: composers, pickers (snooze, hand to agent), the `Ctrl+Enter` menu, Retry and Discard, Done list | ui | after U1 and A2's routes (fake first) | U1, A2 |
| U3 | brief and Moving sections, empty state, tray count and toasts, first-paint cache (D2) | ui | after U1 | U1, E2 |
| U4 | delete the old views in §12 | ui | after U3, one week of the new page default | U3 |
| F1 | perf: D2, D3, D8 on the 5,000-row store in the real app | perf | after U1 | U1 |
| X1 | the day-without script (D14) and the D1 walk | ux | script now; walk after U1 + E1 | U1, E1 |
| Q1 | qa: D4, D5, D9, D10, D11, D13 | qa | after U2 + A2 | U2, A2 |

P0, D1, S0, A1, E1 and the X1 script start the same day, so six lanes are busy from day one. U1 starts the same day on S0's fixture and a stub `RowList`, so ui is busy too. Nothing waits on a source that does not exist: Messages (#2979) is the one external dependency, and Home ships without `dm` rows.

Each phase updates its docs: `docs/sindri/README.md` (or a new `docs/sindri/home.md`), `docs/csharp/sindri-profile.md`, and the CLAUDE.md Commands entries for the new `sindri home` verbs.

## Open questions

1. **Q1.** Does `e` Done stay a bare key (this spec's pick), or move behind `.`? Meta's rule on #2978 says a bare letter never commits a write; Done writes only Sindri's own state and has a 5 s Undo.
2. **Q2.** Where does the Todo panel live? This spec keeps it as a collapsed line under the list (pick). The alternative is to make every open todo a row, which would fill Home with things that do not need the owner.
3. **Q3.** Do "idle with work owed" and "slot wait over 30 min" stay on the Agents page only? This spec says yes, because Home shows no guesses.
4. **Q4.** Should Home offer Merge on a PR in a meta team's merge queue, when the meta lane normally merges it? This spec offers it through `aw-meta merge-run` (which runs `merge-check`), so the owner can merge without the lane.
5. **Q5.** The JSON of `aw-meta status` and `aw-meta lanes-context` was not read for this spec. E1 starts by running both and fixing §3.3 to match.
