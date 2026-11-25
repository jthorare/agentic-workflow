# Linear parity ledger

What this is: every Linear function a staff engineer uses in a normal day, scored against what the Sindri app does on `origin/main` (checked 2026-11-12). It is the Linear half of the parity contract in #2978 and the Linear direction in #2961.

The contract, in 3 lines:
1. Each row is `done`, `partial`, `missing #N` (or `missing (no issue)` until one is filed), or `dropped: why`. No row is silently absent.
2. A write lands in Linear within 2 s, optimistic, with a visible failure and a retry. Linear stays the system of record.
3. Every list uses the same grammar: row (32 px, one line), peek (`Space`, side pane), full (the sheet).

How to update a row: change the Status cell and put a file path (or an issue number) in Evidence. "done" needs a path. If you could not check it, write `unverified`, never guess. When you file a gap issue, say `Part of #2961` and replace `missing (no issue)` with `missing #N`.

Sources. Linear column: confirmed = read on linear.app/docs on 2026-11-11 (inbox, triage, github, assigning-issues, agents-in-linear, parent-and-sub-issues, issue-templates, search, custom-views, display-options, notifications, slack, project-milestones, initiatives). `(mem)` = not confirmed: those docs pages returned 404 or an empty JS shell (keyboard-shortcuts, cycles, project-updates, estimates, due-dates, issue-relations), so the key comes from memory or `gap-bar-projects.md`.

Evidence paths are relative to the repo root.

## Findings that change how to read the tables
- The UI doc `docs/csharp/sindri-ui.md:867` ("Not yet: editing an issue, comments, New issue") is stale. Edit, comment and create are built: `src/Sindri/Aw.Sindri.Ui/ViewModels/Linear/LinearIssueSheetViewModel.Edit.cs`, `.Comment.cs`, `NewLinearIssueViewModel`.
- Writes are not optimistic today. The sheet sends, waits, and shows "Saved. The fields show Linear's answer" (`Labels.LinearEdit.cs`). The 2 s target is unverified; no test measures it.
- The ⌘K palette has no Linear entry. `ui.md` lists "Go to issue..." as not yet, and nothing in `ViewModels/Palette/` or `Host/Catalog/` names a Linear read.
- No MCP tool reads or writes Linear issues (`src/Sindri/Aw.Sindri.Host/Mcp/` has only project cards and connection tools). The write catalog rows exist (`Host/Catalog/LinearCommentCreateRows.cs`) but unverified whether an agent session can reach them through MCP or the CLI.

## 1. Inbox and catch-up

| Function | Linear (key / how) | Sindri today | Status | Evidence |
|---|---|---|---|---|
| Open inbox | `G` `I` (confirmed) | No inbox. "Since you last looked" strip on Work > Linear lists assigned, mentioned, state-move items | partial | `ui.md:857`; `Api/Linear/LinearSince.cs` |
| Walk the inbox | `J`/`K` or arrows (confirmed) | Strip rows are clickable; no `J`/`K` inside the strip | partial | `ui.md:857` |
| Mark done / delete | `Backspace`; `Shift+Backspace` all read (confirmed) | Only Dismiss for the whole strip | missing #2991 | `ui.md:857` |
| Mark read / unread | `U`; `Alt+U` all read (confirmed) | None | missing #2991 | |
| Snooze | `H` (confirmed) | None | missing #2991 | |
| Priority tab / filters | Priority filter, show snoozed, unread first (confirmed) | None | missing #2991 | |
| Notification types | assigned, mentioned, status, comment, reactions... (confirmed) | 3 kinds: assigned, mentioned, state | partial | `Api/Linear/LinearSinceItem.cs` |
| Strip is readable | Inbox shows title, actor, text | Strip is "a wall of underlined links with empty states and no titles" | missing #2536 | #2536 |
| Row in Home "Needs you" when assigned | Inbox entry on assign | Home shows "N new on your Linear issues" count only; no per-issue row | missing #2246 | `ui.md:865`; #2246 |
| OS / tray notification | Desktop, mobile, email, Slack channels (confirmed) | Tray tooltip line only | partial | `ui.md:865` |
| Subscribe / unsubscribe | `Shift+S`; `Ctrl/Cmd+Shift+S`; auto on create, assign, mention (confirmed) | None; Sindri reads only issues of the viewer's teams | missing #2991 | |
| Reminders | Reminder on issue / project (confirmed) | None | missing #2991 | |

