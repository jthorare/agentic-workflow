# Sindri v2: settings, no profile (#2373)

Status: draft, for the owner's read. Part of #2258. Rebased on main on 2026-11-10: #2357 (#2258 PR 1, "edits apply on
save") and #2356 (retires recipes) are both merged, and this spec builds on them.

## 1. The decision

Owner, 2026-11-08, in three parts:

1. Sindri v2 has no profile concept and no approval.
2. Settings have no versions: a save applies at once.
3. Workflows keep their own versions as DB rows with an active flag. Any session, agents included, may change
   settings, and the ledger records who changed what.

#2357 already removed the approval step. `IApprovals`, `ProfileChangeApproval` and `ApproveProfileService` are gone.
`POST /sindri/profile/approve` is a tombstone (`ApproveProfileEndpoint.cs`): it answers 410 `SND-API-014`, "That
route was removed", for one release, and this spec keeps it until PR 2 (§8), which deletes the route and its golden. `sindri profile approve` prints "removed: edits
apply on save" and exits 0 (`Verbs.cs:143`); §5 changes that verb, like every `sindri profile` verb, to exit 2.

What #2357 leaves behind:
- the noun "profile"
- a content hash (`ProfileHash`, `profileHash` in responses, `IfHash` on write requests) that writes send as a guard
  (the full list is in §4)
- the `SND-AUTH-002` refusal on two local CLI verbs, `profile set` and `profile init` (`Verbs.cs:100-108`), and on
  source-token, review-login and Linear connection writes (§3.4)
- the `profile.checked` event, still written by `CheckProfileService`, and the profile view built from it (§4)
- a dead Setup step and a dead NeedsYou row for the profile (§4)

This spec removes all of these. It adds no new database table. The new files are `settings/changes.jsonl` (the
audit lines) and `settings/.last-saved.yaml` (the diff base for edits made outside Sindri), both under §3.

## 2. Workflows: nothing to add

Ledger migration 0008 already has the model the owner described:
- `workflow.active_revision` is the active flag (null = off).
- `workflow_revision (workflow_id, revision, spec, spec_hash, created_by, created_at)` is one immutable row per save.
- Saving on a revision that is no longer the latest is `SND-GRAPH-007`.
- `Activate` pins the revision triggers run.

This spec changes none of it.

## 3. Settings

### 3.1 The file

- **One file:** `$AW_SINDRI_HOME/settings/settings.yaml`, edited in place, comments kept, as `profile.yaml` is today.
- **A save applies at once.** Whatever a save writes is live when the write returns. There is no draft, approval or
  history.
- **Files and modes.** `settings.yaml`, `.last-saved.yaml`, `changes.jsonl` and `.lock` are 0600 in a 0700
  `settings/`. Every temp file is created 0600, so a rename never widens the mode. The migration (§5) creates the
  folder and files with the same modes. On Windows, modes don't apply; the folder sits under the user's profile.
- **The other files sit beside the settings file.** The settings path can be overridden (`AW_SINDRI_SETTINGS`, read
  in `SettingsFromEnvironment.cs:17` like `AW_SINDRI_PROFILE` today), so `.lock`, `.last-saved.yaml` and
  `changes.jsonl` live in the folder of whatever file the path names, never at a fixed place under the home. Two
  homes that share one settings file then share one lock. An override folder must hold one settings file only (the
  three names are fixed), and Sindri sets the 0700 mode only on the default `settings/` folder. Serve and `settings validate` refuse an
  override whose folder is `$HOME`, is not owner-only, or already holds a `changes.jsonl` whose file id belongs to
  another settings file name.
- **"Changed" means the sha256 changed.** Reads go through `LiveApprovedProfile` (becomes `LiveSettings`). Today it
  keeps the parsed answer while the file's last-write time and length stay the same, and `IProfileCache.Invalidate()`
  clears only the process that wrote. That misses a same-length hand edit inside one mtime tick, and a terminal
  `settings set` while serve runs would wait for serve's watcher. So the cache is keyed on the sha256 of the bytes
  read: every read reads the file (a few KB), hashes it, and parses only when the hash differs from the cached one.
  No mtime and no length are used, so any process's save or edit is live for serve's next read, with no
  `Invalidate()`: `IProfileCache` and its calls in the six writers are deleted. `ISettingsSource` parses the very
  bytes `LiveSettings` hashed, never a second read, so hash and parse can't describe two different files. Reads open
  the file with `FileShare.ReadWrite | FileShare.Delete`, so a Windows rename never fails under a reader. Serve's watcher (below) no longer
  keeps the cache fresh; it only feeds the audit.
- **Serve's watcher replaces `ProfileWatch`.** `ProfileWatch` (`Host/Server/ProfileWatch.cs`) is today's 30 s check as
  caller `service:profile-watch`; it only exists to record `profile.checked`, so it is deleted (§4). Serve now watches `settings/` (`FileSystemWatcher`, debounced 500 ms, plus a 30 s poll as
  a fallback). On a change it runs the outside-edit check (§3.4) and copies new audit lines. When the lock is busy
  it retries on the next tick, never waiting on a request thread. When `changes.jsonl`'s file id changes (the file
  was replaced), it rescans from the start.

### 3.2 A write

Six writers change the file today, and none of them locks: `YamlProfileKeyWriter` (one scalar key, key removal, and
the `todo.obsidian` plans), `YamlProfileIndexWriter` (pages and pins), `YamlProfileRepoWriter`,
`YamlProfileSandboxWriter`, `YamlSourceTokenProfileWriter` and `FileProfileInitializer`. Each reads the file, edits the
text and renames a temp file on its own. Three of them (repo, index, sandbox) hold a static `lock (_gate)`, which
covers one process only, and retry once when the file changed under them; a second conflict is an error that says to
try again.

After this change they share one `SettingsFileLock` and one async save routine (steps 1 to 6 below). A writer no
longer plans an edit and saves it later. It passes its edit as a callback, and the routine runs the callback inside
the lock: it reads the text, hands it to the callback, which edits it and validates the result (async, local, no
network), and the routine writes it, then logs it. `IProfileKeyWriter` loses its `Plan`, `PlanText`, `PlanTexts`,
`PlanRemove` and `Save` split and takes a callback instead; `SetReviewLoginService` and `TodoNoteProfileEdit`, which
call it, change with it. The static `_gate` is deleted. The retry is deleted too: with the edit inside the lock, the
text cannot change between the read and the write, so the only `SND-SETTINGS-410` left is a hand edit. Without this, a
writer that planned before the lock and saved after it would see another writer's save as a conflict, and "saves to
different items both land" would fail. The lock is held for milliseconds, because validation reads no network and the
file is a few KB. All six writers go through that one lock. One rule keeps the lock from waiting on itself: code that a writer's
callback reaches never takes `SettingsFileLock` or `Run`'s wait path. An architecture test checks it.

Every write takes an exclusive lock on `.lock` beside the settings file (`settings/.lock` by default). Each write opens its own handle (in-process writers never
share one), and tries the lock without blocking every 50 ms for up to 5 s, so a waiting HTTP request never holds a
thread asleep. Linux and macOS use `flock`; Windows opens `.lock` with `FileShare.None`. In order, while it holds
the lock:

1. Run the migration's inner step, `SettingsMigration.RunLocked`, which assumes the lock is held (§5). It does
   nothing once the migration is done and the file exists; on a home with no file it writes the defaults. It
   returns one of `created`, `migrated` or `existed`, so `settings init` can tell a fresh home from a file that was
   already there (below). (Taking the lock again here would wait on itself: two handles on one file conflict under
   `flock`.)
2. Read `settings.yaml`. Take the file's sha256 and compare it with the last parseable audit line's sha256 (never
   with older lines, so a hand revert to an older text is still caught). The writer finds that line by reading the
   last 64 KB of `changes.jsonl` and walking back from the end; when no line in that tail parses, it counts as no
   line, and the edit is recorded as `outside`:
   - different: someone edited the file outside Sindri since the last save, so append that edit's line first and
     refresh the base (§3.4), then go on;
   - the same, but `.last-saved.yaml` differs from the file (a crash before step 6 last time): refresh the base.
3. Run the writer's edit callback on the text just read and validate the result (§3.3). The callback has a 3 s
   deadline, under the 5 s the waiters poll for; when it runs out (a slow ledger read, say), the write answers
   `SND-SETTINGS-409` and writes nothing. When the result is invalid or changes nothing, stop: nothing is written.
   Once step 3 passes, steps 4 to 6 run with `CancellationToken.None`: a request that is cancelled after the rename
   must not skip the audit line.
4. Write the result to a temp file and fsync it. Re-read `settings.yaml`: when its sha256 isn't the one from step 2,
   the file changed outside Sindri in between, so stop with `SND-SETTINGS-410` and write nothing. Then rename the
   temp file over `settings.yaml`. This is the save. The re-read narrows the window, but it doesn't close it: a hand
   edit landing between the re-read and the rename is overwritten. That's why the docs say to change settings with
   `sindri settings set` or the app while agents run.
