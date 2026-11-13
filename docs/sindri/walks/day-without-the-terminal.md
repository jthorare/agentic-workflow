# A day without the terminal (Agents page, D13)

This is the script for done-check D13 of the Agents page spec (`docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md`, §1 and §14 phase X0). ux writes it in X0 and runs it in X2, after phase U4. It is part of #3018.

The question the day tests: **which lane needs me?** The owner answers it from the Agents page all day. `aw-meta status`, `aw-meta health` and every tmux pane stay closed.

## The count

The result is one number: how many times the owner opened a terminal to find out which lane needs them. The target is 0.

**An open counts** when the owner runs `aw-meta status`, `aw-meta health`, `aw-meta usage` or `aw-meta lanes-context`. It also counts when they attach a tmux pane, switch to the Warp window or run `ps` to answer a question the page should answer. Examples: "is design still alive?", "who holds the heavy slots?", "is my PR in the queue?"

**An open does not count** when the page sent the owner there to act. Example: a permission prompt, where the row's primary action is Jump to terminal (§3.2). The page did its job: it said which lane and why, and the terminal is where the answer goes. The open counts again if the owner had to read the pane to learn what the prompt asks.

Each counted open is a GitHub issue, `Part of #3018`. It names the step, the question, what the page showed and what the terminal showed. One issue per cause. A second open for the same cause adds a comment.

## Rules for the run

1. Steer only a scratch team. Never send a message, an answer, a recycle or a stop to the owner's real lane sessions (#3018 rule, spec §13). Part A only reads. Part B does every steer.
2. Read the page only through aw-ui-driver or by eye. Use no `curl` and no `sindri agents` CLI. The CLI is the agent's surface (D12), not the owner's.
3. Note the time of every event and when the page showed it. A change the page shows more than 1 s late is a D5 finding, filed with perf, and not a counted open.
4. Keep the tally in `$AW_DIR/ux/d13/<date>/tally.md`, one line per event: time, step, what happened, the page's words, open yes or no, issue number. Save screenshots next to it, named `d13-<step>-<time>.png`.
5. At the end, post the count, the tally and the open issues as one comment on #3018.

## Part A: the owner's real day (reads only)

Run it on the owner's box during a normal working day with the full team, 8 hours or more. Open the app on Agents at the start, then keep it open in one window. Each step below is a moment that happens on most days. When one doesn't happen on its own, Part B drills it.

| # | When | What happens on the box | The page must show (spec) | The terminal it replaces |
|---|---|---|---|---|
| A1 | Morning, first look | The team ran overnight. Some lanes sit at the recycle mark, PRs wait in the merge queue, one lane may be asking. | Row 1 is the session that needs the owner most, with its reason in words. The head reads "N need you · M PRs ready to merge". One step from opening the tab (D1, §4.3). | `aw-meta status`: the `owed`, `merge queue` and `context:` lines |
| A2 | Morning | A lane is at the recycle mark, e.g. "meta 201k (recycle)". | Its row in Needs you: "meta: at 201k, recycle from a digest", chip `recycle` (class 4). | the `context:` line of `aw-meta status` or `health` |
| A3 | Any time | A lane sends `ready #N <sha>` and meta queues it. | "ideas: PR #N ready in the merge queue", chip `PR ready` (class 7). The row's action opens the PR's Home row. The reason drops when meta starts the merge. | the `merge queue:` lines of `aw-meta status` |
| A4 | Any time | A lane asks for a permission prompt. | "qa: asking to run Bash", chip `asking` (class 1), in the New band. One tray alert for the episode (D11). Jump to terminal goes to the right pane. | attaching panes one by one to find the prompt |
| A5 | Any time | A lane has been idle for 10 min while its PR failed its gate. | "qa: idle 14 min, PR #N failed its gate", chip `work owed` (class 5). | `aw-meta status`: the `gate` column of the merge queue |
| A6 | Midday | Builds pile up: 3 of 3 heavy slots held and jobs waiting. | The box strip: "heavy 3 of 3 (api, ui, perf)" with the holders' lanes. A waiter of 30 min or more is a row in Waiting (class 6). Until T3 ships, the strip says "holders only, waits not readable yet". | the `heavy lock` lines of `aw-meta health` |
| A7 | Midday | Memory drops under the 8 GB floor (meta sends MEMORY LOW). | The box strip: "mem 4.3 GB free: low", in ink-bold with the word, from `box.health`. | the `memory:` line of `aw-meta health` |
| A8 | Any time | A lane's usage hits its limit. | A row in Waiting: "tools: hit its usage limit, resets 14:00" (class 2). | `aw-meta usage` |
| A9 | After an hour away | Several things changed while the owner was in a meeting. | The New band holds what entered Needs you, oldest first. Sessions that ended are in the folded Recently ended group. No row moved except by a state change (D3). | `aw-meta status` plus a pane-by-pane scan |
| A10 | End of day | The owner wants to close the laptop. | Needs you is empty, or holds only what can wait. The head says so. The owner trusts it enough to close without a last `aw-meta status`. | the last `aw-meta status` before leaving |