## 2. Lists, my issues, views, search

| Function | Linear (key / how) | Sindri today | Status | Evidence |
|---|---|---|---|---|
| My issues | `G` `M` (mem) | Work > Linear, group "Assigned to you" (priority, then updated) | done | `ui.md:851`; `Views/Linear/LinearWorkView.axaml` |
| Team issues | Team page, `G` then team | Group "Your teams" | done | `ui.md:851` |
| Switch to Linear view | n/a | `L` toggles Sindri / Linear on Work | done | `ShellViewModel.Linear.cs:42` |
| Move in list | `J`/`K` | `J`/`K`, stop at ends | done | `ShellViewModel.Linear.cs` `LinearViewKey` |
| Open issue | `Enter` | `Enter` opens the issue sheet | done | `ShellViewModel.Linear.cs` |
| Peek | `Space` (confirmed) | None. Only the full sheet | missing #2992 | #2978 grammar |
| Filter | `F`, filter bar (confirmed) | Filter bar: team, project, cycle, state, assignee, priority, label; `/` focuses it | done | `ui.md:851,853`; `Api/Linear/LinearIssueQuery.cs` |
| Filter by due date, estimate, parent, creator, subscriber | Filter bar | Not in the query | missing #2993 | `LinearIssueQuery.cs` |
| Group / sub-group / order | `Shift+V` display options (confirmed) | Fixed: 2 groups, fixed order | missing #2993 | `ui.md:851` |
| List / board toggle | `Ctrl/Cmd+B` (confirmed) | List only | missing #2993 | |
| Saved / custom views | `Alt+V` save, `O` `V` open (confirmed) | None. #2961 says no builder until the four views are loved | dropped: for now, #2961 point 2 | #2961 |
| Pagination 5k rows at frame rate | Virtualized | 50 per page + "Show more"; 5k budget not measured | partial | `ui.md:851` |
| Workspace search | `/` (confirmed) | `/` focuses list filter, not search. Context query searches issue text from the CLI | partial | `ShellViewModel.Linear.cs`; `docs/sindri/context.md:953` |
| Search by key | `lin123` shorthand (confirmed) | Open by deep link `sindri://linear/ENG-12` only | partial | `ui.md:859` |
| Recent issues / quick open | `O` `I` (confirmed) | None | missing #2994 | |
| Go to issue in palette | `Cmd+K` then `i` prefix (confirmed) | Listed "not yet" | missing #2994 | `ui.md:867` |
| Collapse group | `T` on header (confirmed) | None | missing #2993 | |

## 3. Create

