# Sindri v2: config versions, no profile (#2373)

Status: draft, for the owner's read. Part of #2258. Stacked after #2357, which removes the approval step, and
after #2356, which retires recipes.

## 1. The decision

Owner, 2026-11-08: Sindri v2 has no profile concept. Everything is active and available, and settings are managed
through versions. Activating a version needs no approval: it is a flag naming the active version.

#2357 already removed the approval step: `profile approve`, `needsApproval`, `changedSinceApproval`, the approval
rows, and the typed challenge's use on the profile. What stays is the noun "profile", one file edited in place,
and a content hash (`profileHash`, `ifHash`) used as the write guard. This spec replaces all three.

The first owner question (§9 Q1) is which of two readings of "managed through versions" is meant. The rest of the
spec is written for reading A, and §9 says what changes under B.

## 2. What the person sees

- Settings has a **Versions** list: the newest first, each with its number, when it was saved, who saved it (the
  person, or a named agent session) and a one-line summary of what changed. The active one carries an `Active` chip.
- Saving any setting writes a new version and makes it active in the same write. There is no second step.
- **Use this version** on any older row makes it active. That is the only "rollback" there is. It writes nothing
  but the flag, and it is one click (no typed challenge, no confirm sheet).
- The word "profile" appears nowhere: not in the UI, CLI, API, errors or docs. The settings file is "your settings"
  in copy and `config` in code and on the wire.
- Setup loses its "Save the profile" step. With recipes gone after #2356, its list is repo, mcp, map, sources.

## 3. Storage

```
$AW_SINDRI_HOME/config/
  versions/
    0001.yaml        # the whole settings text, mode 0444, never rewritten
    0001.json        # its record: savedAt, savedBy, sha256, changed[]; mode 0444
    0002.yaml
    0002.json
    ...
  active             # one line: the active version number, e.g. "0002"
  .lock              # held (flock, exclusive) by every write
```

- **Whole files.** A version is the whole settings text (comments kept), never a diff. Files are a few KB, so any
  version can be activated without replaying anything.
- **Numbers.** A version number is parsed as an integer and written padded to at least 4 digits: `0001` … `9999`,
  then `10000`. Sorting is by the integer, never by the string.
- **The write lock.** Every write (a save or a `use`) takes an exclusive `flock` on `config/.lock` and holds it from
  reading `active` until `active` has been replaced. `config set` and `config edit` run without the control plane,
  so the lock has to be cross-process. A file lock covers that and also orders a save against a concurrent `use`.
  A writer that can't get the lock within 5 s fails with `SND-CONFIG-409` ("another write is in progress").
- **A save, in order:**
  1. Take the lock and read `active` = n.
  2. Check `ifVersion` against n. When they differ, release the lock and answer 409 `SND-CONFIG-409` with the
     current number.
  3. Apply the edit to n's text with today's in-place writers (`YamlProfileKeyWriter`, `YamlProfileRepoWriter`, the
     index and sandbox writers, renamed). Validate the result, and stop if it changed nothing (no version is written).
  4. Write the text and its record to temp files. Create `versions/<n+1>.yaml` and `.json` with `link()` from those
     temp files, which fails with `EEXIST` rather than replacing a file. `EEXIST` is answered as 409
     `SND-CONFIG-409`: it can only mean a writer that ignored the lock, and nothing is overwritten.
  5. `chmod 0444` both files.
  6. Write a new `active` to a temp file and rename it over the old one. This rename is the commit point: a crash
     before it leaves an unused version file and n still active.
  7. Release the lock.
- **A `use <m>`:** take the lock, check `m` exists and its text validates, rename a new `active`, release the lock.
  An old version that no longer validates (written by an older, looser build) is refused with `SND-CONFIG-012` and
  its problem, so nothing runs on a file that wouldn't pass validation.
