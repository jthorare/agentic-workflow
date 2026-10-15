# Architecture Review (round 3, final): Sindri context retrieval engine

_Reviewed by `/archReview --slim` on 2026-10-10. Spec: `ctx-wt/docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (978 lines).
Checked against `src/Aw.Kernel.Data/MigrationChecks.cs`, `deps.yaml` (header rules, `limits`, Evidence lock files, Sindri → Evidence `file` edge),
`schemas/deps.schema.json`, `.agents/rules/csharp.md` (ARCH-DIR-003/004), and the S1 worktree's `src/Sindri/` project layout (read only)._

## Verdict: NEEDS WORK

The round-2 fixes all hold in substance. N1, N2, N3, N5, N8, N9, N11 and N12 are resolved, and N13 is answered acceptably. The design is
sound and needs no rework. One new HIGH remains, and it has a one-sentence fix. **R3-1:** owner checks run an unmerged PR build against the
owner's real Sindri home. That breaks R2-1's own invariant ("only throwaway homes ever ran an unmerged migration"), so a later
renumbering puts the owner's real `context.db`, and the human map-review overrides in it, behind an AW-DATA checksum refusal. Two
MEDIUMs are also worth fixing in the same edit. R3-2: the §14 "no expiry" answer for the fan-out exception contradicts `deps.yaml`'s
`exceptionMaxDays`, and the `DEPS-PROJ-007` carve-out is unstated. R3-3: stacking on two unmerged dependencies isn't defined. The
remaining findings are text drift.

## Scores

| Dimension | Score | Notes |
|---|:---:|---|
| Complexity | 4 | Ascend is now narrow (§6.1 step 3), and the stage DAG is complete. Only the multi-parent stacking rule is missing (R3-3) |
| Scalability | 4 | As-of maps are deterministic and cached per commit and extractor version. Retained cut versions still live in `context.db` (R3-7, LOW) |
| Maintainability | 3 | R3-1 is HIGH but contained. Stale phase text contradicts the stage table in three places (R3-5) |

## Round-2 verification

| R2 | Spec now | Verified against | Status |
|---|---|---|---|
| N1 migrations | "Shared files": `NNN_<stage>_<name>.sql`, numbered on the branch's base, renumbered on rebase, contiguous above the base, frozen at merge, throwaway homes only | `CheckSequence` (`MigrationChecks.cs:12-29`) needs 1..n with no gaps, and contiguous-on-base satisfies it on every branch. `CheckApplied` compares (version, name, checksum) per row, so a renumbered file is safe only where no home ever applied it | **Resolved for stage homes.** Broken for owner checks: R3-1. Phase 1 and Phase 3 still say "migration 1" and "migration 2": R3-5 |
| N2 DAG | S-7A ← S-3·S-4·S-FX (wave 4); S-3M ← S-1M·S-3; S-6 ← S-3·S-5A·S-FX; S-CF ← S-4·S-5B | Table, mermaid and waves agree edge for edge. Wave arithmetic holds (S-5B 3, S-CF 4, S-7B 5, S-8 6). The critical path S-1A → S-3 → S-4 → S-7A → S-7B → S-8 matches | **Resolved.** Phase 7's "S-7A wave 3, S-7B wave 4" is stale: R3-5 |
| N3 lock tier | §4: `IHeavyJobLock` port in `Aw.Context.Core`, implemented in Sindri Infrastructure, passed through `AddContextInfrastructure`; §11 row and §14 A-F14 corrected | `deps.yaml:258-262` Sindri → Evidence `file` edge for the lock exists. S1 has `Aw.Sindri.Infrastructure`. ARCH-DIR-004 allows Sindri Infrastructure → `Aw.Context.Core`, and ARCH-DIR-003 is intra-package. No upward file edge | **Resolved** |
| N4 fan-out | §14: "the fan-out exception has no expiry" | `deps.yaml:24-26`: fan-out exceptions are "at most `limits.exceptionMaxDays` long" (183), and `exceptionMaxDays` is a required limit in the schema | **Answer wrong as stated:** R3-2 |
| N5 ascend | §6.1 step 3: the ascended feature doesn't re-enter own scope; further steps start from the reached surface or action | Narrow, as recommended. The flow hop ("the next node") reads as downstream. There is still no depth-2 fixture in §6.1 or §10 | **Resolved** (the fixture gap is R3-6, LOW) |
| N6 truth map | §9.1: truth goes through the sample's as-of (cut) map | No leak. But files the PR created have no `implements` at the cut, so they silently drop out of node truth | **Answered.** The reporting gap is R3-6, LOW |
| N7 as-of builds | §9.1: deterministic only, no labeling, cached by commit sha and extractor version | Removes the token risk. Retention is still in `context.db` (§6.1 "every version a backtest sample cuts at") | **Resolved for cost.** Storage is R3-7, LOW |
| N8 S-2W | Its own stage (S-1A·S-2R); S-5A "merges after S-2W"; P-L2 holds | The table and the dashed mermaid edge agree | **Resolved.** The Phase 2 size line still says "rides in S-1A, or a one-line follow-up": R3-5 |
| N9 conflict check | Stage S-CF (S-4·S-5B); Phase 4 points to it; §14 A-F13 updated | Consistent | **Resolved** |
| N10 isolation | Brief step 3: per-stage `AW_SINDRI_HOME`, port from 7491, a fixture profile approved by `expect` in the throwaway home; real-source parts become owner checks | Stage-owner isolation is complete. There is no Signal column, and CI-only stages (S-1C, S-2R, S-FX) have no stated evidence form | **Resolved** except R3-6 (LOW) and R3-1 (the owner side) |
| N11 canonical key | D16: Linear keys on its UUID; the identifier is an alias | The native adapter side is fixed. §5.1 still maps Onyx's records "from its document link", which carries the mutable identifier | **Resolved for native.** The Onyx side is R3-4 |
| N12 `symbol` | §8.1 "4 (files), 9 (symbols)" | Matches the Phase 4 signal | **Resolved** |
| N13 S-1A size | §14: measured at its plan step, split if over M | Acceptable | **Answered** |

**§8.6 codes vs the stage ranges:** 001–005, 010 and 011 are S-1A (001–019). 030 is S-3. 040–043 are all S-4 (lock, review terminal,
labeling, stale review file). 050 is S-5. 070 is S-6. No listed code falls outside its owner's range. S-CF `100–109` is still a
three-digit `SND-CONTEXT-NNN` and collides with nothing. S-1M (020–029), S-7 (080–089), S-8 (090–099) and S-CF have no codes listed
yet, which is consistent with "§8.6 follows these ranges as phases add codes". Phase 2's invalid-pattern signal reuses `SND-CONTEXT-001`
(S-1A's range), which is a reuse, not an allocation. **Consistent.**

## Findings

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| R3-1 | HIGH | delivery / data | "Work data stays with the owner" (owner checks run "before merging"); "Gates that stay sequential" (`profile approve` of the real profile); "Shared files", migrations row | **Owner checks run unmerged migrations in the owner's real home.** An owner check runs the ready PR's build in the work repo against real sources, and nothing says where the home is, so it is the owner's real `$AW_SINDRI_HOME`. That build carries the stage's unmerged, not-yet-frozen migration. With up to 4 ready PRs and a free merge order, another stage merging first renumbers it. `CheckApplied` then finds a row whose (version, name, checksum) differs from the build, and the real `context.db` refuses to open (`SND-CONTEXT-004`, whose "rebuild" fix doesn't apply). That database holds the `map_override` rows: the owner's review decisions, which can't be rebuilt. This contradicts the R2-1 invariant that "only throwaway homes ever ran an unmerged migration" | One rule in the brief and in "Work data stays with the owner": **an owner check runs in an owner-check home** (`AW_SINDRI_HOME=<scratch>/owner-<id>`, the real profile copied in and approved there, never the real home). Alternatively, run the owner check only on a PR rebased onto `main` and next in merge order. `owner-check.md` states the home. The owner's real home only ever runs merged builds |
| R3-2 | MEDIUM | deps | §4 "Fan-out"; §14 Round 2 "Answered: the fan-out exception has no expiry"; S-1C scope | **The answer contradicts the existing rule.** `deps.yaml:24-26` limits every fan-out exception to `exceptionMaxDays` (183), and the schema requires that limit, so "no expiry" can't be recorded without changing the mechanism. S-1C lists "the fan-out exception mechanism" without saying it changes this. Separately, `compose` needs Sindri Host to reference `Aw.Context.Features` and `.Infrastructure`, which `DEPS-PROJ-007` ("never a Host, Infrastructure, Features or App") and the matching ArchUnitNET rule refuse today. §4 implies a carve-out but S-1C doesn't list it | Put both in S-1C's scope explicitly. (a) `compose` edges are exempt from `DEPS-PROJ-007` and `ARCH-DIR-004` for `Add…()` only (the DEPS-COMPOSE-001 and ARCH-COMPOSE-001 checks). (b) Either `compose` targets aren't counted by `packageFanOut` (they get their own cap), or the exception is renewed every 183 days and §14 says so. Drop "no expiry" unless (b)'s first option is taken |
| R3-3 | MEDIUM | delivery | "Stacking"; "Stages" (`may start once each dependency is merged or open as a review-clean PR`) | **Stacking on two unmerged branches isn't defined.** S-3M, S-5B, S-6, S-7A, S-CF and S-7B each have two or more dependencies that can be open at once (for example S-5B on S-3 and S-5A, both wave 2). "Branches from it and bases its PR on it" assumes one parent. A GitHub PR has one base, and a branch merged from two open branches makes the rebase-and-renumber rule ambiguous (which base sets `NNN`?) | Add: **a stage may stack on at most one unmerged dependency; every other dependency must be merged.** The lead picks the parent on the critical path. The wave numbers stay as earliest starts |
| R3-4 | MEDIUM | data model | §5.1 `Canonical` comment ("Onyx: from its document link"); Phase 6 files | **Onyx-to-UUID mapping is undefined.** A Linear issue's link is `linear.app/<ws>/issue/ENG-482/…`, which is the mutable identifier, not D16's UUID. An issue in Onyx's scope that the native adapter hasn't synced (or whose identifier changed) gets no UUID, so §5.2 dedup fails and the issue is cited twice. Recording only the current identifier as an alias doesn't cover a team move | S-6 maps Linear documents from Onyx's document **id** if it carries Linear's node id (verify at the pinned release). Otherwise it resolves the identifier through `node_alias`, then through the Linear adapter (`issue(id:)` accepts an identifier), and caches the result. The native adapter records previous identifiers as aliases. Contract test: the same issue arriving from both adapters after a team move gives one citation with two refs |
| R3-5 | LOW | consistency | Phase 2 "Size"; Phase 7 "Size"; Phase 1 and 3 "Files" | Stale text against the stage table: Phase 2 still says the wiring "rides in S-1A, or in a one-line follow-up" (it is S-2W). Phase 7 says S-7A is wave 3 and S-7B wave 4 (the table says 4 and 5). Phases 1 and 3 say "migration 1" and "migration 2", but under the named scheme Phase 3's number depends on merge order against S-5A | Point Phase 2 at S-2W and its signal. Fix the Phase 7 waves. Say "S-1A's migration" and "S-3's migration" |
| R3-6 | LOW | eval / delivery | §9.1 truth; §6.1 and §10 fixtures; the stage table | (a) Truth files the PR created have no node at the cut, so they drop out of node truth silently, and a sample whose node truth is empty has undefined node recall. (b) There is no depth-2 cascade fixture and no fixture where a reached node re-reaches its own feature. (c) S-1C, S-2R and S-FX have no running-app signal, and the brief doesn't say their evidence is CI output | (a) Report "truth files unmapped at cut" per run, and exclude empty-truth samples from node metrics with a count. (b) Add both fixtures to §10's unit lane. (c) Add one line: CI-only stages give `check-deps` and test output as evidence |
| R3-7 | LOW | storage | §6.1 retention; §9.1 cache | Cached as-of maps are retained `map_version` rows in `context.db` ("every version a backtest sample cuts at"). They grow with the sample count, and nothing ever purges them | Keep the cut-map cache in an eval-scoped database under `eval/` with the corpus's 90-day TTL. `context.db` then retains only the last 20 versions |

## Edge Case Matrix

| Boundary | Dependency unavailable | Invalid / malicious input | Load 10x | State leakage |
|---|---|---|---|---|
| Sindri Host ↔ `Aw.Context` (facade, compose, lock port) | OK: §8.6 exit 2 / `SND-CONTEXT-004`; exit 75 / `-040` on a busy lock | OK: §4 `CheckSettings` reports key paths | OK: one facade, `compose` limited to `Add…()` | R3-2 (rule carve-outs unstated) |
| Source adapters (repo-docs, Linear, Onyx) | OK: §5.1 `Unavailable` → `degraded`; `-011` when all are down | OK: §5.1 argument validation; §7 sanitize, scrub, permalink allowlist | OK: §8.3 caps | R3-4 (Onyx canonical key) |
| `context.db` (Kernel.Data) | OK: §6.2 batched build, then flip | OK: FTS phrase quoting | R3-7 | R3-1 (real home runs unmerged migrations) |
| Egress (HTTP / CLI / MCP) | OK: §8.4 `degraded` outside the budget | OK: §7 per-field scrub, one escaper; §3 `LoopbackGuard` | OK: §8.3 32k cap | OK: §3 `--scope local`, `context.mcp.enabled` false by default |
| Cascade / planner | OK: graph-only results when sources are down | OK: depth ≤ 4, path ≤ 8 | OK: §6.1 step 3 narrow ascend | OK: visited set (fixture gap R3-6) |
| Eval harness | OK: refs re-fetched per run | OK: `SND-CONTEXT-005` worktree refusal | OK: deterministic, cached as-of maps | R3-6 (silent truth drop) |
| Parallel delivery | R3-3 (multi-parent stacking) | n/a (the input is the spec) | OK: caps of 3 building, 4 ready, depth 2 | OK for stage homes (brief step 3); R3-1 for owner checks |

Findings: R3-1 to R3-7 above.

## Missing Error Handling

- A real `context.db` whose applied migration was renumbered shows `SND-CONTEXT-004` with a fix ("rebuild with `context map build`")
  that would discard `map_override` rows. Give the AW-DATA sequence and checksum causes their own fix text ("this home ran an unmerged
  build; restore from backup or use a scratch home"), and prevent the cause through R3-1.

verdict: NEEDS_WORK
