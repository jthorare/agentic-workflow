# Threat Model — sindri-api-surface (pass 5, final)

**Mode:** plan
**Source:** ~/repos/agentic-workflow/docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md (commit 11500b8, §13 "Pass 4 → final text"); pass 4 at `pass4/security-review.md`
**Reviewed:** 2026-10-10

**Threat model applied (unchanged).** Gates guard against honest mistakes by agents, not hostile ones. Forged `X-Sindri-Caller`, `X-Sindri-Terminal` or `X-Sindri-Repo` headers are out of scope for v0. This pass reports only pass-4 findings that are still open and new issues from the pass-4 edit.

## Summary

The pass-4 edit resolves the substance of all three pass-4 LOWs:
- **P4-1:** `AuthorizingService` now refuses a command that has no caller context (`SND-AUTH-002`, audited as `none`). Only `ServeCommand` sets `service:host`, and it does so explicitly. The §4.2 table now shows `service:host → StopProcess`. A missing context on any command path therefore fails closed.
- **P3-2 residual:** `ReadLedger` is repo-scoped for sessions.
- **P4-2:** a hard gate in the wave table makes the engine's P2 contract unit wait for this spec's P1 contract unit.

The new content is sound from a security view: the worktree-safe repo resolution, `CreateItem` defaulting to the header repo, manual `service.ran`, and the failure row clearing on `profile.approved`. Repo resolution fails closed: outside a profile repo the header is omitted, and the session then sees only items that have no repo. The manual run and the profile approval are both actions only a human can take.

Three LOWs remain, all of them documentation or tracking:
1. A stale §13 row still says `EventFactory` defaults to `service:host`, and the spec still doesn't say what `EventFactory` does when it is called outside the chain with no context.
2. For sessions, the "no item" clause in `ReadLedger` lets item-less `request.filed` rows from other repos through. Only metadata leaks, because the text is left out.
3. The engine spec still hasn't been edited. The gate exists only in this spec's wave table.

Nothing is CRITICAL, HIGH or MEDIUM.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 1 (LOW P5-2, residual of P3-2). P4-1 is resolved |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | No findings. `git rev-parse --git-common-dir` runs client-side on the session's own working directory, and the result is only compared against profile paths |
| A04 Insecure Design | findings: 1 (LOW P4-2, tracking, mitigated by the gate in this spec) |
| A05 Security Misconfiguration | No findings. |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | No findings. `service` still never arrives over HTTP, and R2 is accepted |
| A08 Software & Data Integrity Failures | No findings. Manual `service.ran` is keyed `service.ran:<name>:manual:<jobId>`, so it is idempotent per job |
| A09 Security Logging & Monitoring Failures | findings: 1 (LOW P5-1, a stale default and an unstated actor for writes outside the chain) |
| A10 SSRF | No findings. |

## Pass-4 resolution check

| Pass-4 finding | Status |
|---|---|
| P4-1 `service:host` default and the authorizer's behaviour with no context [LOW] | **Resolved for commands** (§2.1: no context is refused; `service:host` is set only, and explicitly, by `ServeCommand`; §4.2 footnote). Two items are left: the stale §13 text and the unstated `EventFactory` behaviour with no context. See P5-1 |
| P3-2 residual: `ReadLedger` session scope [LOW] | **Resolved**, with one narrow gap left: item-less events. See P5-2 |
| P4-2 engine-spec dependency [LOW] | **Mitigated here** with the wave-table gate. Still open in the engine spec (rev 4 at HEAD): it has `SND-WF-007` and `human-terminal` (lines 249–259, 351, 512, 530–536, 669), and its wave table (line ~1043) doesn't show the gate |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **P5-2: item-less events in `ReadLedger` and the stream bypass the session repo filter** [LOW, residual of P3-2]
  - Where: §3.1, the `ReadLedger` row: "only events whose item is in the session's repo, or that have no item". `request.filed` has `item?` plus a `repo` field (§5 catalog row).
  - Attack (honest): an agent in repo A pages `GET /ledger?after=0` and receives `request.filed` rows that have no item but were filed from repo B. The public form leaves out `text`, so what leaks is the request id, the kind and repo B's name. Unrelated item-less service and process events are harmless.
  - Mitigation: one clause. "…or that have no item **and no `repo` field naming another repo**." The P5 stream's session filter should use the same predicate. If the owner prefers, accept it in writing: the payload is metadata only.

