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
- **The write guard is a file lock, not a hash.** Every write takes an exclusive `flock` on `settings/.lock` and
  holds it while it reads the file, applies its edit, validates, writes a temp file and renames it over
  `settings.yaml`. A writer that can't get the lock within 5 s fails with `SND-SETTINGS-409` ("another change is
  being saved; try again").
  - The lock keeps two writers from losing each other's edits. Every write is a key-level edit (one key, one repo,
    one page, one pin set) applied to the current text, so two saves to different keys both land.
  - Two saves to the same key: the later one wins. That's the right answer for one person's app. The hash check
    existed for approval (the approved text must not shift under you), and approval is gone.
  - Why a file lock: `sindri settings set` runs without the control plane, so a guard inside serve wouldn't cover
    it.
- **Nothing runs on a broken file.** Validation stays before the rename, as today. A hand edit that breaks the file
  is read as `unreadable` with its problem (the `SND-SETTINGS-*` code) until it's fixed.
- **Who may write:** any session, agents included (agent-first D2). The `SND-AUTH-002` refusal leaves `settings set`,
  `settings init`, the page and pin writes and the repo add.
- **Who changed what:** every save writes one ledger event, `settings.changed { by, keys[] }`.
  - `by` is `person` or the session's id.
  - `keys[]` is the key paths the save touched (`repos[2].path`, `index.pages`).
  - Serve owns the ledger, and `settings set` can run while serve is down (today `profile set` writes no event
    at all). So every writer appends one JSON line `{ at, by, keys[] }` to `settings/changes.jsonl` while it holds
    the lock. Serve copies the lines it hasn't seen into `settings.changed` events: at start, and each time it sees
    the file change. Its last copied line number lives in views.db, so the ledger has every change exactly once,
    including the ones made while serve was down.

## 4. Renames

| Today | After |
|---|---|
| `$AW_SINDRI_HOME/profile/profile.yaml` | `$AW_SINDRI_HOME/settings/settings.yaml` |
| `GET /sindri/profile` | `GET /sindri/settings` (`text`, `problem`, `source`) |
| `status.profile { live, problem, source, modelPick, labelPick, surfacePick }` | `status.settings { problem, source, modelPick, labelPick, surfacePick }` |
| `profileHash` in responses, `ifHash` in writes | gone |
| events `profile.approved`, `profile.checked` | `settings.changed { by, keys[] }` |
| `SND-PROFILE-*` | `SND-SETTINGS-*`, same numbers |
| `sindri profile init \| set \| validate` | `sindri settings init \| set \| validate` |
| `IProfileSource`, `IApprovedProfile`, `LoadedProfile`, `SindriProfile`, `ProfileHash`, `Profile/` folders | `ISettingsSource`, `ISettings`, `LoadedSettings`, `SindriSettings`, gone, `Settings/` folders |
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
- Two writers (real child processes, Lane=integration):
  - Saves to different keys both land.
  - Saves to the same key: the later one wins.
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
