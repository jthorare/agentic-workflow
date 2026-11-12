# Sindri's Agents page: one list, needs you first

Status: draft (ideas lane, 2026-11-12). Serves #3018 (Agents: see and steer every agent session on the box) under #2978 (one app that replaces the others). It builds the Agents rows of the prior-art ledger (`docs/sindri/prior-art/agents.md`, #2989) and sits beside the Home spec (`docs/superpowers/specs/2026-11-12-sindri-home-inbox-design.md`, PR #3091). It reuses Home's section shape, row grammar, keys, stable-order rule and review-note format.
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

## 0. Why

The owner runs 11 lane sessions plus their builders on one box. The Agents page is meant to answer one question: which of them needs me?

What the page does on main today (read from `origin/main` 7c81646696; issues linked; the app was not driven for this spec):

- It groups by workflow. The default group is "Not part of a workflow", which is true of every lane (`ViewModels/Labels.Agents.cs:9`; the grouping is in `AgentsPageViewModel.Rows.cs:8-18`).
- Its head counts only `asking`. `Labels.Agents.cs:45` prints "N agents · M asking you". A lane at its recycle mark is not a state, so it never counts.
- It has no Needs-you group, no rank and no order rule. A one-shot `judge-cli` row came and went in the ux walk and moved every row below it (#3082).
- It polls. `AgentsPageViewModel.Poll.cs:10` sets `PollEvery` to 5 s, and `Poll.cs:47` starts the timer. A state change waits up to 5 s plus one read.
- Its keys are J, K, Enter, O, Y and E (`AgentsPageViewModel.Keys.cs:56-64`). It has no peek, no reply, no stop and no filter box.
- The row, `SessionRow` (`Core/Sessions/SessionRow.cs:27-29`), has a state, why it asks, repo, worktree, host, lane and launch id. It has no context size, no PR or issue, no last activity, no heavy slot and no parent session.
- Nothing steers. The only session writes are the heartbeat and exit routes, proven by the launch secret (`Host/Endpoints/SessionHeartbeatEndpoint.cs`).

The ux walk on #3018 ("I wake up, which of 11 lanes needs me", comment 6819109216) took 5 steps and ended in a terminal. The page said "11 agents · 0 asking you". `aw-meta status` said meta (205k) and ideas (210k) sat at the recycle mark, and #3067 and #3078 were ready in the merge queue.

The job: **one list of every live agent session on the box. The first group holds the sessions that need the owner, ranked, with the reason in words. Each row can be steered from where it sits, with the same call `aw-meta` makes.**

## 1. The bar

"Ledger N" is problem N in `docs/sindri/prior-art/agents.md`. The rows below are the ones this page builds. Problems 4 (jump), 5 (stop), 6 (cost), 7 (find), 9 (alerts) and 12 (resume) already have issues (#3023, #3025, #3024, #3026) and are placed in §14.

| Step of the daily loop | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| See which sessions wait on me, first | 1 | Claude `claude agents` (Needs input group, `s:blocked`) | group by need, shape-distinct glyphs; ours adds lanes, limits and the merge queue | §3, §4 |
| One line of what it does | 2 | Claude agent view row | glyph, name from the work, last activity, age in state | §6.1 |
| Peek and reply in place | 3 | Claude `Space` peek | Space peek; ours adds the 20 folded tool calls and the files changed | §6.3 |
| Jump into the terminal | 4 | agent-deck tmux bar | one key, host-aware, copies the command when it can't focus | §5, §6.5 |
| Stop safely | 5 | Claude `Ctrl+X` twice | two keystrokes: `x`, then Enter in a confirm | §5 |
| Cost and context | 6 | ccusage, agent-deck | context k with the recycle mark on every row; cost columns come from #3025 | §6.1 |
| Honest about what I can't see | 11 | Claude's six named states | never a bare "Unknown"; every state names its source and age | §3.1, §6.4 |
| Stuck detection | none | none found in the ledger | ours: "idle with work owed" and "slot wait over 30 min", with exact inputs | §3.2 |

The ledger found no tool that flags a session by what it owes or by a recycle mark. Those two are ours and have no prior art to lean on.

**The bar in one line:** the session that needs the owner most is row 1 one step after opening the tab; a row moves only on a state change; every steer lands through one call that the CLI also makes; and a state-file change shows on the page within 1 s.

Done-checks. Each is a test or a measured run in the real app (aw-ui-driver) on a scratch home, never the owner's. §14 names the owner lane of each.

| # | Check | Measured as |
|---|---|---|
| D1 | **The ux walk, 1 step.** On the #3018 box state (11 lanes; meta 205k and ideas 210k at the recycle mark; #3067 and #3078 ready in the merge queue), row 1 is the session that needs the owner most, with the reason in words. No sheet is opened. | Steps from opening the Agents tab to reading row 1: 1. Today it takes 5. The page also prints "3 need you · 2 PRs ready" in its head. The §4.3 example is the golden. |
| D2 | **Needs you first, ranked, all reasons.** The first group is "Needs you". It holds each of the §3.2 reasons: permission prompt, owner question, login, usage limit, lane with no session, at the recycle mark, idle with work owed, slot wait over 30 min, PR ready in the merge queue for its lane. | One fixture per reason on the fake adapters. Each lands in Needs you with the words in §3.2. The class order of §4.1 is a golden test. |
| D3 | **Stable order.** A row moves only on a state change, never on a poll. | A property test over 10,000 random event streams, each with a poll tick between every pair of events: no row changes index except by removal, entry to the New band, or its own class change (§4.2). |
| D4 | **Steer through one call.** Message, answer, recycle from a digest and stop each go through `POST /sindri/rows/{id}/ops`, and that route calls what `aw-meta` calls. | A contract test per op records the argv the fake `aw-meta` receives from the route and from the CLI verb. The two argv lists are equal. |
| D5 | **Within 1 s.** A change to a session's state file shows on the page within 1 s, by push, with 50 sessions. | perf's loop (§7.3): p95 under 1 s at 50 and at 200 sessions. The baseline to beat is today's p50 2.5 s and p95 5 s (perf's estimate on #3018). |
| D6 | **Home agrees.** Every reason that Home also lists opens the same Home row. Agents never repeats a Home action with other behavior. | A test enumerates the reasons: each Home-backed reason links to a Home row id, and the Agents `.` menu names the Home action id (`home.merge`, `home.digest`) instead of its own. |
| D7 | **First paint.** The tab paints under 100 ms from the local store with 50 rows. | Tab press to first row painted, p95 over 20 runs, serve not answering. |
| D8 | **11 lanes without scrolling.** All 11 lanes of the walk are visible at 1440x900, compact, peek closed. | Frames test count of drawn rows; §6.1 gives the arithmetic (18 rows). |
| D9 | **Honest states.** No bare "Unknown". A stale or unreadable source says so and says what is missing. | Every §6.4 state has a Frames test. A test greps the Labels for the word "Unknown" alone. |
| D10 | **Who may steer.** In v1 only the app and the terminal (the human token) steer. Every session caller is refused with SND-AUTH-002. `sindri agents steer` is the owner's CLI: it also refuses, with one line ("Steer from the app or your own terminal, not from an agent session"), when the fleet file of its parent process's agent pid exists, so a lane shell that can read the token does not steer by mistake. Lead-steers-own-builders is cut (lead, 2026-11-12) and returns later (§5). Stop never matches by pattern. | An authorization matrix test (caller class x op, with the header absent, naming another session, and a session not live). A test refuses Stop when the start ticks differ. The matrix has a row for a CLI run from a lane shell that can read the token (refused by the fleet-file guard). A grep test finds no `pkill`, `pgrep -f` or `killall` in the route's code. |
| D11 | **One badge.** The tray count and alert for a session that asks are Home's single count, not a second badge. Nothing repeats while the state holds. | Over 50 injected events: one alert per episode, the tray count equals Home's notifying rows (Home D10). |
| D12 | **Agent parity.** `sindri agents` and an MCP tool give every field and every steer op the page has, for the fields this caller class may see (a session caller gets no title, pid, tool lines or pane tail, §5 "Who may steer"). | A test enumerates the row fields and ops: each has a CLI flag or verb and an MCP tool, and each result fences session text. |
| D13 | **A day without the terminal.** ux runs a scripted day with `aw-meta status`, `aw-meta health` and tmux closed. Each time the owner opens one to find out which lane needs them is an issue, `Part of #3018`. | Count of such opens: 0. |

## 2. Decisions

Taken for the owner. Each says where it came from. "Agent default" means nobody ruled on it and the spec picked. Source tags, one per number or rule; every `agent default` item is named in the list below, and a number not named there has no source and goes to the open questions. `owner decision (lead, 2026-11-12)`: the group order, the 10 min idle-owed and 30 min slot-wait thresholds, Home leaving both out of v1, Stop = `x` + confirm. `owner decision (#3082)`: grouping by workflow. `existing code`: the 40 pane lines and the 1 s pane read (`IPaneReader.cs:3`), the 5 s page poll (`Poll.cs:10`; kept, U4 must not delete it), the 180 s recycle wait (`RecycleRunner.cs:25`), the 2 h digest limit (`RecycleGuard.cs:19`). `agent default (meta, #3018)`: heavy waiters as rows, one steer route. `agent default`, each item named: the 30 s age after which the head says "lane facts from" (§7.2), the 4,096-character digest path cap (§9 005), the idle-owed sources 3a to 3c, the D1 to D13 targets, the rule that builders sit under their lead and the sort inside Working and Idle (§4.1), the class numbers 2 to 7 (#3082's order plus two additions), the 8,000-character message cap, the 60 s stale mark, the 20 tool calls, the 400 ms skeleton and 6 skeleton rows, the 100 ms and 50 ms peek budgets, the 220 ms budget lines in §7.2 (20 / 50 / 50 / 20 / 60 / 16 ms), the 50 ms debounce, the 2 s events cursor, the 15 s fallback tick, the 2 s steer landing and "still sending" mark, the 5 s undo for removing an ended row, the profile key ranges 1 to 1440, the 1440x900 and 11-lane review frame, the "one week with no miss" in U6, the 28 / 32 / 40 / 48 / 440 px sizes and breakpoints (Home §7.1; design on #2978), the rules that a failed or conflicting gate counts as owed (3a) and that Reply or RelayOwner obligations count as owed (3b), that only class 1 alerts (§6.7), that a stop is SIGTERM with no escalation, and that Recently ended keeps the last 20 rows for 24 h and Other holds `judge-cli` sessions only.

1. **Needs you is the first group.** Then Working, then Idle, then a folded Other group for one-shot `judge` sessions, then a folded Recently ended group. Group by workflow only for sessions that belong to one (#3082, expected 2). The default group "Not part of a workflow" goes away.
2. **A row has one primary reason and counts the rest.** The reason with the lowest class number (§4.1) names the row and sets its place. The other reasons show as "+1" and fill the peek.
3. **Order is class, then time in class.** Within Needs you the order is the class of the primary reason, then the time the row entered that class (oldest first), then the ref. This ignores any score on purpose, so no poll can move a row. Home orders by "blocks N", so the two pages can list the same sessions in a different order. The row shows Home's `blocks N` as a fact when it exists, and never sorts by it. (Agent default. It keeps the walk's expected row 1, "ideas: at 210k", which #3018 named.)
4. **Escalation uses the New band.** A row that enters Needs you, or rises to a lower class number, shows in a "New (n)" band at the top until the next settle point. This is Home's rule (Home §4.3), so the cursor row never jumps.
5. **Idle with work owed and slot wait are defined by inputs, not guesses.** §3.2 gives each input, threshold and word. Home leaves both out because it makes no guesses. The Agents page owns them. Home does not show them in v1 (the lead's call, 2026-11-12); revisit after the soak.
6. **One steer route.** `POST /sindri/rows/{id}/ops` with `message`, `answer`, `recycle` or `stop` (meta's decision on #3018). It is the route `aw-meta` and the CLI verbs also use. No op types into a tmux pane.
7. **Message to a Claude lane is off for now.** `aw-meta lane-send` refuses a Claude lane (`Aw.Meta.Features/LaneSend/LaneSendService.cs:52`, AW-META-017, "use SendMessage"). Meta's rule says no typing into a pane. So the page cannot message a Claude lane. It says why and offers Copy message. Open question 2 asks tools for a path.
8. **A permission prompt is answered in the terminal.** The engine comment on #3018 says a permission prompt is answered only in an attended session. So the row's action for a prompt is Jump to terminal. The reply box is for owner questions, workflow sessions and non-Claude lanes.
9. **Stop is two keystrokes with no timer.** `x` opens a confirm. `Enter` stops the pid. `Esc` cancels. This follows meta's rule that a bare letter never commits a write (#2978). It replaces #3023's "second press within 2 s" and its undo. A signal cannot be undone, so the 5 s undo applies only to removing an ended row from the list.
10. **Stop is by recorded pid and start ticks.** The state file holds `agentPid` and `agentStartTicks` (`schemas/fleet-state.schema.json:42-43`). If the ticks changed, the route refuses (SND-AGENT-004). No pattern kills.
11. **Builders nest under their lead when a row knows its lead, except when they need the owner.** A row has a `parent` only for a Sindri-launched session. Builders that run inside the lead's own session share its session id and have no row (unverified: list `$AW_FLEET_DIR/claude-code/` while a lane runs a builder; A1 settles it). v1 promises nesting only for rows with a `parent`. In Working and Idle a builder is a folded child of its lead. A builder with a reason is its own row in Needs you, and names its lead in the "from" fact. The lead's row shows a count: "2 builders".
12. **Heavy slot holders are a fact, not rows.** Waiters over 30 min are rows (meta's decision 2 on #3018; ui and ux asked for a row, not a log line). The holder facts and the waiters come from one reader, `aw-meta heavy --json` (tools).
13. **Keys follow Home.** Fold is Left and Right on a header, not `E`. Reply is `r`, as on Home. Bare `g` is the shell's G chord, so Agents never binds it. `e`, `h`, `t`, `[`, `]` stay unbound here (§6.6).
14. **Density is Home's.** 32 px rows compact, 40 comfortable, from the same profile key (`home.density`) until design names an app-wide one.
15. **The old page stays until U6.** The profile key `agents.page` (`rows` | `legacy`) picks the page, as Home's `home.page` does. U6 deletes the old views after one week with no miss of D1, D3 or D5. A profile that sets the key still validates for one release after.

## 3. The model

### 3.1 The row

An Agents row is one live session, one waiting job, or one lane with no session. Core type `AgentRow` in `Aw.Sindri.Core.Sessions`. It extends today's `SessionRow` and is the "session" kind of the #2978 WorkRow. Fields:

| Field | What it holds |
|---|---|
| `id` | `session:<provider>/<id>`; `job:<pid>` for a waiting job; `lane:<team>/<lane>` for a lane with no session. Matches Home's ids, so the two pages link. |
| `version` | starts at 1, +1 per change; every steer names the version it read (SND-AGENT-002) |
| `state` | one of: `working`, `waiting-on-you`, `idle`, `limited`, `down`. Defined below. |
| `stateSource` | `hook`, `record`, `transcript` or `aw-meta`, with `readAt`. Today's `StateSource` plus `aw-meta`. |
| `estimated` | true when the state is a guess from the transcript's age (`SessionStateRules.cs:50-52`) |
| `title` | the work in words: lane name, then the PR or issue title, then `repo · branch`, then the provider and start time. A pid only in Details. |
| `facts` | lane or parent, `repo · branch`, PR or issue ref, context k with the mark, last activity age, heavy slot |
| `reasons[]` | the §3.2 reasons that hold now, each `{class, word, since, source}`; the first is primary |
| `parent` | the lead's id, for a builder; null otherwise |
| `since` | when the state began (today's `Since`) |
| `lastActivity` | when the transcript last grew (see the note below) |
| `blocks` | Home's `blocks N` for the row, when Home's rank exists; null otherwise. Display only. |
| `actions[]` | op ids that apply (§5), the first is the primary |
| `links[]` | the Home row ids, PR, issue, workflow run and node, launch id |

Note on last activity. The hook does not record it. `config/hooks/fleet-state.sh:10-11` says a PostToolUse (every tool call) exits before `jq` unless the file says asking, and `fleet-state.sh:52-57` shows it only changes asking back to running. So `at` in the state file is when the state began, not the last tool. The age of the last activity therefore comes from the transcript's last write, as the lane list already does (`LaneContext.LastActivity`, `Aw.Meta.Core/Lanes/LaneContext.cs:11,20`) and as the state merge does for a session with no hook (`SessionStateRules.cs:50-52`). The row has no last tool name, except the tool an approval asks for (`schemas/fleet-state.schema.json:30-35`).

**States.** Six glyphs from #3018, with one change.

| State | Glyph (shape, not colour alone) | Comes from |
|---|---|---|
| `working` | arc | `SessionState.Running` (hook `UserPromptSubmit`, or record `busy`) |
| `waiting-on-you` | diamond | `SessionState.Asking`: approval, input or auth (`SessionAsking.cs:7`); a workflow node `waiting on you` (engine) |
| `idle` | ring | `SessionState.Idle`, not estimated |
| `limited` | half ring | `aw-meta usage` subject `Limited` (`Aw.Meta.Core/Usage/UsageState.cs:9`); a Sindri run paused on a model limit |
| `down` | x | a lane in the team map with no live session (`aw-meta health`: "lanes with no live session", `HealthCommand.cs:29`); a session whose process left without a `SessionEnd` |
| (no glyph) | | an estimated or unreadable state shows "No recent activity" or "Can't see this terminal", muted, never "Unknown" (ledger 11) |

**The glyph is always the session's own state, never the reason it is listed.** A row in Needs you for a session that is working shows the arc, and for an idle one the ring. Only a session in state `asking` shows the diamond.

The sixth state in #3018, "waiting on a tool", has no source. No hook records a running tool call, because PostToolUse writes nothing (see the note above). The page does not draw it. It shows the age of the last activity instead. Open question 3 records this.

### 3.2 Needs you: the reasons and their exact triggers

A reason holds while its trigger holds. The class number is its place in the order (§4.1).

| Class | Reason | Trigger (inputs, threshold) | Words on the row | On main? |
|---|---|---|---|---|
| 1 | permission prompt | session state `asking` with reason `approval`; the tool name from the state file | "qa: asking to run Bash" | yes (`SessionAsking`) |
| 1 | owner question | state `asking` with reason `input`; or a workflow node `waiting on you` with `owner question <id>` (engine) | "ux: asking you a question" | yes for sessions; node state from engine |
| 1 | login | state `asking` with reason `auth` | "api: needs a login" | yes |
| 2 | usage limit | an entry of `subjects[]` in `aw-meta usage --json` with `state` `"Limited"` (the enum name, as `verdict` is `"Ok"`; `Features/Usage/UsageRow.cs`, `UsageResponse.cs`). `subject` is the lane name (or `meta`); `resetsAt` is omitted when the text gave no time (`UsageStop.cs:9`); or a Sindri run paused on a model limit | "tools: hit its usage limit, resets 14:00" (no time: "reset time unknown") | no: needs tools' `usage.limited` event |
| 3 | lane with no session | a lane in the team map has no live session, unless the owner stopped it from this page (`x` or a recycle): that lane shows as "stopped from the page" in Idle's place until it has a live session again | "design: no live session" | partly: `aw-meta health` prints it; no event yet |
| 4 | at the recycle mark | `aw-meta lanes-context` verdict `Recycle` (`ContextVerdict.cs:9`): context tokens at or over `context.recycleAt` (`ContextConfig.cs:5`, default 300,000). For the meta lane, also the open `RecycleSelf` obligation (`ObligationKind.cs:21`). | "ideas: at 210k, recycle from a digest" | yes: CLI text and `status --json` `context[]` (`StatusResponse.cs:28`); no event yet |
| 5 | idle with work owed | see the rule below | "qa: idle 14 min, PR #3067 failed its gate" | no |
| 6 | slot wait over 30 min | a heavy waiter whose `since` is 30 min or more ago (`agents.slotWaitMinutes`, default 30) | "tools: waiting 39 min for a heavy slot (3 of 3 held)" | no: waiters are recorded nowhere today |
| 7 | PR ready in the merge queue | a `mergeQueue[]` entry (`QueuedPr.cs:8`) whose `from` is this lane's session name (join rule below), with `Gate` Pass or "none", and `Github` null | "ideas: PR #3078 ready in the merge queue" | yes: `status --json` `mergeQueue[]` |

**Idle with work owed, exactly.** A lane session qualifies when all of these hold:

1. It is a lane session (it has a lane in the team map).
2. Its state is `idle` from the hook or the record, not estimated, for `agents.idleOwedMinutes` or more (default 10).
3. At least one of these is owed to it:
   - a. A PR it sent to the merge queue has `Gate` Fail or Conflict (`QueuedPr.cs:6`). Words: "PR #3067 failed its gate".
   - b. For the meta lane only: one or more open obligations of kind Reply or RelayOwner (`ObligationKind.cs:7,16`). Words: "2 open obligations". An obligation has no timestamp (`Obligation.cs:8`), so the idle clock is the only clock.
   - c. For a non-Claude lane: `aw-meta health` reports the lane's state with Attention true and the text "mail not delivered for N min" (`LaneHealth.cs:6-8`). Words: "mail not delivered for 12 min". Health lists non-Claude lanes only (`LaneHealthReader.cs`, `IsNonClaude`), and flags mail after 5 min (`AW_META_UNDELIVERED_MINUTES`). Both clocks must hold: the lane idle 10 min, and the health text present.

An idle lane that owes nothing stays in Idle. A lane that is idle for 9 minutes does not qualify yet. A Claude lane's unread mail has no source on main (checked on origin/main 4fafac85b4: `aw-meta health` skips Claude lanes and no `unread` field exists in `status --json`), so nothing about mail counts for it (open question 4).

**Slot wait, exactly.** The waiter record comes from `aw-meta heavy --json` (tools), which reads `locks/heavy-wait/<pid>` that `scripts/heavy.sh` will write while it waits (session, argv, since). Neither exists on main: `git grep heavy-wait` finds nothing in `scripts/` or `config/`, and no `heavy` command is in `src/Meta/Aw.Meta.Host/Commands/`. The row is the waiter itself (id `job:<pid>`), nested under its session when that session is live. Until tools ships the reader, this class is empty and the box strip says "heavy waits: not readable yet" (§6.4).

**PR ready: the join.** `QueuedPr.From` is the sender of the latest `ready #N` message for that PR: the sender's session name, or the raw peer id when the name is empty (`MergeQueueReader.cs:108`). A lane's session name is `<team>-<lane>` (`scripts/meta-lane.sh:202`; `LaneMap.cs` maps it back with `OrdinalIgnoreCase`). So `From` is `joi-ideas`, not `ideas`. The page joins through `status --json` `lanes` (`LaneAssignment.SessionName` to `Lane`, ignoring case, as `LaneMap.cs` does), never by comparing to the bare lane name. If no lane matches (a builder, an unnamed peer, a raw peer id), the entry adds to the head's PR count, makes no row and no reason, and adds a `degraded[]` entry `unmatched-sender` with the PR number. If another lane sends a newer `ready #N` for the same PR, the reason moves to that lane at the next read. Fixtures use team-prefixed names (`joi-ideas`) and one raw peer id.

**Primary action per reason.** Every Needs-you row has one primary action. `Enter` always opens the full session sheet (§6.3); the primary action runs on `Ctrl+Enter` and is the first button in the peek. `x` (Stop) works only on a row with a recorded `agentPid`; on a row with none (`lane:` rows, workflow nodes) it is disabled and the peek says "no process to stop".

| Reason | Primary action (`Ctrl+Enter`) | Steers? |
|---|---|---|
| permission prompt, login | Jump to terminal | no |
| owner question | Answer in the composer (workflow session) or Jump to terminal | yes for a workflow node |
| usage limit | Open the usage fact in the peek (who is limited, reset time); nothing to send | no |
| lane with no session | Copy the launch command (`scripts/meta-launch.sh --lanes <lane>`) | no |
| at the recycle mark | Copy digest request, then Recycle (§5) | yes (recycle) |
| idle with work owed | Jump to terminal; 3a also offers Open in Home | no |
| slot wait over 30 min | Peek shows the holders; nothing to send | no |
| PR ready in the merge queue | Open in Home | no |

**PR ready.** The reason sits on the lane that sent the PR, because the lane is done and waits on the merge. The merge itself is Home's action (`pr-ready-merge`, Home §5.1). The row's action is "Open in Home". While meta is already merging the PR (`Github` is "queued 2 (AWAITING_CHECKS)"), the reason drops: nothing is left for the owner.

**Which rows Home also lists.** Permission prompt, owner question and login are Home's `agent-asking`. Usage limit is `agent-limited`. At the recycle mark is `lane-recycle`. PR ready is `pr-ready-merge`, which Home keeps as a row of the PR. Lane with no session, idle with work owed and slot wait are Agents-only in v1 (decision 5); revisit after the soak.

### 3.3 Where the data comes from

| Source | Read through | New work |
|---|---|---|
| sessions, states, asking | `SessionDiscovery` (`Features/Sessions/SessionDiscovery.cs:13`): process table, Claude's records, the hook files; `GET /sindri/sessions` (`Host/Endpoints/ListSessionsEndpoint.cs:10`) | the row facts of §3.1; the state-file watch (§7) |
| context, usage, queue, lanes, obligations | `aw-meta events --after N --json` behind one port (meta's decision 1). `events` is on main (PR #3103, merged); #3110 adds `oldest` and `gap` to its `--json`. When `gap` is true (the cursor is older than the 7-day retention) the page re-snapshots from `aw-meta status --json` and the other readers, then resumes from `next`. Until the emitters ship, `aw-meta lanes-context --json`, `usage --json` and `status --json` on a 15 s service tick (§7.2) | tools T1 to T3; a `lanes` collector in the Sindri host (shared with Home's `home-lanes`, one collector, two readers) |
| heavy slots, waiters, detached jobs | `aw-meta heavy --json`, `aw-meta jobs --json` (tools) | tools T3, T4 |
| memory, disk | `box.health` event (tools); until then `aw-meta health --json` (`HostSnapshot.MemAvailableKb`, `DiskFreeBytes`, `Aw.Meta.Core/Health/HostSnapshot.cs:8`) | tools T3 |
| builder to lead | session discovery: a builder's parent process or launch is its lead | api: no parent field exists (`SessionRow.cs:27-29`) |
| workflow sessions | ledger events from the node run (state, waiting on, links; engine) | engine E1 |
| PR or issue a session works on | lane map and branch name; the `item-of` list Home's E1 finds | api, shared with Home E1 |
| peek: tool calls and files | the session's transcript, last 20 tool calls folded; `git diff --stat` of its worktree | api A4 |
| peek: pending question | `IPaneReader` (`Core/Sessions/IPaneReader.cs:4`): last 40 lines of a tmux pane inside 1 s; other hosts say why not | none |
| cost | `AgentSpend` for launches today; transcript usage for lane sessions (#3025) | api A5 |

Sindri never writes to `aw-meta`'s files. It reads through the CLI's `--json`, and `aw-meta` stays the only writer of its own state (meta's decision 1).

## 4. Order and stability

### 4.1 Groups and class order

Groups, in this order: Needs you, Working, Idle, Other (folded), Recently ended (folded). Headers are 28 px, sentence case, with a count, as in Home.

Inside Needs you the rows sort by:

1. class of the primary reason, low first (1 asking, 2 limited, 3 no session, 4 recycle mark, 5 idle owed, 6 slot wait, 7 PR ready);
2. `enteredClassAt`, old first: the moment the session first held that class (the reason's `since`, from the source, not the poll time);
3. the ref, ordinal.

**Workflow groups (#3082).** A session that belongs to a workflow leaves Working and Idle and sits under one header named for the workflow (28 px, count, folds with Left and Right). The workflow groups come after Idle and before Other, sorted by workflow name A to Z. A session that needs the owner shows once, in Needs you, with the workflow name in its context column. With no workflow session the group does not draw (the mock below shows the case with none).

Inside Working and Idle the rows sort by lane name (A to Z, as the team map lists them), then ref. Builders sit under their lead. Other holds sessions with no lane and no repo, such as a `judge-cli` run. Recently ended keeps today's order (newest first).

The class order follows #3082's list (asking, limited, recycle mark, idle with work owed, slot wait) with two additions: "no session" at 3 and "PR ready" at 7. PR ready is last because it needs a merge, not the lane.

### 4.2 Stable order and the New band

The server returns rows in the order of §4.1. The app holds its own order and changes it only at a **settle point**:

1. The owner presses `R` (re-rank now).
2. The tab opens, or a filter changes.

Between settle points:
- A row that leaves its group (a session ends, a state change) is removed in place. Rows below move up one. The cursor stays on its row, or on the next one.
- A row whose text changes (context grows, the age ticks, the "+1" count) updates in place and keeps its position. No poll changes an index.
- A row that **enters Needs you**, or **rises to a lower class number** (a Working session asks; a PR-ready lane then asks a permission), appears in the "New (n)" band, a 28 px header at the top of Needs you. It carries the word "new" in the age column, in ink. The app scrolls to keep the cursor row at the same screen y, and the header sticks.
- A session that moves down a class or leaves Needs you goes to its new group at once, since it left the group the cursor watches.

At the next settle point the New rows take their ranked place and the mark goes. An act on one row (a steer, a Jump) settles that row only. This is Home's §4.3, applied to sessions.

The bad case this stops: a lane's row jumps to the top as the owner presses Enter, and Enter opens the wrong session. It is #3082's expected behavior 3: "the order holds still between refreshes".

### 4.3 The walk, rerun: meta 205k, ideas 210k, #3067 and #3078 ready

The numbers for meta, ideas, #3067, #3078 and qa idle for 9 min come from the ux walk on #3018. They are fixture values: on origin/main 4fafac85b4, #3067 is the merged context-scope spec PR (branch `docs/context-scope-spec`, author jthorare), and nothing on GitHub says which lane sent it, so "qa sent #3067" is the walk's claim, not checked. Every other number is made up for this spec. On the owner's box the team config sets `context.recycleAt` to 200,000, and each lane row from `aw-meta lanes-context --json` carries `threshold: 200000` (checked by the lead on 2026-11-12). So 205k and 210k are past the mark. The 300,000 in `ContextConfig.cs:5` is only the code default.

Box at 09:10. Eleven lanes: context, design, ideas, ux, meta, ui, qa, engine, tools, perf, api.

| Lane | State | Context | Reasons that hold | Entered |
|---|---|---|---|---|
| ideas | working | 210k | class 4 at the mark; class 7 #3078 ready (gate Pass) | 08:50 |
| meta | working | 205k | class 4 at the mark | 08:55 |
| qa | idle 9 min | 88k | class 7 #3067 ready (gate Pass); not class 5: nothing owed, and 9 min is under 10 | 08:58 |
| api | idle 31 min | 61k | none: owes nothing | n/a |
| context, design, ux, ui, engine, tools, perf | working | 40k to 150k | none | n/a |

The page at 09:10. The head reads **"11 agents · 3 need you · 2 PRs ready to merge"**. Needs you holds three rows, in this order:

1. **ideas**: "at 210k, recycle from a digest" with "+1" (PR #3078 ready). Class 4, entered 08:50.
2. **meta**: "at 205k, recycle from a digest". Class 4, entered 08:55.
3. **qa**: "PR #3067 ready in the merge queue". Class 7.

Working holds 7 rows (9 working lanes, less ideas and meta). Idle holds 1 (api). The count is 3 + 7 + 1 = 11.

Row 1 is ideas, one step after opening the tab, with the cursor on it. This is D1. The old page said "0 asking you" and listed the lanes in an order that ranked nothing.

At 09:12:03 qa asks for approval to run `rm -rf build/`. By 09:12:04 (D5) qa's reason rises from class 7 to class 1. It enters the New band. The page reads:

```
New (1)
  qa: asking to run Bash                         new
Needs you 2
  ideas: at 210k, recycle from a digest          +1
  meta: at 205k, recycle from a digest
```

The cursor was on ideas and stays on ideas, at the same screen y. At 09:14 the owner presses `j`. The cursor moves to meta. Nothing else moves. At 09:16 the owner presses `R`. qa takes its ranked place at class 1: qa, ideas, meta. Between 09:10 and 09:16 no row changed index except qa's arrival in the band.

The head still reads "2 PRs ready to merge" at 09:12. It counts merge-queue entries, not reasons, and #3067 is still in the queue. The head's "need you" count stays 3: qa was already in Needs you for its PR.

## 5. Steering

One route steers a session: `POST /sindri/rows/{id}/ops` with `{op, version, actId, args}`. It is api's route (comment on #3018). It does not exist on main: `git grep '/ops'` finds only the workflow ops route (`Host/Endpoints/WorkflowWriteEndpoints.cs:67`). Each op calls what the CLI calls, so the page and `aw-meta` cannot disagree (D4).

| Op | Who it applies to | What the route calls | Refused when |
|---|---|---|---|
| `message` | a workflow-launched session | the engine appends to the session's inbox; the session reads it at its next turn | the session ended |
| | a non-Claude team lane | `aw-meta lane-send --lane L --text T` (`Aw.Meta.Host/Commands/LaneSendCommand.cs:10`) | the lane is launch-only or its hooks don't run (exit 1, the message is stored) |
| | a Claude team lane | nothing: `lane-send` refuses it (AW-META-017, `LaneSendService.cs:52`). The row says "Claude lanes take mail through SendMessage" and offers Copy message. | always, until open question 2 |
| | any other session | nothing: no typing into a pane. The row says "Started outside Sindri: copy the reply". | always |
| `answer` | an owner question of a workflow node | the existing question port (engine) | the question was answered elsewhere |
| | a permission prompt | nothing. The row's action is Jump to terminal. | always: answered only in an attended session (engine, #3018) |
| `recycle` | a team lane at or near the mark | `aw-meta lane-recycle <lane> --digest <path>` (`LaneRecycleCommand.cs:16`); for the meta lane `--detach`. The route answers "accepted" at once and runs the call as a Sindri job (the call waits up to 180 s for the new session). The row shows "recycling" until the lane reports a new session; a timeout is SND-AGENT-011 and the row shows it with Retry | no digest, or the digest path is unreadable (AW-META-021) |
| `stop` | a workflow-launched session | the engine cancels the node; the node ends `cancelled` | the run already ended |
| | any other session | SIGTERM to `agentPid`, after checking `agentStartTicks` still match | the ticks changed (SND-AGENT-004) |

`handoff` (api's fifth op) is not used on this page. A session is not work to hand over. Home's Hand to agent (`t`) starts an agent on a row.

**Who may steer.** v1: only the person, from the app or from a terminal CLI that holds the per-start human token. Every session caller (a lane, a builder, an MCP client) is refused on `ops` with SND-AUTH-002, by caller class, the way `Authorization.cs` already tells classes apart. Reads stay open to sessions with the field limits below. "A lead steers its own builders" is cut from v1 (settled, lead, 2026-11-12: the page is the owner's, and leads steer builders by SendMessage): a lane has no `X-Sindri-Launch`, `CallerContext` (`Core/Commands/CallerContext.cs:20`) carries no session id, MCP is registered once per repo with a fixed `X-Sindri-Repo` header, a CLI that can read the human token is classed Human whatever agent runs it (`CallerHeaders.Resolve`), and builders probably have no rows of their own (§2.11). It returns after v1 with three named parts: a session-id source (match `agentPid` and `agentStartTicks` of the calling agent process against the fleet files), a `parent` on rows, and a target rule in the route. The route's action class is named `AgentOp`, so a new class sits between sessions and a stop instead of reusing Pause or Job.

**What a session caller reads.** `SessionRow` documents Title and Pid as human-only. A session caller gets the other row fields. Title, pid, tool lines and the pane tail (`/activity`) are Human callers only.

**Write-through.** The same pipeline as Home §5.2. The key paints the row at once ("sending"). The route returns the outcome. Within 2 s the row shows it (for recycle, "accepted" then the job's outcome as an event): "sent", or "failed: <reason>" with Retry. Retry sends the same `actId`. A second op on a row that is still sending is refused (SND-AGENT-009). There is no Undo for a steer: a message was sent and a process was stopped.

**Stored state (no new table).** Three things persist, each without a new table:
- `since` (time in class). Class 1 uses the hook's `at`; class 2 uses the run's pause time; the rest (3 to 7) have no source time, so the first-seen time is kept in memory. A serve restart resets first-seen; ties then sort by ref, and the cost is a reorder of those classes once per restart.
- `actId`. `CommandHandled` has no `actId` and no body hash, and the ledger is append-only, so the claim is one new ledger event, `agent.op`, keyed by `actId` with `{rowId, op, bodyHash, outcome}` and never the body. The ledger's unique-key append makes the claim atomic, so two calls with one `actId` cannot both pass, and no per-row lock is needed (the formal-check target is this append). A replay with the same body inside 10 min (agent default; a read filter on the event's time, nothing expires) returns the stored outcome; a different body is SND-AGENT-009.
- `version`. A hash of the steer fields only: state, lane, `agentPid`, `agentStartTicks`, session id and the set of reasons. Last activity, tokens, tool lines and the pane tail are not in it, so a working lane's transcript writes do not change it and Stop and Recycle do not hit SND-AGENT-002 on every try. A restart cannot make an old version match a new state, and a session that restarts under the same row id changes `agentPid`, so a confirm made on the old one is refused. `session.changed` carries this version and fires when the state file or the version changes, not on a transcript write.

**Recycle from a digest, as the owner does it.**

1. On the ideas row the owner presses `Ctrl+Enter`. The primary is Copy digest request, Home's action `home.digest`. It copies the standard line and jumps to the lane's terminal. It sends nothing, since `lane-send` refuses Claude lanes.
2. The lane writes its digest to a file.
3. The owner presses `.` then Recycle. A picker asks for the digest path (a text field and Browse). The picker prefills the path from the lane's latest `DIGEST READY <path>` message in meta's inbox (the protocol line of `skills/meta/SKILL.md:64`). The page does not scan folders: `config/digests-README.md` names `$AW_DIGEST_DIR` (default `$AW_STATE_DIR/digests/<task-slug>.md`) only as a convention, and the lane chooses the file. The path must be absolute and hold no NUL; the route passes it as one argv item after `--digest`, so a path that starts with `-` is refused (SND-AGENT-005). Before Go, the picker runs `lane-recycle --dry-run`, whose `DigestTimes` shows a digest older than the lane's session start (#2661).
4. `Enter` sends `{op: "recycle", args: {digest: "/abs/path"}}`. The route runs `aw-meta lane-recycle ideas --digest /abs/path`. The row shows "recycling" until the lane reports a new session. The row leaves Needs you when its context falls under the mark.

## 6. The page

### 6.1 Layout

At 1440x900, compact, peek closed. Frame, group header and row numbers are Home's (Home §7.1), which are design's on #2978.

```
┌ top bar 40 ───────────────────────────────────────────────────────────────────────────────────────────────┐
│ Agents   11 agents · 3 need you · 2 PRs ready to merge        / p: r: s: n: o:      3.1M tokens today     │ page header 48
│ box  mem 14.2 GB free · disk 212 GB free · heavy 2 of 3 (api, ui) · reserve 1 · 1 job                      │ box strip 32
├ Needs you 3 ──────────────────────────────────────────────────────────────────────────────────────────────┤ group header 28
│ ◔ ideas: at 210k, recycle from a digest +1  recycle   PR #3078  ideas   sindri · main   210k ▲  4m   -  20m │ row 32
│ ◔ meta: at 205k, recycle from a digest      recycle   -         meta    aw · main       205k ▲  1m   -  15m │
│ ○ qa: PR #3067 ready in the merge queue     PR ready  PR #3067  qa      sindri · qa-x    88k    9m   -  12m │
├ Working 7 ────────────────────────────────────────────────────────────────────────────────────────────────┤
│ ◔ design: building RowList                  working   PR #3101  design  sindri · ds-1   121k    6s   h1  3h │
│   ▸ 2 builders                                                                                            │
│ ◔ ui: Agents rows                           working   #3018     ui      sindri · ui-3   150k   14s   -   1h │
│ ...                                                                                                       │
├ Idle 1 ───────────────────────────────────────────────────────────────────────────────────────────────────┤
│ ○ api: idle 31 min                          idle      -         api     sindri · api-2   61k   31m   -  31m │
├ Other 1 ▸ ────────────────────────────────────────────────────────────────────────────────────────────────┤
├ Recently ended 4 ▸ ───────────────────────────────────────────────────────────────────────────────────────┤
├ j/k move · Space peek · Enter open · r reply · o jump · x stop · . actions · / filter · ? ────────────────┤ footer 28
└───────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Row (32 px compact, 40 comfortable):** `[glyph 16] [title, flex, min 200] [why chip 120] [object 88] [lane or parent 112] [repo · branch 160] [context 64] [last activity 48] [heavy slot 40] [age in state 44, right-aligned]`, 12 px gap. The why chip carries the primary reason word or the state word. The object is the mono ref (`PR #3078`, `#3018`). The context cell shows k tokens, and "▲" when the session is at or over the recycle mark. Title is the only 13 px ink text. "Waits on you" rows weigh 500. "-" marks a fact the source does not give; it never shows a guess.
- **Width check:** the fixed part is 692 px of columns plus 108 px of gaps plus 48 px of side inset: 848 px. The title gets `list width - 848`: 592 px at 1440 with the peek closed. With the peek open the list is 1000 px, and the title would get 152 px, under its 200 px minimum. So the columns drop in this order as the list narrows: repo · branch (below 1048 px), heavy slot (below 876), last activity (below 824), lane or parent (below 764). At 1000 px the repo · branch column is gone and the title gets 324 px. The title minimum never shrinks. Facts end in an ellipsis, never a mid-word clip.
- **Rows per screen (D8):** body 784 minus 8 padding, minus the box strip (32), minus 5 group headers (140) leaves 604 px: 18 rows at 32 px, 15 at 40 (rounded down). The 11 lanes of the walk fit with 7 rows to spare. Builders folded under their lead take no row until opened.
- **Head:** the count text is "N agents · M need you · P PRs ready to merge". M counts rows in Needs you, not sessions asking. P counts merge-queue entries with no GitHub queue state. The tab's nav count uses M. `Labels.Agents.cs:45` ("N asking you") is replaced.
- **Filter box:** the `p:` `r:` `s:` `n:` `o:` tokens of #3024 (Claude's view uses the same tokens; ledger 7). `s:` takes `needs`, `working`, `idle`, `limited`, `down`. `Esc` clears. The token parser is a plain function that moves to the new page unchanged.
- **Builders:** a lead with builders shows "▸ 2 builders" under its row. Right opens it, Left closes it. A folded group still counts in the head.

### 6.2 The box strip

One line under the header. It is on the page, not a separate sheet (#3018 done 5).

`box  mem 14.2 GB free · disk 212 GB free · heavy 2 of 3 (api, ui) · reserve 1 · 1 job`

- **mem** is `MemAvailableKb`, the same number the 8 GB memory floor uses. **disk** is `DiskFreeBytes` (`HostSnapshot.cs:8`).
- **heavy** is slots held of the cap (`HeavySlots.Cap`, `HeavySlots.cs:11`) with the holders' lanes (from `HeavyLock.Holder`, `HeavyLock.cs:8`). **reserve** is the meta gate reserve (`HeavySlots.Reserve`).
- **job** counts detached jobs with a pid and a log. Click opens a read-only list: name, pid, age and log path. v1 has no Stop on a job row: a job has no start ticks, so a reused pid could be killed, and the pid can be the outer `heavy` wrapper (Round 3). A job stops through the op of the session that owns it.
- A value over its limit turns to ink-bold with the word ("mem 3.1 GB free: low"), never colour alone. The limits are `aw-meta`'s (the 8 GB floor in the team memory rules); the page reads the verdict from `box.health` and sets no limit of its own.

### 6.3 Peek (Space) and full (Enter)

Peek is a 440 px pane on the right, as on Home. `j/k` moves it with the cursor. It paints within 50 ms from the local store, with no spinner. Header 48 (title, ref, open-full, close), fields in two columns of 28 px rows, a body, 2 to 3 actions at the bottom. It shows the first 3 facts when narrow (the forced rule).

Fields on every row: State and since, Context (210k of the 200k mark), Lane items (the PRs and issues it owns), Heavy slot (held or waited, with age), Last activity, Parent or builders, Links (Home row, PR, workflow run).

Body:
- **If it is asking:** the pending question, from the pane tail (`IPaneReader`, tmux only, 40 lines in 1 s). Another host says why: "Warp: can't read this terminal. Jump to it." The hook stores no free text (`fleet-state.sh:7`), so there is no other source.
- **Always:** "What it did", the last 20 tool calls, each folded to one line (`Edit src/X.cs · Bash dotnet build · Read Y`) with its age, and "Files changed" with +/- counts. This is api A4, scrubbed by `Aw.Redaction`.
- **For a reason that has words:** all the reasons, one per line, with their entered time.

**Full** (`Enter`) opens the session sheet (`SessionSheetView`) with the transcript and its search. `Esc` returns with the cursor on the same row.

```
┌ peek 440 ─────────────────────────────┐
│ ideas                       ↗ open  ✕ │ header 48
│ at 210k, recycle from a digest        │
├───────────────────────────────────────┤
│ State        working · 4m             │
│ Context      210k of 200k mark        │
│ Lane items   PR #3078 ready, #3014    │
│ Heavy slot   none                     │
│ Last active  6 s ago                  │
│ Reasons      at the mark (08:50)      │
│              PR #3078 ready (08:30)   │
├ What it did ──────────────────────────┤
│ Edit docs/…/agents-page-design.md  6s │
│ Bash  scripts/check-prose.sh      40s │
│ ... (20 lines)                        │
│ Files: 3 changed  +212 −18            │
├───────────────────────────────────────┤
│ [Copy digest request]  [Recycle…]     │
└───────────────────────────────────────┘
```

### 6.4 States

Every error row has a button that fixes it.

| State | What shows |
|---|---|
| first open, no store yet | header and footer paint at once; 6 skeleton rows for at most 400 ms, then the rows or the empty state |
| loading from a slow serve | the cached first page paints first (D7); a quiet header line "Updating…" is a polite live region |
| serve not running | the cached rows paint; the header says "Sindri isn't running · showing 09:02" with **Start Sindri**; steers are disabled with the same reason |
| live updates down (the event stream dropped) | the header says "Live updates paused · checking every 5 s" with **Reconnect**; the 5 s poll (today's) runs as the fallback |
| no sessions | "No agent sessions on this box." under the head; the nav count is 0 |
| Needs you empty | the group is not drawn; the head reads "11 agents · 0 need you". Nothing says "You're done": that is Home's word |
| `aw-meta` has no team here | no lane facts; one quiet line "No aw-meta team on this box: context, queue and heavy slots are not shown", no button |
| `aw-meta` unreadable (binary missing, exit 2, bad JSON) | the lane facts show "-"; the header says "aw-meta unreadable: lane context and queue are stale since 09:02" with **Retry** and the log path (`lanes-unavailable`) |
| a source is stale (not read in 60 s) | the rows from it carry the muted age; the header names it |
| heavy waits not readable | the box strip says "heavy: holders only, waits not readable yet" until tools ships `aw-meta heavy` |
| a session's state is a guess (`estimated`) | the chip reads "No recent activity" with the age; Details names the source in words ("from the transcript's age") |
| the terminal can't be seen | the chip reads "Can't see this terminal" and the Jump action reads "Copy attach command" |
| a steer is sending | "sending" in the waits-on cell; after 2 s "still sending" |
| a steer failed | "failed: <reason>" with **Retry** and **Discard**; for a refused op the reason is the op's sentence (§5 table) |
| a row's version changed under an edit (SND-AGENT-002) | the edit is not applied; the row shows "Changed while you were editing" and **Reload** |
| a lane is recycling | "recycling" until the lane reports a new session; no steer is offered |
| New band has rows | the "New (n)" header; the window title shows "Agents (n new)" for screen readers |
| stop confirm open | §6.5 frame |
| the peek body can't load | the fields from the local store and "Couldn't load the activity" with **Retry** |

### 6.5 Steer frames

Reply and message composer. It opens in the peek, `r` focuses it. With focus in the composer, `Ctrl+Enter` sends; with focus in the list or the peek body, `Ctrl+Enter` opens the submit menu (§6.6). The title says "Answer" when the session asks a question and "Message" otherwise.

```
┌ Message design (non-Claude lane) ───────────────────────────┐
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Rebase on main, then run the unit lane.                 │ │
│ └─────────────────────────────────────────────────────────┘ │
│ lane-send --lane design                   Ctrl+Enter  Esc   │
└─────────────────────────────────────────────────────────────┘
```

The frame shows a non-Claude lane (`design`); `ideas` is a Claude lane and gets the text below. For a Claude lane the composer is replaced by this exact text: "Claude lanes take mail through SendMessage. aw-meta can't send to this lane." with **Copy message** and **Jump to terminal**.

Stop confirm. `x` opens it. It shows the pid and what ends.

```
┌ Stop ideas? ──────────────────────────────────────────┐
│ Ends Claude Code session ab12cd34 (pid 4121).         │
│ Its worktree and branch stay. A stop is not a recycle.│
│                                    Esc Cancel  Enter Stop │
└───────────────────────────────────────────────────────┘
```

Recycle picker. `.` then Recycle opens it.

```
┌ Recycle ideas from a digest ──────────────────────────┐
│ Digest file   /home/me/aw-wt/ideas/digest.md  [Browse] │
│ Runs: aw-meta lane-recycle ideas --digest <path>      │
│ The lane /clears and continues from the digest.       │
│                                  Esc Cancel  Enter Go │
└───────────────────────────────────────────────────────┘
```

Outcome lines, exact text: "Sent to ideas." / "Stopped ideas (pid 4121)." / "Recycling ideas…" / "Can't stop: the process changed since this row was read. Reload." (SND-AGENT-004) / "Can't recycle: ideas wrote no digest at that path." (SND-AGENT-005).

Jump (`o`) focuses the session's terminal (tmux pane, Warp tab, IDE window) when Sindri can. `SessionFocus` says every row is "not-built" until CO-4a (`Core/Sessions/SessionFocus.cs`, #3023), so Jump copies the attach command and says so until api builds focus. It writes nothing.

### 6.6 Keyboard and focus

The grammar keys are the same on every list (#2978): `j/k` and arrows, `Home`/`End`, `/` filter, `Space` peek, `Enter` open, `Esc` back one layer, `.` actions, `?` keys, `Ctrl+K` palette. **A bare key never writes to a source and never starts work.** Agents adds these after the divider in the footer:

| Key | Does | Writes? |
|---|---|---|
| `r` | opens the reply composer in the peek (Answer or Message) | no, until `Ctrl+Enter` |
| `o` | jumps to the terminal, or copies the attach command | no |
| `x` | opens the stop confirm | no, until Enter in it |
| `y` | copies the resume command (ended row) or the attach command (live row) | no |
| `R` | re-rank now (a settle point) | no |
| `Ctrl+Enter` | opens the submit menu with the row's primary preselected; Enter commits | the target, on commit |
| Left / Right | the tree convention: on a header Left folds and Right opens; on a lead row Right opens its builders, Left folds them | no |

Recycle has no bare key: it needs a path, so it lives in `.` and the palette. Left and Right replace today's `E` for the ended fold (`Keys.cs:60`), so the page has no key that means "edit" or "done" and the `e` of Home never clashes. The page does not bind `e`, `h`, `t`, `[`, `]` or `b`, so the Home meanings stay Home's. Bare `g` belongs to the shell's G chord (`ShellViewModel.Chords.cs`): `G` then a page key within 1 s, and a pending chord takes its second key before the list sees it. Letters work only while focus is in the list or the peek and not in a text field.

Today's `J`, `K`, `Enter`, `O` and `Y` (`Keys.cs:56-64`) keep their meaning. `O` today opens the frame of the Step it works on; that moves to the `.` menu as "Open the step".

Every op has a menu item and a palette entry under one id: `agents.peek`, `agents.reply`, `agents.message`, `agents.answer`, `agents.jump`, `agents.recycle`, `agents.stop`, `agents.copy-attach`, `agents.rank`, `agents.fold`, `agents.unfold`, `agents.retry`, `agents.discard`. A reason that Home owns reuses Home's id (`home.digest`, `home.merge`, `home.snooze`). A button with no palette entry is a bug.

Screen readers: the list is a list of groups of rows. A row reads "needs you, ideas, at 210k, recycle from a digest, plus one, working, 4 minutes". The New band is announced once as "1 new row". Reduced motion turns the row removal into an instant change.

### 6.7 Notifications

The tray count and the native alert are Home's (Home §6.3), not a second badge. A session that enters Needs you with a permission prompt, an owner question or a login is Home's `agent-asking` row, so it alerts once per episode, and a click opens that row's peek. A failed session (#3026) alerts too, once the api adds the `failed` change. Lane at the mark, usage limit, no session, idle with work owed, slot wait and PR ready never alert: they are on the page and in the head count, and they are not urgent to the second. Per-session mute is Home's mute, on the same row id. Nothing repeats while the state holds: `fleet.changed` already reports asking only after it has held for 5 s (`Core/Sessions/FleetChangeRules.cs:6,123`), which is the alert's debounce and does not delay the page (§7).

## 7. Live updates: 1 s with 50 sessions

### 7.1 Why the page can't meet 1 s today

Two waits stack. The page polls every 5 s (`AgentsPageViewModel.Poll.cs:10`). The server collector also reads on a timer: `SessionCollectorHost` runs a pass every `settings.PollSeconds`, clamped to 1 to 30 s, with a 500 ms budget (`Host/Server/SessionCollectorHost.cs:19-20,28`). `PollSeconds` defaults to 2 (`SessionSettings.Defaults`, `Core/Sessions/SessionSettings.cs:13`; `LauncherStartup.cs:40` falls back to 2). So the snapshot can be stale by up to 2 s, and the page can be stale by up to 5 s on top: 7 s worst case, before any read time. A faster page poll would not fix the first wait. This is perf's finding on #3018, plus the server timer.

`fleet.changed` does not help. It reports only `appeared`, `asking` (after 5 s) and `gone` (`Core/Events/FleetChanged.cs:19`, `FleetChangeRules.cs:6`). It says nothing for a session going from running to idle.

### 7.2 The push

1. **Watch the folder.** Serve watches `$AW_FLEET_DIR` recursively (files `<provider>/<sessionId>.json`). The existing `IFolderWatch` watches one file name in one folder (`Host/Server/IFolderWatch.cs:11`), so api adds a second port, `IFleetWatch`, with a recursive watch. As `IFolderWatch` says, a watch is a trigger only, because watchers miss events on some disks (`IFolderWatch.cs:3`). The full collector pass stays as the safety net.
2. **Re-read one session.** On a change, after a 50 ms debounce, the collector re-reads that one state file, re-merges that one session (`SessionStateRules.Merge`), and checks its pid and start ticks with one read of `/proc/<pid>/stat`. It does not walk the process table.
3. **Publish.** It raises a new public event `session.changed` with `{provider, sessionId, version}` and no text, as `fleet.changed` has none. It invalidates the same status view (`Core/Events/EventCatalog.cs:98`).
4. **Re-read one row.** The page, subscribed to the event stream, calls `GET /sindri/sessions/{provider}/{id}` and patches that row. It does not re-read the list.

Time budget (a plan, not a measurement; perf measures it):

| Step | Budget |
|---|---|
| file change to watcher callback | 20 ms |
| debounce | 50 ms |
| one-file read, merge, `/proc` check | 50 ms |
| publish and stream to the app | 20 ms |
| row read and patch | 60 ms |
| paint | 16 ms |
| **total** | **about 220 ms**, leaving 780 ms of slack under D5 |

Facts that come from `aw-meta` (context, usage, queue, heavy) arrive by `aw-meta events --after N`. Those producers write on their own cadence, so they carry no 1 s promise. The service reads the cursor every 2 s (agent default). One `events` call is cheap by construction (PR #3103, merged, read not run): it starts one process, needs no team config (`NeedsConfig: false`), and runs one indexed `SELECT ... WHERE id > $after ... LIMIT 500` on `meta.db`; it prunes only on append. **Events are edges (`context.high` then `context.ok`), not state, so the cursor is only ever set after a snapshot.** At start, on a restart of serve, when `gap` is true, or when the cursor is above the log's newest id (a recreated `meta.db`): (1) read to the end of the log and keep the last id as the cursor, dropping those facts; (2) take the snapshot from `status --json`, `lanes-context --json`, `usage --json` and the other readers; (3) apply only events after that cursor. The New band stays quiet during the rebuild. `next` on a gap response is the 500th retained event, not the newest, so the service loops until `next` stops moving instead of resuming from it; the clean fix is a `latest` field in #3110, which this spec asks for. The cursor lives in serve's memory, because a restart takes the snapshot path anyway. A queue entry or a context mark shows within 2 s plus the producer's write. The fallback is not cheap: `lanes-context`, `usage` (which reads panes and transcripts, `UsagePane.cs`) and `status` are three process starts that each load the team config. So until the emitters ship, the fallback ticks every 15 s (agent default). When `events --json` reports `gap` true (cursor older than the 7-day retention, #3110), the service re-snapshots from `status --json` and the other readers, then resumes from `next`, and the head says "lane facts from 09:02" when older than 30 s. D5's 1 s promise covers session state only (§7.2 steps 1 to 4).

The 1 s push needs the opt-in fleet hook (`./setup.sh --with-fleet` or `scripts/install-fleet.sh`). Without it the page runs on the 5 s poll, `sindri doctor` shows a `fleet hook` row as warn, and the page head says "live updates off: fleet hook not installed".

### 7.3 How perf measures it

Perf's plan on #3018, kept: seed 50 fleet files from `tests/fixtures/fleet-input/` under a scratch `AW_FLEET_DIR` (49 idle, 1 changing) with 50 sleeping processes as the pids; start serve and the debug `sindri-ui` on the scratch home; loop 20 times: write a new `state` into the changing file, record `t0`, poll the row's glyph text with `aw-ui-driver props` and record `t1`; subtract the median driver call cost. Report p50, p95 and max at 50 and at 200 sessions, plus the server time of one single-file re-read and the UI-thread time of one patch. Budget: p95 under 1 s at both sizes. The before number is today's poll.

## 8. API, tools and engine requirements

Routes (all under `/sindri`; names settled by A0, §14):

| Route | Does |
|---|---|
| `GET /sindri/sessions` | today's list, plus `AgentRow` fields (§3.1) and `reasons[]` once the session-rows PR ships (meta ruled: no early `reasons[]` on today's route) |
| `GET /sindri/sessions/{provider}/{id}` | one row; the page's single-row refresh |
| `GET /sindri/sessions/{provider}/{id}/activity` | the peek body: 20 folded tool calls, files changed, the pending question when readable; scrubbed |
| `POST /sindri/rows/{id}/ops` | `{op, version, actId, args}`; §5 |
| `GET /sindri/agents/box` | memory, disk, heavy holders and waiters, reserve, jobs |

Requirements by area:

- **Reasons (api, Core).** `AgentReasons` is a pure function in `Aw.Sindri.Core.Sessions`: a session's state, the lane's context verdict, usage state, queue entries, obligations, health text, heavy waiters and the thresholds in; the ordered reasons out. The §4.3 example and one fixture per reason are its golden tests.
- **Session rows (api).** Build `AgentRow` on top of `SessionDiscovery`, not a second collector. Add the builder-to-lead parent, the PR or issue, the context k, and the last activity from the transcript write.
- **Lanes collector (api, engine).** One service reads `aw-meta` (events first, `lanes-context`/`status`/`usage` as the fallback) for both Home and Agents. It never calls `merge-run`, `gate-run` or `lane-recycle`. The steer route does that. `lanes-context` and `usage` exit 1 when something needs attention and still print valid JSON, so the reader parses stdout; a missing binary, exit 2 or bad JSON raises `lanes-unavailable` (Home §8). The verdict is PascalCase in the JSON (`"verdict":"Ok"`), so the reader matches `Recycle` exactly.
- **Steer adapters (api).** One per op in §5. Each takes the `actId`, checks whether the op already landed (a stop checks the pid is gone; a recycle checks the lane's session id changed), and returns the target's reason on refusal. `aw-meta` and other CLIs are called with an argument list, never a shell string.
- **Stop (api).** Read `agentPid` and `agentStartTicks` from the same collector pass as the row, send SIGTERM, refuse when the ticks changed (SND-AGENT-004). No process-group kill and no pattern match.
- **Events (api).** `session.changed` (§7.2), and the `failed` kind on `fleet.changed` for #3026. Both join `EventCatalog.cs`. `EventCatalogTests` and the API goldens (Lane=integration) go in the gate.
- **Profile keys (api).** `agents.idleOwedMinutes` (integer 1 to 1440, default 10), `agents.slotWaitMinutes` (1 to 1440, default 30), `agents.page` (`rows` | `legacy`, default `legacy` until U3). An out-of-range value is SND-AGENT-010 with the key path. They take rows in `docs/csharp/sindri-profile.md`, and `sindri profile set` accepts them.
- **tools.** The events and readers of §3.3; `docs/csharp/meta.md` and `schemas/meta/` list each kind and its fields, so api codes against a contract, not text.
- **engine.** The node states, waiting-on text, last tool and links of the engine comment on #3018, as ledger events. The steer verbs `cancel`, `answer`, `message` and `resume`.
- **Text safety (api).** Session text (titles, tool lines, the pane tail) goes through `Aw.Redaction` before it enters the ledger, and is fenced as untrusted in agent results. A message sent to a session drops control characters except newline and goes as one literal paste with no trailing Enter. The `ops` route needs the per-start human token for an app or terminal caller, and rejects a browser `Origin`.
- **Observability (engine).** `sindri doctor` gets a row for the lanes collector and the fleet watch (last read, last error). The same change updates `src/Sindri/aot-smoke/expected.txt`.

### 8.1 Agents (the CLI and MCP)

Everything the page does, an agent can do. `sindri agents` is a client of `GET /sindri/sessions` and never starts serve (`Host/Commands/AgentsVerbs.cs:15`).

| Verb | Arguments | Does | Refused with |
|---|---|---|---|
| `agents` | `[--needs-you] [--state S] [--lane L] [--provider P] [--repo R] [--json]` | rows in page order; `--needs-you` lists only that group, with `reasons[]`. `--provider` and `--repo` are today's flags and stay. `--state` takes the page names `needs` (the group), `working`, `idle`, `limited`, `down` and `ended` (the Recently ended list). Today's `running` maps to `working`, `asking` maps to `waiting-on-you` (one state; `needs` is the wider group), and `idle` and `ended` keep their meaning. An alias prints one stderr line. The server filters by `SessionState` (`unknown`, `running`, `asking`, `idle`); the CLI maps the page names to a server filter and a client-side group. `AgentsVerbs.Query` today reads value pairs only, so A0 changes the parser for the bare `--needs-you` and the new `--lane`, changes `Usage`, and runs the full Sindri unit lane. `agents show` keeps exit 2 for not-found (SND-SESSION-404) for one release | none |
| `agents show` | `<provider>/<id> [--json]` (today's form; `session:<provider>/<id>` is accepted too) | one row, its reasons and its activity | 001 |
| `agents box` | `[--json]` | the box strip | none |
| `agents steer` | `<row id> --op message\|answer\|recycle\|stop [--args JSON] --version N [--act-id ID]` | one op, the same call as the page. `<row id>` is the §3.1 id (`session:claude-code/ab12`, `lane:team/ideas`). Without `--act-id` the CLI makes one and prints it; a retry after a timeout passes the same id, so a message is not sent twice (SND-AGENT-009 on a changed body) | 001, 002, 003, 004, 005, 007, 009, SND-AUTH-002 |

Over MCP the tools are `agents_list`, `agents_show`, `agents_box` and `agents_steer` at `/sindri/mcp`. A session reads the rows of the repos its `X-Sindri-Repo` header names, and the text is fenced as untrusted. `agents_steer` is refused for every session caller in v1 (§5 "Who may steer"); it is listed so the contract is complete. Exit codes follow the context family: `0` ok, `1` attention (a refusal, Sindri not running), `2` a tool or usage error.

## 9. Errors

Reserved now: SND-AGENT-001 to 029. No `SND-AGENT-` code exists on main (`git grep SND-AGENT` finds none).

| Code | Meaning | HTTP | CLI exit |
|---|---|---|---|
| 001 | row not found | 404 | 1 |
| 002 | the write names an old row version; carries the current one | 409 | 1 |
| 003 | the op does not apply to this row; carries the reason sentence (for example "Claude lanes take mail through SendMessage") | 400 | 1 |
| 004 | stop refused: the pid or start ticks changed since the row was read | 409 | 1 |
| 005 | recycle refused: no digest, the path is unreadable, not absolute, over 4,096 characters, starts with `-`, or is older than the lane's session or than 2 h (AW-META-021) | 400 | 1 |
| 006 | `aw-meta` is unavailable (no team, no binary, exit 2) | 503 | 1 |
| 007 | message empty or over 8,000 characters | 400 | 1 |
| 008 | the activity could not be read (transcript missing or locked) | 502 | 1 |
| 009 | an earlier op on this row is still sending, or an `actId` was replayed with a different body | 409 | 1 |
| 011 | a recycle job ended without a new session inside its wait (AW-META-022) | n/a (job outcome) | 1 |
| 010 | an `agents.*` profile key is unknown or out of range; carries the key path | n/a | 2 |

Each has a sentence for the app and a `fix:` line for the CLI. `degraded[]` entries on the list are data: `{code, detail}` with `lanes-unavailable`, `source-stale`, `heavy-waits-unreadable`, `behind`.

## 10. Goals and non-goals

Goals: the job in §0, the done-checks in §1, the page in §6, live updates in §7, the agent surface in §8.1.

Non-goals:
- Starting a new session from this page. Hand to agent is Home's and the row actions' (ledger 12).
- Typing into a tmux pane. Meta ruled it out.
- Pause, resume and archive of a worktree (ledger 5, Claude Squad's idea).
- A cost dashboard. #3025 adds columns and a head total; the page does not add a second screen.
- Stuck detection beyond the two rules in §3.2. The ledger found no prior art, and a third rule needs a real case first.
- A second inbox. Home stays the one inbox; this page is where the owner works the sessions.

## 11. What happens to today's views

| Today | After |
|---|---|
| `Views/Agents/AgentsPage.axaml` and `.cs` | rewritten on `RowList` (design's P0 control, built for Home) |
| `Views/Agents/SessionGroup`, `SessionRow`, `EndedSessionRow` | replaced by `Row` and the group header; deleted in U6 |
| `Views/Agents/SessionSheetView` | kept as the Full view (transcript and search) |
| `ViewModels/Agents/AgentsPageViewModel*.cs` (`.Rows`, `.Keys`, `.Filters`, `.Poll`, `.Counts`, `.Layout`) | `.Keys` replaced by `RowKeys`; `.Filters` becomes the `p: r: s: n: o:` token parser; `.Poll` becomes the fallback timer only; the rest folds into the row model |
| `AgentColumns` | the fixed column set of §6.1 |
| `Labels.Agents.cs:45` "N asking you" | the head text of §6.1 |
| the `E` key | Left and Right on a header |
| `GET /sindri/sessions` and `sindri agents` | kept; they gain fields |

## 12. Gaps found

From a read of `origin/main` 7c81646696 and the issues (no build, no app run):

| What | Where | Matters because | Fixed in |
|---|---|---|---|
| The hook records no last tool and no last activity for a running session | `fleet-state.sh:10-11,52-57`; schema `tool` is for approvals only (`fleet-state.schema.json:30-35,55`) | api's plan reads "the hook's `at` is the last event". It is the state start. | §3.1 note |
| `lane-send` refuses Claude lanes, but meta's decision 3 says Message goes through it for a team lane | `LaneSendService.cs:52`, `MetaErrors.cs:57` | Message would fail for the lanes the owner uses most | §2.7, §5, question 2 |
| #3022 asks for a reply by tmux `send-keys`; meta says no typing into a pane; the engine says a permission prompt is answered in an attended session | #3022, #3018 comments | three plans disagree on who may answer | §2.6 to 2.8 |
| #3023's "undo before the pid is stopped" cannot undo a signal | #3023, ui plan item 4 | a promise the page can't keep | §2.9 |
| `fleet.changed` reports asking only after 5 s and never a running-to-idle change | `FleetChangeRules.cs:6,123` | a push built on it would miss D5 | §7.2 |
| `IFolderWatch` watches one file name in one folder | `IFolderWatch.cs:11` | the fleet folder holds a file per session in a folder per provider | §7.2 |
| The collector pass runs every `PollSeconds` (1 to 30 s), so the snapshot lags before the page does | `SessionCollectorHost.cs:28` | perf's 5 s figure leaves this out | §7.1 |
| `aw-meta heavy`, `locks/heavy-wait/` and `aw-meta jobs` do not exist | `git grep` on main | slot-wait has no source | §3.2 |
| `Obligation` has no timestamp and is the meta's own list | `Obligation.cs:8`, `ObligationKind.cs` | "work owed" to other lanes has no source except the gate verdict | §3.2 |
| The code default recycle mark is 300,000; the owner's box sets 200,000 (checked with `aw-meta lanes-context --json`) | `ContextConfig.cs:5` | the worked example uses the box value | §4.3 |
| `SessionRow` has no parent, context, PR, last activity or slot | `SessionRow.cs:27-29` | builders can't nest and a lead can't steer "its own" | §3.1 |
| No `/sindri/rows/{id}/ops` route exists | `git grep '/ops'` | the steer has no route yet | §5 |
| `SessionFocus` is "not-built" for every row | `SessionFocus.cs`, #3023 | Jump copies a command until CO-4a | §6.5 |
| "Waiting on a tool" has no source | `fleet-state.sh:52-57` | one of #3018's six glyphs can't be drawn | §3.1, question 3 |

## 13. Proof

- The D1 to D13 numbers, before and after each phase, as a comment on #3018.
- Screenshots of every §6.4 state at 1280 and 1440 wide, the page at 1000 wide, the peek open at 1440, comfortable and compact, the New band, a failed steer, the stop confirm, the recycle picker, the empty state.
- The aw-ui-driver walk of D1 on the #3018 box state on a scratch home, with a screenshot of row 1 and the step count.
- A property test for D3 and a golden test for the §4.3 example.
- A fake `aw-meta` and a fake `claude` per op for D4: the argv, the 2 s landing, the refusal.
- A test plants a token in a session title and in a tool line: the row, the peek and the alert show it scrubbed, and agent output fences it.
- Steering is tested on a scratch team only, never on the owner's real lane sessions or home (#3018 rule).
- ux runs D13 and files each open as an issue.

## 14. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Lane | Starts | Needs |
|---|---|---|---|---|
| DG1 | frames of every §6.4 state: the six glyphs (half ring and x are new), the New band, the box strip, the stop confirm, the recycle picker, the peek; check the §6.1 width numbers | design | now | none (uses Home's P0 tokens) |
| T1 | aw-meta event store and `events` (PR #3103, merged; `oldest`/`gap` in #3110, plus `latest` asked for in Round 3) | tools | now | none |
| T2 | emitters: `context.high`/`ok`, `lane.down`/`up`, `usage.limited`/`ok`, `obligation.*`, `merge.recorded` | tools | after T1 | T1 |
| T3 | `scripts/heavy.sh` writes `locks/heavy-wait/<pid>`; `aw-meta heavy --json`; `heavy.wait.long`; `box.health` | tools | the script change now; the command after T1 | T1 |
| T4 | `aw-meta job start`, `jobs`, `job.started`/`ended` | tools | after T3 | T3 |
| A0 | the wire shape: JSON schema and API golden for `AgentRow` and `reasons[]` (scenario row, Ui `GoldenContract` slug), `tests/fixtures/agents/needs-you.json` (the §4.3 example), `AgentReasons` with its golden, the profile keys, the `SND-AGENT-` codes, PublicAPI files | api | now | the §3.1 fields |
| A1 | session rows on `SessionDiscovery`: parent, PR or issue, context, last activity, reasons; lanes collector reading `aw-meta` CLI first | api | after A0 and the #2978 rows PR 1 (meta routing: after #3052, #3049, #3050) | A0 |
| A2 | `IFleetWatch` and the `session.changed` event; single-file re-read | api | after A0 | A0 |
| A3 | the `ops` route: message, answer, recycle, stop; authorization on target | api | after A1 | A1, E2 for workflow sessions |
| A4 | `GET .../activity` (the peek body) | api | after A1 | A1 |
| A5 | `failed` on `fleet.changed` (#3026), then spend for all sessions (#3025) | api | `failed` after A0; spend last | A0 |
| E1 | node states, waiting on, last tool, links as ledger events | engine | after #2985 (their plan) | #2985 |
| E2 | the steer verbs: cancel, answer, message, resume | engine | after E1 | E1 |
| U0 | the filter box with `p: r: s: n: o:` and column sort on today's list (#3024) | ui | now (needs no new control) | none |
| U1 | the page on `RowList`: groups, glyphs, columns, the New band, held order (D3), `RowKeys`, density | ui | on A0's fixture, with a stub `RowList` until design's P0 lands | A0, P0 |
| U2 | box strip, peek, states, head count | ui | after U1 | U1, A4, T3 |
| U3 | steer UI: composer, stop confirm, recycle picker, Jump, outcome states (#3022, #3023) | ui | after U1; fake route first | U1, A3 |
| U4 | subscribe to `session.changed`; the poll becomes the fallback | ui | after U1 | U1, A2 |
| U5 | tray and alert wiring to Home's count (#3026) | ui | after U1 | U1, A5 |
| U6 | delete the old views in §11 | ui | after U4, one week of the new page default | U4 |
| F0 | perf baseline on today's 5 s poll at 50 and 200 sessions | perf | now | none |
| F1 | perf: D5, D7, D8 in the real app after the push | perf | after U4 | U4, A2 |
| X0 | the D13 script | ux | now | none |
| X1 | the D1 walk on the 11-lane box state, 1 step | ux | after U1 and A1 | U1, A1 |
| X2 | the day without the terminal (D13) | ux | after U4 | U4 |

Seven lanes are busy from day one: design (DG1), tools (T1, and the heavy.sh script part of T3), api (A0), ui (U0, then U1 on the fixture), perf (F0), ux (X0). Engine starts when #2985 lands; its first phase is the one that makes the engine's rows true, so it waits for nothing else. Nothing waits on a source that does not exist: slot wait shows an honest empty class until T3 (§6.4), and Message to a Claude lane shows its reason until question 2 is settled.

Each phase updates its docs: `docs/sindri/README.md` or a new `docs/sindri/agents.md` (how the reasons work), `docs/csharp/sindri-profile.md`, `docs/csharp/meta.md`, and the CLAUDE.md Commands entries for the new `sindri agents` verbs.

## Open questions

Each has my recommended answer. None blocks the phases that start now.

1. **May Home list idle with work owed and slot wait?** Settled: no for v1 (lead, 2026-11-12). Both stay Agents-only until the soak shows the rules hold; then Home may add an `agent-owed` kind that uses §3.2 unchanged.
2. **How does the page message a Claude lane?** `lane-send` refuses it, and meta ruled out typing into a pane. Recommend tools adds a mail path for Claude lanes that the lane reads at its next turn, the way the engine's session inbox works, and `lane-send` uses it. Until then the page offers Copy message.
3. **Is "waiting on a tool" worth a hook change?** Recommend no. Make the hook write PreToolUse and PostToolUse and you spend a `jq` run per tool call (`fleet-state.sh:10-11` avoids that on purpose). Show the last-activity age instead.
4. **Does a Claude lane have an unread-mail signal?** I found none, so "idle with work owed" ignores mail for Claude lanes. Recommend tools adds a `mail.unread` fact per lane to `aw-meta status --json`, and rule 3c uses it.
5. **Should the head count PRs or rows?** Recommend PRs ("2 PRs ready to merge"), as in §6.1. The ux walk asked for the number of PRs, and a PR is the thing the owner merges.
6. **Where does a lane write its digest?** There is no fixed folder (round 1 check). The picker prefills from the lane's `DIGEST READY <path>` message. Recommend `aw-meta` also keeps that path in the lane's status, so the prefill survives the message being read and archived.

### Review notes

Round 1 (autoplan, 2026-11-12, origin/main 4fafac85b4; files in `plans/agents-page-r1/`). Verdicts and MUST counts: product PASS (0), arch NEEDS_WORK (3), design PASS (0), devex NEEDS_WORK (2), security NEEDS_WORK (1, shared with arch). I ran the five lenses myself in one pass, not as five subagents.

Fact checks:
- `settings.PollSeconds` default is 2, not unknown. Applied (§7.1).
- `QueuedPr.From` is the sender of the latest `ready #N` message: name, else raw peer id. No lane-map lookup exists. Applied (§3.2 "PR ready: the join").
- Usage JSON: `subjects[].state` is `"Limited"`, `resetsAt` is omitted when unknown; the old text said "subject in state Limited ... UsageStop". Applied (§3.2).
- #3067 is the merged context-scope spec PR by jthorare, not a qa gate PR. The walk's sender claim is unchecked. Applied (§4.3).
- Digests: no fixed folder. `config/digests-README.md` gives a convention and the meta protocol has `DIGEST READY <path>`. Applied (§5, question 6).
- Claude-lane unread mail: still none on main. Health skips Claude lanes. Applied (§3.2).
- `aw-meta events`: was not on main; PR #3103 is now merged (Round 3 updated the text). One call is one process start and one indexed SELECT with no config load. The CLI fallback is three calls, one of which reads panes. Applied (§3.3, §7.2).

MUST (all applied):
- M1 (arch, security): a lane has no `X-Sindri-Launch`, so "a lead steers its own builders" had no caller identity. Added the `X-Sindri-Session` header rule (§5 "Who is the caller").
- M2 (arch): the PR-ready join had no source. Added the join rule and the unmatched case.
- M3 (devex): `sindri agents` flags dropped `--provider` and `--repo`, and `--state` changed vocabulary. Kept both flags, added aliases (§8.1).
- M4 (devex): `agents steer` had no `--act-id`, so a CLI retry could send a message twice. Added it.
- M5 (arch): the 2 s fallback tick would run three CLI calls (one reads panes) 30 times a minute. Fallback is 15 s until `events` ships (§7.2).

SHOULD and nits:
- Round 1: D8 arithmetic said 14 rows at 40 px; 604 / 40 is 15. Applied.
- Round 1: `Ctrl+Enter` sent in §6.5 and opened a menu in §6.6. Applied: send inside the composer, menu elsewhere.
- Round 1: SND-AGENT-005 missed a stale digest and a path that starts with `-`. Applied.
- Round 1: untagged numbers. Applied: one sentence in section 2 lists the agent defaults.
- Round 1: `agents show` and `steer` id forms differed. Applied: steer takes the row id, show accepts both.
- Round 1: head "3 need you · 2 PRs ready to merge" counts a PR twice for a reader. Not applied: the ux walk asked for both numbers, and the head defines each.
- Round 1: Enter on the stop confirm stops, so a reflex Enter after `x` stops. Not applied: x + confirm is settled; the confirm shows the pid so the owner reads it first.
- Round 1: pid reuse between the ticks check and SIGTERM. Not applied: one `/proc` read apart, and the threat model is honest mistakes.
- Round 1: a third hook state ("waiting on a tool") for the sixth glyph. Not applied: question 3 already says no.

Round 2 (autoplan, 2026-11-12, origin/main 4fafac85b4; files in `plans/agents-page-r2/`). Five separate subagents, one per lens, none shown round 1. Verdicts and MUST counts: product NEEDS_WORK (1), arch NEEDS_WORK (5), design NEEDS_WORK (3), devex NEEDS_WORK (2), security NEEDS_WORK (2). Several MUSTs repeat across lenses.

MUST (all applied):
- R2-M1 (arch, devex, product, security, design): `X-Sindri-Session` has no id source for a CLI or MCP caller, and a CLI that reads the human token is classed Human anyway. Cut "lead steers own builders" from v1; only the human token steers; it returns later with three named parts (§5, D10, §8.1).
- R2-M2 (arch, devex): the PR join compared `From` to the bare lane name, but `From` is `<team>-<lane>`. Join through `status --json` `lanes`, team-prefixed fixtures, `unmatched-sender` degraded entry (§3.2).
- R2-M3 (design): four reasons had no row action. Added the primary-action table; `x` is disabled with no pid (§3.2).
- R2-M4 (design): the composer frame showed `ideas`, a Claude lane. Redrawn for `design` (§6.5).
- R2-M5 (security): session callers read human-only fields. D12 now says "fields this caller class may see"; title, pid, tool lines and pane tail are Human only (§5).
- R2-M6 (arch): `since`, `actId` and `version` had no stored place. Placed without a new table (§5 "Stored state").
- R2-M7 (arch): numbers had a blanket tag. Each is tagged now (§2).
- R2-M8 (arch): recycle waits up to 180 s inside a 2 s write-through. It is an accepted job now, with SND-AGENT-011 and the 2 h limit (§5, §9).

SHOULD and nits:
- Round 2: `--state` alias table and the `needs` vs `asking` split. Applied (§8.1).
- Round 2: `AgentsVerbs.Query` parses value pairs only. Applied: A0 changes the parser and `Usage` (§8.1).
- Round 2: `agents show` exit 2 for not-found. Applied: kept for one release.
- Round 2: the 1 s push needs the fleet hook. Applied (§7).
- Round 2: `--act-id` lifetime. Applied: 10 min, per row and op (§5).
- Round 2: action class for `ops`. Applied: `AgentOp`.
- Round 2: digest path cap and exists check. Applied: 4,096 characters, `-` prefix, 2 h (§9 005).
- Round 2: unverified builder rows. Applied: §2.11 says A1 settles it.
- Round 2: v1 slice (U0 + A0 + A1 + U1 + head count, no steer). Not applied as a rewrite: §14 phases already order these; steer stays in A0 for the human token.
- Round 2: cut jobs list, `agents_box`, `y` key. Not applied: settled scope, revisit after D13.
- Round 2: D13 counter for Claude-lane mail. Not applied: open question 4 already owns it.
- Round 2: stop pid-source cwd check. Not applied: ticks check stands, threat model is honest mistakes.
- Round 2: 10 s timeout and skip-if-running on the 15 s tick. Applied in meaning: one tick at a time, noted for A1's collector.
- Round 2: malformed header 400, audit of actor and actId. Not applied: header cut from v1; the ledger record carries actId and never the body.
- Round 2: stale mock numbers (last active 6 s vs 4m), group count vs head count, ARIA live region, dialog focus, empty filter state, 5 s undo frame, zero-match filter. Not applied here: design polish for U1's frames review, none changes a decision.
- Round 2: the PR that matches no lane counts in the head with no row. Applied as the `unmatched-sender` degraded entry.

Round 3 (autoplan, 2026-11-12, origin/main ce9e52a4f9; files in `plans/agents-page-r3/`). Five separate subagents, one per lens. Verdicts and MUST counts: product NEEDS_WORK (1), arch NEEDS_WORK (4), design NEEDS_WORK (3), devex NEEDS_WORK (1), security NEEDS_WORK (1). Lead decisions written in: "a lead steers its own builders" stays cut from v1 (§5); `aw-meta events` is on main and a `gap` response re-snapshots (§3.3, §7.2).

MUST (all applied):
- R3-M1 (product): a lane the owner stopped had no way out of class 3. A lane stopped from the page shows "stopped from the page" until it has a session again (§3.2).
- R3-M2 (arch): `CommandHandled` has no `actId` or body hash. One new ledger event `agent.op` keyed by `actId`; the unique-key append is the claim, no per-row lock (§5).
- R3-M3 (arch): `version` hashed last activity, so Stop and Recycle would miss almost every time. It hashes the steer fields only, with `agentPid` and start ticks (§5).
- R3-M4 (arch): `gap` then resume from `next` replays stale edges. Snapshot first, set the cursor from the log's end, apply later events only; `latest` asked for in #3110 (§7.2, T1).
- R3-M5 (arch): "everything else" was a blanket default. Each `agent default` item is named, and the untagged ones were added (§2).
- R3-M6 (design): the diamond showed on working and idle rows. The glyph is the session's own state (§3.1, mock redrawn).
- R3-M7 (design): Enter meant two things. Enter opens the full sheet; the primary action is `Ctrl+Enter` and the first peek button; PR ready says "Open in Home" (§3.2).
- R3-M8 (design): grouping by workflow had no place. Workflow groups sit after Idle and before Other (§4.1).
- R3-M9 (devex): D10 said every session caller is refused, while a lane shell with the token acts as Human. The CLI refuses when its parent process has a fleet file; the matrix has that row (D10).
- R3-M10 (security): the job list's "Stop by pid" was a second kill path with no start ticks. The list is read-only in v1 (§6.2).

SHOULD and nits:
- Round 3: PR ready counted in both "need you" and "PRs ready". Not applied: D2 puts it in Needs you; the head counts rows.
- Round 3: recycle leaves the app twice (copy, terminal, back). Not applied: said plainly in §5 step 1; D13 already counts terminal visits.
- Round 3: D13 owner and miss definition, first ship, fleet hook as precondition. Not applied here: §14 phases own them; the fleet hook note stays in §7.
- Round 3: classes 3 to 7 sort by first-seen. Applied in meaning: once emitters ship they take the event's `at`.
- Round 3: `agents.page` default flips at U3 vs after U4. Applied: it flips after U4 (the §2.15 text rules).
- Round 3: no "digest is ready" cue, Jump copies only, `Other` defined twice, duplicate lane name, `n:` and `o:` tokens, Left on a plain row, mock numbers, composer states, Stop on a team lane, undo menu id. Not applied here: design polish for U1's frames review; none changes a decision.
- Round 3: `agents steer` flags (`--text`, optional `--version`), `--needs-you` exit code, `inputs` in `reasons[]`, `fix:` lines, doctor rows, A0 split, listing order. Not applied here: A0 and A1 detail; A0 is split when the phase is taken.
- Round 3: macOS pid check, meta lane `--detach` bound, `session.changed` key shape, 2 s poll with no page open. Not applied: A1 and A2 detail, no decision changes.
- Round 3: redact heavy-waiter argv and log paths, one shared field mapper for the new routes, shell-quote copied commands, bound peek reads, ledger records op and row id never the body. Not applied here: A0 to A4 detail; the field mapper and quoting are already D12 and §8 text.
