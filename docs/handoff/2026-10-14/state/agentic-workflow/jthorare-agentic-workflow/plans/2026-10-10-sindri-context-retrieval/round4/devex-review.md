# Plan Devex Review (round 4): Sindri context retrieval engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (1019 lines, revision after round 3)
**Reviewed:** 2026-10-10 · slim · round 3: `../round3/devex-review.md` (PASS)

## Summary

All five round-3 devex fixes are in the spec:

- **Migration file form:** line 857 gives `NNNN_<stage>_<name>.sql`, e.g. `0003_s5a_linear_cursor.sql`. This matches the `EmbeddedMigrations` regex.
- **`linear.url`:** line 484, with the S-L2 answer at lines 660–662 limiting it to Linear's host or loopback.
- **Fixture tokens:** brief item 3 (lines 802–803) uses `env:` and `file:` pointers, and the profile names only the sources its base supports.
- **Escalations:** brief item 5 (lines 810–812) sends them to the lead.
- **Restart resume:** brief item 6 (line 814).
- **AOT-smoke publish and the context reset on renumber:** lines 804–806.

The new owner-check home flow (lines 821–827) has the right idea but doesn't run as written. Checked against S1's contracts in `.claude/worktrees/sindri-s1-app-shell/docs/csharp/sindri.md`, it breaks in three places:

- **No map export command.** The flow seeds prior state with "`map import` of an export", but no verb exports a reviewed map. An import would also re-date every override, which skews S-7B's as-of backtest.
- **The home path.** `$AW_SINDRI_HOME/..` breaks when the variable is unset, which is the owner's normal setup.
- **Unwritten steps.** Copying the profile in doesn't say what else is needed: a second approval in that home, a different port, the copied profile's services starting on approval, and an MCP registration that clashes with the real one.

Time to first success for a stage owner is still about 25 minutes, after S1 and S-FX. For the owner's first owner check, once the steps are written down, it is about 20 minutes:

- about 6 minutes for the AOT publish of the PR;
- about 2 minutes to copy and approve the profile;
- `serve`, then about 5–10 minutes for the check itself.

## Findings by severity

### CRITICAL
None.

### HIGH

- **Backwards compatibility / owner-check flow: "seed by `map import` of an export" isn't runnable, and it would corrupt the S-7B backtest's as-of overrides.**
  - Evidence:
    - Line 826: "A check that needs prior real state (a reviewed map) seeds the scratch home by `map import` of an export". The CLI surface on line 410 is `map import <file> | build | status | review [--export F | --apply F]`, and §8.5 HTTP has no export route.
    - `review --export` (line 462) writes the pending review queue, not the confirmed map, its overrides or its rejections.
    - Phase 3 lists only `ImportMap` (line 928).
    - Line 520–521: "Hand-authored imports count as overrides with their import date". After an import, every override is dated today. The per-sample as-of maps (§9.1, line 520: "only the overrides dated before the cut") would then see no human decisions at any historical cut.
    - Rejections, which stop a rebuild from proposing a node again, aren't carried at all.
    - S-7B (line 743) and S-8's arm decision (line 828) are the owner checks that most need this state, and S-8's go or no-go comes from them.
  - Fix: add a `context map export <file>` verb, with an HTTP route, to S-3 next to `ImportMap`. It writes the confirmed map plus every `map_override` row, keeping its original `when`, `who`, action and rejection identity, outside worktrees, in the same versioned schema.
    - Importing an export restores overrides with their recorded dates. Only a hand-authored file gets the import date.
    - Say so in S-L4 and line 826, and add an integration test: export, import into an empty home, then the as-of map at a past cut is the same.
    - Alternative: state that S-7B and S-8's work-data runs happen in the real home after their PRs merge, and keep owner checks limited to stages that need no prior state.

### MEDIUM

