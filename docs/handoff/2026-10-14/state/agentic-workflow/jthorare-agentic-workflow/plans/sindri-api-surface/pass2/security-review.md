# Threat Model — sindri-api-surface (pass 2, rev 2)

**Mode:** plan
**Source:** /Users/thor/repos/agentic-workflow/docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md (rev 2, §13 resolutions); pass 1 at `pass1/security-review.md`; engine spec cross-checked for takeover
**Reviewed:** 2026-10-10

**Threat model applied.** The same model as pass 1. Gates guard against agents' honest mistakes, not hostile agents. A session that deliberately forges `X-Sindri-Caller: human` is out of scope for v0 (owner decision; tokens come later). This pass reports only (a) pass-1 findings that are still unresolved and (b) new issues that rev 2 introduced.

## Summary

Rev 2 fixes both pass-1 HIGHs cleanly. A missing header now means `session` (§6), commands refuse any `Origin` (`SND-API-008`, P1), and both have endpoint tests and P1/P8 signals. The audit step is now outside validation and authorization. The race on "first decision wins" is closed by `CommandGate` plus fact keys, with a concurrency test. Dropping the client `Idempotency-Key` removes A08-1. Session detection is named and backed by a TTY check, `ILauncher` receives only ids, and `control.json` must hold a loopback URL. Nothing is HIGH. Four MEDIUMs remain. Two are new and cheap to fix before P1: (1) §12's prover recipe writes down how an agent acts as the human (`env -u CLAUDECODE … script`, `curl -H 'X-Sindri-Caller: human'`, `confirmed:true`), and nothing ties it to a throwaway home; (2) under the serialized `CommandGate`, a stalled projector blocks every command, including the `views rebuild` that fixes it. The other two are a spec contradiction (the hook sets takeover "through the `service` caller", which never arrives over HTTP) and the partial repo scope (MCP's `all` argument is controlled by the agent, and `ReadLedger` and the HTTP queries aren't scoped).

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 2 (MEDIUM ×2: N1 new, N3 new). Pass-1 A01-1 and A01-2 resolved |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 1 (LOW, N6 new). Pass-1 A03-1 resolved (C-26); A03-2 mostly resolved, the residue is in N6 |
| A04 Insecure Design | findings: 2 (MEDIUM R1 residual of A04-3; MEDIUM N2 new). Pass-1 A04-1 resolved; A04-2 mostly resolved, the residue is in N7 |
| A05 Security Misconfiguration | No findings. Pass-1 A05-1 resolved (C3 brought forward to P1 for commands) |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | findings: 1 (LOW, R2 residual of A07-1) |
| A08 Software & Data Integrity Failures | No findings. Pass-1 A08-1 resolved (client key dropped; fact keys only) |
| A09 Security Logging & Monitoring Failures | findings: 2 (LOW ×2, N4 and N5 new). Pass-1 A09-1 resolved except actor labels (folded into N1); A09-2 accepted by the owner (C-28) |
| A10 SSRF | No findings. Pass-1 A10-1 resolved (§7 loopback check) |

## Pass-1 resolution check

