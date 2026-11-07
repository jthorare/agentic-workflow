---
aw-doc: 1
id: sindri-todo
kind: spec
title: "Sindri's Todo page: the owner's own todos, plus what other pages say is on their plate"
status: draft
repos: [jthorare/agentic-workflow]
---

# Sindri's Todo page

**Status:** draft for the owner, revision 1 (my recommendations stand in for §12's answers until the owner picks) · **Date:** 2026-11-07 · **Lane:** ideas (this spec), with api (store, routes, CLI) and ui (the page) · **Builds on:** the Projects page spec `2026-10-29-sindri-projects-page-design.md` (§5's owner-state pattern: ledger events, a projection, desk-class commands with an owner check), the Linear hub spec `2026-11-04-sindri-linear-hub-design.md` (D9: every top-bar tab costs width at 1,000 px; the Work → Linear view), the PR review page spec `2026-11-04-sindri-pr-review-page-design.md` (the Reviews tab and its waiting count), `docs/csharp/sindri-ui.md` (top bar, G chord, sheets) · **Next:** the owner answers §12; three /autoplan rounds; meta's UX pass; the owner reads; then /specToProvenPR takes §10's phases.

Every file path below is one the author opened on main (bf83261). Every command, route, event, key and type is marked **existing** or **new**.

**Not in scope: notes.** The owner keeps notes in Obsidian (owner, 2026-11-07). A todo is one line, with an optional due date and an optional link. It has no body, no checklist, and no formatting.

## The owner's steps: install -> works

1. **Open Todo.** Click **Todo** in the top bar (right after Home), or press `G T`. Your todos are grouped Overdue · Today · Upcoming · Someday. Below them, **From elsewhere** lists what other pages say is waiting on you.
2. **Add one from anywhere.** Press `N` on any page. A one-line box opens at the top of the window. Type "reply to Sam about the offsite fri" and press Enter. The todo is saved with Friday as its due date, and the box closes. Esc closes it without saving.
3. **Check one off.** On the Todo page, `J`/`K` moves between rows and `X` checks the row off. A "Done. Undo" toast stays for 5 seconds. Done todos fold into "Done today" and are gone from the page the next day (`sindri todos --done` still lists them).
4. **Change one.** Press `Enter` on a row to edit its text, due date and link in place. `Enter` saves, Esc cancels. `Delete` removes the row, with Undo.
5. **Go to what it's about.** A todo linked to an item, a PR, a Linear issue or a workflow shows a chip. Pressing `O` (or clicking the chip) opens that thing's page.
6. **From the terminal.** `sindri todo add "rotate the staging key" --due 2026-11-10`, `sindri todos`, `sindri todo done td_…`.

## 0. Why

The owner runs their day from Sindri: PR reviews, workflows, agents, docs, Linear (owner goals, 2026-11-04: "personal mega app on the work laptop"). Sindri already tracks what's waiting on the owner (Needs you, PRs waiting on review, Linear issues assigned to them). It has no place for the owner's own todos, things like "reply to Sam" or "renew the cert", that have no repo and no run. Today those live somewhere else **(owner fills in, §12 Q7)**, so the owner's plate is split across apps.

Why not Sindri's items? An item (`sindri item add <title> [--repo]`, existing, `docs/csharp/sindri.md:42`) is a unit of agent work. It belongs to a repo of the approved profile (`SND-CLI-003` otherwise), it moves through the queue's groups (`waitingOnYou`, `running`, `queued`, ...), and recipes run on it. A personal todo has none of that. Making todos items would put "call the dentist" in the Work queue and need a fake repo. So todos get their own small store.

What exists today (main bf83261):

