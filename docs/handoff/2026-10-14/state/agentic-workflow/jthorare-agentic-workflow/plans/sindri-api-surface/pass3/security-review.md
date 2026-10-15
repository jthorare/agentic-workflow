# Threat Model — sindri-api-surface (pass 3, rev 3, final)

**Mode:** plan
**Source:** ~/repos/agentic-workflow/docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md (rev 3, §13 "Pass 2 → rev 3"); pass 2 at `pass2/security-review.md`
**Reviewed:** 2026-10-10

**Threat model applied.** Unchanged from passes 1 and 2. Gates guard against honest mistakes by agents, not against hostile agents. A deliberately forged `X-Sindri-Caller: human` or `X-Sindri-Terminal: 1` is out of scope for v0. This pass reports only (a) pass-2 findings that are still unresolved and (b) new issues introduced in rev 3, in particular the `pause` class and `X-Sindri-Terminal`.

## Summary

Rev 3 resolves most of pass 2. The following are closed:
- **N2:** the gate has a 5 s limit, the head wait has a 2 s limit, and stop and rebuild skip the head wait.
- **N3:** the new `pause` class reaches takeover without putting `service` on the wire.
- **R1:** repo scope is now enforced by the server, and the MCP `all` argument is gone.
- **N5 and N6:** refusals are logged, and `ReadLedger` is sanitized, capped and uses a read-only port.

The terminal flag is sound within the threat model. It is honoured only together with `human`, the CLI sends it only when there is a TTY and no agent variables are set, and protected means human plus a terminal.

Two MEDIUMs remain, and both are cheap to fix:
1. **P3-1:** the spec gives `ApplyCommand` two classification rules. §2.1 says "desk otherwise", while §3.5 and §4.1 make takeover `pause`. With authorization running before validation, a classifier that keys on the variant alone could let a `pause`-class payload carry a `grant`.
2. **N1 (residual):** the prover's raw `curl` stand-in is not routed through `sindri-prove.sh`. Nothing ties it to the temp home, so it can still reach the real process as `human` with a terminal. That path includes `protected` through `confirmed:true`.

The other findings are LOW. Nothing is HIGH or CRITICAL.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 3 (MEDIUM P3-1 new; MEDIUM N1 residual; LOW P3-2 new) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | No findings. Pass-2 N6 is resolved |
| A04 Insecure Design | findings: 1 (LOW, P3-3 new). Pass-2 N2 and R1 are resolved |
| A05 Security Misconfiguration | No findings. |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | findings: 1 (LOW, R2 residual) |
| A08 Software & Data Integrity Failures | No findings. |
| A09 Security Logging & Monitoring Failures | findings: 1 (LOW, part of N1: `human:http` is not surfaced) |
| A10 SSRF | No findings. |

## Pass-2 resolution check

