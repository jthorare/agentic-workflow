# Sindri's Notes page: write it down once, see it next to the work

Status: draft (ideas lane, 2026-11-12). Serves #2981 (Notes) and #2978 (one app that replaces the others). Builds the rows of the Notes prior-art ledger (`docs/sindri/prior-art/notes.md`, merged with #2989's rule) and the issues #3034 to #3039.
Frames: design's round 3 at `pr-assets/notes-2981` @ `4e37789f29` (`guesses.md` beside them). Frame names below are files in that folder.
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

## 0. Why

The owner's bar (#2981): the mega app replaces the notes app too. Notes are plain Markdown files in a folder the owner chooses. Sindri is the best editor for them because it is the only one that knows the work.

What main does today (read from `origin/main` 2bb9a2de53; the app was not driven for this spec):

- **There is no notes page.** No capture, daily page, editor, `[[` link, backlink, note search, template or history exists (ledger, "How Sindri stands today").
- **One note is wired.** The todo list syncs with ONE Markdown note in a library vault (`sindri todo note set <vault> <note>`; `docs/sindri/todo-note.md`; `src/Sindri/Aw.Sindri.Core/Todos/Note/`: `ITodoNoteDisk.cs`, `ITodoNoteFiles.cs`, `INoteLocator.cs`). It refuses symlinks and multi-link files and keeps a base to detect edits made outside Sindri. Its code is the starting point for the write path.
- **Vault roots exist.** `context.sources.library.vaults` holds up to 10 `{name, path, deny, models}` entries with the root rules of `SND-LIB-001` (never `/`, the home folder or a folder that holds `$AW_SINDRI_HOME`; no symlink followed; built-in deny for `.obsidian/`, `.trash/`, dot folders, `.env*`, keys). `todo.obsidian.vault` already picks a vault by name.
- **The design-doc library reads only `aw-doc: 1` files** (`docs/sindri/context.md`; `sindri library sync`). Plain notes are skipped on purpose.
- **A project has one free-text `notes` string** (`ProjectLedNoted`, `Core/Projects/ProjectDecisions.cs`). It has no links.
- **Ctrl+K exists with no `note:` scope** (`Ui/ViewModels/Palette/CommandPaletteViewModel.cs`).
- **Home's "Needs you" rows exist on main** (`Ui/ViewModels/NeedsYou/`, `HomePage.NeedsYou.cs`). The ranked `GET /sindri/home` route and `home_links` table are specified in the merged Home spec (`2026-11-12-sindri-home-inbox-design.md` §8) and are not built.
- **The global capture window is not built.** Ctrl+Alt+I belongs to Projects' create window (#3170, frames in `pr-assets/projects-2961`). The tray (`Ui.Desktop/AvaloniaTray.cs`) exists; no global hotkey code does. Notes depends on #3170's window and adds a second kind to it.

The job: **write a thought down in under 2 seconds from anywhere, find it again, and see it next to the issue, PR, thread or run it is about, in files another app can open unchanged.**

## 1. The bar

"Ledger N" is problem N in `docs/sindri/prior-art/notes.md`. "Take N" is item N of its closing list "What we take".

