# Architecture Review (pass 4): Sindri API surface, final edit

_Reviewed by `/archReview --slim` on 2026-10-10_

**Target:** `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (final edit, `0617aae`).

**Checked against:**
- S1's code at `.claude/worktrees/sindri-s1-app-shell` (read only). Its HEAD is now `5c9c4a4`; pass 3 read `a7d2ada`.
  The two new commits are `d302a1c` (a test) and `5c9c4a4` (the `ApproveProfile` order and key).
- `.agents/rules/csharp.md`.
- The engine spec at rev 4 (`f36f2c1`, committed after this edit).

**Scope:** pass-3 findings that are still open, and new issues the final edit introduced. Owner decisions are not
raised again.

## Verdict: SOUND

Both pass-3 HIGHs are resolved:
- **R1:** §2.1 now routes host-internal stops through the chain as `service:host`. The P1 signal "Ctrl-C … records
  `process.stopped`, and an immediate restart takes the lease" tests that path directly, and it matches S1's
  `ServeCommand.cs:64,88`.
- **R2:** `service.ran` now carries an outcome for every scheduled run, keyed `<name>:<epoch>:<slot>`, and P2 and P4
  have a fail-then-succeed signal. That key fits S1's `SchedulerHost.RunOne`, which has the grant's epoch and the slot.

R3 and R4 are resolved as well. R5 is resolved on this spec's side, but the engine spec still hasn't made its edit (P4-3
below).

No finding is HIGH. Three new or remaining MEDIUMs each need a sentence or two of spec text. None stops a phase signal
from passing.

## Scores

| Dimension | Score (1–5) | Notes |
|-----------|:---:|-------|
| Complexity | 4 | The chain, the gate and the head wait are stated and bounded. In-process services outside the chain are unnamed (P4-2) |
| Scalability | 4 | Single user. The projector has a page and sleep wording nit (L13) |
| Maintainability | 3 | The engine spec's caller model still differs (P4-3). The §4.2 table doesn't show the `service:host` exception (L10) |

## Pass-3 resolution check

| Pass 3 | Status | Evidence |
|---|---|---|
| R1 (HIGH): an in-process stop is refused by the chain | **Resolved** | §2.1 "Host-internal callers", the P1 signal and test, and `EventFactory` defaulting to `service:host`. S1 `ServeCommand.cs:64` (port in use) and `:88` (signal) both resolve `IService<StopProcessRequest,…>` from a scope, so after `AddCommand` they reach the chain as described. See L10 for the table wording |
| R2 (HIGH): no success event | **Resolved for scheduled runs** | `service.ran` with an `ok` or `failed` outcome (§3.4, C8), the clearing rule (§3.6), and P2 and P4 signals. The new gap for manual runs is P4-1 |
| R3: the command head lags | **Resolved** | §2.1 and §2.3: commands read `ILedgerStore.Head()`, and fail fast with `SND-VIEW-001` while the projector is stalled |
| R4: `profile-watch` is never scheduled, and its key collides | **Resolved** | It is a hosted service. `profile.checked` is written on every (approved, live) change, keyed by the previous check, and a revert writes `drifted: false` (C8). See L12 for the start-up state |
| R5: the engine's caller model | **Open across the specs (P4-3)** | This spec's §3.5 now lists the exact edit. Engine rev 4 still has `script`, `system`, `human-terminal` and `SND-WF-007` (engine lines 46, 249–259, 509–513, 669), and its signals expect `SND-WF-007` (lines 1123, 1167) |
| L6: a refused `stop` isn't audited | **Resolved** | "A refused `stop` is audited like any other command" (§2.1) |
| L7: the context when none is set | **Resolved** | `service:host` (§2.1) |
| L8: how the gate knows a command reads views | **Resolved** | The `ReadsViews` catalog flag |
| L9: repo scope for session commands | **Resolved** | §3.1, and `request.filed.repo` |

## Findings (new in the final edit, or unresolved)

| id | severity | location | finding | recommended fix |
|---|---|---|---|---|
| P4-1 | MEDIUM | §3.6 `failure` row ("cleared by the service's next `service.ran` with `ok`"; `primaryAction` → `RunService` from P6); §3.2 `RunService` → `JobStarted`; §3.4 job events | Only the scheduler is said to append `service.ran`. In P6 the `failure` row's own primary action, `RunService`, runs the service as a job, and the spec gives that job only `job.*` events. A manual run that succeeds therefore leaves the `failure` row, and `lastRun`, unchanged until the next scheduled slot. That slot may be hours away for a daily service. Pressing the row's button doesn't clear the row | Have a `RunService` job also append `service.ran` when it finishes, with the slot `manual:<jobId>` (the key is then unique). Or say that `job.finished` for kind `service` clears the row too. Add a P6 signal: after a failure, `services run <name>` succeeds and the `failure` row is gone |
| P4-2 | MEDIUM | §2.1 "the catalog allows `service:host` for `StopProcess` and nothing else"; C2 "registrations move to `AddCommand`"; S1 `ServeCommand.cs:39` (`Call<StartProcessRequest,…>`), `SindriFeaturesServiceCollectionExtensions.cs:27,29` (`StartProcess`, `RecordHeartbeat`) | S1 has two more in-process services that append events: `StartProcess` (`process.started`, called from `ServeCommand` before listening) and `RecordHeartbeat` (from the scheduler). The spec never says whether they are `ICommand`s. If P1 registers them with `AddCommand`, as C2 and D1's "commands append one domain event" suggest, then `service:host` is refused for `StartProcess` ("nothing else"), and `serve` can't start. Leaving them out is also undecided: then `ARCH-CQRS-003` and the audit don't cover them | Add one line to §2.1 or C2: "`StartProcess` and `RecordHeartbeat` are host-internal services, not `ICommand`s. They stay plain `IService` registrations outside the chain, and stamp `service:host` and `service:heartbeat`." Add a case to the `ARCH-CQRS-003` test that checks this list is exact |
| P4-3 | MEDIUM | §3.5 auth row; engine rev 4 lines 46, 249–259, 509–513, 656, 669, 1123, 1167 | R5 is still open. Engine rev 4 landed after this edit and still has its own model: the `script` and `system` classes, `human-terminal`, `SND-WF-007`, and `SND-RECIPE-004` for "human without a TTY". Its P-signals expect `SND-WF-007` where this spec's chain returns 403 `SND-AUTH-002`. Whichever spec's P1 lands second fails its own signal, or reworks the other's | This is now an engine-spec action: make the edit §3.5 lists before engine P2 or P4 starts. Or record in both specs which one wins. Nothing more is needed in this spec |
| L10 | LOW | §4.2 table, `desk` × `service` ("only the engine commands its recipe step owns") | The table doesn't show the `service:host` → `StopProcess` exception that §2.1 relies on. An `AuthorizingService` built from the class table alone would refuse the Ctrl-C stop. The P1 signal catches it, so this costs a red build, not wrong behaviour shipped | Add "and `StopProcess` for `service:host`" to that cell. Say that `AuthorizingService` checks the catalog row's allowed callers, not the class alone |
| L11 | LOW | C8 "S1's `profile.approved:<hash>` key needs no change" | Stale. S1 `5c9c4a4` changed the key to `profile.approved:<hash>:<previous approved hash or none>`, so A → B → A records three events. That helps `profile-watch` (the re-approval is in the ledger), but the sentence is now wrong | Update the C8 bullet to S1's new key |
| L12 | LOW | C8 `profile-watch`: "keyed by the previous check", "only when the pair has changed since its last check" | Its state after a restart is unstated. If the watcher starts with no last check in memory, it either re-appends with a key that collides (and gets dropped) or misses a drift that happened while the process was down | On start, read the latest `profile.checked` from `ILedgerReader` and use its sequence and pair as the "last check" |
| L13 | LOW | §2.3 "Between pages it sleeps on the ledger signal, or for 1 s" | Read literally, the projector sleeps even when a page comes back full, so a rebuild of N events takes at least N/100 seconds. Before P5's signal, a `ReadsViews` command also waits up to about 1 s after any earlier append, which includes the previous command's audit row. While a rebuild runs, such commands time out with `SND-API-004` instead of failing fast | Sleep only when a page comes back short. During a rebuild, fail `ReadsViews` commands fast with `SND-VIEW-001`'s "rebuilding" text, as for a stall |

## Edge case matrix (final-edit boundaries)

| Boundary | Dependency unavailable | Invalid/malicious input | Load 10x | State leakage |
|---|---|---|---|---|
| Host-internal calls → chain (`ServeCommand`) | OK: `service:host` stop and the P1 signal (§2.1); `StartProcess` is P4-2 | OK: no input | OK | OK: `EventFactory` defaults to `service:host` |
| `CommandGate` and head wait | OK: 5 s → `SND-API-009`, 2 s → `SND-API-004`, `ILedgerStore.Head()` | OK | L13 (rebuild and pre-P5 latency) | OK: the head comes from the store |
| `AuthorizingService` | OK: in memory | OK: `SND-API-007/008` | OK | L10 (the table vs the catalog) |
| Scheduler → `service.ran` | OK: every run, with the epoch in its key | OK | OK: one per slot | P4-1 (manual runs) |
| `profile-watch` → `profile.checked` | L12 (restart) | OK | OK: writes only on change | OK: keyed by the previous check |
| Engine ↔ API caller model | P4-3 | OK | OK | P4-3 |

## Suggested Improvements (Prioritized)

| Severity | Improvement | Effort | Impact |
|----------|------------|--------|--------|
| MEDIUM | P4-1: a `RunService` job appends `service.ran` (slot `manual:<jobId>`), plus a P6 signal | S | The needs-you button clears its own row |
| MEDIUM | P4-2: name `StartProcess` and `RecordHeartbeat` as host-internal services outside the chain | S | P1 can't break `serve` start-up |
| MEDIUM | P4-3: the engine spec makes its §6.2 and §6.3 edit | S | No P1 signal conflict across the specs |
| LOW | L10–L13 | S each | Fewer mid-phase questions |

verdict: PASS