| Pass-2 finding | Status in rev 3 |
|---|---|
| N1 prover recipe reaches the real home [MEDIUM] | **Partially resolved.** The CLI path goes through `sindri-prove.sh`, which refuses any home outside `$TMPDIR` or the proof folder. The `human:prover` label exists, and the recipe is kept out of `docs/csharp/sindri.md`. The raw `curl` path is not wrapped (see N1 below) |
| N2 stalled projector blocks every command [MEDIUM] | **Resolved.** 5 s gate limit (`SND-API-009`), 2 s head wait, stop and rebuild skip the wait, handlers never do slow work, and there is an integration test. A small residue is in P3-3 |
| N3 takeover through `service` [MEDIUM] | **Resolved by design** with the `pause` class (§4.1, §4.2), and `service` still never arrives over HTTP. The new issues this design creates are in P3-1 and P3-2 |
| R1 repo scope is advisory [MEDIUM] | **Mostly resolved.** The server filters `session` queries by `X-Sindri-Repo`, a missing header gives the narrowest scope, MCP has no `all` argument, and the stream is filtered. The residue (scope of session *commands*, and of `ReadLedger`) is in P3-2 |
| N4 actor labels [LOW] | **Mostly resolved.** `X-Sindri-Client` labels the call, and an unlabelled human is `human:http`. The `doctor` flag for `human:http` on the default home was not adopted (folded into N1) |
| N5 guard refusals invisible [LOW] | **Resolved** ("logged", §6). A counter would be nice but isn't needed |
| N6 `ReadLedger` unsanitized, no cap, no body limit [LOW] | **Resolved.** Sanitizer, `max` ≤ 500, `ILedgerReader`, 64 KiB body limit, repo path from the profile |
| N7 request cap shared by all sessions [LOW] | **Mitigated** (20 per repo, 50 total). Duplicate refusal was not adopted. Accepted for v0 and not raised again |
| R2 unverified session variables [LOW] | **Partially resolved.** The unverified names were dropped. See R2 below |
| A08 note and `v<n>` nit | **Resolved** (the key is unified as `v<version>`) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **P3-1: `ApplyCommand` is classified in two inconsistent ways, and nothing says the classifier must fail closed** [MEDIUM, new]
  - Where: §2.1 ("`ApplyCommand` is `protected` when it carries a `grant` or `accept`, and `desk` otherwise"); §3.5 and §4.1 (takeover is `pause`); §2.1 chain (`AuthorizingService` runs **before** `ValidatingService`).
  - Attack (an implementation bug the spec invites): the class now depends on two things, the variant (takeover → `pause`) and the fields (`grant`/`accept` → `protected`). Authorization runs before validation. An `IClassifiedCommand.ActionClass` that switches on the variant first returns `pause` for `{kind: takeover, grant: …}`, so the session is authorized. Whether the stray `grant` then gets dropped, rejected or applied is up to the engine handler. The same applies to any variant that is added later and hits a `default: desk` or `default: pause` arm. `pause` is now the one class besides `request` that a session can reach, so the classifier is the boundary.
  - Mitigation: (1) Fix §2.1 to state one rule: protected fields beat the variant, takeover is `pause`, and anything else is `desk`. Say that the classifier returns the **most privileged** class implied by any field present, and that an unknown variant is `protected`. (2) Have `ApplyCommand`'s request types make `grant`/`accept` impossible on non-answer variants (separate records per variant, or binding rejects them), so the mix never reaches the chain. (3) Add a unit test: takeover carrying `grant` classifies as `protected` (or fails binding), never as `pause`. Add another so that each `ApplyCommand` variant in the catalog has a classification test (an extension of `ARCH-CQRS-003`). Flag this to the engine spec's owner along with the `SND-WF-007` note.

- **N1 (residual): the raw `curl` human stand-in isn't tied to the temp home** [MEDIUM, residual of pass-2 N1]
  - Where: §12 "Acting as the human", second bullet (`curl -H 'X-Sindri-Caller: human' -H 'X-Sindri-Terminal: 1' -H 'X-Sindri-Client: prover' …`). Only the first bullet goes through `sindri-prove.sh`. §3.2 note: `ApproveProfile` still accepts S1's `confirmed` flag until the engine's challenge lands.
  - Attack (honest mistake): the wrapper's guard covers only the CLI. A prover or implementer that takes the `curl` form fills in the URL itself. If it reads `control.json` from an unset `AW_SINDRI_HOME`, or uses the real instance's port, it reaches the owner's process as `human` with a terminal. It can then decide, run desk commands, or approve a profile with `confirmed:true`. If it also omits `X-Sindri-Client: prover`, the row is `human:http`, and nothing flags that row. The pass-2 concern ("reaches the default home by omission") still applies to this half of the recipe.
  - Mitigation: route the `curl` form through the wrapper as well: `sindri-prove.sh curl <method> <path> [body]`. The wrapper reads the URL from the guarded temp home's `control.json` and adds the three headers itself, so the prover never writes a host, a port or `X-Sindri-Terminal`. Change the P3 signal (`curl -s -H 'X-Sindri-Caller: human' …/queue`) to use it. Optionally have `doctor` warn on any `human:http` or `human:prover` command row in the default home, which gives the owner a trace if the recipe leaks.

- **P3-2: `pause` and `request` from a session aren't repo-scoped, and a session's takeover isn't surfaced to the owner** [LOW, new]
  - Where: §3.1 (scope covers "list and item queries"); §4.2 (`pause` is open to sessions with no scope); §3.2 `FileRequest` (`itemId?`); §3.6 (a `taken-over` instance is in the queue's `withYou` group, but needs-you has no row for it); `ReadLedger` (it isn't stated whether "list" queries include it).
  - Attack (honest): the hook or an agent sends `workflow takeover` with a wrong address, for example an instance in another repo, or one the session was never attached to. The instance stops, its deadlines pause, and it sits in `withYou`, which reads as "the human has it", while only a human can hand it back. In the same way, `FileRequest` with an item id from another repo is accepted. `GET /ledger` from a session may still list other repos' `item.created` titles.
  - Mitigation: apply the `X-Sindri-Repo` scope to session **commands** too. Takeover and `FileRequest` on an item or instance outside the session's repo answer `SND-ITEM-404`, as queries do. State that `ReadLedger` is in scope for sessions (filtered to the repo's items plus item-less events). Have the needs-you projection add a `calm` row when an instance is taken over by a `session:*` actor ("taken over by a session; hand back with `sindri handback …`"), so a pause the human didn't make is visible.

