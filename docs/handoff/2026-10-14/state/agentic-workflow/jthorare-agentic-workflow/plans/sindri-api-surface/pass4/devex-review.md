# Plan Devex Review — sindri-api-surface (pass 4, final edit)

**Plan:** docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md
**Reviewed:** 2026-10-10
**Scope:** I checked each pass-3 finding against the final edit (§13 "Pass 3 → final edit"), `scripts/scaffold/new-slice.mjs` with `lib/slice-templates.mjs`, and the S1 worktree `.claude/worktrees/sindri-s1-app-shell` (5c9c4a4, read only). This report lists only findings that are still open, or that the final edit introduced. Owner decisions are not re-raised, including the accepted gap in `CODEX_SANDBOX` and `CURSOR_AGENT` detection.

## Summary

Both pass-3 MEDIUM findings are fixed as written:
- §8 puts the prompt first and passes the tools as one comma-separated argument.
- §12 step 1 has `--slice-only` declare the unit's own PublicAPI lines.

Three pass-3 LOW findings are also resolved: a refused `stop` is audited, the docs list names `mcp-servers.md` and `API_CONTRACT.md`, and the CLI always sends `X-Sindri-Repo`.

Two of the fixes create new MEDIUM findings:
1. **`--slice-only` still can't produce a red build.** The generated red test calls `A.<Slice>Request()`, but the `A.cs` factory line is now a hand edit in step 2.
2. **Session repo scoping breaks P3's signal.** The repo name taken from a worktree is the worktree folder's name, which is wrong for every agent.

The C2 wording is still not fixed (LOW).

Time to first success: ~10–15 min. It rises to ~20 min the first time an implementer hits either MEDIUM.

## Findings by severity

### CRITICAL
None.

### HIGH
None.

### MEDIUM

- **Ramp-up / scaffolding (new, from the PublicAPI fix):** `--slice-only` as specified can't pass its own build-and-red step.
  - Today's `writeSlice` writes a red test that calls `var request = A.${slice}Request().Build();` (`slice-templates.mjs` `redTest`). It also inserts that factory into `tests/Aw.<Pkg>.Testing/A.cs` (`new-slice.mjs:212`).
  - The spec calls `A.cs` a shared file: §12 step 2 lists "the `A.cs` builder method" as a hand edit, and "Shared files" lists `tests/Aw.Sindri.Testing/A.cs`. Step 1 says `--slice-only` "touches no other shared file".
  - So in `--slice-only` mode the red test doesn't compile. `build` fails before `expectOnlyRed`, and the journal reverts every file it wrote.
  - The same applies, more mildly, to `tests/.../Ids/IdsJsonContext.cs`, which gets an insert for every new id. It isn't on either list.
  - P0's signal makes the problem visible: "only slice files and its own PublicAPI lines change" can't hold while the red test needs `A.cs`.
  - Evidence: §12 "Scaffolding a slice" steps 1–2; "Shared files" list; Phases P0 row; `new-slice.mjs:207-213`; `slice-templates.mjs` `redTest`.
  - Fix (choose one, and say which in §12 and the P0 signal):
    - (a) `--slice-only` also writes the unit's own `A.cs` factory line and the `IdsJsonContext` line, the same way it writes its PublicAPI lines. Move `A.cs` from step 2 into step 1.
    - (b) In `--slice-only` mode, the red test uses `new <Slice>RequestBuilder()`, and the implementer switches it to `A.` in step 2.

- **API / ramp-up — repo scoping (new, from "the CLI always sends `X-Sindri-Repo`"):**
  1. **P3's signal compares two different scopes.**
     - `sindri-prove.sh human item add "Try the queue"` passes no `--repo`. §3.2 gives `CreateItem` a body `repo?` and doesn't say whether a missing body repo defaults to the header.
     - The prover then runs `$SINDRI queue --json` directly inside Claude Code. That call is a `session` caller and carries `X-Sindri-Repo`.
     - The human `curl` sees every item. The session CLI sees only items in its repo, and §3.1 only defines what a request *without* the header sees.
     - Unless `CreateItem` stamps the header repo, the two bodies differ and the signal fails.
  2. **The repo name is wrong in a worktree.**
     - §8 derives the repo name from "its git top-level name". In a worktree, `git rev-parse --show-toplevel` is the worktree folder, for example `sindri-s1-app-shell` or an `isolation: worktree` agent folder, not `agentic-workflow`.
     - Every implementer, every prover, the P8 `claude -p` journey, and the owner's own `.claude/worktrees/*` sessions would scope to a repo that matches no item or profile repo.
     - P10's `OpenItem` resolves "the repo path from the profile", so names that don't match break that too.
  - Evidence: §3.1 "Session callers are scoped to one repo"; §3.2 `CreateItem` row; §8 bullet 3 ("its git top-level name"); P3 signal; P8 signal; P10 row.
  - Fix:
    - Derive the repo from the main worktree: the basename of the parent of `git rev-parse --path-format=absolute --git-common-dir`. Better still, have the server match the working directory against the profile's repo paths, worktrees included, and send that name.
    - State in §3.2 that `CreateItem` with no body `repo` uses the caller's `X-Sindri-Repo` (or uses none, and say so).
    - Make P3's comparison scope-equal: run both sides as `human` through `sindri-prove.sh`, or add `--repo` to both sides and assert the session view separately.