| Pass-1 finding | Status in rev 2 |
|---|---|
| A01-1 missing header = human [HIGH] | **Resolved.** §6, D4, P1 signal (no header → 403 on approve), §12 endpoint tests, P8 `curl` without the header |
| A01-2 CSRF from a loopback origin [HIGH] | **Resolved.** `SND-API-008` on any `Origin` for commands (P1); browser requests can't send the header without a preflight, so they're `session` anyway |
| A03-1 `OpenItem` injection | **Resolved** (C-26: only the item id and repo path; the session pulls its context through MCP). A small residue on where the repo path comes from is in N6 |
| A03-2 control characters, size | **Mostly resolved** (2,000-character cap, sanitizer at projection). The residue (`ReadLedger` bypasses the sanitizer; no body limit) is in N6 |
| A04-1 decision race | **Resolved** (`CommandGate`, fact keys, `SND-ITEM-409`, concurrency test). Nit: §2.1 writes the key as `v<n+1>` and §3.4 as `v<n>`; pick one |
| A04-2 request flood | **Mostly resolved** (20 open per actor). The residue is in N7 |
| A04-3 cross-repo data | **Partially resolved.** See R1 |
| A05-1 C3 deferred | **Resolved** |
| A07-1 unnamed session variables | **Mostly resolved** (named, TTY check, `.env.example`). See R2 |
| A08-1 unscoped idempotency key | **Resolved** (client key removed from v0) |
| A09-1 audit innermost; actor labels | Order **resolved** (§2.1 table plus an order test). Actor labels are **unresolved**: the host lists `human:cli`, `human:ui` and `session:http` but has no input to tell them apart. Folded into N1 |
| A09-2 guard refusals not logged | Accepted as S1's concern (C-28). Not raised again, but see N5 for the new `SND-API-007/008` refusals |
| A10-1 `control.json` URL | **Resolved** |
| Stream slots | **Resolved** (4 reserved for `human`) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **N1: The prover recipe writes down how an agent acts as the human, and nothing ties it to a throwaway home** [MEDIUM, new]
  - Where: §12 "Act as the human" (`env -u CLAUDECODE -u AW_SINDRI_CHILD script -q /dev/null $SINDRI <verb>`, `curl -H 'X-Sindri-Caller: human'`); "Seed an approved profile" (`curl … approve -d '{"hash":…,"confirmed":true}'`); §9's example `curl -H 'X-Sindri-Caller: human' …/sindri/queue`, which will be copied into `docs/csharp/sindri.md`; §2.1 (the actors `human:cli`, `human:ui` and `session:http`, with no input that tells them apart).
  - Attack (honest mistake, no forgery intended): the fail-closed default only works because agents don't know the way round it. Rev 2 now spells out that way in the spec and, through §9's example, in the contract doc that every implementer reads, and it includes the `protected` path (`confirmed:true` skips the typed confirmation). The recipe is only safe because of the separate `export AW_SINDRI_HOME=$(mktemp -d)` line. An agent that hits `SND-AUTH-002` against the owner's real Sindri (a prover rerun in the wrong shell, or an implementer "unblocking" itself the way the docs show) runs the same line without the export. It then decides, acts at the desk or approves the profile on the real ledger, and the audit row says `human:cli` (or an unspecified `human:*`), so the owner can't tell it apart from their own action. This is the owner-accepted forgery case in form, but it reaches the default home through documented procedure rather than intent, which is the honest-mistake class v0 is meant to guard.
  - Mitigation: (1) Put the stand-in behind a guard the recipe can't skip by omission: the `$SINDRI` prover wrapper (or the CLI itself whenever it sends `human` with no TTY ancestry) refuses to run unless `AW_SINDRI_HOME` is set explicitly and isn't the default home. For `curl`, have the prover use only a wrapper that reads the port from the temp home's `control.json`, so there's nothing to point at the real port. (2) Keep the recipe in the `/specToProvenPR` prover prompt, not in `docs/csharp/sindri.md`. In §9's byte-match example, use `$SINDRI queue --json` against a temp home, not a raw `human` `curl`. (3) Compose the actor from an explicit `X-Sindri-Client: cli|ui|mcp` hint that only labels the call and never sets its class. A `human` call without the hint is recorded as `human:http`. `doctor` (and needs-you) flags any `human:http` command on the default home. That gives the owner a trace when the recipe leaks. (4) Add a CLI unit test: agent variables unset, no TTY and the default home, so `human` must not be sent.

