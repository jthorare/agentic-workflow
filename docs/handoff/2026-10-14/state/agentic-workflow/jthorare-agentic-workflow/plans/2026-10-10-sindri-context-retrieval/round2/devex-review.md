# Plan Devex Review (round 2): Sindri context retrieval engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (917 lines, revised after `/autoplan`)
**Reviewed:** 2026-10-10 · slim · round 1: `../devex-review.md`

## Summary

All six round-1 HIGHs are fixed in the spec:

- **D-1 (running the C# build):** §3 lines 84–87 define `$SINDRI`, and every phase signal uses it.
- **D-2 (state directory):** §3 line 79 and §6.2 line 263 put the state under `$AW_SINDRI_HOME/context/`.
- **D-3 (error catalogue):** §8.6 is the `SND-CONTEXT-*` catalogue with exit codes, and it matches S1's `SND-<AREA>-NNN` form.
- **D-4 (keys in URL paths):** §8.5 line 402 moves keys into query strings and bodies.
- **D-5 (profile example):** §8.8 gives an annotated `context:` profile with defaults.
- **D-6 (Onyx and Linear setup):** §8.8 has the Linear key steps and the Onyx runbook, and `docs/sindri/onyx.md` is a Phase 6 deliverable.

Most round-1 MEDIUMs are fixed too: the MCP argument schemas, `structuredContent` without the duplicate text, `schemaVersion` and `queryId`, the fixture app, and the TS `scope` relation.

The new "Parallel delivery with an agent team" section can be run with `/specToProvenPR` as it exists today, but not as written. Two things break it:

- **Migration numbers.** The reserved numbers assume a merge order the dependency graph doesn't impose. `Aw.Kernel.Data`'s runner refuses gaps, so "parallel" stages can't merge independently.
- **Hand-test isolation.** The section never says that each stage owner hand-tests in its own `AW_SINDRI_HOME` and port, with an agent-run approval. Without that, parallel owners collide on the host lease and port 7410, or run against the owner's real ring-1 home.

Time to first success (Phase 1 signal, once S1 has merged and `docs/sindri/context.md` exists): about 25 minutes (build, write the profile, approve, start, query), plus about 5 minutes for the `--scope local` MCP registration and a session check.

## Findings by severity

### CRITICAL
None.

### HIGH

- **Ramp-up / parallel delivery: the reserved migration numbers can't merge in the order the waves allow.**
  - Evidence: line 757: "S-1A `001`, S-3 `002`, S-5A `003`, S-5B `004`, S-4 `005`, S-7A `006`, S-8 `007`. A migration is tested on the schema of every migration below its number". `src/Aw.Kernel.Data/MigrationChecks.cs` `CheckSequence` says: "versions must run 1, 2, 3, … with no gaps, duplicates or reordering".
    - S-5A (003) runs beside S-3 (002) in wave 2, and the two don't depend on each other (lines 680–681).
    - S-4 (005) runs beside S-5B (004) in wave 3. S-7A (006) depends only on S-3.
    - So S-5A can't pass its own migration tests or merge before S-3. S-4 can't merge before S-5B, and S-7A can't merge before S-4. The "tested on every migration below its number" rule needs migrations that aren't in the stage's branch.
  - Fix: stop reserving numbers. A stage writes its migration as "next" and takes the next free number when the lead rebases it onto `main` before ready. Checksums only bind throwaway hand-test homes. Otherwise, add the hidden edges to the dependency table and the mermaid graph (S-5A after S-3, S-4 after S-5B, S-7A after S-4), and accept the lost parallelism. Either way, state that the per-stage hand-test database is recreated after a renumber.

- **Setup / parallel delivery: the section doesn't say where each stage owner runs `$SINDRI` for its hand test.**
  - Evidence: the parallel section (lines 660–777) is absent on this; searched "AW_SINDRI_HOME", "port", "isolated", "approve". S1's control plane takes one host lease and the port 7410 (S1 `docs/csharp/sindri.md`: `SND-LEASE-001`, `SND-API-003`), and its default home is `$AW_STATE_DIR/sindri-app`. That default is the owner's real ring-1 home, with Linear and Onyx keychain pointers (§8.8). `profile approve <hash>` needs an interactive terminal (`SND-PROFILE-004`). Line 767 lists "`profile approve` at a terminal for each new `context:` key" as an owner step, which suggests agents must wait for the owner. S1's own hand test shows the working pattern: `AW_SINDRI_HOME=hand/run/home`, `AW_SINDRI_PORT=7491`, and approval driven by `expect` at a pty (`proof/sindri-s1-app-shell/manual-test.md` lines 4 and 10). It isn't written down anywhere.
  - Fix: add a "Hand-test environment" rule to the parallel section. Each stage owner sets its own `AW_SINDRI_HOME` under `$STAGE_DIR` and its own port (the lead assigns one, for example 7491 + stage index), and points the profile at the fixture app repo. It approves the profile with `expect` in that home. Hand tests never use the default home or work data. Then say that the owner-run `profile approve` at line 767 applies only to the owner's real profile, in S-6 and S-7B. Add a doctor or startup warning when a stage worktree runs `$SINDRI` against the default home.

### MEDIUM

- **Parallel delivery: no brief template, although the brief is how stage owners get this behaviour today.**
  - Evidence: lines 727–729: "Until `/specToProvenPR` takes a stage id … that instruction is spelled out in the teammate's brief". The current skill has several defaults the brief must override, and the spec doesn't list them:
    - step 0 creates one worktree per epic, and step 0.5 asks the autonomy question;
    - step 9 says "after the human merges, start the next stage at step 1";
    - the git topology contract says "each stage gets its own branch off base", which conflicts with stacking at line 738;
    - step 5's `verify-app` handles web and iOS only, and "both/neither → **Ask the user**" stalls a background owner on the CLI, HTTP and MCP stages;
    - resume mode "continue[s] from the first incomplete step" of the epic, not of a given stage.
  - Fix: add a brief template of about 10 lines. It holds the spec path, the stage row, the `stages.md` path, the branch, base and remote, the reservations, the hand-test home and port, and these overrides: skip steps 0 and 0.5; never ask the user, report to the lead; base on the assigned branch (stacking overrides the topology line); stop at step 9 without starting another stage; and for non-web stages, write the evidence pack by hand under `verification/<date>-<stage-slug>/` (the R2 practice, as in `proof/r2-ev-evidence/evidence.md`), with a recorded "verify-app n/a: CLI/HTTP/MCP stage" line.

- **Parallel delivery: a rebase after "ready" makes the proof and review stale, and the lead is barred from resolving it.**
  - Evidence: line 739: "When the dependency merges, the lead rebases the stack onto `main` and retargets the PR". Line 720: the lead never writes "product code or tests". `/specToProvenPR` step 9 re-runs the journey when "fixes changed non-test source" after proof.
  - Fix: the lead rebases only when the rebase is clean. On a conflict, it hands the rebase back to the stage owner. After any rebase that changes the stage's diff, the owner runs a step 9 re-check (the journey and gates, plus one `/review` run when non-test source changed) before the PR stays ready.

- **Parallel delivery: `stages.md` has many writers and no ownership rule.**
  - Evidence: line 720 says the lead writes each stage's state. Line 721 says owners "report to the lead at each step change". Line 776 says "`stages.md` is the single record, so … resume mode works … for any stage owner after a restart".
  - Fix: the lead is the only writer of `stages.md`. Each owner keeps its step state in `$STAGE_DIR/state.md`, which is the file resume reads, and the lead folds those into `stages.md`.

- **Parallel delivery: the S-2R `Aw.Context` wiring has no stage.**
  - Evidence: line 814: the wiring "rides in S-1A, or in a one-line follow-up if S-2R merges after S-1A". S-5A depends on S-2R alone (line 681). P-L2 (line 655) requires identifier scrubbing before any intent source lands.
  - Fix: give the wiring a stage id (S-2W: S-1A plus S-2R, XS) and make S-5A depend on S-2W, not S-2R. Then the "before any intent source" promise is checked by the graph.

- **Parallel delivery: agents can't produce some phase signals.**
  - Evidence: line 804: "in a Claude Code session in the work repo with Sindri's MCP added (`--scope local`)". Line 556 and §10: "Tests never touch work data". Line 767 lists the owner's steps, and the Phase 1 work-repo session isn't among them.
  - Fix: split each signal into an agent part (the fixture repo; MCP through `claude -p --mcp-config <stage port>` or the MCP SDK client) and an owner part (a work-repo session, after merge). Add the owner parts to "Gates that stay sequential".

- **API surface: the `context.mcp.enabled` default contradicts §3.**
  - Evidence: §3 lines 76–78: the egress switches, `context.mcp.enabled` among them, are "all off until approved". §8.8 line 469 shows `mcp: { enabled: true }`, and line 476 says "Defaults are what's shown".
  - Fix: show `enabled: false` with a comment saying to turn it on in the work profile, or drop it from the "all off" list. Implementers will otherwise pick a default at random.

### LOW

- **Setup:** with `$SINDRI` set to `dotnet run --project …`, every call rebuilds, and a rebuild can run while a detached `serve` is executing the same apphost. Recommend `dotnet build` once, then `SINDRI=<…>/bin/Debug/net10.0/sindri` (or `dotnet run --no-build`) in `docs/sindri/context.md`.
  - Evidence: line 85.
  - Fix: as stated.

- **Parallel delivery:** the cap of 3 building stages against 2 heavy-lock slots means one owner's integration or heavy lane waits with exit 75. The full local gate (R2 `gate.sh` runs take many minutes) multiplies that. Say that a 75 is a wait, not a failure, and that the gate retries.
  - Evidence: lines 733–735.
  - Fix: as stated.

- **Parallel delivery:** "named teammates … with `isolation: worktree`" (line 725) doesn't name the mechanism (Claude Code agent teams, or background Agent subagents with `SendMessage`). Each owner runs subagents (implementers, `/review`'s fan-out) one level below itself. Name the mechanism, and confirm it allows that nesting and messaging from owner to lead.
  - Evidence: lines 722–726.
  - Fix: as stated.

