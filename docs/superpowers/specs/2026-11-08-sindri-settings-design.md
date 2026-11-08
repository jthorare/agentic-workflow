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

This spec removes all three. It adds no new store.

## 2. Workflows: nothing to add

Ledger migration 0008 already has the model the owner described:
- `workflow.active_revision` is the active flag (null = off).
- `workflow_revision (workflow_id, revision, spec, spec_hash, created_by, created_at)` is one immutable row per save.
- Saving on a revision that is no longer the latest is `SND-GRAPH-007`.
- `Activate` pins the revision triggers run.

This spec changes none of it.

## 3. Settings

- **One file:** `$AW_SINDRI_HOME/settings/settings.yaml`, edited in place, comments kept, as `profile.yaml` is today.
- **A save applies at once.** Whatever a save writes is live when the write returns. There is no draft, approval or
  history.
- **The write guard is a file lock, not a hash.** Every write takes an exclusive `flock` on `settings/.lock`. In
  order, while it holds the lock:
  1. Run the migration helper (§5). It does nothing once `settings/settings.yaml` exists.
  2. Read `settings.yaml` and note its stamp (last write time and length).
  3. Apply the edit and validate the result (below). When the edit is invalid or changes nothing, stop: nothing is
     written.
  4. Write the result to a temp file created 0600, and fsync it. Check that the stamp from step 2 is unchanged:
     when it moved, a hand edit landed in between, so stop with `SND-SETTINGS-409` and write nothing. Then rename
     the temp file over `settings.yaml`. This is the save.
  5. Append one line to `settings/changes.jsonl` (below).

  A crash between steps 4 and 5 loses one audit line, but never logs a save that didn't land. Why a file lock:
  `sindri settings set` runs without the control plane, so a guard inside serve wouldn't cover it. The cache serve
  reads keys on the file's stamp (as `LiveApprovedProfile` does today), so a CLI save is live for serve's next read.
- **Busy lock.** A writer waits up to 5 s for the lock. After 1 s the CLI prints `waiting for another settings
  save...` on stderr. If the 5 s run out, the CLI exits 1 with "Another change is being saved. Try again in a
  moment.", and `--json` gives `{ "code": "SND-SETTINGS-409", "message": ... }`. The HTTP routes answer 409 with
  the same body.
- **Every write is item-level.** A write sets or removes items in the current text and never replaces a whole list:
  - one scalar key (`settings set <key> <value>`)
  - one repo, by name
  - one page, by id
  - one pin, by id
  - one sandbox host

  `context page set --from-file` and `context pin set --from-file` are one write each: one lock hold, one save
  and one audit line, setting every item in the file. They never remove an item the file leaves out; removing is
  `remove <id>` only, and the CLI's summary line says so (`set 3 pages; pages not in the file are kept`).

  Two saves only collide on the same item, and then the later one wins. That's the right answer for one person's
  app. Without the rule, a stale app form that replaces the whole pages list would drop a page an agent added in
  between. The hash check existed for approval (the approved text must not shift under you), and approval is gone.
- **What validation checks.** With approval gone, step 3 is the only check before a write goes live. Every refusal
  names the key path and the rule. It never quotes the value, so a pasted secret never reaches the API, the logs or
  the ledger.
  - **Schema:** known keys, types and ranges, as `IProfileSource.Load` checks today (`SND-SETTINGS-001..` keep their
    numbers).
  - **Repo add:** the path exists and is a git checkout. It is not `/`, not `$HOME`, not a parent of
    `$AW_SINDRI_HOME`, and not inside `$AW_SINDRI_HOME` (`SND-SETTINGS-020`).
  - **Repo remove:** drops the settings entry only. It never touches the checkout or its worktrees.
  - **Sandbox host:** a host name or a `*.domain` wildcard with at least two labels. A bare `*`, an IP literal, and
    `localhost` are refused (`SND-SETTINGS-021`).
  - **Source URLs** (connections, library vaults): `https`. A loopback host is accepted only when the matching
    `AW_*_URL` test override names it (`SND-SETTINGS-022`).
  - **Secrets:** a value that looks like a credential (the `Aw.Redaction` patterns) is refused. Settings hold
    credential references (`env:NAME`, `command:argv`), never the credential (`SND-SETTINGS-023`).
- **Files and modes.** `settings.yaml`, `changes.jsonl` and `.lock` are 0600 in a 0700 `settings/`. The temp file
  is created 0600, so a rename never widens the mode.