- **N3: The hook sets takeover "through the `service` caller", but `service` never arrives over HTTP** [MEDIUM, new]
  - Where: §3.5, row 1 ("Takeover is set by the hook's turn classification (§9.3) through the `service` caller"); §4.2 ("`service` never arrives over HTTP … nothing sent over the wire can claim it"); the engine spec lists `workflow takeover` as `human` (the engine spec's verb table: `workflow takeover <address>` | human).
  - Attack (spec gap leading to an implementation hole): the classifying hook is a bash script that runs out of process, inside the agent's environment (`CLAUDECODE` set, no TTY). It can only reach Sindri over the CLI or HTTP, so it is classed `session`, and takeover (desk) is refused. The tempting fixes each open the boundary: accepting `X-Sindri-Caller: service`, which makes `service` claimable on the wire with its desk rights; making takeover a `session` command, so any agent can take over or pause deadlines; or having the hook send `human`. The two specs also disagree about who may call takeover.
  - Mitigation: decide it in the spec now. Either the hook writes a file (like `judge/sessions`), which an in-process watcher reads and turns into a `service:hook-watcher` `ApplyCommand(takeover)`, keeping the wire unable to claim `service`; or takeover is `human` only, as the engine spec says, and the hook's classification raises a `request`/needs-you row. Make the engine and API specs agree, and add a unit test that `X-Sindri-Caller: service` is a 400 (`SND-API-007`).

### A02 — Cryptographic Failures

No findings.

### A03 — Injection

- **N6: `ReadLedger` returns raw ledger payloads that skip the sanitizer; small leftovers** [LOW, new]
  - Where: §3.6 ("scrubbed, then escaped … when the projection writes it"); §3.1 `ReadLedger` (reads `ledger.db` directly, `max` has no cap); §2.5 `ARCH-CQRS-002` (the exception gives `ReadLedger` the whole `ILedgerStore`, `Append` included); P10 (where the "repo path" comes from isn't stated).
  - Attack: sanitizing happens at projection time, so text in ledger payloads (request text, later the engine's agent text) reaches `sindri ledger` in the human's terminal with its ANSI or OSC sequences intact. §12 requires escaping on the stream but not on `ReadLedger`. A `max=1000000` makes one huge read. Kestrel's 30 MB default body still reaches the scrubber before the 2,000-character validator, unless the validator checks length first. For P10, if the repo path is taken from the item's `repo` field instead of the approved profile's repo list, odd text could choose the working directory.
  - Mitigation: run `PublicEvent` and `LedgerPage` payloads through the same sanitizer as the stream. Cap `max` (for example at 500). Give `ReadLedger` a read-only `ILedgerReader` port, not `ILedgerStore`. Set a request body limit on the `/sindri` group (64 KB) and have the validator check length before scrubbing. In P10, resolve `RepoName` to a path only through the approved profile.

### A04 — Insecure Design

- **R1: Repo scoping is advisory: the agent controls MCP's `all`, item tools aren't checked, and HTTP/`ReadLedger` aren't scoped** [MEDIUM, residual of A04-3]
  - Where: §8 ("`all: true` widens a query deliberately"; `sindri_item` and `sindri_item_timeline` take any `itemId`); §4.2 (sessions get `ReadLedger` across all repos); §3.1 (`GetQueue`, `ListRequests` and `ListDecisions` have no repo filter at the API).
  - Attack (data leaving the host, honest): an agent that can't find something retries with `all: true`, which is the natural move, and every repo's titles, timelines and pending decision questions enter its context and go to its model provider. The same happens when it runs `curl …/sindri/ledger?after=0` or `sindri queue` from its shell (`session` class, no scope), or when it fetches an item id it saw elsewhere. `FileRequest` from a session can still target another repo's item. The fix in §8 holds only on one of three paths, and on that path the argument that undoes it is in the agent's hands.
  - Mitigation: remove `all` from the MCP tools (a human widens in the UI or CLI). Have `sindri_item`, `sindri_item_timeline` and `sindri_request` refuse an item whose repo isn't the session's repo, with a clear code. For `session`-class HTTP callers, have `ReadLedger` and the list queries require a `repo` parameter, or default to returning ids and states without titles. Raw `curl` beyond that is acceptable for v0, but the CLI run from an agent session should send the cwd's repo as it does in MCP.

- **N2: A stalled projector blocks every command, including the rebuild that fixes it** [MEDIUM, new]
  - Where: §2.1 ("`CommandGate` … serializes every command. Each command then waits for the projector to reach the head"); §2.4 (only queries have the 2 s bound); §2.3 (the stall fix is `sindri views rebuild`, a desk command); engine commands also go through the same gate (§3.5).
  - Attack (honest failure, availability): one projection throws on an event, so the projector stalls (§2.3). The next command takes the gate and waits for the head without limit, so every later command, from the CLI, the UI or the in-process engine and scheduler, queues behind it. That includes `views rebuild` and `stop`. The control plane stops making progress, and the documented fix can't run. The same applies to any long-running handler under the gate (for example `OpenItem` waiting on the launcher, or `RunService` if it runs synchronously).
  - Mitigation: bound the head wait for commands with the same 2 s limit, returning `SND-VIEW-001` or `SND-API-004` and an audit row. Let `RebuildViews` and `StopProcess` skip the head wait, since they read no views. Give gate acquisition a timeout. Keep long work (jobs, launches) outside the gate: append `job.started` under the gate and run the job after releasing it. Add an integration test: with the projector stalled, `views rebuild` and `stop` still succeed and other commands fail fast.

## A05 — Security Misconfiguration

No findings.

### A06 — Vulnerable & Outdated Components

No findings.

### A07 — Identification & Authentication Failures

- **R2: The agent-session variables aren't verified, and the TTY backstop may not hold for every provider** [LOW, residual of A07-1]
  - Where: §6 (the variable list); pass 1 asked for a provider test.
  - Attack: some names in the list (for example `CODEX_SESSION_ID`, `CURSOR_AGENT`) aren't confirmed to be set by the providers' agent shells. `CODEX_SANDBOX` is absent in Codex's full-access mode. An editor's agent that runs commands in its integrated terminal may have a real PTY, and then the TTY check doesn't catch it either. In those cases the CLI sends `human`.
  - Mitigation: confirm each variable empirically (the existing `scripts/probe.sh` hook-input probe, or one recorded `env` dump per provider) and keep the list in one constant with a unit test. Where a provider sets nothing reliable, have its hook adapter or installer export `AW_SINDRI_CHILD=1` into the agent's environment, and test that in `providers/tests`.

### A08 — Software & Data Integrity Failures

No findings. (Note: the audit row and the domain event are two separate appends, so §2.1's "one event, one transaction" is true only of the domain event. A crash between them leaves an accepted command with no `command.handled` row. That's acceptable for v0. Say so in §2.1, or append the audit row in the same transaction when `Append` gains a unit of work, §11.)

### A09 — Security Logging & Monitoring Failures

- **N4: Actor labels can't tell the owner from a stand-in** [LOW, new form of A09-1]: see N1, mitigation (3).
- **N5: `SND-API-007` and `SND-API-008` refusals are invisible** [LOW, new]
  - Where: §6 and C3 (the `Origin` check is an endpoint filter, and the caller header is parsed in middleware; both run before `AuditingService`).
  - Attack: a page in a local browser probing commands (the exact attempt that `SND-API-008` exists to stop) leaves no ledger row, telemetry or `doctor` count.
  - Mitigation: emit a scrubbed telemetry warning and a counter (`sindri.guard.refused` by code) for both, and show the count in `doctor`. A ledger row isn't needed.

### A10 — SSRF

No findings.

## STRIDE findings

### S — Spoofing
- **The documented human stand-in reaches the real home by omission** [MEDIUM]: N1.
- **`service` claimed on the wire to make takeover work** [MEDIUM]: N3.
- Unverified session variables [LOW]: R2.

### T — Tampering
No new findings. The pass-1 decision race is closed. (Nit: the `v<n+1>` and `v<n>` key notations, A04-1 row above.)

### R — Repudiation
- Actor labels, and `human:http` not being separated out [LOW]: N1 and N4.
- Refusals before the chain aren't recorded [LOW]: N5.
- No audit row after a crash between the two appends [note]: the A08 note.

### I — Information Disclosure
- **Cross-repo reads through `all`, item ids, `ReadLedger` and HTTP** [MEDIUM]: R1.
- `ReadLedger` returns unsanitized payloads [LOW]: N6.

### D — Denial of Service
- **The serialized gate plus a stalled projector stops every command** [MEDIUM]: N2.
- **N7: the 20-open-request cap is per actor string, and every session shares one** [LOW, new]: actors are `session:mcp`, `session:cli` or `session:http`, not one per session, so a single session stuck in a loop uses up the request channel for every other session. Refuse a duplicate open request (same kind, item and normalized text) with a 409 that points at the existing one, and count per session id once one is available (for example from the MCP client's initialize info).
- `ReadLedger` `max` has no cap [LOW]: N6.

### E — Elevation of Privilege
- Session → human through the documented recipe [MEDIUM]: N1.
- Session → desk through a `service`-class takeover path [MEDIUM]: N3.

## Verdict

- **Critical findings:** 0
- **High findings:** 0 (both pass-1 HIGHs are resolved)
- **Medium:** 4. N1 (prover recipe and actor labels), N2 (gate plus a stalled projector), N3 (takeover through `service`) and R1 (residual repo scope). N1 and N2 are a few lines each in §2.1 and §12, and are worth fixing before P1 starts. N3 needs the engine and API specs to agree before the engine's P5.
- **Low:** 5 (N4, N5, N6, N7, R2), plus the A08 note and the key-notation nit.
- **Recommendation:** SHIP (no CRITICAL or HIGH). Fold the MEDIUMs into the spec or answer them in writing.

verdict: PASS
