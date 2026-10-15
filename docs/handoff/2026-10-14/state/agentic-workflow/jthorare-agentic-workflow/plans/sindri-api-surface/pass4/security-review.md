# Threat Model — sindri-api-surface (pass 4, final edit)

**Mode:** plan
**Source:** ~/repos/agentic-workflow/docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md (final edit, commit 0617aae; §13 "Pass 3 → final edit"); pass 3 at `pass3/security-review.md`
**Reviewed:** 2026-10-10

**Threat model applied.** Unchanged: gates guard against honest mistakes by agents, not against hostile ones. A deliberately forged `X-Sindri-Caller: human` or `X-Sindri-Terminal: 1` is out of scope for v0. This pass reports only (a) pass-3 findings that are still open and (b) new issues from the final edit, mainly the `service:host` caller.

## Summary

The final edit resolves both pass-3 MEDIUMs:
- **P3-1:** §2.1 now states one fail-closed rule. Protected fields override the variant, an unknown variant is `protected`, `grant` and `accept` are invalid on every variant except `answer`, and a unit test covers each variant.
- **N1:** the raw human `curl` is replaced by `sindri-prove.sh curl`, which reads the URL from the guarded temp home, and no hand-rolled human `curl` is left in the spec.

P3-2 (session commands are now repo-scoped, and idle takeovers become `park` rows) and P3-3 (fast-fail on `stalled`) are also resolved. R2 and the A09 `doctor` note are accepted in writing.

The new `service:host` caller is sound where it matters. It can't come over the wire: `X-Sindri-Caller` accepts only `human` or `session`, and any other value is `SND-API-007`. The catalog limits it to `StopProcess`, so its worst case is a self-stop. One gap remains. "With no caller context set, `EventFactory` stamps `service:host`" is a fail-open default, and the spec doesn't say what the authorizer does when there is no context. That is LOW.