| Job | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Capture in under 2 s | 1 | Obsidian `daily:append`, Raycast Append to Daily Note | one global window, Enter appends `- 14:32 text` under Captured; `[ ]` makes a task; the line is never lost | §4.1 (Take 1, #3034) |
| Today's page | 2 | Obsidian Daily notes, Foam | opens on today, created from a template; previous / next existing day; open tasks from earlier days; our brief fold | §4.2 (Take 2, #3035) |
| Link while typing | 3 | Obsidian `[[`, Bear | `[[` popup: notes first, then issues, pull requests, threads, runs, scopes, files; Obsidian syntax in the file | §4.4 (Take 3, #3036) |
| Backlinks and unlinked mentions | 4 | Obsidian Backlinks | right pane: Linked objects, Linked mentions, Unlinked mentions; "Link it" | §4.5 (Take 3, #3036) |
| Jump by half a name | 5 | Obsidian Quick switcher | `note:` scope in Ctrl+K, title match before text match, newest first | §4.6 (Take 4, #3037) |
| Search with filters | 6 | Obsidian operators, zk FTS | the same box and the `/` filter read the SQLite index; operators `tag:`, `path:`, `about:` | §4.6 |
| Open tasks across notes | 7 | Obsidian Tasks | only the carried-tasks block on today's page in v1 | §4.2, §10 |
| Templates | 8 | Obsidian Templates | the daily template only in v1; other templates after the soak | §4.2, §10 |
| Live state of linked work | 9 | Notion link previews | a chip with live state; nothing cached in the file | §4.7 (Take 5, #3038) |
| Line to issue | 10 | Linear create from selection | Ctrl+Shift+I on a selection | §4.8 (Take 5, #3038) |
| History and undo | 11 | Obsidian File recovery | a version per save; restore an older version as a new one | §4.9 (Take 6, #3039) |
| Agent writes by rule | 12 | Basic Memory | per-folder rule; an agent edit is a proposed diff the owner accepts | §4.9, §7 |
| Notes as a table | 13 | Obsidian Bases | later | §10 |
| Design-doc items vs PRs | 14 | none | stays as is (Take 7) | n/a |

**The bar in one line:** the owner's existing notes folder opens unchanged, an edit round-trips byte-clean, a capture is on disk under 2 s after the hotkey, and every issue, PR and run a note links shows that note on its own page.

Done-checks. Each is a test or a measured run in the real app (`aw-ui-driver`). Each names its lane in §12.

| # | Check | Measured as |
|---|---|---|
| N1 | **Byte-clean.** A fixture vault (made-up notes, mixed line endings, no trailing newline, front matter, a symlink, a hard-linked file). Open each, change one character, save. | `git diff --stat` on the vault shows the one changed line per file and nothing else. The symlink and the hard-linked file are refused. |
| N2 | **Capture speed.** Hotkey to the line on disk. | Under 2 s on the real app with the main window closed; the window paints under 100 ms after the hotkey (window pre-created, hidden; ledger 1). |
| N3 | **Capture never loses a line.** Sindri down, folder removed, write refused, Linear down. | In each case the window keeps the typed text and shows the sentence and the one fix of §6. After the fix, Enter saves it. A kill -9 of the window loses 0 characters (the draft is on disk as typed). |
| N4 | **Today in 100 ms.** Open Notes on a 1,000-note vault. | Today's text paints under 100 ms from a local read; the fold and chips fill in after and never block the text. |
| N5 | **Index speed.** 1,000-note vault. | One edited file reindexes under 100 ms (api's spike number; posted on #2981 before the watcher design is fixed). Cold start under a few seconds. Backlinks for the open note under 100 ms. |
| N6 | **Chips are live.** A linked PR merges. | The chip changes within 1 s of the ledger event. The file is byte-identical before and after. |
| N7 | **Every linked object shows its notes.** | The PR, issue and project pages list the notes that link them (the reverse walk of context's `about` edges). |
| N8 | **Agents never edit silently.** | An agent write over the CLI or MCP lands as a proposal; the file is byte-identical until the owner accepts. A stale proposal cannot be accepted (§4.9). |
| N9 | **A week without the other app.** | ux uses Notes for the owner's real week with the other app closed and files every miss as an issue `Part of #2981`. |
| N10 | **Screen reader.** | `aw-ui-driver tree` names every row, chip, region and button; a chip reads "pull request 3104, merged"; a not-live chip adds "as of 14:02". |

## 2. Decisions

Taken for the owner. Each says where it came from. "Written to the lead's pick" means the inputs left it open and the spec picked the simplest answer; each one is also in Open questions.

1. **Notes are plain Markdown files in a library vault the owner names.** No second root. The profile gets `notes.vault` (a vault name) and `notes.folder` (an optional subfolder), as `todo.obsidian.vault` does. Path checks reuse the vault's deny globs and the `SND-LIB-001` root rules. A denied file is hidden from the index and from every read. (Meta, #2981 api plan, accepted 2026-11-12.)
2. **The files are the store; the index is a view.** One row per `.md` file in `rows.db`, key `note:<vault>/<path>`, rebuildable from disk at any time. Chips, the brief fold and the carried-tasks block are drawn by the app and never written into a file. (Ledger open question 2; api plan.)
3. **Every write is an atomic replace guarded by the file's hash.** The editor opens a file at its sha256. A write re-reads the file; a different hash is a conflict and nothing is written; a matching hash writes a temp file in the same folder, fsyncs and renames. Bytes outside the edit stay as they were. (Api plan; N1.)
4. **Delete moves the note to the vault's `.trash`.** There is no hard delete in v1. (Meta, #2981, 2026-11-12.)
5. **Capture rides the one global Ctrl+Alt+I window shared with Projects (#3170).** The title names the kind: "New issue" or "New note". An Issue / Note switch sits at the left; Ctrl+Tab flips it; the window opens on the kind used last. The switch is hidden when no notes folder is set, and the window then makes issues only. (Lead, 2026-11-12.)
6. **A refused hotkey uses #3170's Settings row.** On Wayland the hotkey goes through the desktop's global-shortcuts portal. A refused hotkey shows "The OS refused this hotkey." and one fix, "Choose another shortcut". The tray item "New issue or note" still works. (Lead, 2026-11-12; `notes-settings-hotkey-refused.png`.)
7. **A capture never loses the typed line.** If Sindri is down, the folder is not set or the write fails, the window stays open, keeps the text and shows one sentence and one fix (§6). If the folder is removed while the window is open, the window shows "No notes folder yet" and keeps the line. (Lead, 2026-11-12; `notes-capture-error-*.png`.)
8. **Capture appends under `## Captured` with a time.** A plain line is `- 14:32 text`. A line that starts with `[ ]` is a task, `- [ ] 14:32 text` (checkbox first, so Obsidian Tasks reads it). Today's file is created from the daily template when missing. The toast says "Captured" and keeps Undo for 8 s; Undo removes exactly that line and turns off once the note changed. Enter on an empty line saves nothing, keeps the window open and says "Nothing to save." (Ledger 1; ideas' check on #3034; guesses 4.)
9. **Today's page opens with a "Needs you / Moved since yesterday" fold (v1).** Up to 5 rows in each group, drawn with Home's rows. The fold stays collapsed once you scroll past it, until the next day. The daily file has no heading: the page draws "Thursday 12 Nov 2026" from the file name, the list row says "Thu 12 Nov", the breadcrumb says `2026-11-12`. (Lead, 2026-11-12; `notes-today.png`, `notes-today-folded.png`.)
10. **Carried tasks are drawn, not stored.** The block "Open tasks from earlier days" comes from the index; it is not in today's file. A tick edits the original line in its own day's file (one character). (Ideas' check on #3035; guess 6.)
11. **Backlinks go in the right pane.** Linked objects (collapsed) above Linked mentions above Unlinked mentions. There is no PR-body backlink row: the index does not read PR bodies. (Lead, 2026-11-12; `notes-backlinks*.png`.)
12. **Every issue, PR and run reference is a live chip, unless the cursor is on its line.** The cursor line shows the raw text (`[[#3104]]`). Plain blue text means a link Sindri cannot resolve. A chip shows a state glyph, the id, the short title and a state word; never color alone. (Lead; `notes-live-chips.png`, `notes-live-chips-legend.png`.)
13. **"Not live" dims only the glyph and the chip edge.** The text stays at normal ink (4.5:1 or more). The tooltip reads "as of 14:02". The right pane's rows dim their glyphs the same way. A chip for an object Sindri does not know says "unknown" with one fix, "Add the repo in Settings". (Lead; `notes-live-chips-notlive.png`, `notes-today-down.png`.)
14. **Make issue is Ctrl+Shift+I.** A bare `m` would type into the editor. (Lead; replaces the `m` in #3038's body.)
15. **History restore writes an older version as a new version and diffs it against the file now.** The current version reads "Current" and has no Restore. (Lead; `notes-history-restore.png`.)
16. **One Notes Settings frame:** the notes folder, the per-folder agent write rule, and the capture hotkey row shared with #3170. (Lead; `notes-settings.png`.)
17. **Checkbox border is `#70757c` everywhere** (3.4:1 on the capture window's `#222326`, more on the page's `#08090a`). (Lead.)
18. **A bare letter never writes to an outside source (GitHub, Linear).** Make issue, resolve and the like go through a button, a chord with Ctrl, or a menu. No typed confirmations. Offline never queues a write to an outside source: Make issue is off while Sindri or the tracker is down, and the line stays as written. Writes to the owner's own files are local and may use a chord. (Lead, team rules.)
19. **Agent writes are proposals.** An agent reads a folder only when its retrieval switch is on (default off). It appends or edits only under the owner's per-folder rule: "Agents propose, you accept" (default) or "Agents can't edit". Every agent edit, an append included, arrives as a diff in History that the owner accepts or declines. The proposal is not in the file until Accept. (Meta, #2981; ledger 12 narrowed: the named-note direct append in the ledger is dropped for v1, see Open questions.)
20. **The queue of notes is a list on the row / peek / full grammar.** `RowList`, `Row` and `PeekPane` on `RowKeys`, with no key handler of its own (#2978; Reviews spec decision 14). Space peeks, Enter opens the editor as the full view.
21. **Day chords in the editor are Alt+PageUp (previous day) and Alt+PageDown (next day).** Alt+Left and Alt+Right jump a word on macOS, and Ctrl+Alt+Arrow switches workspace on GNOME. Previous and next mean the previous and next EXISTING day (Foam's rule). From the list, `g p`, `g n` and `g d` (today) work, since a bare letter types in the editor but not in the list. (Design, round 3; `guesses.md` 16.)
22. **Where history lives: `$AW_SINDRI_HOME/notes-history/`, never in the vault.** A version is kept per save (and per accepted proposal, restore and capture). Written to the lead's pick. (Ledger 11; api plan left git versus copy open.)
23. **Error codes are `SND-NOTE-001..029`.** The frames' `NOTES-WRITE-001`, `NOTES-HISTORY-001` and `LINEAR-CREATE-502` were placeholders (`guesses.md` 2). Reserved now; no `SND-NOTE-` code exists on main.
24. **Agents get the same verbs.** Every action has a CLI verb and an MCP tool; a session reads and proposes but never presses the owner's buttons (§7).

## 3. The model

### 3.1 The note row

`NoteRow` in `Aw.Sindri.Core.Notes`. A view over a file; the file is the truth.

| Field | What it holds |
|---|---|
| `id` | `note:<vault>/<path>` |
| `path`, `size`, `mtime`, `sha256` | from disk; `sha256` is the version a write names |
| `title` | the first `#` heading; else the file name; for a daily note, the date from the file name |
| `kind` | `daily` (name is `YYYY-MM-DD.md` in the daily folder), `linked` (links at least one issue, PR, thread, run or scope), `note` |
| `headings[]`, `tags[]` | `#tag` and front-matter `tags` |
| `links[]` | `[[Note]]`, `[[#123]]`, URLs, `[[thread:..]]`, `[[run:..]]`, `[[scope:..]]`, `[[code:..]]` |
| `firstLine` | the first body line that is not a heading |
| `waitsOn` | `agent edit` while a proposal waits, else empty |

The index also stores the text, chunked by heading, for search (SQLite FTS5). A query reads the index and never walks the disk.

### 3.2 Link forms written into the file

Obsidian-compatible, so an existing vault opens unchanged (`notes-link-written.png`).

| Kind | Written | Note |
|---|---|---|
| Note | `[[Note Title]]` or `[[Note#Heading]]` | exactly as Obsidian writes it |
| Issue or PR | `[[#3112]]` | the repo is the note's linked repo or the profile's first repo |
| Thread | `[[thread:eng/1731]]` | placeholder id |
| Run | `[[run:8841]]` | placeholder id |
| Scope | `[[scope:export/upload]]` | placeholder id |
| Code | `[[code:ctx_9c41e2\|Uploader.cs:88]]` | the context map id keeps the link when a file moves |

The context lane picks the real ids (`guesses.md` 1). The index reads a PR or issue URL as the same link as `[[#123]]`.

### 3.3 Links as edges

Context's plan (accepted on #2981): `[[Note]]` is `links note:`; `[[#123]]` or a URL is `about item:`; `path:42` is `about code:`; a scope id is `about scope:`. "Every linked object shows its notes" is a reverse walk of those edges. The notes index and context's `notes` source read the same files and the same `notes.vault`.

### 3.4 Write intents

| Intent | What changes | Bytes touched |
|---|---|---|
| Edit | the open note, by the owner | the owner's edit |
| Append (capture) | a line under `## Captured` in today's file | one line, plus the heading and the file when missing |
| Tick | one task line, in its own day's file | one character |
| Link a mention | wraps one mention in `[[ ]]` in the other note | the brackets |
| Make issue | replaces one line with a link | that line |
| Restore | the whole file becomes an older version | the file, as a new version |
| Accept a proposal | applies the agent's diff | the diff |

Each is one op through `POST /sindri/rows/{id}/ops`, outbox, sender, reconcile (api plan), with `baseVersion` = the sha256 read. A file changed on disk since the editor opened it is a conflict (§4.10).

## 4. The page

Layout: the top bar, then the list (§4.3) with a peek, and the editor as the full view with the right pane (§4.5). The page opens on today (§4.2). `g d` from anywhere in the app opens today's page.

### 4.1 Capture

One window, shared with Projects (#3170). It is a frameless one-line window over the desktop that opens even with the main window closed.

```
┌ New note ──────────────────────────────────────────────────────┐
│ [Issue | Note]  [ ] ☐ call Priya about the retry cap           │
│ Adds to 2026-11-12 · Captured        - [ ] 09:12 call Priya... │
│ Enter save · Esc close · Ctrl+Tab Issue / Note                 │
└────────────────────────────────────────────────────────────────┘
```

- **Open.** The hotkey (Ctrl+Alt+I, changeable in Settings) or the tray item "New issue or note". The window opens on the kind used last. Title: "New note" or "New issue".
- **Note kind.** A preview line shows the exact text that will be appended. A leading `[ ]` draws an empty task box (border `#70757c`) and the line "starts with [ ]: saved as a task". When today's note does not exist the chip says "Creates 2026-11-12 from your daily template, under Captured" (`notes-capture-bar.png`, `notes-capture-bar-plain.png`).
- **Issue kind.** The Projects window: Project and Assignee chips, "in Linear · EXP", footer Enter create, Esc close, Tab change project, Ctrl+Tab Issue / Note (`notes-capture-bar-issue.png`). Its errors reuse the make-issue sentences of §6 ("Linear isn't connected", "Couldn't create the issue").
- **Enter.** Appends under `## Captured` and closes. A toast "Captured" with Open note and Undo (8 s) shows (`notes-capture-saved.png`). Undo removes exactly that line and turns off once the note changed (the toast says why).
- **Nothing to save.** Enter on an empty line saves nothing and the window says "Nothing to save. Type a line, or press Esc to close." (`notes-capture-empty.png`).
- **No folder.** The switch is hidden and the window only makes issues (`notes-capture-no-folder-no-switch.png`).
- **Failures** keep the line (§6; `notes-capture-error-down.png`, `-folder.png`, `-write.png`).
- **CLI twin.** `sindri note add "text"` (stdin accepted) takes the same append path. With Sindri down it fails with `SND-NOTE-001` and prints the text back; it queues nothing.
- **Pre-filled capture.** A note started from a review, a thread or a scope is pre-linked and quotes the selection (#2981 item 3). In v1: the Reviews page, a Home row and a context scope add a "Note about this" action that opens the editor on a new note with the `[[#123]]` link and the quote; the global window stays one line. Written to the lead's pick (Open question 4).

### 4.2 Today's page

Opening Notes lands on today's page. If the file is missing, Sindri creates it from the daily template and says "Created from your daily template · 09:02" (`notes-today-new.png`).

- **File.** `YYYY-MM-DD.md` in `notes.daily` (profile key, default `daily/`; an Obsidian vault's own daily folder is read once from `.obsidian/daily-notes.json` when present, read-only). The template is a plain `.md` file the owner names (`notes.template`, default none = an empty file). Variables: `{{title}}`, `{{date:FORMAT}}`, `{{time}}` as Obsidian spells them. The file has no heading.
- **Top of the page, outside the file.** In this order: the fold, then "Open tasks from earlier days" (a quiet label says "Drawn from the other days. Not in this file."), then the file's text with its own sections (Today, Captured).
- **The fold.** "Needs you" (up to 5 rows) and "Moved since yesterday" (up to 5 rows) in Home's row grammar. Until the Home route ships, "Needs you" reads the NeedsYou rows main has (`Ui/ViewModels/NeedsYou/`) and "Moved since yesterday" is hidden. Once `GET /sindri/home` ships, "Moved since yesterday" reads the Moving group rows whose `since` is after local midnight of the previous day. It stays collapsed once you scroll past it, until the next day (`notes-today.png`, `notes-today-folded.png`).
- **Day links.** "← Wed 11 Nov" (the previous existing day) and "Next day: none yet" (plain text, not a link). Alt+PageUp and Alt+PageDown in the editor; `g p` and `g n` from the list. A non-daily note shows no day row.
- **Saved.** A quiet "Saved" or "Saving..." sits next to the path.

### 4.3 The list

Tabs: All, Daily, Linked to work. The glyph is one page glyph (blue outline for a daily note; a dark and a light version). Rows are 32 px, as on Home.

`[glyph] [title, flex] [first line] [links] [folder, mono] [waits on] [age]`

- **Row.** Title, first line, link count, folder, age; "agent edit" in the waits-on cell while a proposal waits. A daily note's title is "Thu 12 Nov". A fact the source lacks is blank, never "-".
- **Order.** Pinned, then newest edit first. Groups: Pinned, Today, This week, Older (counts add to the All tab; `notes-list.png`: 3 + 3 + 3 + 205 = 214).
- **Peek (`Space`).** The body (links as chips; the one-line preview in each row stays plain text), Edited, Links "4 out · 3 in", Work (states of linked PRs and issues), File, Proposal. Actions: Open, Copy path, Copy link (`y`).
- **Filter (`/`).** Searches the index: title, headings, tags, text. Operators `tag:`, `path:`, `about:#123`, `kind:`. First result row under 100 ms at 1,000 notes.
- **Open.** Enter opens the editor. Ctrl+N opens a new note in the folder of the selected note (else the notes root), cursor in the body.

### 4.4 The editor and `[[`

The editor is the full view: a Markdown editor with live rendering of headings, lists, tasks, tables, code and images. Pasting an image writes it to `notes.attachments` (default `attachments/`) and inserts `![[name.png]]`. Esc leaves the editor to the list. In the editor a bare letter types.

- **`[[` popup.** Groups in this order, one row each: Notes, Issues, Pull requests, Threads, Runs, Scopes, Files. Recent first, then fuzzy over title and alias. A row shows the glyph, the id and the state word. First rows under 50 ms per keystroke at 10,000 notes (in-memory title index). Enter inserts the link (§3.2).
- **No match.** One row: "No note called "xyzq". Create it" (Enter creates the note and inserts the link; `notes-link-complete-empty.png`).
- **Still reading.** "Reading notes, results fill in" (`notes-link-complete-reading.png`).
- **Code links.** A moved file reads "Moved from src/Export/. The link follows it. Nothing to fix." A gone file reads "File not found. Pick another target." with "Pick another target" (`notes-link-rename.png`).
- **Footer while the popup is open** shows only the popup's keys.

### 4.5 The right pane

Three sections, in this order. The first is collapsed by default.

1. **Linked objects.** The issues, PRs, threads, runs, scopes and files this note links, as chips with live state. The count equals the rows listed.
2. **Linked mentions.** Notes that link this one, with the surrounding paragraph (1 line collapsed).
3. **Unlinked mentions.** Notes whose text holds this note's title without a link. "Link it" (Ctrl+Shift+L) wraps that mention in `[[ ]]` in the other note; the row moves to Linked mentions and says "linked now" (`notes-backlinks-linked.png`).

A filter box (Ctrl+Shift+F) filters both mention lists; it is hidden when both are empty. Empty: "Nothing links here yet." (`notes-backlinks-empty.png`). `notes-backlinks.png` shows the full pane.

**Notes on the other pages.** The PR page, the issue row and the project page list the notes that link them, in the same row grammar (N7). The list is the reverse walk of §3.3.

### 4.6 Ctrl+K `note:` and search

Typing `note:` limits the palette to notes, ranked by title match first, then text match, newest first inside each. A text-only match carries a small "in text" tag. The empty `note:` box lists recent notes with the previous note preselected for Down. A "Create note" row sits under an "Or create" header, shows the full path (the folder of the note you are in, else the notes root) and is hidden when a note with exactly that title exists. Enter opens; Ctrl+Enter creates (`notes-palette.png`, `notes-palette-empty.png`, `notes-palette-none.png`). The page footer behind the palette is hidden while it is open.

### 4.7 Chips

A chip is a view. The file keeps `[[#3104]]` or the URL; nothing about state is written into it (decision 2).

| Part | Rule |
|---|---|
| Anatomy | state glyph, id, short title, state word |
| Case | "In Progress", "Todo", "CI red", "merged", "closed", "open", "draft" (stated once so builders do not normalise it) |
| States | 11 in `notes-live-chips-legend.png`: open, draft, merged, closed without merge (own slashed-circle glyph, never the green check), CI running, CI red, In Progress, Todo, run failed, run passed, unknown |
| Update | within 1 s of the ledger event; 0 network calls on open (ledger 9) |
| Click | the chip opens the peek of its object; Ctrl+Enter on a focused chip does the same from the keyboard |
| Cursor line | raw text, no chip |
| Not live | decision 13 |

Not live means Sindri is down, or the row's `readAt` is older than the source's stale limit (Home's stale-source rule, Home §7.5). The page header says "Not live: Sindri isn't running. States as of 14:02." and the top bar says "Not connected".

### 4.8 Make issue

Select a line (or put the cursor on it) and press Ctrl+Shift+I. A bar under the line shows the target picker, the title (the line's text) and the note "replaces the line with its chip" (`notes-make-issue-picker.png`).

- **Default target.** GitHub in the repo of the note's linked PR when the note links exactly one; otherwise the tracker the profile sets (Linear, team EXP in the frames). The bar shows the chosen target. Written to the lead's pick (Open question 5).
- **Create.** One button, "Create issue" (Enter). It is a write to an outside source, so it is a button and an Enter on that button, never a bare letter.
- **Done.** The line is replaced by the new issue's chip with Undo (Ctrl+Z, 8 s) (`notes-make-issue-done.png`). Undo restores the line; it does not delete the issue (the toast says "Issue stays in Linear").
- **Errors.** "Linear isn't connected. Connect it in Settings, then try again." (`-noconn`). "Couldn't create the issue." with one fix and the code under Details (`-fail`). The line stays as written in both. With Sindri or the tracker down the button is off and nothing is queued (decision 18).
- **Meeting notes.** A "make all" action on an `## Actions` section makes one issue per task line, one confirm listing the titles. Written to the lead's pick (Open question 6).

### 4.9 History and agent edits

Ctrl+Shift+H opens History from the editor; Esc returns. History is a full view with a 340 px version list on the left and the diff on the right.

- **List.** Time, who (you, or "claude-code, session 7f3a" as a link to the agent session) and +/- line counts, newest first. "13 + 1 proposed" in the header. The newest saved version reads "Current" and has no Restore.
- **Restore.** Select an older version: the diff compares it with the file now, the header says "Restoring writes this version as a new version. Nothing is lost.", the one primary is "Restore this version" (`r`). (`notes-history-restore.png`).
- **Proposal.** A row "proposed edit, not in the file yet" with a band: "Proposed by claude-code, session 7f3a, at 14:21. It is not in the file yet. The file stays at the 14:05 version until you accept." and "Your rule for eng/: agents propose, you accept. Change rule" (a link to the Settings row). Accept (`a`, primary) writes it; Decline (`d`) drops it. There is no Undo: a proposal is not in the file, and Restore on the accepted row is the undo (`notes-history.png`).
- **Stale proposal.** The agent wrote against a version that is no longer current (the sha differs). Accept is off, Decline is primary, and the band says "The note changed after claude-code wrote this. Decline it and ask the agent again." (`notes-history-stale.png`).
- **Footer by selection:** proposal: `j/k`, `a`, `d`, Esc, Ctrl+K. Normal older version: `j/k`, `r`, Esc, Ctrl+K. Current, empty, error: Esc and Ctrl+K only.
- **States.** Loading "Reading versions"; empty "No earlier versions."; error "Couldn't read this note's history. Check the notes folder is reachable." with `SND-NOTE-0xx` under Details.

### 4.10 Conflict and save failure

- **File changed on disk** (Obsidian, git, a sync tool) after the editor opened: both versions side by side. "Keep mine" is the one primary, "Keep the file's" is secondary. The version you did not keep goes to History (`notes-today-conflict.png`).
- **Save failed:** one sentence, one fix, the code under Details. The text stays in the editor (`notes-today-savefail.png`).
- **Sindri not running:** the file still opens read-only text with the last index; chips and the fold are muted; one sentence and "Start Sindri" (`notes-today-down.png`).

### 4.11 States

| State | What shows |
|---|---|
| Loading list | rows paint from the local index under 100 ms; with a cold index "Reading 214 notes" (`notes-list-loading.png`) |
| No notes folder | "Notes are Markdown files in a folder you choose." and one primary, "Choose notes folder" (`notes-list-empty.png`). The capture switch is hidden |
| Empty folder | today's page is made from the template; the list shows that one note |
| Index behind | rows show with "as of 14:02"; search says "Still reading" |
| Today, first open | "Created from your daily template · 09:02" |
| Not live / Sindri down | §4.7, §4.10 |
| Conflict, save failed | §4.10 |
| Proposal waiting | "agent edit" in the list row; a band at the top of the editor: "claude-code proposed an edit. Review" |
| Hotkey refused | the Settings row of §4.12 |
| Palette: reading, no folder | one line each (`notes-palette-reading.png`, `notes-palette-nofolder.png`) |

### 4.12 Settings

One frame, Settings -> Notes (`notes-settings.png`, `notes-settings-hotkey-refused.png`):

- **Notes folder.** Choose or change. Writes `notes.vault` and `notes.folder`. A bad value is `SND-NOTE-002` with the key path.
- **Agent rule per folder.** Rows for eng/, daily/, people/, design/ and "Other folders". Two values: "Agents propose, you accept" and "Agents can't edit". A second switch per folder, "Agents can read this folder", sets context's `retrieval` (default off).
- **Capture hotkey.** Shared with #3170. A refused hotkey: "The OS refused this hotkey." and "Choose another shortcut"; the tray item still works.

The profile keys apply on save; `sindri profile set notes.vault <name>` does the same from a terminal.

### 4.13 Keyboard and focus

Grammar keys are Home's: `j/k`, arrows, `Home`/`End`, `/`, `Space`, `Enter`, `Esc`, `.`, `?`, `Ctrl+K`, `Left`/`Right` to fold. A bare letter below changes local state or opens something cancellable. None writes to an outside source (decision 18).

| Key | Where | Does | Writes? |
|---|---|---|---|
| `g d` | anywhere | open today's page | no |
| `g p` / `g n` | list | previous / next existing day | no |
| `Alt+PageUp` / `Alt+PageDown` | editor | previous / next existing day | no |
| `Ctrl+N` | list | new note | creates a file on first save |
| `Ctrl+K`, `note:` | anywhere | the palette scope | no |
| `[[` | editor | link popup | the link, when chosen |
| `Ctrl+Shift+L` | editor, pane | Link it (an unlinked mention) | the other note, one edit |
| `Ctrl+Shift+F` | editor | filter the mentions | no |
| `Ctrl+Enter` | on a chip | open the object's peek | no |
| `Ctrl+Shift+I` | editor | Make issue (opens the bar) | opens; Enter on Create writes |
| `Ctrl+Shift+H` | editor | History | no |
| `a` / `d` / `r` | History | accept / decline / restore, by selection | the file (accept, restore), local (decline) |
| `y` | list peek | copy link | no |
| `Esc` | editor | leave the editor | no |
| `Ctrl+Z` | editor | text undo; while a toast with Undo is up, Ctrl+Z undoes it | local |
| `Ctrl+Alt+I` | global | the capture window | no |

Every action has a menu item and a palette entry under one id (`notes.open`, `notes.today`, `notes.capture`, `notes.make-issue`, `notes.history`, `notes.accept`, `notes.decline`, `notes.restore`). The `?` sheet lists the keys of the screen it is on.

**Focus contract.**

1. Opening a note puts focus in the body at the saved caret. Esc returns to the list row that opened it.
2. After a capture, the window closes and focus goes back to the app that had it.
3. The `[[` popup takes the arrows, Enter and Esc; Esc closes the popup, not the editor.
4. After Make issue, focus is on the new chip. After Accept or Decline, focus moves to the next version row.
5. A conflict view puts focus on "Keep mine".
6. Esc closes the topmost layer only: popup, make-issue bar, History, then the editor.
7. Bare letters fire only on the list and in History; in the editor and in any field they type.
8. Chip state changes and "Saved" are announced through one polite live region. Each outcome is also in a focusable element.

**Names.** A chip reads "pull request 3104, merged, retry upload". A not-live chip adds "as of 14:02". A list row reads "daily note, Thursday 12 November, 3 links, edited 27 minutes ago". A proposal row reads "proposed edit by claude-code, not in the file yet". Reduced motion turns toasts and the fold's open and close into cuts.

## 5. Visual bar

Frames are design's round 3 at `4e37789f29`. The type scale, density (32 px rows), and the Inspora bar come from the shared grammar (#2978). Text is 4.5:1 or more in both themes; non-text marks (the checkbox border `#70757c`, chip edges, glyphs) are 3:1 or more. A state is a glyph and a word, never color alone. The new page glyph has a dark and a light version.

## 6. Errors, in words

### 6.1 One table, one test per row

Each failure maps to `{code, sentence, fix}`. The mapping lives in one catalog file in Core so the app, the CLI and MCP print the same words. The code shows under Details.

| Code | Sentence | Button / command |
|---|---|---|
| `SND-NOTE-001` | "Sindri isn't running, so your line is kept here." (capture) / "Sindri isn't running. Chips and the brief can't update." (page) | Start Sindri / `sindri serve` |
| `SND-NOTE-002` | "No notes folder yet." (capture, today) / "{key} is not a vault of your profile." | Choose notes folder / `sindri profile set notes.vault <name>` |
| `SND-NOTE-003` | "Couldn't write to today's note. Your line is kept." | Check the folder is writable, then Enter / none |
| `SND-NOTE-004` | "{file} changed on disk after you opened it." (conflict) | Keep mine / `sindri note keep <path> --mine` or `--theirs` |
| `SND-NOTE-005` | "Couldn't save {file}. Your text is kept." | Retry / `sindri note show <path>` |
| `SND-NOTE-006` | "{file} is a symlink or has more than one hard link, so Sindri won't write it." | Open on disk / none |
| `SND-NOTE-007` | "{file} is inside a folder your deny list hides." | Change the deny list / none |
| `SND-NOTE-008` | "A note called {title} already exists here." (create, rename) | Open it / none |
| `SND-NOTE-009` | "Undo is off: the note changed after the capture." | none |
| `SND-NOTE-010` | "The note changed after {agent} wrote this. Decline it and ask the agent again." | Decline / `sindri note decline <id>` |
| `SND-NOTE-011` | "Agents can't edit {folder}." (a proposal into a folder with rule "can't edit") | Change rule / none |
| `SND-NOTE-012` | "Couldn't read this note's history. Check the notes folder is reachable." | Retry / `sindri note history <path>` |
| `SND-NOTE-013` | "No earlier versions." (state, not an error; listed so the catalog test covers the sentence) | none |
| `SND-NOTE-014` | "Linear isn't connected. Connect it in Settings, then try again." (make issue) | Open Settings / `sindri connection add <label> --preset linear` |
| `SND-NOTE-015` | "Couldn't create the issue. The line is unchanged." | Retry / none |
| `SND-NOTE-016` | "{repo} isn't a repo of your profile, so Sindri can't show this link's state." (unknown chip) | Add the repo in Settings / `sindri repo add <path>` |
| `SND-NOTE-017` | "The OS refused this hotkey." | Choose another shortcut / none (shared with #3170) |
| `SND-NOTE-018` | "A write names an old version of this note." (409) | Reload / `sindri note show <path>` |
| `SND-NOTE-019` | "Nothing to save." (empty capture) | none |
| `SND-NOTE-020` | "Sindri or the tracker is down. Nothing was sent." (Make issue offline) | Start Sindri / `sindri serve` |
| `SND-NOTE-021` | "The note is over 1 MiB, so Sindri shows it as text only." | Open on disk / none |
| `SND-NOTE-022` | "Attachment {name} is over 25 MB." | none |

022 is the last reserved in v1; 023 to 029 stay free. One catalog test per row asserts the sentence and the fix; the frames' placeholder codes (`NOTES-WRITE-001`, `NOTES-HISTORY-001`, `LINEAR-CREATE-502`) change to these in the frames' Details line. The 1 MiB and 25 MB limits are written to the lead's pick (Open question 7).

## 7. Agents

Everything the page does, an agent can do through one verb. `sindri note <verb>` on the CLI, `note_<verb>` over MCP at `/sindri/mcp`.

| Verb | Arguments | Does | Refused with |
|---|---|---|---|
| `list` | `[--folder F] [--kind K] [--tag T] [--about #123] [--max N] [--json]` | note rows from the index | 002 |
| `show` | `<path> [--json]` | the note's text, links, backlinks, sha256 | 002, 007 |
| `search` | `<query> [--max N] [--json]` | the index, with matching lines | 002 |
| `add` | `<text> [--stdin]` | the capture append (owner or session) | 001, 002, 003 |
| `propose` | `<path> --base <sha256> --diff F` (or `--append T`) | writes a proposal, not the file | 002, 007, 010, 011, 018 |
| `accept`, `decline`, `restore`, `keep` | `<id|path> ...` | owner actions | SND-AUTH-002 for a session |
| `history` | `<path> [--json]` | versions, who, counts | 012 |
| `make-issue` | `<path> --line N --target T` | the owner's Make issue | SND-AUTH-002 for a session |

Rules:
- A session reads a folder only when its retrieval switch is on. A denied path is hidden from every read, as it is from code. Note text in every result is fenced as untrusted, over MCP and in `--json`.
- A session's `add` and `propose` are the only writes. `add` is allowed only in a folder whose rule is not "Agents can't edit"; it still creates a proposal, not a direct append, in v1 (decision 19).
- A session never presses the owner's buttons: `accept`, `decline`, `restore`, `keep` and `make-issue` are refused with `SND-AUTH-002`.
- `--json` carries keys, text carries words. Exit codes follow the context family: `0` ok, `1` attention (a refusal in the last column, Sindri not running), `2` a tool or usage error.
- The context lane never writes a note. It reads `notes.vault` through the `notes` source and cites chunks as `note:<path>#<heading>`.

## 8. API and engine requirements

Routes under `/sindri/notes`, named in P0.

| Route | Does |
|---|---|
| `GET /sindri/notes?kind=&folder=&q=&about=&after=` | rows, counts per tab, `degraded[]` |
| `GET /sindri/notes/{path}` | text, sha256, links, backlinks (linked, unlinked), linked objects with chip state |
| `POST /sindri/notes/{path}/ops` | `{intent, baseVersion, ...}`: edit, tick, link-mention, restore, accept; a stale base is `SND-NOTE-018` |
| `POST /sindri/notes/capture` | the append; creates today's file; returns the line's id for Undo |
| `POST /sindri/notes/{path}/propose`, `.../decline` | proposals |
| `GET /sindri/notes/{path}/history` | versions; `GET .../history/{id}` the diff against the file now |
| `POST /sindri/notes/{path}/make-issue` | `{line, target}`; a Linear or GitHub op on the same write path |
| `GET /sindri/notes/chips?ids=` | live chip state from the local store (#3021's lookup) |

| # | Need | Owner lane | Notes |
|---|---|---|---|
| E1 | Profile keys `notes.vault`, `notes.folder`, `notes.daily`, `notes.template`, `notes.attachments`, `notes.rules.<folder>` (`propose` or `none`); validation reuses the vault checks; `SND-NOTE-002` | api | Settings writes the same keys |
| E2 | Index into `rows.db`: one row per `.md`, text chunked by heading, FTS5; start walk (mtime + size first, hash on change); watcher (inotify, FSEvents) with a rescan every N minutes; burst of events is one reindex per file after a quiet time | api | the 1,000-note spike posts its number on #2981 before the watcher design is fixed |
| E3 | The atomic write op, conflict row, `.trash` delete, byte-clean round trip test on the fixture vault | api | N1; builds on `ITodoNoteDisk` and `ITodoNoteFiles` |
| E4 | Daily note and template; capture append; `sindri note add` | api | #3034 and #3035 ship together (api plan) |
| E5 | Link index (`links`, backlinks, unlinked mentions); `[[` popup source over the index plus context's edges; code ids from the context map | api, context | #3036 |
| E6 | Search route and the Ctrl+K `note:` source | api, ui | #3037; reads the index, never the disk |
| E7 | Chip lookup and `about` edges, the reverse walk for object pages; "Create issue" op | api, context | #3038; needs #3021's lookup |
| E8 | Versions in `$AW_SINDRI_HOME/notes-history/`, proposals with a base hash, the per-folder rule | api | #3039 |
| E9 | Context's `notes` source: `context.sources.notes` reads `notes.vault`; `retrieval` per folder, default off | context | accepted on #2981; first PR is the source on the library feed's walker |
| E10 | Events `note.captured`, `note.saved`, `note.proposed`, `note.accepted`, `note.declined`, `note.restored`, `note.issue.made` join the catalog (row, not text, in the payload; no note text in the ledger) | api | the index is a view, so no `ledger schema` line moves; views migration takes the next free number |
| E11 | Failure-to-sentence catalog (§6) in Core | api | one test per row |
| E12 | Global hotkey on the tray; the shared window's Note kind; the Wayland portal path | ui | depends on #3170's window |
| E13 | `sindri doctor` row `notes` (last index, last error, vault path, count) and the `src/Sindri/aot-smoke/expected.txt` line | api, tools | any doctor change updates the expected file |
| E14 | A session filter: `GET /sindri/ledger` and `/sindri/events` leave `note.*` payloads out for a session; every `/sindri/notes` write needs the per-start human token (`SND-AUTH-002` for a session) | api | as Home §8 |

## 9. Goals and non-goals

Goals:

- The owner's existing notes folder opens unchanged; edits round-trip byte-clean (N1).
- A capture is on disk in under 2 s and never loses a line (N2, N3).
- Every issue, PR and run a note links shows its notes (N7).
- Agents read by rule and write only as a diff the owner accepts (N8).

Non-goals (v1):

- Rename of a note with link updates in other files. Rename is by file manager; Obsidian's setting does it today. Later.
- Tasks view across all notes, saved searches and the property table (ledger 7, 13). Only the carried-tasks block on today's page.
- Templates beyond the daily one (ledger 8). The `{{pr}}`, `{{issue}}`, `{{scope}}` variables follow after the soak.
- Sync, sharing, a cloud store. The files are the owner's; any sync tool works.
- Widening the todo note sync from one note to every task line (ledger open question 1: not before the single-note sync has run a month).
- A Markdown renderer beyond what the editor shows; plugins; a graph view.
- Git history as the version store (decision 22).

## 10. What happens to today's views

| Today | After |
|---|---|
| Project `notes` string (`ProjectLedNoted`) | stays; a project's page also lists the notes that link it |
| Todo note sync (`todo.obsidian`) | stays as is; the todo note is one more note in the list, with its sync-managed lines left alone |
| Ctrl+K palette | gains the `note:` scope (E6) |
| Design-doc library (`aw-doc: 1` files) | stays; a library doc in the notes vault shows in both, read-only in the library |
| Home's "Needs you" rows | feed the fold (§4.2) |
| `context.sources.library.vaults` | a vault named by `notes.vault` is also a notes root |

## 11. Gaps found

1. **The capture window does not exist.** Notes builds the Note kind on #3170's window. If #3170 lags, the Note kind ships first behind the tray item and the Notes page's own "Capture" button (a one-line sheet), then moves to the shared window.
2. **The brief fold depends on Home.** Until `GET /sindri/home` ships, the fold shows "Needs you" from the NeedsYou rows and hides "Moved since yesterday".
3. **Chip state depends on #3021's lookup** for issues and PRs. Runs and scopes need their own lookup.
4. **The watcher drops events.** The rescan is the safety net; the spike measures both.
5. **A vault can hold other tools' files.** Obsidian keeps `.obsidian/` and plugins' folders. The built-in dot-folder deny covers them. Obsidian's trash setting decides where `.trash` is.
6. **The ledger's hard delete and rename are not in v1** (decision 4, §9).
7. **Home's run-failure rows** need the "seen" flag from the same store (Home §8). Notes adds no second flag.
8. **Undo on a capture needs the line's position.** The op returns the line's id and the base sha256; Undo is off after any other change to that file (decision 8).

## 12. Proof

Proof is the app run on seeded data with `aw-ui-driver`, not tests alone.

1. **Byte-clean round trip** on the fixture vault (N1), run in CI.
2. **Capture walk** from a fresh `context-first-run.sh` home with a made-up vault: hotkey, plain line, task line, empty Enter, each failure of §6, Undo. Before and after shots.
3. **Today walk:** first open, folded, next day, previous day, a carried task ticked (one-character diff).
4. **Link walk:** `[[` popup in each group, no match, Link it, the PR page listing the note.
5. **Chip walk:** merge a PR in the forge fixture (`scripts/forge-fixture.sh`) and time the chip (N6); stop Sindri and check the not-live state, the dimmed glyph, the tooltip.
6. **History walk:** an agent proposal over MCP, stale proposal, accept, restore.
7. **Index timing** on a 1,000-note vault (N5), logged in the perf inventory.
8. **Screen reader:** `aw-ui-driver tree` (N10).
9. **Adversarial design review** by the ideas lane on the shots (owner rule, 2026-11-10), judged against inspora.design.

Perf: N2, N4, N5 measured in the real app by the perf lane. The vault in every fixture uses made-up names ("Harbor ferry"); no work app's names.

## 13. Phases and parallel plan (for /specToProvenPR)

Phases are the PRs. Every lane has work from day one; the red and stubs of each phase land first, never behind another phase's PR.

| Phase | What | Lane | Depends on |
|---|---|---|---|
| P0 | Contract PR: routes' request and response records, `NoteRow`, the error catalog stubs, the events, profile keys with validation, golden and `PublicAPI` entries, `tests/fixtures/notes/` (the made-up vault), all as stubs | api | none |
| P1 | Store: `notes.vault` validation, the index, the watcher, read-only; the 1,000-note spike (E1, E2) | api | P0 |
| P1c | Context's `notes` source on the library walker; `retrieval` per folder (E9) | context | none (start now) |
| P2 | Write path: atomic op, conflict row, `.trash`, byte-clean test (E3); capture append and daily note with template (E4); `sindri note add` | api | P1 |
| P2ui | List, today's page, editor shell, Settings frame, states (§4.2, §4.3, §4.11, §4.12) | ui | P0 stubs; X1 for the visual pass |
| P3 | Capture: the Note kind in the shared window, hotkey, tray item, Wayland portal, the failure states (E12) | ui | #3170's window (red and stubs first) |
| P4 | Links: link index, backlinks, unlinked mentions, `[[` popup, right pane (E5) | api, ui | P1, P2ui |
| P5 | Search route and the `note:` palette scope (E6) | api, ui | P1 |
| P6 | Chips and objects: chip lookup, the `about` reverse walk, notes on PR, issue and project pages, Make issue (E7) | api, ui, context | P4; #3021 |
| P7 | History: versions, proposals, per-folder rule, History view, agent verbs (E8, §7) | api, ui | P2 |
| P8 | The fold on today's page from Home's rows; "Moved since yesterday" once `GET /sindri/home` ships | ui | Home's S0 route; P2ui |
| X1 | Design: the visual pass for §4.1 to §4.12 in the Inspora bar; update #3170's frames to the "New issue" / "New note" title rule | design | none |
| X2 | UX: the walks (§12) at each phase's end; N9 week | ux | each phase |
| X3 | Perf: N2, N4, N5 in the real app; the list at 10,000 notes | perf | P1, P2ui |
| X4 | QA: judge each phase against the bar, the shots and N1 to N10 | qa | each phase |
| X5 | Tools: aot-smoke lines, `.env.example`, docs (`docs/csharp/sindri-notes.md`, `docs/sindri/profile.md` keys), scaffolds | tools | each phase |

Day one: api P0; context P1c; design X1; ux X2 baseline; ui P2ui on the P0 stubs; ui P3's red against #3170's stubs. Nothing waits on a dependency PR to start its red.

Gate notes: a PR that adds routes runs the full Sindri integration lane and `EndpointCqrsTests`. A PR that adds a golden or catalog row adds its scenario row and the Ui `GoldenContract` slug, and runs `Aw.Sindri.Ui.Tests` unit. A PR that adds events runs `EventCatalogTests`. A new views migration takes the next free number and runs the Ledger namespace tests. A doctor change updates `src/Sindri/aot-smoke/expected.txt` and runs aot-smoke. A new `using Aw.` or sub-namespace runs `Aw.Architecture.Tests`. A new view runs `Aw.Sindri.Ui.Frames.Tests` integration. New env vars go in `.env.example`. A new project reference runs the locks and `sindri-build-inputs.sh` checks. Note text, titles and chip text are scrubbed by `Aw.Redaction` before they enter the ledger or a log line.

## Open questions

Eight are written to the lead's pick; none blocks P0.

1. **Where the versions live** (decision 22): `$AW_SINDRI_HOME/notes-history/`, a copy per save. Written to the lead's pick. The alternative is the vault's git history when it is a repo. Choose the copy first because it works for every vault.
2. **Agent append to a named note** (ledger 12 allows it as a direct write; decision 19 does not). Written to the lead's pick: v1 treats every agent write as a proposal, because "never edits silently" is the owner's rule. A run-log note that needs a direct append can get a third rule value, "Agents can append", after the soak.
3. **What "Moved since yesterday" reads** (§4.2): Home's Moving rows whose `since` is after local midnight of the previous day. Written to the lead's pick.
4. **Capture with context** (§4.1): "Note about this" actions on Reviews, Home and scope pages open the editor on a new pre-linked note; the global window stays one line. Written to the lead's pick.
5. **Default Make-issue target** (§4.8): GitHub in the linked PR's repo when exactly one PR is linked, else the profile's tracker, as ideas' check on #3038 proposed. Written to the lead's pick.
6. **"Make all" on an `## Actions` section** (§4.8): in v1, with one confirm that lists the titles. Written to the lead's pick.
7. **Size limits** (§6): a note over 1 MiB opens as text only, an attachment over 25 MB is refused. Written to the lead's pick.
8. **Where the daily folder is read from** (§4.2): the profile's `notes.daily`, defaulting to `daily/`, and Obsidian's own `daily-notes.json` read once when present. Written to the lead's pick.
