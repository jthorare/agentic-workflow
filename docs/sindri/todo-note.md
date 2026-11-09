# The todo note

Sindri's todo list can sync with one Markdown note in an Obsidian vault. This page is for the owner: what to set, what happens the first time, what the two kinds of attention rows mean and how to stop. The design is `docs/superpowers/specs/2026-11-07-sindri-todo-design.md` (§4.5); routes, CLI and codes are in `docs/csharp/sindri.md`.

The note is a view of the list, never the store: the todos live in Sindri's ledger, and the sync reads and writes the note's top-level task lines.

## Setting it

1. Name the vault in the profile (`context.sources.library.vaults`, the `name` you gave it).
2. Run `sindri todo note set <vault> <note>`, for example `sindri todo note set notes Todo.md`. `<note>` is a `.md` path inside the vault: no leading `/`, no `..`, not inside a dot folder, not matched by the vault's `deny` globs. The note can't be a symlink or have more than one hard link.
3. The edit is live at once (no profile approval). `sindri todo note` prints the state.

A bad vault or note is `SND-TODO-002` with the key path (`todo.obsidian.vault` or `todo.obsidian.note`). Settings → Connections → Obsidian todo note writes the same key: **Check note** counts what the note holds and writes nothing.

## First sync and import

The first sync never writes to a note that already has open task lines until you say so.

| The note | What happens |
|---|---|
| Doesn't exist | The first write creates it with a `# Todo` heading |
| Has no open `- [ ]` lines | Syncing starts |
| Has open `- [ ]` lines | The service waits ("Waiting for import") and writes nothing |

To import from the terminal:

1. Run `sindri todo note import --dry-run`. It prints the counts (open, done, other), the line numbers of the open lines that can't import and why, and the `--expect <n>` to pass. It writes nothing.
2. Run `sindri todo note import --expect <n>` with that number.

Import re-reads the note first. If its importable count isn't `n` (you edited it since), it is `SND-TODO-409` and nothing is written: run the dry run again. A note with nothing importable is `SND-TODO-008`. An import that names another note than the profile's key is `SND-TODO-002`.

What it adopts: each open, top-level `- [ ]` line. Done lines and lines inside a fenced block, a quote or a callout are left alone. A line that breaks the todo text rules (a pasted markdown link, a `📅` or `^td-` inside the text, a forbidden character) stays as text, and the dry run lists its line number. Adopted lines get their `^td-…` id written onto them and count against the 500 open todos cap; past it the rest stay as text. Sindri's own todos are appended after the last line. The import records `todo.note.imported` with who asked.

Agents can do the same through `sindri_todo`: `noteImport` with `vault` and `note` is a dry run; adding `expectOpen` writes.

## Conflicts

When a todo changed in Sindri and in the note since the last sync, Sindri keeps its own version, leaves the note's line as it is and stops syncing that todo until a side is picked: `sindri todo keep <id> sindri|obsidian`, or the buttons on the Home panel. A pick can be undone for 10 seconds (the note is written only after that window, so Undo never touches the file). A write to a todo with an open conflict is `SND-TODO-423`.

## Holds

If one pass would apply more than 3 removals, edits or un-ticks from the note, and more than 10% of the open todos, Sindri applies none of those and asks (**Apply** or **Put them back** on the Home panel; agents use `sindri_todo` `holdApply`, `holdRestore`, `holdUndo`). Ticks and adds never count. Both picks send the note hash the row was drawn from; if the note moved since, the pick is `SND-TODO-409` and the row refreshes. Apply appends the changes by `obsidian`; Put them back rewrites the lines after the 10 second Undo window.

## Stopping

`sindri todo note stop` (or **Stop syncing** in Settings) removes `todo.obsidian` from the live profile; comments and key order stay. The note stays exactly as it is and the todos stay in Sindri. Changing the note (setting a different one) drops the base and starts the first-sync steps again for the new note; the old note is left as it is.

## One Sindri per machine

Only one Sindri should sync a given note, and that is the work laptop's. A second Sindri on another machine would see the first one's edits as changes made in Obsidian.