- **Hand edits.** Version files are read-only (0444), and the docs teach only `sindri config edit`: it copies the
  active text to a temp file, opens `$EDITOR`, and saves the result as a new version through the steps above. A
  version's `.json` record holds the sha256 of its text. When a person forces an edit anyway (chmod, then edit),
  the load sees the mismatch and refuses that version with `SND-CONFIG-013` ("version 0003 was changed by hand; save
  your change with `sindri config edit`"). So "Use this version 0003" always brings back what 0003 was.
- **The record.** `changed[]` is computed at the save from a key-path diff of n against n+1 (`repos[2].path`,
  `index.pages`, `review.login`). The list shows the first three paths and "and N more". `savedBy` is `person`
  or the session's id (§5 "Who may write").
- **Source of truth.** The Versions list reads the files: the numbers and each `.json` record. The ledger gets a
  `config.version.activated` event when serve sees `active` change (it already watches the file's stamp). The event
  is the audit trail and the UI's refresh signal, but the list never depends on it, because `config set` can run
  while serve is down.
- **Retention.** Every version is kept (§9 Q2).

## 4. Migration

On the first start of a build with this change:

1. When `$AW_SINDRI_HOME/profile/profile.yaml` exists and `config/` doesn't, copy it to `config/versions/0001.yaml`
   (its record says `savedBy: migrated`, `changed: []`) and write `active` = `0001`. Leave `profile/` in place for
   one release.
2. Remove `profile/approvals/` (the cleanup #2357 already does stays as is).
3. Ledger: one `config.version.activated` event for `0001` with `reason: migrated`.

The step is idempotent: with `config/` present it does nothing. `sindri doctor` gets one `config` row, which warns
while a leftover `profile/` sits beside `config/` and names `sindri config cleanup` as the fix. `config cleanup`
removes `profile/` and nothing else.

**Downgrade.** A build from before this change reads `profile/profile.yaml`, so it runs on the settings as they were
at the migration. Saves made since then are not in that file. Going back to the newer build picks up `config/`
again, unchanged. After `config cleanup`, an older build finds no settings and starts as a fresh install.

## 5. Wire and code renames

| Today | After |
|---|---|
| `GET /sindri/profile` | `GET /sindri/config` (the active version: `version`, `savedAt`, `savedBy`, `text`, `problem`) |
| none | `GET /sindri/config/versions` (the list), `POST /sindri/config/versions/{n}/activate` |
| `status.profile { live, problem, source, modelPick, labelPick, surfacePick }` | `status.config { activeVersion, problem, source, modelPick, labelPick, surfacePick }` |
| `profileHash` (responses), `ifHash` (writes) | `version` (responses), `ifVersion` (writes); a moved version is 409 `SND-CONFIG-409` |
| event `profile.approved` | `config.version.activated { version, reason: saved \| activated \| migrated, by }` |
| `SND-PROFILE-*` | `SND-CONFIG-*`, same numbers |
| `sindri profile init \| set \| validate` | `sindri config init \| set \| validate \| edit \| versions \| use <n> \| cleanup` |
| `IProfileSource`, `IApprovedProfile`, `LoadedProfile`, `SindriProfile`, `ProfileHash`, `Profile/` folders | `IConfigSource`, `IActiveConfig`, `LoadedConfig`, `SindriConfig`, `ConfigVersion`, `Config/` folders |
| Setup step `profile` | removed |
| NeedsYou: profile drift row, "approved before auto registration" row | removed; the MCP consent row keys on `setup.consent.given` alone |

`version` is the number as a string ("0002"), not a hash. A number reads in copy ("version 12"), sorts, and is a
stronger write guard than a hash: two edits that happen to produce the same text still get different numbers. The
content hash moves into the version's record and leaves the wire.

The UI's `ProfileWriteFlow`, `PagesSetBody`, `PinConceptsBody` and the todo note-setup send `ifVersion`. Context and
ui rename at the same time in this PR (one PR, stacked after #2357). There is no window where the old names are
served, so there is no compatibility shim.

**Who may write.** The agent-first design's D2 says agent sessions may run every verb in Sindri, and the connection
verbs (#2298) already follow it. Config follows it too: a launched session may run `config set`, `edit` and `use`, and
the `SND-AUTH-002` refusal leaves these verbs. Each version's `savedBy` names the session, and the Versions list shows
it, so any change an agent made can be seen and undone in one click (§9 Q3).

## 6. Docs

`docs/csharp/sindri-profile.md` becomes `sindri-config.md`. These follow:
- AGENTS.md's command lines
- `docs/sindri/SETUP.md`, `context.md`, `errors.md` and `connectai-live-check.md`
- the `sindri-setup.html` page
- `scripts/context-first-run.sh`

`scripts/tests/check-profile-doc.test.sh` becomes `check-config-doc.test.sh` and checks `SindriConfig`'s keys.

## 7. Proof

- Unit:
  - A save makes n+1 and activates it.
  - An `ifVersion` mismatch is 409.
  - An unchanged edit writes nothing.
  - An old version can be activated.
  - An invalid version is refused.
  - A hand-changed version (sha256 mismatch) is refused.
  - `changed[]` lists the key paths.
  - A crash between the version write and the `active` rename leaves the old version active: a fake file system
    fails the second rename.
  - Numbers past 9999.
- Two writers:
  - Two processes save against the same `ifVersion`: exactly one gets n+1, the other gets 409, and n+1's text is
    the winner's.
  - A save racing a `use`: the lock orders them, and `active` ends on whichever wrote last.
  - A writer that skips the lock and finds the next number taken gets `EEXIST`, answered as 409.
  - These run as real child processes (Lane=integration).
- Integration:
  - The migration on a home with `profile/profile.yaml`, run twice.
  - `GET /sindri/config/versions` and activate, through the API goldens.
  - `EndpointCqrsTests` for the new POST.
- UI:
  - The Versions list and **Use this version** in the Frames tests.
  - Setup without the profile step.
- aot-smoke: its `profile/profile.yaml` lines become `config/versions/0001.yaml`, and expected.txt is re-pinned.
- A repo-wide `git grep -i profile -- src tests docs` returns only the migration code, its tests and this spec.

## 8. Size

About 6k lines in about 1000 files under `src/Sindri` say profile today, plus 4.5k in tests and 3k in docs. Most of
it is a mechanical rename: types, folders, namespaces, error codes and copy.

The new code is small:
- the version store and its lock (§3)
- the migration (§4)
- two routes and one event
- the Versions list

The rename runs as scripted passes, one commit per area (Core, Features, Infrastructure, Host, Ui, tests, docs),
each building on its own.

## 9. Questions for the owner

1. **What gets versions: one settings file, or every thing?** "Everything is active and available, managed through
   versions" reads two ways.
   - **A: one settings file.** The settings file is versioned as a whole and one version is active. Repos,
     connections, index pins and pages are keys inside it, so they change by saving a new version. This is what §2
     to §8 describe.
   - **B: every thing.** Each repo, connection, workflow and block has its own versions and its own active flag.
     Workflows already work that way: `workflow_revision` in ledger.db, one immutable revision per save. Under B
     there is no settings file to version. Each kind gets a `*_revision` table and an active pointer, and the
     Versions list is per thing.

   I'd go A for this PR. It's the change the owner's words force (no profile, no approval), it's about 1 new file
   format plus a rename, and B's per-thing model already exists where it pays (workflows). B would turn repos,
   connections and pins from YAML keys into ledger rows, which is a store rewrite, not a cleanup. If B is meant, it
   gets its own spec, and #2373 shrinks to the rename plus "settings are a ledger-backed thing like workflows".
2. **How many versions do we keep?** I'd keep all of them. They're a few KB each, and an old version you can't go
   back to is a surprise. The other option is the last 100.
3. **Agents change settings, per D2: is that what you want here?** §5 lets a session run `config set`, `edit` and
   `use`, with `savedBy` naming the session. The other option keeps these verbs the person's, as `profile set` is
   now. That would be the one exception to D2, so I'd go with D2.

Settled without a question: a save activates at once. A separate "activate" step after a save would be approval in
everything but name.
