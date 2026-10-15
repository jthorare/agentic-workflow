# Plan Devex Review (round 5, final): Sindri context retrieval engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (1069 lines, revision after round 4)
**Reviewed:** 2026-10-10 · slim · round 4: `../round4/devex-review.md` (NEEDS_WORK)

## Summary

The round-4 fixes are in the spec:

- **R4-D:** `context map export` is a CLI verb and an HTTP route (lines 413, 415). Phase 3 (line 978) says an export keeps original timestamps and provenance, that import restores them, and that a round-trip test covers it.
- **R4-X:** the owner-check outline (lines 862–873) covers a fresh per-stage home under `AW_STATE_DIR`, port 7480, an edited and approved profile, the `sindri-ownercheck` MCP name and teardown.

I checked the outline step by step against S1's real contracts in `.claude/worktrees/sindri-s1-app-shell/docs/csharp/sindri.md` and `src/Sindri/aot-smoke/run.sh`. Most of it runs. Four places still need a decision the owner would otherwise make while running the check:

- **Seed step:** the export from the real home runs in a shell where `AW_SINDRI_HOME` already points at the scratch home.
- **Services:** no profile key turns off "scheduled services", and "labeling off" blocks S-5B's decisions check.
- **MCP:** a session in the work repo sees both the real `sindri` server and `sindri-ownercheck`, which have the same tool names.
- **Import across builds:** nothing says the PR build reads an export written by main's older build.

None of these blocks the work, and each has a one-line fix.

Time to first success:

- **Stage owner:** about 25 minutes after S1 and S-FX, unchanged.
- **The owner's first owner check:** about 22 minutes:
  - about 6 minutes for `run.sh`, which also runs the S1 smoke;
  - about 3 minutes to copy, edit, start and approve the profile;
  - about 2 minutes to export and import;
  - 5–10 minutes for the check;
  - about 1 minute for teardown.

## Findings by severity

### CRITICAL
None.

### HIGH
None.

### MEDIUM