- **A broken file never stops work.** A hand edit or an agent's Edit-tool save can still break the file. Serve then
  keeps running on the last settings that validated. Health shows "Your settings file has a mistake on line 14
  (`repos[2].path`: not a git checkout). Sindri keeps using your last good settings, from 14:02. Fix the file and
  save it, and this clears." `sindri settings validate` prints the same text. The message clears on the next read
  that validates. A fresh start with a broken file and no last good settings runs nothing, as today.
- **Hand edits.** The docs say: while agents run, change settings with `sindri settings set` or the app. A hand
  edit between a writer's read and its rename fails that writer with 409 (step 4), so the hand edit is never lost.
  Serve also records hand edits. Each audit line carries the sha256 of the file it saved. When serve sees a stamp
  change whose content doesn't match the last line's sha256, it takes the lock and appends a line with
  `by: hand-edit`, holding the paths that changed.
- **Who may write:** any session, agents included (agent-first D2). The `SND-AUTH-002` refusal leaves `settings set`,
  `settings init`, the page and pin writes and the repo add. `by` comes from the caller. A launched session's CLI
  sends its `AW_SINDRI_SESSION` id, the app sends `person`, and a CLI with neither is `person`.
- **Who changed what:** the ledger gets one `settings.changed { file, line, by, sha256, changes[] }` event per
  audit line.
  - `changes[]` holds `{ path, from, to }`. Paths name items by key, not position (`repos[checkout].path`,
    `index.pages[review]`), so removing a repo never shifts another's path. `from` and `to` are capped at 200
    characters. Validation already keeps credentials out of values.
  - Serve owns the ledger, and `settings set` can run while serve is down. Today `profile set` writes no event at
    all. So each writer appends its line to `settings/changes.jsonl` (step 5). Serve copies every line into a
    `settings.changed` event, at start and each time it sees the file change.
  - **The key.** Line 1 of `changes.jsonl` is `{ "file": "<ulid>" }`, written when the file is first created. Each
    event's idempotency key is `settings.changed:<file>:<line>`. `SqliteLedger.InsertOnce` drops a second insert
    with the same key, so a re-copy after `views rebuild` changes nothing.
  - `changes.jsonl` is never trimmed or deleted, by Sindri or by `settings cleanup`, so line numbers stay stable.
    If someone deletes it by hand anyway, the next save starts a new file id, so its keys never collide with the
    old ones.
  - **Complete lines only.** Serve copies a line only once it ends in `\n`. A last line without one may still be
    in flight, so serve leaves it for the next pass and doesn't count it yet. Before it appends, a writer adds `\n`
    when the file doesn't end in one. That turns a crash's partial line into a complete line that doesn't parse.
    Serve counts such a line but skips it, logs one warning, and keeps line numbers stable. A copied line must also
    match the line schema (`at`, `by`, `sha256`, `changes[]`, under 64 KB), or it is skipped the same way.
- **What the person sees:**
  - **Last changed.** Settings shows one line under its heading: "Last changed by you at 14:02", "Last changed by
    <session title> at 14:02" (the agent's session title, as the Agents page shows it), "Changed by hand at 14:02",
    or "Moved over from your old settings at 14:02" after the migration. A save's own response carries `{ by, at }`,
    so the line updates the moment the person saves. The `settings.changed` event refreshes it for everyone else.
  - **Busy lock.** While a save waits for the lock, its button shows `Saving...`. If it fails, the sheet keeps
    the typed values and shows "Another change is being saved. Try again in a moment." with **Try again**, which
    sends the same save. The message is a polite live region, and focus stays on the save button. The code
    (`SND-SETTINGS-409`) shows only in the CLI's `--json` output and the logs.
  - **A stale open sheet.** The page and pin sheets watch `settings.changed`. When an event touches the item a
    sheet has open and didn't come from that sheet, the sheet shows "<who> changed this page while you had it
    open" with **Load theirs** and **Keep mine**. Keep mine saves the person's values over theirs. A change to
    another item shows nothing.
  - **Broken file:** the Health text above, in a warning row whose state is in its words, not only its color.

## 4. Renames