Two other LOWs remain: a leftover from P3-2 (`ReadLedger` session scope isn't stated) and the cross-spec dependency on the engine spec, which still uses `SND-WF-007` and `human-terminal`. Nothing is CRITICAL, HIGH or MEDIUM.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 2 (LOW P4-1 new; LOW P3-2 residual) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | No findings. |
| A04 Insecure Design | findings: 1 (LOW P4-2, cross-spec dependency). Pass-3 P3-3 is resolved |
| A05 Security Misconfiguration | No findings. |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | No open findings. R2 is accepted in writing; P1 checks the agent variables against real runs |
| A08 Software & Data Integrity Failures | No findings. |
| A09 Security Logging & Monitoring Failures | findings: 1 (LOW, part of P4-1: the default actor misattributes events). The `doctor` warning is deferred to §11, which is accepted |
| A10 SSRF | No findings. |

## Pass-3 resolution check

| Pass-3 finding | Status in the final edit |
|---|---|
| P3-1 two classification rules for `ApplyCommand` [MEDIUM] | **Resolved.** §2.1 has one fail-closed rule with a test for every variant. Adoption by the engine spec is tracked in P4-2 |
| N1 raw human `curl` isn't tied to the temp home [MEDIUM] | **Resolved.** `sindri-prove.sh curl <method> <path> [json]` (§12). The P3 signal uses it, and no raw `X-Sindri-Caller: human` `curl` remains |
| P3-2 session commands aren't repo-scoped, and takeovers aren't surfaced [LOW] | **Mostly resolved.** `FileRequest` and `pause` are repo-scoped, `request.filed` carries `repo`, and the engine edit adds idle-takeover `park` rows. Left open: `ReadLedger`'s session scope (below) |
| P3-3 stalled projector starves stop and rebuild [LOW] | **Resolved.** View-reading commands fail at once with `SND-VIEW-001` while the projector is stalled, and `ReadsViews` is a catalog flag |
| R2 gaps in agent-session detection [LOW] | **Accepted in writing.** P1 checks `CODEX_SANDBOX` and `CURSOR_AGENT` against real runs and records the result |
| A09 `human:http` and `human:prover` rows aren't flagged [LOW] | **Accepted.** Deferred to §11 |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **P4-1: `service:host` is a default as well as a caller, and the authorizer's behaviour with no context isn't stated** [LOW, new]
  - Where:
    - §2.1 "Host-internal callers": "With no caller context set, `EventFactory` stamps `service:host`."
    - §4.2: the `service` column says `desk` is "only the engine commands its recipe step owns", which doesn't list `StopProcess`. The `service:host` exception lives only in the catalog prose.
  - Attack (honest bug): `ICallerContextAccessor` is `AsyncLocal`. Context is lost on paths such as a hosted-service loop that never sets it, a `Task.Run` or timer that suppresses flow, or an endpoint mapped outside `CallerContextMiddleware`. On those paths:
    - (a) every event is stamped `service:host`, so the scheduler's `service.ran` and `profile-watch`'s `profile.checked` show up as written by the host. That hides the missing context instead of surfacing it.
    - (b) An implementer who mirrors the default in `AuthorizingService` turns "no context" into a caller that is allowed to stop the process. The catalog limits the blast radius to `StopProcess`, so this is an unintended self-stop, not an escalation. It is still the same kind of fail-open default that P3-1 removed from the classifier.
  - Mitigation:
    1. State that `AuthorizingService` refuses a command when there is no caller context (`SND-AUTH-002`), and never applies a default. `ServeCommand`'s signal and taken-port handler set `service:host` **explicitly**.
    2. Every in-process writer (scheduler, `profile-watch`, engine) sets its own `service:<name>`. With no context, `EventFactory` should throw in tests, or stamp a distinct `service:unknown` with a log line, not `service:host`.
    3. Add a unit test: a command with no context is refused, and only `StopProcess` accepts `service:host`. Add an architecture test that every mapped command endpoint sits behind the caller middleware.
    4. Add `service:host → StopProcess only` as a footnote to the §4.2 table, so the table and the catalog agree.

- **P3-2 (residual): `ReadLedger`'s scope for `session` callers isn't stated** [LOW]
  - Where: §3.1 scopes "list and item queries" for sessions. `GET /ledger` is an audit read, and for `human` the spec says only that non-public types are added.
  - Attack (honest): an agent in repo A pages `GET /ledger?after=0`. It gets `item.created` titles and `request.filed` rows from repo B, and copies them into its own work.
  - Mitigation: one line in §3.1. `ReadLedger` for a `session` returns only events whose item, or `repo` field, is in the session's repo, plus events that have no item. Add a P5 test.

### A02 — Cryptographic Failures

No findings.

### A03 — Injection

No findings.

### A04 — Insecure Design

- **P4-2: the fail-closed classifier and the single caller model depend on an engine-spec edit that hasn't landed** [LOW, tracking]
  - Where: §3.5 ("The engine spec's rev 3 still differs and needs one edit to its §6.2 and §6.3 before either P1 lands"). The engine spec at HEAD (`2026-10-10-sindri-workflow-engine-design.md`) still has `SND-WF-007` and `human-terminal` (lines 249–259, 351, 512, 530–536, 669), and doesn't list takeover as `pause`.
  - Attack (honest): if engine P1 is built from its own text, takeover is classified by the engine's actor table instead of this spec's fail-closed rule. That reopens the P3-1 mix: two authorizers, and `grant` handled by whichever runs.
  - Mitigation: already flagged. Make it a gate: neither P1 merges until the engine spec's §6.2 and §6.3 match §2.1 and §4 here. Write one shared test, `ApplyCommand_variant_classification`, in the engine's P1 contract unit that cites this spec.

### A05 — Security Misconfiguration

No findings.

### A06 — Vulnerable & Outdated Components

No findings.

### A07 — Identification & Authentication Failures

No open findings. `service` stays in-process only: the header parser rejects any value other than `human` or `session`. R2 is accepted.

### A08 — Software & Data Integrity Failures

No findings. `service.ran` and `profile.checked` are keyed on facts (epoch and slot; the previous check sequence), so a replay can't duplicate them.

### A09 — Security Logging & Monitoring Failures

- The default `service:host` stamp misattributes events from any writer that has lost its context [LOW]. See P4-1.

### A10 — SSRF

No findings.

## STRIDE findings

### S — Spoofing
- `service:host` can't be claimed over HTTP. No findings.

### T — Tampering
No findings. P3-1 is resolved.

### R — Repudiation
- Context-less events are stamped `service:host` and not flagged [LOW]: P4-1.

### I — Information Disclosure
- `ReadLedger`'s session scope isn't stated [LOW]: P3-2 residual.

### D — Denial of Service
- With no context, a mirrored default would let `StopProcess` run unintentionally [LOW]: P4-1(b). Stalled-projector starvation is resolved.

### E — Elevation of Privilege
- No path from session to protected or to human in this spec. The engine spec's divergent caller model is tracked [LOW]: P4-2.

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 0
- **Low:** 3. Each can be answered in writing or with one line of spec text:
  - P4-1: no default caller in the authorizer, and in-process writers set their own context.
  - P3-2 residual: `ReadLedger` session scope.
  - P4-2: gate both P1s on the engine spec edit.
- **Recommendation:** SHIP

verdict: PASS
