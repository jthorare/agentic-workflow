---
aw-doc: 1
id: sindri-todo
kind: spec
title: "Sindri's todos: the owner's own list on Home, synced with one Obsidian note, plus what other pages say is on their plate"
status: draft
repos: [jthorare/agentic-workflow]
---

# Sindri's todos

**Status:** draft for the owner, revision 2 (the owner answered §12 on 2026-11-07) · **Date:** 2026-11-07 · **Lane:** ideas (this spec), with api (store, routes, CLI, MCP), context (the Obsidian note sync) and ui (the Home panel) · **Builds on:** the Projects page spec `2026-10-29-sindri-projects-page-design.md` (§5's owner-state pattern: ledger events, a projection, desk-class commands), the Linear hub spec `2026-11-04-sindri-linear-hub-design.md` (Settings → Connections; the Work → Linear view), the PR review page spec `2026-11-04-sindri-pr-review-page-design.md` (`context.sources.library.vaults`, the Reviews waiting count), `docs/csharp/sindri-ui.md` (Home, the G chord, sheets) · **Next:** three /autoplan rounds; meta's UX pass; the owner reads; then /specToProvenPR takes §10's phases.

Every file path below is one the author opened on main (78750fe). Every command, route, event, key and type is marked **existing** or **new**.

**Not in scope: notes.** The owner keeps notes in Obsidian (owner, 2026-11-07). A todo is one line, with an optional due date and optional links. It has no body, no checklist and no formatting. The one Obsidian note this spec touches is a list of those lines (§4.5).

## The owner's steps: install -> works

1. **See what's next.** Open Sindri. The top of Home is your todo panel: the next five open todos, overdue first, then today, then upcoming. `G T` from any page lands you there with focus in the panel.
2. **Add one from anywhere.** Press `N` on any page. A one-line box opens at the top of the window. Type "reply to Sam about the offsite fri" and press Enter. The todo is saved with Friday as its due date, and the box closes. Esc closes it without saving.
3. **Check one off.** In the panel, `J`/`K` moves between rows and `X` checks the row off. A "Done. Undo" toast stays for 5 seconds.
4. **Keep it in Obsidian too (once).** Settings → Connections → **Obsidian todo note**: pick one of your library vaults, name the note (`Todo.md`), **Save**. Sindri writes your todos into that note as `- [ ]` lines and reads back what you change there, from this laptop or through Obsidian Sync from your phone.
5. **If you changed the same todo in both places**, Sindri doesn't guess. The panel shows "Changed in Sindri and in Obsidian" with both versions side by side, and you pick one.
6. **From the terminal or an agent.** `sindri todo add "rotate the staging key" --due 2026-11-10`, `sindri todos`, `sindri todo done td_…`. Agent sessions have the same access through the `sindri_todos` and `sindri_todo` MCP tools (D2).

## 0. Why

The owner runs their day from Sindri: PR reviews, workflows, agents, docs, Linear (owner goals, 2026-11-04: "personal mega app on the work laptop"). Sindri already tracks what's waiting on the owner (Needs you, PRs waiting on review, Linear issues assigned to them). It has no place for the owner's own todos, things like "reply to Sam" or "renew the cert", that have no repo and no run. Today those live in Obsidian, so the owner's plate is split across two apps.

The owner's ranking (§12 Q7): **seeing what's next must feel instant**, more than capture speed. That's why the list sits at the top of Home, the page Sindri opens on, and not behind a tab.

Why not Sindri's items? An item (`sindri item add <title> [--repo]`, existing, `docs/csharp/sindri.md:42`) is a unit of agent work. It belongs to a repo of the approved profile (`SND-CLI-003` otherwise), it moves through the queue's groups (`waitingOnYou`, `running`, `queued`, ...), and recipes run on it. A personal todo has none of that. Making todos items would put "call the dentist" in the Work queue and need a fake repo. So todos get their own small store.

What exists today (main 78750fe):

| Piece | State | Where |
|---|---|---|
| Owner state in the ledger | **Existing pattern.** `project.attached` / `changed` / `detached` events folded by `ProjectsProjection` into `views.db`, a stale version refused with `SND-PROJECT-409`, catalog rows in `Host/Catalog/ProjectRows.cs` | `src/Sindri/Aw.Sindri.Core/Projects/ProjectsProjection.cs`, Projects spec §5 |
| What's waiting on the owner | **Existing routes.** `GET /sindri/needs-you`, `GET /sindri/reviews` (the Reviews badge's waiting count), `GET /sindri/linear/issues` (Work → Linear, "Assigned to you") | `Host/Endpoints/GetNeedsYouEndpoint.cs`, `GetReviewsEndpoint.cs`, `ListLinearIssuesEndpoint.cs` |
| Home | **Existing.** Home is the Needs you list. It already owns `J`/`K` (move), `A` (primary action), `X` (**Reject**), `Y` (copy), Enter (sheet) | `ViewModels/NeedsYou/NeedsYouViewModel.Keys.cs:80-87` |
| G chord | **Existing.** Second keys H/N (Home), D (Home, decisions filter), P, W, Q, R, C, A, ./S, V, J, U. `T` is free | `ViewModels/Shell/ShellViewModel.Chords.cs:109` |
| Obsidian vaults | **Existing, read-only.** `context.sources.library.vaults` (`{ name, path, deny, models }`, at most 10) names the vaults the design-doc library feed reads. Sindri never writes a vault today | `src/Sindri/Aw.Sindri.Core/Reviews/LibraryRoots.cs`, `docs/csharp/sindri-profile.md` (`context.sources.library`) |
| MCP surface | **Existing.** Seven `sindri_*` tools, one write (`sindri_request`). `McpSurfaceTests` (`tests/Aw.Architecture.Tests`) fails if the MCP namespace references any command but `FileRequestRequest`, a challenge answer or the launch secret | `docs/csharp/sindri.md` "MCP tools (API P8)" |
| Settings → Connections | **In review** (ui, #2245): the group and its Linear card. This spec adds a second card to it | Linear hub spec §3 |

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **Todos are their own store**, not items: ledger events (§4.1) folded into one `views.db` table. No repo, no queue, no recipe | An item is agent work tied to a repo (§0) |
| D2 | **Agent sessions have the same access as the owner, reads and writes** (owner, 2026-11-07, §12 Q6). The todo routes are desk class with **no** owner check, and two MCP tools serve them (`sindri_todos`, `sindri_todo`, §4.4). `McpSurfaceTests`' allow-list grows by the todo commands. This is the opposite of the Linear-write rule (Linear hub D3, `SND-AUTH-002` for sessions) on purpose: a todo list can't change anything outside Sindri, so a session adding "re-run the flaky test tomorrow" is help, not risk. Every event records who wrote it (`by`), the panel tags a row an agent added or last changed ("agent"), and todo text in MCP results is fenced as untrusted. **One exception:** picking a side in a both-places conflict (D11) is the owner's alone (`SND-AUTH-002`), because the owner asked to be asked | §12 Q6 |
| D3 | **A todo is one line**: text (1 to 280 characters, one line, passed through `Aw.Redaction` before the event is written), an optional due **date** (no time), and 0 to 10 links (D10). No notes, no subtasks, no tags, no priority | Notes are Obsidian's (owner, 2026-11-07). Fewer fields means adding one takes one line |
| D4 | **"From elsewhere" is read-only.** The panel shows PRs waiting on the owner's review and Linear issues assigned to the owner, read from the existing routes the app already calls. Needs you isn't repeated: it's the list right under the panel, and the panel's From elsewhere line says "3 need you, below". Nothing is copied into the todo store. A row opens its own page, and it leaves the list when it's finished there | One meaning of "done" per thing: checking off a typed todo never closes a Linear issue or answers a decision by accident. §12 Q1 |
| D5 | **Two-way sync with one Obsidian note** (owner, §12 Q2). The owner names one note in one library vault. Sindri writes every todo into it as a Tasks-style `- [ ]` line carrying the todo's id as an Obsidian block id (`^td-…`), and reads back adds, ticks, edits and deletes made there. It's the only file Sindri ever writes in a vault; the library feed stays read-only and skips that note. Obsidian Sync needs nothing special: its edits land in the file on this laptop like any other edit (§4.5) | §12 Q2 |
| D6 | **A panel at the top of Home, no new tab** (owner, §12 Q3). `G T` opens Home with focus in the panel. `N` opens the quick-add box from any page. `J`/`K`/`X`/Enter/Delete/`D`/`O` work inside the panel. The panel and the Needs you list are **two focus regions**: a key acts only on the region that has focus, so `X` in the panel checks a todo off and can never reach Needs you's Reject (§3.3) | §12 Q3. Home already uses `X` for Reject |
| D7 | **Due dates are local calendar days.** Overdue = before today in the app's time zone. The quick-add box reads a trailing due word ("today", "tomorrow", a weekday, "mon".."sun", "11-10", "2026-11-10") and shows the date it read before Enter. Anything else stays in the text. Groups: Overdue, Today, Upcoming, Someday. The tray tooltip shows the overdue count. No pop-ups | §12 Q4 |
| D8 | **Done is soft.** `todo.done` sets `doneAt`. Undo is `todo.reopened`. A done todo shows under "Done today" until local midnight, then only on `sindri todos --done`. `Delete` is `todo.removed` (Undo re-adds the same id with `todo.restored`). In the note, a done todo stays as `- [x]` for 7 days, then Sindri drops the line | Undo must work and must not lose the id or the links. The note doesn't grow forever |
| D9 | **Stale writes are refused.** Each write carries the todo's version, and a stale one is `SND-TODO-409` (the `SND-PROJECT-409` rule). The app reloads the row and keeps the typed edit in the box | Two windows, the CLI, an agent and the note can all edit the same todo |
| D10 | **Many links** (owner, §12 Q5). A todo links to 0 to 10 Sindri things: items, PRs, Linear issues, workflows. Each is a chip on the row | §12 Q5 |
| D11 | **Edited in both places: ask, every time** (owner, §12 Q2b). When a sync finds the same todo changed in Sindri and in the note since the last sync, and the two differ, Sindri records a conflict, keeps its own version in the app, leaves the note's line as Obsidian has it, and asks the owner in the panel with both versions. Nothing about that todo syncs until the owner picks. Different todos changed on each side merge without asking | §12 Q2b: no silent winner |
| D12 | **The panel's first render comes first** (owner, §12 Q7). On Home, `GET /sindri/todos` is the first read the page issues, before Needs you's. It reads one `views.db` table with no join. Budget: the panel's rows are on screen within 150 ms of Home's first frame while Sindri is running (TP-7) | §12 Q7: seeing what's next is what must feel instant |

## 2. Goals and non-goals

**Problem.** The owner's plate is split: Sindri shows what agents, PRs and Linear need from them, but their own todos live in Obsidian, so no one screen answers "what do I do next?"

**Who and when.** The owner, at the start of the day and between reviews, with Sindri already open on the work laptop; on their phone, in Obsidian, away from the laptop. Agent sessions, when a run turns up something the owner has to do later.

**Ideal.** Opening Sindri shows the next thing at the top with no wait. Adding a todo takes under two seconds from any page without losing your place. A todo ticked on the phone in Obsidian is ticked in Sindri the next time the laptop syncs. Nothing pulled in from elsewhere can be closed by a stray keypress, and nothing typed in either place is silently thrown away.

**Requirements** (EARS):

- REQ-U1 The app shall show, at the top of Home, the owner's next five open todos (Overdue, then Today, then Upcoming, then Someday; each by due date, then by when it was added) and a count of the rest.
- REQ-U2 The panel shall show a read-only From elsewhere line with the PRs waiting on the owner's review, the Linear issues assigned to the owner, and a count of the Needs you rows below it.
- REQ-U3 Sindri shall store todos so they survive a restart and a `views rebuild`, and the CLI, the MCP tools and the app shall show the same list.
- REQ-U4 Where an Obsidian todo note is set, Sindri shall keep every todo as one line of that note, and the note shall show the same open todos as the app within 10 seconds of a change on either side while Sindri is running.
- REQ-E1 When the owner presses `N` on any page, the app shall open the quick-add box with focus in it. When they press Enter on non-empty text, it shall save the todo and close the box, keeping focus where it was before.
- REQ-E2 When the owner checks a todo off or deletes it, the app shall show Undo for 5 seconds, and Undo shall bring it back with the same text, date and links.
- REQ-E3 When local midnight passes while the app is open, the groups and the overdue count shall move without a restart.
- REQ-E4 When the note gains a `- [ ]` line with no `^td-` id, Sindri shall add it as a todo and write the id onto that line.
- REQ-E5 When a sync finds one todo changed in both places since the last sync, Sindri shall show both versions in the panel and change neither until the owner picks one.
- REQ-S1 While Sindri isn't running, the panel shall show the existing Not running card, and `N` shall say "Sindri isn't running" instead of opening a box it can't save.
- REQ-S2 While a link points at something that no longer exists, its chip shall say so ("PR #12 not found") and the todo shall stay.
- REQ-S3 While the note can't be read or written (vault gone, permission), todos shall keep working in Sindri, the panel shall say "Obsidian note: <reason>", and `sindri doctor`'s `todo` row shall warn.
- REQ-O1 Where a todo has links, the row shall show one chip per link, each opening the linked page.
- REQ-O2 Where a todo has a due date, the row shall show it as a relative word for the next 6 days ("Fri") and as a date after that.
- REQ-O3 Where an agent session added or last changed a todo, the row shall say so ("agent").
- REQ-W1 If a save fails (stale version, Sindri unreachable), the app shall keep the typed text in the box and say why.
- REQ-W2 If the text holds a secret-shaped string, from any writer including the note, Sindri shall scrub it before it's stored.
- REQ-W3 If an agent session tries to pick a side in a both-places conflict, Sindri shall refuse it with `SND-AUTH-002` and change nothing.

**Non-goals.** Notes. Syncing more than one Obsidian note. Reminders that pop up. Recurring todos. Sharing todos with anyone. Moving items, issues or decisions into the todo store. Drag-to-reorder in the app. A Todo tab.

**Later.** Recurring todos ("every Mon"). Manual order in the app. Indented sub-tasks in the note. Showing the last list from a local copy before Sindri answers (only if TP-7 shows the cold start hurts).

**Success measure.** Two weeks after Ph4 ships, `sindri todos --stats --since 14d` (new) reports todos added and done, by writer (app, CLI, agent, Obsidian). Target: at least 5 added a week, and at least half the ones due in the window done by their date. The owner says in one line whether they still keep a todo list anywhere but the synced note.

## 3. Where it sits in the app

| Surface | Reads | Writes | Reached from |
|---|---|---|---|
| **Todo panel** (new, top of Home) | `GET /sindri/todos` (new), plus `reviews`, `linear/issues?group=assigned` (existing), and Needs you's own count | todo writes; conflict picks | Home; `G T` (focus in the panel); ⌘K "Todo" |
| **Quick-add box** (new, an overlay at the top of the window) | — | `POST /sindri/todos` | `N` on any page except inside a text box; ⌘K "New todo" |
| **Settings → Connections → Obsidian todo note** (new card beside #2245's Linear card) | the vault list, the note's sync state | the profile key `todo.obsidian` (§4.5), approved like the Linear card's own change | Settings; the panel's "Obsidian note: …" line |
| **Tray** (existing) | the tooltip gains "1 overdue todo" when there is one | none | — |

### 3.1 The panel

Full width of Home's column, above the Needs you list. Header: "Todo", the count line ("4 open · 1 overdue"), the hint "N New todo", and, when a note is set, a quiet sync line ("Synced with Obsidian 10:05", or "Obsidian note: can't write, vault folder missing" in the warning colour). Then up to five rows, overdue first, and "Show all 11" which expands the panel in place to the full grouped list (Overdue, Today, Upcoming, Someday, Done today collapsed). Esc or "Show less" folds it back.

A row is a checkbox, the text, the link chips, an "agent" tag when D2 applies, and the due word on the right. After the rows, one From elsewhere line: "2 PRs waiting on your review · 1 Linear issue assigned · 3 need you, below". Each part opens its page (Reviews, Work → Linear) or moves focus to the Needs you list. Expanded, From elsewhere lists up to 5 rows of each source, with "→" to say they open somewhere else.

**A both-places conflict** is a row of its own at the top of the panel, above Overdue, in the attention colour: "Changed in Sindri and in Obsidian", then two columns, **Sindri** and **Obsidian**, each with that version's text, due word and done state, and a button under each, **Keep this one**. A removal on one side reads "Removed in Obsidian" with **Remove it** / **Keep it**. The panel's count line adds "1 to sort out".

### 3.2 The quick-add box

A 560 px box under the top bar, over the page, which stays visible behind it. One text field with the placeholder "New todo… (add a day: fri, tomorrow, 11-10)". As you type, a line under it shows what will be saved: "Due Fri 11-10" or "No date". Links are added by pasting PR urls, Linear keys (`ENG-12`), item ids (`ITM-…`) or workflow addresses anywhere in the text: the line shows "Links PR #12, ENG-12" and the refs leave the text. Enter saves. Esc closes, keeping the draft until the app restarts. Shift+Enter saves and keeps the box open for the next one.

### 3.3 Keys

| Where | Key | Does |
|---|---|---|
| Anywhere (not in a text field) | `G T` | Opens Home with focus on the panel's first row |
| Anywhere (not in a text field) | `N` | Opens the quick-add box |
| Home | Tab / Shift+Tab | Moves focus between the panel and the Needs you list. The region with focus draws its heading in the accent colour |
| Panel | `J` / `K`, Up / Down | Move between the panel's rows. They stop at the panel's last row; they never fall into Needs you |
| Panel, todo row | `X` or Space | Check off / reopen |
| Panel, todo row | `Enter` | Edit in place (text, then Tab to date, Tab to links). Enter saves, Esc cancels |
| Panel, todo row | `Delete` / Backspace | Remove, with Undo |
| Panel, todo row | `D` | Opens the due picker on the row: today, tomorrow, next Mon, a date, none |
| Panel, row with links | `O` | Opens the first link; `Shift+O` lists them all |
| Panel, conflict row | `1` / `2` | Keep Sindri's / keep Obsidian's (the button names say which) |
| The Undo toast | `⌘Z` | Undo, while the toast shows |

`X` means Reject in Needs you and check off in the panel. The two-region rule (D6) is what keeps them apart, so Ph0's mockups draw the focused region clearly and Ph2 has a test that `X` in the panel never calls Needs you's reject. Ph0 also checks that `N` isn't taken by any page's own single keys; where one clashes, that page's key wins and the palette's "New todo" stays.

### 3.4 States

| Surface | Loading | Empty | Error / slow | Success |
|---|---|---|---|---|
| Panel | three skeleton rows, first load only | no todos: "Nothing on your list. Press N to add one." From elsewhere with nothing waiting: hidden | Sindri not running: the existing Not running card in the panel's place. One From elsewhere source failing: that part says "Reviews didn't load" with Retry, the rest stay. Note unreadable: the sync line in the warning colour with the reason, todos unaffected | the rows |
| Quick-add | — | Enter on empty text does nothing | save failed: the box stays open with the text and the reason (§6's text, or "Sindri isn't answering" when the call failed), Enter retries | the box closes, toast "Added: due Fri" with **Show** (Home, panel expanded at the row) |
| Row edit | "Saving…" after 300 ms | — | `SND-TODO-409`: "Changed somewhere else. Here's the latest", the row reloads, the edit stays in the field. `SND-TODO-423`: "Changed in both places. Pick a version first", focus moves to the conflict row | the row shows the new values, focus stays on it |
| Check off / delete | instant, rolled back on failure | — | failure: the row comes back with "Couldn't save: <reason>" | toast with Undo |
| Conflict row | — | — | the pick fails: the row stays with the reason | the row goes, the todo shows the kept version, the note line is rewritten on the next sync |
| Settings card | "Checking the note…" in a live region | no vaults in the profile: "Add an Obsidian vault in Settings → Library first" with a button there | vault missing, note path outside it, note not writable: inline, plain reason first, key path last | "Syncing with Vault/Todo.md · last synced 10:05" with **Change** and **Stop syncing** (asks; the note stays as it is) |

### 3.5 Accessibility

- The panel is a labelled region ("Todo") with heading-led groups when expanded. Each row reads "Todo, reply to Sam about the offsite, due Friday, not done", plus "2 links" and "added by an agent" when they apply.
- The checkbox is a real `CheckBox` with that name. `X` and Space toggle it.
- The quick-add box is a dialog with a labelled text field. The parsed-date line is a polite live region. Esc returns focus to where it was.
- The Undo toast is a polite live region. `⌘Z` works while it shows. It doesn't take focus.
- A new conflict is announced once ("A todo changed in Sindri and in Obsidian"); its two buttons are named "Keep Sindri's version: <text>" and "Keep Obsidian's version: <text>".
- Due words carry text, never colour only ("Overdue · Mon").
- From elsewhere parts read as links: "Opens Reviews: 2 PRs waiting on your review".

## 4. Data

### 4.1 Events (new, ledger; not Public) (api lane)

| Event | Payload | When |
|---|---|---|
| `todo.added` | `id` (`td_<ulid>`), `text`, `due` (date or null), `links` (§4.3, 0 to 10), `clientId` (for retry), `by` | Add |
| `todo.changed` | `id`, `version`, the changed fields (`text`, `due`, `links`; null clears `due`, `[]` clears `links`), `by` | Edit |
| `todo.done` / `todo.reopened` | `id`, `version`, `by` | Check off / Undo or reopen |
| `todo.removed` / `todo.restored` | `id`, `version`, `by` | Delete / Undo |
| `todo.conflicted` | `id`, `sindri` and `obsidian` (each the todo's text, due, links, done, or `removed: true`), `noteHash` | A sync finds a both-places change (D11) |
| `todo.conflict.resolved` | `id`, `kept` (`sindri` or `obsidian`), `by` (always the owner) | The owner picks. The kept version, when it's Obsidian's, follows as a normal `todo.changed`/`done`/`removed` with `by: obsidian` |
| `todo.note.synced` | `vault`, `note`, `hash` (sha256 of the note as Sindri last read or wrote it), `read` and `wrote` counts | Each sync that changed something |

`by` is `owner`, `session:<id>` (an agent, through MCP or a launched CLI) or `obsidian` (the note). `text` passes through `Aw.Redaction` before the event is written, whoever wrote it. Idempotency key for `todo.added`: `todo.added:<clientId>`, so a retried Enter after a lost answer makes one todo; a todo adopted from the note uses `note:<sha256 of the line>` as its client id, so a sync that dies before writing the id back doesn't add it twice.

### 4.2 Projection

`TodosProjection : IProjection` (new, `Aw.Sindri.Core/Todos/`) folds the events into `views.db` table `todo` (`id`, `text`, `due`, `links` (JSON), `added_at`, `done_at`, `removed`, `version`, `last_by`, `conflict` (JSON or null)), migration `00NN_todos` (next number), with an index on (`removed`, `done_at`, `due`) for the panel's read. It's a pure fold, rebuilt by `sindri views rebuild`, and invalidates `ApiViews.Todos` (new).

### 4.3 Links

A link is one of: an item (`ITM-…`), a PR (`<owner>/<repo>#<n>`, or its GitHub url), a Linear issue key (`ENG-12`), or a workflow address. It's stored as the deep link the app already opens (`sindri://item/…`, `sindri://reviews/<owner>/<repo>/<n>`, `sindri://linear/ENG-12`, `sindri://workflow/…`). The server checks only the shape (`SND-TODO-001`), never whether the thing exists, so a link to something not synced yet is allowed. The chip resolves it when drawn (REQ-S2). Duplicates are dropped; an 11th is `SND-TODO-001`.

### 4.4 Routes, the CLI and MCP (new) (api lane)

| CLI | Route, class | Prints | Exit |
|---|---|---|---|
| `sindri todos [--done] [--all] [--stats --since <N>d] [--json]` | `GET /sindri/todos?done=&all=`, desk | one line per todo: `<id>  <due or ->  <text>  [<links>]`; a conflicted one ends `(changed in both places)` | 0; 1 not running; 2 error |
| `sindri todo add <text> [--due <day>] [--link <ref>]... [--json]` | `POST /sindri/todos`, desk | `td_… added (due Fri 11-10)` | 0; 1; 2 (`SND-TODO-001`) |
| `sindri todo edit <id> [--text T] [--due D \| --no-due] [--link L]... [--no-links] [--version N]` | `POST /sindri/todos/{id}/change`, desk | `td_… changed` | 0; 1; 2 (`-001`, `-404`, `-409`, `-423`) |
| `sindri todo done\|reopen\|remove\|restore <id> [--version N]` | `POST /sindri/todos/{id}/{verb}`, desk | `td_… done` | 0; 1; 2 (`-404`, `-409`, `-423`) |
| `sindri todo keep <id> sindri\|obsidian` | `POST /sindri/todos/{id}/keep`, desk **with an owner check** (D2's exception) | `td_… kept Obsidian's version` | 0; 1; 2 (`-404`, no conflict `-409`, `SND-AUTH-002`) |
| `sindri todo note [--json]` | `GET /sindri/todos/note`, desk | the note's vault and path, last sync, open conflicts, last error | 0; 1; 2 |

Without `--version`, the CLI reads the todo first and sends its version, as `item close` does. `--due` takes the same words as the quick-add box (D7). Each write is a row in `Host/Catalog/TodoRows.cs` (new) with its label in `Labels.Actions.cs`, and `Golden/Api/commands/catalog.json` gains them.

**MCP** (two new tools, after `sindri_request` in `tools/list`):

| Tool | Arguments | Same as | Kind |
|---|---|---|---|
| `sindri_todos` | `done?` (bool), `all?` (bool) | `GET /sindri/todos` | query, `readOnlyHint` |
| `sindri_todo` | `action` (`add`, `change`, `done`, `reopen`, `remove`, `restore`), `id?`, `text?`, `due?`, `links?`, `version?` | the matching `POST` | write, `readOnlyHint: false`, `destructiveHint: false` (remove is soft and undoable) |

Fencing: `todos[].text` and `sindri_todo`'s echoed `text`, source `sindri:todo`, author the row's `last_by`. There is no `keep` tool (D2). `McpSurfaceTests`' allow-list gains the six todo write commands and nothing else. `sindri_todo` waits for the views like `sindri_request` does (`SindriTools.WaitForViews`).

### 4.5 The Obsidian note (context lane)

**Setting it.** A new profile key, `todo.obsidian` (`{ vault, note }`): `vault` is the `name` of one of `context.sources.library.vaults`; `note` is a vault-relative path ending `.md`, no leading `/`, no `..`, not inside a dot folder and not matched by that vault's `deny` globs. A bad value is `SND-TODO-002` with the key path. The Settings card writes it and approves just that change, with the Linear card's rule (Linear hub §5.1: approved in the same click only when the pending diff is exactly this key; otherwise "Your profile has other changes waiting. Review those first"). With no key set, nothing below runs.

**The note's format** ([Obsidian Tasks](https://publish.obsidian.md/tasks/) style, so the note also works with that plugin, though it doesn't need it):

```markdown
# Todo

- [ ] reply to Sam about the offsite 📅 2026-11-14 ^td-01jc5a8x
- [ ] rotate the staging key 📅 2026-11-10 [PR #12](sindri://reviews/jthorare/agentic-workflow/12) [ENG-12](sindri://linear/ENG-12) ^td-01jc5b2k
- [ ] call the dentist ^td-01jc5c90
- [x] renew the cert ✅ 2026-11-07 ^td-01jc5917
```

- One top-level `- [ ]` / `- [x]` line per todo. Due is `📅 YYYY-MM-DD`; done is `[x]` plus `✅ YYYY-MM-DD`; each link is a markdown link to its `sindri://` deep link; the id is the trailing Obsidian block id `^td-<ulid, lowercase>` (Obsidian keeps block ids through edits, and they're invisible in reading view).
- Everything else in the note (headings, prose, blank lines, indented lines, lines that aren't tasks) is kept exactly as it is. Sindri only touches task lines that carry a `^td-` id, plus new ones it adopts.
- **Order is the owner's.** Sindri never reorders lines. A todo added in Sindri goes after the last todo line (or at the end of the note when it has none). A done line stays as `[x]` for 7 days (D8), then Sindri removes it.

**Reading.** A Sindri service, `todo-note` (new, in the `services` list), watches the note's folder (a file watcher, plus a check every 60 s and at serve start, since watchers miss events on some file systems). When the note's sha256 differs from the last `todo.note.synced` hash, it waits until the file has been unchanged for 2 s (Obsidian saves as you type), parses it, and runs the three-way merge below. **Obsidian Sync edits arrive the same way.** Obsidian Sync writes the synced file into the vault folder on this laptop when Obsidian is open here; Sindri only ever sees the file. An edit made on the phone while the laptop was shut lands when Obsidian next syncs here, and the merge treats it like a local edit made at that moment. Obsidian Sync's own handling of two devices editing one file happens before Sindri sees the result.

**The three-way merge**, per todo id, between the **base** (the note as Sindri last synced it, kept at `$AW_SINDRI_HOME/todo/note-base.md`, its hash in the last `todo.note.synced`), the **note** now, and **Sindri** now (the projection):

| Note vs base | Sindri vs base | Result |
|---|---|---|
| same | same | nothing |
| changed (text, due, links, ticked, removed) | same | the matching event, `by: obsidian` |
| same | changed | the note's line is rewritten from Sindri |
| changed | changed, to the same values | nothing (both already agree) |
| changed | changed, differently | `todo.conflicted` (D11): Sindri keeps its version, the note's line is left as is, neither side syncs that todo until `keep` |
| new line, no `^td-` | — | `todo.added` `by: obsidian`, and the id is written onto the line |
| line has a `^td-` id Sindri doesn't know | — | adopted as a new todo with that id (a note copied from another box) |

A pick (`keep sindri`) rewrites the note's line on the next sync; `keep obsidian` applies the note's version as events. Then the base moves.

**Writing.** After any todo event not `by: obsidian`, and after a merge, the service re-reads the note, checks its hash against the one it just merged (if it moved, it merges again first, at most 3 times, then waits for the next change), edits only the lines that need it, writes the result to `.<note name>.sindri-tmp` beside the note (a dot file, which Obsidian doesn't show or sync), and renames it over the note. Then it saves the base and records `todo.note.synced`. The note is never written from a stale read, so an edit typed in Obsidian a moment earlier is merged, not overwritten.

**Failures.** The vault folder gone, the note unreadable, a rename refused: no event, the `todo` doctor row warns with the reason, the panel's sync line says it, and todos keep working in Sindri. The next change or the 60 s check tries again. A note that fails to parse at a line keeps that line as text (it's not a task Sindri knows) and the doctor row names the line number.

**Stopping.** Removing `todo.obsidian` (Settings' **Stop syncing**) stops the service and leaves the note exactly as it was.

## 5. The panel (ui lane)

`TodoPanelViewModel` (new, `ViewModels/Todo/`) reads `GET /sindri/todos` on connect and on `ApiViews.Todos` invalidation, first among Home's reads (D12), and works out the groups from `due` and the app's clock (`IClock`), regrouping at local midnight (REQ-E3). From elsewhere reuses the view models that already read those routes (the Reviews badge's list, Work → Linear's assigned group) and Needs you's row count. `QuickAddViewModel` (new) owns the box, the due-word parser (D7, a pure function in `Aw.Sindri.Ui/Todo/DueWords.cs` with one test per word) and the draft. Home's view gets two focus regions (D6). `SecondKey` gains `"T" => (PageId.Home, HomeFilter.All)` plus a focus-the-panel flag. `ObsidianNoteCardViewModel` (new, `ViewModels/Settings/`) is the Connections card.

## 6. Errors (new)

| Code | HTTP | When | Text |
|---|---|---|---|
| `SND-TODO-001` | 400 | empty or over-280-character text, a line break, a bad date, a link of no known shape, more than 10 links | "A todo is one line of 1 to 280 characters." / "No date 'frday'. Try fri, tomorrow or 11-10." / "That link isn't an item, PR, Linear key or workflow." / "A todo takes at most 10 links." |
| `SND-TODO-002` | 400 | a bad `todo.obsidian` key | "The todo note must be a .md file inside one of your library vaults. Pick one in Settings → Connections → Obsidian todo note (todo.obsidian)." |
| `SND-TODO-404` | 404 | no such todo | "No todo td_01J…." |
| `SND-TODO-409` | 409 | stale version; `keep` on a todo with no conflict | "That todo changed somewhere else. Reload and try again." / "That todo has nothing to sort out." |
| `SND-TODO-423` | 423 | a write to a todo with an open conflict | "That todo changed in Sindri and in Obsidian. Pick which version to keep first." |
| `SND-AUTH-002` (existing) | 403 | an agent session's `keep` | the existing text |

Each is a row in `SindriErrors.cs` and `docs/csharp/sindri.md`'s error table. The doctor row `todo` (new): `note: syncing | not set | can't read | can't write`, `last sync`, `conflicts: N` (warns when one is older than a day).

## 7. Security

- **Agents have the owner's access to todos** (D2). The worst a mistaken session can do is add, edit or soft-delete todos, every one undoable and every one tagged with who did it. It can't sort out a conflict. Todo text in MCP results is fenced, so a todo that says "ignore your instructions" is data to the next session that reads it.
- **Text is scrubbed** (REQ-W2) before the event is written, whoever wrote it, the note included, so a token pasted into the note never reaches the ledger. The note itself still holds what the owner typed there; Sindri doesn't rewrite the owner's text in the note except when the scrubbed version is the one Sindri then writes back for that line.
- **One file in one vault.** The note service writes only the configured note and its dot-file twin, never follows a symlink (the library rule), refuses a note path matched by the vault's `deny` globs, and checks the real path stays inside the vault before every write.
- **Links are shape-checked** and only ever open Sindri's own pages or an https PR url on a configured forge host. A `sindri://` link typed in the note is checked the same way; anything else in a todo line stays as text.

## 8. Docs to update

`docs/csharp/sindri.md` (CLI, routes, errors, events, the MCP table and its fencing table, the `todo-note` service, the doctor row), the events catalog (`EventCatalog` rows, not Public), `docs/csharp/sindri-profile.md` (`todo.obsidian`), `docs/csharp/sindri-ui.md` (the Home panel and its two focus regions, `G T`, `N`, the quick-add box, the conflict row, the Obsidian card, the tray tooltip, the palette rows "Todo" and "New todo"), `docs/sindri/library.md` or its equivalent (the library feed skips the todo note), `planning/ERD.md` (`todo`), AGENTS.md Commands (`sindri todos`, `sindri todo …`), `src/Sindri/aot-smoke/expected.txt` (the doctor row). No new environment variables, no `deps.yaml` edge.

## 9. Proof

| Row | Check |
|---|---|
| TP-1 | On the fixture: `N` on Reviews, type "check flaky test fri", Enter: the box closes, focus is back on the Reviews row it left, Home's panel shows the row due Fri, and `sindri todos` prints it. REQ-E1, REQ-U3 |
| TP-2 | `X` on a todo, then Undo: same id, text, date and links. `Delete`, then Undo: same. `views rebuild`: the list is unchanged. REQ-E2, REQ-U3 |
| TP-3 | The fake clock passes midnight with the app open: Today's row moves to Overdue, the count and the tray tooltip change. REQ-E3 |
| TP-4 | An agent session through MCP: `sindri_todo add`, then `sindri_todos` lists it with fenced text, the panel shows it tagged "agent"; the session's `keep` is `SND-AUTH-002`. D2, REQ-O3, REQ-W3 |
| TP-5 | Edit a todo in the CLI while the app has it open in edit: the app's save gets `SND-TODO-409`, the row reloads, the typed edit stays. REQ-W1 |
| TP-6 | From elsewhere shows the fixture's waiting PR, its assigned Linear issue and the Needs you count; each opens its page or moves focus; Linear failing shows "Linear didn't load" while the rest stay. REQ-U2 |
| TP-7 | With Sindri running and 200 todos in the store, Home's first frame to the panel's rows on screen: under 150 ms at p95 over 20 opens. D12 |
| TP-8 | Focus in the panel, `X` on the last row, `J` past the last row: a todo is checked off, focus stays in the panel, Needs you's reject is never called. D6 |
| TP-9 | Note sync on a temp vault: add in Sindri → the line appears with its `^td-` id within 10 s; tick the line in the file → done in Sindri; add a bare `- [ ] buy milk` → a todo, and the id is written onto the line; delete a line → removed, with Undo in the app; a heading and a paragraph in the note are untouched byte for byte. REQ-U4, REQ-E4 |
| TP-10 | Both places: change a todo's text in Sindri and the same line's text in the file before a sync: the conflict row shows both, the note's line is left as typed, the todo refuses an edit with `SND-TODO-423`, `keep obsidian` applies the file's text and the next sync leaves the file alone. Same with a removal on one side and an edit on the other. REQ-E5, D11 |
| TP-11 | The write race: a file edit lands between the service's read and its rename (the test holds the rename): the service merges again, and the edit is in the final file. §4.5 "Writing" |
| TP-12 | The vault folder renamed away: todos keep working, the panel's sync line and the doctor row say "can't read", and putting it back resumes on the next 60 s check. REQ-S3 |
| TP-13 | On the owner's laptop for a week, with Obsidian Sync on and the phone in use: todos added and ticked on the phone show in Sindri, and the owner answers §2's success line. |

## 10. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Depends on | Proof |
|---|---|---|---|---|
| **Ph0** | ui | Mockups (this spec's `mockups/`, redrawn for rev 2): Home with the panel collapsed and expanded, the two focus regions, the conflict row (an edit on both sides, and a removal), a row with three link chips and the "agent" tag, the quick-add box with a parsed date and two links, the 409 and 423 rows, the Undo toast, empty and Not running, the Settings → Connections card in each state; 1,000 and 1,440 px. The `N` key-clash check (§3.3) | this spec | the owner's pick |
| **Ph1** | api | Events, `TodosProjection` and its migration, `ApiViews.Todos`, the routes, the CLI, the two MCP tools and their fencing, the `keep` owner check, `McpSurfaceTests`' allow-list, catalog rows, errors, API goldens, `--stats` | this spec | unit: fold per event, due-word parsing on the CLI, redaction, `keep` refused to a session, `-423`; integration: add → list → done → reopen → remove → restore over HTTP and over MCP, a retried add with the same `clientId` makes one, `-409`, `views rebuild` gives the same table; ApiGoldens; TP-4's server half |
| **Ph2** | ui | The Home panel, the quick-add box, the keys and focus regions, the tray tooltip, the palette rows | Ph0's pick, Ph1's goldens (hand-written goldens first, so it can start the day Ph1 does) | UI tests: TP-1, TP-2, TP-3, TP-5, TP-8 in headless; one per §3.5 line; Frames tests for each §3.4 panel state; TP-7 timing on the fixture |
| **Ph3** | context | `todo.obsidian` and `SND-TODO-002`, the note parser and renderer (pure, `Aw.Sindri.Core/Todos/Note/`, golden files per §4.5 line shape), the three-way merge (pure, one test per table row), the `todo-note` service (watcher, 60 s check, debounce, re-read-before-rename, dot-file rename), `todo.conflicted` / `todo.conflict.resolved` / `todo.note.synced`, the library feed skipping the note, the doctor row and `aot-smoke/expected.txt` | Ph1's events | TP-9, TP-10, TP-11, TP-12 on temp vaults (integration lane) |
| **Ph4** | ui | From elsewhere, the conflict row, the sync line, the Settings → Connections → Obsidian todo note card | Ph2, Ph3's goldens (hand-written first) | TP-6, TP-10's app half, one Frames test per card state |
| **Ph5** | — | Owner proof | Ph4 | TP-13 |

**Parallel plan.** Day one: ui on Ph0's mockups, api on Ph1 red-first, context on Ph3's pure parts (parser, renderer, merge) red-first against the event shapes in §4.1. When the owner picks, ui starts Ph2 against hand-written goldens of `GET /sindri/todos` (one file, written by api as Ph1's first commit) and swaps in the real ones when Ph1 merges. Context's service half starts when Ph1's events merge. Ph4 starts when Ph2's panel shell has merged, against hand-written goldens of `GET /sindri/todos/note` and the conflict shape (written by context as Ph3's first commit).

**Shared files, one owner each.** `Usage.cs`, `PublicAPI.Unshipped.txt`, API goldens, `catalog.json`, the MCP table in `docs/csharp/sindri.md`: Ph1. `docs/csharp/sindri-profile.md`, the doctor row, `aot-smoke/expected.txt`: Ph3. `docs/csharp/sindri-ui.md`, the chord table, Home's view: Ph2 (Ph4 appends).

**Gates per phase.** `-warnaserror` build; touched projects' unit and integration lanes; `Aw.Architecture.Tests` (new `Todos` folders in Core, Features and Ui; Ph1 changes `McpSurfaceTests`); scoped format; `check-env-example.sh`; ApiGoldens classes (integration lane) and the catalog golden (Ph1); `Aw.Sindri.Ui.Tests` unit + integration and `Aw.Sindri.Ui.Frames.Tests` integration (Ph2, Ph4, since the catalog gains rows and Home a view); the full `Aw.Sindri.Tests` unit lane (Ph1 changes `Usage.cs`); aot-smoke (Ph3, the doctor row).

## 11. Open risks

- **`X` in two meanings on one page.** Needs you's `X` rejects; the panel's checks off. The focus-region rule and TP-8 stop the key from crossing over, but the owner's hands may expect one meaning. Meta's UX pass and Ph0's mockups decide whether the panel needs a different key, with Space as the fallback.
- **Writing a file Obsidian has open.** Obsidian reloads a note changed on disk, and the re-read-before-rename rule (§4.5) closes most of the window, but a keystroke landing in the few milliseconds between Sindri's last read and its rename can be lost from the editor's buffer. TP-11 covers the file side; Ph3 tries it by hand with Obsidian open and reports.
- **Emoji in the note.** `📅` and `✅` are the Tasks plugin's markers. If the owner doesn't use that plugin and finds them noisy, the fallback is Dataview-style `[due:: 2026-11-14]`. The parser can read both from the start; the writer uses one (UX pass).
- **`N` everywhere.** A page may already use `N` (Ph0 checks). The palette's "New todo" always works.
- **Cold start.** D12's budget holds while Sindri is running. When the app opens before serve is up, the panel shows the Not running card like every other page. If that's the usual first sight in the morning, the Later item (draw the last list from a local copy) comes forward.

## 12. Owner questions (all answered, owner 2026-11-07, through meta)

1. **Where do todos come from?** **Decided:** typed by you, plus the read-only From elsewhere section (Needs you, PRs waiting on your review, Linear issues assigned to you). My recommendation. D4 (Needs you shows as a count pointing to the list right below the panel, not repeated rows).
2. **Obsidian?** **Decided:** two-way sync with one note (not my recommendation, which was none in v1). D5, §4.5.
   2b. **Edited in both places before a sync?** **Decided:** ask the owner each time, showing both; no silent winner. D11.
3. **Where in the app, and the keys?** **Decided:** a panel at the top of Home, no new tab (not my recommendation, which was a tenth tab); `N` adds from anywhere; `J`/`K`/`X` inside the panel. D6, §3.3.
4. **Due dates?** **Decided:** optional, grouped Overdue / Today / Upcoming / Someday, the tray shows the overdue count, no pop-ups. My recommendation. D7.
5. **Links?** **Decided:** a todo can link to many Sindri things (items, PRs, Linear issues, workflows), not one (not my recommendation). D10.
6. **Agent sessions?** **Decided:** the same access as the owner, reads and writes, through an MCP tool, no `SND-AUTH-002` (not my recommendation, which was owner only). D2, with one exception this spec adds: picking a side in a conflict stays the owner's, because Q2b asks for the owner to be asked.
7. **Where do todos live today, and what must feel instant?** **Decided:** in Obsidian today; **seeing what's next** must feel instant, so the panel's first render is the priority over capture speed. §0, D12, TP-7.

## Revision log

- **Rev 2** (2026-11-07): the owner's answers to §12 folded in. Four differ from rev 1: two-way Obsidian sync with one note (new §4.5, D5, D11 ask-each-time conflicts, `todo-note` service, context lane Ph3), a panel at the top of Home instead of a tenth tab (D6, two focus regions because Home's `X` is Reject), many links (D10), agent sessions with the owner's access (D2, two MCP tools, `McpSurfaceTests` allow-list; `keep` stays owner-only). Q7: Obsidian today, seeing what's next first (D12, TP-7). Settings → Connections gains the Obsidian todo note card. Phases now Ph0 to Ph5. The rev 1 mockups (ten tabs) are superseded; Ph0 redraws them.
- **Rev 1.1** (2026-11-07): mockups added (`mockups/`, ten tabs fit at 1,000 px with 76 px spare at 100% text; at 130% every tab count hides at the last step-down); §3.4 save-failed reason and Not-running tab count, Home's line matches the tab count.
- **Rev 1** (2026-11-07): first draft on the recommendations above, before the owner's answers.