- **Setup / owner-check flow: the home path breaks in the default setup, and reuse across stages fails on migrations.**
  - Evidence: line 823: "(`$AW_SINDRI_HOME/../sindri-app-ownercheck/`)". S1 `sindri.md` line 40: `AW_SINDRI_HOME`, default `$AW_STATE_DIR/sindri-app`. The owner normally leaves it unset, so the expression becomes `/../sindri-app-ownercheck/`, which is unwritable.
    - The path is fixed and shared by every stage's owner check.
    - Stage A's unmerged migration `0004_s4_…` would stay in `context.db`.
    - Stage B's check, numbered `0004_s5b_…`, then fails with `Kernel.Data`'s "Migration N was applied as … add a new migration instead".
    - "An empty `context.db`" doesn't say who empties it.
  - Fix: write the path as `${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app-ownercheck` and have `owner-check.md` export it as `AW_SINDRI_HOME`. Add a step: "`rm -rf` the owner-check home's `context/` before every check, and the whole home after it." It holds copies of work text.

- **Setup / owner-check flow: "real profile copied in" leaves out four steps the owner will hit.**
  - Evidence: line 823: "a scratch home with the real profile copied in". The S1 contracts say:
    1. The runtime reads only `approved/<hash>/profile.json` in that home (`sindri.md` line 46), and `profile approve` needs the control plane running there (lines 35–36). So the copy needs `serve` and a typed approval in the scratch home.
    2. The copied profile carries the real `api.port`, default 7410 (line 65). If the owner's real Sindri is running, `serve` exits 2 with `SND-API-003`.
    3. Once approved, the control plane runs every service the profile names (line 126). That includes Linear sync, the scheduled map rebuild and labeling through the `claude` CLI under its own `maxTokens` budget, so model spend for the check is double the real home's.
    4. The MCP parts of a check, "a Claude Code session in the work repo", S-1M and S-3M at line 271, need an MCP registration pointing at the scratch port. A `--scope local` add under the same server name as the owner's real registration is refused or replaces it.
  - Fix: have the spec's owner-check template take these steps:
    1. `AW_SINDRI_PORT=7490`.
    2. Copy `profile/profile.yaml` only, then run `$SINDRI serve` in the foreground.
    3. `profile approve <hash>`.
    4. Set `context.labeling.enabled: false` in the copy unless the check is about labeling.
    5. Register MCP as `sindri-ownercheck` and run `claude mcp remove sindri-ownercheck` afterwards.

### LOW

- **Docs: `owner-check.md` has no fixed skeleton.**
  - Evidence: line 808 says "write `owner-check.md` with the exact steps and what counts as a pass". Line 822 says "runs the PR's build", but nowhere says how the owner gets that build.
  - Fix: give a short template in "Work data stays with the owner":
    - build: in the stage worktree, `src/Sindri/aot-smoke/run.sh`, then `SINDRI=<path>`;
    - the environment exports from the MEDIUM above;
    - reset;
    - the steps, with the pass line as counts;
    - cleanup.

  The lead can then check each owner's file against it.

- **API: `linear.url` appears only as a YAML comment.**
  - Evidence: line 484: `# url: Linear's API host (or loopback, tests)`. "Defaults are what's shown" (line 487), so the default value isn't actually shown.
  - Fix: write `url: "https://api.linear.app/graphql"` in the example.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | LOW | 1 (`linear.url` default not shown) |
| Setup / install (time-to-first-success: ~25 min stage owner; ~20 min owner check once written) | MEDIUM | 2 (home path/reset; profile copy steps) |
| Error messages & observability | — | 0 |
| Docs completeness | LOW | 1 (`owner-check.md` template) |
| Backwards compatibility / migration | HIGH | 1 (no map export; override dates lost) |
| Ramp-up cost (tribal-knowledge items: 5) | MEDIUM | (covered above) |

**Tribal-knowledge list:**

1. `AW_SINDRI_HOME` is normally unset, and defaults to `$AW_STATE_DIR/sindri-app`.
2. Approvals live per home. A copied profile needs its own `serve` and typed approval in the scratch home.
3. The copied profile keeps the real `api.port`, so set `AW_SINDRI_PORT` when the real Sindri is running.
4. Approving a profile starts its services right away, including sync, rebuild and labeling spend.
5. An owner-check MCP registration must use its own server name, and be removed afterwards.

## Verdict

NEEDS_WORK. Every round-3 MEDIUM landed, but the new owner-check flow seeds prior state with a map export that doesn't exist. An import would also re-date every override, which undermines S-7B and S-8's as-of backtest. Two MEDIUMs, the home path and the steps the profile copy needs, mean the owner can't run a check from the spec as written.

verdict: NEEDS_WORK