- **Setup / owner-check outline: step 4 exports from the wrong home.**
  - Evidence:
    - Line 864, step 2, sets `AW_SINDRI_HOME=…/sindri-app-ownercheck-<stage>` and `AW_SINDRI_PORT=7480`.
    - Line 868, step 4, then says: "`$SINDRI context map import` of a `context map export` taken from the real home".
    - S1 `sindri.md` lines 32–35 and 45: CLI verbs are API clients that find the process through `<AW_SINDRI_HOME>/control.json`.
    - So a `context map export` run in the check's shell talks to the scratch instance, which has no map yet, and fails with `SND-CONTEXT-030`. It never reaches the real one. The outline also doesn't say which binary takes the export (the real home's main build or the PR build), or that the real instance must be running.
  - Fix: reorder step 4 to come before step 2, and write it out:

    ```
    env -u AW_SINDRI_HOME -u AW_SINDRI_PORT <real sindri> context map export <scratch-home>/seed-map.yaml
    ```

    The real instance must be running. Writing the export into the scratch home means teardown deletes it, and it holds work labels and decision quotes. Today the export would stay under the real home's `context/` (line 80).

- **API / profile: "scheduled services off" has no profile key, and "labeling off" contradicts S-5B's check.**
  - Evidence:
    - Line 866: "edited per the template: labeling off, scheduled services off except the ones the check names".
    - S1 turns services on and off only through the `services:` map (`sindri.md` lines 62–63, 126: "runs the services the approved profile names").
    - The context engine's services have no key in §8.8 (lines 486–496) or anywhere else. These are the repo-docs refresh "every 5 min by default" (line 199), Linear sync, the scheduled rebuild and purge (lines 594, 997, 1008). Searched for "services", "every", "cadence" and "scheduled". So the owner can't do what step 3 asks.
    - Separately, decision extraction sits behind `context.labeling.enabled` (lines 76, 1009). With labeling unconditionally off, S-5B's signal ("its decisions quoting their source", line 1015) can't be seen in an owner check.
    - In a fresh home the first sync covers all three projects, and `maxTokensPerSync` (50k) queues the rest. So the check would see only part of the decisions unless the import carries them.
  - Fix:
    - Register the engine's services under S1's `services:` map with fixed names (`context-repo-docs`, `context-linear-sync`, `context-map-rebuild`, `context-purge`), each with a cadence. A service left out doesn't run. Show them in the §8.8 example.
    - Change step 3 to "labeling off unless the check names it".
    - State whether `ExportMap` carries the `decision` table's rows. If it does, S-5B's check needs no re-extraction.

- **Setup / owner-check outline: an MCP check can call the real server instead of the PR's.**
  - Evidence:
    - Line 870: register `sindri-ownercheck` (`--scope local`, port 7480) in the work repo.
    - Line 91: the owner's real `sindri` server is also registered `--scope local` in that same repo.
    - Both expose `context_query`, `context_node` and the other tools under the same names. A session in that repo gets both sets, and the model picks one. A pass can come from main's build on the real `context.db`, which is the thing the check exists to avoid.
  - Fix: in step 5, start the session with only the owner-check server:

    ```
    claude --strict-mcp-config --mcp-config <scratch-home>/ownercheck-mcp.json
    ```

    The file names `sindri-ownercheck` at `http://127.0.0.1:7480/mcp`. Nothing is registered, so teardown has nothing to remove. The alternative is to remove the real `sindri` registration for the length of the check and re-add it at teardown.
  - Also: a copied profile with `context.mcp.enabled: false` (the default, line 494) answers 403 `SND-CONTEXT-020`. The template should say to set it to true when the check uses a session.

- **Backwards compatibility: an export from main must import into the PR build.**
  - Evidence:
    - Line 978: "`ImportMap` and `ExportMap` (versioned schema, all-or-nothing …; round-trip test)". The round-trip is within one build.
    - In an owner check, the export comes from the real home's merged build, and the import runs on the PR build, which may have changed things:
      - S-4 adds `CodeUnit` nodes and `implements` edges;
      - S-5B adds `Record` and `Decision` content;
      - a PR may bump `EntryKey()`'s version (§6.1 key migration).
    - Nothing says whether import accepts an older export `schemaVersion`, or whether older keys go through the key migration's aliases.
    - Nothing says which map-version status an import creates. The Phase 3 signal (line 985) queries right after an import, which suggests `current`, but §8.7 says new maps start as `draft` (lines 466–468).
    - Nothing says how import tells an export (original dates) from a hand-authored file (import date, line 527).
  - Fix: state in Phase 3 and S-L4 that:
    - import reads every earlier export `schemaVersion`;
    - keys from an older `EntryKey` version are resolved through the key migration's aliases;
    - an import into an empty home becomes `current`, and an import into a home that already has a map is refused;
    - the file's `kind: export | authored` field decides how overrides are dated.

    Add an integration test that imports the previous release's golden export file.

### LOW

- **Setup: `run.sh` can't publish "into a scratch dir".**
  - Evidence:
    - Line 863: "Build the PR head with `src/Sindri/aot-smoke/run.sh` into a scratch dir."
    - S1's `run.sh` takes no output argument. It publishes to `<repo>/artifacts/aot-sindri-smoke/bin/` and fails the whole run when the smoke diff fails.
    - The binary needs the bundled `e_sqlite3` library that sits next to it, so copying `sindri` alone breaks it.
  - Fix: "run `run.sh` in the PR worktree, then `cp -R artifacts/aot-sindri-smoke/bin <scratch>/bin` and `SINDRI=<scratch>/bin/sindri`." Or add an `--out` argument in S-1A.

- **Setup: approval needs a running process.**
  - Evidence: line 866 says "approved in the scratch home at a terminal". S1 `sindri.md` line 35: `profile approve` "needs it running".
  - Fix: write step 3 as:
    1. `$SINDRI --headless` (starts the process detached and exits 1 for "needs approval");
    2. `$SINDRI profile approve` (preview);
    3. `$SINDRI profile approve <hash>`.

- **Setup: port 7480 is shared by every stage's owner check.**
  - Evidence: line 865. Up to 4 PRs can be ready at once (line 889), and two checks at the same time would collide (`SND-API-003`, exit 2).
  - Fix: say "one owner check at a time", or use port 7480 plus the stage's index.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | MEDIUM | 1 (no service keys; labeling vs S-5B) |
| Setup / install (time-to-first-success: ~25 min stage owner; ~22 min owner check) | MEDIUM | 5 (export targets the scratch home; MCP tool clash; `run.sh` output; approval needs a running process; shared port) |
| Error messages & observability | — | 0 |
| Docs completeness | — | 0 (the outline exists; its gaps are listed under setup) |
| Backwards compatibility / migration | MEDIUM | 1 (importing an export across builds and versions) |
| Ramp-up cost (tribal-knowledge items: 5) | MEDIUM | (covered above) |

**Tribal-knowledge list:**

1. CLI verbs find their server through `$AW_SINDRI_HOME/control.json`, so exporting from the real home needs `AW_SINDRI_HOME` and `AW_SINDRI_PORT` unset, and the real instance running.
2. `profile approve` needs the control plane running in that home. Start it with `--headless` first.
3. `run.sh` writes to `artifacts/aot-sindri-smoke/bin/` inside the worktree. Copy the whole directory, because `e_sqlite3` sits next to the binary.
4. Two servers registered in the same repo with the same tool names both reach the session. Use `--strict-mcp-config` for an MCP owner check.
5. The real home must be rebuilt from `main` after each merge, so its export's schema version matches what the next PR expects.

## Verdict

The round-4 HIGH (the missing map export) and both MEDIUMs are fixed, and nothing here is CRITICAL or HIGH. Four MEDIUMs are left, each with a one-line fix:

- the seed-step export runs against the scratch home;
- no profile key turns the engine's services off, and "labeling off" blocks S-5B's check;
- an MCP check sees the real server's tools;
- importing an export across builds and versions is unstated.

verdict: PASS