| Function | Linear (key / how) | Sindri today | Status | Evidence |
|---|---|---|---|---|
| Create issue | `C` | `C` on the Linear view opens New issue; also header button; palette `C / Ctrl+N` is a Sindri item | done | `ShellViewModel.Linear.cs`; `Views/Linear/NewLinearIssueDialog.axaml` |
| Fields on create | title, description, status, assignee, priority, labels, project, cycle, estimate, due, parent | Dialog has team, title, description, state, assignee, priority, labels, cycle. API body also takes project and parent | partial | `Labels.LinearNew.cs`; `Api/Linear/LinearCreateBody.cs` |
| Estimate / due date on create | fields | Not in body | missing #2995 | `LinearCreateBody.cs` |
| Create from template | `Alt+C` (confirmed) | None | missing #2995 | |
| Create sub-issue | `Ctrl/Cmd+Shift+O` (confirmed) | `ParentKey` in API; no dialog field or key | partial | `LinearCreateBody.cs` |
| Create and repeat | `Ctrl/Cmd+Shift+Enter` (confirmed) | None | missing #2995 | |
| Draft survives close | draft kept | "Resume draft / Discard draft" | done | `Labels.LinearNew.cs` |
| Retry-safe create | n/a | Client id makes a retry idempotent; "Linear didn't confirm the issue. Retry?" | done | `LinearCreateBody.cs`; `Labels.LinearNew.cs` |
| Create issue from a PR | `{TEAM}-NEW` in PR body (confirmed) | None | missing #2995 | |
| Create from Slack | `@Linear`, `/linear` (confirmed) | None (Slack epic #2979) | missing #2979 | #2979 |
| Create from the CLI / by an agent | API | Catalog row `CreateLinearIssue` exists; MCP / CLI reach unverified | unverified | `Host/Catalog/LinearCommentCreateRows.cs` |

## 4. Edit one issue

| Function | Linear (key / how) | Sindri today | Status | Evidence |
|---|---|---|---|---|
| Edit title | click | Title text field in the sheet; `Ctrl/Cmd+S` saves | done | `LinearIssueSheetViewModel.Edit.cs:74`; `LinearIssueSheetView.axaml.cs:46` |
| Edit description (markdown) | click, markdown editor | Plain text box; the read side shows plain text only, images as "[image alt from host]" | partial | `Edit.cs:77`; `ui.md:861` |
| Set status | `S` / `Ctrl+Alt+1..9` (mem) | State picker in the sheet; no key | partial | `Edit.cs:173` |
| Set priority | `P` (mem) | Priority picker; no key | partial | `Edit.cs:175` |
| Assign | `A` (confirmed) | Assignee picker; no key | partial | `Edit.cs:177` |
| Self-assign | `I` (confirmed) | No shortcut or button | missing #2996 | |
| Delegate to agent | `A`, pick the agent; human stays assignee (confirmed) | No delegate field. Sindri's own "start a workflow / run on this item" is separate | missing #2997 | #2961 point 5 |
| Set labels | `L` (mem) | Tick list in the sheet | partial | `Edit.cs:181` |
| Set cycle | `C`-key combo (mem) | Cycle picker | partial | `Edit.cs:179` |
| Set project | `Shift+P` (mem) | Shown, never edited (`Fixed`), although the API change takes `projectId` | partial | `Edit.cs:183`; `Api/Linear/LinearChangeBody.cs` |
| Set estimate | `E` (mem) | Not in the sheet; Context change supports it | missing #2996 | `Context.Core/Linear/LinearIssueChange.cs` |
| Set due date | `Shift+D` (mem) | Read only in the side table; no write field | missing #2996 | `LinearIssueFields.cs`; `LinearIssueChange.cs` |
| Set / remove parent | `Ctrl/Cmd+Shift+P` (confirmed) | Context change supports it; sheet has no field; read does not carry a parent | missing #2996 | `ui.md:859` |
| Milestone | `Shift+M` (confirmed) | None | missing #2996 | |
| Edit conflict handling | last write wins | Opened-vs-set check; 409 shows Linear's value beside yours with Use Linear's / Save mine anyway | done (better than Linear) | `Labels.LinearEdit.cs`; `Api/Linear/LinearChangeConflict.cs` |
| Failure and unknown outcome | optimistic, silent retry | Visible "Linear didn't confirm it. Retry?"; rate-limit message with time | done | `Labels.LinearEdit.cs` |
| Write within 2 s, optimistic | instant | Not optimistic; latency unmeasured | partial | #2978 contract |
| Discard guard | n/a | "Discard changes to ENG-12?" | done | `Labels.LinearEdit.cs` |
| Write key setup | OAuth | Separate write key pasted in Settings > Connections > Linear; "Edits need write access" until set | done | `LinearConnectionViewModel.cs`; `Labels.LinearEdit.cs` |

## 5. Comments, relations, attachments, history

| Function | Linear (key / how) | Sindri today | Status | Evidence |
|---|---|---|---|---|
| Read comments | thread | Newest first, author, age, "Sindri" mark | done | `ui.md:859` |
| Post comment | `Cmd+Enter` | Post button; `Ctrl/Cmd+Enter` posts; retry-safe with client id | done | `LinearIssueSheetView.axaml.cs:55`; `Labels.LinearEdit.cs` |
| Comment with markdown | markdown | Sent as text; shown as plain text | partial | `ui.md:861` |
| @mention with picker | `@` | No picker; typed text only | missing #2998 | |
| Read mentions of me | Inbox | Strip "mentioned" kind | done | `LinearSinceItem.cs` |
| Reactions | emoji | None | missing #2998 | |
| Edit / delete / resolve a comment, reply in thread | menu | Post only, no reply threading | missing #2998 | |
| Relations: blocks / blocked by / duplicate / related | `Cmd+K` "Add relation" (mem) | Read in stored text only (`Relations:` section); no UI field, no write | missing #3000 | `docs/sindri/linear.md` section 4 |
| Mark duplicate | `MM` (confirmed in triage) | None | missing #3000 | |
| Attachments and links | `Ctrl+L` (confirmed) | Read: PR attachments become links to GitHub PR records. Write client exists in Context (`LinearWriteClient`), no UI | partial | `LinearWriteClient.cs:102`; `docs/sindri/linear.md` section 5 |
| PR auto-link by key | branch name, PR title, magic words (confirmed) | Read side: issue key in PR title/body links PR and issue both ways ("known-as"); sheet shows PR chip "PR #2071 waiting on you" and opens the review page | done | `docs/sindri/linear.md` section 5; `ui.md:859` |
| PR status automation | Draft, open, review, ready, merged move status (confirmed) | Linear does it server-side; Sindri shows the new state on next sync. It adds PR review state to the row, which Linear lacks | done | `ui.md:851` |
| Activity / history | Issue history | None; sheet shows comments only | missing #2998 | |
| Subscribers list | sidebar | None | missing #2998 | |
| Archive / delete issue | `Cmd+Backspace` / menu (mem) | None. An issue Linear stops serving shows "no longer served" | missing #2998 | `ui.md:863` |
| Copy branch name / open in coding tool | `Ctrl/Cmd+Shift+.`, `W` `O` (confirmed) | None. "Y copy the row's command" does nothing on Linear rows | missing #2998 | `ShellViewModel.Linear.cs` |
| Open in Linear | menu | Button when an issue is not synced | partial | `ui.md:863` |
| Images in description | inline | Never fetched; shown as text | dropped: untrusted-content rule, `ui.md:861` | `ui.md:861` |

## 6. Triage, bulk, keyboard

| Function | Linear (key / how) | Sindri today | Status | Evidence |
|---|---|---|---|---|
| Triage queue | `G` `T` (confirmed) | State type `triage` is stored; no Triage view | missing #3002 | `LinearIssueFields.cs` |
| Accept | `1` (confirmed) | None | missing #3002 | |
| Decline | `2` (confirmed) | None | missing #3002 | |
| Duplicate | `3` or `MM` (confirmed) | None | missing #3002 | |
| Snooze in triage | `H` (confirmed) | None | missing #3002 | |
| Triage responsibility (rotation) | Business plan (confirmed) | None | dropped: Linear plan feature, not a daily action | |
| Multi-select | `X` (confirmed) | None | missing #3004 | |
| Bulk edit status / assignee / label | after `X` | None | missing #3004 | |
| Command menu | `Cmd+K` | `Cmd/Ctrl+K` palette exists, no Linear commands | partial | `Labels.Palette.cs:59` |
| Shortcut sheet | `?` | `?` sheet exists, no Linear rows (J/K/L/C are in the list) | partial | `ShortcutsViewModel.cs` |
| G-nav to Linear places | `G` `I`, `G` `M`, `G` `T`, `G` `P` | Sindri `G` then `H N D T P Q C A . S V J O M W R U`; Work (`Q`) is the Linear home; no My issues or Triage target | partial | `Labels.Palette.cs:60` |
| Undo | `Cmd+Z` | `Cmd/Ctrl+Z` undoes the last Sindri action; unverified for Linear writes | unverified | `Labels.Palette.cs:74` |
| Offline reads | local-first DB | Reads come from the local store (the sheet read "is local") | done | `ui.md:863` |
| Offline writes queue | queued, flushed | Fails visibly with Retry; no queue | missing #3004 | `Labels.LinearEdit.cs` |
| Open-issue deep link | `linear.app/...` | `sindri://linear/ENG-12` | done | `ui.md:859` |
| Keyboard, <100 ms per action | yes | Unmeasured for Linear view (Projects page has its own gap list: #2962 to #2967) | unverified | #2961 |

## 7. Projects, initiatives, cycles

| Function | Linear (key / how) | Sindri today | Status | Evidence |
|---|---|---|---|---|
| Projects list | Projects page | Projects page: led, attached and hand-made cards from the store | done | `docs/superpowers/specs/2026-10-29-sindri-projects-page-design.md`; `Api/Projects/ProjectCardView.cs` |
| Project health (read) | on track / at risk / off track (confirmed) | Card shows Linear's newest update health | done | projects spec, health row |
| Post a project update | update with health | Read only; Linear projects are read-only here (state is Linear's) | missing #3006 | #2962 note |
| Project state / hide / notes | n/a | Hand-made: S picker; hide / unhide, notes; Linear-fed shows "state is Linear's" | done | `Api/Projects/ProjectHide.cs`; #2961 comment |
| Card opens something | click through | "A card opens nothing; counts link nowhere" | missing #2971 | #2971 |
| Project keys (`S`, `H`, `E`, `O`, `Delete`) | n/a | Missing | missing #2962 | #2962 |
| Projects `J`/`K`, `Home`/`End` | n/a | Missing | missing #2966 | #2966 |
| Projects `/` filter | n/a | Missing | missing #2965 | #2965 |
| Projects in palette, `C` adds a project | n/a | Missing | missing #2964 | #2964 |
| Project sheet save keys | n/a | Missing | missing #2967 | #2967 |
| Projects layout and freshness rows | n/a | Three card layouts; source line reads as four errors | missing #2972, #2805, #2806 | #2972, #2805, #2806 |
| Project issues list | project page | Work > Linear filter by project | partial | `LinearIssueQuery.cs` |
| Project milestones | milestones, `Shift+M` (confirmed) | None | missing #3006 | |
| Project timeline / roadmap | timeline layout (confirmed) | Dropped for now: projects spec non-goal | dropped: projects spec "Issue lists, burndown, cycles" non-goal; revisit under #2961 | projects spec |
| Initiatives | status, owner, health (confirmed) | None | missing #3006 | |
| Initiative / project updates to Slack | update posts to channel (confirmed) | None | missing #2979 | #2979 |
| Cycles: see current | Cycle page (mem) | Filter by cycle; cycle shown on issue | partial | `LinearIssueQuery.cs` |
| Cycles: rollover, cooldown, burn-up | auto-rollover (mem, UNCONFIRMED) | None | missing #3006 | |
| Cycle lookups (current, next, last 3) | n/a | Synced into pickers | done | `docs/sindri/linear.md` "Lookups for the pickers" |
| Slack link on issue | synced threads (confirmed) | Read: Slack threads on the issue attach as links | partial | `docs/sindri/linear.md` section "What it reads" |

## Counts

Run `grep -c` on the Status column to refresh; the totals at the time of writing are below.

| Status | Rows |
|---|---|
| done | 25 |
| partial | 26 |
| missing #N (issue filed) | 55 |
| missing (no issue) | 0 |
| dropped on purpose | 4 |
| unverified | 3 |
| total | 113 |

## Top 10 missing, by how often a staff engineer hits them in a day

1. Inbox actions: mark done, snooze, filter (`E`/`Backspace`, `H`). Hit 20+ times a day. Only Dismiss-all exists.
2. Single-key edits on a hovered row: `S` status, `A` assign, `I` self-assign, `P` priority. Hit 10-30 times. Sindri has pickers in the sheet only.
3. Triage: accept `1`, decline `2`, duplicate `3`, snooze. Daily for anyone on call.
4. Command palette entries for Linear (go to issue, set status, assign) and `/` as real search. The fallback for every forgotten key.
5. Peek (`Space`) on a row. Needed to read 25 rows without leaving the list.
6. Multi-select (`X`) plus bulk edit. Backlog grooming and sprint cleanup.
7. Delegate to an agent from the row. Core to #2961 point 5.
8. @mention picker and reactions in comments. Every review thread.
9. Relations (blocks / blocked by / duplicate) and parent / sub-issue create. Used daily in planning.
10. Saved views and group / order options. The "my open, no cycle" view. (#2961 postpones this on purpose; the four fixed views must cover it first.)

Close behind: due date and estimate fields, project update with health, optimistic write under 2 s, MCP / CLI read and write of Linear issues for agents.