| Today | After |
|---|---|
| `$AW_SINDRI_HOME/profile/profile.yaml` | `$AW_SINDRI_HOME/settings/settings.yaml` |
| `GET /sindri/profile` | `GET /sindri/settings` (`text`, `problem`, `source`) |
| `status.profile { live, problem, source, modelPick, labelPick, surfacePick }` | `status.settings { problem, source, modelPick, labelPick, surfacePick }` |
| `profileHash` in responses, `ifHash` in writes | gone |
| events `profile.approved`, `profile.checked` | `settings.changed { file, line, by, sha256, changes[] }`, key `settings.changed:<file>:<line>` |
| `SND-PROFILE-*` | `SND-SETTINGS-*`, same numbers |
| `sindri profile init \| set \| validate` | `sindri settings init \| set \| validate \| cleanup`; `sindri profile *` prints the new name for one release (§5) |
| `AW_SINDRI_PROFILE` | `AW_SINDRI_SETTINGS`; the old name read with a warning for one release (§6) |
| `IProfileSource`, `IApprovedProfile`, `LoadedProfile`, `SindriProfile`, `ProfileHash`, `Profile/` folders | `ISettingsSource`, `ISettings`, `LoadedSettings`, `SindriSettings`, gone, `Settings/` folders |
| the existing Infrastructure `SindriSettings(Home, ProfilePath)` (142 uses) | `SindriPaths(Home, SettingsPath)`, renamed first in its own commit so `SindriSettings` is free for the settings document |
| Setup step `profile` | removed: the list is repo, mcp, map, sources |
| NeedsYou: the profile drift row and the "approved before auto registration" row | removed; the MCP consent row keys on `setup.consent.given` alone |
| Health "Profile" row | "Settings" row: `ok` with the file, or `unreadable` with the problem; the three picks |

The UI's `ProfileWriteFlow`, `PagesSetBody` and `PinConceptsBody` and the todo note-setup stop sending `ifHash`.
Context and ui rename in the same PR, so the old names are never served beside the new ones and nothing needs a
shim.

## 5. Migration

One helper, `SettingsMigration.Run`, runs under the settings lock. Serve's start and every settings verb (step 1
of a write, and the reads `settings validate` and `GET /sindri/settings`) call it before anything else, so a
`settings set` before serve ever starts still migrates. When `profile/profile.yaml` exists and
`settings/settings.yaml` doesn't, it runs these steps:
1. Copy `profile/profile.yaml` to `settings/settings.yaml` by temp file and rename.
2. Create `changes.jsonl` with its file-id line and one line `by: migrated`, which serve copies like any other.
3. Leave `profile/` in place for one release.