5. Append one line to `settings/changes.jsonl` (§3.4).
6. Copy the saved text to `.last-saved.yaml` (temp file and rename).

A crash after step 4 and before step 5 leaves a save with no line. The next writer or serve's watcher sees a
sha256 that isn't the last line's and records it as edited outside Sindri, diffed against the base, so the change
is still in the ledger, under the wrong name. A crash between steps 5 and 6 leaves a stale base, which step 2
refreshes. Why a file lock: `sindri settings set` runs without the control plane, so a guard inside serve wouldn't
cover it.

**Busy lock.** After 1 s of waiting, the CLI prints `waiting for another settings save...` on stderr. When the 5 s
run out, the write fails with `SND-SETTINGS-409`: "Another change is being saved. Try again in a moment."
`SND-SETTINGS-410` is "The settings file changed on disk while you were saving. Nothing was saved; try again."

**How a write fails.** Every refusal has the same shape on the CLI and the API:

| Code | Error kind | CLI exit | HTTP |
|---|---|---|---|
| `SND-SETTINGS-002`, `020..024` (a rule, a broken file) | Invalid | 2 | 400 |
| `SND-SETTINGS-025` (`settings init` when the file exists) | Conflict | 2 | 409 |
| `SND-SETTINGS-026` (a session editing a credential key, §3.4) | Forbidden | 2 | 403 |
| `SND-SETTINGS-409` (busy), `410` (changed on disk) | Conflict | 2 | 409 |
| serve isn't running and the caller is not the person (below) | Unavailable | 1 | n/a |

These are the codes' real mappings today: `HttpResults.StatusOf` turns Invalid into 400, Conflict into 409 and
Forbidden into 403, and `Output.Error` exits 1 only for `NotRunning` and 2 for every other error. The CLI prints
`CODE message` on stderr, as every verb does. `--json` printing `{ "code", "path"?, "message" }` on stdout, with
`path` set when a rule names a key, is new for these verbs. The API answers with the same body. 409 and 410 share
HTTP 409, so clients key on `code`.

**Every write is item-level.** A write sets or removes items in the current text and never replaces a whole list:
- one scalar key (`settings set <key> <value>`)
- one key removal, by key path (`todo note stop` removes `todo.obsidian`; the key's lines go, comments and other
  lines stay, and a parent map left empty goes too). It is an item write like the others, so a concurrent save to
  another key is kept
- one todo note setting (`todo.obsidian` with its `vault` and `note`), set or removed, as above
- one repo, by name
- one page, by id
- one pin, by id
- one sandbox host
- one connection or library source, by name (the source-token writer, `YamlSourceTokenProfileWriter`)

`context page set --from-file` and `context pin set --from-file` are one write each: one lock hold, one save and
one audit line, setting every item in the file. They never remove an item the file leaves out; removing is
`remove <id>` only, and the CLI's summary line says so (`set 3 pages; pages not in the file are kept`).

Two saves only collide on the same item, and then the later one wins. That's the right answer for one person's
app. Without the rule, a stale app form that replaces the whole pages list would drop a page an agent added in
between. The hash check existed for approval (the approved text must not shift under you), and approval is gone. The
`--if-hash` flag and the `IfHash` request fields on page, pin and todo note writes go with it (§4).

**The write route, and who reaches it.** `profile set`, `profile init` and `profile validate` are `ActionClass.Local`
today (`ShellRows.cs:22-28`): they run in the CLI's own process, so no header or token reaches serve and the `by`
rules in §3.4 would have nothing to read. This spec adds one route, `POST /sindri/settings/edit`, in the style of
`POST /sindri/todos/note/set` and `POST /sindri/context/pages`. Its body is `{ op: "set" | "init", key?, value?,
saveId? }`. There is no `remove` op: key removal stays inside the app (`todo note stop` keeps its own route), so no
verb or route lets a caller remove a key by path. Its action class is `Job`, which allows terminal, app, session, launched and service callers
(`Authorization.cs:23`), so sessions may write. The repo, page, pin, sandbox, source and todo note writes keep their
own routes, which call the same save routine.
- `sindri settings set` and `settings init` call the route when serve runs. They send the headers a CLI sends today
  (`X-Sindri-Caller`, the human token when the CLI can read it, the launch headers inside a launched session), so
  serve decides `by`.
- When serve is down, only the person writes: the CLI writes in process, under the terminal rule of §3.4. A session
  with serve down gets `NotRunning` (exit 1, "fix: sindri"), because it has no way to prove who it is and
  a sandboxed session may not even see the home.
- **What "serve is down" means for the CLI.** A refused connection, or a control file whose pid is not alive, is down:
  the person may write in process. A connection that opens but gets no answer within the CLI's cap is not down: the CLI
  exits 1 with a message, and never writes in process, because serve may be mid-save. The CLI call has that cap.
- **`settings init`.** It calls `RunLocked` and reads what it returned: `created` or `migrated` exits 0 (a fresh home
  gets one `created` line, not a refusal), and `existed` is `SND-SETTINGS-025`.
- `settings validate` stays `Local`: it reads, and runs the migration only when it is due (§5).

**The other write routes.** Settings writes stop returning `SND-AUTH-002`, and with it the per-start human token is no
longer a permission for them. The code stays for the other owner-only verbs (`sandbox login`, the Linear writes,
`settings cleanup`). What still
guards the routes is what guards every command route today: serve listens on loopback only, `LoopbackGuard`
checks the Host, and `CommandOriginRefusal` answers any request with an `Origin` header with 403 `SND-API-008`.
That refusal keys on `CommandRouteMetadata`, which every route in `EndpointTable`'s `commands` group carries, so
every new settings, repo, page, pin, sandbox and source write route is mapped there. An architecture test checks
it. The routes also require `Content-Type: application/json`, so a form post without an `Origin` header fails
binding. Reads stay as they are.

### 3.3 What validation checks

With approval gone, validation is the only check before a write goes live. Every refusal names the key path and
the rule. It never quotes the value, so a pasted secret never reaches the API, the logs or the ledger.

