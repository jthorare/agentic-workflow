# Prior art: Agents (watch every live coding-agent session on the box)

This file is the prior-art ledger for the Agents surface of Sindri (rule: #2989). It lists the small daily jobs of running 5 to 30 agent sessions at once, who solves each best today, what we copy, and what Sindri does on origin/main.
Written 2026-11-12 from primary docs and READMEs fetched that day. "vendor claim" = the product's own page. "secondary" = a search snippet or forum post I did not open in full. "unconfirmed" = I could not verify it.
Surface job: show, in one list, every Claude Code, Codex and Cursor session on the box, which one waits on me, what it costs, and let me jump in or stop it, without opening a terminal first.
Direction issues: #3018 (Agents), #2978 (row / peek / full grammar, Home as the one inbox), #2989 (this ledger rule). Gaps today: #2811 (Unknown states, pid names, empty mid-row, sheet with no action), #2820, #2821.

Licence note for builders: we may read and copy ideas from MIT / Apache-2.0 code (ccmanager, agent-deck, ccusage, Crystal, Vibe Kanban, btop). Claude Squad is AGPL-3.0 and htop is GPL-2.0: ideas only, copy no code. Claude Code, Cursor, Conductor, Codex cloud and Warp are closed products: we read their docs, not code.
Note: Crystal was deprecated Feb 2026 (now Nimbalyst); Vibe Kanban's README says it is sunsetting. Both stay useful as ideas, not as live competitors.

---

## 1. See which agents wait on me, before anything else

**Best today**
- Claude Code agent view (`claude agents`, research preview). Rows are grouped Pinned, Ready for review, Needs input, Working, Completed; the ones that need you sit at the top. Yellow icon = "waiting on something only you can provide" (an answer, a permission, a sandbox prompt, an MCP request). Filter `s:blocked` lists everything waiting on you. Docs and screen: https://code.claude.com/docs/en/agent-view (vendor docs; the page embeds the screenshot). source: docs
- Warp notification mailbox: bell icon top right, tabs All / Unread / Errors, an attention badge on a tab whose agent is blocked, marked read when you open that tab. Secondary (search result, page not opened): https://docs.warp.dev/agent-platform/capabilities/agent-notifications source: none (search snippet or forum post only, page not read)
- Cursor Agents Window: sidebar groups agents by status with a "Needs Attention" group (approvals, questions, pending plans). Secondary (user forum post): https://forum.cursor.com/t/agents-approval-window/170388 . The release notes confirm the sidebar and keyboard nav: https://cursor.com/docs/release-notes source: none (search snippet or forum post only, page not read)
- ccmanager (MIT) marks each session busy / waiting / idle; agent-deck (MIT) uses green ● running, yellow ◐ waiting, grey ○ idle, and `@` in search lists the waiting ones. https://github.com/kbwo/ccmanager , https://github.com/asheshgoplani/agent-deck source: docs
- Conductor: its docs say "if the sidebar shows that a session needs input, open the workspace and answer" (vendor docs, llms-full.txt line ~4802): https://www.conductor.build/docs source: docs

**What we take**
- Group by what unblocks me, not by provider: Needs input first, then Working, then Idle/Done, Recently ended collapsed. Fixed group order, group header 24 px (visibly not a row; #2811 item 4 says ours look like rows).
- Three states with three glyphs that also differ in shape (colour-blind safe): `●` working, `◐` needs input, `○` idle, `✕` failed. Claude and agent-deck both converge on this.
- A one-key filter for "blocked" (`s:blocked` in Claude). We add it as a pinned chip with the count.

**Where one app wins**: Claude's agent view lists only Claude sessions you backgrounded (it states interactive sessions in other terminals do not appear until backgrounded). Sindri's collector reads the process table, so it lists Claude, Codex and Cursor sessions you never backgrounded, in one Needs-you group, merged with Home's inbox (a blocked agent next to a review request).

**Desktop gain**: the collector already runs a pass every 2 s from `/proc`, no network. Target: a session flipping to asking shows in the list within 3 s of the hook firing (2 s pass + 1 frame); first paint of 40 rows < 100 ms from the in-memory snapshot.

**Sindri today**: `sindri agents` and the Agents page group by workflow ("Not part of a workflow (6)"), not by need (src/Sindri/Aw.Sindri.Ui/ViewModels/Agents/AgentsPageViewModel.Rows.cs); a state filter exists (`AgentsPageViewModel.Filters.cs`: running / asking / idle / unknown). The CLI head counts `M asking` (docs/csharp/sindri.md:54). 5 of 7 rows show "Unknown" with no reason (#2811). Home does not show blocked agents yet (#2978).

**Verdict**: take and change (Claude's group order, our merged source).

---

## 2. Know what each agent is doing, in one line

**Best today**
- Claude agent view row: name, latest status or question, age (frozen at run time once finished). A finished row can carry the PR (`Opened PR with collision fix  #2048  2h`). https://code.claude.com/docs/en/agent-view source: docs
- agent-deck (MIT): one terminal list, status symbol, group, fuzzy search; `C` opens a context inspector ranked by cost. https://github.com/asheshgoplani/agent-deck source: docs
- Claude Squad (AGPL-3.0): left list of instances, right pane toggles Preview / Diff of the selected one. https://github.com/smtg-ai/claude-squad source: docs

**What we take**
- Row = glyph, name (what it works on, see 10), the agent's last sentence or the question it asks, age in status. The last sentence is the widest column and takes the middle that is empty today (#2811 item 3).
- On finished rows, show the PR chip as a link (agent view does this).
- Age counts from entering the current status (waiting 4m), not session age; ours already has a "For" column (`AgentColumns.For`), so keep it.

**Where one app wins**: the "what it's doing" text can link to the ticket, the PR and the workflow Step the session serves (Sindri holds the launch and the item). A terminal list shows a string.

**Desktop gain**: the text is already read from Claude's session records and the fleet hook files locally (docs/csharp/sindri.md:592). Target: row text updates within one poll (5 s now, `PollEvery`) and 10k ledger-less rows are not needed; 60 rows at 60 fps scroll.

**Sindri today**: columns are Status, Agent, Repository, Where it runs, What it is doing, For (`AgentColumns.cs`); "What it is doing" is only filled when the session asks or runs a Step, so most rows are empty there (#2811 item 3). No last-sentence field in the row.

**Verdict**: take and change.

---

## 3. Peek at the output or the question and answer it, without leaving the list

**Best today**
- Claude agent view: `Space` opens a peek panel with the latest output or the pending question; type and `Enter` replies in place; `Enter`/`→` attaches. Reply to a working session joins its message queue; `/model`, `/effort`, `/usage` run at once. https://code.claude.com/docs/en/agent-view source: docs
- Cursor Agents Window: queue a follow-up with Tab, or send now; Cmd+K searches transcripts across agents. https://cursor.com/docs/agent/overview (vendor docs) source: docs
- Conductor API/MCP: poll session status `idle|working|errored`, read new messages, steer a running session with a message; the docs warn to see `working` once before trusting `idle`. https://www.conductor.build/docs (llms-full.txt, "Wait for working before trusting idle"). source: docs

**What we take**
- `Space` toggles the peek; the peek shows the last 20 lines of the terminal, or the question with its answer buttons if the session asks. Exactly #2978's grammar, and the same as Claude's.
- Reply box in the peek: send to the session. Warn when the session is running (it queues).
- Conductor's lesson: after we send a message, show "sent, waiting for it to start" until we observe `running`, so `idle` right after a send is not shown as done.

**Where one app wins**: the peek can show the diff the session has so far and the CI state of its branch next to the terminal tail. Claude's peek is text only.

**Desktop gain**: tmux `capture-pane` is local. Target: peek paints the last 40 lines < 150 ms after `Space` (CO-7a already caps the capture at 64 KiB and has a 2 s socket timeout per docs/csharp/sindri-sessions.md).

**Sindri today**: the preview read is built (`GetSessionPreview`, spec CA-9, tmux panes only; `not-in-tmux` / `not-live` / `timed-out` otherwise) and `SessionSheetViewModel.cs` exists. The sheet opens with Enter, not Space, and has no reply box; a Focus line exists that "arrives in U11a-f" (AgentsPageViewModel.Keys.cs). Hosts other than tmux have no preview.

**Verdict**: take and change.

---

## 4. Jump into the real terminal of one agent

**Best today**
- Claude agent view: `Enter` / `→` attaches full screen, `←` on an empty prompt detaches, `Ctrl+Z` detaches back to where you started; `Alt+1..9` attaches to session 1-9. https://code.claude.com/docs/en/agent-view source: docs
- Claude Squad (AGPL): `Enter` or `o` attaches its tmux session, `ctrl-q` detaches. README keys table. https://github.com/smtg-ai/claude-squad source: docs
- agent-deck: waiting sessions appear in the tmux status bar and `Ctrl+b` then `1`-`6` jumps straight to them; default order is most recently waiting first. https://github.com/asheshgoplani/agent-deck (README "Notification Bar") source: docs
- Cursor: `Ctrl+Tab` opens a recent-agents switcher in the palette style (release notes). https://cursor.com/docs/release-notes source: docs

**What we take**
- One key (`Enter` opens the sheet, `O` or `Shift+Enter` focuses the terminal) and a global "next agent that waits" key. agent-deck's "most recently waiting is 1" order is the right default for a global jump.
- When we cannot focus the host, say why in the row's action ("Warp: can't focus, copy command") and offer Copy `cd <dir> && <cmd>`, never a dead button.

**Where one app wins**: Claude and Squad attach only their own sessions. Sindri can focus a Claude session in Warp, a Codex session in tmux and a Cursor chat from one key, because the collector knows each session's host (`tmux`, `warp`, `terminal`, `ide`, `ssh`).

**Desktop gain**: focus is a local call (tmux `select-pane`, an X/Warp URL). Target: key to terminal focused < 300 ms for tmux; measured with the real host.

**Sindri today**: missing. `focus` is `{method: none, possible: false, why: not-built}` until CO-4a (docs/csharp/sindri.md:207; `FocusSession` catalog row is `pending:CO-4a`); the sheet has no "Go to terminal" (#2811 item 9).

**Verdict**: take and change (our host-aware focus; Claude's attach/detach keys do not apply because we do not own the terminal).

---

## 5. Stop, pause or archive an agent without losing work

**Best today**
- Claude agent view: `Ctrl+X` stops; `Ctrl+X` again within 2 s deletes it from the list; `Esc` dismisses the confirm. On a group header `Ctrl+X` deletes the whole group after a confirm. https://code.claude.com/docs/en/agent-view source: docs
- Cursor Agents Window: archive with `Cmd+Z` undo (and `Cmd+Shift+Z` to archive again); archiving an agent with running background processes asks first, including "Archive All" on a group. https://cursor.com/docs/release-notes (release note text, vendor) source: docs
- Claude Squad (AGPL): `D` kills, `c` checks out (commits and pauses), `r` resumes. https://github.com/smtg-ai/claude-squad source: docs

**What we take**
- Stop = one key plus a second press within 2 s (agent view's pattern; no modal). Stop is separate from remove-from-list.
- Undo for list removal (`Cmd+Z`, Cursor).
- Squad's pause/resume (commit, free the worktree, resume later) is a workflow feature: skip on this page.

**Where one app wins**: Sindri knows if the session is a workflow Step; stopping it can say "this ends Step `build` of run 41" and mark the run, where a terminal tool just kills a process.

**Desktop gain**: stop is a local signal, answer in the row < 100 ms; the ended row appears with its resume command from the same snapshot.

**Sindri today**: missing on the page. Stopping a Sindri launch records `session.ended` (docs/csharp/sindri.md, "Ended by Stop", CO), and `sindri agents` has no `stop` verb; the sheet has no Stop (#2811 item 9). Sessions not started by Sindri: stopping them is unconfirmed as wanted (open question for the owner).

**Verdict**: take and change.

---

## 6. See cost and tokens per session and in total

**Best today**
- Claude Code itself: the status line gets JSON with `cost.total_cost_usd`, `total_duration_ms`, `total_lines_added`, `context_window.used_percentage` and `rate_limits` (vendor docs: https://code.claude.com/docs/en/statusline). OpenTelemetry metrics `claude_code.cost.usage` (USD) and `claude_code.token.usage` (https://code.claude.com/docs/en/monitoring-usage). source: docs
- ccusage (MIT? the GitHub API reports NOASSERTION; the README is MIT per search results, so confirm before copying code): `ccusage session` lists cost per conversation, `ccusage blocks` shows Claude's 5-hour windows, and it covers Codex, Amp, OpenCode and others from their local logs. https://github.com/ryoppippi/ccusage source: docs
- agent-deck (MIT): `$` opens a Cost Dashboard; the context inspector ranks what the session is sent by cost; context bar shows `≈49.5% (window inferred from model id)` and says `window unknown` rather than guessing. https://github.com/asheshgoplani/agent-deck source: docs
- Claude agent view warns that ten sessions use quota about ten times as fast (vendor docs, Limitations). source: docs

**What we take**
- Two numbers per row, right-aligned: context % and tokens or USD, plus a total in the page head ("7 live, 3.1M tokens today").
- agent-deck's honesty rule: when a model is unpriced or the window is unknown, show the tokens and say "price unknown", never a made-up USD. Sindri's `AgentSpend` already has `SpendBasis.TokensOnly`.
- Rate-limit headroom in the head (Claude exposes `rate_limits`): "5h window 62% used".

**Where one app wins**: cost per session, summed per ticket, PR and workflow run ("this issue cost 41k tokens across 3 agents"). ccusage groups by conversation only.

**Desktop gain**: all of it is local log parsing, no vendor dashboard round trip. Target: today's total for the box loads < 200 ms from an incremental local index (not a re-parse of every transcript).

**Sindri today**: `AgentSpend(Usd, Tokens, Basis)` exists in `src/Sindri/Aw.Sindri.Core/Launches/AgentSpend.cs` (screens spec E-28) for launches. The Agents page has no cost or token column, and I found none in `ViewModels/Agents` (grep for tokens/cost/spend is empty). Sessions not launched by Sindri have no cost source wired.

**Verdict**: take and change.

---

## 7. Find one session among many (by name, state, repo)

**Best today**
- Claude agent view filter tokens typed in the input: `a:<agent>`, `s:<state>` (`s:blocked`, `s:working`), `n:<text>` (name or first prompt), `o:<text>` (result text, so `o:merged` lists the done-and-merged); `Ctrl+F` finds by name. https://code.claude.com/docs/en/agent-view source: docs
- agent-deck: `/` fuzzy search, `!` running, `@` waiting, `#` idle, `&` error, `0` clears, `G` searches every conversation on the machine. https://github.com/asheshgoplani/agent-deck source: docs
- Cursor Cmd+K: searches past agent transcripts across conversations; the palette opens and searches faster with many agents. https://cursor.com/docs/agent/overview and release notes. source: docs

**What we take**
- One text box, Claude's `key:value` tokens plus `p:claude|codex|cursor`, `r:<repo>`, `s:` (same state words as the chips). `Esc` clears. These replace three combo boxes today.
- `o:` on results is good: ours is "last message / PR state".

**Where one app wins**: the same box searches ended sessions, the ledger and the ticket title, so "refund" finds the agent, the issue and the PR.

**Desktop gain**: filter is in-memory over < 500 rows; target: each keystroke redraws < 16 ms.

**Sindri today**: three combo filters (provider, state, repo) and Clear filters (`AgentsPageViewModel.Filters.cs`, `.Layout.cs`). No text search; no `/` key. Keys present: J K Enter O Y E only (`Keys.cs`).

**Verdict**: take and change.

---

## 8. Show many sessions densely, grouped the way I think

**Best today**
- Claude agent view: group by state; `Ctrl+S` switches to group by directory and remembers it; `Alt+↑/↓` jumps between group headers; "… 6 more" collapses the tail. https://code.claude.com/docs/en/agent-view source: docs
- btop (Apache-2.0): process list with live sort switch, filter, tree view, full mouse support; the model for a dense live table. https://github.com/aristocratos/btop . htop (GPL-2.0): same job, ideas only (not read by me; repo link only). https://github.com/htop-dev/htop source: docs
- Cursor sidebar: groups Today / Last 7 days / Older (collapsed), remembers collapsed sections across restarts, Arrow/Home/End walk every row. https://cursor.com/docs/release-notes source: docs
- Conductor sidebar: Pinned, My workspaces, Following, Team with presence dots; a workspace is named by its PR title or branch. https://www.conductor.build/docs source: docs

**What we take**
- 32 px rows (#2978), 25-30 on screen, with sort by column on header click (btop) and `Ctrl+S` for group by Need / Repo / Workflow.
- Remember collapse state and grouping across restarts (Cursor, Claude).
- Header row distinct from data rows: smaller, muted caps, 24 px.
- Walk keys: Up/Down/Home/End/PgUp/PgDn on every list (#2951 asks the same for other lists).

**Where one app wins**: "group by workflow run" and "group by ticket" exist only in Sindri, because the session is joined to the item. No terminal tool can group by ticket.

**Desktop gain**: virtualised list, target 500 rows scrolling at 60 fps, group switch < 100 ms.

**Sindri today**: group headers by workflow ("Not part of a workflow (6)") that look like rows (#2811 item 4); `IsCompact` below 640 px keeps three columns (`AgentsPageViewModel.Layout.cs`); the Agent column ends ~320 px and the next starts ~1075 px at 1280 (#2811). No sort, no group switch.

**Verdict**: take and change.

---

## 9. Hear about it when I am not looking at the app

**Best today**
- Claude Code footer: `← 2 agents` counts sessions waiting (refreshed every ~10 s, `← 99+ agents` cap), `← 2 done` for finished; the count flashes colour on change. Hooks: the `Notification` event with types `permission_prompt`, `idle_prompt`, `elicitation_dialog`, `agent_needs_input` lets a script ring or message you. https://code.claude.com/docs/en/agent-view , https://code.claude.com/docs/en/hooks source: docs
- Warp: toast for an agent in another tab; when Warp is in the background, a native desktop alert; mailbox for what you missed (secondary). https://docs.warp.dev/agent-platform/capabilities/agent-notifications source: none (search snippet or forum post only, page not read)
- agent-deck: Telegram "conductor" bot answers `/status` for every session and "never lets a waiting worker rot"; a notification bar in tmux. https://github.com/asheshgoplani/agent-deck source: docs
- Cursor iOS: a Live Activity shows agent status; a forum report says it showed Waiting while the agent worked (secondary): https://forum.cursor.com/t/cursor-ios-agents-live-activity-shows-waiting-while-agent-is-actively-working/172561 source: none (search snippet or forum post only, page not read)

**What we take**
- A tray/menubar badge with the waiting count, a native notification only for state changes to needs-input or failed (not for done), and a count that refreshes at the collector's pass.
- Notification click = open that row's peek.
- Cursor's bug is a warning: never show Waiting unless the source says asking; our "Unknown" label is the honest version.

**Where one app wins**: one badge across Claude, Codex and Cursor, and the same count feeds Home's "Needs you" section (#2978).

**Desktop gain**: the badge comes from the in-process snapshot, no push service. Target: badge updates within 3 s of the hook.

**Sindri today**: the tab shows a live count (`AgentsPageViewModel.Counts.cs`, `ShowNavCount`) and a tray exists (docs/csharp/sindri-ui.md, per the project instructions); native alerts for a waiting agent: unconfirmed, no issue found. `fleet.changed` is a public event with `appeared | asking | gone`, so the data exists (docs/csharp/sindri.md:589).

**Verdict**: take and change.

---

## 10. Name an agent in words a person recognises

**Best today**
- Claude agent view: named automatically from the first prompt, `Ctrl+R` renames, `/color` tints the name. https://code.claude.com/docs/en/agent-view source: docs
- Conductor: every workspace gets a city name, and the sidebar line shows the PR title if a PR is open, else the branch; `cmd+K -> Passport` lists the cities. https://www.conductor.build/docs source: docs
- ccmanager (MIT): sessions are named by worktree and branch. https://github.com/kbwo/ccmanager source: docs

**What we take**
- Name order: user rename, then session title, then the ticket/PR title the session serves, then `repo · branch`, then terminal + start time. A pid appears only in Details. This is #2811 item 2 as a rule.
- Rename inline (`F2`), stored in Sindri, written back to Claude's session name when possible (unconfirmed that Codex/Cursor accept it).

**Where one app wins**: the ticket and PR title are known to Sindri; a standalone manager only has the branch.

**Desktop gain**: names resolve from local rows, no lookup; target: no row ever paints with a pid-only name on first frame.

**Sindri today**: rows read `pid-411`, `pid-1100`, `0199a1b2` (#2811 item 2). Provider + title exist when the host gives a title.

**Verdict**: take and change.

---

## 11. Say honestly what I cannot see

**Best today**
- Claude agent view: six named states (Working, Needs input, Idle, Completed, Failed, Stopped), and a `∙` mark for "the process has exited; reply and Claude restarts it from the saved conversation". https://code.claude.com/docs/en/agent-view source: docs
- ccmanager: a status "hook" per tool and configurable state-change hooks, and an explicit rule that busy/waiting/idle are detected per CLI. https://github.com/kbwo/ccmanager source: docs
- Conductor API docs: idle can mean "not started yet", so wait for `working` first (see 3). source: docs

**What we take**
- Never show a bare "Unknown". Use "No recent activity" or "Can't see this terminal" with the reason in a tooltip, muted, not bold (#2811 item 1). Claude's per-state wording shows the level of detail users expect.
- Show how the state was learned in Details only in words ("from the session's hook", "from the process table") and the age of that reading.

**Where one app wins**: Sindri merges three sources (process table, Claude's records, hook files) and can say which one disagrees, e.g. process alive but hook silent for 10 minutes. I found no tool doing that cross-check: unconfirmed that none does.

**Desktop gain**: all sources are local; target: the reason string is available on the first frame with the row, no extra read.

**Sindri today**: `unknown` is one of four served states (`_states` in `AgentsPageViewModel.Filters.cs`); the collector knows `stateSource` (`none | record | hook | transcript`) and `estimated` (docs/csharp/sindri-sessions.md row fields), so the reason exists server-side and is not shown (#2811 item 1, 8).

**Verdict**: take and change.

---

## 12. Resume or restart a finished session, and start a new one from the list

**Best today**
- Claude agent view: type a prompt at the bottom and `Enter` starts a new background session; `Ctrl+Enter` starts and attaches; `<agent-name> <prompt>` runs a named subagent; an exited row restarts when you reply. `claude agents --json` prints sessions for scripts. https://code.claude.com/docs/en/agent-view source: docs
- Claude Squad (AGPL): `n` new, `N` new with a prompt, `r` resume a paused one. https://github.com/smtg-ai/claude-squad source: docs
- agent-deck (MIT): `f` / `F` fork a session, quick or with a dialog; first run shows which agents are installed and signed in. https://github.com/asheshgoplani/agent-deck source: docs

**What we take**
- Ended rows keep their resume command with one-key copy (`Y`, already ours) and a one-key "Resume in terminal".
- A dispatch input at the bottom of the page for "run this on repo X with agent Y" is the Home/Reviews hand-to-agent action, not an Agents-page feature: link, do not duplicate.
- `--json` on the CLI exists already (`sindri agents --json`).

**Where one app wins**: "Hand to an agent" from a ticket or PR row (#2997 asks this for Linear) lands the session on the Agents page already linked to its item.

**Desktop gain**: resume command comes from the local snapshot; copy in < 50 ms.

**Sindri today**: recently ended sessions with resume argv are listed (`EndedSessionViewModel.cs`, `Y` copies; the CLI prints the resume argv). An ended session with no resume command still shows Copy (#2821). Start-from-page: missing; delegating an issue to an agent: #2997.

**Verdict**: skip dispatch on this page (belongs to Home / row actions); take as is for resume.

---

## Closing table

| Problem | Best tool | Verdict | Sindri today (issue) |
|---|---|---|---|
| 1 Which agents wait on me | Claude agent view (Needs input group, `s:blocked`) | take and change | grouped by workflow, state filter only (#2811) |
| 2 What each is doing, one line | Claude agent view rows (name, last line, age, PR) | take and change | "What it is doing" empty for most rows (#2811) |
| 3 Peek and reply in place | Claude agent view `Space` peek | take and change | preview read built (CA-9), sheet on Enter, no reply box |
| 4 Jump into the terminal | Claude attach / agent-deck tmux bar | take and change | missing, `focus` is not-built until CO-4a (#2811) |
| 5 Stop / archive safely | Claude `Ctrl+X` twice; Cursor undo | take and change | missing on the page (#2811) |
| 6 Cost and tokens | ccusage, Claude statusline JSON, agent-deck `$` | take and change | `AgentSpend` for launches only, no column |
| 7 Find one session | Claude `s: n: o:` tokens, agent-deck `/` | take and change | three combos, no text search |
| 8 Dense grouped list | Claude agent view, btop, Cursor sidebar | take and change | group headers look like rows, 750 px gap (#2811) |
| 9 Hear about it away from the app | Warp native alert + mailbox, Claude footer count | take and change | tab count only; native alert unconfirmed |
| 10 Human names | Claude auto-name, Conductor PR-title/branch | take and change | `pid-411` names (#2811) |
| 11 Say what I cannot see | Claude's six states | take and change | bare "Unknown" (#2811) |
| 12 Resume / start | Claude agent view, agent-deck fork | skip start; take resume as is | resume built; Copy shows with no command (#2821) |

## Where I found nothing solid
- Stuck detection (a session "running" with no progress for N minutes): no tool found that does it from process + transcript evidence. ccmanager and Claude only report busy/waiting/idle. Open for us.
- Codex cloud task list screen: the Codex cloud doc I fetched (https://developers.openai.com/codex/cloud.md) has no task-list screen; `codex cloud list/status` come from secondary pages only. Not used as evidence.
- Warp's agent panel details (filters, keys) are secondary; I could not open the pages.
- Cursor "Needs Attention" group is a user forum description, not Cursor docs.
- LangSmith (trace view for LLM runs) was not read; it solves a different job (traces, not live local sessions).

---

## What we take (7 lines)

1. Group by need (Needs input, Working, Idle, Recently ended), distinct 24 px group headers, three shape-distinct glyphs: #2811.
2. Row text = what it is doing / its question, human names (user rename, title, ticket or PR title, repo + branch, pid only in Details), reasons instead of "Unknown": #2811.
3. `Space` peek with terminal tail and reply box, `Enter` opens full, shared row keys (J/K, Home/End, PgUp/PgDn): #2951 for keys; #3022 for the peek with reply.
4. Jump into the terminal (host-aware focus, Copy command when it can't focus) and two-press Stop with undo on remove: #3023 (spec CO-4a for focus).
5. One filter box with `p: r: s: n: o:` tokens replacing the three combos; sort by column and Ctrl+S grouping: #3024.
6. Context % and tokens/USD per row plus a total in the head, "price unknown" when unpriced (uses `AgentSpend`): #3025.
7. Tray badge and native alert only for needs-input or failed; resume copy with no dead button: #3026 for the alert; #2821 for the resume fix.

---

## Addendum: the live terminal inside the Agents page (issue #3334)

Written 2026-11-12 from READMEs, docs pages and package metadata fetched that day. Same labels as above: "source: docs" = I read the product's own page or repo file. "secondary" = a search result summary, page not read in full. "unconfirmed" = I could not verify it.
Owner direction (#3334): Sindri becomes "my own version of T3". Click an agent, see its live tmux session in a pane inside the Sindri window (a real terminal: it scrolls and takes keys). Start a new Claude Code or Codex session from the page, in a new tmux session, in a repo he picks. Type one-off prompts into it. The owner confirmed on the issue that "T3" is T3 Code (relayed by meta, 2026-11-12), so T3 Code is the lead row and the reference product for rows 13 to 15.

**What "T3" is.** T3 Code (https://t3.codes, https://github.com/pingdotgg/t3code, MIT, "The open-source control plane for coding agents"). It is a desktop, web and phone app. It drives Claude Code, Codex, Cursor, OpenCode and any ACP agent that is installed and signed in on your machine; each thread can work in its own git worktree. source: docs (README, `docs/user/*.md`, `docs/internals/terminal-runtime.md`, read 2026-11-12). Facts that matter for us:
- The server owns the PTYs, their lifetime and their output; every client, the desktop window included, attaches over the connection, so a client can reconnect or share a running terminal (`docs/internals/terminal-runtime.md`). Same shape as tmux server plus Sindri as a client. source: docs
- The renderer is `libghostty-vt` through its C ABI (web: one WebAssembly instance per tab; Android native). React stays out of terminal frames. A C ABI is something .NET can P/Invoke, so it is a candidate for our pane control (see section A). source: docs
- Scrollback: 5,000 lines and 8 MiB per terminal on the server, 512 KiB per client; `Shift+PageUp/PageDown` reads it without leaving terminal input; full-screen programs keep those keys (`docs/user/terminal.md`). source: docs
- The agent itself shows as a chat thread; the terminal is a drawer per thread (`PersistentThreadTerminals.tsx`, `terminal.toggle` on `mod+j` / `mod+g`). I did not find the agent's own TUI embedded in a terminal. source: docs
- It has no tmux. Its terminals live in its server, so they are not the sessions of agents you started in a terminal yourself. Sindri's job differs here: show the sessions that already exist, and run its own in tmux.

Licence note for builders: T3 Code (MIT), Paradev/Superset (MIT), agent-deck (MIT) allow code reuse with the notice. Claude Squad (AGPL-3.0) and opcode (AGPL per its README): ideas only. Conductor, Warp, VS Code, Zed: docs read, no code read.

---

## 13. See the selected agent's live terminal in a pane next to the list

**Best today**
- T3 Code (MIT, the owner's reference): thread list left, the selected thread right, its terminals in a drawer per thread that keeps running when you switch threads (`PersistentThreadTerminals.tsx`). Clients reattach to the server's PTY with its retained scrollback. `F6` / `Shift+F6` moves focus between the Threads sidebar, the conversation, the message input and the visible panels and restores the last focused control in each (`docs/user/keyboard-focus.md`); default shortcuts carry `!terminalFocus` so they never steal terminal keys (`docs/user/keybindings.md`). https://github.com/pingdotgg/t3code/tree/main/docs/user source: docs
- Zed "Terminal Threads": a terminal-backed thread in the Threads Sidebar. Choosing one opens it in the panel body "just like switching to an agent thread". Zed owns the thread list, grouping and switching; the CLI inside owns auth, model and tools. A bell while the terminal is out of focus raises a notification, and clicking it focuses the terminal. https://zed.dev/docs/ai/terminal-threads source: docs
- Paradev (formerly Superset, MIT): each workspace has a built-in terminal with tabs and splits; the agent runs in that terminal; sessions are held by a background daemon, so a pane reattaches with its scrollback on reopen. Keys: new tab Cmd+T, close pane Cmd+W, switch tabs Cmd+Opt+1-9. https://docs.paradev.com/terminal-integration , https://docs.paradev.com/keep-agents-running source: docs
- Claude Squad (AGPL): left list, right pane shows Preview or Diff (`tab` switches). `Enter` or `o` attaches the real tmux session, `ctrl-q` detaches. https://github.com/smtg-ai/claude-squad source: docs
- Conductor: the agent shows as a chat; a separate terminal runs ad hoc commands in the workspace. Not the agent's own TUI. https://www.conductor.build/llms-full.txt source: docs

**What we take**
- Layout: list left, terminal right, T3 Code's split (Squad and Zed do the same). The list stays visible. The existing peek (row 3, last 20 lines) stays for a quick look; the pane is the same idea grown into a real terminal.
- On row change the pane switches to the new row's session. Keep each visited session's screen and scrollback in memory so a switch back does not redraw from empty (T3 Code and Paradev both reattach with scrollback).
- Focus: list keys act while the list has focus. `Enter` or a click moves focus into the terminal; from then on every key goes to tmux. `F6` returns focus to the list without detaching, `Shift+F6` goes the other way: T3 Code's key, and `F6` is not a key Claude Code or Codex binds (spike checks it reaches neither TUI). Page shortcuts are off while the terminal has focus (T3's `!terminalFocus` rule). Squad and agent-deck use `ctrl-q`, but that detaches.
- Scrollback in the pane: `Shift+PageUp/PageDown` without leaving terminal input (T3).
- A pane badge says "Attached" or "Read-only". tmux can attach read-only (`attach -r`, which also stops that client from resizing the window; checked in `man tmux` 3.4 on this box).

**Where one app wins**: Zed and Paradev show only terminals they started. Sindri's list holds sessions started anywhere on the box (the collector) plus the ones Sindri launched, joined to the ticket, PR and workflow Step. The pane header can show those next to the live terminal.

**Desktop gain**: no network, a local pty to a local tmux server. Targets for perf: row change to first painted frame of the new session < 150 ms (warm tmux); keystroke to echo on screen < 30 ms median; a 10k-line scrollback scrolls at 60 fps.

**Sindri today**: not built. The page opens an external terminal window through `ITerminalViewer` (adapters warp, exec-template, print; `src/Sindri/Aw.Sindri.Core/Launch/ITerminalViewer.cs`). `ISessionHost` can build the attach command for a Sindri-launched tmux session (`ISessionHost.cs`, the attach-argv member). The 20-line preview read exists (row 3). No terminal control package is in `Directory.Packages.props` (grep on origin/main for xterm, VtNet, terminal control found none).

**Verdict**: take and change (T3 Code's split, reattach-with-scrollback and `F6` focus key; Zed's "terminal is a row in the list"; Squad's preview-then-attach).

---

## 14. Start a new agent session from the list

**Best today**
- T3 Code (MIT, the owner's reference): **New thread** opens a project picker when more than one project is open; `Shift`-click starts in the current project. The new thread carries your last model and mode; branch and workspace mode (main checkout or **New worktree**) come from your defaults. `mod+Enter` sends and opens a fresh draft at once, so you can start several in a row; `Shift`-click on models sends one prompt to several agents, one thread and worktree each (`docs/user/thread-sidebar.md` "Start a thread", "Start in the background"). source: docs
- agent-deck (MIT): `n` opens a New Session dialog. `Enter` creates from any row with what is filled in: the current folder, your last-used tool, the selected group, and the folder name when Name is empty. So `n` then `Enter` is the whole common case; `N` creates with no dialog. First run shows which agents are installed and signed in. https://github.com/asheshgoplani/agent-deck source: docs
- Paradev: `Cmd+N` creates a git worktree on its own branch; prompt-first: describe the task, pick an agent, create, and the agent starts at once. https://docs.paradev.com/first-workspace source: docs
- Conductor: `Cmd+Shift+N` or the `...` button next to "New workspace" starts from a branch, pull request, GitHub issue or Linear issue. https://www.conductor.build/llms-full.txt source: docs
- Claude Squad (AGPL): `n` new session, `N` new session with a prompt. https://github.com/smtg-ai/claude-squad source: docs
- Zed: the New Thread menu (agent selector, or the `+` in the panel toolbar) lists Terminal; the `agent.terminal_init_command` setting runs a command such as `claude` when a Terminal Thread opens. https://zed.dev/docs/ai/terminal-threads source: docs
- Claude Code agent view: type a prompt at the bottom and `Enter` starts a session; `Ctrl+Enter` starts it and attaches (row 12). source: docs

**What we take**
- The form has four fields: repo (the profile's repos plus "other folder"), agent (Claude Code or Codex, last used first), prompt (optional), and a "new worktree and branch" switch, off by default. Everything has a default, so `Enter` on an untouched form starts. This is T3 Code's "carry the last choices" rule (agent-deck does the same) plus its prompt-first order.
- `Ctrl+Enter` starts it in the background and leaves the form open for the next one (T3's `mod+Enter`). The "several agents, one prompt" fan-out: skip for v1.
- Start in a new tmux session on Sindri's own socket (`tmux -L sindri-launch`, already how launches work), then show the pane on it at once with focus in the terminal (Claude's `Ctrl+Enter` behaviour).
- Worktree is optional because the owner asked for "a repo he picks"; Paradev and Conductor make it the default.

**Where one app wins**: the repo list is the profile's repos with their onboarding state. The form can also start from a ticket or PR Sindri already knows (Conductor's "from an issue"), and the new row is linked to it on the first frame.

**Desktop gain**: all local. Targets for perf: `Enter` on the form to a live prompt in the pane < 1.5 s (the launcher's own start deadline today is 4 s); the new row in the list < 300 ms after `Enter`.

**Sindri today**: the launch path exists: `ISessionHost.Start` (4 s deadline), `TmuxSessionHost` runs `tmux new-session -d -s <name> -c <workdir> sh <script>` (`src/Sindri/Aw.Sindri.Infrastructure/Launch/TmuxSessionHost.cs:239`), request type `LaunchRequest.cs`. Workflow Steps and the CLI reach it. A "new session" button or form on the Agents page: not checked.

**Verdict**: take and change.

---

## 15. Type a one-off prompt into a scratch session, and close it afterwards

**Best today**
- T3 Code (MIT, the owner's reference): **Start without a project** (`mod+alt+n`). Each such thread works in its own folder under `~/.t3/scratch`, named date + first words of the first message + a short id (`2026-09-25-convert-these-pngs-to-webp-a1b2c3d4`); deleting the thread keeps the folder. **Turn into project** later makes that folder a project in place, nothing moves. **Side chat** (`/side` or `/btw`): ask a question about a running thread without adding to it, the thread keeps working; **Bring back** hands the answer to the thread (`docs/user/thread-sidebar.md`). source: docs
- Claude Code agent view: type a prompt, `Enter` starts a background session; `Ctrl+X` stops, `Ctrl+X` again within 2 s deletes it. https://code.claude.com/docs/en/agent-view source: docs
- agent-deck (MIT): `o` sends a message to the selected session (a busy Claude session queues it, no interrupt), `Shift+Q` queues after the current turn. `A` archives (stops the tmux process, keeps conversation and worktree), `d` deletes (`Ctrl+Z` undoes in the TUI). https://github.com/asheshgoplani/agent-deck source: docs
- Paradev: a terminal can be a plain shell; closing one with a running process asks to confirm, with a "Don't ask again" box. https://docs.paradev.com/terminal-integration source: docs
- Zed: Terminal Threads "are closed rather than archived" and do not go to Thread History (`shift-backspace` or the x button). https://zed.dev/docs/ai/terminal-threads source: docs
- Conductor, Squad, Vibe Kanban: every session is tied to a worktree or branch; I found no scratch mode in their docs (unconfirmed that none exists).

**What we take**
- A scratch session has no ticket and no worktree, runs in the repo the owner picked (root), and is named by its first prompt (row 10 rule, and T3's folder name rule). T3's no-repo scratch folder: open question for the spec (the owner said "in a repo he picks").
- "Turn into a ticket" later, as T3's Turn into project (see "Where one app wins").
- Typing goes straight into the live pane; the pane already takes keys. A one-line composer under the pane is optional. Show "queued" when the agent is busy (agent-deck queues).
- Closing a scratch session asks one question: Stop it (ends the tmux session) or Keep it (leave it running, drop the pane). Default Keep when the agent is working, Stop when it is idle. This is agent-deck's archive-versus-delete split plus Zed's close-not-archive.

**Where one app wins**: a scratch session can be promoted to a ticket, PR or workflow Step later because Sindri holds the join. Zed and Squad lose the scratch once it is closed.

**Desktop gain**: sessions are tmux processes, no extra work. Targets for perf: scratch started and first prompt sent < 2 s; "Keep it" returns control in < 100 ms.

**Sindri today**: no scratch concept found in `src/Sindri/Aw.Sindri.Core/Launch` (I did not read `LaunchRequest.cs` in full). Page: not checked.

**Verdict**: take and change.

---

## 16. A session the app cannot attach (not in tmux: a Warp tab, an IDE terminal)

**Best today**
- Warp: vertical tabs show per tab the agent, branch, directory and status; an unread-activity dot clears when you focus the pane. The Agent Management Panel lists only agents Warp started and cloud runs, not third-party CLIs; for those Warp points at vertical-tab status. Notifications exist for Claude Code, Codex and OpenCode (a Claude plugin needs `jq`). https://docs.warp.dev/guides/agent-workflows/how-to-run-multiple-ai-coding-agents.md , https://docs.warp.dev/agents/cli-agents/overview.md source: secondary (search summary, pages not opened)
- Zed Terminal Threads: only terminals Zed opened itself. source: docs
- Claude Code agent view: only sessions you backgrounded; interactive sessions in other terminals do not appear (row 1). source: docs
- agent-deck and Squad: only tmux sessions they created. source: docs
- VS Code integrated terminals reconnect to their process after a window reload (`terminal.integrated.enablePersistentSessions`); that covers VS Code's own terminals only. https://code.visualstudio.com/docs/terminal/advanced source: secondary

**What we take**
- Nobody in this set embeds a session they did not start. So Sindri offers a per-host action: attach in the pane (tmux), focus the host (row 4: Warp, terminal window), or read-only. For a non-tmux session the pane shows the last read-only snapshot we have (transcript tail, as in row 3), the reason in words ("Warp tab: Sindri can't type into it"), and two buttons: "Copy resume command" and "Move to tmux". Move to tmux resumes the same Claude or Codex session id in a new tmux session; the old one must be closed first, so it asks.
- Never show a dead terminal. No pane, a card with the reason (row 11).

**Where one app wins**: Sindri knows the host kind per session (`tmux`, `warp`, `terminal`, `ide`, `ssh`) and can pick the action per kind. No other product in the list does.

**Desktop gain**: local reads only. Target for perf: the card with its reason paints on the same frame as the row selection (< 16 ms, no I/O).

**Sindri today**: the collector sets `host.kind` and `terminal` per session (the fixture `docs/superpowers/specs/2026-10-20-sindri-ui-f3-redesign/data/tools/pg_agents.py` has tmux, warp and unknown rows). Preview answers `not-in-tmux` for other hosts (row 3). Move to tmux is not built. Not checked: whether Claude or Codex can resume a session id in a different process (needs a spike).

**Verdict**: take and change (ours wins on breadth; Move to tmux is a new idea, unconfirmed until the resume spike).

---

## 17. Close the pane versus end the agent

**Best today**
- Paradev: agents live in a background terminal daemon, not the window. Closing the window, quitting the app or updating does not stop them. "Quit Paradev Completely" or a restart ends every session. Closing a single terminal with a running process asks to confirm. https://docs.paradev.com/keep-agents-running , https://docs.paradev.com/terminal-integration source: docs
- Claude Squad (AGPL): `ctrl-q` detaches (agent keeps running); `D` kills; `c` checks out (commits and pauses); `r` resumes. source: docs
- agent-deck (MIT): `Ctrl+Q` detaches; `A` archive stops the tmux process but keeps conversation, worktree and links; `d` delete removes it from the registry; the web UI gives 30 s to undo a delete. source: docs
- T3 Code (MIT): closing a terminal opens a destructive confirm: `Close terminal "<name>"? This stops the running process and clears its history.` (`apps/web/src/lib/terminalCloseConfirm.ts`). source: docs
- Zed: closing a Terminal Thread removes it; no history. source: docs

**What we take**
- Two controls with different names. **Hide terminal** detaches the pane; the session keeps running; no confirm. **Stop agent** ends the tmux session; one confirm that names the cost ("ends the session; its screen and scrollback are gone"). Row 5's second-press-within-2-s pattern is an option instead of a dialog.
- Quitting Sindri never ends a tmux session. tmux does this by itself (the server outlives the client), and it matches Paradev's promise.
- On Stop, keep the resume command on the ended row (row 12).
- When more than one client is attached, say so ("also open in Warp"): `ISessionHost.AttachedClients` already counts them.

**Where one app wins**: tmux gives Paradev's daemon behaviour with no daemon of our own, and the session survives a Sindri crash. T3's terminals live in its server process.

**Desktop gain**: detach is a local pty close. Targets for perf: Hide < 50 ms and the tmux session is still alive afterwards (`tmux has-session`); Stop confirmed to row gone < 300 ms.

**Sindri today**: `ISessionHost.Stop` exists and is idempotent; `AttachedClients` exists. Stop has no button on the page (row 5). Hide terminal does not exist (no pane).

**Verdict**: take and change (T3 Code's server-owns-the-terminal shape and its wording of the cost, Squad's detach and kill split, Paradev's "quit never kills").

---

## 18. Many live panes (grid) or one

**Best today**
- Paradev: tabs and splits per workspace; layouts saved as terminal scripts. https://docs.paradev.com/terminal-integration source: docs
- Zed: one Terminal Thread open in the panel at a time, many in the sidebar. source: docs
- Claude Squad and agent-deck: one preview or attached session at a time. source: docs
- T3 Code: split and new-terminal actions in the drawer (`onSplitTerminal`, `onNewTerminal` in `ThreadTerminalDrawer.tsx`). source: docs
- Warp: many tabs with a vertical-tab status list. source: secondary

**What we take**
- One live pane first. The list is how you see many. A second pane (split, or pin one session) comes later. Each live pane is a pty plus a VT engine feeding a 60 fps render, so it has a cost.

**Where one app wins**: not applicable for v1.

**Desktop gain**: not for v1. The spike measures N live panes (CPU % with 4 panes each printing 1k lines/s) so a later grid is a known number.

**Sindri today**: no pane.

**Verdict**: skip for now (one pane); measure in the spike.

---

# Terminal control candidates (a spike decides; no pick here)

Context: the Sindri UI is Avalonia 12.1.4 with NativeAOT (`Directory.Packages.props` comment), MIT and Apache-only packages. Checked 2026-11-12 on api.nuget.org and api.github.com.

## A. Controls and emulators for .NET and Avalonia

| Candidate | Licence | Last release / push | What it is | AOT / trim claim | Notes |
|---|---|---|---|---|---|
| **Devolutions.Terminal.Control** (github.com/Devolutions/devolutions-terminal) | MIT (NuGet and LICENSE file; GitHub shows NOASSERTION) | 2026.3.7 on 2026-11-10; pushed 2026-11-12 | Avalonia control that renders their C# VT engine (`Devolutions.Terminal.Core`); forkpty on Linux and macOS, ConPTY on Windows | README: the Devolutions Terminal app is "published as a self-contained NativeAOT desktop app" on Avalonia 12 (source: docs). Strongest AOT evidence in the list. Not checked: whether the control package alone is marked AOT-compatible | Needs Avalonia 12.1.1, Skia, HarfBuzz native assets. 30 stars, young. Has `samples/Devolutions.Terminal.Control.Sample`. Same Avalonia major as ours |
| **RoyalApps.RoyalTerminal.Avalonia** (github.com/royalapplications/RoyalTerminal) | MIT | 0.8.0-preview.1 on 2026-11-07; 29 releases | Backend-neutral Avalonia control; C# VT processor or native `libghostty-vt`; a Unix forkpty package; Sixel; shaders | No AOT claim found in README, props or file names (unconfirmed) | Avalonia 12.x. Most complete. The Ghostty path adds a native binary per platform. Preview version |
| **libghostty-vt** (Ghostty's VT library, C ABI; T3 Code's renderer, `docs/internals/terminal-runtime.md`) | MIT (Ghostty) | T3 pins a revision in `native/libghostty-vt/VERSION` | VT state machine only; drawing and input are the host's. RoyalTerminal (row above) already wraps it for Avalonia | A C ABI called through `LibraryImport` is trim-safe; one native binary per platform to ship beside `sindri-ui`, as `e_sqlite3` is today | Same engine the owner's reference product uses; RoyalTerminal's Ghostty path is the cheap way to try it |
| **Terminal.Avalonia / Terminal.Emulation / Terminal.Pty** (github.com/b-y-t-e/Terminal.Avalonia) | MIT | 0.4.5 on 2026-10-22; 19 versions | From-scratch Avalonia control with its own VT engine and PTY layer (ConPTY, forkpty). Its NuGet text says it hosts "claude code" and "opencode" and runs many instances side by side | No AOT claim found (unconfirmed) | Avalonia 12.1.2. The GitHub API returned 404 for the repo, so only NuGet metadata was read |
| **VirtualTerminal / VirtualTerminal.Avalonia** (github.com/Rikitav/VirtualTerminal) | Apache-2.0 (GitHub); NuGet shows a licence URL only | 1.10.0 on 2026-09-03; pushed 2026-11-10 | UI-agnostic VT engine plus Avalonia and WPF controls | None found | 9 stars |
| **AgentSpace.Avalonia.XTerm** (github.com/pramin/AgentSpace) | MIT (NuGet) | 0.1.7 on 2026-05-18 | Avalonia control on a real PTY, Avalonia 12 | None found | Repo returned 404; NuGet only; likely stale |
| **Exclr8.Terminal** (NuGet) | MIT per NuGet (a search summary said Apache-2.0: they disagree) | 1.0.10 on 2026-11-12 | Native Avalonia terminal control | None found | Repo returned 404 |
| **XtermSharp** (github.com/migueldeicaza/XtermSharp) | MIT | last push 2022-11-08; NuGet 1.0.0-alpha.10 | Xterm emulator library for .NET | None | Stale |
| **VtNetCore** (github.com/darrenstarr/VtNetCore) | MIT | last push 2023-06-16; 1.0.30 | VT100 and xterm emulator, .NET Standard 2.0, no UI | None | Stale; needs our own Avalonia renderer |

## B. PTY layer (needed for `tmux attach` over a pty)

| Candidate | Licence | Last release / push | Notes |
|---|---|---|---|
| `forkpty` / `openpty` P/Invoke written by us | ours | n/a | A few `LibraryImport` calls plus `ioctl TIOCSWINSZ` for resize. Source-generated P/Invoke is the trim-safe form. Smallest dependency; macOS and Linux only, which is where Sindri runs |
| **vs-pty.net** (github.com/microsoft/vs-pty.net) | MIT | last push 2024-12-09 | "Fork pseudoterminals in C#". Small, stale. No AOT claim |
| **Quick.PtyNet** (github.com/aaasoft/Quick.PtyNet) | none shown by GitHub | 1.0.5 on 2026-10-30 | forkpty bindings. No visible licence: do not use without one |
| **Pty.Net** (NuGet, dgriffen) | licence URL only | 0.1.16-pre | Old pre-release. Skip |
| Terminal.Pty, RoyalTerminal's Pty.Unix, Devolutions' forkpty transport | see A | see A | Come with the controls above |

## C. tmux side: three ways to talk to one session

| Way | How it works | Tradeoff |
|---|---|---|
| **`tmux attach` over a pty** | Spawn `tmux -L sindri-launch attach -t <session>` on a pty; the VT engine draws what tmux draws; keys go to the pty | Real screen with colours, mouse and alternate screen, no parsing of tmux's protocol. tmux owns scrollback and copy-mode, so wheel scroll is tmux's, not ours. Size: this client can resize the window (`window-size latest` is the option; `man tmux` 3.4); `attach -r` (read-only, ignore-size) peeks without that. Same approach Squad and agent-deck use, full screen |
| **tmux control mode (`-C` / `-CC`)** | Text protocol. tmux sends `%output %<pane> <bytes>` and other `%` lines; we send tmux commands and key input (`send-keys`). iTerm2 uses it. An empty line detaches. Source: tmux wiki "Control Mode" (docs) | Per-pane output and layout events; a client of our own with flags `ignore-size`, `no-output`, `pause-after=` (all in `man tmux` 3.4). We feed pane bytes to the VT engine and encode keys ourselves. More code and edge cases (output escaping, layout parsing), but we own scrollback and many panes need no pty each |
| **`capture-pane -e -p` polling + `send-keys`** | Read the visible screen with escapes (`-e`) on a timer; type with `send-keys -l` | Code exists already: Meta's `TmuxLanePane.cs` and `TmuxSessionHost.cs:164` do this. Not a real terminal: no cursor control, polling lag, no mouse, flicker risk. Fine for row 3's peek and a one-line composer; wrong for "scrolls and takes keys" |

## What to try first (not a pick)
1. Spike 1: **Devolutions.Terminal.Control** on a pty running `tmux -L sindri-launch attach -r -t <session>`, in a throwaway Avalonia window published NativeAOT on Linux. It is the only candidate with a NativeAOT desktop app behind it. Questions: does the control package pass trim and AOT warnings in our build rails; keystroke-to-echo latency; wheel scroll; resize; the focus-handoff key.
2. Spike 2: **RoyalTerminal.Avalonia** with its C# VT processor and Unix PTY package on the same session. Questions: trim and AOT warnings, preview-version stability, and whether the C# VT engine draws Claude Code's and Codex's full-screen UI (colours, alternate screen, bracketed paste).
3. If both fail the AOT check: our own `forkpty` P/Invoke plus the VT engine of Terminal.Avalonia (MIT) or Devolutions' Core, with a control of our own.
4. In each spike, repeat once with tmux control mode instead of a pty attach and compare code size and echo latency. Keep `capture-pane` polling only for the row-3 peek.

5. RoyalTerminal's spike runs twice: C# VT processor, then its `libghostty-vt` path (T3 Code's engine). Compare Claude Code's full-screen UI drawing and the AOT result.

Focus key, answered by T3 Code: `F6` / `Shift+F6` moves between the list, the pane and other areas without detaching (row 13). The spike checks `F6` reaches neither Claude Code's nor Codex's TUI.
