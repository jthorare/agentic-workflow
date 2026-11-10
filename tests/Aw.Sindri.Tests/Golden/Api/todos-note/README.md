# Hand-written todo-note goldens (todo spec Ph3b, first commit)

Written before the routes existed; the server now serves them (#2418), so these files are regenerated with `AW_GOLDEN_UPDATE=1` and any field that changed on the way is called out in that PR.

- `note.json`: `GET /sindri/todos/note` while syncing, one open conflict, one pick in its Undo window, one line that
  can't sync.
- `hold.json`: the same route with an open hold (REQ-E7) of four held changes.
- `conflict.json`: `GET /sindri/todos/{id}` for a todo with an open both-places conflict (D11), in the `TodoItem`
  shape the list serves, without the stored row's internal fields (#2355 makes the route answer it).

Fields of the note route:

- `state`: the doctor row's words: `not set`, `waiting for import`, `syncing`, `one way`, `can't read`, `note busy`.
- `vault`, `note`: `todo.obsidian`; null when `not set`.
- `lastCheckAt`, `lastSyncAt`, `lastError`: the last 60 s check, the last `todo.note.synced`, the last failure's reason.
- `conflicts`: ids of todos with an open conflict, oldest first (each row's `conflict` holds both versions).
- `hold`: null, or the open hold: `noteHash` (sent back by Apply and Put them back), `heldAt`, `openInBase` (the base's
  open todos, for the 10% rule), `changes` (per id: `kind` `removed` | `edited` | `reopened`, Sindri's `text`, the
  note's `noteLine`, null for removed).
- `picks`: the picks still in their 10 s Undo window: `kind` `conflict` (with `id`, `kept` `sindri` | `obsidian`) or
  `hold` (no `id`, `kept` `applied` | `restored`), `by`, `undoUntil`.
- `cantSync`: note lines that can't sync: 1-based `line` and the `reason`.

A session caller, and any caller with `?fence=1`, gets every text (`text`, `noteLine`, a conflict side's `text`,
`reason`) inside the untrusted fence (§4.4); these goldens are the owner's plain view.
