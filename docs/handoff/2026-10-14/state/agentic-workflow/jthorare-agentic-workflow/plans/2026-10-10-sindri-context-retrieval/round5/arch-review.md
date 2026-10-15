# Architecture Review: Sindri context retrieval engine (round 5, final)

_Reviewed by `/archReview --slim` on 2026-10-10. Spec: `ctx-wt/docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`
(1069 lines, commit e1a5f05). Checked against `scripts/check-deps.mjs` (`contractOk`/`sourceOk` :224-227, `DEPS-PROJ-001/007/008`
:228-259, `DEPS-FANOUT-001` :170-176, `DEPS-EXC-002` :196-198, project-edge staleness :268-271), `deps.yaml` (header rules, Sindri
tier 3, Sindri → Evidence `file` edge for the heavy-job lock, Sindri's 4 planned project edges, `exceptions: []`),
`tests/Aw.Architecture.Tests/Rules/DirectionRules.cs` (ARCH-DIR-004 `Judge`), `.agents/rules/csharp.md` (JSON context in Host),
and the S1 worktree (read only): Sindri project references, `Scheduling/ServiceSchedule.cs`._

## Verdict: SOUND

Every round-4 fix holds. R4-1 passes the real rules. Sindri Infrastructure implements a Sindri Core port, and the Host adapter is
the only place that references `Aw.Context.Core`. `sourceOk` allows that (Host), `contractOk` allows it (a Core target), and
ARCH-DIR-004 allows it (`to.Layer == Core`). The lock directory is already covered by the declared Sindri → Evidence `file` edge.
No HIGH or CRITICAL findings remain. Two MEDIUMs are one-line spec corrections and should go in before `/specToProvenPR`:

- **R5-1:** the fan-out exception is tied to a `compose` edge, but `DEPS-FANOUT-001` counts only `project` edges.
- **R5-2:** `map import` can write `human` provenance without the interactive-terminal gate that review has.

## Scores

| Dimension | Score | Notes |
|---|:---:|---|
| Complexity | 4 | The stage DAG, caps, stacking and owner-check outline are complete. The owner-check MCP step is missing one guard (R5-4) |
| Scalability | 4 | Unchanged. Caps, batched builds and retention are in place |
| Maintainability | 4 | The R4-1 placement is now correct. R5-1 is wording that would cause a confusing `DEPS-EXC-002` at S-1C |

## Round-4 verification

| R4 | Spec now | Verified against | Status |
|---|---|---|---|
| R4-1 lock adapter | §4: Sindri Infrastructure implements Sindri's own lock port (Sindri Core) over the shared directory; a Host adapter wraps it as `IHeavyJobLock`; §11 row and §14 match | `check-deps.mjs:227` (`sourceOk` refuses only `*.Core`/`*.Infrastructure` sources), `:224` (`contractOk`: target `Aw.Context.Core`), `DirectionRules.cs` `Judge` (cross-package to Core → null). `deps.yaml` Sindri → Evidence `file` edge (lock directory). Host project fan-out exempt | **Resolved.** The Sindri → Context `project` edge it needs is R5-1 |
| R4-2 owner-check home | Seven-step outline: per-stage home under `${AW_STATE_DIR:-…}`, created fresh, port 7480, profile edited and approved there, `sindri-ownercheck` MCP name, teardown | `LoopbackBinding` (env port wins over `api.port`); approvals per home | **Resolved**, except that the real `sindri` server stays registered beside the check server and nothing confirms which build answered (R5-4) |
| R4-3 export | `map export` (CLI) and `GET …/map/export` in S-3. The export keeps original timestamps and provenance, which import restores. Round-trip test | §8.5, Phase 3 Files | **Resolved.** Content gap: R5-3. Trust gap on import: R5-2 |
| R4-4 pending-keyed decisions | §5.1: pending keys come only from structured metadata and are never a decision source. §5.2: native fetch includes comments | §6.1 decision key = sha256(canonical ‖ quote text) | **Resolved** |
| R4-5 S-1C row | Names both carve-outs and declares the exception "with the planned Sindri → Context `compose` edge" | `check-deps.mjs:172` | **Partly resolved.** The edge kind is wrong for the fan-out rule (R5-1) |
| R4-6 aliasing | §5.2 writes aliases on every fetch | §14 round 4 doesn't mention R4-6 | **Unanswered** (R5-5) |
| R4-7 stacking intro | Lines 761-763 match the one-parent rule | "Stacking" bullet | **Resolved** |

## Findings

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| R5-1 | MEDIUM | deps | §4 "Fan-out"; S-1C row; §11 compose row | **The exception sits on the wrong edge kind, and the Core edge is never declared.** `DEPS-FANOUT-001` counts only `e.kind === "project"` targets (`check-deps.mjs:172`). A Sindri → Context `compose` edge leaves Sindri at 4, so the exception suppresses nothing and S-1C fails `DEPS-EXC-002` (`:197`). Its fix text even says "delete the exception". Separately, Sindri Features (`IContextEngine`) and the Host lock adapter reference `Aw.Context.Core`. `DEPS-PROJ-001` (`:259`) needs a declared `Sindri\|Context\|project` edge for that, and the spec never declares one (§4 lists only Context's own edges) | S-1C declares **two** edges: a `project` edge Sindri → Context (`planned`; S-1A sets it to `current` when the first reference lands, or `DEPS-STALE-002` fires), and the `compose` edge. The `DEPS-FANOUT-001` exception (subject Sindri) suppresses the `project` edge's fan-out. S-1C's checks give `compose` a tier rule and staleness check, and a Mermaid arrow (`ARROW` map, `:276`). Or state outright that `compose` counts toward fan-out and implies `project`. Either way, fix the §4 sentence "the `compose` edge … carries a recorded exception" |
| R5-2 | MEDIUM | trust (D15) | §8.5 `POST /sindri/context/map/import`, CLI `map import`; Phase 3 ("importing an export restores … provenance, so `human` stays `human`"); §9.1 ("hand-authored imports count as overrides") | **Import can mint trusted `human` entries with no terminal gate.** §3 limits `human` provenance to the CLI at an interactive terminal, and review `--apply` asks for confirmation there. Import does neither, over HTTP or the CLI. Under the honest-mistake threat model, a session agent that helpfully runs `$SINDRI context map import` on a file it wrote (or that POSTs to the loopback route) makes every entry `confirmed` and `trusted`, which is exactly what D15 is meant to prevent | Import writes `human` provenance (as restored from an export, or as granted to a hand-authored map) only under the same rule as `review --apply`: the CLI at an interactive terminal, with counts printed and a confirmation. Otherwise it is refused with `SND-CONTEXT-041`, or imported as unconfirmed `model` drafts. Stage owners already approve at a pseudo-terminal through `expect`, and owner checks are run by the human, so neither flow breaks. Add an integration test: an import without a terminal gives no `human` rows |
| R5-3 | LOW | interface | Phase 3 export contents ("nodes, edges, aliases, overrides and rejections"); owner-check step 3 ("labeling off") | The export doesn't say it carries the `decision` table (wording, quote span, trust) or the `quotes`/`supersedes`/`mentions` edges with their evidence refs. Owner checks run with labeling off, so a seeded home can't re-extract decisions, and S-5B's and S-CF's owner checks would have no decisions to test | State that the export holds decision rows and every edge with its `edge_evidence`. Extend the round-trip test to cover a decision with an `accept` override and a `supersedes` pair |
| R5-4 | LOW | delivery | Owner-check outline steps 1, 2, 5 | (a) The real `sindri` MCP server stays registered in the work repo beside `sindri-ownercheck`. A session can answer through `mcp__sindri__context_query` and pass against the merged build. (b) No step confirms the build identity (round-4 recommendation). (c) After an aborted check, a process left on 7480 makes the next check fail with `PortInUse`. Step 2 deletes the home but doesn't stop that process | Step 5: the check's prompts name `mcp__sindri-ownercheck__*` tools, or the real `sindri` registration is removed for the duration of the check. Step 6 starts by asserting that `$SINDRI` version or the doctor header shows the PR head commit. Step 2 stops any listener on 7480 first |
| R5-5 | LOW | data model | §5.1 `Canonical`; §5.2 `linear` row; §14 round 4 (R4-6 not listed) | Round 4's R4-6 was neither fixed nor answered. (a) Aliases are written only on fetch, not on native search hits, so an unsynced issue found by both adapters in one query is cited twice. (b) After a team move, an Onyx document still carrying the old identifier stays `linear:pending/…` forever and is never deduplicated | Write aliases on native hits as well as fetches. In S-5A, record previous identifiers where Linear exposes them (verify `previousIdentifiers`). Add the cross-adapter contract test (before sync, and after a team move: one citation with two refs). Or answer R4-6 in §14 |
| R5-6 | LOW | consistency | §6.1 `Record` key example `record:linear:issue/ENG-482`; §8.7 `record:linear:issue/ENG-301` | The examples key `Record` nodes on the mutable identifier, which contradicts D16 (UUID key, identifier as alias). An implementer following the table would orphan `mentions` links on every team move | Use `record:linear:issue/<uuid>` in both examples, and show the identifier as the display alias |
| R5-7 | LOW | operability | §5.2 repo-docs ("scheduled service, every 5 min by default"); §8.8 profile example; §6.2 purge | In S1, `ServiceSchedule.From` schedules only the services the approved profile names under `services:` (`Scheduling/ServiceSchedule.cs`). The §8.8 example lists none, so the repo-docs refresh, sync, rebuild and purge never run unless the owner adds them. Phase 1's index stays empty until a manual `sources sync` | Add the context services with their cadences to the §8.8 example (`services: { context-docs-refresh: { every: 5m }, … }`). Doctor `context:sources` warns when `context:` is configured but its refresh service isn't scheduled |

## Edge Case Matrix

| Boundary | Dependency unavailable | Invalid / malicious input | Load 10x | State leakage |
|---|---|---|---|---|
| Sindri Host ↔ `Aw.Context` (facade, compose, lock port) | OK: §8.6 exit 2 / `-004`; exit 75 / `-040` | OK: `CheckSettings` with key paths | OK: one facade; `compose` limited to `Add…()` | R5-1 (edge declaration); the lock placement itself is OK (`check-deps.mjs:224,227`) |
| Source adapters (repo-docs, Linear, Onyx) | OK: `Unavailable` → `degraded`; `-011` when all are down | OK: §5.1 validation; pending keys only from structured metadata | OK: §8.3 caps | R5-5 (double citation, permanent pending) |
| `context.db` and map import/export | OK: batched build, then flip | R5-2 (import grants `human` without a terminal) | OK: retention and purge | OK: real home runs merged migrations only; R5-3 (export contents) |
| Egress (HTTP / CLI / MCP) | OK: `degraded` | OK: per-field scrub, one escaper, `LoopbackGuard` | OK: 32k cap | OK: `--scope local`; MCP off by default |
| Cascade / planner | OK: graph-only results | OK: depth ≤ 4, path ≤ 8 | OK: narrow ascend | OK: visited set |
| Scheduled services | R5-7 (not scheduled unless named in the profile) | n/a | OK: one map build at a time | OK |
| Parallel delivery and owner checks | OK: single-parent stacking | OK: denylist script prints locations only | OK: caps 3 / 4 / depth 2 | R5-4 (wrong MCP server, no build identity, stale listener) |

Findings: R5-1 to R5-7 above.

## Missing Error Handling

- `map import` has no non-terminal refusal path (R5-2). Reuse `SND-CONTEXT-041`.
- The owner check has no build-identity assertion, so a check against the wrong build passes and says nothing (R5-4).

verdict: PASS
