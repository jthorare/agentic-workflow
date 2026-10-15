# Architecture Review (pass 5, final): Sindri API surface

_Reviewed by `/archReview --slim` on 2026-10-10_

**Target:** `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` at `11500b8` (the pass-4 edit).

**Checked against:**
- S1's code at `.claude/worktrees/sindri-s1-app-shell` (read only, HEAD `5c9c4a4`, unchanged since pass 4).
- The pass-4 review (`pass4/arch-review.md`) and §13 "Pass 4 → final text".
- The engine spec at rev 4 (`f36f2c1`; it has not changed since).

**Scope:** pass-4 findings that are still open, and new issues the pass-4 edit introduced. Owner decisions are not
raised again.

## Verdict: SOUND

Every pass-4 finding is resolved in this spec's text (table below). P4-3 is now ordered by a hard gate in the wave
table, and the remaining work belongs to the engine spec. No finding is HIGH. The pass-4 edit opened two MEDIUM gaps:
- **N1:** the new failure-row clearing rule can't be computed from the ledger.
- **N2:** the "no context" change leaves `EventFactory`'s own no-context behaviour unstated, and the P6 job runner is
  missing from the list of writers.

Both need a sentence of spec text, and neither stops a phase signal from passing. The rest are LOW text drift.

## Scores

| Dimension | Score (1–5) | Notes |
|-----------|:---:|-------|
| Complexity | 4 | The internal services, the `service:host` scope and the rebuild fast-fail are now explicit. N2 is the last unnamed writer |
| Scalability | 4 | Single user. The projector no longer sleeps between full pages (L13 is fixed) |
| Maintainability | 4 | The §4.2 table, C7 and C8 now match S1. Some text drifted after the edit (N4, N5) |

## Pass-4 resolution check

| Pass 4 | Status | Evidence |
|---|---|---|
| P4-1 (MEDIUM): a manual run never clears the failure row | **Resolved** | §3.4 adds a `service.ran` (manual) row keyed `service.ran:<name>:manual:<jobId>`. §3.6 clears on a scheduled or manual `ok`. P6 has a signal for it. The slot detail is N3 |
| P4-2 (MEDIUM): `StartProcess` and `RecordHeartbeat` are unclassified | **Resolved** | §2.1 "S1's internal services stay outside the chain". This matches S1: `ServeCommand.cs:39` calls `StartProcess` directly, and `RecordHeartbeatService` appends through `EventFactory`. The suggested exact-list case in `ARCH-CQRS-003` was not added; that's optional |
| P4-3 (MEDIUM): the engine's competing caller model | **Resolved on this side; engine action still owed** | The wave table has a `Gate` row: engine P2's contract unit waits for this P1's contract unit. §3.5 names rev 4. The engine spec rev 4 is unchanged: it still has `human-terminal` and `SND-WF-007`, and its wave table has no gate. Nothing more is needed in this spec |
| L10: §4.2 doesn't show `service:host` | **Resolved** | The `desk` row now reads "and `service:host` → `StopProcess` (§2.1)" |
| L11: a stale C8 note on the approval key | **Resolved** | C8 cites S1 `5c9c4a4`. This matches `ApproveProfileService`'s key `profile.approved:<hash>:<previous or none>` |
| L12: `profile-watch` state after a restart | **Resolved** | C8: "On restart, `profile-watch` takes its last (approved, live) pair from the latest `profile.checked` event" |
| L13: the projector sleeps between full pages | **Resolved** | §2.3: it continues at once after a full page, and `ReadsViews` commands fail fast during a rebuild. The error-table wording is N4 |

## Findings (new in the pass-4 edit, or unresolved)

