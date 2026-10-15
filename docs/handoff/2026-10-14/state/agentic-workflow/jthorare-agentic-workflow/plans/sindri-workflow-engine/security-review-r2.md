# Threat Model — sindri-workflow-engine (pass 2, revision 2)

**Mode:** plan · **Source:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 2), with pass 1 (`security-review.md`), `consolidated-review.md`, and S1 `feat/sindri-s1-app-shell` (`CommandTable.Interactive()`, `EventScope.Actor`, `EndpointTable`) · **Reviewed:** 2026-10-10

**Threat model applied (A §17.28.3, `docs/csharp/bugfix.md`):** gates guard honest mistakes and shortcuts. Crafted bypasses are LOW/informational, and the written answers in §13 are not re-raised.

## Pass-1 status

| Pass 1 | Status in revision 2 |
|---|---|
| H1 generated recipe weakens guards or budgets | **Partly resolved.** `RCP-CAP-005/006` and the code-chosen base are in. Dominating-path guards, new or renamed loops, absent budgets and timeouts are still open (M-C) |
| H2 fact-fed capability parameters | **Resolved.** `RCP-CAP-007` takes literals or `{ profile }` only, with a run-time intersection. This is stricter than pass 1 asked |
| H3 trigger authority and self-grant | **Partly resolved.** The W22 classes, `SND-WF-007` and the capped, terminal-only grant are in. The doc never says how a class is derived (H-1) |
| M1 approval was a body flag | **Resolved for `recipe approve`** (§4.2 challenge, S1-D5). The architecture test covers only the approve route (M-F) |
| M2 revoke cascade | **Resolved for derived recipes.** Running instances and child waits are still open (M-D) |
| M3 children by id | **Resolved:** pinned by hash at approval, and counted in `RCP-CAP-001` |
| M4 child recursion | **Resolved:** `RCP-CHILD-002/003` |
| M5 sleep shift starvation | **Resolved:** W18, wall time minus monotonic time, at least 60 s, per-instance keyed `shift` |
| M6 YAML features and canonical form | **Resolved:** §3.1 refuses duplicate keys, anchors, aliases, merge keys, tags, size and depth; hashing is JCS over the normalized form; approval shows a preview with a diff |
| M7 `Commit` checks the version before the key | **Resolved:** §5.3 checks the key first; `Retried_command_returns_AlreadyApplied_not_conflict` |
| M8 registry events | **Resolved:** `recipe.registered/approved/revoked` |
| M9 scrubbing | **Partly resolved.** It is a field list, not the whole payload (M-E) |
| L1–L8 | Answered in §13. The answers are consistent with the threat model |

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 1 (MEDIUM) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 1 (LOW) |
| A04 Insecure Design | findings: 2 (MEDIUM) |
| A05 Security Misconfiguration | No findings. |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | findings: 1 (HIGH) |
| A08 Software & Data Integrity Failures | findings: 4 (2 MEDIUM, 2 LOW) |
| A09 Security Logging & Monitoring Failures | findings: 2 (1 MEDIUM, 1 LOW) |
| A10 SSRF | No findings. |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **M-F. Only the approve route is kept out of the UI and MCP. The other `human-terminal` routes, and MCP's `human` routes, are not** [MEDIUM]
  - Where: §4.2 step 4; §10 `Ui_and_mcp_reference_no_approve_endpoint`; W22 "Sessions and MCP can only file requests"; §6.2 `--grant`, `--accept` and `revoke` are `human-terminal`.
  - Attack (honest wiring): a later MCP tool or Avalonia view model maps `POST …/answer` with `grant`/`accept`, or `…/revoke`, the same way a normal button is mapped. `allowedCommands` advertises them to the UI. The architecture test passes because it only looks for approve.
  - Mitigation:
    - Generate the architecture test from the verb table: no MCP tool routes to any command whose class is `human` or higher, and no UI binding routes to any `human-terminal` command.
    - The MCP host stamps `session` on the server side and never takes the class from the client.
    - Define what "file a request" means (for example a `request.filed` event that shows up in `--needs-me`), so "sessions can only file requests" can be implemented.

