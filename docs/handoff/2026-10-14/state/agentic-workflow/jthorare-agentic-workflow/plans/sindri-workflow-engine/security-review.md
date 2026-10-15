# Threat Model — sindri-workflow-engine

**Mode:** plan
**Source:** `~/repos/agentic-workflow/docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (read with factory addendum §17.1 inv. 13, §17.19, §17.21, §17.22 D2/D9/D10, §17.28; `docs/csharp/bugfix.md` "Threat model"; S1 branch `feat/sindri-s1-app-shell` for `LoopbackGuard`, `LoopbackBinding`, `ApproveProfileService`, `EndpointTable`)
**Reviewed:** 2026-10-10

**Threat model applied (A §17.28.3, bugfix.md):** gates guard an agent's honest mistakes and shortcuts (weakening a check, skipping a phase, unblocking itself, re-rolling a verdict). Containing a deliberately adversarial agent is the sandbox's job (§8, §17.19 "What the sandbox really enforces"). A finding that needs a crafted bypass (a hand-built HTTP request, a direct SQLite write, a squatted port) is rated LOW/informational and marked **[written answer]**: answer it against the threat model in §11 "Review resolutions", not with a new rule.

## Summary

The design is security-minded at its core. Blocks fail closed (W5). The `when` grammar is closed and unknown fields are refused (W3). The rung comes from the descriptor, so a recipe can't promote a judge question (W4). Approval is keyed by hash and stored outside the file (W8). Approval is re-checked at start and at dispatch (W11). The transition, event and outbox commit in one fenced transaction (W13). No CRITICAL findings. Three HIGH findings are gaps in invariant 13 or in the "agents don't unblock themselves" line:
1. `RCP-CAP-001/002` cover only capability-bearing block uses and `with:` values. A generated recipe can still drop the gates and evidence that guard a kept effect, lower a rung-2/3 threshold, or raise the recipe's `budget` and loop caps. Budget is a named capability field in invariant 13.
2. A capability `with:` parameter can take `{ fact: <key> }`, so a Step's (model) output can set a capability value at run time.
3. Nothing says who may fire which trigger. A session can run `sindri workflow answer --grant n` (or a recipe `command.<verb>`) to clear its own `rounds`/`budget`/`no-progress` park.

The MEDIUM findings cover approval-tier enforcement that is client-side today (`Confirmed` in the request body), revocation that doesn't reach generated derivatives, children named by id rather than hash, child recursion, the sleep shift starving deadlines, YAML duplicate keys, the `Commit` step order (a retried `answer --grant` can apply twice), registry events missing from the ledger, and secrets in append-only ledger payloads.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 2 (1 HIGH, 1 MEDIUM) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 2 (LOW) |
| A04 Insecure Design | findings: 3 (2 HIGH, 1 MEDIUM) |
| A05 Security Misconfiguration | findings: 1 (LOW) |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | findings: 1 (LOW) |
| A08 Software & Data Integrity Failures | findings: 3 (MEDIUM) |
| A09 Security Logging & Monitoring Failures | findings: 2 (MEDIUM) |
| A10 SSRF | findings: 1 (LOW, informational) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **H3. Nothing says who may fire a trigger, so a session can answer its own park and grant itself rounds** [HIGH]
  - Where: W7 (line 26); §3.4 `on: command.<verb>` (line 140); §3.7 `parked → resumeTo` (line 173); §6.1 `ApplyTrigger` (line 280); Phase 4 verbs `sindri workflow start|status|answer|cancel` (line 510); §5.4 takeover.
  - Attack (honest shortcut, no crafting): a Step session is parked with `rounds` or `no-progress`. Its `next` text and `sindri workflow status` point at `sindri workflow answer`. The agent runs `sindri workflow answer T-1 --grant 3` from its own shell (or through the Sindri MCP server) and keeps going. The park existed to bring a human in. This is the "skipping a phase" class that bugfix.md lists as in scope. The same applies to recipe `command.<verb>` transitions used as human sign-off, and to `takeover`/`handback`, which pause timers. Note that §17.22 D2 makes `answer` a **desk command** (lease-checked), but this doc doesn't say it is human-bound.
  - Mitigation: give each trigger source a type and write the rule down.
    - (a) `step.completed`/`step.failed` come only from the Step runtime, with the launcher's fenced attempt token.
    - (b) `wait.satisfied` comes only from engine services and adapters (router, slot allocator, child inbox, claim effect), never from a public endpoint that takes an arbitrary event kind.
    - (c) `command.answer` with a `grant`, and any answer to a `rounds`, `budget`, `no-progress` or `spec-conflict` park, is approval-tier: a typed TTY confirmation or an enrolled-device passkey (A 17.21 D6), like `recipe approve`.
    - (d) The CLI refuses those verbs when `SINDRI_MODE=1` or a session id is in the environment, and the Sindri MCP server doesn't expose them (README: "Calls from a session are requests, never decisions").
    - (e) Cap `grant` (for example ≤ the loop's `cap`).
    - Add `SND-WF-0xx` "this trigger needs a human" and a test `Session_cannot_answer_its_own_rounds_park`.

- **M1. "An API token alone can't approve" is enforced only on the client today** [MEDIUM]
  - Where: §4 `sindri recipe approve` "(typed confirmation at a TTY; an API token alone can't do it, A 17.21)" (line 198); S1-D5 (line 292). On S1, `ApproveProfileService.Refusal` checks `request.Confirmed`, a boolean in the POST body to `/sindri/profile/approve`. The loopback API has no token or other auth (`LoopbackGuard` checks only Host and Origin).
  - Attack: the honest-mistake path is a future client of the same API that sets `Confirmed = true` as part of a normal click: the Avalonia UI (CLAUDE.md: "a client of Sindri's HTTP API with no logic of its own") or an MCP tool that maps the endpoint. Approval then becomes an ordinary call with no human proof. A hand-crafted `curl -d '{"confirmed":true}'` is the adversarial version: LOW **[written answer]**.
  - Mitigation: in this doc and in S1-D5, say where the TTY proof lives. Prefer a server-issued, single-use approval challenge (nonce plus hash) that the CLI shows on the TTY and the human types back. Keep approval-tier endpoints out of the MCP server and out of the UI's bindings, with an architecture test that no MCP tool or UI view model routes to `/recipe/approve`, `/recipe/revoke` or `/profile/approve`. State that a UI approve control needs the passkey path (A 17.19 phone decisions), not `Confirmed`.

### A02 — Cryptographic Failures

No findings. SHA-256 over canonical JSON is fine for content addressing. The 6-character typed prefix (S1 pattern) is a typo guard, not a security control. Receipt signing (§17.23.12) is out of scope here. See A08 M6 for canonicalization.

### A03 — Injection

- **L1. Effect payloads built from facts and tracker ids reach git and adapters** [LOW]
  - Where: `effect.claim.ref` creates `sindri/<user>/{item}` (line 399, 375); `IEffectKind.Build(EffectContext)` (line 71); `with:` `{ fact: <key> }` (line 112).
  - Attack: an item id or fact value with `..`, a leading `-`, spaces or control characters ends up in a ref name or a CLI argument (`git update-ref`, `gh`). Most likely as an honest malformed tracker id, rarely crafted.
  - Mitigation: effect builders validate ref components (`git check-ref-format`, a typed `ItemId` with a closed character set) and the dispatcher passes argv arrays, never a shell string. Put a `BlockContract` case for hostile ids in `effect.claim.ref`'s contract tests.

- **L2. Recipe YAML and eval globs: aliases, size, paths that escape** [LOW]
  - Where: §3 (YAML 1.2 via S1 `YamlToJson`, line 82); `evals: [evals/workflow.item/*.jsonl]` (line 414).
  - Attack: an anchor/alias bomb or a very large file stalls `recipe validate`. An `evals` glob like `../../…` reads files outside the recipe folder during `recipe eval`. Low impact (local, read-only).
  - Mitigation: refuse anchors, aliases, merge keys and custom tags in recipes (they also blur the hash, see M6). Cap the input size. Resolve `evals` relative to the recipe's directory and refuse paths that leave it (`RCP-SCHEMA-001`).

### A04 — Insecure Design

- **H1. `RCP-CAP-*` checks only block uses and `with:` values. A generated recipe can weaken the protection around a kept effect and raise budgets** [HIGH]
  - Where: W9/W10 (lines 28–29); §4 Rules (lines 203–206); §3.1 `budget` (line 98), §3.3 state `budget` (line 122), §3.6 `loops.cap` (line 159); §3.4 `gates`/`evidence`/`threshold` (lines 142–143). Invariant 13 names **budget allowlists** as a capability field. §17.19 says capability fields are approval-tier and "no model output may set them".
  - Attack (honest, no crafting): workflow generation (§17.7, model-proposed Step order) emits a recipe whose base is `workflow.bugfix@h`. It keeps `evaluate → report` with `effect.pr.draft`, so `RCP-CAP-001` (block id plus `from → to`) passes. But it:
    - drops `gate.judge.resolved` or `evidence: [test@head]` from that transition;
    - lowers a rung-2 `threshold` from 0.8 to 0.1;
    - raises `budget.tokens` from 400k to 4M, or `loops.attempts.cap` from 3 to 30.
    The packet freeze then approves it (W9). The human sees a valid packet and may never diff the recipe against its base. A model can also choose the most permissive approved `base` to derive from. This is "weakening the check", which the threat model puts in scope.
  - Mitigation: widen the generated-recipe rules.
    - (a) `RCP-CAP-005`: for each capability-bearing use the generated recipe keeps, the gates and evidence on that transition, and on every path that dominates it, are a superset of the base's. Each kept gate's `threshold` is ≥ the base's.
    - (b) `RCP-CAP-006`: the recipe and state `budget`, `loops.*.cap` and timeout `then:` actions are ≤ the base's, or equal to them, matching W10's "narrowing counts as a change" if you want one rule.
    - (c) Sindri code picks `base` from the deliverable's lens or recipe type, never from model output, and the freeze screen shows the diff against the base.
    - Add fixtures `invalid/RCP-CAP-005-dropped-gate.yaml` and `RCP-CAP-006-raised-budget.yaml`.

- **H2. A capability `with:` parameter can be fed by a fact, so a Step's output sets a capability at run time** [HIGH]
  - Where: §3.2 `with:` value `{ fact: <key> }` (line 112); §2 `ParameterSpec.IsCapabilityField` (line 62); `RCP-CAP-002` compares `with:` values with the base's (line 204). If the base already uses `{ fact: fact:scope }`, the comparison passes.
  - Attack: a recipe author (or the generator) wires a write-scope or command allowlist parameter to `{ fact: fact:scope }`, and a model-run Step produces `fact:scope`. The allowlist is now model-decided at run time, though invariant 13 says scope is "computed by Sindri code" and the session policy is the profile ceiling ∩ lens ∩ task. Static validation can't see the value.
  - Mitigation: add a rule (for example `RCP-CAP-007`) that a parameter with `IsCapabilityField` takes only a literal or a profile reference, never `{ fact: … }`. The one exception is a fact whose producing block is rung-1 deterministic code that declares the fact as a capability source in its descriptor, such as the packet's code-computed scope. At run time, intersect every capability value with the profile's approved allowlist before the block sees it (the ceiling rule), and record the intersection in the event.

- **M3. Children are named by id, not by hash, and composing a child isn't treated as a capability use** [MEDIUM]
  - Where: §3.3 `waitFor: { child: { recipe, … } }` (line 130); `recipe: workflow.step` (line 401); `RCP-CHILD-001` (line 328); `RCP-CAP-001` compares block uses only.
  - Attack: (1) The parent was approved at hash H. The child it runs depends on whichever `workflow.step` version is current when the parent starts it. The parent's approval and its replay (Property 1) don't pin the child's behaviour. (2) A generated recipe adds a `wait` state whose child is an approved recipe with outward effects (for example a ship or merge recipe) that the base never composed. Each recipe is approved alone, but the composition widens what the deliverable can do outward.
  - Mitigation: name children as `recipe: <id>@sha256:…` (or resolve and freeze the child hash into the parent instance at start and record it in `workflow.started`). Count child references as capability-bearing uses in `RCP-CAP-001`: a generated recipe's set of children must be a subset of its base's.

### A05 — Security Misconfiguration

- **L7. The loopback API has no caller authentication, so any local process or OS user can call it** [LOW] **[written answer]**
  - Where: §6.1 Host endpoints (line 282); S1 `LoopbackBinding` (127.0.0.1 only) and `LoopbackGuard` (Host and Origin only).
  - Attack: another local OS account, or a process that squats the port before `sindri` starts (if the endpoint record goes stale), can read `/sindri/events` or post commands. This needs crafting or a shared machine, and the machine is single-owner.
  - Mitigation: record in §11 that the control plane is single-user and loopback-bound by design. Before team mode (A 17.21, P4), add a per-run bearer token in a 0600 file next to the endpoint record, and have the CLI check the server's pid against the record. No change needed for v1.

### A06 — Vulnerable & Outdated Components

No findings. The new code adds no packages beyond S1's YAML reader. Central package management, locked restore and `scripts/dotnet-verify-packages.sh` already gate it. The recipe-specific YAML hardening is in L2/M6.

### A07 — Identification & Authentication Failures

- **L3. `actor` and `approver` can't tell the human from an agent session on the same host** [LOW]
  - Where: §5.2 "`actor` is set by the host process" (line 237); §4 `approver` (line 193); S1-D7 (line 294).
  - Attack: an honest session runs `sindri recipe register`, `workflow cancel` or `takeover`. The ledger records the same actor as the owner's desk action, and `register` records provenance `human` (line 201) for agent-written text. Audit and the per-rung metrics can't separate them.
  - Mitigation: the CLI tags the actor as `session:<id>` when `SINDRI_MODE`/a session id is set, and `human:<user>` only after a TTY confirmation. Provenance is `human` only for `approve` (which is TTY-bound), and `register` records `registered-by`. This is cheap, and labels honest callers correctly (it is not a security boundary).

### A08 — Software & Data Integrity Failures

- **M2. Revoking a base doesn't reach the generated recipes approved from it** [MEDIUM]
  - Where: W9/W11 (lines 28, 30); §4 `recipe revoke <id@hash>` (line 199); `IRecipeApprovals` stores `base?` (line 194).
  - Attack: the human revokes `workflow.bugfix@h1` because an effect misbehaves. Packets frozen earlier with generated recipes `base: workflow.bugfix@h1` still hold their own freeze approval. They keep starting instances and dispatching the same effect.
  - Mitigation: an approval `via: freeze` is valid only while its `base` is approved. The start and dispatch re-checks (W11) walk the `base` chain, and `revoke` reports the derived recipes it parks. Test: `Revoking_a_base_refuses_dispatch_for_its_generated_recipes`.

- **M6. Duplicate YAML keys and alternate YAML forms: what the human reads isn't what is hashed and approved** [MEDIUM]
  - Where: §3.8 the hash is over the parsed document (line 178); `approve <file|id@hash>` (line 198).
  - Attack (honest copy-paste): a recipe has two `effects:` keys on a transition, or a merge key `<<`. The human reads the first. The parser keeps the last one, and that is what gets hashed, approved and run. A canonicalization that is underspecified (number forms `1e3` vs `1000`, string escapes, Unicode normalization) also lets two builds disagree on a hash.
  - Mitigation: refuse duplicate keys, anchors, aliases, merge keys and tags (`RCP-SCHEMA-001`). Pin the canonical form to RFC 8785 (JCS) or write the rules down. Have `recipe approve` show the canonical parsed recipe, and for a new version the diff against the last approved one, not the raw file.

- **M7. `Commit` compares the version before checking the key, so a retry gets `VersionConflict` instead of `AlreadyApplied`, and a re-issued `answer --grant` applies twice** [MEDIUM]
  - Where: §5.3 steps 2–3 (lines 243–244); W12 (line 31); the Phase 4 signal "the same cancel … run twice records one event" (line 515).
  - Attack (honest retry): `answer --grant 2` commits, but the response is lost. The retry carries the same `fromVersion`, so step 2's `UPDATE … WHERE version = @fromVersion` matches no row and returns `VersionConflict` before the key check. The error text says "re-read and retry". The caller retries at the new version, and the grant is applied again (4 rounds). Exactly-once holds for the event key, but the documented recovery path breaks it.
  - Mitigation: look the event key up first (or on a version miss) and return `AlreadyApplied(sequence)`. Add to `WorkflowStoreContract`: `Retry_after_applied_returns_AlreadyApplied_not_VersionConflict`.

### A09 — Security Logging & Monitoring Failures

- **M8. Registering, approving and revoking a recipe aren't ledger events** [MEDIUM]
  - Where: §5.2 lists only `workflow.*` events (lines 230–235); §4's verbs write `IRecipeApprovals`/`IRecipeStore` only. The profile precedent appends `ProfileApproved` to the ledger. A 17.21 "Audit" requires every approval and capability change to be recorded with its actor.
  - Attack: a recipe approval, a freeze approval of a generated recipe, or a revoke leaves no event trail, so the receipt (§17.23.12) and digests can't show who approved the capability set an effect ran under.
  - Mitigation: add `recipe.registered`, `recipe.approved { via, base?, approver }` and `recipe.revoked`, with the §17.21 envelope, appended in the same transaction as the store write. Count W5 refusals (`SND-WF-010`) and dispatch refusals as metrics (a fail-closed rate), so a broken runtime shows up.

- **M9. Gate reasons, Step outputs and `workflow.started` inputs go into an append-only ledger without scrubbing** [MEDIUM]
  - Where: §5.2 `gates[] { …, reasons }`, `workflow.started` inputs (lines 232–233); S1-D6 ledger read endpoint (line 293).
  - Attack (honest): a gate's reasons quote command output with a token, or a `--spec spec.json` input carries a secret. The ledger is append-only (and hash-chained later), so it can't be removed, and `sindri ledger` and the event stream serve it.
  - Mitigation: run every event payload through `Aw.Redaction` before it reaches `LedgerEntry`, as Kernel telemetry does for logs. Store large or sensitive payloads as registered artifacts referenced by hash, and keep only `{ ref, sha256 }` in the event.

### A10 — SSRF

- **L8. No outbound fetch in v1. Future evidence providers and effects take targets** [LOW, informational]
  - Where: §2 `Capability.Network` (line 46); A 17.11 evidence providers (`ui-browser` navigates to URLs).
  - Note: when `evidence.ui-browser` or an HTTP effect lands, its URL or host must come from the profile or the approved recipe (a capability field, see H2), never from a fact, and must be intersected with the per-task egress allowlist (§17.19 Network row). Nothing to change in v1.

## STRIDE findings

### S — Spoofing
- **L7** (A05): unauthenticated loopback callers and port squatting. **[written answer]**
- **M1** (A01): the server can't tell a TTY-confirmed approval from a body flag.

### T — Tampering
- **L4. Writing `recipe_approvals` or `recipes` directly in `ledger.db`** [LOW] **[written answer]**. A process with the user's file access could insert an approval row with `sqlite3`. That is a crafted bypass, so it is the sandbox's job (sessions don't mount Sindri's state dir, §17.19 "Reads: mounts"). Answer in §11. Optionally, store the approval's ledger sequence (M8) and have start/dispatch confirm that the approval row has a matching `recipe.approved` event.
- **M6** (YAML duplicates and canonicalization), **M7** (double grant on retry), **H1** (weakened gates in generated recipes).

### R — Repudiation
- **L3** (A07): human and session actors are indistinguishable, and `register` records provenance `human`.
- **M8** (A09): no ledger events for approve or revoke.

### I — Information Disclosure
- **M9** (A09): secrets in append-only ledger payloads, served by the ledger endpoint and event stream.

### D — Denial of Service
- **M4. Child recursion: a recipe can start itself as a child, directly or through a cycle** [MEDIUM]
  - Where: §3.3 `child` (line 130); `RCP-CHILD-001` checks only that the child exists and that its inputs are covered (line 328).
  - Attack (an honest generator or authoring mistake): `workflow.a` waits on child `workflow.b`, which waits on child `workflow.a`. Each start commits at once (§5.5), so the instance count grows without bound until the host stalls. Slots don't limit it, because `child` isn't a `resource` wait.
  - Mitigation: `RCP-CHILD-002`: the child-recipe graph across registered recipes is acyclic, checked at register and approve. Add a run-time depth cap (for example 4) that parks with `spec-conflict`.
- **M5. The sleep shift can keep pushing deadlines back on a host that ticks slowly but never sleeps** [MEDIUM]
  - Where: W14 (line 33); §5.4 "when the gap since the last tick is larger than the interval, the service first shifts every armed deadline by the gap" (line 253).
  - Attack (honest load): under heavy-lane load, GC pauses or a busy SQLite writer, ticks land every 16–30 s. Each tick sees a gap above 15 s and shifts every deadline by the whole gap, so `timeout` never fires and a stuck Step is never parked or escalated.
  - Mitigation: shift only above a sleep threshold (for example a gap ≥ 4× the interval, or 60 s), and only by `gap − interval`. Better, detect sleep by comparing a monotonic clock that stops during sleep with the wall clock (D §9.2). Add the test `Slow_ticks_below_threshold_do_not_shift_deadlines`. Also note that a multi-instance shift isn't a single-instance `Commit` (W16), so either shift per instance through `Commit` or say why the bulk write is safe.
- **L5. Budgets rely on `StepResult.spent`, which the Step reports itself** [LOW]
  - Where: §2 `IStep.Run → StepResult(outcome, outputs, spent)` (line 70); §3.6 budgets park (line 163).
  - Note: a Step runtime that forgets to report (spent = 0) makes budget parks silently never fire. Take `spent` from the launcher or model-proxy meter (§17.19 "Budgets: the model proxy and the tick"), and treat a missing meter as fail-closed (W5).
- **L2** (A03): YAML alias bombs and input size.

### E — Elevation of Privilege
- **H1** (A04): a generated recipe weakens the gates, evidence and thresholds around a kept effect, or raises budgets and loop caps.
- **H2** (A04): fact-fed capability parameters.
- **H3** (A01): a session answers its own park with a grant, or fires `command.<verb>` sign-offs.
- **M3** (A04): a generated recipe widens its outward reach by composing an approved child.
- **L6. `next` texts are prompt text that agents read as "the one allowed next step"** [LOW]. A generated `next` (it self-adopts as prompt text under A 7.4) could say "run `sindri workflow answer --grant 5`". This is harmless once H3 makes such verbs human-bound. Until then, keep `next` variables to the closed set (already `RCP-NEXT-001`) and refuse CLI verb text for approval-tier verbs in `next`.

## Items for the written answer in §11 (threat model, no new rule)

- L4: approval rows written directly with `sqlite3`.
- L7: unauthenticated loopback callers, other local OS users, port squatting.
- M1 (crafted half): a hand-built `POST /recipe/approve` with `confirmed: true`. The honest half (UI or MCP wiring) is the MEDIUM above.
- Any crafted `with:` or fact value meant to get past H2's run-time intersection.

## Verdict
- **Critical findings:** 0
- **High findings:** 3 (H1 generated-recipe weakening and budgets; H2 fact-fed capability parameters; H3 trigger authorization and self-answer)
- **Medium findings:** 9 (M1–M9)
- **Low findings:** 8 (L1–L8)
- **Recommendation:** FIX-CRITICAL-FIRST. Fold H1–H3 into the design (new `RCP-CAP-005/006/007` rules, a trigger-source table in §3.4/§3.7, a human-bound `answer --grant`) before Phase 2 (approval) and Phase 4 (`answer`). The MEDIUM fixes fit their phases: M6 and M4 in Phases 1–2, M7 in Phase 4, M5 in Phase 5, M2 in Phase 6, M3 in Phase 7.

verdict: NEEDS_WORK