| Rule | Runs on | Code |
|---|---|---|
| Schema: known keys, types and ranges, as `IProfileSource.Load` checks today | every read and every write | `SND-SETTINGS-002` (today's `SND-PROFILE-002`; the message names the key path) |
| `workflow.http.hosts`: at most 50; each a lower-case DNS name (no scheme, port or path, no wildcard, no IP, no `localhost`); no repeats (`FlowRules.Hosts`) | every read and every write | `SND-SETTINGS-002` |
| `workflow.callDepth`: a whole number from 1 to 10 (`FlowRules.CallDepth`) | every read and every write | `SND-SETTINGS-002` |
| `workflow.poll`: `intervalSeconds` at least 30, `catchUpPages` 1 to 100 (`ProfileRules.Poll`) | every read and every write | `SND-SETTINGS-002` |
| `workflow.forEach`: `maxItems` 1 to 10000, `batchSize` 1 to 100 (`ForEachRules`) | every read and every write | `SND-SETTINGS-002` |
| `todo.obsidian`: `vault` names one of the `context` library vaults; `note` is a vault-relative `.md` path with no `.` or `..`, no dot folder and no deny glob of that vault (`TodoObsidianRules`) | every read and every write | `SND-TODO-002` (unchanged; it is a todo code, not a settings code) |
| Repo add: the path exists and is a git checkout; not `/`, not `$HOME`, not a parent of `$AW_SINDRI_HOME`, not inside it | the repo add only | `SND-SETTINGS-020` |
| Repo path shape: not `/`, `$HOME`, a parent of or inside `$AW_SINDRI_HOME` | every read and every write | `SND-SETTINGS-020` |
| Sandbox host: a host name, or `*.` followed by at least two labels (`*.example.com`, never `*.com`); no bare `*`, IP literal or `localhost` | every read and every write | `SND-SETTINGS-021` |
| Source URLs (connections, library vaults): `https`, no user name or password, no query string; loopback only when the matching `AW_*_URL` test override names it | every read and every write | `SND-SETTINGS-022` |
| Credential references: `env:NAME` with `NAME` matching `[A-Z_][A-Z0-9_]*`; `command:argv` whose first word is an absolute path or a bare name found on serve's `PATH` at save time (it runs without a shell, so no shell syntax applies) | every read and every write | `SND-SETTINGS-022` |
| Secrets: a free-text value, or a word after the first in a `command:argv`, that matches an `Aw.Redaction` pattern | every write | `SND-SETTINGS-023` |
| The file itself is broken (doesn't parse or fails a rule above) | every write | `SND-SETTINGS-024` |
| A session makes an edit that touches a credential key (§3.4) | every write | `SND-SETTINGS-026` |
| `settings init` when `settings.yaml` exists | `settings init` | `SND-SETTINGS-025` |

- **Error codes: what is true today.** `SindriErrors.cs` defines three profile codes: `SND-PROFILE-001` (no
  profile file), `002` (invalid: every schema, range and cross-key failure, with the key path in the message, so the
  new flow, poll, forEach and call-depth rules above all answer 002) and `003` (the hash doesn't match). The
  numbers `004` to `017` in `docs/sindri/errors.md` and `docs/csharp/sindri-profile.md` are old documentation that no
  code throws (the one use in `SetupStepsTests.cs:143` is a made-up code), and several of those rows already say
  "Removed". So the rename carries `002` only, deletes `001` (no caller sees a missing file after §5) and `003` (with the hash
  guard), and adds `020..026`, `409` and `410`. It does not carry `004..017`, and the docs rows for them are removed.
  Rules that already answer a code of another family (`SND-TODO-002`) keep it.
- **Not checked at save today.** `sandbox.limits.memory` has no format rule in the settings checks. The workflow
  check reads it as a cap (`ProfileWorkflowChecker.BoxCap`): `auto`, no value, or a size that doesn't parse caps
  nothing there. This spec adds no rule; the other `sandbox.limits.*` sizes are the same.
- **Which fields the secret check scans.** Free-text strings (titles, summaries, notes, aliases, labels and
  feature names) and the arguments of a `command:argv`. Id, hash, ulid, path, URL and `env:` fields are never
  scanned (so `todo.obsidian.note`, a path the `todo.obsidian` rule already limits to a vault-relative `.md` file,
  is not), so a long id or a sha256 never trips it. The message: "This looks like a password or token. Reword it,
  or leave the secret out." For a `command:` argument it adds: "Have the command read the secret itself, for
  example from an environment variable."
- **A missing checkout is not a broken file.** The "exists and is a git checkout" check runs only when a repo is
  added. A repo whose folder is moved or deleted later shows a Health warning on that repo ("`checkout`: the folder
  /home/me/src/checkout is gone"), and the rest of the settings keep working.
- **An edit on a broken file.** When the file already fails a rule (a mistake made outside Sindri), every write is
  refused with `SND-SETTINGS-024`: "Your settings file has a mistake on line 14 (`sandbox.hosts[3]`: not a host
  name). Fix that line first; nothing was saved." It names the line, never blames the edit being saved, and never
  quotes the value. All writes stop until the file validates again.
- **Agent reach.** `workflow.http.hosts` is a list of hosts an `http` node may send to, so it follows §10's rule for
  sandbox hosts: no Needs-you row, shown in Last changed, See changes and the ledger. It keeps its own check
  above, which has no wildcard, so `*.example.com` is refused there. That check is about shape only. A name that
  looks fine can still point at an address that Sindri's own network rules should refuse (a service like `nip.io`
  turns any name into an IP). This spec does not change what an `http` node can reach; it only says the settings
  check doesn't prove a host is safe.
- **Repo remove** drops the settings entry only. It never touches the checkout or its worktrees.

### 3.4 Who changed what

- **Who may write:** any session, agents included (agent-first D2). `settings set`, `settings init`, the page, pin,
  repo, sandbox, source and todo note writes all accept sessions. What `SND-AUTH-002` covers today, so what changes:
  - the two local CLI verbs `profile set` and `profile init` refuse a session Sindri launched, and a CLI with no
    readable per-start human token (`Verbs.cs:100-108`). `settings set` and `settings init` drop that refusal;
  - the HTTP writes for repo add, pages and pins are `MapJob` routes (`ContextJobEndpoints.cs`) in action class
    `Job`, which already allows `Session` and `Launched` (`Authorization.cs:23`). They need no change;
  - Linear connection writes (`LinearConnectionRules.IsOwner`) refuse every session: only a human or a service may
    write them. The review-login write (`SetReviewLoginService`) takes any caller that has a context, and it already
    records the caller. Both keep today's rules;
  - **new, because `settings set` takes any scalar key:** a session may not make any edit that touches a credential
    key. The set comes from the writers' key constants (the source-token and Linear write-token writers) and from a
    test that walks every credential-reference key in `SindriSettings` and fails when one isn't in the set; for
    example `context.sources.linear.writeToken`. A future op inherits the rule. The route and the in-process CLI refuse a session with
    `SND-SETTINGS-026`: "This key holds a connection token. Set it in the app or from your own terminal." The app and a
    person's terminal may set it, as today.
- **`by`** comes from the caller rules serve and the CLI already share (`CallerClass`, `X-Sindri-Launch`). The
  per-start human token stops being a permission for settings writes, but it stays the test for `person`:
  - with serve running, `person` needs the token, as `CallerClass.Human` does today (the app, or a CLI that can
    read the token). A request that says `X-Sindri-Caller: human` without a valid or with a stale token is refused
    403 `SND-AUTH-001` by `CallerContextMiddleware` and writes nothing: it is never downgraded to `session`. A
    request that carries a launch header or launch secret is a session before that check;
  - with serve down, the CLI writes in process only for the person, under the terminal rule: a terminal with no agent
    signal is `person`. Any other caller gets `NotRunning` (above);
  - a session Sindri launched is `launch:<id>`;
  - any other session is `session`, with its repo when it sends one.

  With serve down, `person` is best effort: the terminal rule is a heuristic, and `by` is a label, not proof.
- **Settings tools for agents.** In v1 sessions use the CLI and the route only. MCP settings tools are a follow-up
  issue, not part of this spec.

  Edits found outside Sindri are `outside`, the migration's line is `migrated`, and the defaults' line is
  `created`. Every app save and CLI save
  sends a save id (a ulid, made before the request goes out) that the line carries as `saveId`, so a sheet can
  tell its own save from any other, even when the event arrives before the save's response.
- **The line.** Each line in `changes.jsonl` is `{ at, by, saveId?, sha256, changes[] }`.
  - `sha256` is the hash of the file text the line's save produced.
  - `changes[]` holds `{ path, from, to }`. Paths name items by key, not position (`repos[checkout].path`,
    `index.pages[review]`), so removing a repo never shifts another's path. `path`, `from` and `to` pass through
    `Aw.Redaction` first (a match becomes `[redacted]`), and only then are `from` and `to` capped at 200
    characters, so a token cut at the cap is still caught. That covers edits outside Sindri, which never went
    through the secret check.
  - **Size.** A line stays under 64 KB. When `changes[]` would pass that (a huge `--from-file`, or a big edit
    outside Sindri), the line keeps every `path`, drops `from` and `to`, and sets `"trimmed": true`. If the paths
    alone still pass it, the line keeps the first paths that fit and adds `"more": <count>`. A save is never
    refused for its audit line's size.
- **Edits outside Sindri.** A hand edit, an agent's Edit-tool save or a crashed save all look the same: the file's
  sha256 matches no line. Two places catch it:
  - every writer, at step 2 of a write;
  - serve's watcher, for an edit that no save follows. When serve sees the file change, it takes the lock, re-reads
    the sha256, and skips when it equals the last line's (a writer's own save, or a writer that already caught it).

  Either way the line is `by: outside`, and its `changes[]` is the diff between `.last-saved.yaml` and the current
  text, redacted as above. Then the current text becomes the new base, so the next outside diff starts from it.
  The line's `at` is when Sindri saw the edit, not when it was made. When the current text doesn't parse, nothing is
  appended: there are no paths to name, and Health already shows the mistake. The line is written once the file
  parses again. When `.last-saved.yaml` is missing (a home from before this change), the line holds `changes: []`
  and writes the base.
- **The ledger.** Serve owns the ledger, and `settings set` can run while serve is down. Today `profile set` writes
  no event at all. So serve copies every line into a `settings.changed { file, line, at, by, saveId?, sha256,
  changes[] }` event: at start, and each time it sees `changes.jsonl` change. The event's time is the line's `at`,
  not the copy time, so a save made while serve was down keeps its real time.
- **The key.** Line 1 of `changes.jsonl` is `{ "file": "<ulid>" }`, written when the file is first created. Each
  event's idempotency key is `settings.changed:<file>:<line>`. `SqliteLedger.InsertOnce` drops a second insert
  with the same key, so a re-copy after `views rebuild` changes nothing.
- **Never trimmed.** `changes.jsonl` is never trimmed or deleted, by Sindri or by `settings cleanup`, so line
  numbers stay stable. If someone deletes it by hand anyway, the next save starts a new file id, so its keys never
  collide with the old ones. Serve keeps a copied-through byte offset and the file id in memory and reads only
  new bytes; it rescans the whole file only at start. `sindri doctor` warns when the file passes 10 MB.
- **Complete lines only.** Serve copies a line only once it ends in `\n`. A last line without one may still be in
  flight, so serve leaves it for the next pass and doesn't count it yet. Before it appends, a writer adds `\n` when
  the file doesn't end in one. That turns a crash's partial line into a complete line that doesn't parse. Serve
  counts such a line but skips it, logs one warning, and keeps line numbers stable. A copied line must also match
  the line schema (`at`, `by`, `sha256`, `changes[]`, under 64 KB), or it is skipped the same way.

### 3.5 A broken file never stops work

A hand edit or an agent's Edit-tool save can still break the file. Serve then keeps running on the last settings
that validated. Health shows "Your settings file has a mistake on line 14 (`sandbox.hosts[3]`: not a host name).
Sindri keeps using your last good settings, from 14:02. Fix the file and save it, and this clears." `sindri
settings validate` prints the same text. The message clears on the next read that validates. A fresh start with a
broken file and no last good settings runs nothing, as today.

### 3.6 What the person sees

- **Who, in words.** One set of words everywhere (Last changed, See changes, the stale banner):
  - `person` with this sheet's `saveId`: "you"; `person` from another save (the CLI, another window): "you,
    elsewhere";
  - `launch:<id>`: the session's title, as the Agents page shows it; when the ledger no longer has the session,
    "an agent session";
  - `session`: "an agent session";
  - `outside`: "someone outside Sindri";
  - `migrated`: "the move from your old settings";
  - `created`: "Sindri's defaults".
- **Which kinds have a sheet.** The stale banner and the busy and refusal messages below attach to the places the
  person edits settings in the app today. The rest is read-only in the app, and changes by the file, the CLI or an
  agent:

  | Kind | Where the person edits it | Stale banner, 409/410 messages |
  |---|---|---|
  | page | the page sheet and the add-several-pages sheet (Context) | yes |
  | pin | the pin sheet and the pin-several sheet (Context) | yes |
  | source (connection, library) | the Connections card, Linear form and Library sheet | yes |
  | setting | the todo note card; the review login, through a **Change** link on the "reviews as <login>" line | yes |
  | repo | Add repo only; no edit sheet | add only |
  | sandbox host | none; the Sandbox section is read-only | no |
  | model, label and surface picks | none; read-only lines with Copy | no |

  The three read-only lines say "Change it in the profile" today (`SettingsView.axaml:19-71`); they say "Change it in
  settings.yaml" instead. The Sandbox card has no such line today (the words are only in an XML comment,
  `SandboxSettingsSection.axaml:6`), so that line is added under its heading, not swapped. Neither has a button of its own (the Settings heading row has the one
  Copy file path button, below). The stale banner's kinds are source, setting, page and pin: the kinds that have a sheet or
  card. "Sheet" below means a sheet or a card. **Bulk sheets** (add several pages, pin several): they show no stale
  banner. A 410 on a bulk save shows "These pages changed on disk. Reload to see them." (or "These pins ..."), and
  **Reload** refreshes the list and keeps the ticks for items still on disk. **Keep mine** and **Load theirs** exist
  on one-item sheets only.
- **Review login.** Today the field disappears once a login is set, and Reviews shows only "reviews as <login>". That
  line gets a **Change** link, which opens the same sheet the table gives the review login (stale banner, 409/410
  messages). It is not file-only.
- **The file path, always.** One **Copy file path** button per page: the Settings page's sits in its heading row and is always shown, with or
  without an audit line and not only when the file is broken. "Copied" uses the page's polite live region, not a
  sheet's.
  Agents and the person's editor are the normal way to change the read-only parts, and the Setup step "profile" was
  the only place that said where the file is. When the file validates again after a break, focus stays where it was:
  the save button was focusable all along, so nothing moves. The stale banner never takes focus; the shared live region
  announces it.
- **Loading and errors.** While Settings loads, its rows show their labels with empty values and no
  buttons. When the load fails (serve down, an error), Settings shows "Can't reach Sindri" with **Retry**. There is
  no "no settings" state: Sindri writes the defaults before the first read (§5), so the person never presses a
  button to get settings. On a fresh start with a broken file and no last good settings, the rows are
  read-only under the Health text from §3.5.
- **Last changed.** One line under the Settings heading, read from the last audit line: "Last changed by <who> at
  14:02", or "Changed by someone outside Sindri at 14:02", or "Moved over from your old settings at 14:02", or
  "Created with the defaults at 14:02". Times are local, and get a day when not today ("yesterday at 14:02", "Nov 6 at
  14:02"). With no audit line the line is hidden. A save's response carries `{ by, at }`, so the line updates the
  moment the person saves, and `settings.changed` refreshes it for everyone else.
- **See changes** (PR 2, §8). A link next to Last changed opens a panel with the last 50 settings changes,
  newest first: one row per change with <who>, the time, and each path with its from and to (`[redacted]` and trimmed
  values shown as written). It reads `GET /sindri/ledger?type=settings.changed&newest=true&max=50`; `newest` is one
  new optional parameter on that route. The panel has a loading row, "No changes yet", and "Can't reach Sindri" with
  **Retry**. The ledger lags `changes.jsonl` by one copy pass, so the panel re-reads on `settings.changed`. Closing it
  returns focus to the link. The CLI equivalent is `sindri ledger --type settings.changed`.
- **Saving.** While a save waits for the lock, its button shows `Saving...` and is busy (`aria-busy`, a second
  click does nothing). A failed save keeps the typed values:
  - `SND-SETTINGS-409`: "Another change is being saved. Try again in a moment." with **Try again**, which sends the
    same save. Focus moves to **Try again**.
  - `SND-SETTINGS-410`: "The settings file changed on disk while you were saving. Nothing was saved." The sheet then
    re-reads its item. When the item on disk now differs from the values the sheet opened with, the outside edit
    touched it: the message becomes "This <kind> changed on disk. Reload to see it." with **Reload** only, which
    fills the sheet with the saved values, so a retry never overwrites an edit the person hasn't seen. Otherwise
    **Try again** shows, as for 409. PR 2 replaces the Reload message with the stale banner (below).

  The codes show only in the CLI's `--json` output and the logs.
- **Refusals on a field.** A rule refusal shows under the field it names (`aria-invalid`, linked by
  `aria-describedby`), the field keeps the typed value, and focus moves to the first refused field. The live region
  says "1 problem: <field label>".
  - 020: "That folder doesn't exist." / "That folder isn't a git checkout." / "Sindri can't use this folder as a
    repo." (for `/`, your home folder, and Sindri's own folders)
  - 021: "Use a host name like api.example.com, or *.example.com for every host under it."
  - 022: "Use an https address with no user name, password or query." / "Use env:NAME, or a command Sindri can find
    on its PATH."
  - 023: the secret message in §3.3.
- **A broken file.** Every sheet in the table above shows the Health text from §3.5 at its top, above any stale banner. Its
  save button stays focusable but inactive (`aria-disabled`), with "Fix the settings file first" as plain text
  above it. The Settings heading's **Copy file path** copies the path of `settings.yaml` and shows "Copied" for 2 s. On any
  other page (Context, Reviews, and so on), a broken-file sheet or card shows the settings file path as text with its
  own **Copy file path** button. That is the only Copy button on that page, and its accessible name is the same. When the file
  validates again, the text goes and the save button comes back without reopening the sheet. Last changed adds
  "(the file has a mistake now)" while it's broken.
- **A stale open sheet** (PR 2, §8). Every one-item sheet in the table above watches `settings.changed`. When an event touches the item the sheet
  has open and its `saveId` isn't the sheet's own, the sheet shows one banner, naming the item's kind (repo, host,
  source, setting, page or pin):
  - "<who> changed this <kind> while you had it open." with **Load theirs** and **Keep mine**. A newer change
    updates <who>.
  - "<who> removed this <kind> while you had it open." with **Keep mine** (saves it back) and **Close**.
  - **Keep mine** saves only the fields the person edited, so a field the other writer changed and the person
    didn't keeps their value. **Load theirs** fills the sheet with the saved values.
  - After the event stream reconnects, the sheet re-reads its item and shows the banner, with "someone" as <who>,
    when it changed in the gap.
  - A change to another item shows nothing.
- **One voice.** The busy message, refusals, "Copied" and the stale banner share one polite live region on the
  sheet. A newer message replaces an older one only at the same priority. A lower-priority message that arrives while
  a higher one shows goes to the live region only, never to the top slot. For example, a stale event during the
  broken-file text is announced, and the broken-file text stays. So two never announce at once.
- **One message at the top of a sheet, in this order.** The Health broken-file text wins. Then the 410 "changed on disk.
  Reload". Then the stale banner. Only the first that applies shows. A 410 on a broken file can't happen: the writer
  refuses a broken file first (`SND-SETTINGS-024`), and the sheet then shows only the broken-file text.

## 4. Renames

| Today | After |
|---|---|
| `$AW_SINDRI_HOME/profile/profile.yaml` | `$AW_SINDRI_HOME/settings/settings.yaml` |
| `GET /sindri/profile` | `GET /sindri/settings` (`text`, `problem`, `source`) |
| `status.profile { live, problem, source, modelPick, labelPick, surfacePick }` | `status.settings { problem, source, modelPick, labelPick, surfacePick }` |
| `profileHash` in responses, `ifHash` in writes | gone |
| events `profile.approved`, `profile.checked`, `profile.approvals.removed` | `profile.approved` is already emitted nowhere (only the catalog and projector know it). `profile.checked` is still written by `CheckProfileService` (`ProfileCheck.Decide`: "drifted = approved != live", always false now), so this spec deletes that writer (`CheckProfileService`, `ProfileRecheck`, `RecheckingProfileIndexWriter`, `ProfileWatch` and the `profile.checked` payload writer), not just no-ops it. `profile.approvals.removed` was added by #2357 (`EventTypes.cs:48`, non-public) and is no longer written after the first start. The ledger is append-only, so old rows stay; the event catalog and the projector keep all three types as legacy no-ops for `views rebuild` |
| `IOldApprovalsCleanup`, `ApprovalsDirectoryCleanup` (removes `<home>/approved` at serve start) | stays as it is, beside the migration (§5), because a home that has not started since #2357 still has the folder. `SindriSettings.ApprovalsDirectory` moves with the `SindriPaths` rename |
| (new) | `settings.changed { file, line, at, by, saveId?, sha256, changes[] }`, key `settings.changed:<file>:<line>` |
| `SND-PROFILE-001`, `002`, `003` (the only three `SindriErrors.cs` defines; §3.3) | `SND-SETTINGS-002` only. `001` ("no profile") is deleted: Sindri writes the defaults before any read (§5), so no caller sees a missing file, and `ISettings.Latest` never answers "none". The places that test for it change: `LiveApprovedProfile.Convert`, `CodeIndexSymbolSource.cs:126` and `Labels.TodoNoteCard.cs:164`. `003` is deleted with the hash guard. New: `020..026`, `409`, `410` (all free today). `SND-PROFILE-004..017` exist only in old docs and are not carried. `settings init` on an existing file answers the new `025`; today it answers `SND-PROFILE-002` with "a profile already exists" (`FileProfileInitializer.cs:18`) |
| `sindri profile init \| set \| validate` | `sindri settings init \| set \| validate \| cleanup`; `sindri profile *` exits 2 naming the new verb (§5). `sindri profile approve` too: it exits 0 with "removed" today (`Verbs.cs:143`) so old scripts keep going, and exit 2 is on purpose, because the approval has been gone since #2357 and a script that still calls it should read why. No tracked script runs it; the one call, `scripts/tests/sindri-prove.test.sh:154`, tests the prove wrapper and moves to a settings verb. `settings cleanup` is the person's verb (it deletes `profile/`): a launched session gets `SND-AUTH-002`, like `sandbox login`, and a test checks it |
| `AW_SINDRI_PROFILE` | `AW_SINDRI_SETTINGS`; a set `AW_SINDRI_PROFILE` is not read, and gives a warning naming the new variable (§6) |
| `IProfileSource`, `IApprovedProfile`, `LoadedProfile`, `SindriProfile`, `ProfileHash`, `Profile/` folders | `ISettingsSource`, `ISettings`, `LoadedSettings`, `SindriSettings`, gone, `Settings/` folders |
| the existing Infrastructure `SindriSettings(Home, ProfilePath)` (59 mentions in `src`, 71 in `tests`) | `SindriPaths(Home, SettingsPath)`, renamed first in its own commit so `SindriSettings` is free for the settings document |
| Setup step `profile` (`SetupStepId.Profile`, `SetupSteps.Profile` "Your profile is saved and live", `ProfilePresence`, `ProfileFact`, `ProfileSetupReader`, its `SetupDoctor` line) | removed: the list is repo, mcp, map, sources |
| NeedsYou: `NeedsYouKind.Profile` and its drift row (`DriftKey = "profile:drift"`) | removed. `ProfileRow` and `ProfileProjection` (the view `ViewNames.Profile`, table `view_profile`) are built from `profile.checked`, so they go too. `ProfileRow.ApprovedServices` feeds service-failure clearing (`NeedsYouClearing.FailureStands`, `NeedsYouProjection`, `HealthServiceRows`, `ApprovedServices.Of`): those read the services from the live settings instead (`ISettings`), and `GetProfileService` and `WholeMapNotReady` read the settings, not the view |
| `setup.consent.given` (event key `setup.consent.given:<profileHash>`, payload field `ProfileHash`) and the "approved before auto registration" wording | the MCP consent row keys on `setup.consent.given` alone (`McpEvents.ConsentGiven` already ignores the hash). The key becomes `setup.consent.given`: `ConsentOnHumanPress` records once, so one key is enough. The payload field stays readable on old rows and is left empty on new ones |
| other places that carry a `ProfileHash` | `RepoOnboarded.ApprovedHash`, `ContextPageWritten.Hash`, `ContextPinWritten.Hash`, `ProcessStarted.Profile`, `McpAutoMemory` (remembers the hash per repo and port), `StartProcessResponse` and the Linear connection responses: new rows and responses leave the field out, old rows keep it, and `McpAutoMemory` remembers the settings sha256 instead |
| `Approve` field on the Ui request bodies (`ReviewSettingsSave`, `LibraryRootAdd`, `LibraryAwSwitch`, and `RemoveLibraryRoot`'s `approve` argument) | the new write routes ignore an `Approve` field: it is accepted, has no effect and is not echoed, so a client from before the change keeps working. The UI PR that moves each of those senders stops sending it |
| `IfHash` request fields, `--if-hash`, `SND-PROFILE-003`, `SND-TODO-409` | all gone: `IfHash` on `SetPagesRequest`, `PinConceptsRequest`, `SetTodoNoteRequest` and `StopTodoNoteRequest`; `--if-hash` in `Usage.cs:116,124` and the context page and pin verbs; `IProfileIndexWriter.Write(edits, ProfileHash? ifHash)`; `TodoNoteProfileEdit.Set/Stop(ifHash)`; `LoadedProfile.Hash`, `ProfileHashing`, `ProfileHash`. `SND-TODO-409` stays for the todo's own version check but no longer guards the note setting |
| Health "Profile" row | "Settings" row: `ok` with the file, or `unreadable` with the problem; the three picks; a warning per repo whose checkout is gone |
| API goldens `Golden/Api/profile/*` | `Golden/Api/settings/*`, re-recorded; `Golden/Api/profile-approve/removed.json` (the 410 answer) stays while the tombstone does |

The UI's `ProfileWriteFlow`, `PagesSetBody` and `PinConceptsBody` and the todo note setup (`TodoNoteSetBody`,
`TodoNoteStopBody`, `TodoNoteCard`; the slices `SetTodoNote`, `StopTodoNote`, `CheckTodoNote`, `SyncTodoNote` and
`ImportTodoNote`; the routes `POST /sindri/todos/note/set` and `stop`; the MCP tool `SindriTools.Todos`) stop sending
`ifHash`.
Context and ui rename in the same PR, so the old names are never served beside the new ones and nothing needs a
shim.

## 5. Migration

One helper, `SettingsMigration`, with two entry points:
- `RunLocked` assumes the caller holds the settings lock. Step 1 of every write calls it.
- `Run` is for serve's start and the reads: `settings validate`, `GET /sindri/settings` and `Latest`, the call about
  100 places make to read the live settings. It first checks without the lock whether the migration is due or the file
  is missing, and returns at once when it isn't, so reads never queue behind writers. "Done" is kept in memory once
  seen, so a `profile/` folder left on disk doesn't make every read look due; the scan reads `changes.jsonl` from the
  top for the `migrated` line. When it is due, it waits for the lock like a writer: it polls every 50 ms for up to
  5 s, then calls `RunLocked`. The migration copies a few KB, so only a stuck lock holder runs the 5 s out. A read
  then never errors: it answers the last good settings when this process has them, else it parses
  `profile/profile.yaml` read-only, and it logs a warning (the Health "Settings" row says "settings busy"). `409`
  stays a write-only answer, because a busy lock is not a broken file and must not fail serve's start.

The migration carries every section of the file as it is, which includes the sections added since this spec was first
written (`workflow.http`, `workflow.poll`, `workflow.forEach`, `workflow.callDepth`, `todo.obsidian`,
`sandbox.limits`): it copies the text and parses none of it, so a new section needs no migration code. The approvals
folder cleanup (`ApprovalsDirectoryCleanup`) runs at serve start beside it and stays independent.

A `settings set` before serve ever starts still migrates. The migration is due when `profile/profile.yaml` exists
and `changes.jsonl` holds no `migrated` line. `RunLocked` then runs these steps:

1. Create `settings/` (0700) and `changes.jsonl` (0600) with its file-id line, unless they exist.
2. When `settings.yaml` doesn't exist, copy `profile/profile.yaml` to `.last-saved.yaml`, then to `settings.yaml`,
   each by temp file (0600) and rename. `settings.yaml` comes last. When it already exists (a crash after this step
   last time), copy nothing.
3. Append one line `by: migrated` carrying the sha256 of `profile/profile.yaml`. This line is the done signal.
4. Leave `profile/` in place.

Why this order:
- A crash before step 3 re-runs the migration, and step 2 never copies over a `settings.yaml` that exists.
- Once the `migrated` line is written, deleting `settings.yaml` by hand never brings the old profile back: the next
  run writes the defaults (below).
- A fresh home has no `profile/`, so it never gets a "Moved over" line.
- If `changes.jsonl` is deleted while `profile/` still exists, the next run writes a new file id and a `migrated`
  line, and copies nothing. That leaves one extra "Moved over" line, which is harmless.

**Defaults.** When `settings.yaml` is missing and the migration isn't due (a fresh home, or the file deleted after the
migration), `RunLocked` writes the default settings: the same text `settings init` writes, to `.last-saved.yaml`,
then to `settings.yaml`, each by temp file (0600) and rename, then one line `by: created` (Last changed: "Created
with the defaults at 14:02"). Serve's start runs it before it serves anything, and every read and write runs it
through `Run` or step 1, so no caller ever sees a home without settings. This is why there is no "no file" code.

A deleted file never loses the person's settings. When `settings.yaml` is missing but `.last-saved.yaml` exists (a
hand `rm` after a save, or a sync tool that moved the file), `RunLocked` first:
1. appends a `by: outside` line whose `changes[]` removes every path in `.last-saved.yaml`, so the ledger shows a
   deletion, not a fresh home;
2. renames `.last-saved.yaml` to `settings/.before-defaults-<ulid>.yaml` (0600);
3. then writes the defaults and the `created` line as above.

`sindri doctor`'s `settings` row and the Health "Settings" row then warn: "Your settings file was deleted, so Sindri
started from the defaults. Your last settings are in <path>: move it back to settings.yaml to use them." The warning
lasts until that backup file is gone. A fresh home has no `.last-saved.yaml`, so it gets no backup and no warning.

`settings init` stays as the CLI verb for scripts that want the file before anything else runs: the same step, with
the same `created` line. It refuses when `settings.yaml` exists, as `profile init` does today. A custom `AW_SINDRI_PROFILE`
path outside the home isn't migrated: set `AW_SINDRI_SETTINGS` to it, or copy it to `settings/settings.yaml`.

When both `profile/` and `settings/` exist, `settings/` wins. `sindri doctor`'s `settings` row and the Health
"Settings" row warn and name `sindri settings cleanup`, which removes `profile/` and nothing else. The warning
lasts until then. The text a session reads doesn't name the command, because a session can't run it.

**Downgrade.** An older build reads `profile/profile.yaml`, which holds the settings as they were at the migration.
Every change saved after that is invisible to it, with no warning, because the older build doesn't know
`settings/` exists. After `settings cleanup`, an older build finds no settings and starts as a fresh install.

**Old names.** There is no grace period and no follow-up to remove anything:
- `sindri profile <anything>` exits 2 with `sindri profile is now sindri settings: run sindri settings <same
  args>`. It is one line in the verb table and stays.
- A set `AW_SINDRI_PROFILE` is not read. Sindri prints one warning: "AW_SINDRI_PROFILE is no longer read; set
  AW_SINDRI_SETTINGS".

## 6. Docs and callers

The scope is everything the §7 grep finds, outside `docs/superpowers/` (dated specs and plans are history and
keep the old words). Known today, by group:

- **Docs.** `docs/csharp/sindri-profile.md` becomes `sindri-settings.md`. Every other doc the widened §7 grep finds
  (it also matches `profile approve`, `profile init`, `profile set`, `sindri-profile` and `SND-PROFILE`) changes too.
  Known today: `docs/csharp/sindri.md`,
  `sindri-ui.md`, `sindri-context.md` and `packets.md`; `docs/sindri/SETUP.md`, `README.md`, `OPERATIONS.md`,
  `deploy.md`, `context.md`, `errors.md`, `sources.md`, `linear.md`, `first-context-run.md`, `demo-handoff.md`,
  `connectai-live-check.md`, `handoff.md`, and `profile.md` (becomes `settings.md`). Also `docs/csharp/code-index.md`,
  `forge.md`, `sindri-launcher.md`, `sindri-sandbox.md` and `scaffolds/new-package.md`. #2357 already rewrote
  parts of these (`sindri-profile.md` says "`--if-hash` stays as a check"; `sindri.md:69,137` has the `profile approve`
  tombstone row and a "--if-hash only" sentence): those lines change again. `errors.md` also drops `AW_PROFILE_DIR` and
  `--profile`, which no code reads; `docs/onboarding.html`,
  `sindri-setup.html`, `ONBOARDING.md`, `.agents/rules/hooks.md`, and AGENTS.md's command lines.
- **The doc checker.** `scripts/check-profile-doc.mjs` becomes `check-settings-doc.mjs` and reads
  `sindri-settings.md`. Its test becomes `check-settings-doc.test.sh`. `scripts/ci/changed-areas.sh` names the new
  doc and checker, and AGENTS.md's test line changes with them.
- **The environment variable.** `AW_SINDRI_PROFILE` becomes `AW_SINDRI_SETTINGS` in `SettingsFromEnvironment.cs`,
  the child-environment allow list and `.env.example`. A set `AW_SINDRI_PROFILE` is not read and gives the warning
  in §5. Scripts that unset or read it change too:
  `context-first-run.sh`, `model-limit-check.sh`, `owner-check.sh`, `sindri-pod.sh`, `tests/ui-ux/lib.sh` and
  `config/lib/tests/sindri-cs-hook-surface.test.sh`.
- **Scripts and hooks** that call `sindri profile` or read `profile/profile.yaml`, each with its test:
  `config/hooks/sindri-nudge.sh`, `context-first-run.sh`, `cutover-clean-ts.sh`, `cutover-prove.sh`,
  `install-cs.sh`, `linear-probe.sh`, `model-limit-check.sh`, `owner-check.sh`, `probe-linear-projects.sh`,
  `sindri-pod.sh`, `sindri-prove.sh` and `sindri-ui-fixture.sh`. Their tests are in `scripts/tests/`, including
  `sindri-ui-fixture-prs.test.sh` and `sindri-ui-fixture-reviews.test.sh`. Also the dry-run golden
  `scripts/tests/fixtures/context-first-run.up-dry-run.txt` and `sindri-smoke-normalize.test.sh`.
  (`sindri-sandbox-e2e.sh` and `tests/fixtures/scoping/proof.sh` no longer exist.)
- **UI checks.** `tests/ui-checks/gh-1071-settings-skipped-width.sh`, `gh-1539-settings-leftovers.sh`,
  `gh-1701-proof.sh` and `pin-two-clicks.sh` move to `settings/settings.yaml`. The approval and drift checks
  (`ux-b4-approve-focus.sh`, `ux-b5-second-drift.sh`) and `gh-1069-settings-arrival.sh` were already deleted by #2357.
- **`install-cs.sh`'s `profile_hint`** (defined at lines 380-389, called at line 424) becomes `settings_hint`. It
  runs `sindri settings validate` and prints nothing on exit 0. On exit 1 it prints "your settings file has a
  mistake; run `sindri settings validate` to see it". Its text already says "edits apply on save, there is nothing to
  approve", so the "no approve advice" part is true today, and it never fails the install. It runs after install-cs's
  running-serve guard, because `settings validate` may run the migration.
- **More callers the first list missed.** `docs/sindri/sources.md:14,53` (says `$AW_SINDRI_HOME/profile.yaml` and
  "approve the profile"); `scripts/cutover-clean-ts.sh:50` and `scripts/cutover-prove.sh` (read `AW_PROFILE_DIR`;
  `scripts/tests/cutover-clean-ts.test.sh` and `scripts/tests/serve-build-guard.test.sh:66` set it); and
  `scripts/tests/ci-changed-areas.test.sh:56`, which names `docs/csharp/sindri-profile.md`. `AW_PROFILE_DIR` is read
  only by the cleanup of the old TypeScript install, so it stays there under that name and is not renamed; the
  closing grep leaves it out of its pattern list for that reason.
- **Words the person reads.** `ModelChoiceCopy` (`SettingsView.axaml:31`) and `ModelChoiceValidate`
  (`Labels.ModelChoice.cs:19`) copy the profile path plus `sindri profile validate`, which would exit 2. They are
  dropped, not renamed to `settings validate`: the Settings heading's Copy file path button is the one button, as §3.6
  says. `Labels.Setup.cs:139,142` say "Waits for your settings" for rows that wait on a
  step the list no longer shows; they say "Waits for a repository" like the row above them. The NeedsYou empty page
  is checked for "approval" wording once its row goes.
- **More words in the UI.** `SetupRepoBody` (`Labels.SetupForms.cs:21`, "Adding it changes your settings, so you
  approve them next.") is rewritten without "approve", like these others the phrase grep finds (checked at `origin/main`): `LibraryFoldersSave` "Add and approve" (`Labels.LibraryFolders.cs:52`), `LibraryFoldersFormNote` "asks for your approval" (`:49`), `LinearConnSaveHint` "approves just this change" (`Labels.LinearConnection.cs:58`), the "Pin all and approve" submit text (`PinSeveralViewModel.cs:112`), `SetupErr004` "approve your settings first" (`Labels.SetupForms.cs:42`) and `ApproveSettings` "Approve settings" (`Labels.Sends.cs:52`); `Labels.ModelChoice.cs:16`, `Labels.LabelChoice.cs:19` and `Labels.SurfaceChoice.cs:19`
  say "Change it in the profile" and "once you approve"; the hash-clash strings `PageSheetClash` (`Labels.PageSheet.cs:121`),
  `AddSeveralPagesClash` and `AcceptAllProfileFull` (`Labels.AddSeveralPages.cs:81,45`), `SetupProfileChanged`
  (`Labels.SetupForms.cs:89`, also keyed on `SND-PROFILE-002` at line 99) and `ReviewSettingsStale`
  (`Labels.ReviewSettings.cs:51`) talk about "the profile changed" and are reworded or dropped with the hash guard;
  `ProfileCard` (`Labels.cs:95`) and `CellProfile` (`Labels.cs:188`, used by `StatusText.cs:41`) say "Profile"; and
  `Labels.Actions.cs:46,47,147` ("Create profile", "Set a profile key", "Validate profile"). The closing grep gets
  `-e "the profile" -e "your profile"` and the settings-approval phrases (`-e "and approve" -e "approve your settings" -e "approve them" -e "your approval" -e "approves just" -e "Approve settings" -e "once you approve"`) for `src/Sindri/Aw.Sindri.Ui/ViewModels`. A bare "approve" would match about 80 lines that stay on purpose (the decision bar's Approve, "Approve on GitHub", "Approve all workflows").
- **Text in code.** Strings that still say "re-approval" or "approved" (for example `SandboxCopy.cs:45`, the
  `SND-LAUNCH-019` row) are reworded in the Core commit of the rename.
- **Not in scope.** `recipes/dev/profile.yaml` is a workflow recipe (retired by #2356), not settings.
  `tests/fixtures/backtest/pin-questions.md` and `evals/planDevexReview/...` use "profile" in their own sense and
  stay.

## 7. Proof

- Unit:
  - A save is live for serve's next read, and so is a same-length hand edit inside one mtime tick (the test writes both
    texts with the same length and mtime).
  - An invalid edit writes nothing.
  - A save writes one line with its `by`, `sha256`, `changes[]` and, from the app, its `saveId`.
  - A session's save is accepted: a launched session's `by` is `launch:<id>`, another session's is `session`, and
    the owner's is `person`.
  - Validation refuses each of: `/`, `$HOME`, a path inside the Sindri home, a bare `*` host, `*.com`, an IP host,
    an http source URL, a URL with a user name or a query, a `command:` reference whose first word isn't found, an
    `env:` reference with a bad name, and a free-text value matching a credential pattern. No refusal text holds
    the value.
  - A ulid in an id field and a sha256 in a hash field are not refused as secrets.
  - A `--from-file` with three pages is one save and one line, and keeps pages not in the file.
  - A `--from-file` whose line would pass 64 KB saves, and its line keeps the paths with `"trimmed": true`.
  - A write, a read and serve's start on a fresh home each write the defaults once, with one `created` line; two
    at once still write one.
  - Each refusal's CLI exit code, `--json` body and HTTP status match the §3.2 table.
  - A request saying `X-Sindri-Caller: human` without the token is 403 `SND-AUTH-001` and writes nothing.
  - A session's `settings set` on `context.sources.linear.writeToken` is refused with `SND-SETTINGS-026`; the person's is not.
  - `settings set` with serve running goes through `POST /sindri/settings/edit` and records the caller; with serve down a
    session gets `NotRunning` and writes nothing.
  - `settings init` on an existing file answers `SND-SETTINGS-025`.
  - A same-length hand edit inside one mtime tick is read by the next read (the cache keys on the sha256).
  - A save to one repo and a `settings set` of another key, started together from two processes, both land with no 410
    (the edit callback runs inside the lock).
  - A read that finds the migration due, while a writer holds the lock for under 5 s, waits and then succeeds.
  - A read that finds the migration due while the lock is held for more than 5 s answers the last good settings (or
    reads `profile/profile.yaml` when there are none), logs a warning and never returns `SND-SETTINGS-409`; serve's
    start does not fail.
  - `settings init` on a fresh home exits 0 with one `created` line; on an existing file it answers `SND-SETTINGS-025`.
  - A `profile/` folder left on disk doesn't make every read take the migration path.
  - An edit callback that outlasts 3 s answers `SND-SETTINGS-409` and writes nothing; a request cancelled after the
    rename still gets its audit line.
  - `settings cleanup` from a launched session is refused with `SND-AUTH-002`.
  - A test walks every credential-reference key in `SindriSettings` and fails when one is missing from the set that
    `SND-SETTINGS-026` guards.
  - An architecture test: no code a writer's callback reaches takes `SettingsFileLock` or `Run`'s wait path.
  - A CLI whose connection opens but gets no answer exits 1 and never writes in process.
  - A secret in a `command:` argument is refused; redaction runs before the 200-character cap.
  - Removing one repo doesn't change another repo's path in `changes[]`.
  - A broken file keeps the last good settings in use, and the next valid read clears the warning.
  - A write on a broken file is refused with `SND-SETTINGS-024` naming the line, and writes nothing.
  - A repo whose checkout is deleted after it was added gives a Health warning, and other saves still work.
- The audit copy:
  - A crash after the rename and before the append leaves the save in place; the next pass records it as `outside`
    with its changes, diffed against the base.
  - A crash after the append and before the base copy leaves a stale base, which the next write refreshes.
  - Copying twice, the second time after `views rebuild`, writes each event once.
  - An event's time is its line's `at`, also for a line copied after serve was down.
  - A last line without `\n` is not copied, then is copied once it completes.
  - A torn line padded by the next append is counted, skipped and warned once, and later lines keep their keys.
  - Deleting `changes.jsonl` and saving again starts a new file id, and its events land.
- Edits outside Sindri:
  - A hand edit seen by serve becomes one `by: outside` line, with `changes[]` against `.last-saved.yaml`, and the
    next outside edit's diff doesn't repeat it.
  - A hand revert to the text of an older save becomes an `outside` line.
  - A hand edit made while serve is down, followed by a CLI save, becomes two lines: `outside`, then the save.
  - Serve, waiting on the lock while a writer saves, appends nothing for the writer's own save.
  - A hand edit that adds a credential-shaped value writes `[redacted]` in `from` and `to`.
  - A hand edit that doesn't parse appends nothing until it parses.
- Two writers (real child processes, Lane=integration):
  - Saves to different items both land.
  - Saves to the same item: the later one wins.
  - An app page save and an agent's page add on different page ids both land.
  - A hand edit between a writer's read and rename fails the writer with `SND-SETTINGS-410` and is kept.
  - A writer kept off the lock for 5 s gets `SND-SETTINGS-409`, exit 2, with the `--json` shape.
- Migration:
  - `settings set` before serve ever started migrates, without waiting on its own lock.
  - Run twice: no second copy and no second `migrated` line.
  - A crash between the copy and the `migrated` line is finished by the next run, with one `migrated` line.
  - Deleting `settings.yaml` after the migration gives the defaults with a `created` line, never the old profile.
  - Deleting `settings.yaml` after a save gives, in order, an `outside` line removing every path, the backup
    `.before-defaults-<ulid>.yaml` holding the last saved text, and the `created` line; doctor and Health name the
    backup until it is removed.
  - A fresh `settings init` writes no `migrated` line.
  - With both folders, `settings/` wins and doctor warns.
  - The folder is 0700 and the files 0600.
- Integration: `GET /sindri/settings` and status in the API goldens; the page and pin writes without `ifHash`;
  `EndpointCqrsTests`; an architecture test that every settings write route is in `EndpointTable`'s `commands`
  group; a settings write with an `Origin` header is 403 `SND-API-008`; a settings write with a form content type
  is refused; the new route's catalog row, action id, API golden and Ui `GoldenContract` slug; and, in PR 2, `GET /sindri/ledger` with `newest=true`.
- UI (Frames tests), PR 1a: Setup without the profile step (its count states on a first run with no repo), NeedsYou
  without the profile row, and no "approve" wording; the read-only lines say "Change it in the profile file". That wording is intended: in 1a the file is still
  `profile/profile.yaml`, so "settings.yaml" would name a file that doesn't exist yet, and 1b swaps the words when it
  moves the file.
- UI (Frames tests), PR 1b: the read-only lines say "Change it in settings.yaml"; the Health "Settings" row with its broken-file,
  leftover and missing-checkout warnings; the loading and can't-reach states; Last changed reading "Created with the
  defaults" on a fresh home, and after the person's own save, with a day when not today; both busy messages keeping
  typed values; a 410 on an item the outside edit touched showing Reload with no Try again; a field refusal with
  focus on the field; a sheet open on a broken file showing only the broken-file text (no 410 text, no stale banner), and its save coming back
  once the file is fixed.
- UI (Frames tests), PR 2: See changes with its empty and error states; a 410 on an item the outside edit
  touched showing the banner instead of Reload; and the stale-sheet banner for a change, a removal, Keep mine leaving the other
  writer's field alone, a reconnect, and the sheet's own save showing nothing.
- aot-smoke: its `profile/profile.yaml` lines move to `settings/settings.yaml`, and expected.txt is re-pinned.
- The closing grep returns only the migration and tombstone code, their tests, and this spec:

  ```
  git grep -i -e "sindri profile" -e "profile/profile.yaml" -e "profile\.yaml" -e AW_SINDRI_PROFILE \
    -e "profile approve" -e "profile init" -e "profile set" -e "profile validate" -e "/sindri/profile" \
    -e sindri-profile -e SND-PROFILE -- \
    src tests docs scripts config providers skills bootstrap evals setup.sh .env.example \
    ONBOARDING.md AGENTS.md .agents ':!docs/superpowers' ':!evals/planDevexReview' ':!tests/fixtures/backtest'
  git grep -i -e "the profile" -e "your profile" -e "and approve" -e "approve your settings" -e "approve them" -e "your approval" -e "approves just" -e "Approve settings" -e "once you approve" \
    -- src/Sindri/Aw.Sindri.Ui/ViewModels
  ```

## 8. Size

Counted at the rebase, as whole-word `profile` matches: about 4.5k lines in 957
`src/Sindri` files, 2.3k in 698 files under `tests`, and 0.6k in 59 files under `docs` (outside `docs/superpowers`).
That is about 25% less in `src`, 50% less in tests and 80% less in docs than the first count (6k, 4.5k, 3k), because
#2357 and the recipe retirement deleted code and docs.
- Almost all of it is a scripted rename, one commit per area (Core, Features, Infrastructure, Host, Ui, tests,
  docs), each building on its own.
- The removals shrink the code: the hash guard (`IfHash` and `--if-hash` are spread over about 30 files in `src`, so
  the old "about 494 lines" count is too low and is not repeated here), `ProfileHash`, the `profile.checked` writer
  with `ProfileRow` and `ProfileProjection`, the profile Setup step, the NeedsYou profile row, and the UI checks for
  approval and drift that #2357 already deleted.
- The new code is the file lock, the audit line with its edits-outside-Sindri check, the `settings.changed` event,
  the migration with the defaults, `settings cleanup`, and the UI's Last changed line, See changes, and the
  stale-sheet banner.
- It ships as three PRs, to keep each one reviewable:
  1. **1a, the leftovers of #2357, still on `profile.yaml`.** The `IfHash` fields and `--if-hash`, `ProfileHash` and
     its carriers, `SND-PROFILE-003`, the `profile.checked` writer with `ProfileWatch`, `ProfileRow` and
     `ProfileProjection`, the profile Setup step and NeedsYou row, the stale words in the UI (the `sindri profile
     validate` copy, "Waits for your settings", "Change it in the profile") and `ApprovedServices` read from the live
     profile. Its read-only lines say "Change it in the profile file" on purpose (the file is still `profile.yaml` in 1a), with no approve wording; 1b swaps that to
     `settings.yaml`. The per-writer "file is still the text I read" check stays in 1a, so writes keep a guard until 1b
     replaces it with the lock.
  2. **1b, the move.** The renames, the lock with the in-lock edit callbacks, the hash-keyed cache, the write route,
     the audit line with its outside-Sindri check (without it the ledger misses edits), `settings.changed`, the
     migration and defaults, `settings cleanup`, and in the UI Last changed, the file path button, the busy messages with
     **Try again**, field refusals and the broken-file state.
  3. **2, the two UI pieces.** See changes (with `newest=true` on `GET /sindri/ledger`) and the stale-sheet banner, with
     their Frames tests. See changes shows the last 50 changes only; a filter by who or by path is a later change.
- Every commit that adds or removes a public type updates its package's `PublicAPI.Unshipped.txt` in the same commit.
- Land 1a first and branch 1b after it merges: both touch about 30 of the same files, and the order avoids a rebase
  of the rename over the removals. 1b deletes `SND-PROFILE-001` (§3.3), adds the route's catalog row, action id, API
  golden and `GoldenContract` slug, and PR 2 deletes the `profile approve` tombstone (route, verb line and golden).
- The PR body of 1b gives a reading order: the new code first (lock, audit line, migration, UI), then the per-area
  rename commits.

## 9. Questions

None open. The heads-up question is settled in §10.

## 10. Settled

- Settings versions: none (owner).
- Workflow versions: the existing `workflow_revision` rows and `active_revision` (owner, §2).
- Who may change settings: any session, recorded in the ledger (owner).
- Recipes: retired by #2356.
- The optimistic check: none. A file lock orders writes, and the same key's later save wins (§3.2).
- A change that widens agents' reach (a sandbox host, a `workflow.http.hosts` host, a repo, a connection or library source URL, a credential
  reference) gets no Needs-you row: it shows only in Last changed, See changes and the ledger (owner, 2026-11-08).
  Validation still refuses `*`, `*.com`, IP hosts and `localhost` (and, for `workflow.http.hosts`, any wildcard); `/`, `$HOME` and the Sindri home as repos;
  non-https or credential-bearing URLs; secrets in `command:` arguments; and `command:` references Sindri can't find.

## Review rounds

Four autoplan rounds ran on this spec, each with five lenses (product, architecture, design, devex, security). The
full findings are in `plans/sindri-settings-spec/round1` to `round4`, each with its own `consolidated-review.md`.

| Round | Reviewed | Verdicts | HIGH findings and how they were fixed |
|---|---|---|---|
| 1 | the 2026-11-08 draft before `a20c1272b7` | product pass; arch, design, devex and security needed work | audit key collided when `changes.jsonl` shrank (file id in the key); a copier could spend a torn line's key (copy complete lines only, schema and 64 KB cap); `settings set` before serve skipped the migration (`Run` under the lock, resumable); "validate" was undefined (rules `020..023`); about 13 scripts and a hook still called `sindri profile` (caller list, widened grep); `AW_SINDRI_PROFILE` not renamed; the downgrade claim was wrong; "Last changed" lagged the person's own save (response carries `by` and `at`); a stale open sheet got no signal (banner) |
| 2 | `a20c1272b7` | product ship, security pass; arch, design, devex needed work | a hand edit had no diff base (`.last-saved.yaml`); a broken file left every save failing with no state (`024`, Health text, disabled save); caller list incomplete; the closing grep couldn't come back clean; `install-cs.sh` hint (`settings_hint`) |
| 3 | `e38e2dc8e1` | product ship, security pass, devex pass; arch and design needed work | a hand revert to an older text was missed (compare the last parseable line only); a crash between the base copy and the append gave an empty `outside` line (append first, base second); the migration re-locked inside the write lock (`RunLocked` and `Run`); Keep mine reverted fields the person never edited (send edited fields only) |
| 4 | `5221f47032`, after the rebase on main | product ship, security pass; arch, design and devex needed work | **R4-1** the edit planned before the lock turned an unrelated save into a 410 (the edit is a callback inside the lock, no Plan/Save split, §3.2); **R4-2** `settings set` had no route and no way to read `by` (one `POST /sindri/settings/edit`, `Job` class, §3.2); the MEDIUM rows: the hash-keyed cache (§3.1), the lock beside the file (§3.1), one async routine in place of `_gate` (§3.2), the real code mappings (§3.2), reads wait 5 s for a due migration (§5), no `001` and a code for the init refusal (§4), the missed callers and UI words (§6), `profile approve` exit 2 on purpose (§4), connection-token keys refused to sessions (§3.4), the 403 for a human without a token (§3.4), the sheet table and the file path button (§3.6), and the 1a/1b/2 split (§8) |

Round 4 found nothing that contradicts a settled item in §10.

**Round 5** (reviewed `3c1ef388f8`, the round-4 fixes). Product, architecture, design and security pass; devex needed
work on four medium findings. There is no HIGH. What changed, by group:
- Migration and reads (R5-1 to R5-4, R5-17 to R5-21): a read never errors when the 5 s wait runs out; `settings init`
  reads what `RunLocked` returned; "migration done" is kept in memory; the callback has a 3 s deadline and steps 4 to
  6 ignore cancellation; the audit tail is a 64 KB walk back; one parse per hash; the override-folder check; a
  rule that the lock is never taken from a callback; what "serve is down" means for the CLI.
- Text and tests (R5-5 to R5-6, R5-8, R5-14 to R5-16): exit 2 for 409, the `SND-AUTH-002` wording, the closing grep's
  exclusions, 1a lands before 1b, who deletes `SND-PROFILE-001`, and the route's catalog row and golden.
- Remove, cleanup and credentials (R5-7, R5-22): `op: "remove"` is dropped, `026` covers any edit that touches a
  credential key, `settings cleanup` is human-only, and serve-down `person` is said to be best effort.
- UI words and layout (R5-9 to R5-11, R5-23): the missed UI strings, one always-shown Copy file path button, PR names
  1a/1b/2 with a 1a Frames line, and the kinds and bulk-sheet wording.
- Scope (R5-12, R5-13): no MCP settings tools in v1, and the tombstone goes in PR 2.

Three decisions: a read that outwaits the migration answers the last good settings; `op: "remove"` is dropped and
`settings cleanup` is human-only; settings are CLI and route only in v1, and the `profile approve` tombstone is
deleted in PR 2.
