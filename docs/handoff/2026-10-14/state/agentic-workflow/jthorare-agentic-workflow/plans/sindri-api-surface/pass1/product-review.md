# Product Review: Sindri API surface — commands, queries, the event stream and the MCP server

_Reviewed by `/productReview` on 2026-10-10 | Mode: mvp_

Plan: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (branch `docs/sindri-api-surface`, commit 831409c).
Cross-checked against the S1 worktree (`.claude/worktrees/sindri-s1-app-shell`, head a363f22, read only) and the workflow engine spec (`docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`).

Owner decisions taken as given and not reopened: CQRS (D1), local-only v0 auth with no tokens, passkeys or tailnet (D4), webhooks cut (D7), the MCP server proven by a real Claude Code subagent (D6), and a threat model that guards honest mistakes, not adversarial agents.

## Verdict: ITERATE

One HIGH concern (#1) sets the verdict at ITERATE. P7's item desk commands (`HoldItem`, `ParkItem`, `UnparkItem`, `HandBack`) and P9's `StartItem`/`CancelInstance` define park, takeover and handback a second time, on items. The workflow engine spec, written the same day, already owns them on instances (`command.answer` with `grant`, `command.takeover`, `command.handback`, `sindri takeover|handback <item>`). If both specs ship as written, there are two state machines and two owners for the `sindri handback` verb. The rest of the plan is a sound, well-phased spine. The MEDIUM concerns (#2–#5) cover scope sequencing, a stale S1 baseline, a P4 loop that has no effect before S2, and an auto-start inconsistency. None of them blocks once #1 is reconciled.

## Lens checklist (mvp)

| Lens item | Result |
|---|---|
| Scope check | 10 phases, 30 routes, 8 MCP tools. The UI needs only P1–P5 (the spec says so itself). P6 (jobs, Ctrl-C cancel), P8 (MCP), P10 (`sindri open`) and the P7 desk verbs come ahead of any consumer. Cut or defer P7 (#1) and P10 (#2). Trim the P1/P5 limits machinery for one local user (#6) |
| Persona clarity | Clear: one owner at the desk (`human`). `session` and `service` are caller classes, not personas. §5.1's notifier, phone head and other instances are labelled "later" and add no scope |
| Timeline reality | 7 M-sized and 2 S-sized PRs, gated on S1 merging, each with a review loop to zero issues. With 3 implementers and 2 heavy-lock slots, W1–W2 (P1, P2, P3, P5, P6) fit in about 2 focused weeks. The full P1–P10 set doesn't. Name P1–P5 as the v0 milestone ("the UI can render live state") and treat the rest as follow-on waves (#2) |
| Riskiest assumption | That the API's item-level model (item states, desk verbs, needs-you rows) matches what the engine will run on instances. Evidence already says it doesn't fully (#1). Test it before building: put the two specs' state and verb tables side by side and agree on one owner per verb and event before P3's `item_view` columns are frozen |
| Build vs. skip | Skip in v0: `IdempotentService`'s body-mismatch check (`SND-API-005`) and the 16-stream cap / 429 (`SND-API-004`), since the server is single-user and loopback-only. Skip P10's Warp launcher here because `ILauncher` belongs to the engine's Step runtimes (#2). Keep `reset` on overflow (cheap and correct). Keep `X-Sindri-After`, because the async projector requires it |

## Strengths

1. **One authorization table, two enforcement layers.** The action class × caller class table (§4.2), plus an MCP tool list with no decision, desk or protected tools, is a clean answer to "sessions request, humans decide". It already matches the honest-mistake threat model, and switching from a header to tokens later changes only where the caller class comes from (§6).
2. **Every phase ends in an observable signal.** Each phase lists a concrete `curl` or CLI result as its proof (for example P1's 403 that writes only the audit row, and P4's "decided elsewhere"), and the P8 prover is a real Claude Code subagent. Each phase can be checked by hand.
3. **Projections can be rebuilt instead of migrated.** `views.db` is kept apart from `ledger.db`, rebuilt on a schema change, and has a byte-for-byte rebuild test. A new or changed read model becomes a replay rather than a migration, which keeps the cost of iterating on the UI's views low.
4. **Conflicts with S1 are listed, not silently redesigned** (§10), and error codes and typed IDs are reserved per phase. That makes the agent-team plan with stacked branches realistic.

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | HIGH | **P7 and P9 duplicate the workflow engine's park, takeover and handback model.** The API spec puts `ParkItem`/`UnparkItem(hint)`/`HoldItem`/`HandBack` on items, emitting `item.state-changed`/`item.taken-over`/`item.handed-back`, with CLI `sindri park|unpark|hold|handback <item>`. The engine spec (§3 transitions, §5.4, its Phase 5) puts `parked(reason)` → `command.answer` (optionally with `grant`, W7), `command.takeover`/`command.handback` (which pause and re-arm deadlines, W15) and CLI `sindri takeover|handback <item>` on instances. Both specs claim `sindri handback`. The API has no command that answers a park with a grant, which is the human's main gate interaction. `hold` has no counterpart in the engine at all | Pick one owner per verb before P3 freezes `item_view`. Suggested split: the engine owns instance state and its commands, and the API spec exposes them as thin command slices (`AnswerPark{grant?}`, `TakeOver`, `HandBack`) that call the engine's commands in-process, landing with S2 (merge P7 into P9). The item view projects instance state rather than keeping its own. Drop `HoldItem` unless the engine adds a matching state. Add a cross-spec verb/event table to both docs |
| 2 | MEDIUM | **Scope runs ahead of any consumer, and one phase sits in the wrong spec.** Neither the UI (S1b) nor the engine (S2) exists yet, but the plan builds P6, P8 and P10 too. P10's `ILauncher`/`WarpLauncher` is what the engine's Step runtimes wait on (engine W18: "Step runtimes failing closed until `ILauncher` exists"), so it belongs on the engine's critical path, not at the end of the API waves | Define **v0 = P1–P5** (UI unblocked) and ship it first. Keep P6 and P8 as the next wave (D6 stands; this only changes sequencing). Move `ILauncher` to the engine or a launcher spec and keep only the `OpenItem` slice here |
| 3 | MEDIUM | **The S1 baseline in §3.2 and §10 is stale against S1 head (a363f22).** (a) S1 has no `POST /profile/validate` route: `ValidateProfile` runs in-process from `Host/Commands/Verbs.cs`, so classing it as `job` "so `ARCH-CQRS-001` holds without changing S1's route" works around a route that doesn't exist. (b) C1 is already fixed: `EndpointTable.cs` maps `group.MapGet("/status", …)` inside `MapGroup("/sindri")`. (c) S1 maps `GET /ping` (`PingProcessRequest → Ping`), which isn't in §3.1, so `ARCH-CQRS-001` and the registry test will hit it unplanned | Re-baseline §3.2, §10 and P1's file list against S1's actual `EndpointTable.cs`. Either expose validation as a query (`GET /profile/validation`) or leave it CLI-local and drop the `job` workaround. List `GET /ping` as an S1 query. Close C1 |
| 4 | MEDIUM | **P4's request → decision loop has no effect before S2.** Accepting a `rescope` or `add-task` request records `request.accepted` and nothing else, "a recorded fact that the engine acts on once it does". So P4 proves the plumbing, but the owner gets nothing usable from deciding, and the needs-you queue fills with decisions that have no effect | Either make two kinds take effect now (an accepted `add-task` runs `CreateItem` in-process as a `service` caller; an accepted `note` shows on the item's timeline), or say plainly in P4 and in the UI copy that accepting is recorded only until S2. Don't show inert decisions as "needs you" without that label |
| 5 | MEDIUM | **The auto-start rule contradicts itself for sessions (§7).** `sindri queue` run by an agent (`CLAUDECODE` set, so caller `session`) spawns the human's `sindri serve`, while `sindri mcp` refuses because "the process lifecycle is the human's". The same caller class gets two behaviours | Make it a rule per caller class: a `session`-class CLI never starts the process and prints the same `SND-API-001` text as MCP. Add it to P1's CLI tests next to the session-header rule |
| 6 | LOW | **P1 and P5 carry multi-client hardening for a single-user loopback server.** The body-mismatch detection in `IdempotentService` (`SND-API-005`) and the 16-stream cap with 429 (`SND-API-004`) guard against problems one local owner and their own agents won't create in v0 | Defer both to the token or tailnet phase (§11). In v0, keep the key-based dedupe (`AlreadyRecorded`) and `reset` on overflow |
| 7 | LOW | **Owner friction from the session check.** The owner works in Claude Code sessions. Typing `!sindri decide …` inside one has `CLAUDECODE` set, so it is refused as `session`. That's correct under the guard, but the fix line has to say where to go instead | Make the `SND-AUTH-002` fix line name both routes ("run it in a plain terminal tab, or decide in the Sindri app"), and cover it in the P1 CLI test |
| 8 | LOW | **The engine spec expects a ledger read surface that this spec doesn't list.** Engine S1-D6 needs "a ledger read endpoint (`ReadAfter`, filterable by item) and a `sindri ledger` verb". This spec offers only the per-item timeline and the event stream | Either point the engine spec at `GET /items/{id}/timeline` plus `GET /events`, or add a `GET /ledger?after=&item=` query to a phase, so the two specs agree |

## Scope Suggestions

### Cut
- **P7 as a separate item-level phase.** Fold its verbs into P9 as thin wrappers over the engine's instance commands (#1). Drop `HoldItem` unless the engine models a hold.
- **`ILauncher`/`WarpLauncher` from P10.** Move it to the engine or launcher work that needs it first (#2).
- **The `SND-API-005` body-mismatch check and the `SND-API-004` stream cap** for v0 (#6).
- **The `job` classification of `ValidateProfile`.** It works around a route S1 doesn't have (#3).

### Keep
- **P1's authorization table, `AuditingService` and `CommandRegistry` + `GET /commands`.** They are the backbone of "sessions request, humans decide" and the UI's action list.
- **P2's rebuildable projections and `X-Sindri-After`.** These keep the async projector honest.
- **P5's public-event registry and `reset`.** They keep request, packet and session text off the wire, and they are the UI's live signal.
- **P8 with the real-subagent proof run** (owner decision). Sequence it after v0.

### Add
- **A cross-spec ownership table** (verb → command → event → owning spec) shared by the API and engine specs, covering at least start, cancel, park/answer, takeover, handback and the ledger read (#1, #8).
- **A named v0 milestone: "P1–P5 merged, so the UI can render live state",** with its own end-to-end signal: the UI shell shows the queue and updates within 100 ms of `sindri item add`.
- **One explicit line in P4 about what an accepted request does before S2** (#4).

## Key Questions for the Team

1. Who owns park, takeover and handback: the item (API spec) or the instance (engine spec)? Is "hold" a real state the engine will honour?
2. Which spec delivers `ILauncher`, given that the engine's Step runtimes fail closed without it?
3. Before S2, should accepting an `add-task` or `note` request do anything, or is "recorded only" acceptable to show in the needs-you queue?
4. Should a `session`-class CLI ever start the human's process?

## Recommended Next Action

Before `/specToProvenPR` dispatches W1, add a §10b "Conflicts with the engine spec" table to this document that resolves #1 and #8. Re-baseline §3.2 and §10 against S1's `EndpointTable.cs` (#3). Then mark P1–P5 as the v0 milestone and start W1 (P1, P5, P2), which concern #1 doesn't touch.

verdict: NEEDS_WORK
