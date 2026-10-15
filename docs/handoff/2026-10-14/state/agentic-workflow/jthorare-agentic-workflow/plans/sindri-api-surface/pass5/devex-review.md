# Plan Devex Review — sindri-api-surface (pass 5, final)

**Plan:** docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md (11500b8)
**Reviewed:** 2026-10-10
**Scope:** I checked each pass-4 devex finding against the pass-4 edit (§13 "Pass 4 → final text"). I also checked `scripts/scaffold/new-slice.mjs` and the S1 worktree `.claude/worktrees/sindri-s1-app-shell` (5c9c4a4), read only. This report lists only findings that are still open, or that the pass-4 edit introduced. Owner decisions are not re-raised.

## Summary

Both pass-4 MEDIUM findings are resolved.
- **`--slice-only` now writes the slice's `A.cs` factory and `IdsJsonContext` lines** (§12 step 1, C7). The red test can compile. This matches the generator's existing inserts (`new-slice.mjs:176-177`, `:212`). S1's `tests/Aw.Sindri.Testing/A.cs` ends in the closing brace those inserts expect.
- **Repo naming now goes through `--git-common-dir` and the profile's `RepoEntry(Name, Path)`**, which exists in S1. `CreateItem` defaults to the header repo, and P3 compares two human calls.
- The ICommand marker step is in §12 step 2, and C7 matches §12.

Three small text leftovers remain. §13 says they are fixed, but the body text wasn't changed: the C2 row, the P0 signal, and a stale `A.cs` bullet in §12 step 2. The pass-4 repo fix also adds one new LOW: path matching on macOS.

Time to first success: ~10–15 min (unchanged).

## Findings by severity

### CRITICAL
None.

### HIGH
None.

### MEDIUM
None.

### LOW

- **Docs consistency (pass-4 LOW, marked resolved in §13 but not changed in the body):** the §10 C2 row still says only "Their handler bodies don't change; their registrations move to `AddCommand`". §13 says "C2 text is corrected: the request records gain the marker". The pass-3 table in §13 also says it. But the C2 row an implementer reads in §10 doesn't mention adding `ICommand<…>` to `StopProcessRequest` and `ApproveProfileRequest`.
  - Evidence: line 760 (C2 row); line 1001 ("C2 text is corrected"); line 975.
  - Fix: append to the C2 resolution cell: "and `StopProcessRequest` and `ApproveProfileRequest` implement `ICommand<…>`".

- **Docs consistency (new, from the `A.cs` fix):** §12 step 2 still lists "the `A.cs` builder method" as one of the implementer's hand edits. Step 1 now says the generator writes it. An implementer who follows step 2 adds a second `A.<Slice>Request()` and gets a duplicate-member compile error.
  - Evidence: line 835 (step 2's last bullet) against lines 830–832 (step 1).
  - Fix: delete the bullet from step 2.

- **Docs consistency (new, from the `A.cs` fix):** the P0 signal still reads "only slice files and its own PublicAPI lines change". The generator now also edits `A.cs` and `IdsJsonContext`. A P0 prover asserting the signal as written would fail, or would have to weaken it.
  - Evidence: line 1017 (P0 row) against C7 (line 761) and §12 step 1.
  - Fix: change the signal to "only slice files and its own PublicAPI, `A.cs` and `IdsJsonContext` lines change".

- **API ergonomics (new, from the repo-name fix):** §3.1 resolves the repo name to "the profile's repo entry whose path contains the main checkout". It doesn't say how the two paths are made comparable:
  - Plain `git rev-parse --git-common-dir` returns the relative `.git` in a main checkout; inside a worktree it returns an absolute path.
  - A profile `Path` can use `~`.
  - On macOS, `$TMPDIR` repos show up as `/var/folders/…` in one source and `/private/var/folders/…` in the other.

  If the paths don't match, nothing fails loudly. The header is silently omitted, and the session then sees only items that have no repo. That most likely shows up in a P8 or P10 prover's temp-repo profile.
  - Evidence: lines 293–295; S1 `RepoEntry(string Name, string Path)` (`Aw.Sindri.Core/PublicAPI.Unshipped.txt:225`).
  - Fix: one clause in §3.1. Use `--path-format=absolute --git-common-dir`, take its parent, expand `~`, and compare real paths. Add a CLI unit case for a worktree path and a `/private` alias. Optionally have `doctor` print the resolved repo name for the cwd.

## Pass-4 resolution check

| Pass-4 finding | Status |
|---|---|
| MEDIUM: `--slice-only` red test needs `A.cs` | Resolved (§12 step 1, C7). Two leftovers: step 2's `A.cs` bullet and the P0 signal (LOW above) |
| MEDIUM: repo scope in worktrees; P3 mixed scopes | Resolved (§3.1, §8, P3). New LOW: how the paths are normalized |
| LOW: C2 record changes | Fixed in §13 only. §10 C2 is unchanged (LOW above) |
| LOW: ICommand/IQuery marker not in step 2 | Resolved (§12 step 2) |
| LOW: C7 contradicts §12 | Resolved |

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | LOW | 1 |
| Setup / install (time-to-first-success: ~10–15 min) | None | 0 |
| Error messages & observability | None | 0 |
| Docs completeness | LOW | 3 |
| Backwards compatibility | None | 0 |
| Ramp-up cost (tribal-knowledge items: 1) | None | 0 |

Tribal knowledge still unwritten:
1. The repo name only resolves when the profile's `Path` and git's common dir are the same real, absolute path.

## Verdict

No CRITICAL, HIGH or MEDIUM findings remain, and both pass-4 MEDIUM findings are resolved in the text. What's left is three one-line text fixes (§10 C2, §12 step 2's `A.cs` bullet, the P0 signal) and one clause on how paths are normalized in §3.1.
verdict: PASS
