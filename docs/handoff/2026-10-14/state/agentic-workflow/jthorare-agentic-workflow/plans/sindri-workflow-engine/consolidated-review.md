# Consolidated Plan Review — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`
**Reviewed:** 2026-10-10
**Lenses applied:** product · arch · design · devex · security

## Normalized findings

Severity per `_shared/severity.md`. Lens ids: P = product, A = arch, D = design, X = devex, S = security.
Overlapping findings are merged into one row, listing every lens that raised them.

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| C1 | HIGH | arch F1/F3, sec M7 | §5.3, W12 | The idempotency key names the instance version, not the trigger. A redelivered or late trigger applies again at the next version. `Commit` checks the version before the key, so a replay returns a conflict | Key on the trigger's identity (command nonce, step-run token, source event seq, deadline id). Check the key first. Refuse step results whose state entry is stale |
| C2 | HIGH | arch F2, F4 | §3.4, §5.2 | A refusal uses the transition's key, so a later pass is blocked. After a refusal nothing re-runs, and the instance sits until timeout | Each retry is a new trigger. Define what follows a refusal: re-run within the loop, or park |
| C3 | HIGH | arch F5 | §5 | Nothing delivers external ledger events (`item.routed`, `claim.created`) to the instance: no cursor, no matching, no buffering | An event router with a durable cursor feeds the engine inbox. Rows are kept until the instance can take them |
| C4 | HIGH | arch F6, devex 2 | §3.3, `RCP-WHEN-001` | Event waits don't declare their outcomes, so `workflow.item` fails its own rule. Event kinds aren't checked | Register `EventDescriptor`s (kind, outcomes, facts) in the catalog |
| C5 | HIGH | arch F7 | §5.1 | A single `resumeTo` can't hold a takeover of a parked instance | A resume stack |
| C6 | HIGH | arch F8, F15 | §5.5 | Child gaps: resume isn't propagated, an answer at the parent strands the child, cancellation isn't propagated, a busy parent has no rule, the child start can strand, child ids collide | Complete the propagation table. Create the child in the parent's commit. Child id includes the parent's entry |
| C7 | HIGH | product 1, design F5 | §10, phases | No phase ends in a useful outcome: Steps fail closed until `ILauncher` exists, and a `needs-answer` park can't be answered usefully | A desk runtime: the human does the Step in their own session and reports completion |
| C8 | HIGH | design F1, arch F22 | §3.7, W6 | Universal states have no `next` text or outcome; `escalated` has no way out; `{reason}` never renders | Engine-owned `next` texts, outcomes `cancelled`/`escalated`, exits for each |
| C9 | HIGH | design F2, F3, F4 | §4, §6 | The answer flow, what the approve verb shows first, and the `status` layout are unspecified | A CLI surface section with layouts and the approval preview |
| C10 | HIGH | devex 1, arch F16 | Phases | Phase signals can't pass in their own phase (descriptors in P2, evals in P3, fixture blocks not in the binary, the P5 contradiction, an unlisted verb, P7 size) | Re-cut what each phase ships; dev recipes use real blocks |
| C11 | HIGH | devex 3, 4 | §6 | The CLI process model (in-process vs `serve`) and profile-listed services are unstated; instance addressing is ambiguous | State them; an address grammar |
| C12 | HIGH | devex 5, arch F18 | §2 | Block authoring incomplete (where blocks live, how they're declared, tier rule); `runtime.Register(id, factory)` is service location | Blocks are DI services exposing their descriptor; a `BlockRuntime` built from `IEnumerable<…>` |
| C13 | HIGH | devex 6, product 4 | §4 | A block descriptor can change under an approved recipe | Approval pins the descriptor hashes of the named blocks; start refuses on a mismatch |
| C14 | HIGH | devex 7 | §6a | Error codes were pointed at a generated TS doc (`docs/sindri/errors.md`) | Use `docs/csharp/sindri.md` |
| C15 | HIGH | devex 8, product 6, arch F17 | §6.2 | No PR owns the S1 dependencies; S1-D3 is out of date; `InsertOnce` commits; `EventFactory` hardcodes `Item: null` | Each engine phase owns its S1 delta; correct the table |
| C16 | HIGH | sec H1 | §4 | A generated recipe can drop gates or evidence around a kept effect, lower thresholds, or raise budgets and caps | `RCP-CAP-005`/`006`; Sindri code picks the base; the freeze shows the diff |
| C17 | HIGH | sec H2 | §3.2 | A capability `with:` parameter can take `{ fact }`, so run-time output sets a capability | `RCP-CAP-007`: literals or profile references only; intersect with the profile at run time |
| C18 | HIGH | sec H3 | §3.7 | Nothing says who may fire which trigger; a session can grant itself rounds | Typed trigger sources; grant needs a human at a terminal; capped |
| C19 | MEDIUM | arch F9, sec M5 | §5.4 | The sleep shift is unversioned, can't tell sleep from downtime, has no key, and can starve deadlines | Wall-vs-monotonic detection, a minimum gap, per-instance keyed commits |
| C20 | MEDIUM | arch F10 | §5.2, §8 | Events don't carry the full delta, so the log can't rebuild the row | Events carry the post-state delta; fold test |
| C21 | MEDIUM | arch F11, F12 | §7 | `produces` unchecked; missing rules (outcome coverage, `step.failed`, child cycles, child hash pinning, rung-2/3 without threshold, re-register version) | `RCP-WIRE-002`, `RCP-OUTCOME-001`, `RCP-CHILD-002`/`003`, `RCP-THRESH-002`, `RCP-VERSION-001` |
| C22 | MEDIUM | arch F13, devex | §3.3 | `retry` on a wait doesn't re-send effects; the dispatcher's existence check misreads the instance's own claim | `retry` re-emits the entering effects with derived command ids |
| C23 | MEDIUM | sec M1 | §4 | The "typed confirmation" is only a body flag; the loopback API has no auth | A server-issued challenge; architecture test that UI and MCP have no approve route |
| C24 | MEDIUM | sec M2, M3, M4 | §4, §3.3 | Revoke doesn't cascade; children by id; recipe cycles unbounded | Cascade; children pinned by hash, counted as capability uses; acyclic and a depth cap |
| C25 | MEDIUM | sec M6, arch F19 | §3.8 | Duplicate keys, anchors, merge keys; short/long unions need converters the schema exporter can't describe; canonical form unpinned | Refuse those YAML features; normalize short forms in the reader; RFC 8785 over the normalized document |
| C26 | MEDIUM | sec M8, M9 | §4, §5.2 | Registry actions write no events; gate reasons and inputs aren't scrubbed | Registry events; scrub through `Aw.Redaction` |
| C27 | MEDIUM | product 2, 3 | §9, §10 | No eval recording or block scaffold; v1 never shown to express bugfix and direction check | `recipe eval --record`; validate-only fixtures for both in P1; scaffold answered |
| C28 | MEDIUM | product 5, design F8, F9, F10 | §6 | No "needs me" view or notification; empty states; `GetInstance` lacks allowed commands; `--json` only on validate | `workflow list --needs-me`; park/escalate notify; allowed commands in the API; `--json` on every read |
| C29 | MEDIUM | devex (exit codes, telemetry, YAML positions, eval format, `/new-slice` names, `ApplyTrigger` deps, docs per phase, version rules, dev loop, migration numbers) | §6, §9 | Assorted devex gaps | Addressed in the CLI section, §9 and Phases |
| C30 | MEDIUM | arch F14 | §5.4 | No idle-takeover notification (D §9.3) | An engine-owned `takeoverIdle` deadline |
| C31 | MEDIUM | design F6, F7, F12 | §6 | Revoke has no stated exit; verb namespaces split; `next` mood mixed and a literal `<user>` | Stated in the CLI section |
| C32 | LOW | product 7, 8; design F11, F13, F14, F15; devex LOW; arch F20, F21, F23–F25; sec L1–L8 | various | Host move of approvals, usage metrics, formatting, `--version` optionality, `alreadySatisfied` mapping, redundant field, gate re-run after conflict, command ids, profile dependency, retry policy, indexes, injection, alias bombs, provenance, self-reported cost, `next` hints, crafted bypasses | Answered in §11 |

## Top tensions (cross-lens)

1. **Desk Steps versus "text typed into a session is never a decision"** (product C7 ∥ security C18). Product wants the human to complete Steps by hand before the launcher exists. Security wants trigger sources typed. Resolution: `workflow complete` is a desk verb that needs a human actor (CLI at a terminal), never a session or MCP call. It reports work done; it doesn't grant anything.
2. **Strict `RCP-CAP-*` versus generation's usefulness** (security C16 ∥ product). Supersets and ≤ budgets make generated recipes strictly narrower than their base. Resolution: accept it. Widening is a desk edit of the base (invariant 13), and the freeze shows the diff.
3. **Signals in the real binary versus no fixture code in production** (devex C10 ∥ arch F16). Resolution: dev recipes in `recipes/dev/` use real, harmless blocks (`step.desk`, `effect.notify.desk`); fixture blocks stay in tests.
4. **Closed schema versus short forms** (design/devex readability ∥ arch C25). Resolution: short forms are normalized by the reader before typed binding; the schema and the hash describe the long form only.
5. **Product and design both raise HIGHs; their only tension is C7 vs C8** (a desk Step must not be a `needs-answer` park). Resolution: a desk Step keeps the state `active`, with an imperative `next`.
6. **No tension between arch and devex on C10, C11, C15:** both ask for the same re-cut.
7. **No tension between security and arch on C1/C18:** trigger identity and trigger source are separate fields.

## Per-lens highlights

### Product
- Top wins: fail-closed per block kind; validator messages that name a rule; laptop-friendly timers.
- Top gaps: no useful end (C7), no "needs me" view (C28), block versions unpinned (C13).
- File: `product-review.md`

### Architecture
- Top wins: pure Core decisions; one fenced transaction; deadlines as data.
- Top gaps: idempotency key (C1), refusal liveness (C2), event delivery (C3), resume stack (C5), children (C6).
- File: `arch-review.md`

### Design
- Top wins: closed outcomes; one-line rule errors.
- Top gaps: universal states (C8), answer, approve and status surfaces (C9).
- File: `design-review.md`

### Devex
- Top wins: the S1 dependency table; named rules.
- Top gaps: signals that can't pass (C10), the process model and addressing (C11), block authoring (C12), the errors doc path (C14).
- File: `devex-review.md`

### Security
- Top wins: closed grammar; rung from the descriptor; approval outside the file; recheck at dispatch.
- Top gaps: capability laundering through generated recipes (C16) and facts (C17); trigger authority (C18).
- File: `security-review.md`

## Recommended next actions
1. Rework the idempotency and refusal model and add event delivery (C1–C6) — the engine's correctness — arch.
2. Close invariant 13 for generated recipes, facts and triggers (C16–C18, C13) — security, devex.
3. Re-cut the Phases so every signal passes in its own phase, with a desk Step runtime and a CLI surface section (C7–C11, C15) — product, design, devex.

## Lens status
| Lens | File | Status |
|---|---|---|
| Product | product-review.md | ✓ |
| Architecture | arch-review.md | ✓ |
| Design | design-review.md | ✓ |
| Devex | devex-review.md | ✓ |
| Security | security-review.md | ✓ |

## Overall verdict
There are no CRITICAL findings. HIGH findings C1–C18 make the verdict NEEDS_WORK.

verdict: NEEDS_WORK
