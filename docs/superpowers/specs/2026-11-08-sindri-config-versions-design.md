# Sindri v2: config versions, no profile (#2373)

Status: draft, for the owner's read. Part of #2258. Stacked after #2357, which removes the approval step.

## 1. The decision

Owner, 2026-11-08: Sindri v2 has no profile concept. Everything is active and available, and settings are managed
through versions. Activating a version needs no approval: it is a flag naming the active version.

#2357 already removed the approval step: `profile approve`, `needsApproval`, `changedSinceApproval`, the approval
rows, and the typed challenge's use on the profile. What stays is the noun "profile", one file edited in place,
and a content hash (`profileHash`, `ifHash`) used as the write guard. This spec replaces all three.

## 2. What the person sees

- Settings has a **Versions** list: the newest first, each with its number, when it was saved, who saved it (person or
  session) and a one-line summary of what changed. The active one carries an `Active` chip.
- Saving any setting writes a new version and makes it active in the same write. There is no second step.
- **Use this version** on any older row makes it active. That is the only "rollback" there is. It writes nothing
  but the flag, and it is one click (no typed challenge, no confirm sheet).
- The word "profile" appears nowhere: not in the UI, CLI, API, errors or docs. The settings file is "your settings"
  in copy and `config` in code and on the wire.
- Setup loses its "Save the profile" step. Its list becomes repo, recipes, mcp, map, sources (see Q2 for recipes).

## 3. Storage

```
$AW_SINDRI_HOME/config/
  versions/
    0001.yaml        # immutable once written
    0002.yaml
    ...
  active             # one line: the active version number, e.g. "0002"
```

- A version file is the whole settings text (comments kept), never a diff. Files are small (a few KB), so whole
  copies keep reads simple and let any version be activated without replaying anything.
- A write: read `active`, check `ifVersion` against it (409 `SND-CONFIG-409` when it moved), apply the edit to that
  version's text with today's in-place writers (`YamlProfileKeyWriter`, `YamlProfileRepoWriter`, the index and
  sandbox writers, renamed), validate, write `versions/<n+1>.yaml` by temp file + rename, then rename a new `active`
  over the old one. The rename of `active` is the commit point: a crash before it leaves an unused version file and
  the old version still active.
