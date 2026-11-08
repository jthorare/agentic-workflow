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
  1. Read `settings.yaml`.
  2. Apply its edit and validate the result. When the edit is invalid or changes nothing, stop: nothing is written.
  3. Write the result to a temp file and rename it over `settings.yaml`. This is the save.
  4. Append one JSON line `{ at, by, keys[] }` to `settings/changes.jsonl` (below).

  A crash between steps 3 and 4 loses one audit line, but never logs a save that didn't land. A writer that can't
  get the lock within 5 s fails with `SND-SETTINGS-409`. Why a file lock: `sindri settings set` runs without the
  control plane, so a guard inside serve wouldn't cover it.
- **Every write is item-level.** Each write changes one item of the current text:
  - one scalar key (`settings set <key> <value>`)
  - one repo (add or remove by path)
  - one page by id (set or remove)
  - one pin by id (set or remove)
  - one sandbox host (add or remove)

  No write replaces a whole list. `context page set --from-file` and `context pin set --from-file` become one
  set per item in the file, and they never remove an item the file leaves out: removing is `remove <id>` only.
  With item-level writes, two saves only collide on the same item, and then the later one wins. That's the right
  answer for one person's app. Without that rule, a stale app form that replaces the whole pages list would drop
  a page an agent added in between. The hash check existed for approval (the approved text must not shift under
  you), and approval is gone.
- **Hand edits.** An editor's save doesn't take the lock, so a writer that read the file before it can rename over
  it and drop the hand edit. The docs say: while agents run, change settings with `sindri settings set` or the app.
  Edit the file by hand only when nothing else is writing.
- **Nothing runs on a broken file.** Validation stays before the rename, as today. A hand edit that breaks the file
  is read as `unreadable` with its problem (the `SND-SETTINGS-*` code) until it's fixed.
- **Who may write:** any session, agents included (agent-first D2). The `SND-AUTH-002` refusal leaves `settings set`,
  `settings init`, the page and pin writes and the repo add.
- **Who changed what:** the ledger gets one `settings.changed { line, by, keys[] }` event per save.
  - `by` is `person` or the session's id.
  - `keys[]` lists the item paths the save touched (`repos[2].path`, `index.pages[checkout]`).
  - Serve owns the ledger, and `settings set` can run while serve is down. Today `profile set` writes no event at
    all.
  - So each writer appends its line to `settings/changes.jsonl` (step 4 above). Serve copies every line into a
    `settings.changed` event, at start and each time it sees the file change.
  - Each event's idempotency key is `settings.changed:<file>:<line>` (below), where `<line>` is the line's number in the file.
    Copying is exactly-once by key: a re-copy after `views rebuild` (which wipes any cursor in views.db) changes
    nothing.
  - `changes.jsonl` is never trimmed or deleted, by Sindri or by `settings cleanup`. It holds one short line per
    save, so line numbers stay stable.
  - **A file id in the key.** Line 1 of `changes.jsonl` is `{ "file": "<ulid>" }`, written when the file is first
    created. The key is `settings.changed:<file>:<line>`. If someone deletes the file by hand anyway, the next save
    starts a new file id, so its events never collide with the old keys and are never dropped as duplicates.
  - **Torn lines.** A crash during an append can leave a partial last line. Before it appends, the writer adds `\n`
    when the file doesn't end in one. Serve skips any line that isn't valid JSON but still counts it, so line
    numbers stay stable. A skipped line is one lost audit entry, logged once as a warning.

- **What the person sees:**
  - Settings shows one line under its heading: "Last changed by you at 14:02" or "Last changed by <session name>
    at 14:02". It comes from the newest `settings.changed` and reads "Not changed since setup" when there is none.
  - A save that hits the busy lock shows "Another change is being saved. Try again in a moment." with a **Try again**
    button. The code (`SND-SETTINGS-409`) appears only in the CLI's `--json` output and the logs.
  - A broken file shows "Your settings file has a mistake: <problem>", with the file path and the line.

## 4. Renames

