# Sindri's Notes page: write it down once, see it next to the work

Status: draft, autoplan round 2 folded in (ideas lane, 2026-11-12). Round 3 is next. Serves #2981 (Notes) and #2978 (one app that replaces the others). Builds the rows of the Notes prior-art ledger (`docs/sindri/prior-art/notes.md`, merged with #2989's rule) and the issues #3034 to #3039.
Frames: design's round 4 at `pr-assets/notes-2981` @ `d0c2fd7d2e` (`guesses.md` beside them; round 4 dropped `notes-link-rename.png`). Frame names below are files in that folder. §5 lists the frames this round changes.
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read). Rounds 1 and 2 are folded in (2026-11-12); "Review notes" at the end lists what each round changed. The lead's calls are in §2.

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
| Line to issue | 10 | Linear create from selection | v1.1: Ctrl+Shift+I on one line, then make-all. v1 has no outside write | §4.8 (Take 5, #3038) |
| History and undo | 11 | Obsidian File recovery | a snapshot at most every 60 s of editing; restore an older version as a new one | §4.9 (Take 6, #3039) |
| Agent writes by rule | 12 | Basic Memory | v1: agents read by rule and write nothing. v1.1 (P8, #3207): an agent edit is a proposed diff the owner accepts | §4.9, §7 |
| Notes as a table | 13 | Obsidian Bases | later | §10 |
| Design-doc items vs PRs | 14 | none | stays as is (Take 7) | n/a |

**The bar in one line:** the owner's existing notes folder opens unchanged, an edit round-trips byte-clean, a capture is on disk under 2 s after the capture action, and every issue and PR a note links shows that note on its own page.

Done-checks. Each is a test or a measured run in the real app (`aw-ui-driver`). Each names its lane in §12.

| # | Check | Measured as | Runs as |
|---|---|---|---|
| N1 | **Byte-clean.** A fixture vault (made-up notes, mixed line endings, no trailing newline, a BOM, tabs, front matter, a symlink, a hard-linked file, and an Obsidian-style daily folder `Journal/` with date format `DD-MM-YYYY` named in `.obsidian/daily-notes.json`). Open each, change one character, save. Today's page lands in `Journal/`, not in `daily/`. | `git diff --stat` on the vault shows the one changed line per file and nothing else. The symlink is not followed and the hard-linked file is read-only. | `Aw.Sindri.Tests.Notes.ByteCleanRoundTripTests` (integration) on a vault that `tests/fixtures/notes/make-vault.sh` builds at test time. Only the plain notes are checked in, because git does not keep links on every OS. This test never goes through the editor control; N1b does |
| N1b | **Byte-clean through the editor.** The same fixture notes, opened in the real editor. | `aw-ui-driver type` one character, save, then `git diff --stat` on the vault shows the one changed line per file. CRLF, a mixed ending, a BOM, tabs and a missing final newline survive. | `scripts/notes-proof.sh bytes`; also a go criterion of E15 |
| N2 | **Capture speed.** Two parts, because `aw-ui-driver` cannot press a global hotkey (on Wayland the desktop's portal decides). | (a) Scripted: the `notes.capture` action id to a painted window under 100 ms with the main window closed (window pre-created, hidden; ledger 1). The driver types the 10-word line in one call, so typing time is out of the count. Enter to fsync done under 300 ms. The whole walk ends in under 2 s. (b) By hand, once per OS (X11, Wayland, macOS): after "Start Sindri at login" (§4.12) and a reboot, the hotkey opens the window. Part (b) gates P3. If the Wayland portal refuses, the tray item is the documented path. | (a) `scripts/notes-proof.sh capture`, through `sindri-ui-fixture.sh` and `aw-ui-driver`, run by the perf lane. (b) the owner, in P3's PR |
| N3 | **Capture never loses a line.** Folder removed, write refused, Linear down; Sindri down. | Linear down: the line stays and Issue is off. Folder removed or write refused: the window keeps the typed text and shows the sentence and the one fix of §6, and Enter saves it after the fix. Sindri down: the window runs the helper `sindri note add --direct` and shows "Saved without Sindri" (§4.1); the editor opens the note read-only. Serve and the helper append at the same moment: both lines are on disk. A kill -9 of the window loses at most the last 250 ms of typing (the draft is on disk, §4.1). A kill -9 of the editor loses at most the last 1.5 s of typing. | `Aw.Sindri.Tests.Notes.CaptureNeverLosesLineTests` (integration), including the two-writer case and the direct path with a symlinked daily file, a hard-linked file, a denied folder and a `daily` key that points outside the vault; and the kill walk in `scripts/notes-proof.sh capture` |
| N4 | **Today in 100 ms.** Open Notes on a 1,000-note vault. | Today's text paints under 100 ms from a local read; the fold and chips fill in after and never block the text. | `scripts/notes-proof.sh today`, `make-vault.sh --notes 1000` |
| N5 | **Index speed.** 1,000-note vault. | One edited file reindexes under 100 ms (api's spike number; posted on #2981 before the watcher design is fixed). Cold start under 5 s. Backlinks for the open note under 100 ms. | `Aw.Sindri.Tests.Notes.IndexSpikeTests` (`Lane=heavy`, so CI never runs it; the perf lane runs it by hand at P1 and posts the numbers) |
| N6 | **Chips are live.** A linked PR merges. | The chip changes within 1 s of the ledger event, and the run also logs the time from the forge fixture's merge call to the chip, so a late event shows. The file is byte-identical before and after. | `scripts/notes-proof.sh chips`, with `scripts/forge-fixture.sh` |
| N7 | **Every linked object shows its notes.** | The PR, issue and project pages list the notes that link them (the reverse walk of context's `about` edges). | `Aw.Sindri.Tests.Notes.ObjectPageNotesTests` (integration) |
| N8 | **Agents never write a note in v1.** | Every `POST /sindri/notes/*` route refuses a session token with `SND-AUTH-002`, and the files stay byte-identical. One test per route, plus one test that walks the command catalog and asserts every `note.*` write row refuses `Session` and `Launched`, so a new intent without a row fails. (The proposal check, "the file is byte-identical until the owner accepts", arrives with proposals in P8.) | `Aw.Sindri.Tests.Notes.SessionWriteRouteTests` (integration) |
| N9 | **A week without the other app.** The owner, on their real notes folder, for 5 working days with the other app closed. | Pass line: 0 lost lines, 0 byte diffs (`git diff --stat` on the vault shows only lines the owner typed), and no open miss labelled `blocking`. Every miss is an issue `Part of #2981`, labelled `blocking` or `rough`. v1.1 starts when the line holds. | the owner, by hand; the ux lane runs the same walk on a made-up vault earlier and files its misses (it proves the flow, not the habit) |
| N9a | **Slice A soak.** P0 to P3 (capture and today) are "slice A". The owner uses capture and today's page for 3 working days before P4 to P7 land in the owner's build. | The same pass line as N9, for capture and today only. | the owner, by hand |
| N10 | **Screen reader.** | `aw-ui-driver tree` names every row, chip, region and button; a chip reads "pull request 3104, merged"; a not-live chip adds "as of 14:02". | `scripts/notes-proof.sh tree` |
| N11 | **Safety rules.** One row for the rules that no other check covers. | (1) A session `note show` on a fresh folder is refused, the folder's read switch on allows it, a denied path never. Flipping the switch off removes the folder from context's index: a query finds nothing. (2) A note in `people/` links a note in `eng/`: a session's `show` of the `eng/` note returns the link count 0 and no paragraph. (3) A capture while the daily page is open reloads silently and raises no conflict (§4.10). (4) History: a restore, the pruning pass, modes 0700 and 0600, a move into a denied folder drops the history from a session's reads. (5) Link it wraps exactly one mention. (6) The search box returns its first row under 100 ms at 1,000 notes. | `Aw.Sindri.Tests.Notes.SafetyRuleTests` (integration), one test per numbered part |
| N12 | **Make issue (v1.1).** With the forge and Linear fakes. | Enter in the title field creates nothing. The button creates exactly one issue and disables until the route answers. Sindri or the tracker down creates none and keeps the line. A stale base is refused before the outside call. A created issue with a refused rewrite shows the issue and "Replace the line", and a retry makes no second call. Undo restores the line only. | `Aw.Sindri.Tests.Notes.MakeIssueTests` (integration), with P6b in v1.1 |

## 2. Decisions

Taken for the owner. Each says where it came from. "Written to the lead's pick" means the inputs left it open and the spec picked the simplest answer; each one is also in Open questions. A number with no source is an agent default (listed in Open questions). A tag such as "Lead", "Meta", "Design" or "Api plan" says who said it. Only a pointer to an owner comment (`owner decision (#2981 <comment>)`) makes a value the owner's; every other number is an agent default.

1. **Notes are plain Markdown files in a library vault the owner names.** No second root. The profile gets `notes.vault` (a vault name) and `notes.folder` (an optional subfolder), as `todo.obsidian.vault` does. Path checks reuse the vault's deny globs and the `SND-LIB-001` root rules. A denied file is hidden from the index and from every read. One named exception: Sindri reads `.obsidian/daily-notes.json` and nothing else under `.obsidian/`, read-only, only for `folder`, `format` and `template` (§4.2). "Choose notes folder" (§4.12) does two edits in one job: it adds a folder that is not yet a library vault to `context.sources.library.vaults`, then sets `notes.vault`. The owner needs no library vault first. (Meta, #2981 api plan, accepted 2026-11-12; lead call 7 below.)
2. **The files are the store; the index is a view.** One row per `.md` file in `$AW_SINDRI_HOME/notes-index.db`, its own SQLite file (WAL, FTS5), key `note:<vault>/<path>`. A walk of the files rebuilds it at any time, and a corrupt file is deleted and rebuilt. It is not in `views.db`, and `sindri views rebuild` never touches it; `sindri note reindex` rebuilds it (E2). Only `note.*` events go to the ledger. Pins are not in this file: they live in `$AW_SINDRI_HOME/notes-state.json` (mode 0600), so a rebuild never loses them. A rename leaves the old path in a `previous_paths` column of the row. Chips, the fold and the carried-tasks block are drawn by the app and never written into a file. Why a second index beside context's: the notes index owns the link table, the unlinked-mention search and the pins; context's `notes` source (E9) takes its chunks and links from the notes index's reader (`INoteIndexReader`) and does not parse the folder a second time. This is an agent default; the context lane confirms it on #2981. (Ledger open question 2; api plan; lead call 4.)
3. **Every write is an atomic swap guarded by the file's hash, under one cross-process lock.** The editor opens a file at its sha256. A write takes an `flock` on `$AW_SINDRI_HOME/notes-locks/<hash of vault and path>.lock` (never in the vault). Serve, the capture helper (decision 7) and the todo note sync all take this same lock. The writer re-reads the file and compares the hash. A different hash follows the conflict rule of §4.10 and nothing is written. A matching hash writes a dot-named temp file in the same folder (`.<name>.sindri-tmp`, which sync tools skip), copies the original's mode and fsyncs. It then calls main's `ITodoNoteFiles.Swap`, which swaps the temp file and the note in one call and returns `CameOut`, the file that was displaced. Sindri hashes `CameOut`. If it is not the hash the writer read, an outside writer got in after the re-read: Sindri swaps `CameOut` back or keeps it with `Keep` (`.sindri-tmp.keep-<time>`), writes those bytes (never the earlier read) to History and raises the conflict (§4.10). The lock stops Sindri's writers from racing each other; the swap is the net for Obsidian and sync tools. A crash between the temp file and the swap leaves an intent file and a temp file: the next start reuses main's intent file and `Leftover` recovery, and sweeps temp files older than 1 h in the vault's own folders. A missing note is created without replacing anything that appeared meanwhile; if one appeared, a capture retries as an append. Bytes outside the edit stay as they were. (Api plan; N1; arch rounds 1 and 2.)
4. **Delete moves the note to the vault's `.trash`.** There is no hard delete in v1. `.trash` is on the built-in deny list, so the index never lists a trashed note. (Meta, #2981, 2026-11-12.)
5. **Capture rides the one global Ctrl+Alt+I window shared with Projects (#3170).** It always opens on the Note kind, titled "New note". A switch at the left (Ctrl+Tab) flips to "New issue". One window exists at a time: a second hotkey press focuses the open one. The Issue kind creates on Ctrl+Enter or its button "Create issue in Linear". Enter there only moves focus to that button. A bare Enter never writes to Linear or GitHub. The switch is hidden when no notes folder is set, and the window then makes issues only, by the same Ctrl+Enter. #3170's frames and spec follow this rule (X1). (Lead, 2026-11-12; autoplan round 1.)
6. **A refused hotkey uses #3170's Settings row.** On Wayland the hotkey goes through the desktop's global-shortcuts portal. A refused hotkey shows "The OS refused this hotkey." and one fix, "Pick another key" (the frame's words). The tray item "New issue or note" still works. (Lead, 2026-11-12; `notes-settings-hotkey-refused.png`.)
7. **A capture never loses the typed line.** The window writes the text to a draft file on every 250 ms pause: `$AW_SINDRI_HOME/notes-drafts/capture-draft.txt` (directory 0700; the file is created with mode 0600, not changed after). A kill loses at most the last 250 ms. It deletes the draft after a successful save, and Esc deletes it too, because Esc is an explicit drop (the frames' footer says "Esc drop"). Only a crash or kill leaves a draft, and only then does the next open offer it back: "Restored your last unsaved line". The Issue kind never fills from a restored draft. If Sindri is down, the window starts the helper `sindri note add --direct` and the helper appends to today's file (lead calls 2 and 8). "Down" means no live host lease (the lease file serve holds), never a timeout. With a live lease the window goes through serve; if that fails it shows "Sindri is busy. Your line is kept." (`SND-NOTE-023`) and never falls back to a direct write. If the folder is not set or the write fails, the window stays open, keeps the text and shows one sentence and one fix (§6). If the folder is removed while the window is open, the window shows "No notes folder yet" and keeps the line. (Lead, 2026-11-12; `notes-capture-error-*.png`.)
8. **Capture appends under `## Captured` with a time.** A plain line is `- 14:32 text`. A line that starts with `[ ]` is a task, `- [ ] 14:32 text` (checkbox first, so Obsidian Tasks reads it). The window is one line: Shift+Enter does nothing, and a pasted line break becomes a space, with the notice "Line breaks became spaces" for 3 s. A long line scrolls and never wraps, so the window keeps its height. Today's file is created from the daily template when missing. The toast says "Captured" and keeps Undo for 8 s; Undo removes exactly that line and turns off once the note changed. The toast is a small Sindri window that takes no focus and is also announced as an OS notification. The keyboard route is the palette entry `notes.undo-capture` (Ctrl+K in the main window, valid for the same 8 s). After 8 s the way back is History. Enter on an empty line saves nothing, keeps the window open and says "Nothing to save." (Ledger 1; ideas' check on #3034; guesses 4.)
9. **Today's page opens with a "Needs you" fold (v1).** Up to 5 rows, drawn with Home's rows. "Moved since yesterday" joins in v1.1. The fold stays collapsed once you scroll past it, until the next day. The daily file has no heading: the page draws "Thursday 12 Nov 2026" from the file name, the list row says "Thu 12 Nov", the breadcrumb says `2026-11-12`. (Lead, 2026-11-12; `notes-today.png`, `notes-today-folded.png`.)
10. **Carried tasks are drawn, not stored.** The block "Open tasks from earlier days" comes from the index; it is not in the file for today. A tick edits the original line in its own day's file (one character). (Ideas' check on #3035; guess 6.)
11. **Backlinks go in the right pane, as the frame shows it.** On a note: Linked objects (collapsed), then Linked mentions, then Unlinked mentions. On today's page: Linked objects (open) and one list named "Backlinks". §4.5 says what each holds. There is no PR-body backlink row: the index does not read PR bodies. (Lead, 2026-11-12; `notes-backlinks*.png`, `notes-today-conflict.png`.)
12. **Every issue and PR reference is a live chip, unless the cursor is on its line.** The cursor line shows the raw text (`[[acme/api#3104]]`). Plain blue text means a link Sindri cannot resolve. A chip shows a state glyph, the id, the short title and a state word; never color alone. The chip truncates a long title and never wraps, so the line keeps its height when the cursor leaves it. Its width differs from the raw text, so the line can still reflow sideways when the cursor enters it; that is accepted. Chips are read-only in v1. (Lead; `notes-live-chips.png`, `notes-live-chips-legend.png`.)
13. **"Not live" dims only the glyph and the chip edge, and never below 3:1.** The dimmed glyph and edge stay at about `#7a7d85` or lighter on `#08090a`, and the chip also changes shape: a dashed edge and a hollow glyph, so lightness is not the only signal. The text stays at normal ink (4.5:1 or more). The tooltip reads "as of 14:02". The right pane's rows dim their glyphs the same way. A chip for an object Sindri does not know says "unknown" with one fix, "Add the repo in Settings". (Lead; `notes-live-chips-notlive.png`, `notes-today-down.png`.)
14. **Make issue is v1.1: Ctrl+Shift+I, one issue from one line.** A bare `m` would type into the editor. Make-all comes after it. v1 has no outside write of its own (the Issue kind of the capture window is #3170's). (Lead call 11; replaces the `m` in #3038's body.)
15. **History restore writes an older version as a new version and diffs it against the file now.** The current version reads "Current" and has no Restore. (Lead; `notes-history-restore.png`.)
16. **One Notes Settings frame:** the notes folder, "Start Sindri at login", the capture hotkey row shared with #3170, and the per-folder "Agents can read this folder" switch. The switch writes `notes.rules` (E1) and nothing else; context's `retrieval` is derived from it. The CLI twin is `sindri note access <folder> --read on|off`. The per-folder write-rule rows in the frame wait for v1.1. (Lead; `notes-settings.png`.)
17. **Checkbox border is `#70757c` everywhere** (3.4:1 on the capture window's `#222326`, more on the page's `#08090a`). (Lead.)
18. **A bare letter never writes to an outside source (GitHub, Linear).** Make issue (v1.1), resolve and the like go through a button, a chord with Ctrl, or a menu. No typed confirmations. Offline never queues a write to an outside source: Make issue is off while Sindri or the tracker is down, and the line stays as written. Writes to the owner's own files are local and may use a chord. (Lead, team rules.)
19. **Agents write nothing to notes in v1.** An agent reads a folder only when its retrieval switch is on (default off). Every `POST /sindri/notes/*` route refuses a session (E14). In v1.1 (P8, #3207) an agent edit is a proposal the owner accepts, under a per-folder rule (§4.9, v1.1 part). Refusal comes from a new owner-only action class (E14). (Meta, #2981; lead call 6; ledger 12 narrowed.)
20. **The queue of notes is a list on the row / peek / full grammar.** `RowList`, `Row` and `PeekPane` on `RowKeys`, with no key handler of its own (#2978; Reviews spec decision 14). Space peeks, Enter opens the editor as the full view. `p` pins or unpins the row (local state in the index store's pins table, never in the file).
21. **Day chords wait for v1.1.** In v1 the day links on the page are clickable and reachable by Tab, and the palette has `notes.today`. In v1.1 the chords are Alt+PageUp (previous day) and Alt+PageDown (next day), because Alt+Left and Alt+Right jump a word on macOS and Ctrl+Alt+Arrow switches workspace on GNOME. Previous and next mean the previous and next EXISTING day (Foam's rule). From the list, `g p`, `g n` and `g d` (today) work; `g d` works anywhere except a text field. (Design, round 3; `guesses.md` 16.)
22. **Where history lives: `$AW_SINDRI_HOME/notes-history/`, never in the vault.** The policy is in §4.9: content-addressed copies, a 60 s snapshot floor, pruning, directory mode 0700 and file mode 0600, and the deny and retrieval checks on every read. (Ledger 11; lead call; api plan left git versus copy open.)
23. **Error codes are `SND-NOTE-001..022`.** The frames' `NOTES-WRITE-001`, `NOTES-HISTORY-001` and `LINEAR-CREATE-502` were placeholders (`guesses.md` 2). Reserved now; no `SND-NOTE-` code exists on main.
24. **Agents get the read verbs in v1.** Every action has a CLI verb. A session may run `list`, `show`, `search` and `history` (CLI and MCP) and never presses the owner's buttons (§7). The owner verbs have no MCP tool on purpose: a session is refused anyway.
25. **The editor saves 1.5 s after the last key and on blur.** "Saving..." shows only when a write takes over 300 ms; a failed save shows "Not saved". On a failed save the buffer stays in the editor and Sindri also writes it to `$AW_SINDRI_HOME/notes-drafts/<sha256 of the path>.draft` (same modes). A `.draft` file is deleted after the next good save of that path, and any older than 7 days is deleted at start. (Autoplan rounds 1 and 2; agent default.)
26. **The todo note is read-only in the Notes editor.** The todo sync writes that note under its own base and intent files. The Notes editor opens it with the banner "Sindri's todo list writes this note. Edit it from Todo." The list still shows it. (Lead call 15; arch round 2.)
27. **With Sindri down, the editor opens a note read-only.** Chips and the fold are muted. Only the capture window writes without Sindri, through the helper. The frame's "and saves" is wrong and is reshot (§5). (Lead call 16.)
28. **A session is refused by an owner-only action class.** `ActionClass` gets a new value that allows `Terminal` and `App` only. Every `note.*` write command is in it (E14). Reusing `Desk` would let an agent write a note. (Lead call 9.)

### Lead calls after autoplan round 1 (2026-11-12, lead)

1. **v1 scope.** v1 is P0, P1, P2, P2ui, P3, P4, P5 (search and the `note:` scope), read-only chips and History restore. (Round 2, call 11, moved the single Make issue to v1.1.) Deferred to v1.1, after the N9 week: Make issue, make-all, "Note about this", image paste, agent proposals and accept, day chords, "Moved since yesterday", and the thread, run, scope and code link forms. An editor-control spike (E15) is the first phase, owned by the ui lane. §13 re-cuts the phases with the red and stubs first and every lane busy from day one.
2. **Capture while Sindri is down.** The capture window appends to the daily file itself, with the same hash-guarded write, and shows "Saved without Sindri". The typed line is never lost. The index picks the line up on the next start.
3. **Autostart.** Setup offers "Start Sindri at login" as one click once a notes folder is set (§4.12). Nothing turns it on silently. N2 says "once the owner clicked it".
4. **Index store.** Its own file, `notes-index.db`, rebuilt from the files, not in `views.db`.
5. **Link grammar for v1.** `[[Note]]`, `[[owner/repo#N]]` and plain URLs. The thread, run, scope and code forms go to v1.1.
6. **Agent writes.** Proposals only, deferred with call 1 (now phase P8, call 18), so v1 has no agent writes to notes. E14 names the exact routes a session may call (read routes only in v1), with one test per route. An issue gets filed for "Agents can append" later (Open question 2).
7. **Also decided.** Capture always opens on the Note kind (decision 5). The window is one line (decision 8). The conflict rule on the open daily page is in §4.10. The history policy is in §4.9. One write route family, `/sindri/notes/...`, synchronous, with no outbox (§8); there is no `/sindri/rows/{id}/ops`. The write mechanics are decision 3. Draft file locations are in decisions 7 and 25. The sentence for `SND-NOTE-015` and the frame wording for 003, 005 and 012 are in §6. The right pane follows the frame (decision 11).

### Lead calls after autoplan round 2 (2026-11-12, lead)

Numbered on from round 1. Each is `agent default (lead, 2026-11-12)`. The two marked *owner can flip* have an open question for the owner (Open questions 11 and 12).

8. **Capture while Sindri is down** (autoplan D1). The window keeps "Saved without Sindri", through a small helper it starts: `sindri note add --direct`. One cross-process lock is shared by serve, the helper and the todo sync (decision 3). "Down" means no live host lease. `Aw.Sindri.Ui` gets no reference to Core. *Owner can flip.* The alternative: drop the direct write; the window says "Sindri isn't running. Your line is kept." and holds the draft until serve is back. It is smaller, but capture from anywhere then needs serve. `agent default (lead, 2026-11-12)`
9. **Who a session is refused by** (D2). A new owner-only action class in `Authorization`; every `note.*` write command is in it (decision 28, E14). `agent default (lead, 2026-11-12)`
10. **The write route** (D3). One `NoteOpCommand` with an `Intent` variant and one catalog row per intent, as `Command:Variant` rows work on main (§8). `agent default (lead, 2026-11-12)`
11. **Make issue moves to v1.1** (D4, the alternative). v1 has no outside write of its own. The reason is a smaller v1, as in round 1. Its tests, frames and error codes move to the v1.1 row. *Owner can flip.* The pick that was not taken: keep it in v1 with the duplicate-issue safeguards of §4.8 and the check N12. `agent default (lead, 2026-11-12)`
12. **Obsidian's daily folder** (D5). Sindri reads exactly `.obsidian/daily-notes.json` as a named read-only exception, for `folder`, `format` and `template`. The date format joins the spec (`notes.dailyFormat`). N1 gets a fixture with a non-default folder and format. `agent default (lead, 2026-11-12)`
13. **History before the write path** (D6). The store half of E8 (content-addressed copy, modes 0700 and 0600) is built in P2. P7 keeps the view, restore, pruning and the doctor row. `agent default (lead, 2026-11-12)`
14. **Proof** (D7). The owner runs N9 for 5 working days with a pass line. N2 is split into a scripted part and a by-hand hotkey check per OS. N1b tests the editor. P0 to P3 are "slice A" with a 3-day soak (N9a). `agent default (lead, 2026-11-12)`
15. **The todo note** (D8) opens read-only in the Notes editor, with a banner (decision 26). `agent default (lead, 2026-11-12)`
16. **Sindri down, editor** (D9). The editor opens read-only (decision 27); the frame's "and saves" is fixed. `agent default (lead, 2026-11-12)`
17. **The agent read switch** (D10). `notes.rules` is the one store. Settings and `sindri note access <folder> --read on|off` write it. Context's `retrieval` is derived. `agent default (lead, 2026-11-12)`
18. **Agent proposals get their own phase** (D11). "P8 (v1.1)" builds #3207: agents PROPOSE appends and edits, and the owner accepts. It depends on P7, P2 and the owner-only class (call 9), and starts after N9 holds. "Agents can append" stays a separate open question (Open question 2). `agent default (lead, 2026-11-12)`

## 3. The model

### 3.1 The note row

`NoteRow` in `Aw.Sindri.Core.Notes`. A view over a file; the file is the truth.

| Field | What it holds |
|---|---|
| `id` | `note:<vault>/<path>` |
| `path`, `size`, `mtime`, `sha256` | from disk; `sha256` is the version a write names |
| `title` | the first `#` heading; else the file name; for a daily note, the date from the file name |
| `kind` | `daily` (the file name matches `notes.dailyFormat`, default `YYYY-MM-DD`, in the daily folder), `linked` (links at least one issue or PR), `note` |
| `headings[]`, `tags[]` | `#tag` and front-matter `tags` |
| `links[]` | `[[Note]]`, `[[owner/repo#123]]`, plain URLs (v1). `[[thread:..]]`, `[[run:..]]`, `[[scope:..]]`, `[[code:..]]` join in v1.1 |
| `firstLine` | the first body line that is not a heading |
| `pathHash` | the hash of `path` that `note.*` events carry; `sindri note list --json` prints it, so a ledger line maps to a file without the index |
| `waitsOn` | v1.1: `agent edit` (or "3 agent edits") while proposals wait, else empty. v1 does not draw the cell |

The index also stores the text, chunked by heading, for search (SQLite FTS5). A query reads the index and never walks the disk. A file with more than one hard link is indexed and read-only. A symlink is not followed (`SND-LIB-001`), so it is not indexed. A denied path is not indexed.

### 3.2 Link forms written into the file

Obsidian-compatible, so an existing vault opens unchanged (`notes-link-written.png`). The grammar lives in the owner's files for good, so v1 writes only forms Sindri can resolve today.

| Kind | Written | Note |
|---|---|---|
| Note | `[[Note Title]]` or `[[Note#Heading]]` | exactly as Obsidian writes it. When two notes share a title, Sindri writes `[[folder/Title]]`, and the index resolves an exact path first |
| Issue or PR | `[[owner/repo#3112]]` | the picker knows the repo, so the file always names it |
| Plain URL | `https://github.com/owner/repo/pull/3112` | the index reads an issue or PR URL as the same link as `[[owner/repo#3112]]` |

A bare `[[#3112]]` found in a file (typed by hand, or from an older tool) is read against the profile's first repo, and the chip says "repo guessed: acme/api". The guessed link makes no `about` edge until the owner fixes it, so the note does not show on the wrong repo's page or reach agent context. Sindri never writes that form. In Obsidian, `[[owner/repo#3112]]` shows as an unresolved link, and a click offers to make a note called `owner/repo`; the plain URL is the form that never makes a file. The thread, run, scope and code forms (`[[thread:..]]`, `[[run:..]]`, `[[scope:..]]`, `[[code:..]]`) wait for v1.1, when the context lane fixes their ids (`guesses.md` 1). v1.1 also adds a Linear issue URL, which the index reads as an `about` link and the chip shows as "EXP-203" (§4.8).

### 3.3 Links as edges

Context's plan (accepted on #2981): `[[Note]]` is `links note:`; `[[owner/repo#123]]` or a URL is `about item:`. "Every linked object shows its notes" is a reverse walk of those edges. Code and scope edges (`path:42`, a scope id) join in v1.1. The notes index and context's `notes` source read the same files and the same `notes.vault`.

### 3.4 Write intents

| Intent | What changes | Bytes touched |
|---|---|---|
| Edit | the open note, by the owner | the owner's edit |
| Create | a new note | the new file |
| Delete | the note moves to `.trash` | none; the file moves |
| Append (capture) | a line under `## Captured` in the file for today | one line, plus the heading and the file when missing |
| Undo capture | removes the line the capture added | that line |
| Tick | one task line, in its own day's file | one character |
| Link a mention | wraps one mention in `[[ ]]` in the other note | the brackets |
| Make issue (v1.1) | replaces one line with a link | that line |
| Restore | the whole file becomes an older version | the file, as a new version |

v1.1 adds Accept a proposal (applies the agent's diff, or re-applies its append) in P8.

Each is one synchronous call under `/sindri/notes/...` (§8): lock, hash check, temp file, swap, check of `CameOut` (decision 3), then a `note.*` event. There is no outbox and no `/sindri/rows/{id}/ops`. An Append names no base: it re-reads the file under the lock and adds the line, so a capture never conflicts with an earlier capture. Every other intent that changes existing text names `baseSha256`, the sha256 the caller read. A file changed on disk since the editor opened it follows the conflict rule (§4.10).

## 4. The page

Layout: the top bar, then the list (§4.3) with a peek, and the editor as the full view with the right pane (§4.5). The page opens on today (§4.2). The palette entry `notes.today` opens today's page from anywhere; `g d` joins it in v1.1. "The read view" in this spec means the peek (§4.3) or the editor in read-only.

### 4.1 Capture

One window, shared with Projects (#3170). It is a frameless one-line window over the desktop that opens even with the main window closed. The frames' captions under the window (for example "No notes folder is set, so the switch is hidden") are captions for reviewers, not UI text. The owner clicks "Start Sindri at login" once (§4.12) so a process holds the hotkey after a reboot.

```
┌ New note ──────────────────────────────────────────────────────┐
│ [Issue | Note]  [ ] ☐ call Priya about the retry cap           │
│ Adds to 2026-11-12 · Captured        - [ ] 09:12 call Priya... │
│ Enter save · Esc drop · Ctrl+Tab Issue / Note                  │
└────────────────────────────────────────────────────────────────┘
```

- **Open.** The hotkey (Ctrl+Alt+I, changeable in Settings) or the tray item "New issue or note". The window always opens on the Note kind. Title: "New note" (also on the toast and in the no-folder frame, which the reshoot fixes).
- **Note kind.** A preview line shows the exact text that will be appended. A leading `[ ]` draws an empty task box (border `#70757c`) and the line "starts with [ ]: saved as a task". When today's note does not exist the chip says "Creates 2026-11-12 from your daily template, under Captured" (`notes-capture-bar.png`, `notes-capture-bar-plain.png`).
- **Issue kind.** The Projects window: Project and Assignee chips, "in Linear · EXP", the primary button "Create issue in Linear", footer Ctrl+Enter create, Esc drop, Tab change project, Ctrl+Tab Issue / Note (`notes-capture-bar-issue.png`, which design reshoots for this footer). Enter with text only moves focus to the button. Its errors reuse the make-issue sentences of §6 ("Linear isn't connected", "Couldn't create the issue").
- **Enter (Note kind).** Appends under `## Captured` and closes. A toast "Captured" with Open note and Undo (8 s) shows (`notes-capture-saved.png`). Undo removes exactly that line and turns off once the note changed (the toast says why).
- **Draft.** On every 250 ms pause the window writes the text to `$AW_SINDRI_HOME/notes-drafts/capture-draft.txt` (decision 7). A successful save deletes it, and so does Esc ("drop"). After a crash or kill the next open offers it back: "Restored your last unsaved line".
- **Sindri down.** "Down" means no live host lease. The window starts the helper `sindri note add --direct` (the `sindri` binary, found through `bin/cs/current`). The helper runs the same `NoteAppend` code and the same lock and swap as serve (decision 3), so `Aw.Sindri.Ui` gets no Core reference; `deps.yaml` gets one `cli` edge from `Ui.Desktop` to `sindri`. The helper reads the profile file itself through the profile loader. If the profile is unreadable or the key is missing, it fails closed: it keeps the line and shows `SND-NOTE-002`. It never reuses a last-seen path. It repeats every check serve applies: `NotePath.Resolve`, deny globs, the 1 MiB limit, the hard-link rule and `SND-LIB-001`. It writes the History snapshot (decision 22). It skips three things: no `note.captured` event, no index update until the next start walk, and no chip refresh. The skip list has a test. The toast reads "Saved without Sindri". When today's file is missing, the helper creates it from the daily template or the built-in default (§4.2). With a live lease the window never writes itself (decision 7). Design shoots the toast; it replaces `notes-capture-error-down.png`.
- **Nothing to save.** Enter on an empty line saves nothing and the window says "Nothing to save. Type a line, or press Esc to close." (`notes-capture-empty.png`).
- **No folder.** The switch is hidden and the window only makes issues (`notes-capture-no-folder-no-switch.png`).
- **Failures** keep the line (§6): the folder removed while open, `notes-capture-error-folder.png`; a refused write, `notes-capture-error-write.png`.
- **CLI twin.** `sindri note add "text"` (stdin accepted) takes the same append path through serve. With Sindri down it fails with `SND-NOTE-001` and prints the text back; it queues nothing. The CLI has no second write path by design, and `--direct` is the one explicit flag for it: the window uses it, and the owner may. Only the owner runs either form; a session is refused (E14).
- **Pre-filled capture** ("Note about this" on the Reviews page, a Home row or a context scope) is v1.1 (Open question 4). The global window stays one line.

### 4.2 Today's page

Opening Notes lands on today's page. If the file is missing, Sindri creates it from the daily template and says "Created from your daily template · 09:02" (`notes-today-new.png`).

- **File.** The date in `notes.dailyFormat` (default `YYYY-MM-DD`, so `2026-11-12.md`) in `notes.daily` (profile key, default `daily/`). When the vault has `.obsidian/daily-notes.json`, Sindri reads its `folder`, `format` and `template` once, read-only, and uses them as the defaults. That file is the one named exception to the `.obsidian/` deny (decision 1). A profile key set by the owner wins. Without this read, a vault whose Obsidian keeps dailies in `Journal/` as `12-11-2026.md` would get a second daily note on day one. The template is a plain `.md` file the owner names (`notes.template`). With no template, Sindri uses a built-in default with `## Today` and `## Captured`. Variables: `{{title}}`, `{{date:FORMAT}}`, `{{time}}` as Obsidian spells them. The file has no heading. A new page shows a ghost line under `## Captured`: "Lines from the capture window land here." The ghost line is not in the file. A capture that creates the file races Obsidian's daily-note plugin; if the file appeared meanwhile, the capture retries as an append.
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
- **Filter (`/`).** Searches the index: title, headings, tags, text. Operators `tag:`, `path:`, `about:owner/repo#123`, `kind:`. First result row under 100 ms at 1,000 notes. P2ui ships `/` as a title-only filter in the app; P5 owns the operators and the route behind them.
- **Open.** Enter opens the editor. `n` on the list, or Ctrl+N inside Notes, opens a new note in the folder of the selected note (else the notes root), cursor in the body. The todo note shows in the list and opens read-only (decision 26).

### 4.4 The editor and `[[`

The editor is the full view. The target is a Markdown editor with live rendering of headings, lists, tasks, tables, code and images. The control is not on main: E15 spikes AvaloniaEdit (MIT) with Markdig, with a NativeAOT check, before P2ui builds the live control. The go criteria: `Ui.AotSmoke` reports 0 IL2xxx and IL3xxx warnings, a 10,000-line paste takes under 300 ms, the caret does not jump when a chip swaps in or out, and N1b passes (CRLF, a BOM, tabs and a missing final newline survive). Until the spike says go, the editor is plain monospace text, chips show in the right pane and the peek, and the cursor-line rule of decision 12 does not apply. Live rendering draws no remote image (it shows the link text, so opening a note makes 0 network calls). A relative image or link target goes through `NotePath.Resolve` and the deny list. A click on a plain URL opens only `http` and `https`. Esc leaves the editor to the list. In the editor a bare letter types. Image paste waits for v1.1.

The editor saves by decision 25, and Ctrl+S saves now. The toolbar is one button, History, top right as the frames draw it, with its chord in the tooltip. Link it lives in the right pane and the palette. Make issue joins in v1.1.

- **`[[` popup.** Groups in this order, one row each: Notes, Issues, Pull requests. Recent first, then fuzzy over title and alias. A row shows the glyph, the id and the state word (`notes-link-complete.png`). The id reads `repo#id` when the profile has more than one repo, so two rows never both read "#3112". First rows under 50 ms per keystroke at 10,000 notes (in-memory title index). Enter inserts the link (§3.2). Threads, runs, scopes and files join in v1.1.
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

The pane collapses under about 1000 px of window width. A filter box (Ctrl+Shift+F) filters both mention lists; it is hidden when both are empty. Empty: "Nothing links here yet." (`notes-backlinks-empty.png`).

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
| States | v1 draws the legend's nine in `notes-live-chips-legend.png`: open, draft, merged, closed without merge (own slashed-circle glyph, never the green check), issue closed, CI running, CI red, In Progress, Todo. "Unknown" is drawn by `notes-live-chips-notlive.png`, so v1 draws 10 of the 12 states. Run failed and run passed arrive with run links in v1.1 |
| Update | within 1 s of the ledger event; 0 network calls on open (ledger 9) |
| Click | the chip opens the peek of its object; Ctrl+Enter on a focused chip does the same from the keyboard. No other action in v1 |
| Cursor line | raw text, no chip |
| Not live | decision 13 |

Not live means Sindri is down, or the row's `readAt` is older than the source's stale limit (Home's stale-source rule, Home §5.5 and §7.3). The page header says "Not live: Sindri isn't running. States as of 14:02." and the top bar says "Not connected".

### 4.8 Make issue (v1.1)

This section waits for v1.1 (lead call 11; *owner can flip*, Open question 12). It is written out so the frames and tests stay ready. Select a line (or put the cursor on it) and press Ctrl+Shift+I, or press the toolbar button. A bar under the line shows the target rows, the title (the line's text, in an editable field) and the note "replaces the line with its chip" (`notes-make-issue-picker.png`, which design reshoots: it is a menu today, with no button). Enter in the title moves focus to the button; only Enter on the button, or a click, creates. One issue comes from one line. Make-all on an `## Actions` section follows it (Open question 6).

- **Default target.** The tracker the profile sets (Linear, team EXP in the frames). GitHub is offered only for a repo of the profile; a linked PR in another repo never becomes the target, so one Enter cannot open a public issue in someone else's project. The bar shows the chosen target. Written to the lead's pick (Open question 5).
- **Title check.** `Aw.Redaction` scans the title. A hit shows "Looks like a token. Check the title." in the bar. It does not block; the owner decides.
- **Create.** One button that names the target: "Create issue in Linear" or "Create issue in acme/api". It is a write to an outside source, so it is a button and an Enter on that button, never a bare letter. The button disables on the first press until the route answers.
- **Order of work.** The route takes the lock and checks `baseSha256` before the outside call, and refuses with `SND-NOTE-018` if the note changed. It passes an idempotency key to the tracker where the tracker takes one. If the issue is created but the rewrite fails, the response carries `IssueId` and `Url`. The bar then says "Issue acme/api#3115 was created, but the line couldn't be replaced." (`SND-NOTE-024`), and its only action is "Replace the line". There is no second create.
- **Done.** The line is replaced by the new issue's chip. For GitHub the file holds `[[owner/repo#N]]`. For Linear the file holds the issue's URL, the plain-URL form of §3.2, which the index reads as an `about` link, and the chip shows "EXP-203" (Linear ids are team and number; the frame's `#3203` is fixed). Undo (Ctrl+Z, 8 s, while focus is on the new chip; the toast's button works anywhere) (`notes-make-issue-done.png`). Undo restores the line; it does not delete the issue (the toast says "Issue stays in Linear"). The History row for the change links the issue.
- **Errors.** "Linear isn't connected. Connect it in Settings, then try again." (`notes-make-issue-noconn.png`). "Couldn't create the issue. The line stays as you wrote it. Check your connection, then try again." with the button "Try again" and the code under Details (`notes-make-issue-fail.png`). The line stays as written in both. With Sindri or the tracker down the button is off and nothing is queued (decision 18). Check N12 covers all of this.

### 4.9 History

Ctrl+Shift+H (or the toolbar button) opens History from the editor; Esc returns. History is a full view with a 340 px version list on the left and the diff on the right.

- **List.** Time, what made it (edit, capture, tick, restore, link; v1.1 adds Make issue) and +/- line counts, newest first. The header counts versions ("13 versions"). The newest saved version reads "Current" and has no Restore.
- **Restore.** Select an older version: the diff compares it with the file now, the header says "Restoring writes this version as a new version. Nothing is lost.", the one primary is "Restore this version" (`r`). (`notes-history-restore.png`).
- **Footer by selection:** an older version: `j/k`, `r`, Esc, Ctrl+K. Current, empty, error: Esc and Ctrl+K only.
- **States.** Loading "Reading versions" (`notes-history-loading.png`); empty "No earlier versions." (`notes-history-empty.png`); error "Couldn't read this note's history. Check the notes folder is reachable." with the button "Open notes folder" and `SND-NOTE-012` under Details (`notes-history-error.png`).

**What History keeps.**

1. **Content-addressed.** A version is a copy whose file name is the sha256 of its bytes, so the same bytes are stored once. Copies sit under `$AW_SINDRI_HOME/notes-history/<vault>/<hash of the note path>/`.
2. **A 60 s floor.** The editor's autosave makes at most one snapshot per 60 s of editing. Every other write (capture, tick, restore, Make issue, link) makes its own snapshot at once. The store half (the copy, the modes, the write) is built in P2, because the conflict rules need it; P7 builds the view, restore and pruning. A conflict snapshots `CameOut` (decision 3), never the earlier read.
3. **Pruning.** Sindri keeps every version for 30 days, then one per day for the next 90 days, then drops the rest. A pass runs at start and once a day. Two caps also apply: 200 versions per note and 1 GiB for `notes-history/`, oldest first inside the daily tier. `sindri doctor` warns near the total cap. The owner can delete one version ("Delete this version", local, no outside write), so a pasted token does not stay for 120 days.
4. **Modes.** Directories are 0700 and files 0600. `sindri doctor` warns when one is looser.
5. **Reads re-check.** `history` and `show` apply the current deny and retrieval rules on every read, not the rules at save time, on the note's current path after the `previous_paths` alias resolves. A note moved from `eng/` into `people/` and asked for as `eng/x.md` is checked as `people/x.md`. A note moved into a denied path drops its history from a session's reads. A deleted note keeps its history, read-only, under a "Deleted notes" filter until the prune drops it. A `version` is exactly 64 lowercase hex characters, and `<vault>` must be a configured vault name.

**Agent proposals (v1.1).** The rest of this section waits for v1.1.

- A proposal is a row "proposed edit, not in the file yet" with a band: "Proposed by claude-code, session 7f3a, at 14:21. It is not in the file yet. The file stays at the 14:05 version until you accept." and "Your rule for eng/: agents propose, you accept. Change rule" (a link to the Settings row). Accept (`a`, primary) writes it; Decline (`d`) drops it. There is no Undo: a proposal is not in the file, and Restore on the accepted row is the undo (`notes-history.png`). The band shows the full proposed text, never a truncated diff under 20 lines. The header reads "13 + 1 proposed". The list shows what made each version ("you" alone is not enough: edit, capture, tick, restore, link). The current row reads "Current" everywhere. The list's "who" shows "claude-code, session 7f3a" as a link to the agent session.
- A proposal lives in `$AW_SINDRI_HOME/notes-proposals/<id>.json` (directory 0700, file 0600), outside the vault, so the index never reads it as a note. An edit proposal carries a base sha256. An append proposal stores the intent (`append: text`, `section`) and Accept applies it to the current file, so a capture does not make it stale. A proposal for a missing file is a create proposal.
- Stale edit proposal (the sha differs): Accept is off, Decline is primary, and the band says "The note changed after claude-code wrote this. Decline it and ask the agent again." (`notes-history-stale.png`).
- `propose` needs the folder's read switch on, since it needs `show`; otherwise `SND-NOTE-011`.
- Footer for a proposal: `j/k`, `a`, `d`, Esc, Ctrl+K. After Accept or Decline, focus moves to the next version row.

### 4.10 Conflict and save failure

- **File changed on disk** (Obsidian, git, a sync tool, or Sindri's own capture, tick or Link it) after the editor opened. The editor listens to the watcher and to `note.*` events and follows three rules:
  1. **Clean buffer.** Reload silently and keep the caret. Sindri's own capture never raises a conflict on the open daily page. The editor remembers the sha256 of its last successful write and ignores any watcher event or `note.*` event with that hash, so its own autosave never runs a merge.
  2. **Dirty buffer.** Merge by line against the version the editor opened. A capture under `## Captured` merges cleanly.
  3. **The same lines differ.** Show both versions side by side with a word per differing line ("ticked in the file") (`notes-today-conflict.png`). "Keep my line" and "Keep the file's line" are both secondary buttons, with no default, and both columns carry a word ("you edited it", "ticked in the file"). Focus goes to the first differing line. The version you did not keep goes to History. Design reshoots the frame without the primary button and with these labels.
  Codes: `SND-NOTE-018` is the only code a CLI or MCP caller sees for a changed file (a stale `baseSha256`). `SND-NOTE-004` is only the editor's conflict view; both a stale base and a mismatch found after the swap lead into it. A capture names no base, so a mismatch after its swap reports `SND-NOTE-003` and the line stays in the draft, never 004.
- **Save failed:** "Couldn't save today's note. Your text is kept here. Check the folder is writable, then press Ctrl+S." with the button "Open notes folder" and the code under Details. The text stays in the editor and in its draft file (`notes-today-savefail.png`).
- **Sindri not running:** the file still opens as read-only text with the last index; chips and the fold are muted; one sentence and "Start Sindri" (`notes-today-down.png`, whose banner reads "and saves" today; design reshoots it to the sentence of `SND-NOTE-001`). Decision 27.

### 4.11 States

| State | What shows |
|---|---|
| Loading list | rows paint from the local index under 100 ms; with a cold index "Reading 214 notes" (`notes-list-loading.png`; the tab counts come from the index) |
| No notes folder | "Notes are Markdown files in a folder you choose. Plain files. Sindri keeps no hidden data in them." and one primary, "Choose notes folder" (`notes-list-empty.png`; the frame's "Sindri adds nothing to them" is false after the first capture). The capture switch is hidden |
| Notes folder set but gone | "Your notes folder isn't there. Check it is connected, or choose another." and the same primary. Not the "No notes folder yet" sentence |
| Todo note open | the editor's banner slot: "Sindri's todo list writes this note. Edit it from Todo." The body is read-only |
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

- **Notes folder.** Choose or change. One job with two steps: add the folder to `context.sources.library.vaults` (name from the folder; the `SND-LIB-001` checks) when it is not there yet, then write `notes.vault` and `notes.folder`. A refusal names the step that failed. A bad value is `SND-NOTE-002` with the key path. The vault's name is the folder's last segment, lower-cased. On a collision with a name already in `context.sources.library.vaults`, `setup` refuses, names the existing vault and answers `--name N`. CLI twin: `sindri note setup <path> [--name N] [--folder SUB]`. `sindri setup` lists "Notes folder" as a step with id `notes`: `NeedsYou` until `notes.vault` is set, `Done` after, and optional, so a person who never uses Notes does not see `sindri setup` exit 1. The step adds a `SetupStepId` and a branch in `Core/Setup/SetupSteps.cs`, with its golden. Its second line is "Start Sindri at login".
- **Start Sindri at login.** One button, offered once a notes folder is set. It writes the OS's login entry for `sindri-ui` (Linux `~/.config/autostart/sindri-ui.desktop`, macOS a LaunchAgent in `~/Library/LaunchAgents`). The entry calls the `sindri-ui` wrapper at `bin/cs/current/sindri-ui`, never a stamped build folder (the next install would break it), and holds no environment values. Nothing turns it on silently. `sindri setup` is read-only and only prints the step. The write is this button (the app, a human) or `sindri note autostart on|off` (terminal only, refused for a session). The row says "On" and offers "Turn off", the only remover; `./setup.sh --dry-run` prints nothing about it. The capture hotkey works after a reboot only while it is on or `sindri-ui` is running (N2).
- **Agents can read this folder.** One row per top-level folder of the vault, plus "Other folders" (the frame's eng/, daily/, people/ and design/ are fixture names, and `notes-settings.png` still needs a read-switch row drawn). The switch writes `notes.rules` (default off); context derives its `retrieval` from it. Terminal: `sindri note access <folder> --read on|off`. Turning a folder off removes its chunks from context's own store too (E9). The per-folder write-rule dropdown in the frame ("Agents propose, you accept", "Agents can't edit") waits for v1.1.
- **Capture hotkey.** Shared with #3170. A refused hotkey: "The OS refused this hotkey." and "Pick another key"; the tray item still works.

The profile keys apply on save; `sindri profile set notes.vault <name>` does the same from a terminal. `sindri profile set` refuses a list, so `notes.rules` is edited only by Settings and `sindri note access`.

### 4.13 Keyboard and focus

Grammar keys are Home's: `j/k`, arrows, `Home`/`End`, `/`, `Space`, `Enter`, `Esc`, `.`, `?`, `Ctrl+K`, `Left`/`Right` to fold. A bare letter below changes local state or opens something cancellable. None writes to an outside source (decision 18).

| Key | Where | Does | Writes? |
|---|---|---|---|
| `n`, `Ctrl+N` | list; Notes | new note | creates a file on first save |
| `Ctrl+S` | editor | save now | the file |
| `p` | list | pin or unpin the row | local |
| `Ctrl+K`, `note:` | anywhere | the palette scope | no |
| `[[` | editor | link popup | the link, when chosen |
| `Ctrl+Shift+L` | editor, pane | Link it (an unlinked mention) | the other note, one edit |
| `Ctrl+Shift+F` | editor | filter the mentions | no |
| `Ctrl+Enter` | on a chip | open the object's peek | no |
| `Ctrl+Shift+H` | editor | History | no |
| `r` | History | restore, on an older version | the file |
| `y` | list peek | copy link | no |
| `Esc` | editor | leave the editor | no |
| `Ctrl+Z` | editor | text undo in the body (main refuses toast Undo in a text field). The toast's button works anywhere; Ctrl+Z reaches a toast's Undo only while focus is on the chip it made | local |
| `Ctrl+Alt+I` | global | the capture window | no |

v1.1 adds `Ctrl+Shift+I` (Make issue; opens the bar, and Enter on the button writes), `g d` (open today, anywhere except a text field), `g p` / `g n` (list: previous / next existing day), `Alt+PageUp` / `Alt+PageDown` (editor: previous / next existing day) and, in History, `a` / `d` (accept / decline a proposal).

Every action has a menu item and a palette entry under one id (`notes.open`, `notes.today`, `notes.capture`, `notes.undo-capture`, `notes.history`, `notes.restore`; v1.1 adds `notes.make-issue`, `notes.accept`, `notes.decline`). The editor toolbar has the History button. The `?` sheet lists the keys of the screen it is on.

**Focus contract.**

1. Opening a note puts focus in the body at the saved caret. Esc returns to the list row that opened it.
2. After a capture, the window closes and focus goes back to the app that had it. The toast takes no focus (decision 8), so its Undo has the palette route `notes.undo-capture`.
3. The `[[` popup takes the arrows, Enter and Esc; Esc closes the popup, not the editor.
4. After Restore, focus is on the new Current row. (v1.1: after Make issue, focus is on the new chip; after Accept or Decline, focus moves to the next version row.)
5. A conflict view puts focus on the first differing line. No button is the default.
6. Esc closes the topmost layer only: popup, History, then the editor (v1.1: the make-issue bar sits above History).
7. Bare letters fire only on the list and in History; in the editor and in any field they type.
8. Chip state changes and "Saved" are announced through one polite live region. Each outcome is also in a focusable element.

**Names.** A chip reads "pull request 3104, merged, retry upload". A not-live chip adds "as of 14:02". A list row reads "daily note, Thursday 12 November, 3 links, edited 27 minutes ago". (v1.1: a proposal row reads "proposed edit by claude-code, not in the file yet".) Reduced motion turns toasts and the fold's open and close into cuts.

## 5. Visual bar

Frames are design's round 4 at `d0c2fd7d2e`. The type scale, density (32 px rows), and the Inspora bar come from the shared grammar (#2978). Text is 4.5:1 or more in both themes; non-text marks (the checkbox border `#70757c`, chip edges, glyphs) are 3:1 or more. A state is a glyph and a word, never color alone. The new page glyph has a dark and a light version.

Frames to reshoot (design lane, X1). A builder follows the frame, so a frame that still draws a v1.1 part is listed with what builders leave out:

| Frame | Change |
|---|---|
| `notes-capture-bar-issue.png`, and #3170's Issue frames | primary "Create issue in Linear"; footer "Ctrl+Enter create" |
| `notes-capture-saved.png`, `notes-capture-no-folder-no-switch.png` | top label "New note" (they say "New issue"); footer "Esc drop"; the no-folder caption is a reviewer caption, not UI |
| every capture frame | footer words match §4.1: "Esc drop", "Ctrl+Tab Issue / Note" |
| `notes-capture-error-down.png` | replaced by the "Saved without Sindri" toast |
| `notes-today-conflict.png` | no primary button; buttons "Keep my line" and "Keep the file's line"; a word on both columns |
| `notes-today.png`, `notes-today-new.png`, `notes-today-down.png`, `notes-today-savefail.png` | v1.1 parts drawn: the "Moved since yesterday" group and the `Alt+PgUp`, `Alt+PgDn` and `g d` footer keys; builders leave them out. `notes-today-new.png` shows the ghost line of §4.2. `notes-today-down.png`: banner reads the `SND-NOTE-001` sentence, not "and saves" |
| `notes-list.png` | v1.1 parts drawn: the "proposed edit" cell and the Proposal peek row; builders leave them out. Footer `n new note` stays |
| `notes-history-restore.png` | v1.1 parts drawn: "13 + 1 proposed" and an agent row; builders leave them out. Rows name what made each version; the current row reads "Current" |
| `notes-list-empty.png` | the sentence of §4.11; a second frame for "folder gone" |
| `notes-link-complete.png` | `repo#id` in the id column when the profile has more than one repo |
| `notes-live-chips-notlive.png`, `notes-today-down.png` | dimmed glyph and edge at 3:1 or more, a dashed edge and a hollow glyph (decision 13) |
| `notes-settings.png` | the "Start Sindri at login" row; the "Agents can read this folder" switch row drawn (no frame has it today); the write-rule dropdown marked v1.1 and left out by builders |
| `notes-settings-hotkey-refused.png`, #3170's frame | one string, "Pick another key", and the line "The tray item 'New issue or note' still works." (also added to `SND-NOTE-017`) |
| `notes-make-issue-picker.png`, `notes-make-issue-done.png` (v1.1, no reshoot in v1) | picker: title field, target rows, a primary button that names the target. Done: the Linear URL in the raw text and the chip "EXP-203"; for GitHub `[[owner/repo#N]]` |
| every frame with `[[#3112]]`, and the three today frames that show bare `#3109` | `[[acme/api#3112]]`, `[[acme/api#3109]]` |

## 6. Errors, in words

### 6.1 One table, one test per row

Each failure maps to `{code, sentence, fix}`. The mapping lives in one catalog file in Core so the app, the CLI and MCP print the same words. The code shows under Details. Where a frame at `d0c2fd7d2e` shows the words, the table uses the frame's words. "Kind" is `error` or `state`: the catalog test asserts a fix only for an error. Exit codes follow the context family (`docs/sindri/context.md`, its exit-code section): `0` ok, `1` attention (a refusal, Sindri not running), `2` this input can't be written. `SND-NOTE-002`, `008` and `022` exit 2; every other refusal exits 1.

| Code | Kind | Sentence | Button / command |
|---|---|---|---|
| `SND-NOTE-001` | error | "Sindri isn't running. Chips and the brief can't update. The note opens read-only." (page) / "Sindri isn't running, so nothing was added. Your text is below." (CLI `add`) | Start Sindri / `sindri serve`. The capture window does not use this code: it saves without Sindri (decision 7) |
| `SND-NOTE-002` | error | "No notes folder yet. Choose one in Settings, then press Enter." (capture) / "{key} is not a vault of your profile." | Choose notes folder / `sindri note setup <path>` |
| `SND-NOTE-003` | error | "Couldn't write to today's note. Your line is kept. Check the folder is writable, then press Enter." | Open notes folder / none |
| `SND-NOTE-004` | error | "The file changed on disk after you opened it. Another app wrote to it (Obsidian, git or sync). Pick the version to keep. The other stays in History." (the editor's conflict view only; a CLI caller sees 018) | Keep my line, Keep the file's line (no default) / none: the CLI has no `keep`; `sindri note show <path>` reads the file now |
| `SND-NOTE-005` | error | "Couldn't save {file}. Your text is kept here. Check the folder is writable, then press Ctrl+S." ({file} reads "today's note" for a daily note) | Open notes folder / `sindri note show <path>` |
| `SND-NOTE-006` | error | "{file} has more than one hard link, so Sindri won't write it." | Open on disk / none |
| `SND-NOTE-007` | error | "{file} is inside a folder your deny list hides." | Change the deny list / none |
| `SND-NOTE-008` | error | "A note called {title} already exists here." (create) | Open it / none |
| `SND-NOTE-009` | state | "Undo is off: the note changed after the capture." | none |
| `SND-NOTE-010` | error (v1.1, P8) | "The note changed after {agent} wrote this. Decline it and ask the agent again." | Decline / `sindri note decline <id>` |
| `SND-NOTE-011` | error (v1.1, P8) | "Agents can't edit {folder}." (a proposal into a folder with rule "can't edit", or with read off) | Change rule / none |
| `SND-NOTE-012` | error | "Couldn't read this note's history. Check the notes folder is reachable." | Open notes folder / `sindri note history <path>` |
| `SND-NOTE-013` | state | "No earlier versions." (listed so the catalog test covers the sentence) | none |
| `SND-NOTE-014` | error (v1.1) | "Linear isn't connected. Connect it in Settings, then try again." (make issue) | Open Settings / `sindri connection add linear:<label> --preset linear --client-id <id>` |
| `SND-NOTE-015` | error (v1.1) | "Couldn't create the issue. The line stays as you wrote it. Check your connection, then try again." | Try again / none |
| `SND-NOTE-016` | error | "{repo} isn't a repo of your profile, so Sindri can't show this link's state." (unknown chip) | Add the repo in Settings / `sindri repo add <path>` |
| `SND-NOTE-017` | error | "The OS refused this hotkey. The tray item 'New issue or note' still works." | Pick another key / none (shared with #3170) |
| `SND-NOTE-018` | error | "A write names an old version of this note." (409) | Reload / `sindri note show <path>` |
| `SND-NOTE-019` | state | "Nothing to save. Type a line, or press Esc to close." (empty capture) | none |
| `SND-NOTE-020` | error (v1.1) | "Sindri or the tracker is down. Nothing was sent." (Make issue offline) | Start Sindri / `sindri serve` |
| `SND-NOTE-021` | state | "The note is over 1 MiB, so Sindri shows it as text only." | Open on disk / none |
| `SND-NOTE-022` | error (v1.1) | "Attachment {name} is over 25 MB." (with image paste) | none |
| `SND-NOTE-023` | error | "Sindri is busy. Your line is kept." (the capture window with a live host lease and a failed call) | Try again / none |
| `SND-NOTE-024` | error (v1.1) | "Issue {id} was created, but the line couldn't be replaced." (Make issue) | Replace the line / none |

024 is the last reserved; 025 to 029 stay free. To a session, a denied path and a missing path both answer "not found"; only the owner sees `SND-NOTE-007`. `Aw.Sindri.Tests/Notes/ErrorCatalogTests.cs` holds one test per row: it asserts the sentence, and the fix for an error. The frames' placeholder codes (`NOTES-WRITE-001`, `NOTES-HISTORY-001`, `LINEAR-CREATE-502`) change to these in the frames' Details line. The 1 MiB and 25 MB limits are written to the lead's pick (Open question 7). A symlink is not followed, so it has no write-refusal code (§3.1).

## 7. Agents

Everything the page does has one verb on the CLI, `sindri note <verb>`. In v1 a session may run four of them, and they also exist over MCP at `/sindri/mcp` as `note_list`, `note_show`, `note_search` and `note_history`. The owner verbs have no MCP tool on purpose: a session is refused anyway. A doc test checks that this list and the table below agree. Paths are vault-relative, use `/`, and match case exactly (`eng/retry-policy-for-exports.md`). The CLI sends them URL-encoded.

The Phase column says which phase of §13 builds the verb with the route and store it needs. A builder builds the verbs of the phase it owns and no others.

| Verb | Phase | Arguments | Example | Who | Refused with |
|---|---|---|---|---|---|
| `list` | P1 | `[--folder F] [--kind daily\|linked\|note] [--tag T] [--about owner/repo#N] [--max N] [--json]` (`--json` prints each note's `pathHash`) | `sindri note list --kind linked --about acme/api#3112 --json` | owner, session | 002 |
| `show` | P1 | `<path> [--json]` | `sindri note show eng/retry-policy-for-exports.md` | owner, session | 002, 007 |
| `reindex` | P1 | `[--json]` (rebuilds `notes-index.db`; the fix the doctor row prints) | `sindri note reindex` | owner | 002 |
| `add` | P2 | `<text>` or `--stdin`, plus `--direct` (the capture helper, §4.1) | `sindri note add "[ ] call Priya"` | owner | 001, 002, 003, 023 |
| `create` | P2 | `<path> [--text T \| --stdin]` | `sindri note create eng/idea.md --text "# Idea"` | owner | 002, 007, 008 |
| `delete` | P2 | `<path>` (moves to `.trash`) | `sindri note delete eng/idea.md` | owner | 002, 007 |
| `tick` | P2 | `<path> --line N --base <sha256>` (1-based; the line must be a task; `--base` comes from `show --json`) | `sindri note tick daily/2026-11-10.md --line 14 --base 9f2c...` | owner | 002, 007, 018 |
| `setup` | P1 | `<path> [--name N] [--folder SUB]` | `sindri note setup ~/notes --name notes` | owner | 002 |
| `access` | P1 | `<folder> --read on\|off [--json]` (writes `notes.rules` through the Settings job) | `sindri note access eng --read on` | owner | 002 |
| `autostart` | P3 | `on\|off` (terminal only) | `sindri note autostart on` | owner | 002 |
| `pin` | P2ui | `<path> [--off]` | `sindri note pin eng/retry-policy-for-exports.md` | owner | 002 |
| `link` | P4 | `<path> --line N --title T --base <sha256>` (Link it: wraps the first unlinked mention of T on that line) | `sindri note link people/priya.md --line 3 --title "Retry policy for exports" --base 9f2c...` | owner | 002, 007, 018 |
| `search` | P5 | `<query> [--max N] [--json]` | `sindri note search "retry cap" --max 5` | owner, session | 002 |
| `history` | P7 | `<path> [--json]` | `sindri note history daily/2026-11-12.md` | owner, session | 002, 007, 012 |
| `restore` | P7 | `<path> --version <sha256> --base <sha256>` | `sindri note restore daily/2026-11-12.md --version 9f2c... --base 41ab...` | owner | 002, 007, 012, 018 |
| `make-issue` (v1.1) | P6b | `<path> --line N --base <sha256> --target linear:<team>\|github:<owner/repo>` | `sindri note make-issue daily/2026-11-12.md --line 9 --base 9f2c... --target linear:EXP` | owner | 002, 014, 015, 018, 020, 024 |

v1.1 adds `propose <path> --base <sha256> (--diff F \| --append T)` for a session, with `--diff` a unified diff and `--append` naming the section by `--section` (default `Captured`), and `accept` and `decline` for the owner (P8, #3207). Conflict resolution (`keep`) exists only in the UI: it needs the buffer a session cannot have. A verb that names a line takes `--base`, so a file edited between the CLI's read and its write is refused with 018 instead of ticking the wrong task.

Rules:
- A session reads a folder only when its retrieval switch is on. A denied path is hidden from every read, as it is from code. The check runs per result, not per query, and on every field that carries text from another note: a backlink or mention paragraph, `firstLine`, counts, search lines and a history diff. A hidden source is dropped, and counts count only visible rows. A link from an `eng/` note to a note in `people/` shows the link text but not the target's snippet or paragraph. A note in `people/` that links an `eng/` note does not appear in the `eng/` note's backlinks for a session.
- Note text in every result is fenced as untrusted, over MCP and in `--json`. So are chip and linked-object titles, which come from Linear and GitHub. A session's `show`, `history`, `search` and `list` text also passes `Aw.Redaction` after the deny check (it masks, it does not drop).
- A session writes nothing in v1. The owner verbs above are refused with `SND-AUTH-002` for a session, including `add` and `add --direct`.
- `--json` carries keys, text carries words; both follow the context family's rule (`docs/sindri/context.md`, its output section). Exit codes are in §6.
- The context lane never writes a note. It reads `notes.vault` through the `notes` source and cites chunks as `note:<path>#<heading>`.

## 8. API and engine requirements

One route family under `/sindri/notes`, named in P0. Every write is synchronous: the file write is the commit, then Sindri emits a `note.*` event. There is no outbox, because offline never queues. The path is a query or body field, URL-encoded, vault-relative, case-sensitive, with no `..`, so a note called `history` never collides with a route. An error body is the same `{code, sentence, fix}` triple the CLI prints, so a client needs one parser. The `GET` routes are queries: they read `notes-index.db` and the files through a new read port, `INoteIndexReader`, never `ILedgerReader` (ARCH-CQRS-002).

| Route | Does |
|---|---|
| `GET /sindri/notes?kind=&folder=&q=&about=&after=` | rows, counts per tab, `degraded[]` |
| `GET /sindri/notes/file?path=` | text, sha256, links, backlinks (linked, unlinked), linked objects with chip state |
| `GET /sindri/notes/history?path=`, `GET /sindri/notes/history/version?path=&version=` | versions; the diff of one version against the file now |
| `GET /sindri/notes/chips?ids=` | live chip state from the local store (#3021's lookup) |
| `POST /sindri/notes/ops` | `{intent, path, baseSha256, ...}`: edit, create, delete, tick, link-mention, restore, pin, undo-capture; a stale base is `SND-NOTE-018`. ARCH-CQRS-001 wants one command per POST, so this route binds one `NoteOpCommand` with an `Intent` variant, and the command catalog (`Core/Commands/CommandRow.cs`) has one row per intent (`note.edit`, `note.create`, `note.delete`, `note.tick`, `note.link-mention`, `note.restore`, `note.pin`, `note.undo-capture`), each in the owner-only class (E14) |
| `POST /sindri/notes/capture` | the append; creates today's file; returns what Undo needs. Takes a command id as an idempotency key: a retry after a timeout never appends twice |
| `POST /sindri/notes/make-issue` (v1.1) | `{path, line, target, title, baseSha256}`; base check, then the Linear or GitHub call, then the line rewrite (§4.8) |
| `POST /sindri/notes/setup` | `{path, name?, folder?}`: add the vault if needed, set `notes.vault` and `notes.folder` |

v1.1 adds `POST /sindri/notes/make-issue` (P6b) and, in P8, `POST /sindri/notes/propose` and `POST /sindri/notes/decline`.

P0 freezes only the route and record shapes below; the index internals (tables, watcher) are P1's, so a failed spike (N5) does not reopen P0. P0 writes these records as stubs (names only; fields in order). One request and one response per route, as the slice recipe wants.

| Route | Request | Response |
|---|---|---|
| list | `ListNotesRequest(Kind, Folder, Query, About, After)` | `ListNotesResponse(Rows[], Counts, Degraded[])` |
| file | `ReadNoteRequest(Path)` | `ReadNoteResponse(Path, Text, Sha256, Links[], LinkedMentions[], UnlinkedMentions[], LinkedObjects[])` |
| history | `NoteHistoryRequest(Path)`, `NoteVersionRequest(Path, Version)` | `NoteHistoryResponse(Versions[])`, `NoteVersionResponse(Version, Diff)` |
| chips | `NoteChipsRequest(Ids[])` | `NoteChipsResponse(Chips[])` |
| ops | `EditNoteRequest(Path, BaseSha256, Edits[])`, `CreateNoteRequest(Path, Text)`, `DeleteNoteRequest(Path, BaseSha256)`, `TickTaskRequest(Path, Line, BaseSha256)`, `LinkMentionRequest(Path, Line, Title, BaseSha256)`, `RestoreVersionRequest(Path, Version, BaseSha256)`, `PinNoteRequest(Path, Pinned)`, `UndoCaptureRequest(Path, UndoToken)`; `RestoreVersionRequest.Version` is exactly 64 lowercase hex | `NoteWriteResponse(Path, Sha256, VersionId)` |
| capture | `CaptureRequest(Text)` | `CaptureResponse(Path, Sha256, UndoToken)` |
| make-issue (v1.1) | `MakeIssueRequest(Path, Line, Target, Title, BaseSha256)` | `MakeIssueResponse(IssueId, Url, Sha256)` (`Sha256` empty when the rewrite was refused) |
| setup | `SetupNotesRequest(Path, Name, Folder)` | `SetupNotesResponse(Vault, Folder)` |

| # | Need | Owner lane | Notes |
|---|---|---|---|
| E1 | Profile keys `notes.vault`, `notes.folder`, `notes.daily`, `notes.dailyFormat`, `notes.template`, `notes.attachments` (v1.1), and `notes.rules`, a list of `{folder, read, write}` objects: the one store of the agent read switch, edited by Settings and by `sindri note access` (a dotted key with a folder name in it would break `sindri profile set`, which refuses a list); the nearest folder wins and "Other folders" is the default; `write` is unused until v1.1; validation reuses the vault checks; `SND-NOTE-002`; the `setup` job (§4.12); the `notes` setup step (id, `SetupStepId`, `SetupSteps.cs` branch, golden; optional) | api | Settings writes the same keys; docs name `docs/sindri/profile.md` too |
| E2 | Index into `$AW_SINDRI_HOME/notes-index.db`: one row per `.md`, text chunked by heading, FTS5 (pins are in `notes-state.json`, decision 2); `sindri note reindex`; start walk (mtime + size first, hash on change); watcher (inotify, FSEvents) on directories only, a rescan every 10 minutes, a burst of events is one reindex per file after 300 ms of quiet; a delete plus a create of the same (size, mtime, hash) in one scan is a rename, and the row keeps the old path in `previous_paths` so chips, edges and history follow (the deny and retrieval checks run on the current path); a `notes` doctor warning when the inotify limit is reached ("watcher off: limit reached, rescanning every 10 min"; its fix is `sindri note reindex`) | api | the 1,000-note spike posts its number on #2981 before the watcher design is fixed |
| E3 | The swap write op of decision 3 (the cross-process lock, `ITodoNoteFiles.Swap`, the check of `CameOut`, intent and `Leftover` recovery), the conflict rules (§4.10), `.trash` delete, the History store half (§4.9), byte-clean round trip test on the fixture vault. One function, `NotePath.Resolve(vault, relative)`, rejects `..`, an absolute path, NUL and backslash, matches the filesystem's case, refuses a symlinked segment and matches deny globs case-insensitively; every route and verb uses it, with a table test of 15 cases (the cases are listed in `docs/csharp/sindri-notes.md`, and the `version` regex and the vault name join them). It builds on `INoteLocator`, `ITodoNoteDisk` and `ITodoNoteFiles` | api | N1 |
| E4 | Daily note and template; capture append (`NoteAppend` in Core, called by serve and by the helper `sindri note add --direct`); the palette's "Create note" write | api | #3034 and #3035 ship together (api plan) |
| E5 | Link index (`links`, backlinks, unlinked mentions); `[[` popup source over the index plus context's edges for issues and PRs | api, context | #3036 |
| E6 | Search route and the Ctrl+K `note:` source | api, ui | #3037; reads the index, never the disk |
| E7 | Chip lookup and `about` edges (a guessed link makes none), the reverse walk for object pages; Make issue is v1.1 | api, context | #3038; needs #3021's lookup |
| E8 | Versions in `$AW_SINDRI_HOME/notes-history/` with the policy of §4.9. The store half (copy, modes, write) is P2; the view, restore, pruning, caps and "Delete this version" are P7. v1.1 (P8): proposals and the per-folder write rule | api | #3039 |
| E9 | Context's `notes` source: `context.sources.notes` reads `notes.vault`; `retrieval` per folder, default off, derived from `notes.rules`; turning a folder off purges its chunks and embeddings from context's store, with a test that a query finds nothing after the flip; chunks and links come from `INoteIndexReader` (decision 2) | context | accepted on #2981; first PR is the source on the library feed's walker |
| E10 | Events `note.captured`, `note.saved`, `note.restored` join the catalog (v1.1: `note.issue.made`, and in P8 `note.proposed`, `note.accepted`, `note.declined`). Each is `Public: true` with a small `PublicPayload` record: the vault name and a hash of the note path, never the path, title or text; the path is resolved from the index. Dedupe keys are `note.captured:<command id>` and `note.saved:<command id>`, so two identical lines both land; each row names its `Invalidates` list (the ApiViews the app refetches). No `ledger schema` line moves and no views migration is needed | api | `Aw.Redaction` scrubs any title that reaches a log line |
| E11 | Failure-to-sentence catalog (§6) in Core | api | one test per row, in `Aw.Sindri.Tests/Notes/ErrorCatalogTests.cs` |
| E12 | Global hotkey on the tray; the shared window's Note kind and the start of `sindri note add --direct` when Sindri is down (a `cli` edge in `deps.yaml`, no Core reference); the Wayland portal path; the "Start Sindri at login" entry and Settings row (Linux autostart file, macOS LaunchAgent, §4.12) | ui | depends on #3170's window and on P2 |
| E13 | `sindri doctor` row `notes`: `warn` when the index is older than 10 minutes, the watcher is off (fix `sindri note reindex`), a history or draft directory is looser than 0700 (file than 0600), `notes-history/` nears its 1 GiB cap, or draft files pile up (the row counts them); `fail` when the vault path is missing or unreadable, with the fix `sindri note setup <path>`. `--json` shows the vault name, never its path. The PR updates `src/Sindri/aot-smoke/expected.txt` | api, tools | any doctor change updates the expected file |
| E14 | The session rule. `ActionClass` gets a new owner-only value, allowed for `Terminal` and `App` only; the change touches `ActionClass`, `Authorization._table`, `Authorization.CallersFor` and the catalog docs. Every `note.*` write command is in it, one catalog row per `ops` intent plus `capture`, `make-issue` (v1.1) and `setup`. Reusing `Desk` would let a session write a note. A session may call only the read routes: `GET /sindri/notes`, `GET /sindri/notes/file`, `GET /sindri/notes/history`, `GET /sindri/notes/history/version` and `GET /sindri/notes/chips`. Each applies the deny and retrieval rules per result and per field (§7). Every `POST /sindri/notes/*` route refuses a session with `SND-AUTH-002`. N8 holds one test per route and the catalog walk. A session sees only the `PublicPayload` of `note.*` events, which is how `EventCatalog` already works. In P8, `propose` joins the callable list and creates a proposal only | api | as Home §8 |
| E15 | Editor control spike: AvaloniaEdit (MIT) with Markdig, a NativeAOT check on `sindri-ui` (`Ui.AotSmoke`), the cursor-line chip swap and the 10,000-line paste. It ends in a go or no-go on #2981 before P2ui builds the live control. The go criteria are in §4.4. No-go means the plain monospace editor ships in v1 | ui (owner), tools (AOT check) | neither package is on main today |
| E16 | Portal shortcut spike: a global hotkey through the desktop's global-shortcuts portal (Wayland) on the owner's desktop, and the X11 and macOS paths. Nothing on main registers a global shortcut. It starts on day one in parallel with E15. Fallback: the tray item only, which N2's text already names | ui | go or no-go posted on #2981 before P3 builds the hotkey |

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
- Everything in the v1.1 list of §2 (lead calls 1 and 11): Make issue, make-all, "Note about this", image paste, agent proposals, day chords, "Moved since yesterday", and the thread, run, scope and code links.
- Hard delete and rename (decision 4).
- Sync, sharing, a cloud store. The files are the owner's; any sync tool works.
- Widening the todo note sync from one note to every task line (ledger open question 1: not before the single-note sync has run a month).
- A Markdown renderer beyond what the editor shows; plugins; a graph view.
- Git history as the version store (decision 22).
- Home's "seen" flag for run-failure rows. It belongs to Home (Home §8); Notes adds no second flag.

## 10. What happens to today's views

| Today | After |
|---|---|
| Project `notes` string (`ProjectLedNoted`) | stays; a project's page also lists the notes that link it |
| Todo note sync (`todo.obsidian`) | stays as is; the todo note is one more note in the list and opens read-only in the Notes editor (decision 26). The sync takes the same cross-process lock as the Notes writers (decision 3) |
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
6. **Undo on a capture needs the line's position.** The op returns what Undo needs (the line and the base sha256); Undo is off after any other change to that file (decision 8).
7. **Start at login is new code.** Nothing on main writes a login entry (a `git grep` for autostart in `src/Sindri` and `docs/csharp/sindri-ui.md` finds none). E12 owns it for Linux and macOS.
8. **The ledger's rename of a note with link updates** is not in v1 (§9).
9. **Two riskiest assumptions have a spike each.** The editor control (E15) and the Wayland portal hotkey (E16). Both run on day one.

## 12. Proof

Proof is the app run on seeded data with `aw-ui-driver`, not tests alone. The scripts are `scripts/notes-proof.sh <capture|today|chips|tree>` (through `sindri-ui-fixture.sh`) and the test classes of the N table.

1. **Byte-clean round trip** on the fixture vault (N1), run in CI, and through the editor (N1b).
2. **Capture walk** from a fresh `context-first-run.sh` home with a made-up vault: the `notes.capture` action, plain line, task line, empty Enter, Sindri down ("Saved without Sindri"), serve and the helper appending at once, each failure of §6, Undo from the palette, Esc dropping the draft. Before and after shots. The hotkey itself is the by-hand part of N2.
3. **Today walk:** first open, folded, next day, previous day, a carried task ticked (one-character diff), a capture while the daily page is open (silent reload).
4. **Link walk:** `[[` popup in each group, no match, Link it, the PR page listing the note. (Make issue joins this walk in v1.1.)
5. **Chip walk:** merge a PR in the forge fixture (`scripts/forge-fixture.sh`) and time the chip (N6); stop Sindri and check the not-live state, the dimmed glyph, the tooltip.
6. **History walk:** edits, a capture, a restore, the pruning pass. (v1.1 adds an agent proposal over MCP, a stale proposal and accept.)
7. **Index timing** on a 1,000-note vault (N5), logged in the perf inventory.
8. **Screen reader:** `aw-ui-driver tree` (N10).
9. **Adversarial design review** by the ideas lane on the shots (owner rule, 2026-11-10), judged against inspora.design.

Perf: N2, N4, N5 measured in the real app by the perf lane. The vault in every fixture uses made-up names ("Harbor ferry"); no work app's names.

## 13. Phases and parallel plan (for /specToProvenPR)

Phases are the PRs. P0 to P3 are "slice A: capture and today", the smallest slice the owner can start using; it has a 3-day soak (N9a) before P4 to P7 reach the owner's build. Every lane has work from day one; the red and stubs of each phase land first, never behind another phase's PR. The ui lane runs several builders at once, since the spike, the list and the capture window do not share files.

| Phase | What | Lane | Depends on |
|---|---|---|---|
| P0 | Contract PR: the route and request and response record shapes of §8 (only the shapes; index internals are P1's), `NoteRow`, `INoteIndexReader`, the error catalog stubs, the events with their `PublicPayload`, profile keys with validation, the owner-only action class and the command catalog rows for each `note.*` write, golden and `PublicAPI` entries, `tests/fixtures/notes/` (`make-vault.sh` and the plain notes), all as stubs and the red tests for N1 and N8 | api | none |
| E15 | Editor control spike: go or no-go on #2981 | ui (owner), tools (AOT check) | none |
| E16 | Portal shortcut spike (Wayland, X11, macOS): go or no-go on #2981 | ui | none |
| P1 | Store: `notes.vault` validation, the `setup` job and the `notes` setup step, `notes-index.db`, `notes-state.json`, the watcher, read-only; the 1,000-note spike (E1, E2); verbs `list`, `show`, `reindex`, `setup`, `access`; the MCP tools `note_list` and `note_show` | api | P0 |
| P1c | Context's `notes` source on the library walker; `retrieval` derived from `notes.rules`, purge on flip-off (E9) | context | none (start now) |
| P2 | Write path: the cross-process lock, the swap with `CameOut` check, conflict rules, `.trash`, byte-clean test (E3); the History store half (§4.9); capture append, `NoteAppend`, `sindri note add` and `add --direct`, daily note with template (E4); the "Create note" write; the verbs marked P2 in §7 | api | P1 |
| P2ui | List, today's page with the Needs you fold, editor shell (plain until E15 says go; the todo note read-only), Settings frame with the folder and read switches (the login row is P3), states (§4.2, §4.3, §4.11, §4.12); `pin` | ui | P0 stubs; E15 for the live control; X1 for the visual pass |
| P3 | Capture: the Note kind in the shared window, hotkey, tray item, Wayland portal, the helper start when Sindri is down, the autostart entry and its Settings row, `notes.undo-capture`, the failure states (E12); the by-hand part of N2 | ui | P2 (for `NoteAppend` and the helper) and #3170's window (red and stubs first); E16 |
| P4 | Links: link index, backlinks, unlinked mentions, `[[` popup (notes, issues, PRs), right pane (E5); verb `link` | api, ui | P1, P2ui |
| P5 | Search: the route, the operators of the `/` filter, the `note:` palette scope (a scope source on the palette; whether the palette takes one is P5's first red test), verb `search` and the MCP tool `note_search` (E6) | api, ui | P1 |
| P6 | Read-only chips, the `about` reverse walk, notes listed on the PR, issue and project pages (the ui lane edits the Reviews and Projects page files, with their owners reviewing) (E7) | api, ui, context | P4; #3021 |
| P7 | History: view, restore, retention and caps, "Delete this version", the doctor row; verbs `history` and `restore`; the MCP tool `note_history` (E8) | api, ui | P2 |
| X1 | Design: the visual pass for §4.1 to §4.12 in the Inspora bar; the reshoot list of §5; update #3170's frames to the "New issue" / "New note" title rule and the Ctrl+Enter create | design | none |
| X2 | UX: the walks (§12) at each phase's end; N9 week | ux | each phase |
| X3 | Perf: N2, N4, N5 in the real app; the list at 10,000 notes | perf | P1, P2ui |
| X4 | QA: judge each phase against the bar, the shots and N1 to N12 | qa | each phase |
| X5 | Tools: `scripts/notes-proof.sh`, `make-vault.sh`, aot-smoke lines, `.env.example` (and `check-env-example.sh` when a watcher or hotkey env var exists, e.g. `AW_NOTES_NO_WATCH`), and docs: `docs/csharp/sindri-notes.md` (with the list of the 15 path cases), the `notes` section of `docs/csharp/sindri-profile.md` and `docs/sindri/profile.md`, the route table and `note` verbs in `docs/csharp/sindri.md`, an `AGENTS.md` Commands line for `sindri note ...`, and this spec's line in `docs/sindri/README.md` | tools | each phase |
| P6b (v1.1) | Make issue (§4.8): the route, the bar, the verb, N12 | api, ui | P4, P6, N9 |
| P8 (v1.1) | #3207: agents PROPOSE appends and edits, the owner accepts. `propose`, `accept`, `decline`, the History bands, stale proposals, `SND-NOTE-010` and `011`, the per-folder write rule, and `propose` as the one session write (E14). Its own red tests and review, because it changes the session rules. "Agents can append" stays a separate open question (2) | api, ui | P7, P2, the owner-only class (call 9); starts after N9 holds |
| V1.1 | After the N9 week: make-all, "Note about this", image paste, day chords, "Moved since yesterday", thread, run, scope and code links | api, ui, context | N9 |

Day one: api P0 (red and stubs); ui E15 and E16 (the spikes), ui P2ui on the P0 stubs, ui P3's red against #3170's stubs; context P1c; design X1; ux X2 baseline; tools X5 (`make-vault.sh`, the proof script stubs including `bytes`, the docs skeleton); perf X3 (the 1,000-note generator and the stopwatch harness); qa X4 (the N1 and N8 reds). Nothing waits on a dependency PR to start its red.

Gate notes: a PR that adds routes runs the full Sindri integration lane and `EndpointCqrsTests`. A PR that adds `sindri note` verbs changes `Usage.cs` and `PublicAPI.Unshipped.txt` and runs the whole `Aw.Sindri.Tests` unit lane (P1, P2, P4, P5, P7). A PR that adds an MCP tool changes the `/sindri/mcp` `tools/list` block of `src/Sindri/aot-smoke/expected.txt` and runs aot-smoke (P1, P5, P7, not only the doctor row). The `notes` setup step adds a `SetupStepId` and a branch in `Core/Setup/SetupSteps.cs` with its golden, and the `setup not finished` line of the same file may move (P1). Each `note.*` command needs a catalog row (`Core/Commands/CommandRow.cs`), so the Ui `GoldenContract` slug and scenario row apply, and the owner-only class change runs the Authorization and command-list tests (P0, P2). The helper's `cli` edge changes `deps.yaml` (P3). A PR that adds a golden or catalog row adds its scenario row and the Ui `GoldenContract` slug, and runs `Aw.Sindri.Ui.Tests` unit. A PR that adds events runs `EventCatalogTests`. `notes-index.db` is its own file, so no ledger migration and no views migration lands with Notes; a PR that does add a ledger migration takes the next free number and runs the Ledger namespace tests. A doctor change updates `src/Sindri/aot-smoke/expected.txt` and runs aot-smoke. A new `using Aw.` or sub-namespace runs `Aw.Architecture.Tests`. A new view runs `Aw.Sindri.Ui.Frames.Tests` integration. New env vars go in `.env.example`. A new project reference runs the locks and `sindri-build-inputs.sh` checks. Note text, titles and chip text are scrubbed by `Aw.Redaction` before they enter the ledger or a log line.

## Open questions

Twelve are written to the lead's pick or an agent default. None blocks P0; "blocks" names what each one does block. Questions 9 and 10 repeat settled lead calls and are listed for the record. The ones the owner will notice first are 11, 12 and 5.

1. **Where the versions live** (decision 22): `$AW_SINDRI_HOME/notes-history/`, a copy per snapshot with the policy of §4.9. Written to the lead's pick. The alternative is the vault's git history when it is a repo. The copy comes first because it works for every vault. Blocks: none.
2. **Agent append to a named note** (ledger 12 allows a direct write; v1 has no agent writes at all). Written to the lead's pick: "never edits silently" is the owner's rule. A run-log note that needs a direct append can get a third rule value, "Agents can append", after the soak. An issue will be filed for it `Part of #2981`, with this trigger: more than 5 pending proposals older than a day. It stays apart from #3207 (P8), which is proposals the owner accepts. Blocks: v1.1.
3. **What "Moved since yesterday" reads** (§4.2, v1.1): Home's Moving rows whose `since` is after local midnight of the previous day. Written to the lead's pick. Blocks: v1.1.
4. **Capture with context** (§4.1, v1.1): "Note about this" actions on Reviews, Home and scope pages open the editor on a new pre-linked note; the global window stays one line. Written to the lead's pick. Blocks: v1.1.
5. **Default Make-issue target** (§4.8, v1.1): the profile's tracker; GitHub only for a repo of the profile (round 2 dropped "the linked PR's repo", which could open a public issue in someone else's project). Written to the lead's pick. Blocks: P6b.
6. **"Make all" on an `## Actions` section** (§4.8): v1.1, with one confirm that lists the titles. Written to the lead's pick. Blocks: v1.1.
7. **Size limits** (§6): a note over 1 MiB opens as text only, an attachment over 25 MB is refused. Written to the lead's pick. Blocks: P2ui (the 1 MiB banner).
8. **Where the daily folder is read from** (§4.2): Obsidian's `.obsidian/daily-notes.json` (`folder`, `format`, `template`) read once when present, as the one named exception to the `.obsidian/` deny; the profile keys `notes.daily` and `notes.dailyFormat` win when set; the defaults are `daily/` and `YYYY-MM-DD`. Written to the lead's pick (call 12). Blocks: P2.
9. **Capture while Sindri is down** (lead calls 2 and 8): settled; the helper `sindri note add --direct` writes the line and the window shows "Saved without Sindri". The open part is question 11. Blocks: P3.
10. **Start at login** (lead call 3): settled; one click in Settings, or `sindri note autostart on` in a terminal, once a notes folder is set; `sindri setup` only prints the step; nothing turns it on silently. Blocks: P3.
11. **Capture while Sindri is down: helper or no direct write** (lead call 8, *owner can flip*). Written to this pick: a small helper the window starts (`sindri note add --direct`), one cross-process lock shared with serve and the todo sync, and "down" means no live host lease, so the Ui project gets no Core reference. The other way: no direct write at all. The window then says "Sindri isn't running. Your line is kept." and holds the draft until serve is back. That is smaller, and capture from anywhere then needs serve running. What do you want? Blocks: P3.
12. **Make issue in v1 or v1.1** (lead call 11, *owner can flip*). Written to this pick: v1.1, so v1 writes nothing outside your files. The other way: keep it in v1 with the safeguards of §4.8 (base checked before the outside call, a button that disables after one press, the created issue shown if the line can't be replaced) and check N12. v1 then has one outside write. What do you want? Blocks: P6b, or P6 if it moves back.

Agent defaults (numbers no owner or lead gave): 5 working days for N9 and 3 for slice A, 5 s cold start, 7 days for old drafts, 1 h to sweep a temp file, 3 s for the paste notice, 200 versions per note, 1 GiB for the history folder, 30 and 90 day history tiers with a daily prune, 15 path cases, 10 minute index warning, about 1000 px for the pane to collapse, `#7a7d85` for dimmed marks, 2 s capture walk, 100 ms paint and reindex, 300 ms Enter to fsync, 50 ms popup, 5 rows in the fold, 8 s Undo, 1 s chip update, 340 px version list, 32 px rows, 10 minute rescan, 300 ms watcher quiet time, 250 ms draft pause, 1.5 s autosave, 60 s snapshot floor, 10,000 notes for the popup, 3-character and 20-row limits on unlinked mentions, 10 vaults (main's own limit).

## Review notes

### Autoplan round 1 (ideas, 2026-11-12)

Folded in commit `95cf6701c6`: v1 cut to a smaller slice (lead call 1), capture while Sindri is down (call 2), autostart (call 3), the index in its own file (call 4), the link grammar (call 5), no agent writes in v1 (call 6), one synchronous route family, the history policy, the conflict rules, the error catalog with frame words, and the editor spike E15.

### Autoplan round 2 (ideas, 2026-11-12)

Verdict was NEEDS_WORK: 23 MUSTs across five lenses, and 10 decisions for the lead. Folded:

- **Lead calls 8 to 18** (§2): the capture helper, the owner-only action class, one `NoteOpCommand`, Make issue to v1.1, the Obsidian daily-notes exception, the History store in P2, the proof changes, the read-only todo note, the read-only editor with Sindri down, `notes.rules` as the one read-switch store, and phase P8 for #3207. Calls 8 and 11 are *owner can flip* (Open questions 11 and 12).
- **Write path** (decision 3, E3): the swap returns `CameOut`, which Sindri hashes, so an outside write between the re-read and the swap is no longer lost; one cross-process lock for serve, the helper and the todo sync; `Leftover` recovery; stale temp files swept.
- **Capture without Sindri** (§4.1): "down" means no live host lease; the helper does the checks serve does and fails closed; a skip list with a test; `SND-NOTE-023` when serve is live but fails.
- **Session refusal and routes** (§8, E14): the owner-only class, a catalog row per intent, the public event payload with dedupe keys, `INoteIndexReader`.
- **Reads** (§7): the deny and retrieval rules apply to every field that carries another note's text; chip titles are fenced; session text passes `Aw.Redaction`; history checks the current path after a rename; the `version` regex; flipping a folder off purges context's store.
- **Daily notes** (§4.2): `.obsidian/daily-notes.json` is a named read-only exception; `notes.dailyFormat`; a built-in default template; fixture in N1.
- **Proof** (§1): N1b through the editor, N2 split in two, N9 with an owner, five days and a pass line, N9a slice A soak, N11 safety rules, N12 for Make issue, softer numbers fixed.
- **Build order** (§13): the History store half in P2, P3 depends on P2, a phase column on the verbs, the login row in P3, who edits the PR, issue and project pages, E16 for the Wayland hotkey, four more gate notes.
- **CLI** (§7): `sindri note access`, `reindex`, `autostart`, `--base` on verbs that name a line, the setup step `notes`, the vault name collision rule, the corrected `connection add` fix.
- **Frames** (§5): round 4 reference, a reshoot list that names every frame still drawing a v1.1 part, "Esc drop" with the draft deleted on Esc, "Keep my line" and "Keep the file's line", the Create button on the Make issue picker, the dimmed-chip floor.
- **Smaller items**: pins moved to `notes-state.json`; the reason for a second index; History caps and "Delete this version"; the editor ignores its own write's echo; draft cleanup; the toast's keyboard route `notes.undo-capture`; no remote images; a guessed link makes no `about` edge; exit codes per code; Open questions 9 and 10 marked settled.

Not folded, with the reason: the per-code `exit` column in the §6 table (one paragraph under the table says the same in fewer lines); the 13-verb cut (product S8: the verbs stay, each now ships in the phase of its own row).