A10 is the hardest step. The day passes only if the owner closes the laptop without a last look in the terminal.

## Part B: drills on a scratch team (steers and rare events)

Run Part B on a scratch Sindri home and a scratch meta team, never the owner's. Set it up like this:

1. Make a scratch Sindri home under `$TMPDIR` and start it with `scripts/sindri-ui-fixture.sh up` (`AW_SINDRI_HOME` set to the scratch home).
2. Load the sessions fixture with `scripts/sindri-ui-fixture.sh sessions up`. Its `ask <pid>`, `end <pid>` and `back <pid>` verbs move a session between asking, ended and live.
3. Make a scratch meta team on a scratch clone: `aw-meta team-init --team d13 --repo <scratch clone> --lanes api,ui,ideas,qa,tools,design`. The lanes run no agents. The drills feed the page through the fake `aw-meta` and the fake adapters from phases A1 and A3 (`tests/fixtures/agents/needs-you.json`).
4. Open the scratch app with `AW_UI_DRIVER=1` and drive it through aw-ui-driver.

Each drill has a pass line. A drill that needs a source or an op that isn't built yet is "blocked by <phase>" in the tally, not a counted open.

| # | Drill | Do | Pass |
|---|---|---|---|
| B1 | Recycle from a digest | On a lane at the mark: `Ctrl+Enter` (Copy digest request), write a digest file, `.` then Recycle, check the prefilled path, Go. | The fake `aw-meta` receives `lane-recycle <lane> --digest <abs path>`, the same argv as the CLI's (D4). The row shows "recycling", then leaves Needs you. |
| B2 | Stop a runaway session | Focus a session row with a recorded pid, press `x`, then Enter. Then run the same stop after restarting that pid. | The first stop sends SIGTERM to the recorded pid. The second is refused with SND-AGENT-004 and the row says why. Nothing is matched by pattern. |
| B3 | Message a non-Claude lane | Reply to a Codex or Cursor lane's row and send. | The fake `aw-meta` receives `lane-send --lane <lane> --text <text>`. The row shows "sent", or "Stored for <lane>: the lane reads it at its next turn" when the lane hasn't run its hooks yet. |
| B4 | Message a Claude lane | Reply to a Claude lane's row. | The page sends nothing. It says "Claude lanes take mail through SendMessage" and offers Copy message (decision 7). |
| B5 | A lane dies | End a lane session with `sessions end <pid>`. | "design: no live session", chip `no session` (class 3). The primary copies `scripts/meta-launch.sh --lanes design`. After `sessions back <pid>` the row leaves Needs you. |
| B6 | A prompt comes and goes | `sessions ask <pid>`, then answer it (`sessions back <pid>`), five times in a row. | One tray alert per episode, and the tray count equals Home's count (D11). No row above or below moves (D3). |
| B7 | Serve goes down | `sindri stop` with the page open. | The page keeps the last rows and says how old they are. It never shows a bare "Unknown" (D9). It comes back with no restart of the app. |
| B8 | Live updates off | Run with the fleet hook not installed, on the scratch home only. Check the banner and the button. Don't press it against the owner's provider config: `install-fleet.sh --uninstall` there takes the real lanes' 1 s push away for the day. | The page says live updates are off and offers **Turn on live updates** (A6). Rows still update on the 5 s poll. |
| B9 | A steer that fails | Make the fake `aw-meta` time out on a recycle. | The row shows "failed: <reason>" with Retry and Discard within 2 s. Retry sends the same `actId`. |

## What "done" looks like

X2 passes when Part A's count is 0 for one full day and every Part B drill passes or is blocked by a named phase. The comment on #3018 has:

- the count: "D13: 0 opens over <hours> h, <events> events" (or the opens, each with its issue);
- the tally, with the page's words for each event;
- one screenshot per Part A step that happened, and one per Part B drill;
- the blocked drills, each with the phase that unblocks it.

## Before the page: what today's Agents page answers

This is a reading of `origin/main` 7c81646696 (spec §0), not a run. It sets the "before" number for #3018.

| Step | Today |
|---|---|
| A1 | no: the page groups by workflow and the head counts only `asking` ("11 agents · 0 asking you"). The #3018 walk took 5 steps and ended in a terminal. |
| A2 | no: the recycle mark is not a state. |
| A3 | no: the page has no merge queue. |
| A4 | partly: `asking` shows, on a 5 s poll, with no Jump. |
| A5, A6, A7, A8 | no: none of these sources reaches the page. |
| A9 | no: no New band, and a one-shot `judge-cli` row moves every row below it (#3082). |
| A10 | no |
| B1 to B9 | no: nothing steers. Only the heartbeat and exit routes write. |

So today the owner opens the terminal at least 9 times a day: once for each Part A step but A4, plus every time A4's prompt has to be found.