| id | severity | location | finding | recommended fix |
|---|---|---|---|---|
| N1 | MEDIUM | §3.6 `failure` row: "…or a `profile.approved` whose profile no longer names the service"; §2.3 "Projections are pure folds"; S1 `Core/Events/ProfileApproved.cs:8` | S1's `profile.approved` payload is `(Hash, Changes)`, and `Changes` holds key paths such as `services.<name>.every`. A changed cadence and a removed service produce the same path, so a pure fold over the ledger can't tell whether the approved profile still names the service. The only other source is `IApprovals`, which holds the latest snapshot. Reading it during a replay gives today's profile, not the one approved at that sequence, so a rebuild can produce different rows. The P4 signals don't test this path, so it would ship undecided | Pick one and write it into the spec: <ul><li>P2 adds the service names to the `profile.approved` payload, as an additive S1 change with a C-table row. This is the cleaner fold.</li><li>Or `GetNeedsYou` drops `failure` rows at query time for services the approved profile doesn't name. `ARCH-CQRS-002` forbids `IApprovals` in queries, so this needs a read-only port.</li></ul> |
| N2 | MEDIUM | §2.1 "Only `ServeCommand` sets `service:host`" and "No context is refused" (the "`EventFactory` defaults to `service:host`" sentence was removed); C6; §3.4 job events; §4.1 `job` class | The edit removed the only statement of what `EventFactory` does when no caller context is set. Refusal applies only in `AuthorizingService`. `EventFactory` still runs with no context in two places: <ul><li>`AuditingService` writes the audit row for a refused no-context command, with caller `none`. What actor does its envelope carry?</li><li>The P6 job runner appends `job.progress`, `job.finished` and the manual `service.ran` after `RunService` has returned.</li></ul> The job runner isn't in the "every other in-process writer" list. Whether `AsyncLocal` flows into the job depends on how the runner is started: `Task.Run` from the handler flows it, a queue consumer doesn't. The C6 implementer must pick a behaviour (throw, or a sentinel actor), and either choice breaks one of those paths | Add one line to C6: "With no context, `EventFactory` stamps `host:sindri` (S1's actor) and the audit caller is `none`." Add "the job runner, which runs each job under `service:job` (or the starting caller's actor)" to the §2.1 writer list |
| N3 | LOW | §3.4 `service.ran` (manual) "slot `manual:<jobId>`"; §4.1 `job`: "writes only its own job events"; S1 `Core/Scheduling/ScheduledRun.cs:8` | A manual run still calls `IScheduledService.Run(ScheduledRun)`, and S1's `ScheduledRun.Slot` is a `DateTimeOffset`. The service's own events, such as `service.heartbeat:<epoch>:<slot>`, need a real timestamp. Only `service.ran` can carry `manual:<jobId>`, so that payload's `slot` becomes a string with two shapes. Separately, a `job`-class command whose run appends `service.ran` and the service's own events contradicts "writes only its own job events" | Say that the job passes `ScheduledRun(grant, now)` to the service, and that `service.ran.slot` is a string: an ISO time, or `manual:<jobId>`. Reword §4.1's `job` row to "its own job events and the service run's events" |
| N4 | LOW | §2.3 "fail at once with `SND-API-004` ('views rebuilding')"; §2.1 "Waiting for the views" bullet; §9 error table | `SND-API-004` gained a third meaning, but its error-table row still says only "didn't reach … within 2 s". The §2.1 bullet that lists the fast-fail cases names `stalled` but not `rebuilding`. An implementer reading §2.1 alone makes rebuilding commands wait 2 s | Add "or views are rebuilding (fails at once)" to the `SND-API-004` row and to the §2.1 bullet |
| N5 | LOW | §12 step 2 "the `A.cs` builder method"; P0 signal "only slice files and its own PublicAPI lines change" vs C7 and §12 step 1 | After the edit, the generator writes the `A.cs` method and the `IdsJsonContext` entry, but step 2 still has the implementer add the `A.cs` method. P0's signal still asserts that only slice files and PublicAPI lines change. A prover following P0's signal would assert something C7 says is false | Drop `A.cs` from step 2. Update P0's signal to "slice files plus its own PublicAPI, `A.cs` and `IdsJsonContext` lines" |
| N6 | LOW | §3.1 "The repo name … `git rev-parse --git-common-dir`" | <ul><li>Run in the main checkout, the command prints a relative `.git`, so it needs `--path-format=absolute` or resolving against the working directory.</li><li>"Whose path contains" is ambiguous when profile repos nest, so it needs the longest match.</li><li>A submodule's common dir is under the superproject's `.git/modules/`, so a submodule session resolves to the superproject.</li><li>The text doesn't say which profile the CLI and `sindri mcp` read, live or approved.</li></ul> | One sentence: an absolute common dir, the parent of `.git` (for a submodule, the superproject), the longest containing path, and the approved profile |
| N7 | LOW | §3.1 `ReadLedger` for `session`: "only events whose item is in the session's repo, or that have no item" | `request.filed` and `decision.recorded` for item-less requests carry the repo in the payload, not as the item. So a session's ledger read shows other repos' request and decision events, while §3.1 scopes those rows by `request.filed.repo`. The text is left out of the public payload, so only ids and kinds cross repos. Under the honest-mistake model this is a consistency gap, not a leak | Scope by the event's item repo, else its payload `repo`, else unscoped. Use the same rule for the session SSE filter (§5.2) |

## Edge case matrix (pass-4 edit boundaries)

| Boundary | Dependency unavailable | Invalid/malicious input | Load 10x | State leakage |
|---|---|---|---|---|
| `ServeCommand` → `StartProcess`, `StopProcess` | OK: `StartProcess` is outside the chain (§2.1, `ServeCommand.cs:39`), and the stop is `service:host` through the chain (`:64`, `:88`) | OK: no input | OK | OK: `service:host` is set only there |
| `AuthorizingService` and `EventFactory` with no context | N2 (what `EventFactory` stamps) | OK: refused, `SND-AUTH-002` | OK | N2 (the job runner's actor) |
| `RunService` job → `service.ran` (manual) | OK: written when the job ends | N3 (the slot shape) | OK: one per job | OK: the key is unique per job |
| needs-you `failure` projection | OK | OK | OK | N1 (replay depends on the approvals store) |
| Projector rebuild → `ReadsViews` commands | OK: fail fast (§2.3) | OK | OK: no sleep between full pages | N4 (the code's text) |
| CLI and MCP repo resolution | OK: outside any profile repo, the header is omitted | N6 (relative path, nesting, submodules) | OK | N7 (`ReadLedger` and the stream scope) |
| Engine ↔ API caller model | OK: the wave-table gate | OK | OK | Engine-spec edit owed (not this spec) |

## Suggested Improvements (Prioritized)

| Severity | Improvement | Effort | Impact |
|----------|------------|--------|--------|
| MEDIUM | N1: carry the service names in `profile.approved`, or filter at query time | S | The failure row's clearing survives a rebuild |
| MEDIUM | N2: state `EventFactory`'s no-context actor, and name the job runner's caller | S | C6 and P6 have no undecided branch |
| LOW | N3–N7: text fixes | S each | Fewer mid-phase questions |

verdict: PASS