- **Observability (carry-over):** span and metric names under `Aw.Context.*` are still unnamed. §7 line 319 covers only what logs may carry.
  - Evidence: absent; searched "span", "Meter", "metric".
  - Fix: one line in §7 naming them: per-source latency, deadline misses, degraded count, truncation.

- **Docs (carry-over):** later phases don't list their docs. Phases 3, 5 and 7 don't name the map YAML format, the Linear section or the eval and probe-file how-to. Line 761's "one section per stage" covers this only by implication.
  - Evidence: lines 825–829 and 879–881.
  - Fix: add one "Docs:" word per phase.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | MEDIUM | 1 |
| Setup / install (time-to-first-success: ~25 min + ~5 min MCP, after S1 merges) | HIGH | 2 |
| Error messages & observability | LOW | 1 |
| Docs completeness | LOW | 1 |
| Backwards compatibility | None | 0 |
| Ramp-up cost and parallel delivery (tribal-knowledge items: 6) | HIGH | 7 |

**Tribal-knowledge list:**

1. Agent hand tests approve profiles with `expect` at a pty in an isolated `AW_SINDRI_HOME` and `AW_SINDRI_PORT` (S1's `manual-test.md`).
2. `verify-app` covers web and iOS only. CLI and HTTP stages hand-build `pack.json` under `verification/<date>-<slug>/`, as the R2 stages did.
3. `MigrationRunner` refuses migration gaps, so a migration number is a merge-order constraint.
4. The R2 `gate.sh` pattern (per-gate logs in `$STAGE_DIR`) is how GREEN is shown locally.
5. S1 isn't merged. Its contracts live in `.claude/worktrees/sindri-s1-app-shell/docs/csharp/sindri.md`.
6. Which teammate mechanism the lead uses, and how an owner messages the lead.

## Verdict

The round-1 HIGHs are resolved. The verdict is still NEEDS_WORK because of two new HIGHs in the parallel-delivery section: the reserved migration numbers conflict with the gap-refusing `MigrationRunner` and the wave graph, and the section doesn't isolate each stage owner's hand-test home, port and approval. Both are doc-level fixes that don't reopen any owner decision.
verdict: NEEDS_WORK