### LOW

- **Backwards compatibility (carried from pass 3, now contradictory):** §13 says "the request records are unchanged". But §2.1 line 60 says a command "is a request record that implements the marker `ICommand<TResult>`", and `ARCH-CQRS-001` requires `TCommand : ICommand<_>`.
  - S1's `StopProcessRequest(StopReason)` and `ApproveProfileRequest(ProfileHash?, bool)` are records in `Aw.Sindri.Features` with PublicAPI lines. Adding the marker changes their declarations and their `PublicAPI.Unshipped.txt` lines.
  - §10 C2 still says only "registrations move to `AddCommand`".
  - Fix: in C2 and §13, say that S1's request records gain `ICommand<…>` (and the query records gain `IQuery<…>`), with their PublicAPI lines.
- **Ramp-up / scaffolding:** §12's step 2 list leaves out the CQRS marker. The generator's request template knows nothing of `ICommand` or `IQuery`. So each implementer must add the marker to the generated `<Slice>Request` by hand, or `ARCH-CQRS-001` fails at integration. Fix: add "make the request implement `ICommand<…>` or `IQuery<…>`" to step 2, or give `--slice-only` a `--cqrs command|query` flag.
- **Docs consistency:** §10 C7 still says `--slice-only` "writes only the slice folder, the test, the builder and new ids, and edits no shared file". That contradicts §12 step 1 and "Shared files", which now have it write PublicAPI lines. Fix: update C7 to match §12, and to match whichever option the first MEDIUM chooses.

## Pass-3 resolution check

| Pass-3 finding | Status |
|---|---|
| MEDIUM: `--allowedTools` swallows the P8 prompt | Resolved (§8, P8 row) |
| MEDIUM: units can't build without their PublicAPI lines | Resolved as worded. The same root cause remains for `A.cs` (MEDIUM above) |
| LOW: agent-session variables are unverified | Accepted by the owner (P1 checks against real runs). Not re-raised |
| LOW: a refused `stop` isn't audited | Resolved (§2.1) |
| LOW: the CLI sends no `X-Sindri-Repo` | Resolved (§3.1). It created the repo-scoping MEDIUM above |
| LOW: docs list | Resolved (§12 "Docs per phase") |
| LOW: C2 record changes | Not resolved, and §13 now contradicts §2.1 (LOW above) |

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | MEDIUM | 1 |
| Setup / install (time-to-first-success: ~10–15 min) | None | 0 |
| Error messages & observability | None | 0 |
| Docs completeness | LOW | 1 |
| Backwards compatibility | LOW | 1 |
| Ramp-up cost (tribal-knowledge items: 3) | MEDIUM | 2 |

Time-to-first-success assumptions:
- A fresh worktree on P1.
- Cold locked restore plus build: ~5–8 min.
- `--slice-only` plus the anchor lines: ~2–3 min.
- Seeding with `sindri-prove.sh`: ~2 min.
- Add ~5 min the first time `--slice-only` reverts on the missing `A.` factory.

Tribal knowledge still unwritten:
1. The generated red test needs its `A.cs` factory before anything builds.
2. In a worktree, the "git top-level name" is the worktree folder, not the repo name.
3. Generated request records need the `ICommand` or `IQuery` marker by hand.

## Verdict

No CRITICAL or HIGH findings remain, and every pass-3 MEDIUM is fixed as worded. Two new MEDIUM findings need short spec edits before P0, P1 and P3 start: `--slice-only` can't build its red test without `A.cs`, and session repo names come from worktree folder names, which also breaks P3's signal. Three LOW consistency fixes remain.
verdict: PASS
