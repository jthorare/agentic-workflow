# Prior art: Notes (a staff engineer's notes, daily page, links to live work, design docs)

This file is the prior-art ledger for the Notes surface of Sindri (rule: #2989). It lists the small daily jobs of keeping notes, who solves each best today, what we copy, and what Sindri does on origin/main (checked at 74d702c6ac, 2026-11-12).
Sources are each product's own docs, READMEs and changelogs, read 2026-11-12. A vendor's own claim is marked "vendor claim". Anything I could not confirm is marked "unconfirmed". No screen captures: links go to the page that describes the screen (design captures frames).
Surface job: capture a thought in seconds, find it again, and see it next to the issue, PR, thread or run it is about, in plain Markdown files the owner keeps.
Direction issue: #2981. Parent: #2978 (row/peek/full grammar), #2989 (this rule). Context lane plan for notes as a retrieval source: comments on #2981.

Licence note for builders (we may read and copy ideas from MIT code; GPL/AGPL = ideas only, no code): Foam MIT, Obsidian Tasks MIT, Obsidian Git MIT, Dataview MIT, Obsidian GitHub Link has no declared licence on GitHub (NOASSERTION): ideas only. zk is GPL-3.0. nb, Logseq, Templater and Basic Memory are AGPL-3.0. Obsidian itself is closed source; `obsidian-help` has no licence, so we read its docs and copy no text.

How Sindri stands today, in one paragraph, so the rows below can be short. There is **no notes page**. What exists on main:
1. A design-doc library feed. It reads only `.md` files whose first 4 KiB hold front matter with `aw-doc: 1`, from the `aw` state folder, each vault in `context.sources.library.vaults`, and each profile repo's doc globs (`docs/conventions/design-docs.md`). It cuts a doc into sections, phases, requirements, acceptance criteria, test rows and tasks, and a PR claims items with a `Covers:` line. Surfaces: `sindri library sync|list|show|validate`, `GET /sindri/library` (`docs/csharp/sindri-library.md`), and a library sheet on the Reviews page. It lists at most 200 broken docs and 200 near misses; a doc over 1 MiB or 2,000 items is stored as "too large".
2. A todo list that syncs with ONE Markdown note in an Obsidian vault (`sindri todo note set <vault> <note>`, `docs/sindri/todo-note.md`). Conflicts, holds on bulk edits, 500 open todos cap.
3. A free-text `notes` field on a project (`ProjectLedNoted` event, `src/Sindri/Aw.Sindri.Core/Projects/ProjectDecisions.cs`). One string per project, no links.
4. A Ctrl+K command palette (`src/Sindri/Aw.Sindri.Ui/ViewModels/Palette/`). I found no `note:` verb.
Missing: capture, daily note, editor, `[[` links, backlinks, note search, templates, history, note-to-work links. The context lane has planned a `notes` retrieval source on the library walker (comment on #2981, accepted by meta). It indexes read-only and is not a UI.

---

## 1. Capture a thought in under 2 seconds without leaving the app I'm in

**Best today**
- Obsidian, from the terminal and from other apps. The CLI has `obsidian daily:append content="- [ ] Buy groceries"` and the docs say the app must be running or the first command starts it. https://github.com/obsidianmd/obsidian-help/blob/master/en/Extending%20Obsidian/Obsidian%20CLI.md . The URI scheme has `obsidian://new`, `obsidian://daily`, `obsidian://search`: https://github.com/obsidianmd/obsidian-help/blob/master/en/Extending%20Obsidian/Obsidian%20URI.md source: docs
- Raycast + Obsidian (Marc Julian's extension, third party). Command "Append to Daily Note": appends to the end or under a named heading, creates the day's note if missing, honours templates, has a silent mode so Obsidian does not come to the front (from the Raycast store listing and its changelog). https://www.raycast.com/marcjulian/obsidian and https://www.raycast.com/marcjulian/obsidian/changelog . I found no confirmed global-hotkey doc; Raycast lets any command take a hotkey (general knowledge, unconfirmed on that page). source: none (not read; see the unconfirmed note in the line)
- Apple Notes Quick Note. Hot corner, lower-right by default (Apple's Mac guide, via search snippet; the keyboard shortcut I could not confirm). Safari "highlight text, Share, Quick Note" pre-links the page. A known complaint: the corner reopens the last quick note instead of a fresh one (Apple Community thread). https://support.apple.com/guide/notes/create-a-quick-note-apd6a8c4d6ea/mac (page is a JS shell; text not readable by my fetch) source: none (not read; see the unconfirmed note in the line)
- nb (AGPL-3.0, terminal): `nb add "This is a note."`, piped input, or clipboard in, each commits to git in the background. https://github.com/xwmx/nb#-notes source: docs

**What we take**
- A global hotkey opens a one-line capture bar (no window switch, no main window needed). Enter saves and closes. Shift+Enter makes it multi-line. Esc drops it.
- The text appends to today's daily note under a `## Captured` heading, with a time prefix (`- 14:32 ...`). If today's note doesn't exist, it is made first. That is Obsidian `daily:append` behaviour plus Raycast's "under a heading".
- A CLI twin: `sindri note add "text"` for scripts and agents (stdin accepted). Same append path.
- Silent mode: the bar never raises the main window.

**Where one app wins**: capture starts pre-filled with where I am. In a review it quotes the selected diff lines and links the PR; in a thread it links the message; on a run it links the run. Apple Notes only knows the Safari page; Obsidian knows nothing about the PR.

**Desktop gain**: bar visible < 100 ms after the hotkey (window pre-created, hidden), measured keypress to first paint; saved to disk and visible in the daily note < 50 ms after Enter.

**Sindri today**: missing. The only free-text write is a project's `notes` string. Needs a new issue (capture), under #2981.

**Verdict**: take and change (Obsidian/Raycast append path, plus context pre-fill that none of them have).

---

## 2. Open today's page, with yesterday one key away

**Best today**
- Obsidian Daily notes (core plugin): ribbon, command or hotkey "Open today's daily note"; creates it if missing, named `YYYY-MM-DD`, optional folder format like `YYYY/MMMM/YYYY-MMM-DD`, optional template file. https://github.com/obsidianmd/obsidian-help/blob/master/en/Plugins/Daily%20notes.md source: docs
- Foam (MIT, VS Code): `Alt+D` opens the daily note; `/today`, `/yesterday`, `/tomorrow` snippets in any note; template variable `$FOAM_PREVIOUS_DAILY_NOTE` links the last note that exists, skipping weekends and holidays, which `/yesterday` does not. https://github.com/foambubble/foam/blob/main/docs/user/features/daily-notes.md source: docs
- Logseq: journals are made for the current day in the Journals view; `g n` / `g p` go to the next / previous day; a `#Journal` tag can carry properties that appear on every journal (Logseq DB-version doc, vendor). https://github.com/logseq/docs/blob/master/db-version.md#journals source: docs

**What we take**
- One key (proposal: `g d`, plus a Home row) opens today's page; `g p` / `g n` step to the previous / next EXISTING day (Foam's skip-the-gap rule, not the calendar day).
- Page name `YYYY-MM-DD.md` in a folder the owner sets. Same default as Obsidian, so an existing vault opens unchanged (#2981 "done when").
- A "Previously: [[...]]" line from the template.

**Where one app wins**: the issue (#2981) wants the daily note to open with a brief of what needs me and what moved. Obsidian's template can only insert static text. We can render the Home "Needs you" and "Since you last looked" rows (#2978) at the top of the page, live, outside the file's byte content (see problem 9 for how live blocks stay out of the file).

**Desktop gain**: today's page paints < 100 ms from a local file read, offline; the brief block fills in after, never blocking the text.

**Sindri today**: missing. Home has a todo panel only (`HomePage.axaml`), and the todo note is one named file.

**Verdict**: take and change (Foam's gap-skipping link and Obsidian's file naming; the brief block is ours).

---

## 3. Link to another note while typing

**Best today**
- Obsidian: type `[[`, pick from a fuzzy list; `[[#` lists headings in this note; `[[Note#` headings of another; `[[##` searches headings across the vault; `^` for blocks. Rename updates links (setting). Links to missing notes create the note. https://github.com/obsidianmd/obsidian-help/blob/master/en/Linking%20notes%20and%20files/Internal%20links.md source: docs
- Bear: type `[[` for Note Autocomplete showing recent notes; type letters to narrow; arrow keys and Return; press `/` on a result to pick one of its H1-H6 headings; `[[/` for headings in the current note. https://bear.app/faq/how-to-link-notes-together/ and https://bear.app/faq/how-to-use-tag-autocomplete-and-note-autocomplete/ source: docs
- zk (GPL-3.0) and nb (AGPL-3.0): both read `[[Wikilinks]]` and plain Markdown links in ordinary files. zk README, "Supports most Markdown syntax flavors". https://github.com/zk-org/zk#highlights . nb: "wiki-style linking" https://github.com/xwmx/nb#-linking source: docs

**What we take**
- `[[` opens a list: recent first, then fuzzy matches over title and alias; Enter inserts; `/` drills into headings (Bear). The file holds `[[Note Title]]` or `[[Note#Heading]]` exactly as Obsidian writes it (byte-compatible with an existing vault).
- The same popup also lists work objects (see problem 9): `[[#123` issues and PRs, `[[run:`, `[[scope:`. This is the context lane's edge set (`about item:`, `about code:`, `about scope:`).
- Rename updates links in the same write.

**Where one app wins**: `[[` completes things no notes app has: issues, PRs, Slack threads, workflow runs, context scopes, files at a line. Obsidian's links stop at files.

**Desktop gain**: popup first rows < 50 ms per keystroke at 10k notes (in-memory title index).

**Sindri today**: missing.

**Verdict**: take and change (Obsidian syntax for compatibility, Bear's heading drill-down, our extra object kinds).

---

## 4. See what links to this note, including mentions I never linked

**Best today**
- Obsidian Backlinks: right-sidebar tab with "Linked mentions" and "Unlinked mentions"; options collapse results, show more context, sort, and a search filter box that takes the Search operators; a linked tab pins the view to a chosen note; can also render at the bottom of the note. https://github.com/obsidianmd/obsidian-help/blob/master/en/Plugins/Backlinks.md source: docs
- Foam (MIT): Connections panel with filter buttons for backlinks, forward links or all. https://github.com/foambubble/foam/blob/main/docs/user/features/backlinking.md source: docs
- Logseq: each page has "Linked References" and "Unlinked References" sections; forum threads report unlinked ones are searched on demand and always start collapsed, and that a block can show in both. https://github.com/logseq/docs/blob/master/db-version.md (DB version), forum: https://discuss.logseq.com/t/have-you-ever-designed-for-unlinked-references/19896 (user report, not vendor docs) source: docs
- Bear: a backlinks panel exists per a third-party comparison; I found no official page, so unconfirmed. source: none (not read; see the unconfirmed note in the line)

**What we take**
- Right pane under the peek, two groups: Linked (with the surrounding paragraph as context, 1 line collapsed) and Unlinked (title found in text, not linked; one key to link it). Both filterable.
- Row = source note title, the paragraph, age. Same row grammar (#2978).
- Backlinks from work objects are in the same list (problem 9).

**Where one app wins**: the PR page and the issue row show the notes that link them, with no plugin. Obsidian can show only links between files in the vault.

**Desktop gain**: backlinks for the open note < 100 ms from a local link index (target: 10k notes, 100k links); incremental update < 1 s after an edit (the context lane's own budget for a 1,000-note reindex).

**Sindri today**: missing. The context plan makes links into graph edges, so "every linked object shows its notes" is a reverse walk (comment on #2981).

**Verdict**: take and change (Obsidian's two groups; add object backlinks).

---

## 5. Jump to a note by half-typing its name

**Best today**
- Obsidian Quick switcher: `Ctrl/Cmd+O`; fuzzy, letters in order (`dn` matches `Daily notes`); matches aliases; empty box shows recent notes, Down then Enter toggles between two notes; Enter on no match creates it; Shift+Enter forces a new exact name; Ctrl+Enter opens in a new tab. https://github.com/obsidianmd/obsidian-help/blob/master/en/Plugins/Quick%20switcher.md source: docs
- zk: interactive browser powered by fzf over the notebook. https://github.com/zk-org/zk#highlights source: docs
- nb: items addressed by id, title or selector, so `nb 12 peek` shows item 12. https://github.com/xwmx/nb#-selectors (README; I did not read the selector grammar in full) source: docs

**What we take**
- Already half-built: Ctrl+K. Add a `note:` scope to it (or typing a name with no verb), with Obsidian's rules: fuzzy in order, aliases, empty box = recents, Down+Enter = previous note, Enter on no match = create.
- Same palette finds issues and PRs by number, so one box reaches both notes and work.

**Where one app wins**: one palette. The owner types `export` and gets the note, the PR and the run.

**Desktop gain**: < 50 ms per keystroke at 10k notes, list rendered at 60 fps while arrow keys repeat.

**Sindri today**: Ctrl+K palette exists (`Labels.Palette.cs`: "⌘K / Ctrl+K"); no note source in it.

**Verdict**: take as is (Obsidian's switcher rules), plug into our palette.

---

## 6. Search everything I wrote, with filters

**Best today**
- Obsidian Search: `Ctrl/Cmd+Shift+F`; operators `file:` `path:` `content:` `tag:` `line:` `block:` `section:` `task:` `task-todo:` `task-done:`, property filters `[property:value]`, `OR`, `-` negate, parentheses, "Explain search term" button. https://github.com/obsidianmd/obsidian-help/blob/master/en/Plugins/Search.md source: docs
- zk (GPL-3.0): SQLite full-text search (`fts`) by default, "near-instant" (vendor claim), plus `exact` and `re` strategies; `title:` and `body:` prefixes, `edi*` wildcard, `^` anchors, `--link-to`, `--linked-by`, `--tag`, `--orphan`. https://github.com/zk-org/zk/blob/main/docs/notes/note-filtering.md source: docs
- Foam (MIT): saved queries as YAML in `.foam/queries/` or inline `foam-query` blocks with `filter`, `sort`, `limit`; shown as "Smart Folders". https://github.com/foambubble/foam/blob/main/docs/user/features/foam-queries.md source: docs

**What we take**
- Obsidian's operator names (owner's existing vault habits), zk's engine choice (SQLite FTS5 plus link filters like `linked-to:` / `orphan`), Foam's saved query as a file (a saved search is a small YAML, version-controlled).
- Results are rows with the matching line; Space peeks the note at the match.
- Same box also filters by work object: `about:#123`.

**Where one app wins**: a note search can also rank by what the owner is working on right now (open PRs, current scope). The context lane plans note chunks in `context_query` as `note:<path>#<heading>` (comment on #2981).

**Desktop gain**: first result row < 100 ms at 1,000 notes and < 250 ms at 50k (SQLite FTS5 local; perf to confirm the second number).

**Sindri today**: missing for notes. `sindri library list|show` lists only `aw-doc: 1` docs and has no text search.

**Verdict**: take and change (Obsidian operators over a zk-style FTS index).

---

## 7. See every open task across my notes and tick it in place

**Best today**
- Obsidian Tasks (MIT): "Track tasks across your entire vault. Query them and mark them as done wherever you want ... toggle the task status in any view or query and it will update the source file." Due dates, recurrence, done dates, filters. https://github.com/obsidian-tasks-group/obsidian-tasks (README) source: docs
- Obsidian Search `task-todo:` finds open checkboxes block by block. https://github.com/obsidianmd/obsidian-help/blob/master/en/Plugins/Search.md source: docs
- Logseq: tasks are part of its core; the Query Builder and queries on today's journal depend on them (DB doc, vendor). https://github.com/logseq/docs/blob/master/db-version.md#tasks source: docs

**What we take**
- A "Tasks" view: rows of open `- [ ]` lines from all notes, grouped by note, one key to tick; the tick edits the source line only (one character), nothing else in the file.
- Tasks query as a saved search (problem 6).

**Where one app wins**: Sindri already has a todo list in the ledger and a sync with one note. Ticking a task in any note could be the same todo. Obsidian Tasks can't see a Linear issue or a failed run.

**Desktop gain**: toggling a task writes the file < 50 ms and the row updates in place (optimistic).

**Sindri today**: partial. One note is synced to the todo list with ids (`^td-...`), a conflict model and a 500-todo cap (`docs/sindri/todo-note.md`). Tasks in other notes are not read. Whether the sync should widen to all notes is a decision for the owner (open question below).

**Verdict**: take and change (Tasks' "tick updates the source line"; widen only after the single-note sync is proven).

---

## 8. Start a meeting / 1:1 / decision note from a template

**Best today**
- Obsidian Templates (core): template folder; variables `{{title}}`, `{{date}}`, `{{time}}` with Moment.js formats; the same variables work in Daily notes and Unique note creator. https://github.com/obsidianmd/obsidian-help/blob/master/en/Plugins/Templates.md . Unique note creator makes time-named notes like `202401010945`. https://github.com/obsidianmd/obsidian-help/blob/master/en/Plugins/Unique%20note%20creator.md source: docs
- Foam (MIT): `.foam/templates/` with `$FOAM_DATE_*`, `$FOAM_TITLE`; the daily-note template is one of them. https://github.com/foambubble/foam/blob/main/docs/user/features/daily-notes.md source: docs
- zk (GPL-3.0): templates for `zk new` (Highlights list, "Creating notes from templates"). https://github.com/zk-org/zk#highlights source: docs

**What we take**
- Templates are plain `.md` files in a folder the owner names; `{{title}}`, `{{date:FORMAT}}`, `{{time}}` as Obsidian spells them, so an existing template folder works.
- Our extra variables: `{{pr}}`, `{{issue}}`, `{{scope}}` filled when the note starts from a review, issue or scope (problem 1's pre-link).
- "Meeting" template with an `## Actions` section that problem 10 reads.

**Where one app wins**: a note started from a PR fills in the title, link, branch and reviewers. Obsidian's `{{title}}` is only the file name.

**Desktop gain**: new note from a template open with cursor in the body < 100 ms.

**Sindri today**: missing. The design-doc convention shows a hand-copied starter in `docs/conventions/design-docs.md` ("Your first doc"), and `officeHours`/skills write docs, but there is no in-app template.

**Verdict**: take as is (Obsidian variables), add our work variables.

---

## 9. Show the live state of an issue / PR / run inside a note

**Best today**
- Notion link previews and mentions: paste a GitHub or Linear link, choose "Preview" or "Mention"; the preview "updates as changes are made to any relevant fields"; first paste asks to authenticate; supported list includes GitHub, Linear, Slack, Jira, Figma, GitLab. Anyone who can view the Notion page sees the preview once the owner authenticated. https://www.notion.com/help/link-previews (vendor) source: docs
- Obsidian GitHub Link (community plugin, no declared licence): GitHub links become inline tags showing number, title, author and status; a ```` ```github-query ```` block renders a table with `columns: [number, title, author, status]` that refreshes when the note is opened. https://community.obsidian.md/plugins/github-link source: docs
- Obsidian GitHub Embeds / GitHub Tracker / GitHub PR Tracker (community plugins): embeds in reading view only per the roadmap, a note per issue with a status property, a sidebar of PRs with review status. Listed at https://www.obsidianstats.com/tags/github . I read only their listings. source: none (not read; see the unconfirmed note in the line)

**What we take**
- A link to an issue/PR/run/thread in a note renders as an inline chip: glyph (open / merged / red CI), number, title, "waits on". Click opens the peek; the chip is a view, the file keeps the plain URL or `[[#123]]`.
- A query block is a saved search (problem 6) rendered as rows, refreshed live, not "on open".
- Stored-in-file stays plain: no cached state is written into the Markdown (byte-clean round trip, #2981).

**Where one app wins**: all four tools above poll one forge and refresh slowly. Sindri has the PR, CI, review threads, Slack thread, agent run and Linear issue in one local store, already event-driven; the chip updates when the ledger event arrives. And the reverse works too: the PR page lists the notes that link it (problem 4).

**Desktop gain**: a chip's state is read from the local store, 0 network calls on open; update visible < 1 s after the ledger event.

**Sindri today**: missing for notes. The Reviews page already ties design-doc items to PRs through `Covers:` (`docs/conventions/design-docs.md`), which is the same idea one-way.

**Verdict**: take and change (Notion's preview/mention split; the "state lives outside the file" rule is ours).

---

## 10. Turn a line in a note into a tracked issue

**Best today**
- Linear: in a document, highlight text and click "Create issue from selection" in the formatting bar (Linear docs). https://linear.app/docs/creating-issues . A changelog entry says selections in team documents now create the issue in the correct team (via search snippet; I did not open the changelog). source: docs
- Obsidian GitHub Tracker / Issues plugins: create or sync GitHub issues as notes (listing level only; I did not read their pages). https://www.obsidianstats.com/tags/github source: none (not read; see the unconfirmed note in the line)
- Notion: unconfirmed. I did not find a primary Notion page that turns a text line into a task. source: none (not read; see the unconfirmed note in the line)

**What we take**
- `.` on a task line (or the selected text) -> "Make issue": opens the create form pre-filled with the line, the note link, the repo from the note's context; on save the line becomes `- [ ] ... [[#412]]` in place.
- A meeting note's `## Actions` section gets a "make all" key (the #2981 "one key" wish).

**Where one app wins**: the new issue links back to the note; the note shows the issue's live state (problem 9). Linear's selection-to-issue goes one way only inside Linear's own docs.

**Desktop gain**: form opens pre-filled < 100 ms; the optimistic write-through to the forge within the 2 s of the parity contract (#2978).

**Sindri today**: missing. Issue creation exists in the item flow (`sindri item add`, `docs/csharp/sindri.md`), not from a note.

**Verdict**: take and change. Thin evidence: only Linear's one feature is confirmed.

---

## 11. Undo: see what changed in a note, and go back

**Best today**
- Obsidian File recovery (core): snapshots at least 5 min apart, kept 7 days (both configurable), stored outside the vault in global settings, device-local (do not sync), only `.md` and `.canvas`, "Show changes" diff between snapshots. https://github.com/obsidianmd/obsidian-help/blob/master/en/Plugins/File%20recovery.md . The CLI has `obsidian diff file=README from=1 to=3`. source: docs
- Obsidian Git (MIT, community): automatic pull / commit / push on a schedule or X minutes after the last change. https://github.com/Vinzent03/obsidian-git source: docs
- Notion page history: a version every 10 minutes while editing and another 2 minutes after the last edit; highlighted changes and who made them; deleted pages stay in Trash 30 days (vendor). https://www.notion.com/help/duplicate-delete-and-restore-content source: docs
- nb (AGPL-3.0): git in the background for every note, with revision history and sync. https://github.com/xwmx/nb#-revision-history source: docs

**What we take**
- A history list in the peek pane, per note: time, source (me / agent run / sync), +/- lines; open one for a side-by-side diff; one key restores. Snapshot rule from Obsidian (5 min, 7 days, configurable) but kept in `$AW_SINDRI_HOME`, not in the vault (vault stays clean; Notion/nb-style git commits only if the owner's folder is already a git repo).
- Every agent write is its own entry, so "what did the agent change" is the same list (problem 12).

**Where one app wins**: history entries name the run or session that wrote them, and link to it.

**Desktop gain**: restore a version < 200 ms; history list for a note with 500 versions < 100 ms.

**Sindri today**: missing. The ledger records events, not note bodies.

**Verdict**: take and change.

---

## 12. Let an agent read my notes and append to a named note, without it editing anything silently

**Best today**
- Basic Memory (AGPL-3.0, ideas only): MCP server over a folder of Markdown; "AI and humans write to the same files; sync keeps them in step"; works with Claude Code, Codex, Cursor, VS Code, and "Obsidian reads/writes the same Markdown directly". https://github.com/basicmachines-co/basic-memory (README; vendor claims) source: docs
- Obsidian CLI: `append`, `prepend`, `daily:append`, `read`, `search`, `diff`; its docs call out use by agentic coding tools for the developer commands. https://github.com/obsidianmd/obsidian-help/blob/master/en/Extending%20Obsidian/Obsidian%20CLI.md source: docs
- zk (GPL-3.0): "made with automation in mind"; `zk list` filters as an API for scripts. https://github.com/zk-org/zk#highlights source: docs

**What we take**
- Folder-level switch per folder: agents may read (`retrieval: on`) or not (context lane's plan, accepted). Default off.
- Agent writes arrive as a diff the owner accepts or rejects (rejected = file untouched), never in place. Named-note append is the only direct write (run log, decision record), shown as an entry in history (problem 11).
- MCP tools mirror the CLI: `note_read`, `note_search`, `note_append`, `note_propose_edit`.

**Where one app wins**: Basic Memory and Obsidian CLI have no idea which agent run is writing or what the owner approved. Ours can show "run 8f2 appended 3 lines, accept?" on Home as a row.

**Desktop gain**: reindex 1,000 notes after one edit < 1 s (the context lane's budget); an agent read of a note chunk < 50 ms locally.

**Sindri today**: only the plan (comments on #2981). The `context.sources.library` feed fences library text as untrusted for sessions.

**Verdict**: take and change.

---

## 13. See a set of notes as a table by property (all open 1:1s, all decisions with status)

**Best today**
- Obsidian Bases (core): database-like views over notes and their properties; table, list, cards, map layouts; filter, sort, group; formulas; stored as a `.base` file or embedded in a code block; "All the data ... is stored in your local Markdown files". https://github.com/obsidianmd/obsidian-help/blob/master/en/Bases/Introduction%20to%20Bases.md source: docs
- Foam (MIT): saved queries / Smart Folders by tag, link, property. https://github.com/foambubble/foam/blob/main/docs/user/features/smart-folders.md source: docs
- Dataview (MIT, community): query language over notes; last push 2025-11-17 (GitHub API), so slower-moving than Bases. https://github.com/blacksmithgu/obsidian-dataview source: docs
- Notion databases: the model Bases copies; I did not fetch a primary page (unconfirmed). source: none (not read; see the unconfirmed note in the line)

**What we take**
- A saved filter is a Sindri list: rows of notes with property columns (`kind`, `status`, `repos`, `issues` from front matter, the library convention's keys), same row grammar, same keys, sortable columns.
- Library docs are one such view (`kind:`, `status:`), not a separate feature.

**Where one app wins**: columns can be live work facts (linked PR state, open issue count), not only front matter.

**Desktop gain**: table of 10k notes scrolls at 60 fps (virtualised), sort by any column < 100 ms.

**Sindri today**: partial. The library's front-matter keys (`id`, `kind`, `status`, `repos`, `issues`, `project`, `parent`) are already parsed for `aw-doc: 1` files, and the Reviews page has a library sheet. Plain notes without `aw-doc` are skipped on purpose.

**Verdict**: take and change.

---

## 14. Know which requirement of a design doc has a PR and which has none

**Best today**
- Nothing solid found for "requirement id -> PR -> test row". ADR tools (adr-tools, Log4brains) record a decision's status (proposed, accepted, deprecated, superseded) and list or publish ADRs; they don't track build progress per item (search results; I did not read their READMEs). https://adr.github.io/adr-tooling source: none (not read; see the unconfirmed note in the line)
- Linear docs and Notion databases link a doc to issues, not items inside a doc to PRs (unconfirmed for Notion). source: none (not read; see the unconfirmed note in the line)

**What we take**: nothing. This is Sindri's own feature (the `Covers:` line and the library). Keep it.

**Where one app wins**: it already does, by design; this is the strongest "one object model" case on the surface.

**Desktop gain**: n/a (parse is local; `sindri library list` runs from the stored data).

**Sindri today**: done for design docs with `aw-doc: 1` (`docs/conventions/design-docs.md`, `GET /sindri/library`). Known open issues: #2830 (the Ui calls library roots and review settings routes the host doesn't map yet), #2555 (Reviews shows "Library: reading..." with no meaning).

**Verdict**: ours is already better, with the caveat that I found no competitor to measure against. Proof is the rules table and `sindri library validate` in the docs.

---

## Closing table

| Problem | Best tool | Verdict | Sindri today (issue) |
|---|---|---|---|
| 1 Capture in < 2 s | Obsidian CLI `daily:append` + Raycast "Append to Daily Note"; Apple Quick Note | take and change | missing (#2981) |
| 2 Today's page | Obsidian Daily notes; Foam `$FOAM_PREVIOUS_DAILY_NOTE` | take and change | missing (#2981) |
| 3 Link while typing | Obsidian `[[`; Bear heading drill-down | take and change | missing (#2981) |
| 4 Backlinks + unlinked mentions | Obsidian Backlinks pane | take and change | missing; edges planned by context (#2981) |
| 5 Jump by half a name | Obsidian Quick switcher | take as is | Ctrl+K exists, no note source |
| 6 Search with filters | Obsidian operators; zk FTS | take and change | missing for notes; library has no text search |
| 7 Open tasks across notes | Obsidian Tasks (MIT) | take and change | partial: one-note todo sync (`docs/sindri/todo-note.md`) |
| 8 Templates | Obsidian Templates; Foam | take as is | missing |
| 9 Live state of linked work | Notion link previews; Obsidian GitHub Link | take and change | missing |
| 10 Line to issue | Linear "Create issue from selection" | take and change (thin evidence) | missing |
| 11 History / undo | Obsidian File recovery; Notion history | take and change | missing |
| 12 Agent read/append by rule | Basic Memory; Obsidian CLI | take and change | plan only (#2981 comments) |
| 13 Notes as a property table | Obsidian Bases | take and change | partial: library front-matter keys |
| 14 Design-doc items vs PRs | none found | ours is already better (no rival found) | done (library, `Covers:`); #2830, #2555 open |

## Open questions for the owner (only the ones that change a design)

1. Should the todo sync widen from one note to every task line in the vault (problem 7)? Obsidian Tasks does it, but one wrong bulk edit then reaches many files. I'd keep one note until the single-note sync has run a month.
2. Where do live chips and brief blocks (problems 2 and 9) get stored? I'd say never in the file: the Markdown keeps `[[#123]]` and the app draws the state.

## What we take

1. A global-hotkey capture bar that appends to today's daily note under a heading, plus `sindri note add` for scripts: #3034.
2. Daily note on `g d` with gap-skipping previous/next, Obsidian file naming so the owner's vault opens unchanged: #3035.
3. `[[` link popup (recents, fuzzy, `/` headings, issues/PRs/runs/scopes) with a backlinks and unlinked-mentions pane under the peek: #3036 (context edges are planned on #2981).
4. A `note:` scope in the Ctrl+K palette, Obsidian quick-switcher rules, over a SQLite FTS index with Obsidian's search operators: #3037.
5. Note chips for live issue/PR/run state and "make issue" from a line, state kept outside the Markdown file: #3038.
6. Per-note history with agent writes as accept/reject diffs, folder-level retrieval switch: #3039 (context's `notes` source is a plan on #2981).
7. Keep the design-doc library and `Covers:` as is; fix its two open gaps: #2830, #2555.
