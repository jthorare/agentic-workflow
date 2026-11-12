# Sindri's Notes page: write it down once, see it next to the work

Status: draft (ideas lane, 2026-11-12). Serves #2981 (Notes) and #2978 (one app that replaces the others). Builds the rows of the Notes prior-art ledger (`docs/sindri/prior-art/notes.md`, merged with #2989's rule) and the issues #3034 to #3039.
Frames: design's round 3b at `pr-assets/notes-2981` @ `866ccbb667` (`guesses.md` beside them). Frame names below are files in that folder. §5 lists the frames this round changes.
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read). Round 1 is folded in (2026-11-12). The lead's calls are in §2.

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

The job: **write a thought down in under 2 seconds from anywhere, find it again, and see it next to the issue or PR it is about, in files another app can open unchanged.** (Threads, runs and scopes join in v1.1.)

## 1. The bar

"Ledger N" is problem N in `docs/sindri/prior-art/notes.md`. "Take N" is item N of its closing list "What we take". "v1.1" means after the N9 week; §2 lists what ships first and what waits.

| Job | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Capture in under 2 s | 1 | Obsidian `daily:append`, Raycast Append to Daily Note | one global window, Enter appends `- 14:32 text` under Captured; `[ ]` makes a task; the line is never lost | §4.1 (Take 1, #3034) |
| Today's page | 2 | Obsidian Daily notes, Foam | opens on today, created from a template; previous / next existing day; open tasks from earlier days; our brief fold | §4.2 (Take 2, #3035) |
| Link while typing | 3 | Obsidian `[[`, Bear | `[[` popup: notes first, then issues and pull requests (threads, runs, scopes and files in v1.1); Obsidian syntax in the file | §4.4 (Take 3, #3036) |
| Backlinks and unlinked mentions | 4 | Obsidian Backlinks | right pane: Linked objects, Linked mentions, Unlinked mentions; "Link it" | §4.5 (Take 3, #3036) |
| Jump by half a name | 5 | Obsidian Quick switcher | `note:` scope in Ctrl+K, title match before text match, newest first | §4.6 (Take 4, #3037) |
| Search with filters | 6 | Obsidian operators, zk FTS | the same box and the `/` filter read the SQLite index; operators `tag:`, `path:`, `about:` | §4.6 |
| Open tasks across notes | 7 | Obsidian Tasks | only the carried-tasks block on today's page in v1 | §4.2, §10 |
| Templates | 8 | Obsidian Templates | the daily template only in v1; other templates after the soak | §4.2, §10 |
| Live state of linked work | 9 | Notion link previews | a chip with live state; nothing cached in the file | §4.7 (Take 5, #3038) |
| Line to issue | 10 | Linear create from selection | Ctrl+Shift+I on one line (v1); make-all in v1.1 | §4.8 (Take 5, #3038) |
| History and undo | 11 | Obsidian File recovery | a snapshot at most every 60 s of editing; restore an older version as a new one | §4.9 (Take 6, #3039) |
| Agent writes by rule | 12 | Basic Memory | v1: agents read by rule and write nothing. v1.1: an agent edit is a proposed diff the owner accepts | §4.9, §7 |
| Notes as a table | 13 | Obsidian Bases | later | §10 |
| Design-doc items vs PRs | 14 | none | stays as is (Take 7) | n/a |

**The bar in one line:** the owner's existing notes folder opens unchanged, an edit round-trips byte-clean, a capture is on disk under 2 s after the hotkey, and every issue and PR a note links shows that note on its own page.

Done-checks. Each is a test or a measured run in the real app (`aw-ui-driver`). Each names its lane in §12.

| # | Check | Measured as | Runs as |
|---|---|---|---|
| N1 | **Byte-clean.** A fixture vault (made-up notes, mixed line endings, no trailing newline, front matter, a symlink, a hard-linked file). Open each, change one character, save. | `git diff --stat` on the vault shows the one changed line per file and nothing else. The symlink is not followed and the hard-linked file is read-only. | `Aw.Sindri.Tests.Notes.ByteCleanRoundTripTests` (integration) on a vault that `tests/fixtures/notes/make-vault.sh` builds at test time. Only the plain notes are checked in, because git does not keep links on every OS |
| N2 | **Capture speed.** Hotkey to a painted window, then Enter to the line on disk. | The window paints under 100 ms after the hotkey with the main window closed (window pre-created, hidden; ledger 1). Enter to fsync done under 300 ms. A scripted walk (hotkey, a 10-word line, Enter) ends in under 2 s. The hotkey works after a reboot once the owner clicked "Start Sindri at login" (§4.12). | `scripts/notes-proof.sh capture`, through `sindri-ui-fixture.sh` and `aw-ui-driver`; run by hand by the perf lane |
| N3 | **Capture never loses a line.** Folder removed, write refused, Linear down; Sindri down. | Linear down: the line stays and Issue is off. Folder removed or write refused: the window keeps the typed text and shows the sentence and the one fix of §6, and Enter saves it after the fix. Sindri down: the window appends to the file itself and shows "Saved without Sindri". A kill -9 of the window loses 0 characters (the draft is on disk, §4.1). A kill -9 of the editor loses at most the last 1.5 s of typing. | `Aw.Sindri.Tests.Notes.CaptureNeverLosesLineTests` (integration), and the kill walk in `scripts/notes-proof.sh capture` |
| N4 | **Today in 100 ms.** Open Notes on a 1,000-note vault. | Today's text paints under 100 ms from a local read; the fold and chips fill in after and never block the text. | `scripts/notes-proof.sh today`, `make-vault.sh --notes 1000` |
| N5 | **Index speed.** 1,000-note vault. | One edited file reindexes under 100 ms (api's spike number; posted on #2981 before the watcher design is fixed). Cold start under a few seconds. Backlinks for the open note under 100 ms. | `Aw.Sindri.Tests.Notes.IndexSpikeTests` (`Lane=heavy`) |
| N6 | **Chips are live.** A linked PR merges. | The chip changes within 1 s of the ledger event. The file is byte-identical before and after. | `scripts/notes-proof.sh chips`, with `scripts/forge-fixture.sh` |
| N7 | **Every linked object shows its notes.** | The PR, issue and project pages list the notes that link them (the reverse walk of context's `about` edges). | `Aw.Sindri.Tests.Notes.ObjectPageNotesTests` (integration) |
| N8 | **Agents never write a note in v1.** | Every `POST /sindri/notes/*` route refuses a session token with `SND-AUTH-002`, and the files stay byte-identical. One test per route. (The proposal check, "the file is byte-identical until the owner accepts", arrives with proposals in v1.1.) | `Aw.Sindri.Tests.Notes.SessionWriteRouteTests` (integration) |
| N9 | **A week without the other app.** | ux uses Notes for the owner's real week with the other app closed and files every miss as an issue `Part of #2981`. | the ux lane, by hand |
| N10 | **Screen reader.** | `aw-ui-driver tree` names every row, chip, region and button; a chip reads "pull request 3104, merged"; a not-live chip adds "as of 14:02". | `scripts/notes-proof.sh tree` |

## 2. Decisions

Taken for the owner. Each says where it came from. "Written to the lead's pick" means the inputs left it open and the spec picked the simplest answer; each one is also in Open questions. A number with no source is an agent default (listed in Open questions).

1. **Notes are plain Markdown files in a library vault the owner names.** No second root. The profile gets `notes.vault` (a vault name) and `notes.folder` (an optional subfolder), as `todo.obsidian.vault` does. Path checks reuse the vault's deny globs and the `SND-LIB-001` root rules. A denied file is hidden from the index and from every read. "Choose notes folder" (§4.12) does two edits in one job: it adds a folder that is not yet a library vault to `context.sources.library.vaults`, then sets `notes.vault`. The owner needs no library vault first. (Meta, #2981 api plan, accepted 2026-11-12; lead call 7 below.)
2. **The files are the store; the index is a view.** One row per `.md` file in `$AW_SINDRI_HOME/notes-index.db`, its own SQLite file (WAL, FTS5), key `note:<vault>/<path>`. A walk of the files rebuilds it at any time. It is not in `views.db`, and `sindri views rebuild` never touches it. Only `note.*` events go to the ledger. Pins are the one table a rebuild keeps. Chips, the fold and the carried-tasks block are drawn by the app and never written into a file. (Ledger open question 2; api plan; lead call 4.)
3. **Every write is an atomic replace guarded by the file's hash, under a lock.** The editor opens a file at its sha256. A write takes a per-path lock inside Sindri, re-reads the file and compares the hash. A different hash follows the conflict rule of §4.10 and nothing is written. A matching hash writes a dot-named temp file in the same folder (`.<name>.sindri-tmp`, which sync tools skip), copies the original's mode, fsyncs and renames. After the rename Sindri reads the file again. If its hash is not the one written, an outside writer got in between: Sindri raises `SND-NOTE-004` and keeps the old bytes in History. The lock covers Sindri's own writes only, so the re-read is the net for Obsidian and sync tools. Bytes outside the edit stay as they were. (Api plan; N1; arch round 1.)
4. **Delete moves the note to the vault's `.trash`.** There is no hard delete in v1. `.trash` is on the built-in deny list, so the index never lists a trashed note. (Meta, #2981, 2026-11-12.)
5. **Capture rides the one global Ctrl+Alt+I window shared with Projects (#3170).** It always opens on the Note kind, titled "New note". A switch at the left (Ctrl+Tab) flips to "New issue". The Issue kind creates on Ctrl+Enter or its button "Create issue in Linear". Enter there only moves focus to that button. A bare Enter never writes to Linear or GitHub. The switch is hidden when no notes folder is set, and the window then makes issues only, by the same Ctrl+Enter. #3170's frames and spec follow this rule (X1). (Lead, 2026-11-12; autoplan round 1.)
6. **A refused hotkey uses #3170's Settings row.** On Wayland the hotkey goes through the desktop's global-shortcuts portal. A refused hotkey shows "The OS refused this hotkey." and one fix, "Pick another key" (the frame's words). The tray item "New issue or note" still works. (Lead, 2026-11-12; `notes-settings-hotkey-refused.png`.)
7. **A capture never loses the typed line.** The window writes the text to a draft file on every 250 ms pause: `$AW_SINDRI_HOME/notes-drafts/capture-draft.txt` (directory 0700, file 0600). It deletes the draft after a successful save and offers it back on the next open. If Sindri is down, the window appends to today's file itself (lead call 2). If the folder is not set or the write fails, the window stays open, keeps the text and shows one sentence and one fix (§6). If the folder is removed while the window is open, the window shows "No notes folder yet" and keeps the line. (Lead, 2026-11-12; `notes-capture-error-*.png`.)
8. **Capture appends under `## Captured` with a time.** A plain line is `- 14:32 text`. A line that starts with `[ ]` is a task, `- [ ] 14:32 text` (checkbox first, so Obsidian Tasks reads it). The window is one line: Shift+Enter does nothing, and a pasted line break becomes a space. Today's file is created from the daily template when missing. The toast says "Captured" and keeps Undo for 8 s; Undo removes exactly that line and turns off once the note changed. Enter on an empty line saves nothing, keeps the window open and says "Nothing to save." (Ledger 1; ideas' check on #3034; guesses 4.)
9. **Today's page opens with a "Needs you" fold (v1).** Up to 5 rows, drawn with Home's rows. "Moved since yesterday" joins in v1.1. The fold stays collapsed once you scroll past it, until the next day. The daily file has no heading: the page draws "Thursday 12 Nov 2026" from the file name, the list row says "Thu 12 Nov", the breadcrumb says `2026-11-12`. (Lead, 2026-11-12; `notes-today.png`, `notes-today-folded.png`.)
10. **Carried tasks are drawn, not stored.** The block "Open tasks from earlier days" comes from the index; it is not in today's file. A tick edits the original line in its own day's file (one character). (Ideas' check on #3035; guess 6.)
11. **Backlinks go in the right pane, as the frame shows it.** On a note: Linked objects (collapsed), then Linked mentions, then Unlinked mentions. On today's page: Linked objects (open) and one list named "Backlinks". §4.5 says what each holds. There is no PR-body backlink row: the index does not read PR bodies. (Lead, 2026-11-12; `notes-backlinks*.png`, `notes-today-conflict.png`.)
12. **Every issue and PR reference is a live chip, unless the cursor is on its line.** The cursor line shows the raw text (`[[acme/api#3104]]`). Plain blue text means a link Sindri cannot resolve. A chip shows a state glyph, the id, the short title and a state word; never color alone. The chip truncates a long title and never wraps, so the line keeps its height when the cursor leaves it. Chips are read-only in v1. (Lead; `notes-live-chips.png`, `notes-live-chips-legend.png`.)
13. **"Not live" dims only the glyph and the chip edge.** The text stays at normal ink (4.5:1 or more). The tooltip reads "as of 14:02". The right pane's rows dim their glyphs the same way. A chip for an object Sindri does not know says "unknown" with one fix, "Add the repo in Settings". (Lead; `notes-live-chips-notlive.png`, `notes-today-down.png`.)
14. **Make issue is Ctrl+Shift+I, one issue from one line (v1).** A bare `m` would type into the editor. Make-all waits for v1.1. (Lead; replaces the `m` in #3038's body.)
15. **History restore writes an older version as a new version and diffs it against the file now.** The current version reads "Current" and has no Restore. (Lead; `notes-history-restore.png`.)
16. **One Notes Settings frame:** the notes folder, "Start Sindri at login", the capture hotkey row shared with #3170, and the per-folder "Agents can read this folder" switch. The per-folder write-rule rows in the frame wait for v1.1. (Lead; `notes-settings.png`.)
17. **Checkbox border is `#70757c` everywhere** (3.4:1 on the capture window's `#222326`, more on the page's `#08090a`). (Lead.)
18. **A bare letter never writes to an outside source (GitHub, Linear).** Make issue, resolve and the like go through a button, a chord with Ctrl, or a menu. No typed confirmations. Offline never queues a write to an outside source: Make issue is off while Sindri or the tracker is down, and the line stays as written. Writes to the owner's own files are local and may use a chord. (Lead, team rules.)
19. **Agents write nothing to notes in v1.** An agent reads a folder only when its retrieval switch is on (default off). Every `POST /sindri/notes/*` route refuses a session (E14). In v1.1 an agent edit is a proposal the owner accepts, under a per-folder rule (§4.9, v1.1 part). (Meta, #2981; lead call 6; ledger 12 narrowed.)
20. **The queue of notes is a list on the row / peek / full grammar.** `RowList`, `Row` and `PeekPane` on `RowKeys`, with no key handler of its own (#2978; Reviews spec decision 14). Space peeks, Enter opens the editor as the full view. `p` pins or unpins the row (local state in the index store's pins table, never in the file).
21. **Day chords wait for v1.1.** In v1 the day links on the page are clickable and reachable by Tab, and the palette has `notes.today`. In v1.1 the chords are Alt+PageUp (previous day) and Alt+PageDown (next day), because Alt+Left and Alt+Right jump a word on macOS and Ctrl+Alt+Arrow switches workspace on GNOME. Previous and next mean the previous and next EXISTING day (Foam's rule). From the list, `g p`, `g n` and `g d` (today) work; `g d` works anywhere except a text field. (Design, round 3; `guesses.md` 16.)
22. **Where history lives: `$AW_SINDRI_HOME/notes-history/`, never in the vault.** The policy is in §4.9: content-addressed copies, a 60 s snapshot floor, pruning, directory mode 0700 and file mode 0600, and the deny and retrieval checks on every read. (Ledger 11; lead call; api plan left git versus copy open.)
23. **Error codes are `SND-NOTE-001..022`.** The frames' `NOTES-WRITE-001`, `NOTES-HISTORY-001` and `LINEAR-CREATE-502` were placeholders (`guesses.md` 2). Reserved now; no `SND-NOTE-` code exists on main.
24. **Agents get the read verbs in v1.** Every action has a CLI verb. A session may run `list`, `show`, `search` and `history` (CLI and MCP) and never presses the owner's buttons (§7).
25. **The editor saves 1.5 s after the last key and on blur.** "Saving..." shows only when a write takes over 300 ms; a failed save shows "Not saved". On a failed save the buffer stays in the editor and Sindri also writes it to `$AW_SINDRI_HOME/notes-drafts/<sha256 of the path>.draft` (same modes). (Autoplan round 1; agent default.)

### Lead calls after autoplan round 1 (2026-11-12, lead)

1. **v1 scope.** v1 is P0, P1, P2, P2ui, P3, P4, P5 (search and the `note:` scope), read-only chips, History restore and a single Make issue (Ctrl+Shift+I, one issue from one line; the frames for it passed). Deferred to v1.1, after the N9 week: make-all, "Note about this", image paste, agent proposals and accept, day chords, "Moved since yesterday", and the thread, run, scope and code link forms. An editor-control spike (E15) is the first phase, owned by the ui lane. §13 re-cuts the phases with the red and stubs first and every lane busy from day one.
2. **Capture while Sindri is down.** The capture window appends to the daily file itself, with the same hash-guarded write, and shows "Saved without Sindri". The typed line is never lost. The index picks the line up on the next start.
3. **Autostart.** Setup offers "Start Sindri at login" as one click once a notes folder is set (§4.12). Nothing turns it on silently. N2 says "once the owner clicked it".
4. **Index store.** Its own file, `notes-index.db`, rebuilt from the files, not in `views.db`.
5. **Link grammar for v1.** `[[Note]]`, `[[owner/repo#N]]` and plain URLs. The thread, run, scope and code forms go to v1.1.
6. **Agent writes.** Proposals only, deferred with call 1, so v1 has no agent writes to notes. E14 names the exact routes a session may call (read routes only in v1), with one test per route. An issue gets filed for "Agents can append" later (Open question 2).
7. **Also decided.** Capture always opens on the Note kind (decision 5). The window is one line (decision 8). The conflict rule on the open daily page is in §4.10. The history policy is in §4.9. One write route family, `/sindri/notes/...`, synchronous, with no outbox (§8); there is no `/sindri/rows/{id}/ops`. The write mechanics are decision 3. Draft file locations are in decisions 7 and 25. The sentence for `SND-NOTE-015` and the frame wording for 003, 005 and 012 are in §6. The right pane follows the frame (decision 11).

## 3. The model

### 3.1 The note row

`NoteRow` in `Aw.Sindri.Core.Notes`. A view over a file; the file is the truth.

| Field | What it holds |
|---|---|
| `id` | `note:<vault>/<path>` |
| `path`, `size`, `mtime`, `sha256` | from disk; `sha256` is the version a write names |
| `title` | the first `#` heading; else the file name; for a daily note, the date from the file name |
| `kind` | `daily` (name is `YYYY-MM-DD.md` in the daily folder), `linked` (links at least one issue or PR), `note` |
| `headings[]`, `tags[]` | `#tag` and front-matter `tags` |
| `links[]` | `[[Note]]`, `[[owner/repo#123]]`, plain URLs (v1). `[[thread:..]]`, `[[run:..]]`, `[[scope:..]]`, `[[code:..]]` join in v1.1 |
| `firstLine` | the first body line that is not a heading |
| `waitsOn` | v1.1: `agent edit` (or "3 agent edits") while proposals wait, else empty. v1 does not draw the cell |

The index also stores the text, chunked by heading, for search (SQLite FTS5). A query reads the index and never walks the disk. A file with more than one hard link is indexed and read-only. A symlink is not followed (`SND-LIB-001`), so it is not indexed. A denied path is not indexed.

### 3.2 Link forms written into the file

Obsidian-compatible, so an existing vault opens unchanged (`notes-link-written.png`). The grammar lives in the owner's files for good, so v1 writes only forms Sindri can resolve today.

| Kind | Written | Note |
|---|---|---|
| Note | `[[Note Title]]` or `[[Note#Heading]]` | exactly as Obsidian writes it. When two notes share a title, Sindri writes `[[folder/Title]]`, and the index resolves an exact path first |
| Issue or PR | `[[owner/repo#3112]]` | the picker knows the repo, so the file always names it |
| Plain URL | `https://github.com/owner/repo/pull/3112` | the index reads an issue or PR URL as the same link as `[[owner/repo#3112]]` |

A bare `[[#3112]]` found in a file (typed by hand, or from an older tool) is read against the profile's first repo, and the chip says "repo guessed: acme/api". Sindri never writes that form. In Obsidian, `[[owner/repo#3112]]` shows as an unresolved link, and a click offers to make a note called `owner/repo`; the plain URL is the form that never makes a file. The thread, run, scope and code forms (`[[thread:..]]`, `[[run:..]]`, `[[scope:..]]`, `[[code:..]]`) wait for v1.1, when the context lane fixes their ids (`guesses.md` 1).

### 3.3 Links as edges

Context's plan (accepted on #2981): `[[Note]]` is `links note:`; `[[owner/repo#123]]` or a URL is `about item:`. "Every linked object shows its notes" is a reverse walk of those edges. Code and scope edges (`path:42`, a scope id) join in v1.1. The notes index and context's `notes` source read the same files and the same `notes.vault`.

### 3.4 Write intents

| Intent | What changes | Bytes touched |
|---|---|---|
| Edit | the open note, by the owner | the owner's edit |
| Create | a new note | the new file |
| Delete | the note moves to `.trash` | none; the file moves |
| Append (capture) | a line under `## Captured` in today's file | one line, plus the heading and the file when missing |
| Undo capture | removes the line the capture added | that line |
| Tick | one task line, in its own day's file | one character |
| Link a mention | wraps one mention in `[[ ]]` in the other note | the brackets |
| Make issue | replaces one line with a link | that line |
| Restore | the whole file becomes an older version | the file, as a new version |

v1.1 adds Accept a proposal (applies the agent's diff, or re-applies its append).

Each is one synchronous call under `/sindri/notes/...` (§8): lock, hash check, temp file, rename, re-read, then a `note.*` event. There is no outbox and no `/sindri/rows/{id}/ops`. An Append names no base: it re-reads the file under the lock and adds the line, so a capture never conflicts with an earlier capture. Every other intent that changes existing text names `baseSha256`, the sha256 the caller read. A file changed on disk since the editor opened it follows the conflict rule (§4.10).

## 4. The page

Layout: the top bar, then the list (§4.3) with a peek, and the editor as the full view with the right pane (§4.5). The page opens on today (§4.2). The palette entry `notes.today` opens today's page from anywhere; `g d` joins it in v1.1.

### 4.1 Capture

One window, shared with Projects (#3170). It is a frameless one-line window over the desktop that opens even with the main window closed. The owner clicks "Start Sindri at login" once (§4.12) so a process holds the hotkey after a reboot.

```
┌ New note ──────────────────────────────────────────────────────┐
│ [Issue | Note]  [ ] ☐ call Priya about the retry cap           │
│ Adds to 2026-11-12 · Captured        - [ ] 09:12 call Priya... │
│ Enter save · Esc close · Ctrl+Tab Issue / Note                 │
└────────────────────────────────────────────────────────────────┘
```

- **Open.** The hotkey (Ctrl+Alt+I, changeable in Settings) or the tray item "New issue or note". The window always opens on the Note kind. Title: "New note".
- **Note kind.** A preview line shows the exact text that will be appended. A leading `[ ]` draws an empty task box (border `#70757c`) and the line "starts with [ ]: saved as a task". When today's note does not exist the chip says "Creates 2026-11-12 from your daily template, under Captured" (`notes-capture-bar.png`, `notes-capture-bar-plain.png`).
- **Issue kind.** The Projects window: Project and Assignee chips, "in Linear · EXP", the primary button "Create issue in Linear", footer Ctrl+Enter create, Esc close, Tab change project, Ctrl+Tab Issue / Note (`notes-capture-bar-issue.png`, which design reshoots for this footer). Enter with text only moves focus to the button. Its errors reuse the make-issue sentences of §6 ("Linear isn't connected", "Couldn't create the issue").
- **Enter (Note kind).** Appends under `## Captured` and closes. A toast "Captured" with Open note and Undo (8 s) shows (`notes-capture-saved.png`). Undo removes exactly that line and turns off once the note changed (the toast says why).
- **Draft.** On every 250 ms pause the window writes the text to `$AW_SINDRI_HOME/notes-drafts/capture-draft.txt` (directory 0700, file 0600). A successful save deletes it. The next open offers it back: "Restored your last unsaved line".
- **Sindri down.** The window appends to today's file itself: the same hash-guarded atomic write (decision 3), from `Aw.Sindri.Core.Notes` code that needs no serve call. The toast reads "Saved without Sindri" and the index picks the line up on the next start. When today's file is missing, the window creates it from the daily template (a file read) or empty. Design shoots the toast; it replaces `notes-capture-error-down.png`.
- **Nothing to save.** Enter on an empty line saves nothing and the window says "Nothing to save. Type a line, or press Esc to close." (`notes-capture-empty.png`).
- **No folder.** The switch is hidden and the window only makes issues (`notes-capture-no-folder-no-switch.png`).
- **Failures** keep the line (§6): the folder removed while open, `notes-capture-error-folder.png`; a refused write, `notes-capture-error-write.png`.
- **CLI twin.** `sindri note add "text"` (stdin accepted) takes the same append path. With Sindri down it fails with `SND-NOTE-001` and prints the text back; it queues nothing. Only the owner runs it; a session is refused (E14).
- **Pre-filled capture** ("Note about this" on the Reviews page, a Home row or a context scope) is v1.1 (Open question 4). The global window stays one line.

### 4.2 Today's page

Opening Notes lands on today's page. If the file is missing, Sindri creates it from the daily template and says "Created from your daily template · 09:02" (`notes-today-new.png`).

- **File.** `YYYY-MM-DD.md` in `notes.daily` (profile key, default `daily/`; an Obsidian vault's own daily folder is read once from `.obsidian/daily-notes.json` when present, read-only). The template is a plain `.md` file the owner names (`notes.template`, default none = an empty file). Variables: `{{title}}`, `{{date:FORMAT}}`, `{{time}}` as Obsidian spells them. The file has no heading.
- **Top of the page, outside the file.** In this order: the fold, then "Open tasks from earlier days" (a quiet label says "Drawn from the other days. Not in this file."), then the file's text with its own sections (Today, Captured).
- **The fold.** "Needs you" (up to 5 rows) in Home's row grammar, read from the NeedsYou rows main has (`Ui/ViewModels/NeedsYou/`). Once `GET /sindri/home` ships, the fold reads that route. In v1.1 it gains "Moved since yesterday" (up to 5 rows, from the Moving group's rows whose `since` is after local midnight of the previous day). It stays collapsed once you scroll past it, until the next day (`notes-today.png`, `notes-today-folded.png`).
- **Day links.** "← Wed 11 Nov" (the previous existing day) and "Next day: none yet" (plain text, not a link). In v1 they are clickable and reachable by Tab. The chords of decision 21 join in v1.1. A non-daily note shows no day row.
- **Saved.** A quiet "Saved", "Saving..." or "Not saved" sits next to the path (decision 25).
- **Header line.** "Chips are live: updated 4 s ago" (`notes-today.png`).

### 4.3 The list

Tabs: All, Daily, Linked to work. The glyph is one page glyph (blue outline for a daily note; a dark and a light version). Rows are 32 px, as on Home.

`[glyph] [title, flex] [first line] [links] [folder, mono] [age]` (v1.1 adds `[waits on]` before the age)

- **Row.** Title, first line, link count, folder, age. A daily note's title is "Thu 12 Nov". A fact the source lacks is blank, never "-". In v1.1 the waits-on cell reads "agent edit", or "3 agent edits" for more than one.
- **Order.** Pinned, then newest edit first. Groups: Pinned, Today, This week, Older (counts add to the All tab; `notes-list.png` shows fixture numbers, not a rule). `p` pins or unpins the selected row.
- **Peek (`Space`).** The body (links as chips; the one-line preview in each row stays plain text), Edited, Links "4 out · 3 in", Work (states of linked PRs and issues), File. v1.1 adds Proposal. Actions: Open, Copy path, Copy link (`y`).
- **Filter (`/`).** Searches the index: title, headings, tags, text. Operators `tag:`, `path:`, `about:owner/repo#123`, `kind:`. First result row under 100 ms at 1,000 notes.
- **Open.** Enter opens the editor. Ctrl+N opens a new note in the folder of the selected note (else the notes root), cursor in the body.

### 4.4 The editor and `[[`

The editor is the full view. The target is a Markdown editor with live rendering of headings, lists, tasks, tables, code and images. The control is not on main: E15 spikes AvaloniaEdit (MIT) with Markdig, with a NativeAOT check, before P2ui builds the live control. Until the spike says go, the editor is plain monospace text, chips show in the right pane and the read view, and the cursor-line rule of decision 12 does not apply. Esc leaves the editor to the list. In the editor a bare letter types. Image paste waits for v1.1.

The editor saves by decision 25. A toolbar above the body has three buttons, History, Make issue and Link it, each with its chord in the tooltip.

- **`[[` popup.** Groups in this order, one row each: Notes, Issues, Pull requests. Recent first, then fuzzy over title and alias. A row shows the glyph, the id and the state word (`notes-link-complete.png`). First rows under 50 ms per keystroke at 10,000 notes (in-memory title index). Enter inserts the link (§3.2). Threads, runs, scopes and files join in v1.1.
- **No match.** One row: "No note called "xyzq". Create it" (Enter creates the note and inserts the link; `notes-link-complete-empty.png`).
- **Still reading.** "Reading notes, results fill in" (`notes-link-complete-reading.png`).
- **Code links** (v1.1). A moved file reads "Moved from src/Export/. The link follows it. Nothing to fix." A gone file reads "File not found. Pick another target." with "Pick another target" (`notes-link-rename.png`).
- **Footer while the popup is open** shows only the popup's keys.

### 4.5 The right pane

The frame (`notes-backlinks.png`, `notes-today-conflict.png`) shows two shapes of one pane.

**On a note** (`notes-backlinks.png`), three sections in this order:

1. **Linked objects.** The issues and PRs this note links, as chips with live state. Collapsed by default. The count equals the rows listed. The label says "live from Sindri" while live.
2. **Linked mentions.** Notes that link this one, with the surrounding paragraph (1 line collapsed).
3. **Unlinked mentions.** Notes whose text holds this note's title without a link. The section says "title found, not linked". "Link it" (Ctrl+Shift+L) wraps that mention in `[[ ]]` in the other note; the row moves to Linked mentions and says "linked now" (`notes-backlinks-linked.png`). Only titles of 3 or more characters that are not a stop word are offered, at most 20 rows, then "N more".

A filter box (Ctrl+Shift+F) filters both mention lists; it is hidden when both are empty. Empty: "Nothing links here yet." (`notes-backlinks-empty.png`).

**On today's page** (`notes-today-conflict.png`), two sections. "Linked objects" is open and lists the issues and PRs the day's file links. "Backlinks" holds the notes that link this day, with their paragraph. A date has no unlinked mentions worth offering, so that section and the filter box are not drawn.

**Notes on the other pages.** The PR page, the issue row and the project page list the notes that link them, in the same row grammar (N7). The list is the reverse walk of §3.3.

### 4.6 Ctrl+K `note:` and search

Typing `note:` limits the palette to notes, ranked by title match first, then text match, newest first inside each. A text-only match carries a small "in text" tag. The empty `note:` box lists recent notes with the previous note preselected for Down. A "Create note" row sits under an "Or create" header, shows the full path (the folder of the note you are in, else the notes root) and is hidden when a note with exactly that title exists. Enter opens; Ctrl+Enter creates (`notes-palette.png`, `notes-palette-empty.png`, `notes-palette-none.png`). The page footer behind the palette hides while it is open, as on Home (Home §7.4).

### 4.7 Chips

A chip is a view. The file keeps `[[acme/api#3104]]` or the URL; nothing about state is written into it (decision 2).

| Part | Rule |
|---|---|
| Anatomy | state glyph, id, short title, state word |
| Case | "In Progress", "Todo", "CI red", "merged", "closed", "open", "draft" (stated once so builders do not normalise it) |
| States | v1 draws 9 of the 11 in `notes-live-chips-legend.png`: open, draft, merged, closed without merge (own slashed-circle glyph, never the green check), CI running, CI red, In Progress, Todo, unknown. Run failed and run passed arrive with run links in v1.1 |
| Update | within 1 s of the ledger event; 0 network calls on open (ledger 9) |
| Click | the chip opens the peek of its object; Ctrl+Enter on a focused chip does the same from the keyboard. No other action in v1 |
| Cursor line | raw text, no chip |
| Not live | decision 13 |

Not live means Sindri is down, or the row's `readAt` is older than the source's stale limit (Home's stale-source rule, Home §5.5 and §7.3). The page header says "Not live: Sindri isn't running. States as of 14:02." and the top bar says "Not connected".

### 4.8 Make issue

Select a line (or put the cursor on it) and press Ctrl+Shift+I, or press the toolbar button. A bar under the line shows the target picker, the title (the line's text, in an editable field) and the note "replaces the line with its chip" (`notes-make-issue-picker.png`). One issue comes from one line. Make-all on an `## Actions` section is v1.1 (Open question 6).

- **Default target.** GitHub in the repo of the note's linked PR when the note links exactly one; otherwise the tracker the profile sets (Linear, team EXP in the frames). The bar shows the chosen target. Written to the lead's pick (Open question 5).
- **Title check.** `Aw.Redaction` scans the title. A hit shows "Looks like a token. Check the title." in the bar. It does not block; the owner decides.
- **Create.** One button, "Create issue" (Enter). It is a write to an outside source, so it is a button and an Enter on that button, never a bare letter.
- **Done.** The line is replaced by the new issue's chip, written `[[owner/repo#N]]` for GitHub, with Undo (Ctrl+Z, 8 s) (`notes-make-issue-done.png`). Undo restores the line; it does not delete the issue (the toast says "Issue stays in Linear"). The History row for the change links the issue.
- **Errors.** "Linear isn't connected. Connect it in Settings, then try again." (`notes-make-issue-noconn.png`). "Couldn't create the issue. The line stays as you wrote it. Check your connection, then try again." with the button "Try again" and the code under Details (`notes-make-issue-fail.png`). The line stays as written in both. With Sindri or the tracker down the button is off and nothing is queued (decision 18).

### 4.9 History

Ctrl+Shift+H (or the toolbar button) opens History from the editor; Esc returns. History is a full view with a 340 px version list on the left and the diff on the right.

- **List.** Time, what made it (edit, capture, tick, restore, Make issue, link) and +/- line counts, newest first. The header counts versions ("13 versions"). The newest saved version reads "Current" and has no Restore.
- **Restore.** Select an older version: the diff compares it with the file now, the header says "Restoring writes this version as a new version. Nothing is lost.", the one primary is "Restore this version" (`r`). (`notes-history-restore.png`).
- **Footer by selection:** an older version: `j/k`, `r`, Esc, Ctrl+K. Current, empty, error: Esc and Ctrl+K only.
- **States.** Loading "Reading versions" (`notes-history-loading.png`); empty "No earlier versions." (`notes-history-empty.png`); error "Couldn't read this note's history. Check the notes folder is reachable." with the button "Open notes folder" and `SND-NOTE-012` under Details (`notes-history-error.png`).

**What History keeps.**

1. **Content-addressed.** A version is a copy whose file name is the sha256 of its bytes, so the same bytes are stored once. Copies sit under `$AW_SINDRI_HOME/notes-history/<vault>/<hash of the note path>/`.
2. **A 60 s floor.** The editor's autosave makes at most one snapshot per 60 s of editing. Every other write (capture, tick, restore, Make issue, link) makes its own snapshot at once.
3. **Pruning.** Sindri keeps every version for 30 days, then one per day for the next 90 days, then drops the rest. A pass runs at start and once a day.
4. **Modes.** Directories are 0700 and files 0600. `sindri doctor` warns when one is looser.
5. **Reads re-check.** `history` and `show` apply the current deny and retrieval rules on every read, not the rules at save time. A note moved into a denied path drops its history from a session's reads. A deleted note keeps its history, read-only.

**Agent proposals (v1.1).** The rest of this section waits for v1.1.

- A proposal is a row "proposed edit, not in the file yet" with a band: "Proposed by claude-code, session 7f3a, at 14:21. It is not in the file yet. The file stays at the 14:05 version until you accept." and "Your rule for eng/: agents propose, you accept. Change rule" (a link to the Settings row). Accept (`a`, primary) writes it; Decline (`d`) drops it. There is no Undo: a proposal is not in the file, and Restore on the accepted row is the undo (`notes-history.png`). The band shows the full proposed text, never a truncated diff under 20 lines. The header reads "13 + 1 proposed". The list's "who" shows "claude-code, session 7f3a" as a link to the agent session.
- A proposal lives in `$AW_SINDRI_HOME/notes-proposals/<id>.json` (directory 0700, file 0600), outside the vault, so the index never reads it as a note. An edit proposal carries a base sha256. An append proposal stores the intent (`append: text`, `section`) and Accept applies it to the current file, so a capture does not make it stale. A proposal for a missing file is a create proposal.
- Stale edit proposal (the sha differs): Accept is off, Decline is primary, and the band says "The note changed after claude-code wrote this. Decline it and ask the agent again." (`notes-history-stale.png`).
- `propose` needs the folder's read switch on, since it needs `show`; otherwise `SND-NOTE-011`.
- Footer for a proposal: `j/k`, `a`, `d`, Esc, Ctrl+K. After Accept or Decline, focus moves to the next version row.

### 4.10 Conflict and save failure

- **File changed on disk** (Obsidian, git, a sync tool, or Sindri's own capture, tick or Link it) after the editor opened. The editor listens to the watcher and to `note.*` events and follows three rules:
  1. **Clean buffer.** Reload silently and keep the caret. Sindri's own capture never raises a conflict on the open daily page.
  2. **Dirty buffer.** Merge by line against the version the editor opened. A capture under `## Captured` merges cleanly.
  3. **The same lines differ.** Show both versions side by side with a word per differing line ("ticked in the file") (`notes-today-conflict.png`). "Keep mine" and "Keep the file's" are both secondary buttons, with no default. Focus goes to the first differing line. The version you did not keep goes to History. Design reshoots the frame without the primary "Keep mine".
- **Save failed:** "Couldn't save today's note. Your text is kept here. Check the folder is writable, then press Ctrl+S." with the button "Open notes folder" and the code under Details. The text stays in the editor and in its draft file (`notes-today-savefail.png`).
- **Sindri not running:** the file still opens as read-only text with the last index; chips and the fold are muted; one sentence and "Start Sindri" (`notes-today-down.png`).

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
| Note over 1 MiB (`SND-NOTE-021`) | a banner in the editor's banner slot (the style of the save-failed banner): "The note is over 1 MiB, so Sindri shows it as text only." No new frame |
| Link to a denied note (`SND-NOTE-007`) | the same banner slot on the page that holds the link, with the sentence of §6. No new frame |
| Proposal waiting (v1.1) | "agent edit" in the list row; a band at the top of the editor: "claude-code proposed an edit. Review" |
| Hotkey refused | the Settings row of §4.12 |
| Palette: reading, no folder | one line each (`notes-palette-reading.png`, `notes-palette-nofolder.png`) |

### 4.12 Settings

One frame, Settings -> Notes (`notes-settings.png`, `notes-settings-hotkey-refused.png`):

- **Notes folder.** Choose or change. One job with two steps: add the folder to `context.sources.library.vaults` (name from the folder; the `SND-LIB-001` checks) when it is not there yet, then write `notes.vault` and `notes.folder`. A refusal names the step that failed. A bad value is `SND-NOTE-002` with the key path. CLI twin: `sindri note setup <path> [--name N] [--folder SUB]`. `sindri setup` lists "Notes folder" as a step; `sindri setup --json` carries it.
- **Start Sindri at login.** One button, offered once a notes folder is set. It writes the OS's login entry for `sindri-ui` (Linux `~/.config/autostart/sindri-ui.desktop`, macOS a LaunchAgent in `~/Library/LaunchAgents`). Nothing turns it on silently. The row says "On" and offers "Turn off". The capture hotkey works after a reboot only while it is on or `sindri-ui` is running (N2).
- **Agents can read this folder.** Rows for eng/, daily/, people/, design/ and "Other folders". The switch sets context's `retrieval` for the folder (default off). The per-folder write-rule dropdown in the frame ("Agents propose, you accept", "Agents can't edit") waits for v1.1.
- **Capture hotkey.** Shared with #3170. A refused hotkey: "The OS refused this hotkey." and "Pick another key"; the tray item still works.

The profile keys apply on save; `sindri profile set notes.vault <name>` does the same from a terminal.

### 4.13 Keyboard and focus

Grammar keys are Home's: `j/k`, arrows, `Home`/`End`, `/`, `Space`, `Enter`, `Esc`, `.`, `?`, `Ctrl+K`, `Left`/`Right` to fold. A bare letter below changes local state or opens something cancellable. None writes to an outside source (decision 18).

| Key | Where | Does | Writes? |
|---|---|---|---|
| `Ctrl+N` | list | new note | creates a file on first save |
| `p` | list | pin or unpin the row | local |
| `Ctrl+K`, `note:` | anywhere | the palette scope | no |
| `[[` | editor | link popup | the link, when chosen |
| `Ctrl+Shift+L` | editor, pane | Link it (an unlinked mention) | the other note, one edit |
| `Ctrl+Shift+F` | editor | filter the mentions | no |
| `Ctrl+Enter` | on a chip | open the object's peek | no |
| `Ctrl+Shift+I` | editor | Make issue (opens the bar) | opens; Enter on Create writes |
| `Ctrl+Shift+H` | editor | History | no |
| `r` | History | restore, on an older version | the file |
| `y` | list peek | copy link | no |
| `Esc` | editor | leave the editor | no |
| `Ctrl+Z` | editor | text undo; while a toast with Undo is up, Ctrl+Z undoes it | local |
| `Ctrl+Alt+I` | global | the capture window | no |

v1.1 adds `g d` (open today, anywhere except a text field), `g p` / `g n` (list: previous / next existing day), `Alt+PageUp` / `Alt+PageDown` (editor: previous / next existing day) and, in History, `a` / `d` (accept / decline a proposal).

Every action has a menu item and a palette entry under one id (`notes.open`, `notes.today`, `notes.capture`, `notes.make-issue`, `notes.history`, `notes.restore`; v1.1 adds `notes.accept`, `notes.decline`). The editor toolbar has buttons for History, Make issue and Link it. The `?` sheet lists the keys of the screen it is on.

**Focus contract.**

1. Opening a note puts focus in the body at the saved caret. Esc returns to the list row that opened it.
2. After a capture, the window closes and focus goes back to the app that had it.
3. The `[[` popup takes the arrows, Enter and Esc; Esc closes the popup, not the editor.
4. After Make issue, focus is on the new chip. After Restore, focus is on the new Current row. (v1.1: after Accept or Decline, focus moves to the next version row.)
5. A conflict view puts focus on the first differing line. No button is the default.
6. Esc closes the topmost layer only: popup, make-issue bar, History, then the editor.
7. Bare letters fire only on the list and in History; in the editor and in any field they type.
8. Chip state changes and "Saved" are announced through one polite live region. Each outcome is also in a focusable element.

**Names.** A chip reads "pull request 3104, merged, retry upload". A not-live chip adds "as of 14:02". A list row reads "daily note, Thursday 12 November, 3 links, edited 27 minutes ago". (v1.1: a proposal row reads "proposed edit by claude-code, not in the file yet".) Reduced motion turns toasts and the fold's open and close into cuts.

## 5. Visual bar

Frames are design's round 3b at `866ccbb667`. The type scale, density (32 px rows), and the Inspora bar come from the shared grammar (#2978). Text is 4.5:1 or more in both themes; non-text marks (the checkbox border `#70757c`, chip edges, glyphs) are 3:1 or more. A state is a glyph and a word, never color alone. The new page glyph has a dark and a light version.

Frames this round changes (design lane, X1):

| Frame | Change |
|---|---|
| `notes-capture-bar-issue.png`, and #3170's Issue frames | primary "Create issue in Linear"; footer "Ctrl+Enter create" |
| `notes-capture-error-down.png` | replaced by the "Saved without Sindri" toast |
| `notes-today-conflict.png` | no primary button; a word per differing line |
| `notes-settings.png` | the "Start Sindri at login" row; the write-rule dropdown marked v1.1 |
| `notes-settings-hotkey-refused.png`, #3170's frame | one string, "Pick another key" |
| `notes-make-issue-done.png` | `[[owner/repo#N]]` in the chip's raw text |
| every frame with `[[#3112]]` | `[[acme/api#3112]]` |

## 6. Errors, in words

### 6.1 One table, one test per row

Each failure maps to `{code, sentence, fix}`. The mapping lives in one catalog file in Core so the app, the CLI and MCP print the same words. The code shows under Details. Where a frame at `866ccbb667` shows the words, the table uses the frame's words. "Kind" is `error` or `state`: the catalog test asserts a fix only for an error. Every refusal exits 1 on the CLI.

| Code | Kind | Sentence | Button / command |
|---|---|---|---|
| `SND-NOTE-001` | error | "Sindri isn't running. Chips and the brief can't update." (page) / "Sindri isn't running, so nothing was added. Your text is below." (CLI `add`) | Start Sindri / `sindri serve`. The capture window does not use this code: it saves without Sindri (decision 7) |
| `SND-NOTE-002` | error | "No notes folder yet. Choose one in Settings, then press Enter." (capture) / "{key} is not a vault of your profile." | Choose notes folder / `sindri note setup <path>` |
| `SND-NOTE-003` | error | "Couldn't write to today's note. Your line is kept. Check the folder is writable, then press Enter." | Open notes folder / none |
| `SND-NOTE-004` | error | "The file changed on disk after you opened it. Another app wrote to it (Obsidian, git or sync). Pick the version to keep. The other stays in History." | Keep mine, Keep the file's (no default) / none: the CLI has no `keep`; `sindri note show <path>` reads the file now |
| `SND-NOTE-005` | error | "Couldn't save {file}. Your text is kept here. Check the folder is writable, then press Ctrl+S." ({file} reads "today's note" for a daily note) | Open notes folder / `sindri note show <path>` |
| `SND-NOTE-006` | error | "{file} has more than one hard link, so Sindri won't write it." | Open on disk / none |
| `SND-NOTE-007` | error | "{file} is inside a folder your deny list hides." | Change the deny list / none |
| `SND-NOTE-008` | error | "A note called {title} already exists here." (create) | Open it / none |
| `SND-NOTE-009` | state | "Undo is off: the note changed after the capture." | none |
| `SND-NOTE-010` | error (v1.1) | "The note changed after {agent} wrote this. Decline it and ask the agent again." | Decline / `sindri note decline <id>` |
| `SND-NOTE-011` | error (v1.1) | "Agents can't edit {folder}." (a proposal into a folder with rule "can't edit", or with read off) | Change rule / none |
| `SND-NOTE-012` | error | "Couldn't read this note's history. Check the notes folder is reachable." | Open notes folder / `sindri note history <path>` |
| `SND-NOTE-013` | state | "No earlier versions." (listed so the catalog test covers the sentence) | none |
| `SND-NOTE-014` | error | "Linear isn't connected. Connect it in Settings, then try again." (make issue) | Open Settings / `sindri connection add <label> --preset linear` |
| `SND-NOTE-015` | error | "Couldn't create the issue. The line stays as you wrote it. Check your connection, then try again." | Try again / none |
| `SND-NOTE-016` | error | "{repo} isn't a repo of your profile, so Sindri can't show this link's state." (unknown chip) | Add the repo in Settings / `sindri repo add <path>` |
| `SND-NOTE-017` | error | "The OS refused this hotkey." | Pick another key / none (shared with #3170) |
| `SND-NOTE-018` | error | "A write names an old version of this note." (409) | Reload / `sindri note show <path>` |
| `SND-NOTE-019` | state | "Nothing to save. Type a line, or press Esc to close." (empty capture) | none |
| `SND-NOTE-020` | error | "Sindri or the tracker is down. Nothing was sent." (Make issue offline) | Start Sindri / `sindri serve` |
| `SND-NOTE-021` | state | "The note is over 1 MiB, so Sindri shows it as text only." | Open on disk / none |
| `SND-NOTE-022` | error | "Attachment {name} is over 25 MB." (v1.1, with image paste) | none |

022 is the last reserved in v1; 023 to 029 stay free. `Aw.Sindri.Tests/Notes/ErrorCatalogTests.cs` holds one test per row: it asserts the sentence, and the fix for an error. The frames' placeholder codes (`NOTES-WRITE-001`, `NOTES-HISTORY-001`, `LINEAR-CREATE-502`) change to these in the frames' Details line. The 1 MiB and 25 MB limits are written to the lead's pick (Open question 7). A symlink is not followed, so it has no write-refusal code (§3.1).

## 7. Agents

Everything the page does has one verb on the CLI, `sindri note <verb>`. In v1 a session may run four of them, and they also exist over MCP at `/sindri/mcp` as `note_list`, `note_show`, `note_search` and `note_history`. A doc test checks that this list and the table below agree. Paths are vault-relative, use `/`, and match case exactly (`eng/retry-policy-for-exports.md`). The CLI sends them URL-encoded.

| Verb | Arguments | Example | Who | Refused with |
|---|---|---|---|---|
| `list` | `[--folder F] [--kind daily\|linked\|note] [--tag T] [--about owner/repo#N] [--max N] [--json]` | `sindri note list --kind linked --about acme/api#3112 --json` | owner, session | 002 |
| `show` | `<path> [--json]` | `sindri note show eng/retry-policy-for-exports.md` | owner, session | 002, 007 |
| `search` | `<query> [--max N] [--json]` | `sindri note search "retry cap" --max 5` | owner, session | 002 |
| `history` | `<path> [--json]` | `sindri note history daily/2026-11-12.md` | owner, session | 002, 007, 012 |
| `add` | `<text>` or `--stdin` | `sindri note add "[ ] call Priya"` | owner | 001, 002, 003 |
| `create` | `<path> [--text T \| --stdin]` | `sindri note create eng/idea.md --text "# Idea"` | owner | 002, 007, 008 |
| `delete` | `<path>` (moves to `.trash`) | `sindri note delete eng/idea.md` | owner | 002, 007 |
| `tick` | `<path> --line N` (1-based; the line must be a task) | `sindri note tick daily/2026-11-10.md --line 14` | owner | 002, 007, 018 |
| `link` | `<path> --line N --title T` (Link it: wraps the first unlinked mention of T on that line) | `sindri note link people/priya.md --line 3 --title "Retry policy for exports"` | owner | 002, 007, 018 |
| `pin` | `<path> [--off]` | `sindri note pin eng/retry-policy-for-exports.md` | owner | 002 |
| `restore` | `<path> --version <sha256>` | `sindri note restore daily/2026-11-12.md --version 9f2c...` | owner | 002, 007, 012, 018 |
| `make-issue` | `<path> --line N --target linear:<team>\|github:<owner/repo>` | `sindri note make-issue daily/2026-11-12.md --line 9 --target github:acme/api` | owner | 002, 014, 015, 020 |
| `setup` | `<path> [--name N] [--folder SUB]` | `sindri note setup ~/notes --name notes` | owner | 002 |

v1.1 adds `propose <path> --base <sha256> (--diff F \| --append T)` for a session, with `--diff` a unified diff and `--append` naming the section by `--section` (default `Captured`), and `accept` and `decline` for the owner. Conflict resolution (`keep`) exists only in the UI: it needs the buffer a session cannot have.

Rules:
- A session reads a folder only when its retrieval switch is on. A denied path is hidden from every read, as it is from code. Search applies the folder switch per result, not per query, and a link from an `eng/` note to a note in `people/` shows the link text but not the target's snippet. Note text in every result is fenced as untrusted, over MCP and in `--json`.
- A session writes nothing in v1. The owner verbs above are refused with `SND-AUTH-002` for a session, including `add`.
- `--json` carries keys, text carries words. Exit codes follow the context family (`docs/sindri/context.md`): `0` ok, `1` attention (a refusal in the last column, Sindri not running), `2` a tool or usage error.
- The context lane never writes a note. It reads `notes.vault` through the `notes` source and cites chunks as `note:<path>#<heading>`.

## 8. API and engine requirements

One route family under `/sindri/notes`, named in P0. Every write is synchronous: the file write is the commit, then Sindri emits a `note.*` event. There is no outbox, because offline never queues. The path is a query or body field, URL-encoded, vault-relative, case-sensitive, with no `..`, so a note called `history` never collides with a route.

| Route | Does |
|---|---|
| `GET /sindri/notes?kind=&folder=&q=&about=&after=` | rows, counts per tab, `degraded[]` |
| `GET /sindri/notes/file?path=` | text, sha256, links, backlinks (linked, unlinked), linked objects with chip state |
| `GET /sindri/notes/history?path=`, `GET /sindri/notes/history/version?path=&version=` | versions; the diff of one version against the file now |
| `GET /sindri/notes/chips?ids=` | live chip state from the local store (#3021's lookup) |
| `POST /sindri/notes/ops` | `{intent, path, baseSha256, ...}`: edit, create, delete, tick, link-mention, restore, pin, undo-capture; a stale base is `SND-NOTE-018` |
| `POST /sindri/notes/capture` | the append; creates today's file; returns what Undo needs |
| `POST /sindri/notes/make-issue` | `{path, line, target, title}`; a Linear or GitHub call, then the line rewrite |
| `POST /sindri/notes/setup` | `{path, name?, folder?}`: add the vault if needed, set `notes.vault` and `notes.folder` |

v1.1 adds `POST /sindri/notes/propose` and `POST /sindri/notes/decline`.

P0 writes these records as stubs (names only; fields in order). One request and one response per route, as the slice recipe wants.

| Route | Request | Response |
|---|---|---|
| list | `ListNotesRequest(Kind, Folder, Query, About, After)` | `ListNotesResponse(Rows[], Counts, Degraded[])` |
| file | `ReadNoteRequest(Path)` | `ReadNoteResponse(Path, Text, Sha256, Links[], LinkedMentions[], UnlinkedMentions[], LinkedObjects[])` |
| history | `NoteHistoryRequest(Path)`, `NoteVersionRequest(Path, Version)` | `NoteHistoryResponse(Versions[])`, `NoteVersionResponse(Version, Diff)` |
| chips | `NoteChipsRequest(Ids[])` | `NoteChipsResponse(Chips[])` |
| ops | `EditNoteRequest(Path, BaseSha256, Edits[])`, `CreateNoteRequest(Path, Text)`, `DeleteNoteRequest(Path, BaseSha256)`, `TickTaskRequest(Path, Line, BaseSha256)`, `LinkMentionRequest(Path, Line, Title, BaseSha256)`, `RestoreVersionRequest(Path, Version, BaseSha256)`, `PinNoteRequest(Path, Pinned)`, `UndoCaptureRequest(Path, UndoToken)` | `NoteWriteResponse(Path, Sha256, VersionId)` |
| capture | `CaptureRequest(Text)` | `CaptureResponse(Path, Sha256, UndoToken)` |
| make-issue | `MakeIssueRequest(Path, Line, Target, Title, BaseSha256)` | `MakeIssueResponse(IssueId, Url, Sha256)` |
| setup | `SetupNotesRequest(Path, Name, Folder)` | `SetupNotesResponse(Vault, Folder)` |

| # | Need | Owner lane | Notes |
|---|---|---|---|
| E1 | Profile keys `notes.vault`, `notes.folder`, `notes.daily`, `notes.template`, `notes.attachments` (v1.1), and `notes.rules`, a list of `{folder, read, write}` objects that Settings edits (a dotted key with a folder name in it would break `sindri profile set`); the nearest folder wins and "Other folders" is the default; `write` is unused until v1.1; validation reuses the vault checks; `SND-NOTE-002`; the `setup` job (§4.12) | api | Settings writes the same keys |
| E2 | Index into `$AW_SINDRI_HOME/notes-index.db`: one row per `.md`, text chunked by heading, FTS5, a `pins` table a rebuild keeps; start walk (mtime + size first, hash on change); watcher (inotify, FSEvents) on directories only, a rescan every 10 minutes, a burst of events is one reindex per file after 300 ms of quiet; a delete plus a create of the same (size, mtime, hash) in one scan is a rename, and the index keeps the old path as an alias so chips, edges and history follow; a `notes` doctor warning when the inotify limit is reached ("watcher off: limit reached, rescanning every 10 min") | api | the 1,000-note spike posts its number on #2981 before the watcher design is fixed |
| E3 | The atomic write op of decision 3, the conflict rules (§4.10), `.trash` delete, byte-clean round trip test on the fixture vault. One function, `NotePath.Resolve(vault, relative)`, rejects `..`, an absolute path, NUL and backslash, matches the filesystem's case, refuses a symlinked segment and matches deny globs case-insensitively; every route and verb uses it, with a table test of 15 cases. It builds on `INoteLocator`, `ITodoNoteDisk` and `ITodoNoteFiles` | api | N1 |
| E4 | Daily note and template; capture append (`NoteAppend` in Core, called by serve and by the capture window); `sindri note add` | api | #3034 and #3035 ship together (api plan) |
| E5 | Link index (`links`, backlinks, unlinked mentions); `[[` popup source over the index plus context's edges for issues and PRs | api, context | #3036 |
| E6 | Search route and the Ctrl+K `note:` source | api, ui | #3037; reads the index, never the disk |
| E7 | Chip lookup and `about` edges, the reverse walk for object pages; the single "Create issue" op | api, context | #3038; needs #3021's lookup |
| E8 | Versions in `$AW_SINDRI_HOME/notes-history/` with the policy of §4.9; restore. v1.1: proposals and the per-folder write rule | api | #3039 |
| E9 | Context's `notes` source: `context.sources.notes` reads `notes.vault`; `retrieval` per folder, default off | context | accepted on #2981; first PR is the source on the library feed's walker |
| E10 | Events `note.captured`, `note.saved`, `note.restored`, `note.issue.made` join the catalog (v1.1: `note.proposed`, `note.accepted`, `note.declined`). The payload holds the vault name and a hash of the note path, never the path, title or text; the path is resolved from the index. No `ledger schema` line moves and no views migration is needed | api | `Aw.Redaction` scrubs any title that reaches a log line |
| E11 | Failure-to-sentence catalog (§6) in Core | api | one test per row, in `Aw.Sindri.Tests/Notes/ErrorCatalogTests.cs` |
| E12 | Global hotkey on the tray; the shared window's Note kind and its own append when Sindri is down; the Wayland portal path; the "Start Sindri at login" entry (Linux autostart file, macOS LaunchAgent) | ui | depends on #3170's window |
| E13 | `sindri doctor` row `notes`: `warn` when the index is older than 10 minutes, the watcher is off, or a history or draft directory is looser than 0700 (file than 0600); `fail` when the vault path is missing or unreadable, with the fix `sindri note setup <path>`. `--json` shows the vault name, never its path. The PR updates `src/Sindri/aot-smoke/expected.txt` | api, tools | any doctor change updates the expected file |
| E14 | The session rule. A session may call only the read routes: `GET /sindri/notes`, `GET /sindri/notes/file`, `GET /sindri/notes/history`, `GET /sindri/notes/history/version` and `GET /sindri/notes/chips`. Each applies the deny and retrieval rules per result. Every `POST /sindri/notes/*` route (`ops`, `capture`, `make-issue`, `setup`) needs the per-start human token and refuses a session with `SND-AUTH-002`. One test per route, and one test that a session token reaches no `ops` intent. `GET /sindri/ledger` and `/sindri/events` leave `note.*` payloads out for a session. In v1.1, `propose` joins the callable list and creates a proposal only | api | as Home §8 |
| E15 | Editor control spike: AvaloniaEdit (MIT) with Markdig, a NativeAOT check on `sindri-ui` (`Ui.AotSmoke`), the cursor-line chip swap and the 10,000-line paste. It ends in a go or no-go on #2981 before P2ui builds the live control. No-go means the plain monospace editor ships in v1 | ui (owner), tools (AOT check) | neither package is on main today |

## 9. Goals and non-goals

Goals:

- The owner's existing notes folder opens unchanged; edits round-trip byte-clean (N1).
- A capture is on disk in under 2 s and never loses a line (N2, N3).
- Every issue, PR and run a note links shows its notes (N7).
- Agents read by rule and write nothing in v1 (N8); in v1.1 they write only as a diff the owner accepts.

Non-goals (v1):

- Rename of a note with link updates in other files. Rename is by file manager; Obsidian's setting does it today. Later.
- Tasks view across all notes, saved searches and the property table (ledger 7, 13). Only the carried-tasks block on today's page.
- Templates beyond the daily one (ledger 8). The `{{pr}}`, `{{issue}}`, `{{scope}}` variables follow after the soak.
- Everything in the v1.1 list of §2 (lead call 1): make-all, "Note about this", image paste, agent proposals, day chords, "Moved since yesterday", and the thread, run, scope and code links.
- Hard delete and rename (decision 4).
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
| `context.sources.library.vaults` | a vault named by `notes.vault` is also a notes root; "Choose notes folder" adds the folder here when it is not a vault yet |

## 11. Gaps found

1. **The capture window does not exist.** Notes builds the Note kind on #3170's window. If #3170 lags, the Note kind ships first behind the tray item and the Notes page's own "Capture" button (a one-line sheet), then moves to the shared window.
2. **The fold depends on Home.** Until `GET /sindri/home` ships, the fold shows "Needs you" from the NeedsYou rows. "Moved since yesterday" is v1.1.
3. **Chip state depends on #3021's lookup** for issues and PRs. Runs and scopes need their own lookup, so they are v1.1.
4. **The watcher drops events.** The rescan is the safety net; the spike measures both.
5. **A vault can hold other tools' files.** Obsidian keeps `.obsidian/` and plugins' folders. The built-in dot-folder deny covers them. Obsidian's trash setting decides where `.trash` is.
6. **Home's run-failure rows** need the "seen" flag from the same store (Home §8). Notes adds no second flag.
7. **Undo on a capture needs the line's position.** The op returns what Undo needs (the line and the base sha256); Undo is off after any other change to that file (decision 8).
8. **Start at login is new code.** Nothing on main writes a login entry (a `git grep` for autostart in `src/Sindri` and `docs/csharp/sindri-ui.md` finds none). E12 owns it for Linux and macOS.
9. **The ledger's rename of a note with link updates** is not in v1 (§9).

## 12. Proof

Proof is the app run on seeded data with `aw-ui-driver`, not tests alone. The scripts are `scripts/notes-proof.sh <capture|today|chips|tree>` (through `sindri-ui-fixture.sh`) and the test classes of the N table.

1. **Byte-clean round trip** on the fixture vault (N1), run in CI.
2. **Capture walk** from a fresh `context-first-run.sh` home with a made-up vault: hotkey, plain line, task line, empty Enter, Sindri down ("Saved without Sindri"), each failure of §6, Undo. Before and after shots.
3. **Today walk:** first open, folded, next day, previous day, a carried task ticked (one-character diff), a capture while the daily page is open (silent reload).
4. **Link walk:** `[[` popup in each group, no match, Link it, the PR page listing the note.
5. **Chip walk:** merge a PR in the forge fixture (`scripts/forge-fixture.sh`) and time the chip (N6); stop Sindri and check the not-live state, the dimmed glyph, the tooltip.
6. **History walk:** edits, a capture, a restore, the pruning pass. (v1.1 adds an agent proposal over MCP, a stale proposal and accept.)
7. **Index timing** on a 1,000-note vault (N5), logged in the perf inventory.
8. **Screen reader:** `aw-ui-driver tree` (N10).
9. **Adversarial design review** by the ideas lane on the shots (owner rule, 2026-11-10), judged against inspora.design.

Perf: N2, N4, N5 measured in the real app by the perf lane. The vault in every fixture uses made-up names ("Harbor ferry"); no work app's names.

## 13. Phases and parallel plan (for /specToProvenPR)

Phases are the PRs. Every lane has work from day one; the red and stubs of each phase land first, never behind another phase's PR. The ui lane runs several builders at once, since the spike, the list and the capture window do not share files.

| Phase | What | Lane | Depends on |
|---|---|---|---|
| P0 | Contract PR: the request and response records of §8, `NoteRow`, the error catalog stubs, the events, profile keys with validation, golden and `PublicAPI` entries, `tests/fixtures/notes/` (`make-vault.sh` and the plain notes), all as stubs and the red tests for N1 and N8 | api | none |
| E15 | Editor control spike: go or no-go on #2981 | ui (owner), tools (AOT check) | none |
| P1 | Store: `notes.vault` validation, the `setup` job, `notes-index.db`, the watcher, read-only; the 1,000-note spike (E1, E2) | api | P0 |
| P1c | Context's `notes` source on the library walker; `retrieval` per folder (E9) | context | none (start now) |
| P2 | Write path: atomic op, conflict rules, `.trash`, byte-clean test (E3); capture append, `NoteAppend`, daily note with template (E4); `sindri note add` and the owner verbs | api | P1 |
| P2ui | List, today's page with the Needs you fold, editor shell (plain until E15 says go), Settings frame with "Start Sindri at login", states (§4.2, §4.3, §4.11, §4.12) | ui | P0 stubs; E15 for the live control; X1 for the visual pass |
| P3 | Capture: the Note kind in the shared window, hotkey, tray item, Wayland portal, its own append when Sindri is down, autostart entry, the failure states (E12) | ui | #3170's window (red and stubs first) |
| P4 | Links: link index, backlinks, unlinked mentions, `[[` popup (notes, issues, PRs), right pane (E5) | api, ui | P1, P2ui |
| P5 | Search route and the `note:` palette scope (E6) | api, ui | P1 |
| P6 | Read-only chips, the `about` reverse walk, notes on PR, issue and project pages, and the single Make issue (E7) | api, ui, context | P4; #3021 |
| P7 | History: versions, retention, restore, the History view (E8) | api, ui | P2 |
| X1 | Design: the visual pass for §4.1 to §4.12 in the Inspora bar; the reshoot list of §5; update #3170's frames to the "New issue" / "New note" title rule and the Ctrl+Enter create | design | none |
| X2 | UX: the walks (§12) at each phase's end; N9 week | ux | each phase |
| X3 | Perf: N2, N4, N5 in the real app; the list at 10,000 notes | perf | P1, P2ui |
| X4 | QA: judge each phase against the bar, the shots and N1 to N10 | qa | each phase |
| X5 | Tools: `scripts/notes-proof.sh`, `make-vault.sh`, aot-smoke lines, `.env.example` (and `check-env-example.sh` when a watcher or hotkey env var exists, e.g. `AW_NOTES_NO_WATCH`), and docs: `docs/csharp/sindri-notes.md`, the `notes` section of `docs/csharp/sindri-profile.md`, the route table and `note` verbs in `docs/csharp/sindri.md`, an `AGENTS.md` Commands line for `sindri note ...`, and this spec's line in `docs/sindri/README.md` | tools | each phase |
| V1.1 | After the N9 week: agent proposals and the per-folder write rule (history proposals, `propose`, `accept`, `decline`), make-all, "Note about this", image paste, day chords, "Moved since yesterday", thread, run, scope and code links | api, ui, context | N9 |

Day one: api P0 (red and stubs); ui E15 (the spike), ui P2ui on the P0 stubs, ui P3's red against #3170's stubs; context P1c; design X1; ux X2 baseline; tools X5 (`make-vault.sh`, the proof script stubs, the docs skeleton); perf X3 (the 1,000-note generator and the stopwatch harness); qa X4 (the N1 and N8 reds). Nothing waits on a dependency PR to start its red.

Gate notes: a PR that adds routes runs the full Sindri integration lane and `EndpointCqrsTests`. A PR that adds a golden or catalog row adds its scenario row and the Ui `GoldenContract` slug, and runs `Aw.Sindri.Ui.Tests` unit. A PR that adds events runs `EventCatalogTests`. `notes-index.db` is its own file, so no ledger migration and no views migration lands with Notes; a PR that does add a ledger migration takes the next free number and runs the Ledger namespace tests. A doctor change updates `src/Sindri/aot-smoke/expected.txt` and runs aot-smoke. A new `using Aw.` or sub-namespace runs `Aw.Architecture.Tests`. A new view runs `Aw.Sindri.Ui.Frames.Tests` integration. New env vars go in `.env.example`. A new project reference runs the locks and `sindri-build-inputs.sh` checks. Note text, titles and chip text are scrubbed by `Aw.Redaction` before they enter the ledger or a log line.

## Open questions

Ten are written to the lead's pick or an agent default. None blocks P0; "blocks" names what each one does block. The three the owner will notice first are 9, 10 and 5.

1. **Where the versions live** (decision 22): `$AW_SINDRI_HOME/notes-history/`, a copy per snapshot with the policy of §4.9. Written to the lead's pick. The alternative is the vault's git history when it is a repo. The copy comes first because it works for every vault. Blocks: none.
2. **Agent append to a named note** (ledger 12 allows a direct write; v1 has no agent writes at all). Written to the lead's pick: "never edits silently" is the owner's rule. A run-log note that needs a direct append can get a third rule value, "Agents can append", after the soak. An issue is filed for it `Part of #2981`, with this trigger: more than 5 pending proposals older than a day. Blocks: v1.1.
3. **What "Moved since yesterday" reads** (§4.2, v1.1): Home's Moving rows whose `since` is after local midnight of the previous day. Written to the lead's pick. Blocks: v1.1.
4. **Capture with context** (§4.1, v1.1): "Note about this" actions on Reviews, Home and scope pages open the editor on a new pre-linked note; the global window stays one line. Written to the lead's pick. Blocks: v1.1.
5. **Default Make-issue target** (§4.8): GitHub in the linked PR's repo when exactly one PR is linked, else the profile's tracker, as ideas' check on #3038 proposed. Written to the lead's pick. Blocks: P6.
6. **"Make all" on an `## Actions` section** (§4.8): v1.1, with one confirm that lists the titles. Written to the lead's pick. Blocks: v1.1.
7. **Size limits** (§6): a note over 1 MiB opens as text only, an attachment over 25 MB is refused. Written to the lead's pick. Blocks: P2ui (the 1 MiB banner).
8. **Where the daily folder is read from** (§4.2): the profile's `notes.daily`, defaulting to `daily/`, and Obsidian's own `daily-notes.json` read once when present. Written to the lead's pick. Blocks: P2.
9. **Capture while Sindri is down** (lead call 2): the window appends to the daily file itself, with the same hash-guarded write, and shows "Saved without Sindri". Written to the lead's pick. It gives the window its own write path, so a bug there skips serve's checks; the shared `NoteAppend` code and the shared tests are the guard. Blocks: P3.
10. **Start at login** (lead call 3): one click in Settings and in `sindri setup` once a notes folder is set; nothing turns it on silently. Written to the lead's pick. Blocks: P3.

Agent defaults (numbers no owner or lead gave): 2 s capture walk, 100 ms paint and reindex, 300 ms Enter to fsync, 50 ms popup, 5 rows in the fold, 8 s Undo, 1 s chip update, 340 px version list, 32 px rows, 10 minute rescan, 300 ms watcher quiet time, 250 ms draft pause, 1.5 s autosave, 60 s snapshot floor, 10,000 notes for the popup, 3-character and 20-row limits on unlinked mentions, 10 vaults (main's own limit).