### A02 — Cryptographic Failures
No findings. The approval and command nonces should come from `RandomNumberGenerator`. That is an implementation note, not a finding.

### A03 — Injection
- **L-d. `--fact <key>=<file>` and `--output <fact>=<file>` take paths** [LOW]
  - Where: §2 desk runtime; §6.2 `start`, `complete`.
  - Attack (honest): if the server resolves a path the client supplied, a relative path resolves against the server's cwd, or a large file gets stored as a fact.
  - Mitigation: the CLI reads the file and uploads its content. The server never opens a path a client gave it. Cap the size, and store fact artifacts 0600 under the owner-only state directory.

### A04 — Insecure Design

- **M-A. `--accept` "applies the escalated transition", but its scope isn't defined** [MEDIUM]
  - Where: §3.8 `escalated`; §3.5 "gates in order; the first failure refuses"; §5.6 "Escalated: escalated, with a reference to the child".
  - Attack (honest shortcut):
    - Gate 1, a rung-3 gate, comes back uncertain, so gates 2..n and the `evidence` never run.
    - `--accept` then applies the transition and its effects with those later checks skipped. One human override of a single model gate becomes a skip of every guard on that transition.
    - The accept can also arrive days later, after `headSha` has moved, against evidence that is now stale.
    - For an escalation that came from a child, the parent has no escalated transition of its own, so what `--accept` applies there isn't defined.
  - Mitigation:
    - `--accept` overrides only the escalated gate result. The engine then evaluates the remaining gates and evidence, and any refusal follows §5.4.
    - The override is bound to the gate result shown (block, score, the facts' `headSha`). If the head moved, re-evaluate before applying.
    - Accept and reject on a child-originated escalation are forwarded to the child, the same way parks are (§5.6), and the override is recorded on the child's transition.
    - Tests: `Accept_overrides_only_the_escalated_gate`, `Accept_on_parent_is_forwarded_to_child`.

- **M-C. `RCP-CAP-005/006` compare by name, so a restructured generated recipe can still drop guards or raise limits** [MEDIUM] (H1 partly open)
  - Where: §4.3 `RCP-CAP-005` ("on that transition"), `RCP-CAP-006`.
  - Attack (an honest generator):
    - It keeps `evaluate → report` and its gates, but removes the `checks` state, or adds a new path around it. The deterministic gate that guarded the path to the effect sat on an upstream transition, so `005` doesn't see it go. Pass 1 asked for "every path that dominates it".
    - It renames loop `attempts` to `tries` with `cap: 30`. A new state carries its own `budget`, or the recipe drops `budget:` altogether. With nothing in the base to compare against, `006` is silent.
    - It changes a timeout from `escalate` to `retry`, or from `2d` to `365d`.
  - Mitigation:
    - `005`: the guards on every path from `initial` to a kept capability use are a superset of the base's guards on its paths. Equivalently, refuse a generated recipe that removes a base state or transition that dominates a capability use.
    - `006`: a loop, state budget or timeout with no counterpart in the base must be at or below the minimum the base declares. An absent budget counts as unbounded, so it fails when the base has one. Timeout `then` may only move toward `park` or `escalate`, and `after` may not grow.
    - Fixtures: `RCP-CAP-005-bypass-path.yaml`, `RCP-CAP-006-renamed-loop.yaml`, `RCP-CAP-006-dropped-budget.yaml`.

### A05 — Security Misconfiguration
No findings. Loopback-only and the owner-only state directory are answered in §13.

### A06 — Vulnerable & Outdated Components
No findings.

### A07 — Identification & Authentication Failures

- **H-1. The actor class (W22) is the access control for every command, but the doc never says how it is derived. `workflow complete` is only `human`** [HIGH]
  - Where:
    - W22; §6.2 Actor column (`complete`, `answer --text`, `cancel`, `takeover`, `start` and `register` are `human`);
    - §13 S-L3 ("CLI at a TTY, CLI without a TTY, MCP, or a service"), which never maps those channels to classes;
    - §3.7 / §6.3, where `next` says "…then run: `sindri workflow complete … --outcome done`";
    - consolidated tension 1, which says `complete` "needs a human actor (CLI at a terminal), never a session".
    - On S1, "interactive" is `!IsInputRedirected && !IsOutputRedirected` in the client, and the event actor is a fixed `host:sindri`.
  - Attack (honest shortcut, no crafting):
    - (1) A Claude, Codex or Cursor session reads `next` and runs `sindri workflow complete T-1/step/1 --outcome accepted` from its shell. If "CLI without a TTY" counts as `human` (the only reading S-L3 allows besides `session`), the agent closes its own `verify` and `checks` desk Steps. That is "skipping a phase", which is in scope. It can also `takeover` its instance to pause the timeout, or `answer` its own `needs-answer` park.
    - (2) Agent shells that run commands in a pty (Codex, Cursor's terminal, a Warp agent) pass the TTY heuristic. Then even `human-terminal` (`--grant`, `--accept`) rests on isatty alone, because §4.2's typed challenge is specified only for `approve`.
    - (3) The CLI computes the class and sends it, so the server can't check it. That is acceptable under the threat model only if the CLI's rule is conservative.
  - Mitigation: write a derivation table into W22 or §6.2.
    - **`session`:** any harness marker is present, whatever the TTY state. That covers `SINDRI_MODE`, a Sindri session id, and the provider markers the hook adapters already know (for example `CLAUDECODE`, Codex and Cursor session variables).
    - **`human`:** the CLI with no session marker.
    - **`human-terminal`:** `human` with stdin and stdout on a TTY, **and** the same server-issued typed challenge as §4.2 (for example `grant T-1 1`, `accept T-1`). Never isatty alone.
    - **`system`:** only the in-process built-in services. No client can claim it.
    - **MCP and the UI:** MCP is always `session`, stamped by the server. The Avalonia UI is `human`.
    - Make `complete` refuse `session` explicitly, as tension 1 decided. When the class is `session`, `next` and `status` show the request path ("ask the human to run …"), not the command to copy.
    - Tests: `Session_marker_makes_actor_session_even_on_a_tty`, `Complete_from_a_session_is_SND_WF_007`, `Grant_without_typed_challenge_is_refused`.

### A08 — Software & Data Integrity Failures

- **M-B. A desk `complete` isn't bound to the open run, and can set any fact** [MEDIUM]
  - Where: §2 desk runtime (`--output <fact>=<file>`); §5.4 (the command id is a version-bound nonce, while a Step result's id is the run token); §5.3 (`SND-WF-002` "returns … a fresh nonce"); §6.1 (the CLI fetches the version and nonce itself).
  - Attack (honest retry or duplicate):
    - Each CLI run fetches a fresh nonce, so running `complete --outcome failed` twice (for example after an exit-2 timeout) applies to whatever desk task is open by then. A recipe whose states share outcome names moves twice. Exactly-once holds within one CLI invocation, not across re-runs. The Phase 4 signal ("re-sending with its used nonce") tests a path a human never takes.
    - `--output fact:stepSpec=…`, or any key not in the Step's `Outputs`, sets a fact that later `when: { fact, is: present }` or `produces` reads.
    - Receipts and metrics can't tell a desk-reported `checks: passed` from a run check.
  - Mitigation:
    - `complete` carries the run token from `status` (`{entry}.{n}`) as its trigger id, so a stale or second report is `SND-WF-005` or `AlreadyApplied`.
    - The CLI never auto-retries a command on `SND-WF-002` with a fresh nonce.
    - Refuse outputs that aren't in the descriptor's `Outputs` (a `SND-WF-0xx` code), and refuse outcomes the descriptor doesn't declare.
    - Record `runtime: desk` and the actor on the transition. Receipts label desk outcomes, and they never count as deterministic evidence.
    - Optional and informational: refuse `verify` from the same actor that completed `produce` in the same entry chain.

- **M-D. Revocation reaches a running instance only at start or dispatch, and a revoked child isn't defined** [MEDIUM] (M2 partly open)
  - Where: W11; `RCP-CAP-003` ("start, dispatch"); §3.8 ("After revoke, a `spec-conflict` park says …", but no trigger enters that park); §5.6 "Created: inserted in the parent's `Commit`".
  - Attack (honest): the human revokes `workflow.step@h` because its checks are wrong. Running instances keep moving through their gates and desk Steps, and parents keep creating children pinned to `h` until an effect dispatches. `RCP-CAP-003` says "revoked hash" for the recipe being started, and "child hashes no longer match the build" doesn't cover a revoked child, since the build hasn't changed.
  - Mitigation:
    - `revoke` writes a keyed `revoked` trigger to every live instance of the hash and of its derived recipes, in the same transaction or through the inbox. Each one parks `spec-conflict`, which is the §3.8 text.
    - `Commit` re-checks the child's approval when a parent enters a `child` wait. A revoked child parks the parent with `spec-conflict`.
    - `RCP-CAP-003` also names a revoked pinned child.
    - Test: `Revoke_parks_running_instances_and_their_children`.

- **L-a. The command nonce lifecycle isn't specified** [LOW]
  - Every `GET /workflow/instances/{address}` hands out a nonce, and nothing says when it expires or how unused ones are cleaned up. Give nonces a TTL and keep one live nonce per (instance, version) rather than one per read.

- **L-c. A descriptor hash pins the descriptor, not the block's behaviour** [LOW, informational]
  - A gate's logic can change while its `Version` and descriptor stay the same, and the approval still holds. Document that a behaviour change bumps `Version`, and that code review is the backstop.

### A09 — Security Logging & Monitoring Failures

- **M-E. Scrubbing is a list of fields, not the whole payload** [MEDIUM] (M9 partly open)
  - Where: §5.2 ("Gate reasons and inputs pass through `Aw.Redaction`"), while W21 puts the full post-state delta in each event: the park `question`, `answer --text/--file`, the `override` note and `workflow.refused` content.
  - Attack (honest): a human pastes an error that contains a token into `answer --text`. It lands in the append-only ledger and is served by `/sindri/events`.
  - Mitigation: redact the whole serialized payload in the single `LedgerEntry` writer, not field by field. Store `--file` answers as artifacts by hash.

- **L-e. Shortcut attempts and overrides aren't in the metrics** [LOW]
  - Add `SND-WF-007` refusals by verb and actor class (an honest agent trying a human verb), plus `--accept` overrides and grants per recipe, to §11's metrics and A §17.2's digest.

### A10 — SSRF
No findings. Nothing in v1 fetches outbound. §13 S-L8 stands.

## STRIDE findings

- **S — Spoofing:** H-1 (actor class derived on the client, undefined, and passed by a TTY heuristic); M-F (MCP and UI routes). **L-b [LOW]:** `EventDescriptor` names no producer, so the router accepts any registered kind it finds on the ledger. Add `producer` to the descriptor, and have the router skip events of that kind from other actors.
- **T — Tampering:** M-B (a stale or duplicate `complete`; undeclared outputs); M-C (a restructured generated recipe).
- **R — Repudiation:** M-B (desk outcomes aren't labeled); L-e.
- **I — Information Disclosure:** M-E.
- **D — Denial of Service:** L-a (nonces pile up). Child recursion and the sleep shift are resolved.
- **E — Elevation of Privilege:** H-1 (a session completes, answers or takes over its own work); M-A (`--accept` skips the remaining guards); M-C; M-D.

## Verdict
- **Critical findings:** 0
- **High findings:** 1 (H-1: how the actor class is derived, and `complete`'s class)
- **Medium findings:** 6 (M-A, M-B, M-C, M-D, M-E, M-F)
- **Low findings:** 5 (L-a, L-b, L-c, L-d, L-e)
- **Recommendation:** FIX-CRITICAL-FIRST. Add the derivation table and the typed challenge for `human-terminal` to W22 and §6.2 before P2 and P4. Fold M-A and M-B into P3 and P4, M-C into P1(d), M-D into P2(c) and P7, M-E into P4, and M-F into P2(d).

verdict: NEEDS_WORK