### A02 — Cryptographic Failures

No findings.

### A03 — Injection

No findings.

### A04 — Insecure Design

- **P4-2: the engine spec's caller model still differs, and the gate appears in one wave table only** [LOW, tracking]
  - Where: the new "Gate" row in this spec's wave table says "Both wave tables must show this gate". The engine spec's wave table and §6.2/§6.3 are unchanged at rev 4.
  - Attack (honest): an engine-team agent works from the engine spec alone. It never sees the gate, starts engine P2 (the place where `recipe approve`, a `human-terminal` command, lands), and builds the `SND-WF-007` actor table. That gives two authorizers again.
  - Mitigation: already accepted as an owner action. Before engine P2's contract unit starts, add the gate row to the engine spec's wave table, or land its §6.2/§6.3 edit. Check that engine P1 (model + validator) defines no actor-class enum, because the gate only covers P2 onward.

### A05 — Security Misconfiguration

No findings.

### A06 — Vulnerable & Outdated Components

No findings.

### A07 — Identification & Authentication Failures

No findings.

### A08 — Software & Data Integrity Failures

No findings.

### A09 — Security Logging & Monitoring Failures

- **P5-1: a stale `EventFactory` default remains in §13, and the actor used for writes outside the chain is unstated** [LOW, residual of P4-1]
  - Where:
    - §13, pass-2 resolution row (line ~969): "…`service:host`… and `EventFactory` defaults to it." This contradicts the new §2.1.
    - §2.1: "`StartProcess` and `RecordHeartbeat`… keep writing through `EventFactory` with `service:host` and `service:heartbeat`." The spec doesn't say whether they set the caller context or pass the actor explicitly. It also doesn't say what `EventFactory` stamps when an event is written outside the chain with no context.
  - Attack (honest): an implementer follows the §13 row and keeps a `service:host` fallback in `EventFactory`. The authorizer still refuses context-less commands, so access control isn't affected. But a hosted-service path that lost its `AsyncLocal` (a timer, or `Task.Run` with suppressed flow) gets its events attributed to the host with no signal, which is the misattribution half of P4-1.
  - Mitigation:
    - Strike "and `EventFactory` defaults to it" from the §13 row, or annotate it as superseded by pass 4.
    - Add one sentence to §2.1: "`EventFactory` never defaults an actor. Writers outside the chain pass theirs explicitly, and a context-less write throws in Debug/tests and stamps `service:unknown` with a warning in Release."

### A10 — SSRF

No findings.

## STRIDE findings

### S — Spoofing
No findings. `service:*` can't be claimed over HTTP. Header forgery is out of scope.

### T — Tampering
No findings.

### R — Repudiation
- Writes outside the chain may be misattributed if the stale default is implemented [LOW]: P5-1.

### I — Information Disclosure
- Item-less `request.filed` metadata crosses the repo scope [LOW]: P5-2.

### D — Denial of Service
No findings. The fail-fast `SND-API-004` during a rebuild and "no sleep between full pages" both reduce the starvation that P3-3 described.

### E — Elevation of Privilege
No findings within this spec: no context is refused, and `service:host` is limited to `StopProcess`. The engine spec's divergent model is tracked in P4-2.

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 0
- **Low:** 3. Each can be answered with one line of spec text or accepted in writing:
  - P5-1: strike the stale §13 default and state how `EventFactory` behaves with no context.
  - P5-2: the item-less `request.filed` repo clause.
  - P4-2: mirror the gate in the engine spec.
- **Recommendation:** SHIP

verdict: PASS