| Piece | State | Where |
|---|---|---|
| Owner-only writes | **Existing pattern.** `ProjectWrite.Begin` refuses an agent session with `SND-AUTH-002`, even though desk-class routes let sessions through | `src/Sindri/Aw.Sindri.Core/Projects/ProjectWrite.cs` |
| Owner state in the ledger | **Existing pattern.** `project.attached` / `changed` / `detached` events folded by `ProjectsProjection` into `views.db`, a stale version refused with `SND-PROJECT-409`, catalog rows in `Host/Catalog/ProjectRows.cs` | `src/Sindri/Aw.Sindri.Core/Projects/ProjectsProjection.cs`, Projects spec §5 |
| What's waiting on the owner | **Existing routes.** `GET /sindri/needs-you`, `GET /sindri/reviews` (the Reviews badge's waiting count), `GET /sindri/linear/issues` (Work → Linear, "Assigned to you") | `Host/Endpoints/GetNeedsYouEndpoint.cs`, `GetReviewsEndpoint.cs`, `ListLinearIssuesEndpoint.cs` |
| Top bar | **Existing.** Nine tabs: Home, Projects, Workflows, Work, Recipes, Context, Agents, System, Reviews. G chord second keys H/N, D, P, W, Q, R, C, A, ./S, V, J, U. `T` is free. Tabs draw short labels under 1,100 px | `ViewModels/Shell/ShellViewModel.Chords.cs:109`, `NavEntry.cs`, `Navigation/PageId.cs` |
| A "Today" page | **Planned, unrelated.** The UI spec's later "Today" view is "what happened today" (activity), not todos. This spec doesn't use the name | `2026-10-11-sindri-ui-design.md:141`, `PageId.cs:3` |

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **Todos are their own store**, not items: four ledger events (§4.1) folded into one `views.db` table. No repo, no queue, no recipe | An item is agent work tied to a repo (§0) |
| D2 | **Only the owner reads or writes todos.** Every route is desk class with an owner check (`TodoWrite.Begin`, the `ProjectWrite.Begin` pattern). An agent session gets `SND-AUTH-002`, reads included. No MCP tool. The events are not `Public` in `EventCatalog`, so the event stream never serves them | They're personal. §12 Q6 |
| D3 | **A todo is one line**: text (1 to 280 characters, one line, passed through `Aw.Redaction` before the event is written), an optional due **date** (no time), an optional link (§4.3). No notes, no subtasks, no tags, no priority | Notes are Obsidian's (owner, 2026-11-07). Fewer fields means adding one takes one line |
| D4 | **"From elsewhere" is read-only and lives in the app.** The page shows Needs you rows, PRs waiting on the owner's review, and Linear issues assigned to the owner, read from the three existing routes the app already calls. Nothing is copied into the todo store. A row opens its own page, and it leaves the list when it's finished there | One meaning of "done" per thing: checking off a typed todo never closes a Linear issue or answers a decision by accident. No new server join. §12 Q1 |
| D5 | **No Obsidian contact in v1.** Sindri never reads or writes a tasks note | Sindri's library feed reads vaults and never writes them (`SND-LIB-*`). Writing a note means racing Obsidian's own edits and its sync. §12 Q2 |
| D6 | **A tenth tab, "Todo", right after Home.** `G T` opens it; `N` from any page opens the quick-add box (§3.2). Its count is overdue plus due today | The owner asked for a screen. Being right after Home means the first two tabs together answer "what's on my plate". The 1,000 px fit is measured on the mockups (§10, Ph0), with a short label as the fallback. §12 Q3 |
| D7 | **Due dates are local calendar days.** Overdue = before today in the app's time zone. The quick-add box reads a trailing due word ("today", "tomorrow", a weekday, "mon".."sun", "11-10", "2026-11-10") and shows the date it read before Enter. Anything else stays in the text | "fri" in the box is what makes adding fast. Showing the parsed date avoids a surprise. §12 Q4 |
| D8 | **Done is soft.** `todo.done` sets `doneAt`. Undo is `todo.reopened`. A done todo shows under "Done today" until local midnight, then only on `sindri todos --done`. `Delete` is `todo.removed` (Undo re-adds the same id with `todo.restored`) | Undo must work and must not lose the id or the link |
| D9 | **Stale writes are refused.** Each write carries the todo's version, and a stale one is `SND-TODO-409` (the `SND-PROJECT-409` rule). The app reloads the row and keeps the typed edit in the box | Two windows or the CLI and the app editing the same todo |

