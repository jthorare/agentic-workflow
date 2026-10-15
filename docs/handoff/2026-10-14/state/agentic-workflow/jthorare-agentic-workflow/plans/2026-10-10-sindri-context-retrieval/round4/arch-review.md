# Architecture Review: Sindri context retrieval engine (round 4)

_Reviewed by `/archReview --slim` on 2026-10-10. Spec: `ctx-wt/docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (1019 lines).
Checked against `src/Aw.Kernel.Data/{EmbeddedMigrations,MigrationChecks}.cs`, `deps.yaml` (header rules, `limits`, Sindri and Evidence
nodes, `exceptions: []`), `schemas/deps.schema.json`, `scripts/check-deps.mjs` (`sourceOk`, `DEPS-PROJ-007/008`, exception checks),
`tests/Aw.Architecture.Tests/Rules/DirectionRules.cs`, and the S1 worktree's `src/Sindri/` (project references, `LoopbackBinding`,
`SettingsFromEnvironment`, `SindriSettings`), read only._

## Verdict: NEEDS WORK

The round-3 fixes hold in intent. The migration file form matches the loader exactly. Single-parent stacking is defined. The fan-out
exception now follows the schema. Onyx's Linear records have a key path. The design itself needs no rework. One HIGH remains, and it
predates round 3. **R4-1:** the `IHeavyJobLock` adapter is placed in `Aw.Sindri.Infrastructure`, which `check-deps.mjs` refuses
(`DEPS-PROJ-008`: Core and Infrastructure reference tier-0 packages only). Round 3 checked this placement against ARCH-DIR-004 only.
Three MEDIUMs are worth fixing in the same edit:

- **R4-2:** the R3-1 owner-check home is fixed and never reset. It collides with the real instance's port, and the work repo's MCP
  registration still points at the real instance.
- **R4-3:** the owner-check seeding step calls for a map export, and no verb produces one.
- **R4-4:** decision keys can be minted from `linear:pending/…` keys, because the native adapter doesn't fetch comments.

## Scores

| Dimension | Score | Notes |
|---|:---:|---|
| Complexity | 4 | Stage DAG, caps and stacking are now complete and consistent. The owner-check environment still has three loose ends (R4-2) |
| Scalability | 4 | Unchanged from round 3. Cut-map caches are purged by the retention service (§14 round 3) |
| Maintainability | 2 | R4-1 contradicts an enforced merge-gate rule, and §4/§11/Phase 4 would send S-4 straight into a check failure |

## Round-3 verification

| R3 | Spec now | Verified against | Status |
|---|---|---|---|
| R3-1 owner-check home | "Work data stays with the owner": the owner-check home is `$AW_SINDRI_HOME/../sindri-app-ownercheck/`, with the real profile copied in, an empty `context.db`, and prior state seeded by `map import` of an export | `SettingsFromEnvironment.cs:12-17` (home is `AW_SINDRI_HOME`, else `$AW_STATE_DIR/sindri-app`; the profile defaults to `<home>/profile/profile.yaml`). `SindriSettings.cs:12` (approvals live under `<home>/approved`). `LoopbackBinding.cs:12-13` (the port is `AW_SINDRI_PORT`, else the approved profile's `api.port`, default 7410) | **The invariant is restored:** the real `context.db` only runs merged builds. The environment around it is underspecified (R4-2), and the seeding step has no producer (R4-3) |
| R3-2 fan-out / `DEPS-PROJ-007` | §4: the exception carries the schema's review date; S-1C adds the `DEPS-PROJ-007` carve-out for the Host's `Add…()` calls | `deps.yaml:22-26,67-70,441-443`. `check-deps.mjs:126-140` (exception dates), `:174-175` (`DEPS-FANOUT-001`), `:197` (`DEPS-EXC-002`), `:227-228` | **Resolved in substance.** The S-1C row omits both carve-outs, and ARCH-DIR-004 needs one as well (R4-5) |
| R3-3 stacking | "Stacking": at most one unmerged dependency, otherwise wait | Table and waves: S-3M, S-5B, S-6, S-7A, S-CF and S-7B each have two or more dependencies, and the rule covers each | **Resolved.** The stage-table intro (line 724-725) still says "each dependency … merged or open" (R4-7) |
| R3-4 Onyx keys | §5.1: Onyx's identifier resolves through `node_alias`. With no alias yet it is `linear:pending/<id>`, merged into the UUID key when the native sync resolves it | §5.2 native fetch list. §6.1 decision key `sha256(canonical key ‖ span)` | **Resolved for dedup.** The interaction with decision keys is R4-4. Aliasing at hit time and previous identifiers are R4-6 |
| Loader migration form | `NNNN_<stage>_<name>.sql`, e.g. `0003_s5a_linear_cursor.sql` | `EmbeddedMigrations.cs:69` regex `^(?<version>[0-9]{4})_(?<name>[A-Za-z0-9_]+)\.sql$`. `MigrationChecks.cs:12-29` (1..n with no gaps) | **Correct.** The name is part of the applied row, so freezing name and number at merge is what `CheckApplied` needs |

## Findings

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| R4-1 | HIGH | deps | §4 "The heavy-job lock is a port, implemented by Sindri" ("implemented in Sindri (Infrastructure)"); §11 heavy-job lock row; Phase 4 Files; §14 R2-4 | **The lock adapter's placement fails the merge gate.** `check-deps.mjs:227` `sourceOk` allows a cross-package reference only when the referencing project is not `*.Core` or `*.Infrastructure`. `:258` fails `DEPS-PROJ-008` otherwise ("Core and Infrastructure use tier-0 packages only … pass what Core needs in through a port"). `scripts/tests/check-deps.test.sh:471` pins this. `Aw.Sindri.Infrastructure` implementing `Aw.Context.Core.IHeavyJobLock` needs a `ProjectReference` to `Aw.Context.Core`, so S-4 fails `node scripts/check-deps.mjs`. Round 3 verified the edge against ARCH-DIR-004 only | Keep the port in `Aw.Context.Core`. Put the **bridge in Sindri's Host** (`Composition`). Sindri Infrastructure implements Sindri's own lock over the shared directory, behind a Sindri Core port. A small Host class adapts that port to `IHeavyJobLock` and passes it to `AddContextInfrastructure`. The Host may reference `Aw.Context.Core` and already references Sindri Infrastructure. An adapter in `Aw.Sindri.Features` is the alternative. Correct §4, §11, Phase 4 and the §14 R2-4 line |
| R4-2 | MEDIUM | delivery | "Work data stays with the owner" (owner-check home); Phases 1 and 3 signals (a `context_query`/`context_node` call from a session in the work repo) | **The owner-check environment is incomplete in four ways.** (a) **One fixed path, never reset.** If PR A's check applies `0003_s3_…` and PR B's build carries `0003_s5a_…`, `CheckApplied` refuses (`SND-CONTEXT-004`) in the scratch home. "An empty `context.db`" isn't an action anyone takes. (b) **Port.** The copied real profile carries the real `api.port` (default 7410, `LoopbackBinding.cs:13`). If the real Sindri is running, the PR build fails with `PortInUse`. (c) **MCP target.** The work repo's `--scope local` registration points at the real port. If the owner sets `AW_SINDRI_PORT` for the check, the session-based signal parts query the **real, merged build** and pass without testing the PR. (d) Approval is per home (`<home>/approved`), so the copied profile needs `profile approve` in the scratch home. `$AW_SINDRI_HOME/..` is undefined when the variable is unset (the default home) | Rule for `owner-check.md`: home `${AW_STATE_DIR:-~/.agentic-workflow}/sindri-app-ownercheck-<stage>`, deleted before each check. `AW_SINDRI_PORT=<free port>`. `profile approve` in that home. For session steps, `claude mcp add --transport http --scope local sindri-check http://127.0.0.1:<port>/mcp` in the work repo, removed after the check (or the real `sindri` server is stopped for the check). The check's first step asserts the build identity it is talking to (e.g. `$SINDRI version` or the doctor header shows the PR's commit) |
| R4-3 | MEDIUM | interface | "Work data stays with the owner" ("seeds the scratch home by `map import` of an export"); §8.5 CLI and HTTP; Phase 3/4 Files | **No verb produces a map export.** The CLI has `map import <file> \| build \| status \| review [--export F \| --apply F]`. `review --export` writes the review queue, not the reviewed map, and its file is refused once the map version changes (`SND-CONTEXT-043`). So any owner check needing a reviewed map (S-4's second-build check, S-5B, S-6, S-CF) has no supported way to seed one. Copying `context.db` is forbidden | Add `context map export <file>`: the current map with its overrides, in `map import`'s versioned schema, refused inside a worktree (`SND-CONTEXT-005`). Assign it to S-3, beside `ImportMap`. Add a round-trip test (export → import into an empty home → same nodes, edges and confirmed states). State that imported entries become overrides dated at import, which is what §9.1 already assumes |
| R4-4 | MEDIUM | data model | D4 and §5.2 native `linear` row (fetches "projects, Linear Documents, project updates, issues, issue↔PR attachments"); §9.1 ("decisions in comments"); §6.1 `Decision` key; §5.1 `linear:pending/…` | **Decisions in Linear comments have no stable source.** The native adapter's fetch list has no issue comments, but the corpus puts the decisions there. Onyx reads comments (D4). If S-5B or a later stage extracts decisions from Onyx's Linear records, the canonical key may be `linear:pending/ENG-482`. The decision key hashes that key, so the pending → UUID merge changes every such decision key and orphans its `accept`/`reject` overrides, breaking D8 | (a) The native adapter fetches issue comments (`Issue.comments`), either as part of the issue record or keyed `linear:comment/<uuid>`. S-5B's decision extraction then runs on native records. (b) Decision extraction never runs on a `pending` key: the record waits until the alias resolves. Alternatively, the pending → UUID merge writes old → new decision-key aliases, as a key migration does. Add a unit test: a decision extracted before a pending merge keeps its override after the merge |
| R4-5 | LOW | deps | S-1C row; §4 "Fan-out"; §14 round 3 | **The S-1C row is short three items.** (a) It lists neither carve-out. `DirectionRules.cs:48` (ARCH-DIR-004) also flags `Aw.Sindri.Host → Aw.Context.Features/Infrastructure`, so ARCH-COMPOSE-001 must be stated as the ARCH-DIR-004 carve-out for `compose` targets, beside the `DEPS-PROJ-007` one. (b) "The fan-out exception mechanism" already exists (`check-deps.mjs:126-140`). What S-1C adds is one `DEPS-FANOUT-001` exception with subject Sindri. It is keyed by rule and package, not "on the `compose` edge". (c) It needs a `planned` Sindri → Context `project` edge in the same PR (Sindri Features references `Aw.Context.Core`), or it suppresses nothing and fails `DEPS-EXC-002` (`:197`) | Reword the S-1C row: "`compose` edge kind and its checks; the `DEPS-PROJ-007` and ARCH-DIR-004 carve-outs for `compose` targets' `Add…()` calls; Sindri's `DEPS-FANOUT-001` exception, recorded with the planned Sindri → Context project edge" |
| R4-6 | LOW | data model | §5.1 `Canonical` comment; §10 | Two gaps in aliasing. (a) Aliases are written only on sync. A native search or fetch hit already carries the identifier, so an Onyx hit and a native hit for the same unsynced issue in one query are cited twice. (b) Previous identifiers after a team move aren't recorded, so an Onyx document still carrying the old identifier stays `pending` forever. No contract test covers either case | Record the identifier → UUID alias on every native hit or fetch, and the issue's previous identifiers where the API exposes them (verify `previousIdentifiers` in S-5A). Add the round-3 contract test to §10: the same issue from both adapters, before sync and after a team move, gives one citation with two refs |
| R4-7 | LOW | consistency | "Stages and their dependencies", lines 724-725 | "A stage may start once each dependency is merged or open as a review-clean PR" contradicts the at-most-one-unmerged rule under "Stacking" | Change it to "…once its dependencies are merged, or all but one are merged and that one is open as a review-clean PR (Stacking, below)" |

## Edge Case Matrix

| Boundary | Dependency unavailable | Invalid / malicious input | Load 10x | State leakage |
|---|---|---|---|---|
| Sindri Host ↔ `Aw.Context` (facade, compose, lock port) | OK: §8.6 exit 2 / `SND-CONTEXT-004`; exit 75 / `-040` | OK: §4 `CheckSettings` with key paths | OK: one facade; `compose` limited to `Add…()` | R4-1 (lock adapter tier rule), R4-5 (carve-outs) |
| Source adapters (repo-docs, Linear, Onyx) | OK: §5.1 `Unavailable` → `degraded`; `-011` when all are down | OK: §5.1 argument validation; §7 sanitize, scrub, permalink allowlist | OK: §8.3 caps | R4-4 (pending-keyed decisions), R4-6 (double citation before sync) |
| `context.db` (Kernel.Data) | OK: §6.2 batched build, then flip | OK: FTS phrase quoting; loader form matches `EmbeddedMigrations.cs:69` | OK: §6.2 retention and purge | OK for the real home (R3-1 fix); R4-2 (a) for the shared owner-check home |
| Egress (HTTP / CLI / MCP) | OK: §8.4 `degraded` | OK: §7 per-field scrub, one escaper; §3 `LoopbackGuard` (`LoopbackGuard.cs:3-17`) | OK: §8.3 32k cap | OK: §3 `--scope local`; `context.mcp.enabled` false by default |
| Cascade / planner | OK: graph-only results when sources are down | OK: depth ≤ 4, path ≤ 8 | OK: §6.1 narrow ascend | OK: visited set |
| Eval harness | OK: refs re-fetched per run | OK: `SND-CONTEXT-005` | OK: deterministic, cached cut maps | OK: §14 round 3 (created files excluded from truth, by decision) |
| Parallel delivery and owner checks | OK: single-parent stacking (R4-7 text only) | n/a (the input is the spec) | OK: caps of 3 building, 4 ready, depth 2 | R4-2 (port, MCP target, reset), R4-3 (no export to seed) |

Findings: R4-1 to R4-7 above.

## Missing Error Handling

- Owner checks have no build-identity check. A check that reaches the real instance through the work repo's MCP registration
  passes against the merged build and says nothing (R4-2 c).
- `SND-CONTEXT-004`'s fix text ("rebuild with `context map build`") is still the only guidance for an AW-DATA sequence or
  checksum refusal. For the owner-check home the right fix is "delete the scratch home". Give that cause its own message.

verdict: NEEDS_WORK
