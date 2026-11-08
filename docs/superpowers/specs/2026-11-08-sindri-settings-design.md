# Sindri v2: settings, no profile (#2373)

Status: draft, for the owner's read. Part of #2258. Stacked after #2357 (which removes the approval step) and
#2356 (which retires recipes).

## 1. The decision

Owner, 2026-11-08, in three parts:

1. Sindri v2 has no profile concept and no approval.
2. Settings have no versions: a save applies at once.
3. Workflows keep their own versions as DB rows with an active flag. Any session, agents included, may change
   settings, and the ledger records who changed what.

What #2357 leaves behind:
- the noun "profile"
- a content hash (`profileHash`, `ifHash`) that every write sends as a guard
- the `SND-AUTH-002` refusal on settings writes from a session

This spec removes all three. It adds no new database table. The new files are `settings/changes.jsonl` (the
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
- **The stamp.** "The file changed" always means its sha256 changed. Serve's cache keys on inode, mtime in
  nanoseconds and length to decide when to re-read, and every save is a rename, so a save always gets a new inode.
  A re-read then compares sha256, so a same-length edit inside one mtime tick is still seen.

### 3.2 A write

Every write takes an exclusive lock on `settings/.lock`. Each write opens its own handle (in-process writers never
share one), and tries the lock without blocking every 50 ms for up to 5 s, so a waiting HTTP request never holds a
thread asleep. Linux and macOS use `flock`; Windows opens `.lock` with `FileShare.None`. In order, while it holds
the lock:

1. Run the migration helper (§5). It does nothing once `settings/settings.yaml` exists.
2. Read `settings.yaml` and take its sha256. When that sha256 matches no audit line, someone edited the file
   outside Sindri since the last save: append that edit's line first (§3.4), then go on.
3. Apply the edit and validate the result (§3.3). When the result is invalid or changes nothing, stop: nothing is
   written. When the audit line this save would write is over 64 KB (a huge `--from-file`), stop with
   `SND-SETTINGS-025` ("this save changes too much for one audit line; split the file into smaller saves").
4. Write the result to a temp file and fsync it. Re-read `settings.yaml`: when its sha256 isn't the one from step 2,
   the file changed outside Sindri in between, so stop with `SND-SETTINGS-410` and write nothing. Then rename the
   temp file over `settings.yaml`. This is the save.
5. Copy the saved text to `.last-saved.yaml` (temp file and rename).
6. Append one line to `settings/changes.jsonl` (§3.4).

A crash after step 4 and before step 6 leaves a save with no line. The next writer or serve's watcher sees a
sha256 that matches no line and records it as edited outside Sindri, so the change is still in the ledger, under
the wrong name. Why a file lock: `sindri settings set` runs without the control plane, so a guard inside serve
wouldn't cover it.

**Busy lock.** After 1 s of waiting, the CLI prints `waiting for another settings save...` on stderr. When the 5 s
run out, the CLI exits 1 with "Another change is being saved. Try again in a moment.", and `--json` gives
`{ "code": "SND-SETTINGS-409", "message": ... }`. `SND-SETTINGS-410` exits 1 with "The settings file changed on
disk while you were saving. Nothing was saved; try again." The HTTP routes answer both with 409 and the same body.

**Every write is item-level.** A write sets or removes items in the current text and never replaces a whole list:
- one scalar key (`settings set <key> <value>`)
- one repo, by name
- one page, by id
- one pin, by id
- one sandbox host
- one connection or library source, by name

`context page set --from-file` and `context pin set --from-file` are one write each: one lock hold, one save and
one audit line, setting every item in the file. They never remove an item the file leaves out; removing is
`remove <id>` only, and the CLI's summary line says so (`set 3 pages; pages not in the file are kept`).

Two saves only collide on the same item, and then the later one wins. That's the right answer for one person's
app. Without the rule, a stale app form that replaces the whole pages list would drop a page an agent added in
between. The hash check existed for approval (the approved text must not shift under you), and approval is gone.

**The write routes.** `SND-AUTH-002` goes, and with it the per-start human token on settings writes. What still
guards the routes is what guards every command route today: serve listens on loopback only, and
`CommandOriginRefusal` answers any request with an `Origin` header with 403 `SND-API-008`. The settings, repo,
page, pin, sandbox and source write routes all carry `CommandRouteMetadata`, so a browser tab's form post is
refused. They also require `Content-Type: application/json`, so a form post without an `Origin` header fails
binding. Reads stay as they are.

### 3.3 What validation checks

With approval gone, validation is the only check before a write goes live. Every refusal names the key path and
the rule. It never quotes the value, so a pasted secret never reaches the API, the logs or the ledger.

| Rule | Runs on | Code |
|---|---|---|
| Schema: known keys, types and ranges, as `IProfileSource.Load` checks today | every read and every write | `SND-SETTINGS-001..017` (today's numbers) |
| Repo add: the path exists and is a git checkout; not `/`, not `$HOME`, not a parent of `$AW_SINDRI_HOME`, not inside it | the repo add only | `SND-SETTINGS-020` |
| Repo path shape: not `/`, `$HOME`, a parent of or inside `$AW_SINDRI_HOME` | every read and every write | `SND-SETTINGS-020` |
| Sandbox host: a host name, or `*.` followed by at least two labels (`*.example.com`, never `*.com`); no bare `*`, IP literal or `localhost` | every read and every write | `SND-SETTINGS-021` |
| Source URLs (connections, library vaults): `https`, no user name or password, no query string; loopback only when the matching `AW_*_URL` test override names it | every read and every write | `SND-SETTINGS-022` |
| Credential references: `env:NAME` with `NAME` matching `[A-Z_][A-Z0-9_]*`; `command:argv` whose first word is an absolute path or a bare name found on `PATH` at save time (it runs without a shell, so no shell syntax applies) | every read and every write | `SND-SETTINGS-022` |
| Secrets: a free-text value that matches an `Aw.Redaction` pattern | every write | `SND-SETTINGS-023` |
| The file itself is broken (doesn't parse or fails a rule above) | every write | `SND-SETTINGS-024` |

- **Which fields the secret check scans.** Only free-text strings: titles, summaries, notes, aliases, labels and
  feature names. Id, hash, ulid, path, URL and credential-reference fields are never scanned, so a long id or a
  sha256 never trips it. When a free-text value is refused wrongly, the message names the escape: "This looks like
  a password or token. Put it in an environment variable and enter `env:NAME`, or reword it."
- **A missing checkout is not a broken file.** The "exists and is a git checkout" check runs only when a repo is
  added. A repo whose folder is moved or deleted later shows a Health warning on that repo ("`checkout`: the folder
  /home/me/src/checkout is gone"), and the rest of the settings keep working.
- **An edit on a broken file.** When the file already fails a rule (a mistake made outside Sindri), every write is
  refused with `SND-SETTINGS-024`: "Your settings file has a mistake on line 14 (`sandbox.hosts[3]`: not a host
  name). Fix that line first; nothing was saved." It names the line, never blames the edit being saved, and never
  quotes the value. All writes stop until the file validates again.
- **Repo remove** drops the settings entry only. It never touches the checkout or its worktrees.

### 3.4 Who changed what

- **Who may write:** any session, agents included (agent-first D2). `settings set`, `settings init`, the page, pin,
  repo, sandbox and source writes all accept sessions.
- **`by`** comes from the caller rules serve and the CLI already share (`CallerClass`, `X-Sindri-Launch`):
  - the owner (the app, or the CLI at a terminal with no agent signal) is `person`;
  - a session Sindri launched is `launch:<id>`;
  - any other session is `session`, with its repo when it sends one.

  Edits found outside Sindri are `outside`, and the migration's line is `migrated`. The app also sends a save id
  (a ulid) that the line carries as `saveId`, so a sheet can tell its own save from any other.
- **The line.** Each line in `changes.jsonl` is `{ at, by, saveId?, sha256, changes[] }`.
  - `sha256` is the hash of the file text the line's save produced.
  - `changes[]` holds `{ path, from, to }`. Paths name items by key, not position (`repos[checkout].path`,
    `index.pages[review]`), so removing a repo never shifts another's path. `from` and `to` are capped at 200
    characters, and both pass through `Aw.Redaction` before they're written: a match is written as `[redacted]`.
    That covers edits outside Sindri, which never went through the secret check.
- **Edits outside Sindri.** A hand edit, an agent's Edit-tool save or a crashed save all look the same: the file's
  sha256 matches no line. Two places catch it:
  - every writer, at step 2 of a write;
  - serve's watcher, for an edit that no save follows. When serve sees the file change, it takes the lock, re-reads
    the sha256, and skips when a line already carries it (a writer's own save, or a writer that already caught it).

  Either way the line is `by: outside`, and its `changes[]` is the diff between `.last-saved.yaml` and the current
  text, with `from` and `to` redacted as above. When the current text doesn't parse, nothing is appended: there are
  no paths to name, and Health already shows the mistake. The line is written once the file parses again. When
  `.last-saved.yaml` is missing (a home from before this change), the line holds `changes: []` and the next save
  writes the base.
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

- **Loading and no file.** While Settings loads, its rows show their labels with empty values and no buttons. With
  no settings file at all (a fresh home before `settings init`), Settings shows "No settings yet" and **Set up**,
  which runs the same step as `settings init`.
- **Last changed.** One line under the Settings heading, read from the last audit line's `by` and `at`:
  - "Last changed by you at 14:02"
  - "Last changed by <session title> at 14:02", with the title the Agents page shows; when that session is gone
    from the ledger, "an agent session (01HX…7Q)"
  - "Changed outside Sindri at 14:02"
  - "Moved over from your old settings at 14:02"

  The time gets a day when it isn't today: "yesterday at 14:02", "Nov 6 at 14:02". A save's own response carries
  `{ by, at }`, so the line updates the moment the person saves. The `settings.changed` event refreshes it for
  everyone else. **See changes** next to it opens the last 50 settings changes, newest first, one row per change
  (`by`, `at`, and each path with its from and to), read from `GET /sindri/ledger?type=settings.changed`. The CLI
  equivalent is `sindri ledger --type settings.changed`.
- **Saving.** While a save waits for the lock, its button shows `Saving...`. A failed save keeps the typed values,
  and focus stays on the save button.
  - `SND-SETTINGS-409`: "Another change is being saved. Try again in a moment." with **Try again**, which sends the
    same save.
  - `SND-SETTINGS-410`: "The settings file changed on disk while you were saving. Nothing was saved." with
    **Try again**.

  The codes show only in the CLI's `--json` output and the logs.
- **Refusals on a field.** A rule refusal shows under the field it names, in plain words, and the field keeps the
  typed value:
  - 020: "That folder isn't a git checkout." / "Sindri can't use your home folder or its own folder as a repo."
  - 021: "Use a host name like api.example.com, or *.example.com for every host under it."
  - 022: "Use an https address with no user name, password or query." / "Use env:NAME, or a command Sindri can
    find on your PATH."
  - 023: the secret message in §3.3.
  - 025: shown above the save button, as there is no single field.
- **A broken file.** Every settings sheet shows the Health text from §3.5 at its top, and its save button is
  disabled with "Fix the settings file first". **Copy file path** copies the path of `settings.yaml`. The Health
  row states its warning in its words, not only its color.
- **A stale open sheet.** Every settings sheet (repo, sandbox host, source, scalar keys, page, pin) watches
  `settings.changed`. When an event touches the item the sheet has open, and its `saveId` isn't the sheet's own,
  the sheet shows a banner:
  - "<who> changed this page while you had it open" with **Load theirs** and **Keep mine**. <who> uses the same
    words as Last changed ("you, from the CLI", "<session title>", "someone outside Sindri").
  - "<who> removed this page while you had it open" with **Keep mine** (saves it back) and **Close**.
  - A second change while the banner shows updates it to the newest <who>, with "and 1 more change".
  - **Keep mine** saves the person's values over theirs. **Load theirs** fills the sheet with the saved values and
    shows **Undo** until the sheet closes, which brings back the values the person had typed.
  - A change to another item shows nothing.
- **One voice.** The busy message, the refusal and the stale banner share one polite live region on the sheet. A
  newer message replaces an older one, so two never announce at once.

## 4. Renames

| Today | After |
|---|---|
| `$AW_SINDRI_HOME/profile/profile.yaml` | `$AW_SINDRI_HOME/settings/settings.yaml` |
| `GET /sindri/profile` | `GET /sindri/settings` (`text`, `problem`, `source`) |
| `status.profile { live, problem, source, modelPick, labelPick, surfacePick }` | `status.settings { problem, source, modelPick, labelPick, surfacePick }` |
| `profileHash` in responses, `ifHash` in writes | gone |
| events `profile.approved`, `profile.checked` | no new ones are written. The ledger is append-only, so old rows stay; the event catalog and the projector keep both types as legacy no-ops for `views rebuild` |
| (new) | `settings.changed { file, line, at, by, saveId?, sha256, changes[] }`, key `settings.changed:<file>:<line>` |
| `SND-PROFILE-001..017` | `SND-SETTINGS-001..017`, same numbers; new `020..025`, `409`, `410` (all free today) |
| `sindri profile init \| set \| validate` | `sindri settings init \| set \| validate \| cleanup`; `sindri profile *` prints the new name during the grace period (§5) |
| `AW_SINDRI_PROFILE` | `AW_SINDRI_SETTINGS`; the old name read with a warning during the grace period (§6) |
| `IProfileSource`, `IApprovedProfile`, `LoadedProfile`, `SindriProfile`, `ProfileHash`, `Profile/` folders | `ISettingsSource`, `ISettings`, `LoadedSettings`, `SindriSettings`, gone, `Settings/` folders |
| the existing Infrastructure `SindriSettings(Home, ProfilePath)` (142 uses) | `SindriPaths(Home, SettingsPath)`, renamed first in its own commit so `SindriSettings` is free for the settings document |
| Setup step `profile` | removed: the list is repo, mcp, map, sources |
| NeedsYou: the profile drift row and the "approved before auto registration" row | removed; the MCP consent row keys on `setup.consent.given` alone |
| Health "Profile" row | "Settings" row: `ok` with the file, or `unreadable` with the problem; the three picks; a warning per repo whose checkout is gone |
| API goldens `Golden/Api/profile/*` | `Golden/Api/settings/*`, re-recorded; `Golden/Api/profile-approve/*` deleted with the route |

The UI's `ProfileWriteFlow`, `PagesSetBody` and `PinConceptsBody` and the todo note-setup stop sending `ifHash`.
Context and ui rename in the same PR, so the old names are never served beside the new ones and nothing needs a
shim.

## 5. Migration

One helper, `SettingsMigration.Run`. Serve's start and every settings verb (step 1 of a write, and the reads
`settings validate` and `GET /sindri/settings`) call it before anything else, so a `settings set` before serve
ever starts still migrates. It first checks, without the lock, whether `profile/profile.yaml` exists and
`settings/settings.yaml` doesn't. When not, it returns at once, so reads never queue behind writers. When so, it
takes the lock, checks again, and runs these steps:

1. Create `settings/` (0700) and `changes.jsonl` (0600) with its file-id line, unless it exists. Append one line
   `by: migrated` carrying the sha256 of `profile/profile.yaml`, unless the last line already is that line.
2. Copy `profile/profile.yaml` to `settings/settings.yaml` and to `.last-saved.yaml`, by temp file (0600) and
   rename.
3. Leave `profile/` in place during the grace period.

The audit line comes first, so a crash between the steps leaves a harmless line and the next run copies the file.
The rule keys on `profile/profile.yaml` existing, so a fresh `settings init` never gets a "Moved over" line.
`settings init` refuses when `settings.yaml` exists, as `profile init` does today.

When both `profile/` and `settings/` exist, `settings/` wins, and `sindri doctor`'s `settings` row warns and names
`sindri settings cleanup`, which removes `profile/` and nothing else. The Health "Settings" row shows the same
warning, so a person who only uses the app sees it too.

**Downgrade.** An older build reads `profile/profile.yaml`, which holds the settings as they were at the migration.
Every change saved after that is invisible to it, with no warning, because the older build doesn't know
`settings/` exists. After `settings cleanup`, an older build finds no settings and starts as a fresh install.

**Old verbs.** During the grace period, `sindri profile <anything>` exits 2 with `sindri profile is now sindri
settings: run sindri settings <same args>`. An old script then fails with that line, not a usage dump.

**The grace period.** The toolkit has no releases, so the grace period is 30 days from this PR's merge. The PR
opens one issue, "Remove the profile leftovers", due 30 days after merge and assigned to the api lane. It covers:
the `sindri profile` tombstone, the `AW_SINDRI_PROFILE` warning, and the `profile/` leftover warning in doctor and
Health. `settings cleanup` stays.

## 6. Docs and callers

The scope is everything the §7 grep finds, outside `docs/superpowers/` (dated specs and plans are history and
keep the old words). Known today, by group:

- **Docs.** `docs/csharp/sindri-profile.md` becomes `sindri-settings.md`. Also: `docs/csharp/sindri.md`,
  `sindri-ui.md`, `sindri-context.md` and `packets.md`; `docs/sindri/SETUP.md`, `README.md`, `OPERATIONS.md`,
  `deploy.md`, `context.md`, `errors.md`, `sources.md`, `linear.md`, `first-context-run.md`, `demo-handoff.md`,
  `connectai-live-check.md`, and `profile.md` (becomes `settings.md`); `docs/onboarding.html`,
  `sindri-setup.html`, `ONBOARDING.md`, `.agents/rules/hooks.md`, and AGENTS.md's command lines.
- **The doc checker.** `scripts/check-profile-doc.mjs` becomes `check-settings-doc.mjs` and reads
  `sindri-settings.md`. Its test becomes `check-settings-doc.test.sh`. `scripts/ci/changed-areas.sh` names the new
  doc and checker, and AGENTS.md's test line changes with them.
- **The environment variable.** `AW_SINDRI_PROFILE` becomes `AW_SINDRI_SETTINGS` in `SettingsFromEnvironment.cs`,
  the child-environment allow list and `.env.example`. During the grace period, a set `AW_SINDRI_PROFILE` is
  still read, with a one-line warning naming the new variable. Scripts that unset or read it change too:
  `context-first-run.sh`, `model-limit-check.sh`, `owner-check.sh`, `sindri-pod.sh`, `tests/ui-ux/lib.sh` and
  `config/lib/tests/sindri-cs-hook-surface.test.sh`.
- **Scripts and hooks** that call `sindri profile` or read `profile/profile.yaml`, each with its test:
  `config/hooks/sindri-nudge.sh`, `context-first-run.sh`, `cutover-clean-ts.sh`, `cutover-prove.sh`,
  `install-cs.sh`, `linear-probe.sh`, `model-limit-check.sh`, `owner-check.sh`, `probe-linear-projects.sh`,
  `sindri-pod.sh`, `sindri-sandbox-e2e.sh`, `sindri-ui-fixture.sh` and `tests/fixtures/scoping/proof.sh`. Also the
  dry-run golden `scripts/tests/fixtures/context-first-run.up-dry-run.txt` and `sindri-smoke-normalize.test.sh`.
- **UI checks.** `tests/ui-checks/gh-1069-settings-arrival.sh`, `gh-1071-settings-skipped-width.sh`,
  `gh-1539-settings-leftovers.sh`, `gh-1701-proof.sh` and `pin-two-clicks.sh` move to `settings/settings.yaml`.
  `ux-b4-approve-focus.sh` and `ux-b5-second-drift.sh` test approval and drift, which are gone, so they are
  deleted.
- **`install-cs.sh`'s `profile_hint`** (lines 414-422) becomes `settings_hint`. It runs `sindri settings validate`
  and prints nothing on exit 0. On exit 1 it prints "your settings file has a mistake; run `sindri settings
  validate` to see it". It gives no approve advice, and it never fails the install.
- **Not in scope.** `recipes/dev/profile.yaml` is a workflow recipe (retired by #2356), not settings.
  `tests/fixtures/backtest/pin-questions.md` and `evals/planDevexReview/...` use "profile" in their own sense and
  stay.

## 7. Proof

- Unit:
  - A save is live for serve's next read, including a same-length edit inside one mtime tick.
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
  - A `--from-file` whose line would pass 64 KB is refused with `SND-SETTINGS-025` and writes nothing.
  - Removing one repo doesn't change another repo's path in `changes[]`.
  - A broken file keeps the last good settings in use, and the next valid read clears the warning.
  - A write on a broken file is refused with `SND-SETTINGS-024` naming the line, and writes nothing.
  - A repo whose checkout is deleted after it was added gives a Health warning, and other saves still work.
- The audit copy:
  - A crash after the rename and before the append leaves the save in place; the next pass records it as `outside`.
  - Copying twice, the second time after `views rebuild`, writes each event once.
  - An event's time is its line's `at`, also for a line copied after serve was down.
  - A last line without `\n` is not copied, then is copied once it completes.
  - A torn line padded by the next append is counted, skipped and warned once, and later lines keep their keys.
  - Deleting `changes.jsonl` and saving again starts a new file id, and its events land.
- Edits outside Sindri:
  - A hand edit seen by serve becomes one `by: outside` line, with `changes[]` against `.last-saved.yaml`.
  - A hand edit made while serve is down, followed by a CLI save, becomes two lines: `outside`, then the save.
  - Serve, waiting on the lock while a writer saves, appends nothing for the writer's own save.
  - A hand edit that adds a credential-shaped value writes `[redacted]` in `from` and `to`.
  - A hand edit that doesn't parse appends nothing until it parses.
- Two writers (real child processes, Lane=integration):
  - Saves to different items both land.
  - Saves to the same item: the later one wins.
  - An app page save and an agent's page add on different page ids both land.
  - A hand edit between a writer's read and rename fails the writer with `SND-SETTINGS-410` and is kept.
  - A writer kept off the lock for 5 s gets `SND-SETTINGS-409`, exit 1, with the `--json` shape.
- Migration:
  - `settings set` before serve ever started migrates.
  - Run twice: no second copy and no second `migrated` line.
  - A crash between the audit line and the copy is finished by the next run, with one `migrated` line.
  - A fresh `settings init` writes no `migrated` line.
  - With both folders, `settings/` wins and doctor warns.
  - The folder is 0700 and the files 0600.
- Integration: `GET /sindri/settings` and status in the API goldens; the page and pin writes without `ifHash`;
  `EndpointCqrsTests`; a settings write with an `Origin` header is 403 `SND-API-008`; a settings write with a form
  content type is refused.
- UI (Frames tests): Setup without the profile step; the Health "Settings" row with its broken-file, leftover and
  missing-checkout warnings; the loading and no-file states; the "Last changed" line after the person's own save,
  with a day when not today; See changes; both busy messages keeping typed values; a field refusal; a sheet on a
  broken file; and the stale-sheet banner for a change, a removal, a second change, Load theirs then Undo, and the
  sheet's own save showing nothing.
- aot-smoke: its `profile/profile.yaml` lines move to `settings/settings.yaml`, and expected.txt is re-pinned.
- The closing grep returns only the migration and tombstone code, their tests, and this spec:

  ```
  git grep -i -e "sindri profile" -e "profile/profile.yaml" -e AW_SINDRI_PROFILE -- \
    src tests docs scripts config providers skills bootstrap evals setup.sh .env.example \
    ONBOARDING.md AGENTS.md .agents ':!docs/superpowers'
  ```

## 8. Size

About 6k lines in about 1000 `src/Sindri` files say profile, plus 4.5k in tests and 3k in docs.
- Almost all of it is a scripted rename, one commit per area (Core, Features, Infrastructure, Host, Ui, tests,
  docs), each building on its own.
- The removals shrink the code: the hash guard (about 494 lines across src, tests and docs), `ProfileHash`, the
  profile Setup step, two NeedsYou rows, and two UI checks.
- The new code is the file lock, the audit line with its edits-outside-Sindri check, the `settings.changed` event,
  the migration, `settings cleanup`, and the UI's Last changed line, See changes, and the stale-sheet banner.
- If the PR runs long, the stale-sheet banner is the one piece that can move to a follow-up PR. The
  outside-Sindri check stays, because without it the ledger misses edits.

## 9. Questions

1. **Should a session that widens agents' reach give you a heads-up?** Reviews flagged four kinds of change a
   session can now save, its own runs included:
   - a sandbox host (network access);
   - a repo (file access);
   - a connection or library source URL (what Sindri fetches);
   - a credential reference: `command:argv` runs a command as you, outside the sandbox, and `env:NAME` reads a
     variable from serve's environment.

   This isn't about re-adding approval. It's about whether you want to hear about it.
   - My take: the write goes live at once, and Needs-you gets a row such as "<session title> opened network access
     to api.example.com at 14:02", with **Undo**. Undo removes or restores that one item as a normal save. The row
     goes when you press Undo or dismiss it. Dismissing writes a new `settings.heads-up.dismissed { file, line }`
     ledger event, and the NeedsYou projection drops the row on it. That is one new event type, not a new store. When the item has changed again since, **Undo** is
     replaced by "Changed again since. Open Settings", so Undo never overwrites a newer change.
   - Validation already refuses `*`, `*.com`, IP hosts and `localhost`; `/`, `$HOME` and the Sindri home as repos;
     non-https or credential-bearing URLs; and `command:` references Sindri can't find.
   - The other option: no row. The change shows only in "Last changed by", See changes and the ledger.

## 10. Settled

- Settings versions: none (owner).
- Workflow versions: the existing `workflow_revision` rows and `active_revision` (owner, §2).
- Who may change settings: any session, recorded in the ledger (owner).
- Recipes: retired by #2356.
- The optimistic check: none. A file lock orders writes, and the same key's later save wins (§3.2).
- Open: §9 Q1 (a heads-up row when a session widens agents' reach), for the owner.