## 2. Goals and non-goals

**Problem.** The owner's plate is split: Sindri shows what agents, PRs and Linear need from them, but their own todos live elsewhere, so no one screen answers "what do I do next?"

**Who and when.** The owner, at the start of the day and between reviews, with Sindri already open on the work laptop. Triggers: a thought to capture right now (in a review, in a run's sheet), a due date arriving, finishing something.

**Ideal.** Adding a todo takes under two seconds from any page without losing your place. Opening Todo shows the next thing at the top. Nothing pulled in from elsewhere can be closed by a stray keypress.

**Requirements** (EARS):

- REQ-U1 The app shall list the owner's open todos grouped Overdue, Today, Upcoming, Someday, each group by due date then by when it was added.
- REQ-U2 The app shall show, under the todos, a read-only From elsewhere section with the Needs you rows, the PRs waiting on the owner's review, and the Linear issues assigned to the owner, each opening its own page.
- REQ-U3 Sindri shall store todos so they survive a restart and a `views rebuild`, and the CLI and the app shall show the same list.
- REQ-E1 When the owner presses `N` on any page, the app shall open the quick-add box with focus in it. When they press Enter on non-empty text, it shall save the todo and close the box, keeping focus where it was before.
- REQ-E2 When the owner checks a todo off or deletes it, the app shall show Undo for 5 seconds, and Undo shall bring it back with the same text, date and link.
- REQ-E3 When local midnight passes while the app is open, the groups and the tab's count shall move without a restart.
- REQ-S1 While Sindri isn't running, the page shall show the existing Not running card, and `N` shall say "Sindri isn't running" instead of opening a box it can't save.
- REQ-S2 While a todo's link points at something that no longer exists, the chip shall say so ("PR #12 not found") and the todo shall stay.
- REQ-O1 Where a todo has a link, the row shall show a chip that opens the linked page.
- REQ-O2 Where a todo has a due date, the row shall show it as a relative word for the next 6 days ("Fri") and as a date after that.
- REQ-W1 If an agent session calls any todo route, Sindri shall refuse it with `SND-AUTH-002` and change nothing.
- REQ-W2 If a save fails (stale version, Sindri unreachable), the app shall keep the typed text in the box and say why.
- REQ-W3 If the quick-add text holds a secret-shaped string, Sindri shall scrub it before it's stored, the same as Projects' notes.

**Non-goals.** Notes. Obsidian sync. Reminders that pop up. Recurring todos. Sharing todos with anyone. Moving items, issues or decisions into the todo store. Drag-to-reorder.

**Later.** Recurring todos ("every Mon"). A read-only import of `- [ ]` lines from one Obsidian note (§12 Q2's second option). Agent sessions proposing a todo for one click (§12 Q6's third option). Manual order within a group.

**Success measure.** Two weeks after Ph2 ships, `sindri todos --stats --since 14d` (new) reports todos added and done. Target: at least 5 added a week, and at least half the ones due in the window done by their date. The owner says in one line whether they still keep a todo list anywhere else.

## 3. Where it sits in the app

| Surface | Reads | Writes | Reached from |
|---|---|---|---|
| **Todo page** (new, `PageId.Todo`, deep link `sindri://todo`) | `GET /sindri/todos` (new), plus `needs-you`, `reviews`, `linear/issues?group=assigned` (existing) for From elsewhere | todo writes | the top-bar tab after Home, `G T`, ⌘K "Todo" |
| **Quick-add box** (new, an overlay at the top of the window) | — | `POST /sindri/todos` | `N` on any page except inside a text box; ⌘K "New todo" |
| **Home** (existing) | one new line, "1 due today · 1 overdue" (the same two numbers as the tab's count), which opens Todo | none | Home |
| **Tray** (existing) | the tooltip gains "1 overdue todo" when there is one | none | — |

### 3.1 The page

One column, max 760 px wide, centred. Header: "Todo", the count line ("4 open · 1 overdue"), and the hint "N New todo". Then the groups, each a heading with its count, hidden when empty:

- **Overdue**: red due word ("Mon", "Oct 30").
- **Today**.
- **Upcoming**: due after today, by date.
- **Someday**: no due date, newest first.
- **Done today**: collapsed, with a count. Expands with Enter on its heading.

A row is a checkbox, the text, the due word on the right, and the link chip if there is one. **From elsewhere** comes after a divider: three groups (Needs you, Reviews waiting on you, Linear assigned to you), at most 5 rows each, then "Show all N in Reviews/Work/Home". Its rows have no checkbox: a "→" marks that they open somewhere else.

### 3.2 The quick-add box

A 560 px box under the top bar, over the page, which stays visible behind it. One text field with the placeholder "New todo… (add a day: fri, tomorrow, 11-10)". As you type, a line under it shows what will be saved: "Due Fri 11-10" or "No date". A link is added by pasting a PR url, a Linear key (`ENG-12`) or an item id (`ITM-…`) anywhere in the text: the line shows "Links PR #12" and the url leaves the text. Enter saves. Esc closes, keeping the draft until the app restarts. Shift+Enter saves and keeps the box open for the next one.

### 3.3 Keys

| Where | Key | Does |
|---|---|---|
| Anywhere (not in a text field) | `G T` | Opens Todo |
| Anywhere (not in a text field) | `N` | Opens the quick-add box |
| Todo page | `J` / `K`, Up / Down | Move between rows, across groups and into From elsewhere |
| Todo page, todo row | `X` or Space | Check off / reopen |
| Todo page, todo row | `Enter` | Edit in place (text, then Tab to date, Tab to link). Enter saves, Esc cancels |
| Todo page, todo row | `Delete` / Backspace | Remove, with Undo |
| Todo page, todo row | `D` | Opens the due picker on the row: today, tomorrow, next Mon, a date, none |
| Todo page, any row with a link | `O` | Opens the linked page |
| Todo page, From elsewhere row | `Enter` | Opens its page |
| The Undo toast | `⌘Z` | Undo, while the toast shows |

Ph0 checks `N`, `X`, `D` and `O` are free on every page they apply to, and that `N` isn't taken by a page's own single keys. Where one clashes, that page's key wins and the palette's "New todo" stays.

### 3.4 States

| Surface | Loading | Empty | Error / slow | Success |
|---|---|---|---|---|
| Todo page | skeleton rows, first load only (while Sindri isn't running, the tab shows no count) | no todos at all: "Nothing on your list. Press N to add one." From elsewhere, nothing waiting: hidden | Sindri not running: the existing Not running card. One From elsewhere source failing: that group says "Couldn't load Reviews" with Retry, and the rest stay | the groups |
| Quick-add | — | Enter on empty text does nothing | save failed: the box stays open with the text and the reason (§6's text, or "Sindri isn't answering" when the call failed), Enter retries | the box closes, toast "Added: due Fri" with **Show** (opens Todo at the row) |
| Row edit | "Saving…" after 300 ms | — | `SND-TODO-409`: "Changed somewhere else. Here's the latest", the row reloads, the edit stays in the field | the row shows the new values, focus stays on it |
| Check off / delete | instant, rolled back on failure | — | failure: the row comes back with "Couldn't save: <reason>" | toast with Undo |

### 3.5 Accessibility

- Groups are heading-led sections. Each row reads "Todo, reply to Sam about the offsite, due Friday, not done", plus "linked to PR 12" when it has a link.
- The checkbox is a real `CheckBox` with that name. `X` and Space toggle it.
- The quick-add box is a dialog with a labelled text field. The parsed-date line is a polite live region. Esc returns focus to where it was.
- The Undo toast is a polite live region. `⌘Z` works while it shows. It doesn't take focus.
- Due words carry text, never colour only ("Overdue · Mon").
- From elsewhere rows read as links: "Opens in Reviews: PR 2071, waiting on you".

## 4. Data (api lane)

### 4.1 Events (new, ledger; not Public)

| Event | Payload | When |
|---|---|---|
| `todo.added` | `id` (`td_<ulid>`), `text`, `due` (date or null), `link` (§4.3 or null), `clientId` (the box's, for retry) | Add |
| `todo.changed` | `id`, `version`, the changed fields (`text`, `due`, `link`; null clears `due`/`link`) | Edit |
| `todo.done` / `todo.reopened` | `id`, `version` | Check off / Undo or reopen |
| `todo.removed` / `todo.restored` | `id`, `version` | Delete / Undo |

`text` passes through `Aw.Redaction` before the event is written. Idempotency key for `todo.added`: `todo.added:<clientId>`, so a retried Enter after a lost answer makes one todo.

### 4.2 Projection

`TodosProjection : IProjection` (new, `Aw.Sindri.Core/Todos/`) folds the events into `views.db` table `todo` (`id`, `text`, `due`, `link`, `added_at`, `done_at`, `removed`, `version`), migration `00NN_todos` (next number). It's a pure fold, rebuilt by `sindri views rebuild`, and invalidates `ApiViews.Todos` (new).

### 4.3 Links

A link is one of: an item (`ITM-…`), a PR (`<owner>/<repo>#<n>`, or its GitHub url), a Linear issue key (`ENG-12`), or a workflow address. It's stored as the deep link the app already opens (`sindri://item/…`, `sindri://reviews/<owner>/<repo>/<n>`, `sindri://linear/ENG-12`, `sindri://workflow/…`). The server checks only the shape (`SND-TODO-001`), never whether the thing exists, so a link to something not synced yet is allowed. The chip resolves it when drawn (REQ-S2).

### 4.4 Routes and the CLI (new)

| CLI | Route, class | Prints | Exit |
|---|---|---|---|
| `sindri todos [--done] [--all] [--stats --since <N>d] [--json]` | `GET /sindri/todos?done=&all=`, desk + owner check | one line per todo: `<id>  <due or ->  <text>  [<link>]` | 0; 1 not running; 2 refused or error |
| `sindri todo add <text> [--due <day>] [--link <ref>] [--json]` | `POST /sindri/todos`, desk + owner | `td_… added (due Fri 11-10)` | 0; 1; 2 (`SND-TODO-001`, `SND-AUTH-002`) |
| `sindri todo edit <id> [--text T] [--due D \| --no-due] [--link L \| --no-link] [--version N]` | `POST /sindri/todos/{id}/change` | `td_… changed` | 0; 1; 2 (`-001`, `-404`, `-409`) |
| `sindri todo done\|reopen\|remove\|restore <id> [--version N]` | `POST /sindri/todos/{id}/{verb}` | `td_… done` | 0; 1; 2 (`-404`, `-409`) |

Without `--version`, the CLI reads the todo first and sends its version, as `item close` does. `--due` takes the same words as the quick-add box (D7). Each write is a row in `Host/Catalog/TodoRows.cs` (new) with its label in `Labels.Actions.cs`, and `Golden/Api/commands/catalog.json` gains them.

## 5. The page (ui lane)

`TodoPageViewModel` (new, `ViewModels/Todo/`) reads `GET /sindri/todos` on connect and on `ApiViews.Todos` invalidation, and works out the groups from `due` and the app's clock (`IClock`), regrouping at local midnight (REQ-E3). From elsewhere reuses the view models that already read those routes (Home's Needs you, the Reviews badge's list, Work → Linear's assigned group), capped at 5 rows. `QuickAddViewModel` (new) owns the box, the due-word parser (D7, a pure function in `Aw.Sindri.Ui/Todo/DueWords.cs` with one test per word) and the draft. `NavEntry` gains Todo with key `T`, short label "Todo", count = overdue + today. `SecondKey` gains `"T" => PageId.Todo`.

## 6. Errors (new)

| Code | HTTP | When | Text |
|---|---|---|---|
| `SND-TODO-001` | 400 | empty or over-280-character text, a line break, a bad date, a link of no known shape | "A todo is one line of 1 to 280 characters." / "No date 'frday'. Try fri, tomorrow or 11-10." / "That link isn't an item, PR, Linear key or workflow." |
| `SND-TODO-404` | 404 | no such todo | "No todo td_01J…." |
| `SND-TODO-409` | 409 | stale version | "That todo changed somewhere else. Reload and try again." |
| `SND-AUTH-002` (existing) | 403 | an agent session, any todo route | the existing text |

Each is a row in `SindriErrors.cs` and `docs/csharp/sindri.md`'s error table.

## 7. Security

- **Todos are the owner's alone** (D2): reads refused to sessions too, no MCP tool, events not Public. Agent sessions run as the owner's uid and could read `ledger.db` directly; that's accepted under the threat model (honest mistakes, not a bad actor).
- **Text is scrubbed** (REQ-W3) before the event is written, so a pasted token never reaches the ledger.
- **Links are shape-checked** and only ever open Sindri's own pages or an https PR url on a configured forge host.

## 8. Docs to update

`docs/csharp/sindri.md` (CLI, routes, errors, events), the events catalog (`EventCatalog` rows, not Public), `docs/csharp/sindri-ui.md` (the Todo tab, `G T`, `N`, the quick-add box, Home's line, the tray tooltip, the palette rows "Todo" and "New todo"), `planning/ERD.md` (`todo`), AGENTS.md Commands (`sindri todos`, `sindri todo …`). No new environment variables, no `deps.yaml` edge, no profile key.

## 9. Proof

| Row | Check |
|---|---|
| TP-1 | On the fixture: `N` on Reviews, type "check flaky test fri", Enter: the box closes, focus is back on the Reviews row it left, Todo shows the row under Upcoming due Fri, and `sindri todos` prints it. REQ-E1, REQ-U3 |
| TP-2 | `X` on a todo, then Undo: same id, text, date and link. `Delete`, then Undo: same. `views rebuild`: the list is unchanged. REQ-E2, REQ-U3 |
| TP-3 | The fake clock passes midnight with the app open: Today's row moves to Overdue, the tab count and the tray tooltip change. REQ-E3 |
| TP-4 | An agent session calls every todo route, reads included: `SND-AUTH-002` each, nothing changes. REQ-W1 |
| TP-5 | Edit a todo in the CLI while the app has it open in edit: the app's save gets `SND-TODO-409`, the row reloads, the typed edit stays. REQ-W2 |
| TP-6 | From elsewhere shows the fixture's Needs you row, its waiting PR and its assigned Linear issue. Enter on each opens its page, and Linear failing shows "Couldn't load Linear" while the other two stay. REQ-U2 |
| TP-7 | The top bar at 1,000 px and text scale 1.3 with ten tabs: nothing is cut, and the step-down order holds. D6 |
| TP-8 | On the owner's laptop for a week: the owner adds and finishes real todos and answers §2's success line. |

## 10. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Depends on | Proof |
|---|---|---|---|---|
| **Ph0** | ui | Mockups (in this spec's `mockups/`, refreshed after §12's answers): the page with every group and From elsewhere, the quick-add box with a parsed date and a link, a row in edit, the 409 row, the Undo toast, empty and Not running, the top bar with ten tabs at 1,000 and 1,440 px. The key-clash check (§3.3) | this spec | the owner's pick; the 1,000 px top bar fits (TP-7) or the fallback is chosen |
| **Ph1** | api | Events, `TodosProjection` and its migration, `ApiViews.Todos`, `TodoWrite.Begin`, the routes, the CLI, catalog rows, errors, API goldens, `--stats` | this spec | unit: fold per event, due-word parsing on the CLI, redaction, owner check per route; integration: add → list → done → reopen → remove → restore over HTTP, a retried add with the same `clientId` makes one, `-409`, `views rebuild` gives the same table; ApiGoldens |
| **Ph2** | ui | The Todo page, the quick-add box, the keys, Home's line, the tray tooltip, the palette rows | Ph0's pick, Ph1's goldens (hand-written goldens first, so it can start the day Ph1 does) | UI tests: TP-1, TP-2, TP-3, TP-5 in headless; one per §3.5 line; Frames tests for each §3.4 state |
| **Ph3** | ui | From elsewhere | Ph2 | TP-6 on the fixture |
| **Ph4** | — | Owner proof | Ph3 | TP-8 |

**Parallel plan.** Day one: ui on Ph0's mockups, api on Ph1 red-first. When the owner picks, ui starts Ph2 against hand-written goldens of `GET /sindri/todos` (one file, written by api as Ph1's first commit), and swaps in the real goldens when Ph1 merges. Ph3 needs no server change, so it can run in a second ui builder as soon as Ph2's page shell has merged. There's no context-lane work.

**Shared files, one owner each.** `Usage.cs`, `PublicAPI.Unshipped.txt`, API goldens, `catalog.json` and `docs/csharp/sindri.md`: Ph1. `docs/csharp/sindri-ui.md`, `NavEntry` and the chord table: Ph2.

**Gates per phase.** `-warnaserror` build; touched projects' unit and integration lanes; `Aw.Architecture.Tests` (new `Todos` folders in Core, Features and Ui); scoped format; `check-env-example.sh`; ApiGoldens classes (integration lane) and the catalog golden (Ph1); `Aw.Sindri.Ui.Tests` unit + integration, and `Aw.Sindri.Ui.Frames.Tests` integration for the new view (Ph2, Ph3, since the catalog gains rows and the app a view); the full `Aw.Sindri.Tests` unit lane (Ph1 changes `Usage.cs`).

## 11. Open risks

- **Ten tabs at 1,000 px.** "Todo" is already as short as a label gets, so if TP-7 fails, the fallbacks in order: Todo becomes a button in Home's header ("Todo 4", `G T` unchanged), or the window minimum grows to 1,060 px. Ph0 measures and the owner picks.
- **`N` everywhere.** A page may already use `N` (Ph0 checks). The palette's "New todo" always works.
- **From elsewhere duplicates Home.** Home already shows Needs you. If the owner finds it noisy, From elsewhere folds behind a heading that remembers its state.

## 12. Owner questions (asked 2026-11-07 through meta; my recommendation first)

1. **Where do todos come from?** Recommend: typed by you, plus the read-only From elsewhere section (Needs you, PRs waiting on your review, Linear issues assigned to you). Alternatives: typed only; or one merged list where checking off a Linear row closes it in Linear (needs the write key, easy to do by accident). *Pending.*
2. **Obsidian?** Recommend: none in v1 (D5). Alternatives: read-only import of `- [ ]` lines from one note you name; two-way sync with one note. *Pending.*
3. **Where in the top bar, and the keys?** Recommend: a tenth tab after Home, `G T`, `N` to add from anywhere, `J/K/X/Enter/Delete/D/O` on the page (§3.3). Alternatives: a Todo panel at the top of Home, no tab; a third view on Work's Sindri / Linear switch. *Pending.*
4. **Due dates?** Recommend: an optional date, the four groups, the overdue count on the tray, no pop-ups. Alternatives: no dates; dates with desktop notifications. *Pending.*
5. **Links?** Recommend: one optional link to an item, PR, Linear issue or workflow. Alternative: plain text only. *Pending.*
6. **Agent sessions?** Recommend: owner only, reads included (D2). Alternatives: sessions may read; sessions may propose a todo you accept in one click. *Pending.*
7. **Where do your todos live today, and what must feel instant: adding one, or seeing what's next?** Fills §0 and ranks §3.2 vs §3.1 in the mockups. *Pending.*

## Revision log

- **Rev 1.1** (2026-11-07): mockups added (`mockups/`, ten tabs fit at 1,000 px with 76 px spare at 100% text; at 130% every tab count hides at the last step-down); §3.4 save-failed reason and Not-running tab count, Home's line matches the tab count.
- **Rev 1** (2026-11-07): first draft on the recommendations above, before the owner's answers.
