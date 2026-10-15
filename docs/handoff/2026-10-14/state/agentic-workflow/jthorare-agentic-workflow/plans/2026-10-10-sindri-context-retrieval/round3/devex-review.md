# Plan Devex Review (round 3, final): Sindri context retrieval engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (978 lines, revision after round 2)
**Reviewed:** 2026-10-10 · slim · round 2: `../round2/devex-review.md`

## Summary

Both round-2 HIGHs are fixed.

- **Migrations (R2-1, Shared files line 819):** migrations are now named rather than pre-numbered. Each is numbered on its own branch and renumbered on rebase. That fits `Aw.Kernel.Data`'s rule of versions 1..n with no gaps.
- **Isolation (R2-2, brief item 3, lines 775–779):** each stage gets its own `AW_SINDRI_HOME` and `AW_SINDRI_PORT`, a fixture profile and `expect` approval.

`AW_SINDRI_PORT` is a real S1 variable, so the brief needs no other mechanism. It is `LoopbackBinding.PortVariable` and overrides the profile's `api.port`; `0` picks a free port. It is in `.env.example` line 29, and `SND-API-003`'s fix message names it. The S1 hand test used `AW_SINDRI_PORT=7491` itself.

Two more S1 facts make per-home isolation safe:

- The host lease is a row in `<home>/ledger.db`, so each throwaway home runs its own control plane.
- Clients find the server through `<home>/control.json`.

The stage-owner brief covers the epic defaults that matter in `/specToProvenPR`:

- steps 0 and 0.5;
- the base branch;
- `verify-app` and `/ui-evidence` for CLI, HTTP and MCP stages, matching S1's evidence precedent with no `pack.json`;
- stopping after step 9.

The round-2 MEDIUMs are resolved:

- the owner rebases and redoes the step 9 re-check;
- the lead is the only writer of `stages.md`;
- S-2W is its own stage with a "merges before" edge;
- owner checks have count-only records;
- `mcp.enabled` defaults to false.

Three new MEDIUMs remain, each a small doc fix:

- the spec's migration file name doesn't match the loader's regex;
- the Linear adapter has no setting to point it at the fake server;
- the brief doesn't send the skill's "ask the user" points to the lead.

Time to first success for a stage owner is about 25 minutes, after S1 merges and S-FX exists. That covers the AOT publish through `aot-smoke/run.sh`, writing the fixture profile, `serve`, approval with `expect`, and the first `context query`.

## Findings by severity

### CRITICAL
None.

### HIGH
None.

### MEDIUM

- **Parallel delivery / migrations: the spec's migration file name fails the migration loader.**
  - Evidence: line 819: "A stage's migration file is `NNN_<stage>_<name>.sql`". Stage ids hold hyphens (`S-5A`, `S-1A`, lines 705–722). `src/Aw.Kernel.Data/EmbeddedMigrations.cs` accepts only `^(?<version>[0-9]{4})_(?<name>[A-Za-z0-9_]+)\.sql$`, which means four digits and no hyphen. Anything else returns `AW-DATA-005` ("is not named NNNN_name.sql").
  - Fix: write `NNNN_<stage>_<name>.sql` with the stage written as `s5a`, for example `0003_s5a_linear_sync.sql`. Note that the name is part of a migration's recorded identity (`schema_version.name`), so it freezes at merge along with the number.

- **Hand-test isolation: the Linear adapter can't be pointed at the fake Linear server.**
  - Evidence: brief item 3 (line 777) says the profile uses "the fake Linear and Onyx servers". §8.8 line 480 gives `linear: { token, teams }` with no URL, while `onyx` has `url`. §14 S-L2 (line 655): "the only outbound calls are to Linear's API host". S1's profile refuses unknown keys. So the S-5A, S-5B, S-6 and S-CF owners must each invent the override, or hand-test against the real Linear host, which the brief forbids.
  - Fix: add `linear.url` to §8.8, defaulting to `https://api.linear.app/graphql` and accepting only that host or a loopback URL (validated at `profile validate`). Amend S-L2 to say so. Also allow `env:` or `file:` pointers for the fake token in brief item 3 (§4 line 129 already lists them), so hand tests never write to the owner's login keychain. Add one clause saying the fixture profile carries only the source keys the stage's base already supports, because `linear` and `onyx` don't exist before S-5A and S-6.