- An edit that changes nothing writes no version (today's "unchanged" exit stays).
- Activate: check the version exists and validates, then rename a new `active`. An invalid old version (written by
  an older build whose rules were looser) is refused with `SND-CONFIG-012` and its problem, so nothing runs on a
  file that wouldn't validate.
- Hand edits: a person may still open the active version's file in an editor. The live cache keys on the file's
  stamp as it does today (`LiveApprovedProfile`); a hand edit of `versions/<n>.yaml` is read as that version's new
  text. `sindri config edit` instead copies the active text to a temp file, opens `$EDITOR`, and saves the result as a
  new version, which is the path the docs teach.
- Retention: every version is kept (see Q3).

## 4. Migration

On the first start of a build with this change:

1. When `$AW_SINDRI_HOME/profile/profile.yaml` exists and `config/` doesn't, copy it to `config/versions/0001.yaml`
   and write `active` = `0001`. Leave `profile/` in place for one release so a downgrade still finds its file.
2. Remove `profile/approvals/` (the cleanup #2357 already does stays as is).
3. Ledger: write one `config.version.activated` event for `0001` with `reason: migrated`.

The step is idempotent: with `config/` present it does nothing. `sindri doctor` gets one row, `config`, which warns
while a leftover `profile/` folder sits beside `config/` and names `sindri config cleanup` as the fix.

## 5. Wire and code renames

| Today | After |
|---|---|
| `GET /sindri/profile` | `GET /sindri/config` (the active version: `version`, `savedAt`, `savedBy`, `text`, `problem`) |
| none | `GET /sindri/config/versions` (the list), `POST /sindri/config/versions/{n}/activate` |
| `status.profile { live, problem, source, modelPick, labelPick, surfacePick }` | `status.config { activeVersion, problem, source, modelPick, labelPick, surfacePick }` |
| `profileHash` (responses), `ifHash` (writes) | `version` (responses), `ifVersion` (writes); a moved version is 409 `SND-CONFIG-409` |
| event `profile.approved` | `config.version.activated { version, reason: saved \| activated \| migrated, by }` |
| `SND-PROFILE-*` | `SND-CONFIG-*`, same numbers |
| `sindri profile init \| set \| validate` | `sindri config init \| set \| validate \| edit \| versions \| use <n>` |
| `IProfileSource`, `IApprovedProfile`, `LoadedProfile`, `SindriProfile`, `ProfileHash`, `Profile/` folders | `IConfigSource`, `IActiveConfig`, `LoadedConfig`, `SindriConfig`, `ConfigVersion`, `Config/` folders |
| Setup step `profile`, `Recipes(…, profileApproved)` | step removed; `Recipes(…, hasConfig)` |
| NeedsYou: profile drift row, "approved before auto registration" row | removed; the MCP consent row keys on `setup.consent.given` alone |

`version` is the zero-padded number as a string ("0002"), not a hash. A number reads in copy ("version 12"), sorts,
and is a stronger write guard than a hash: two edits that happen to produce the same text still get different
numbers. The content hash stays internal (the code index's cache keys) and leaves the wire.

The UI's `ProfileWriteFlow`, `PagesSetBody`, `PinConceptsBody` and the todo note-setup send `ifVersion`. Context and
ui rename at the same time in this PR (one PR, stacked after #2357); there is no window where the old names are
served, so no compatibility shim.

Who may write: unchanged from today. A launched agent session may run the reads and the `PinConcepts` and `SetPages`
jobs; `config set`, `config edit` and `config use` are the person's (`SND-AUTH-002` for a session), as `profile set` is
now (see Q4).

## 6. Docs

`docs/csharp/sindri-profile.md` becomes `sindri-config.md`. AGENTS.md's command lines, `docs/sindri/SETUP.md`,
`context.md`, `errors.md`, `connectai-live-check.md`, the `sindri-setup.html` page and `scripts/context-first-run.sh`
follow. `scripts/tests/check-profile-doc.test.sh` becomes `check-config-doc.test.sh` and checks `SindriConfig`'s keys.

## 7. Proof

- Unit: write makes `n+1` and activates it; `ifVersion` mismatch is 409; unchanged edit writes nothing; activate an
  old version; activate an invalid one is refused; crash between the version write and the `active` rename leaves
  the old version active (a fake file system that fails the second rename).
- Integration: the migration on a home with `profile/profile.yaml`, run twice; `GET /sindri/config/versions` and
  activate through the API goldens; `EndpointCqrsTests` for the new POST.
- UI: the Versions list and **Use this version** in the Frames tests; Setup without the profile step.
- aot-smoke: `profile/profile.yaml` lines become `config/versions/0001.yaml`; expected.txt re-pinned.
- A repo-wide `git grep -i profile -- src tests docs` returns only the migration code, its tests and this spec.

## 8. Size

About 6k lines in about 1000 files under `src/Sindri` say profile today, plus 4.5k in tests and 3k in docs. Most of
it is a mechanical rename (types, folders, namespaces, error codes, copy). The new code is small: the version
store (§3), the migration (§4), two routes, one event, the Versions list. The rename runs as scripted passes, one
commit per area (Core, Features, Infrastructure, Host, Ui, tests, docs), each building on its own.

## 9. Questions for the owner

1. **Does a save activate at once?** I'd say yes: a save makes a new version and makes it active. The other way
   (save as a draft, then activate) brings back a second step that looks a lot like approval.
2. **Do recipes stop needing approval too?** Setup still has "Approve the workflows" (`sindri recipe approve-all`).
   "Everything is active and available" reads as yes. If so, recipes follow the same model here (each recipe
   version is active once loaded) and the recipes step leaves Setup. I'd fold that in: it's the same rule.
3. **How many versions do we keep?** I'd keep all of them. They're a few KB each, and an old version you can't go
   back to is a surprise. The other option is the last 100.
4. **May an agent session change settings?** Today a session can't run `profile set`. I'd keep that rule for `config
   set`, `edit` and `use`: no approval doesn't have to mean any session can rewrite the settings.