A crash after step 1 and before step 2 is picked up by the next run: `settings.yaml` exists and `changes.jsonl`
doesn't, so the helper writes step 2. When both `profile/` and `settings/` exist, `settings/` wins, and `sindri
doctor`'s `settings` row warns and names `sindri settings cleanup`, which removes `profile/` and nothing else.
The Health "Settings" row shows the same warning, so a person who only uses the app sees it too. `settings init`
refuses when `settings.yaml` exists, as `profile init` does today.

**Downgrade.** An older build reads `profile/profile.yaml`, which holds the settings as they were at the migration.
Every change saved after that is invisible to it, with no warning, because the older build doesn't know
`settings/` exists. After `settings cleanup`, an older build finds no settings and starts as a fresh install.

**Old verbs.** For one release, `sindri profile <anything>` exits 2 with `sindri profile is now sindri settings:
run sindri settings <same args>`. An old script then fails with that line, not a usage dump.

## 6. Docs and callers

`docs/csharp/sindri-profile.md` becomes `sindri-settings.md`. The following also change:
- AGENTS.md's command lines.
- `docs/sindri/SETUP.md`, `context.md`, `errors.md` and `connectai-live-check.md`.
- `sindri-setup.html`.
- `AW_SINDRI_PROFILE` becomes `AW_SINDRI_SETTINGS` in `SettingsFromEnvironment.cs`, the child-environment allow
  list, `.env.example` and the scripts that unset or read it (`context-first-run.sh`, `model-limit-check.sh`,
  `owner-check.sh`, `sindri-pod.sh`). For one release, a set `AW_SINDRI_PROFILE` is still read, with a one-line
  warning naming the new variable.
- Every script and hook that calls `sindri profile` or reads `profile/profile.yaml`, with its test:
  `config/hooks/sindri-nudge.sh`, `context-first-run.sh`, `cutover-clean-ts.sh`, `cutover-prove.sh`,
  `install-cs.sh`, `linear-probe.sh`, `model-limit-check.sh`, `owner-check.sh`, `probe-linear-projects.sh`,
  `sindri-pod.sh`, `sindri-sandbox-e2e.sh` and `sindri-ui-fixture.sh`, plus the dry-run golden
  `scripts/tests/fixtures/context-first-run.up-dry-run.txt` and `sindri-smoke-normalize.test.sh`.

`check-profile-doc.test.sh` becomes `check-settings-doc.test.sh`.

## 7. Proof

- Unit:
  - A save is live for serve's next read.
  - An invalid edit writes nothing.
  - A save writes one line with its `by`, `sha256` and `changes[]`.
  - A session's save is accepted, with `by` set to its id.
  - Validation refuses each of: `/`, `$HOME`, a path inside the Sindri home, a bare `*` host, an IP host, an http
    source URL, a value matching a credential pattern. No refusal text holds the value.
  - A `--from-file` with three pages is one save and one line, and keeps pages not in the file.
  - Removing one repo doesn't change another repo's path in `changes[]`.
  - A broken file keeps the last good settings in use, and the next valid read clears the warning.
- The audit copy:
  - A crash after the rename and before the append leaves the save in place and no line.
  - Copying twice, the second time after `views rebuild`, writes each event once.
  - A last line without `\n` is not copied, then is copied once it completes.
  - A torn line padded by the next append is counted, skipped and warned once, and later lines keep their keys.
  - Deleting `changes.jsonl` and saving again starts a new file id, and its events land.
  - A hand edit seen by serve becomes one `by: hand-edit` line.
- Two writers (real child processes, Lane=integration):
  - Saves to different items both land.
  - Saves to the same item: the later one wins.
  - An app page save and an agent's page add on different page ids both land.
  - A hand edit between a writer's read and rename fails the writer with 409 and is kept.
  - A writer kept off the lock for 5 s gets `SND-SETTINGS-409`, exit 1, with the `--json` shape.
- Migration:
  - `settings set` before serve ever started migrates.
  - Run twice: no second copy.
  - A crash between the copy and `changes.jsonl` is finished by the next run.
  - With both folders, `settings/` wins and doctor warns.
- Integration: `GET /sindri/settings` and status in the API goldens; the page and pin writes without `ifHash`;
  `EndpointCqrsTests`.
- UI (Frames tests): Setup without the profile step, the Health "Settings" row and its broken-file and leftover
  warnings, the "Last changed" line after the person's own save, the busy message keeping typed values, and the
  stale-sheet banner.
- aot-smoke: its `profile/profile.yaml` lines move to `settings/settings.yaml`, and expected.txt is re-pinned.
- `git grep -i -e "sindri profile" -e "profile/profile.yaml" -e AW_SINDRI_PROFILE -- src tests docs scripts config
  providers setup.sh skills bootstrap` returns only the migration and tombstone code, their tests and this spec.

## 8. Size

About 6k lines in about 1000 `src/Sindri` files say profile, plus 4.5k in tests and 3k in docs.
- Almost all of it is a scripted rename, one commit per area (Core, Features, Infrastructure, Host, Ui, tests,
  docs), each building on its own.
- The removals shrink the code: the hash guard (about 494 lines across src, tests and docs), `ProfileHash`, the
  profile Setup step and two NeedsYou rows.
- The new code is the file lock, the `settings.changed` event, the migration and `settings cleanup`.

## 9. Questions

1. **Should an agent opening network access get a heads-up?** Two reviews flagged one case: a session that adds a
   sandbox host or a repo widens what agents can reach, its own runs included. This isn't about re-adding approval.
   It's about whether you want to hear about it.
   - My take: the write goes live at once, and Needs-you gets a row such as "<session title> opened network
     access to api.example.com at 14:02", with **Undo**. Undo removes that one item as a normal save, and the row
     goes when you dismiss it or press Undo.
   - Validation already refuses `*`, IP hosts and `localhost`, and refuses `/`, `$HOME` and the Sindri home as
     repos.
   - The other option: no row. The change shows only in the "Last changed by" line and the ledger.

## 10. Settled


- Settings versions: none (owner).
- Workflow versions: the existing `workflow_revision` rows and `active_revision` (owner, §2).
- Who may change settings: any session, recorded in the ledger (owner).
- Recipes: retired by #2356.
- The optimistic check: none. A file lock orders writes, and the same key's later save wins (§3).