### A02 — Cryptographic Failures

No findings.

### A03 — Injection

No findings. (Pass-2 N6 is resolved: same sanitizer on `ReadLedger`, a cap, a read-only port, a 64 KiB body limit, and the repo path taken from the profile.)

### A04 — Insecure Design

- **P3-3: with a stalled projector, view-reading commands still each hold the gate for 2 s, so rebuild and stop can starve** [LOW, new residue of N2]
  - Where: §2.1 (the head wait happens *inside* the gate; the gate is `SemaphoreSlim(1)`, which isn't FIFO-fair, with a 5 s acquire limit).
  - Attack (honest failure): the projector stalls while the engine or scheduler keeps issuing view-reading commands in process. Each one holds the gate for the full 2 s before failing. `RebuildViews` and `StopProcess` skip the head wait but still have to acquire the gate, and they can lose that race for more than 5 s and get `SND-API-009`. The fix still works on a retry, so this is not the pass-2 deadlock.
  - Mitigation: when `IProjectorStatus.Stalled` is set, have view-reading commands fail at once with `SND-VIEW-001` instead of waiting the 2 s. Extend the existing integration test so that `views rebuild` succeeds while a loop of view-reading commands runs.

### A05 — Security Misconfiguration

No findings.

### A06 — Vulnerable & Outdated Components

No findings.

### A07 — Identification & Authentication Failures

- **R2: the TTY plus variable check still has known gaps** [LOW, residual]
  - Where: §6 (`CODEX_SANDBOX`, `CURSOR_AGENT`).
  - Attack: `CODEX_SANDBOX` is absent in Codex's full-access mode, and `CURSOR_AGENT` isn't confirmed. An agent that runs in an editor's integrated terminal can have a real PTY. In those cases the CLI sends `human` together with `X-Sindri-Terminal: 1`, and that pair now unlocks `protected`, which raises the stakes of this check compared with rev 2.
  - Mitigation (unchanged): have each provider's installer or hook adapter export `AW_SINDRI_CHILD=1` into agent sessions, and test it in `providers/tests`. Confirm the remaining names with one recorded `env` dump per provider. Keep the list in one constant with a unit test.

### A08 — Software & Data Integrity Failures

No findings.

### A09 — Security Logging & Monitoring Failures

- An unlabelled human command (`human:http`) on the default home is recorded but never surfaced [LOW]. See the optional `doctor` warning in N1's mitigation.

### A10 — SSRF

No findings.

## STRIDE findings

### S — Spoofing
- The unwrapped `curl` stand-in reaches the real process as `human` with a terminal [MEDIUM]: N1.
- Agent-session detection gaps, now guarding `protected` [LOW]: R2.
- `X-Sindri-Terminal` itself: no findings within the threat model. It is ignored without `human` and is sent only by the CLI under the TTY and variable rule. Forging it is the out-of-scope case.

### T — Tampering
- A takeover payload carrying `grant` could be classified `pause` [MEDIUM]: P3-1.

### R — Repudiation
- `human:http` and `human:prover` rows on the default home aren't flagged [LOW]: N1 and A09.

### I — Information Disclosure
- `ReadLedger` scope for sessions isn't stated [LOW]: P3-2.

### D — Denial of Service
- Gate starvation of rebuild and stop while the projector is stalled [LOW]: P3-3.
- A session can pause any instance, in any repo, and only a human can undo it [LOW]: P3-2.

### E — Elevation of Privilege
- Session → protected through a misclassified `ApplyCommand` variant [MEDIUM]: P3-1.
- Session → human through the unwrapped prover `curl` [MEDIUM]: N1.

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium:** 2.
  - P3-1: write one fail-closed classification rule for `ApplyCommand`, with a test. This needs the engine spec to agree before engine P4/P5.
  - N1 residual: route the `curl` stand-in through `sindri-prove.sh`.
  - Each is a few lines in §2.1 and §12, and both are worth doing before P1.
- **Low:** 4 (P3-2, P3-3, R2, the A09 note).
- **Recommendation:** SHIP (no CRITICAL or HIGH). Fold the two MEDIUMs into the spec or answer them in writing. The LOWs can be answered in writing.

verdict: PASS