- **Stage-owner brief: the skill's "ask the user" points aren't sent to the lead.**
  - Evidence: `/specToProvenPR` SKILL.md step 8: "After 5 iterations with a blocking finding still open: stop … ask the user". Step 8 also says to return to step 3 when a finding exposes wrong behaviour. Step 9 says to "update stage status" in `stages.md`. The brief (lines 770–783) overrides steps 0, 0.5, 5, 6 and the stop after step 9, but not these. The owner row (line 762) says owners never write `stages.md`. A background owner that runs `AskUserQuestion`, or edits `stages.md`, either stalls or breaks the single-writer rule.
  - Fix: add a brief item. "Never ask the user. Every escalation goes to the lead by message: the cap-5 oscillation, a scope change, a spec ambiguity or a behaviour-wrong finding. Wherever the skill says to update `stages.md`, send the lead the new state instead." The lead decides and relays.

### LOW

- **Brief item 3: say how to build `$SINDRI` and what to do after a renumbering rebase.**
  - Evidence: line 778: "`$SINDRI` is the stage's published binary, rebuilt after each rebase". A direct `dotnet publish -r <rid>` in the worktree changes `packages.lock.json` (csharp rule, "AOT scope"). S1 published through `src/Sindri/aot-smoke/run.sh`, which publishes on a copy (`proof/sindri-s1-app-shell/manual-test.md` line 3). After a rebase renumbers a migration, the old home's `context.db` fails with "Migration N was applied as … add a new migration instead", and that fix is wrong in this case.
  - Fix: "build it with `src/Sindri/aot-smoke/run.sh` and copy the binary to `$STAGE_DIR/hand/`, or use `dotnet build` and the `bin/` apphost. After a rebase that renumbers a migration, delete `home-<id>/context/`."

- **Phase 2 text is stale after S-2W.**
  - Evidence: line 876: "the `Aw.Context` wiring rides in S-1A, or in a one-line follow-up if S-2R merges after S-1A". This contradicts S-2W (line 710).
  - Fix: "the wiring is stage S-2W".

- **Owner restart has no resume source.**
  - Evidence: line 837: "`stages.md` is the single record, so … resume mode works for the lead after a restart". There's nothing for a restarted stage owner.
  - Fix: one line saying a replacement owner resumes from `$STAGE_DIR`'s artifacts and the step recorded in the lead's `stages.md` row.

- **Carry-over round-2 LOWs that §14 doesn't answer.** These are non-blocking, so take them or leave them:
  - name the `Aw.Context.*` spans and metrics (§7 line 322 covers only content);
  - say that exit 75 from the heavy lock is a wait, not a failure;
  - name the teammate mechanism and confirm that owners can nest implementers and `/review`'s fan-out;
  - add one "Docs:" line for Phases 3, 5 and 7.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | MEDIUM | 1 (`linear.url`) |
| Setup / install (time-to-first-success: ~25 min per stage owner, after S1 + S-FX) | LOW | 1 |
| Error messages & observability | LOW | 1 (carry-over) |
| Docs completeness | LOW | 1 |
| Backwards compatibility / migration | MEDIUM | 1 (file-name regex) |
| Ramp-up cost and parallel delivery (tribal-knowledge items: 4) | MEDIUM | 2 |

**Tribal-knowledge list:**

1. Migration resource names must match `NNNN_[A-Za-z0-9_]+.sql`.
2. Publish `$SINDRI` through `src/Sindri/aot-smoke/run.sh` (on a copy), never with `dotnet publish` in the worktree.
3. `profile approve` needs the control plane running in the same home. It's an API client fenced by the lease.
4. S1 isn't merged. Its contracts are in `.claude/worktrees/sindri-s1-app-shell/docs/csharp/sindri.md`.

## Verdict

PASS. Both round-2 HIGHs are resolved:

- named migrations renumbered on rebase;
- per-stage `AW_SINDRI_HOME` and `AW_SINDRI_PORT` (a real S1 variable), a fixture profile and `expect` approval.

The brief makes the team runnable with `/specToProvenPR` as it is today. What remains are three MEDIUM doc fixes: the migration file-name pattern, a `linear.url` override for the fake server, and sending the skill's ask-user points to the lead.

verdict: PASS
