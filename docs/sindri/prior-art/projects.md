# Prior art: Projects (Linear replacement)

This file is the prior-art ledger for the Projects surface of the Sindri desktop app (rule: issue #2989). It lists daily jobs, who does each best, what we copy, and the verdict.
Sources are vendor docs and changelogs unless marked. "unconfirmed" means I could not verify it from a primary page. Screens are linked as docs pages; I did not capture screenshots (research only, no browser).
Surface job: one row per piece of work (issue + PR + CI + review + agent run) with four views and full write-through, faster than Linear + GitHub. Direction issue: #2961. Grammar: #2978. Linear function parity is a separate file (`parity-linear.md`), not repeated here.
"Desktop gain" numbers are targets from #2961/#2978 for perf to measure, not measurements.

Sindri today (origin/main): Projects page = card grid (`Views/Projects/ProjectsPage.axaml`, `ProjectCard.axaml`, `CardGrid.cs`); Linear feed page (`Views/Linear/LinearWorkView.axaml`: since-you-looked strip, filter bar, two row groups, Show more); issue sheet with Ctrl+S save (`LinearIssueSheetView.axaml.cs:46`); new-issue dialog; Queue page (`Views/Queue/QueuePage.axaml`). Open gaps filed under #2961: #2962, #2964-2967, #2971-2973, #2991-2998, #3000, #3002, #3004, #3006.

---

## 1. Know what needs me right now, ranked

**Best today**
- GitHub pull requests dashboard (GA 2026-07-09, vendor changelog): Inbox with review requests, PRs needing fixes (CI failures, new comments), PRs ready to merge or in merge queue; sections can be reordered or hidden; author filter includes agent PRs. https://github.blog/changelog/2026-07-09-new-pull-requests-dashboard-is-now-generally-available source: docs
- Graphite PR inbox: six default sections (Needs your review, Approved, Returned to you, Merging and recently merged, Drafts, Waiting for review); drag to reorder; share section filters; review from the section. https://graphite.com/docs/use-pr-inbox source: docs
- Mergeable (self-hosted, Show HN May 2025): arbitrary sections, each a search query; keyboard nav plus command bar; data in browser storage. https://pvcnt.github.io/mergeable/user-guide/inbox source: docs
- Linear Inbox: `G` then `I` opens it, `H` snoozes. https://linear.app/docs/inbox source: docs

**What we take**: Graphite's section names are the best answer to "whose turn is it": split by who the item waits on (me / reviewer / CI / agent), not by object type. Each section is a saved query; the four sections that matter are Needs me, Returned to me (changes requested, red CI, conflict), Ready to merge, Waiting on others. License: Mergeable is open source (license unconfirmed); we copy the idea, not code.
**Where one app wins**: GitHub's inbox sees no Linear state and no agent run; Graphite sees no issues. Ours ranks one list across issue, PR, CI, agent.
**Desktop gain**: first paint of the list < 100 ms from the local store (#2978 budget).
**Sindri today**: Home/Queue page exists; no "waits on" column on Projects/Linear rows; #2961 item 1 not built. Issue: missing (covered by #2961 itself).
**Verdict**: take and change (rank by what unblocks the most; add agent and Linear sources).

## 2. See my work by state, one line each

**Best today**
- Linear My Issues + display options: group by status/priority/assignee/cycle/project; order; list or board; choose shown properties. https://linear.app/docs/display-options source: docs
- GitHub Projects table layout: high-density table, board, roadmap; board column field sums. https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects source: docs
- gh-dash (MIT, per pkg.go.dev listing): sections are a title plus a GitHub search filter (`is:open author:@me`, `review-requested:@me`); vim keys; custom keybindings run shell commands. https://github.com/dlvhdr/gh-dash source: docs

**What we take**: default group by state with collapsible groups, 32 px rows, columns fixed: glyph, key, title, PR/CI glyph, who it waits on, age. No view builder; display options limited to group (state/project/none) and order. gh-dash's "section = a query" is the model for the four views.
**Where one app wins**: the state group can use PR truth (In review only if a PR is open and not draft), which Linear and GitHub each get half right.
**Desktop gain**: 5k rows scroll at 60 fps (#2961 item 4).
**Sindri today**: Linear feed has two groups + Show more (`LinearWorkView.axaml`); grouping/order/board = missing (#2993).
**Verdict**: take and change.

## 3. Status that reflects the PR (red CI, changes requested, conflict)

**Best today**
- Linear GitHub integration: per-team statuses for PR open, PR merge, and "ready to merge" (checks passed); waits for all linked PRs to merge before the merge step; shows review requested / in review on the attachment. Does not model red CI or conflict as issue state (my read of the docs; unconfirmed that no such state exists). https://linear.app/docs/github , https://linear.app/changelog/2023-11-15-github-workflow-updates source: docs
- Shortcut: event handlers map branch associated / PR opened / review requested / merged to a workflow state; moves forward only, never back automatically. https://help.shortcut.com/hc/en-us/articles/207540323 source: docs
- GitHub Projects built-in workflows: closed and merged PR set Status to Done by default. https://docs.github.com/en/issues/planning-and-tracking-with-projects/automating-your-project/using-the-built-in-automations source: docs
- gh-pr-status (gh extension, small): CI status, review decision, merge readiness and thread counts in one view (per search listing). https://awesome.ecosyste.ms/projects/github.com%2Fjoeyism%2Fgh-pr-status source: docs

**What we take**: do not move a typed status; derive a second, read-only "work state" from the PR: draft / in review / changes requested / CI red / conflict / ready to merge / merged. Show it as one glyph + word on the row. The typed Linear state still writes through (separate column). Take Shortcut's rule "automation moves forward only" for any write we do on the user's behalf.
**Where one app wins**: only we hold issue, PR, check runs and review threads in one store.
**Desktop gain**: state recomputes locally on each event; a CI flip shows on the row without a refetch (target < 2 s from webhook/poll, which perf should measure).
**Sindri today**: Projects cards show open issue/PR counts and a health dot (#2971 says counts link nowhere); per-row PR state = missing. No open issue for per-row PR truth: **gap to file**.
**Verdict**: ours can be better; take Linear's per-team mapping UI idea only if users want to change defaults (skip for v1).

## 4. Triage new and unowned work fast

**Best today**
- Linear Triage: accept / decline (cancels, optional comment) / mark duplicate (merges attachments) / snooze; keys `1`/`2`/`3`/`H` but docs disagree on 2 vs 3 (unresolved). Responsibility routing and Triage Intelligence are Business/Enterprise. https://linear.app/docs/triage source: docs
- Plane Intake (AGPL-3.0): accept, decline, snooze, duplicate; select several to accept, decline or assign in one action. https://docs.plane.so/intake/overview source: docs
- Universal Inbox (open source, license unconfirmed): turns GitHub/Linear/Slack notifications into tasks. https://github.com/universal-inbox source: docs

**What we take**: Triage view = unowned or new items from all sources; four actions on four single keys, applied to a multi-selection; Decline asks for no comment unless the submitter is external. One list, auto-advance to the next row after an action.
**Where one app wins**: a GitHub issue, a Linear issue and a Sindri item land in the same triage list; duplicate detection can cross sources.
**Desktop gain**: action applies locally at once, next row focused in < 50 ms.
**Sindri today**: missing; #3002.
**Verdict**: take as is (Linear's four actions), take Plane's multi-select.

## 5. Create an item in 10 seconds from anywhere

**Best today**
- Linear: `C` in app, `Alt+C` from template; Raycast extension "Create Issue"; Slack message action. I found no official system-wide capture window (unconfirmed). https://linear.app/docs/creating-issues , https://linear.app/integrations/raycast source: docs
- Raycast + Linear extension for global creation (third-party-hosted store page). https://www.raycast.com/extensions/linear source: docs
- Jira: none found that beats a browser form; community threads ask for a native quick-add (low-quality source). Marked weak. source: docs

**What we take**: `C` anywhere in the app opens a one-line title field with project, assignee-me, state defaults pre-set; Enter creates, Shift+Enter creates and opens, the new row appears at once. Add a global hotkey from the tray (desktop app can; web tools cannot). Create from a PR or a Slack thread = pre-filled link.
**Where one app wins**: "create from this PR / this message / this failed run" with the link already attached.
**Desktop gain**: global hotkey capture without the main window; title to row < 100 ms optimistic.
**Sindri today**: new-issue dialog exists (`Views/Linear/NewLinearIssueDialog.axaml`); `C` on Projects not wired (#2964); estimate/template/from-PR = #2995.
**Verdict**: take and change (add global hotkey).

## 6. Edit one row without opening it (single keys, peek)

**Best today**
- Linear: hover or focus then `A` assign, `I` assign to me, set status by key; `Space` peeks (per a third-party cheat sheet, unconfirmed in docs). https://linear.app/docs/assigning-issues , https://fastshortcuts.com/shortcuts/linear/ source: docs
- Mergeable and gh-dash: keyboard navigation; gh-dash custom keys. See row 1-2 links. source: docs
- Universal Inbox documents its own keys per source. https://universal-inbox-doc.pages.dev/quick_start/inbox_screen source: docs

**What we take**: the #2978 grammar already matches (`j/k`, `Space` peek, Enter, Esc). Add `s` state, `a` assign, `i` assign to me, `p` priority, `l` label. Action menu opens at the row, filtered by typing; focus stays on the row afterward.
**Where one app wins**: `.` actions can include "open PR", "re-run CI", "hand to agent" next to Linear fields.
**Desktop gain**: key to visible change < 50 ms.
**Sindri today**: no row keys on Projects (#2962, #2966); Linear row edits #2996; peek #2992.
**Verdict**: take as is.

## 7. Bulk edit

**Best today**
- Linear: hold Shift with up/down to select many, then a bulk action. https://linear.app/docs/select-issues source: docs
- Plane Intake: multi-select accept/decline/assign. https://docs.plane.so/intake/overview source: docs
- Jira: users report Shift-select not working in Advanced Roadmaps; checkboxes only (community thread, weak). https://community.atlassian.com/t/Jira-Software-questions/Advanced-Roadmaps-Select-Multiple/qaq-p/1689513 source: docs

**What we take**: `x` toggles a row, Shift+j/k extends, `Ctrl+A` selects the group; any single-key edit then applies to all selected. A failure on one row shows on that row with Retry, not a dialog.
**Where one app wins**: bulk "hand 5 issues to agents" or "move 8 to next cycle" across sources.
**Desktop gain**: 100 rows edited locally in one frame; network writes batch in the background.
**Sindri today**: missing; #3004.
**Verdict**: take as is.

## 8. Real project health from PR and CI data

**Best today**
- Linear project updates: health On track / At risk / Off track set by a person, with a short text; reminders on a schedule to the lead; overdue follow-ups; can post to Slack; Linear added agent-drafted updates (June 2026, per changelog cited in prior notes, not re-fetched). https://linear.app/docs/project-updates source: docs
- Linear cycle graph: scope line, started, completed. https://linear.app/docs/cycle-graph source: docs
- GitHub Projects insights: charts built from project items. https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects source: docs
- GitBiased (commercial): PR tracking, CI health, DORA-style metrics, shareable read-only dashboard. https://www.producthunt.com/products/gitbiased source: docs

**What we take**: computed health, shown as a word plus the reason: "At risk: 3 of 9 PRs red > 2 days, 2 changes-requested". Rules are fixed and listed in the peek (no scripting). A person can override with a note, like Linear's update. Burn-up from done/total issues (what #2971 asks for), plus PR throughput. No predictions in v1.
**Where one app wins**: Linear health is hand-typed; GitHub charts lack Linear scope. Only we combine both.
**Desktop gain**: health recomputed from the local store on each event, no report job.
**Sindri today**: health dot on cards (value source unconfirmed from code); no progress bar (#2971 item 3).
**Verdict**: ours is better in principle; needs build. Take and change.

## 9. Project update without a meeting

**Best today**
- Linear: lead gets Inbox reminder on a chosen day/hour in their timezone, only for In Progress projects; follow-ups when overdue; comments sync with Slack. https://linear.app/docs/initiative-and-project-updates source: docs
- Plane: agents triggered by mention, assignment or schedule could draft updates (unconfirmed for updates specifically). https://plane.so/blog/agents-are-now-live-in-plane source: docs
- Height (shut down 2025-09-24): AI kept docs/specs updated and recorded decisions (vendor description). https://alternativeto.net/news/2025/3/height-project-management-tool-to-shut-down-by-september-2025 source: docs

**What we take**: one button "Draft update": computed health + merged PRs since last update + open risks, as editable text; post writes to Linear (project update) and optionally a Slack channel. Reminder lands as a Home row, not an email.
**Where one app wins**: the draft is built from PR/CI/agent data Linear lacks.
**Desktop gain**: draft ready in < 1 s from local data (agent polish optional).
**Sindri today**: missing; #3006.
**Verdict**: take and change.

## 10. Hand work to an agent and see its state on the row

**Best today**
- Linear: assign to an agent makes it the delegate; you stay assignee; `A` or mention; sessions are waiting for input / working / completed / errored; filter by Delegate. Coding sessions return a draft PR and use AI credits. https://linear.app/docs/assigning-issues , https://linear.app/docs/coding-sessions source: docs
- Plane: assign or mention an agent; each run is an Agent Run; Cursor example links the PR back. https://developers.plane.so/dev-tools/agents/overview source: docs
- Jira: assign to Rovo/Copilot/Claude/Cursor agents from the assignee dropdown; PRs link back. https://www.atlassian.com/blog/company-news/claude-agent-for-jira source: docs
- Vibe Kanban (BloopAI, Apache-2.0 per a third-party listing, unconfirmed on the LICENSE file): kanban cards, one git worktree per workspace, PR creation. https://github.com/BloopAI/vibe-kanban source: docs
- Conductor (closed source per a competitor page): workspaces per agent, at-a-glance dashboard, diff review, checks, PR, merge. https://www.conductor.build/ source: docs

**What we take**: delegate-vs-assignee split (human stays accountable). Row shows agent glyph + state (working / needs you / done / failed) + last tool or step in the peek. "Ask an agent to fix" and "Scope this" on `.` actions. Agent run is a child of the row, not a separate list.
**Where one app wins**: our own lanes and sessions are the agents; Linear needs each wired as an app.
**Desktop gain**: live run state from local sessions with no webhook hop; state change on the row in < 1 s.
**Sindri today**: missing on rows; #2997.
**Verdict**: take and change.

## 11. One row across Linear, GitHub and Jira

**Best today**
- Huly GitHub sync (EPL-2.0 per listings, unconfirmed on repo): syncs issues and PRs with a Huly project; open PR #10889 proposes detecting ABC-123 in PR titles to link instead of duplicating. https://docs.huly.io/integrations/github/ source: docs
- Universal Inbox: GitHub + Linear + Slack notifications to one list. https://github.com/universal-inbox source: docs
- Linear/Shortcut link PRs by issue key in branch or title. https://linear.app/docs/github source: docs
- Cross-tool sync tools (Exalate etc.) exist; vendor products, not reviewed. source: docs

**What we take**: key-based linking (ABC-123 in branch/title/body) builds the edge between issue and PR; the PR is never mirrored as a second task. Sources stay the system of record; a row shows a source chip (small muted word, per #2972). No import; read-through plus write-through.
**Where one app wins**: no other tool holds all three without sync copies.
**Desktop gain**: join happens in the local store; no cross-service query at read time.
**Sindri today**: Linear + GitHub + by-hand projects on one page with three layouts (#2972); Jira = missing (no issue found; **gap to file** if Jira is in scope).
**Verdict**: take and change (link by key, no mirrors).

## 12. Plan a cycle / sprint

**Best today**
- Linear cycles: automated schedule, rollover of unfinished issues, optional auto-add of started/completed issues without a cycle, graph of scope/started/completed. https://linear.app/docs/use-cycles source: docs
- Plane cycles (AGPL-3.0): time-boxed sprints with burndown (per listings). https://docs.plane.so/core-concepts/cycles (URL unconfirmed) source: docs
- Shortcut iterations: fixed periods for a set of stories; epics and objectives above. https://www.shortcut.com/help/getting-started/quick-start source: docs
- GitHub Projects iteration fields, grouped by iteration. https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects source: docs

**What we take**: one cycle concept (Linear's), with rollover on close and a scope-change line. Keep it as a column and a grouping, not a separate area. Skip epics/objectives/initiatives for now (Shortcut's feature count is the cautionary tale).
**Where one app wins**: cycle burn-up counts PR-merged work, not only status changes.
**Desktop gain**: graph from local data, no report load.
**Sindri today**: missing; cycles listed in #3006.
**Verdict**: take and change; low priority behind rows 1-10.

## 13. Search and filter without a view builder

**Best today**
- gh-dash / Mergeable: a section is a plain search query. See rows 1-2. source: docs
- Linear filters: filter chips, `Alt+V` turns a filter into a saved custom view; `/` for search (third-party). https://linear.app/docs/filters source: docs
- GitHub Projects: filter bar with qualifiers; auto-add filters use `is:pr label:` etc. https://docs.github.com/en/issues/planning-and-tracking-with-projects/automating-your-project/adding-items-automatically source: docs

**What we take**: `/` opens one text box with query words (`state:review waits:ci repo:x @me`) and completion; typing plain words filters by title live. No saved-view builder; "pin this filter" adds a fifth section only after the four views are loved (#2961 item 2).
**Where one app wins**: query words span PR/CI/agent fields no single tool indexes.
**Desktop gain**: filter 5k rows < 16 ms per keystroke (target).
**Sindri today**: filter bar on Linear feed; Projects has no type-to-filter (#2965).
**Verdict**: take as is (query line), skip view builder.

## 14. Keyboard speed and optimistic writes

**Best today**
- Linear sync engine: local store, writes applied locally first then synced; same code path for local and remote changes (summary of Artman's talk; no published latency numbers found). https://linear.app/now/scaling-the-linear-sync-engine , https://performance.dev/what-makes-linear-feel-instant source: docs
- Mergeable: all data local in browser storage; command bar. https://pvcnt.github.io/mergeable/overview source: docs
- Universal Inbox keeps source, task manager and inbox in sync. https://universal-inbox-doc.pages.dev/how/collecting_notifications source: docs

**What we take**: every write is local first, queued, flushed to the source within 2 s, with a visible failed state and Retry on the row (#2978 contract 2). Offline queue survives restart. Keyboard map from row 6; Ctrl+K reaches every action.
**Where one app wins**: Linear's engine speeds only Linear; ours is one store for all sources.
**Desktop gain**: keys answer < 50 ms; first paint < 100 ms; offline reads (all measured by perf in the real app).
**Sindri today**: sheet save on Ctrl+S; palette entries for Projects missing (#2964); optimistic + offline queue = #3004.
**Verdict**: take as is (design); the build is the work.

## 15. Catch up on what changed since I looked

**Best today**
- Linear inbox with snooze and mark-done. https://linear.app/docs/inbox source: docs
- GitHub dashboard: sections with counts. See row 1. source: docs
- Sindri's own since-you-looked strip. `LinearWorkView.axaml` (strip with groups, Mark all seen). source: docs

**What we take**: keep our strip; make it a ranked 5-10 line brief with links (#2536). No competitor does this as a brief that I found.
**Where one app wins**: the brief covers PR, CI, agent and issue changes together.
**Desktop gain**: built from local change log; no fetch.
**Sindri today**: strip exists for Linear; brief across sources = #2536.
**Verdict**: ours is already ahead in shape; extend.

---

## Where I found nothing solid
- Jira: no primary source on its row-level PR/CI status or quick create; only vendor agent posts and weak community threads.
- Huly and Plane keyboard shortcuts and row density: not found; walk the apps before copying.
- Linear automatic "at risk" prediction: no official page found; health looks hand-set.
- Real Reddit/HN threads on complaints: none retrieved.
- Screenshots: none captured; every link is a docs page.

## Summary table

| Problem | Best tool | Verdict | Sindri today (issue) |
|---|---|---|---|
| 1 What needs me | GitHub PR dashboard, Graphite inbox | take and change | missing (#2961) |
| 2 My work by state | Linear display options, gh-dash | take and change | partial (#2993) |
| 3 Status from PR truth | Linear GitHub mapping, Shortcut handlers | ours can be better | missing, file gap |
| 4 Triage | Linear Triage, Plane Intake | take as is | missing (#3002) |
| 5 Create in 10 s | Linear `C` + Raycast | take and change | partial (#2964, #2995) |
| 6 Single-key edit, peek | Linear | take as is | missing (#2962, #2996, #2992) |
| 7 Bulk edit | Linear, Plane Intake | take as is | missing (#3004) |
| 8 Real health | Linear updates, GitBiased | take and change | partial (#2971) |
| 9 Update without meeting | Linear reminders | take and change | missing (#3006) |
| 10 Agent on the row | Linear delegate, Plane runs | take and change | missing (#2997) |
| 11 One row across tools | Huly sync, key linking | take and change | partial (#2972), Jira gap |
| 12 Cycles | Linear cycles | take and change, low priority | missing (#3006) |
| 13 Search without builder | gh-dash, Mergeable | take as is | partial (#2965) |
| 14 Keys + optimistic writes | Linear sync engine | take as is | partial (#3004, #2964) |
| 15 Catch up brief | none; ours | ours ahead | partial (#2536) |

## What we take

1. Triage view for new and unowned items: four actions on four single keys, applied to a multi-selection, auto-advance to the next row (#3002).
2. Single-key edits on a row (state, assign, assign to me, priority, label) plus Space to peek, focus staying on the row (#2996).
3. Bulk edit: `x` marks rows, any single-key edit applies to all, a failure shows on its row with Retry (#3004).
4. Sections by whose turn it is (Needs me, Returned to me, waiting on reviewer / CI / agent), one row with the truth (#2961).
5. Derived read-only work state on each row from the PR: draft, in review, changes requested, CI red, conflict, ready, merged (#3017).
6. Agent glyph and state (working, needs you, done, failed) on the row, with "Ask an agent to fix" in the actions (#2997).
7. `/` opens one query line (`state:review waits:ci repo:x @me`) that also filters live by title (#2965).