| Today | After |
|---|---|
| `$AW_SINDRI_HOME/profile/profile.yaml` | `$AW_SINDRI_HOME/settings/settings.yaml` |
| `GET /sindri/profile` | `GET /sindri/settings` (`text`, `problem`, `source`) |
| `status.profile { live, problem, source, modelPick, labelPick, surfacePick }` | `status.settings { problem, source, modelPick, labelPick, surfacePick }` |
| `profileHash` in responses, `ifHash` in writes | gone |
| events `profile.approved`, `profile.checked` | `settings.changed { file, line, by, keys[] }`, key `settings.changed:<file>:<line>` |
| `SND-PROFILE-*` | `SND-SETTINGS-*`, same numbers |
| `sindri profile init \| set \| validate` | `sindri settings init \| set \| validate` |
| `IProfileSource`, `IApprovedProfile`, `LoadedProfile`, `SindriProfile`, `ProfileHash`, `Profile/` folders | `ISettingsSource`, `ISettings`, `LoadedSettings`, `SindriSettings`, gone, `Settings/` folders |
| the existing Infrastructure `SindriSettings(Home, ProfilePath)` (142 uses) | `SindriPaths(Home, SettingsPath)`, renamed first in its own commit so `SindriSettings` is free for the settings document |
| Setup step `profile` | removed: the list is repo, mcp, map, sources |
| NeedsYou: the profile drift row and the "approved before auto registration" row | removed; the MCP consent row keys on `setup.consent.given` alone |
| Health "Profile" row | "Settings" row: `ok` with the file, or `unreadable` with the problem; the three picks |

The UI's `ProfileWriteFlow`, `PagesSetBody` and `PinConceptsBody` and the todo note-setup stop sending `ifHash`.
Context and ui rename in the same PR, so the old names are never served beside the new ones and nothing needs a
shim.

## 5. Migration

On the first start of a build with this change, when `profile/profile.yaml` exists and `settings/` doesn't:
1. Copy `profile/profile.yaml` to `settings/settings.yaml`.
2. Write one `settings.changed` event with `by: migrated`.
3. Leave `profile/` in place for one release.

The step is idempotent. `sindri doctor`'s `settings` row warns while `profile/` is still there and names
`sindri settings cleanup`, which removes `profile/` and nothing else.

Downgrade: an older build reads `profile/profile.yaml`, so it runs on the settings as they were at the migration.
After `settings cleanup` it finds none and starts as a fresh install.

## 6. Docs

`docs/csharp/sindri-profile.md` becomes `sindri-settings.md`. The following also change:
- AGENTS.md's command lines
- `docs/sindri/SETUP.md`, `context.md`, `errors.md` and `connectai-live-check.md`
- `sindri-setup.html`
- `scripts/context-first-run.sh`

`check-profile-doc.test.sh` becomes `check-settings-doc.test.sh`.

## 7. Proof

- Unit:
  - A save is live when it returns.
  - An invalid edit writes nothing.
  - A save writes one `settings.changed` with its `by` and `keys[]`.
  - A session's save is accepted, with `by` set to its id.
  - A crash after the rename and before the append leaves the save in place and no line in the file.
  - Copying `changes.jsonl` twice, the second time after `views rebuild`, writes each event once.
- Two writers (real child processes, Lane=integration):
  - Saves to different keys both land.
  - Saves to the same key: the later one wins.
  - An app page save and an agent's page add on different page ids both land.
  - A `--from-file` with fewer pages than the file holds removes none of them.
  - A writer kept off the lock for 5 s gets `SND-SETTINGS-409`.
- Integration:
  - The migration on a home with `profile/profile.yaml`, run twice.
  - `GET /sindri/settings` and status in the API goldens.
  - The page and pin writes without `ifHash`.
- UI: Setup without the profile step, the Health "Settings" row, the page sheet saving without a hash (Frames tests).
- aot-smoke: its `profile/profile.yaml` lines move to `settings/settings.yaml`, and expected.txt is re-pinned.
- `git grep -i profile -- src tests docs` returns only the migration code, its tests and this spec.

## 8. Size

About 6k lines in about 1000 `src/Sindri` files say profile, plus 4.5k in tests and 3k in docs.
- Almost all of it is a scripted rename, one commit per area (Core, Features, Infrastructure, Host, Ui, tests,
  docs), each building on its own.
- The removals shrink the code: the hash guard (about 494 lines across src, tests and docs), `ProfileHash`, the
  profile Setup step and two NeedsYou rows.
- The new code is the file lock, the `settings.changed` event, the migration and `settings cleanup`.

## 9. Settled, no question left

- Settings versions: none (owner).
- Workflow versions: the existing `workflow_revision` rows and `active_revision` (owner, §2).
- Who may change settings: any session, recorded in the ledger (owner).
- Recipes: retired by #2356.
- The optimistic check: none. A file lock orders writes, and the same key's later save wins (§3).
