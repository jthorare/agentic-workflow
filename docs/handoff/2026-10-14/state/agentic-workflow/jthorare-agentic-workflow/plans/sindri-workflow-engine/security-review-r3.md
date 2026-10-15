# Threat Model — sindri-workflow-engine (pass 3 of 3, revision 3)

**Mode:** plan · **Source:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 3), with `security-review.md`, `security-review-r2.md`, `consolidated-review.md` and `consolidated-review-r2.md` · **Reviewed:** 2026-10-10

**Threat model applied (A §17.28.3, `docs/csharp/bugfix.md` "Threat model"):** gates guard against honest mistakes and shortcuts. Crafted bypasses are LOW or informational. This pass doesn't re-raise the written answers in §13 (for example, timeouts aren't capability fields, and unsetting harness markers is out of scope).

## Summary

Revision 3 closes pass 2's only HIGH (H-1). §6.2 now derives the actor class from a fixed table: harness markers mean `session`, MCP is stamped `session` by the server, a CLI without a TTY is `script`, and `human-terminal` needs the shared §4.2 challenge. `complete` refuses sessions, and Phase 4 signal 4 proves it. Most of M-A, M-B, M-D, M-E, M-F, L-a and L-b are resolved. Five new or leftover gaps remain, all MEDIUM or LOW:
- the marker list hasn't been checked against what each provider actually sets;
- the challenge binds the action text, not the hash or version the human previewed;
- `RCP-CAP-005` compares gate ids and thresholds, but not a gate's own `with:` or the evidence `expect`/`at`;
- recipe-defined `command.<verb>` names can collide with engine verbs;
- a revoke wake into a two-deep stack isn't defined.

There are no CRITICAL or HIGH findings.

## Pass-2 status

| Pass 2 | Revision 3 |
|---|---|
| H-1 actor derivation | **Resolved.** §6.2 table, W22, `complete` is `human` and refuses `session`, plus tests in §10. The marker list itself is still unverified (N-1) |
| M-A `--accept` scope | **Resolved.** §3.8: it overrides only that gate, then the remaining gates and evidence run at the current head, and the result is forwarded to the child. Binding to the previewed result is covered under N-2 |
| M-B desk `complete` | **Resolved.** `--run` is required (`SND-WF-005`), undeclared outcomes and outputs get `SND-WF-008`, and results are labeled `via: desk` |
| M-C CAP-005/006 by name | **Resolved for paths and loops.** Gate parameters and evidence fields aren't compared (N-3). A loop or state budget on a new state has no counterpart, and the instance budget bounds it (L, not re-raised) |
| M-D revoke reach | **Mostly resolved.** The `recipe.revoked:{seq}` wake, the preview count, and `outbox.refused` at dispatch are in. A revoked pinned child at child creation, and the stack depth, are still open (N-5) |
| M-E scrubbing | **Resolved.** The whole payload is redacted in the ledger writer (§5.2) |
| M-F terminal routes in UI/MCP | **Resolved.** The architecture test covers every `human-terminal` route, and MCP gets read verbs plus `workflow.request` only. The UI channel is missing from §6.2 (L-2) |
| L-a nonces | **Resolved** (§5.4: 128-bit, stored, 10 min, consumed, cleaned up) |
| L-b producer | **Resolved** (`EventDescriptor.Producer`, which the router checks) |
| L-c behaviour vs descriptor | Answered (§2, §13) |
| L-d file paths | **Mostly resolved** (the CLI sends content). There's no size cap or storage mode for fact artifacts (L-1) |
| L-e metrics | **Resolved** (§11 session refusals, overrides) |

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 2 (1 MEDIUM, 1 LOW) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 1 (LOW) |
| A04 Insecure Design | findings: 2 (1 MEDIUM, 1 LOW) |
| A05 Security Misconfiguration | No findings. |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | findings: 1 (MEDIUM) |
| A08 Software & Data Integrity Failures | findings: 1 (MEDIUM) |
| A09 Security Logging & Monitoring Failures | No findings. |
| A10 SSRF | N/A — nothing in v1 fetches outbound (§13 S-L8 stands) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **N-4. Recipe `command.<verb>` names aren't reserved, and their class and invocation path are undefined** [MEDIUM]
  - Where: §3.5 `on: command.<verb>` "from any non-terminal state (actor ≥ `human`)"; §6.3 has no verb that fires a recipe command; `RCP-TRIG-001`.
  - Attack (honest authoring): a hand-written recipe declares `on: command.accept` or `command.grant` on a transition with effects. Whichever route ends up firing it does so at `human` class. That gives the effect an "accept" that never needed the terminal challenge, and the name hides the difference. And because §6.3 has no verb for recipe commands, an implementer may route them through `workflow answer`, which mixes the two namespaces.
  - Mitigation:
    - Reserve the engine verbs (`answer`, `grant`, `accept`, `reject`, `resume`, `cancel`, `takeover`, `handback`, `complete`) in `RCP-TRIG-001`.
    - Name the CLI verb that fires a recipe command (for example `workflow do <address> <verb>`) and give it a class: `human`, or `human-terminal` when the transition has capability uses.
    - Fixture: `RCP-TRIG-001-reserved-verb.yaml`.

- **L-2. The Avalonia UI channel is missing from the §6.2 table** [LOW]
  - Where: §6.2 lists the CLI, MCP and services. "The CLI reports its channel" doesn't cover a UI client of the HTTP API.
  - Mitigation: add the row *UI → `human` (never `human-terminal`)* now, so the first UI phase doesn't have to guess. The architecture test already keeps terminal routes out of it.

### A02 — Cryptographic Failures
No findings. The nonces are 128-bit random values (§5.4).

### A03 — Injection
- **L-1. Fact and output content has no size cap or storage mode** [LOW] (from S2 L-d)
  - Where: §6.1 "the CLI sends file contents"; §5.1 `facts { ref, sha256, headSha }`.
  - Mitigation: cap `--fact`, `--output` and `--file` content (for example the same 256 KiB as recipes, or a profile key). Store the artifacts by hash, 0600, under the owner-only state directory, and send the content through `Aw.Redaction` before it is written.

### A04 — Insecure Design

- **N-3. `RCP-CAP-005` compares a gate's id and threshold, but not its `with:` or an evidence check's `expect`/`at`** [MEDIUM] (the remainder of H1/M-C)
  - Where: §4.3 `RCP-CAP-005` ("a superset of the base's, with thresholds at or above"); `RCP-CAP-002` covers capability parameters only; §3.5 gate `{ use, threshold?, with? }`, evidence `{ kind, at, expect }`.
  - Attack (honest generator): it keeps `gate.verify.judge` at threshold 0.8 but rewrites its `with: { question }` or rubric into something easier to pass. Or it changes an evidence check from `at: head` to `at: base`, or `expect: fail` to `expect: pass`. The guard is still "present" by id, so 005 passes, and the freeze diff is the only backstop.
  - Mitigation: in 005, a guard matches the base's only when the block id, every `with:` value, and the evidence `kind`/`at`/`expect` are equal. Raising a threshold stays allowed. Fixtures: `RCP-CAP-005-weakened-gate-with.yaml` and `RCP-CAP-005-evidence-at-base.yaml`.

- **N-5. A revoke wake into an instance that is already two frames deep isn't defined, and a revoked pinned child isn't checked when the child is created** [LOW] (the remainder of M-D)
  - Where: W16 "at most two deep"; §5.4 wake `recipe.revoked:{seq}` "Always (it parks)"; §5.7 "Created: inserted in the parent's `Commit`"; `RCP-CAP-003` names only the recipe's own hash and its pins against the build.
  - Attack (honest): take an instance that is parked and then taken over (two suspended frames), and revoke its recipe. Pushing a `spec-conflict` park would make the stack three deep. So the wake either fails the `Commit`, in which case the revoke silently doesn't reach the instance, or the implementer improvises. Separately, a parent whose pinned child hash has been revoked still creates the child row in its own `Commit`, because the build hasn't changed.
  - Mitigation:
    - A revoke or drift park **replaces** the stack rather than pushing onto it (its only way out is `cancel`), so depth never overflows.
    - `RCP-CAP-003` also checks that each pinned child hash is approved and not revoked, in the parent's `Commit` that creates the child.
    - Tests: `Revoke_parks_a_taken_over_park`, `Parent_parks_when_pinned_child_is_revoked`.

### A05 — Security Misconfiguration
No findings.

### A06 — Vulnerable & Outdated Components
No findings.

### A07 — Identification & Authentication Failures

- **N-1. The harness-marker list is the only line between `session` and `human`, and nothing checks it against what the providers actually set** [MEDIUM]
  - Where: §6.2 row 1 (`SINDRI_SESSION`, `CLAUDECODE`, `CLAUDE_CODE_ENTRYPOINT`, `CODEX_SANDBOX`, `CURSOR_TRACE_ID`, "the list in `HarnessMarkers`").
  - The repo's hook adapters read hook payloads and the provider home dirs (`CODEX_HOME`, `CURSOR_PROJECT_DIR`). They don't detect a session from the environment, so nothing backs this list.
  - Attack (honest, no crafting):
    - (a) **False negative.** `CODEX_SANDBOX` is, as far as is known, set only while Codex runs sandboxed. A Codex session with full access, running a command in a pty (unified exec), has no marker and does have a TTY, so it classifies as `human`. Following `next` and `allowedCommands`, it runs `workflow complete` on its own `verify` desk Step, which is the E2 case that §6.2 exists to stop.
    - (b) **False positive.** `CURSOR_TRACE_ID` is likely present in Cursor's integrated terminal for the human as well. If so, the owner can't approve or grant from that terminal (`SND-WF-007` or `SND-RECIPE-004`), and the honest workaround is to strip the variable, which teaches the habit that §13 lists as out of scope.
  - Mitigation:
    - Before P4 (b) lands, record the real environment of each provider's agent shell (sandboxed and not, pty and not) and of each IDE's human terminal. `scripts/probe.sh` already does this for hook inputs. Pin the result in a `HarnessMarkers` golden test that has one row per provider and mode.
    - Prefer markers the agent runtime always sets over sandbox-dependent ones.
    - Add a second signal that needs no environment variable: a parent-process ancestry check for the known agent binaries (`claude`, `codex`, `cursor-agent`). It is still honest-mistake grade.
    - Document which markers mean "human in an IDE terminal" and so must not count.

### A08 — Software & Data Integrity Failures

- **N-2. The challenge binds the action text, not the hash or version the human previewed** [MEDIUM]
  - Where: §4.2 step 1 ("a challenge for one action, such as `approve workflow.step 1` … `accept T-1/step/1`"), step 3; §5.4 command nonces are version-bound but minted separately; §6.3 `recipe approve <file|id@hash>`.
  - Attack (honest race):
    - (a) The human runs `recipe approve recipes/sindri/workflow.step.yaml`, reads the preview, and types `approve workflow.step 1`. Meanwhile an agent session working in the same worktree saves an edit to the file without changing `version`. If the CLI reads or sends the file again at confirm time, the server approves content the human never saw. The action text matches, and the file wasn't registered under that version before, so `RCP-VERSION-001` doesn't fire.
    - (b) For `accept` and `grant`, the preview ("`--accept` fires verify -> done with no effects", "grant 1") was rendered for version *v*. Before the human confirms, a forwarded child event or a revoke wake moves the instance to *v+1*. If the CLI then mints the command nonce for *v+1*, it confirms an action whose consequences differ from what was previewed.
  - Mitigation:
    - The server stores the challenge as `(action, subject hash or instance version, nonce, expiresAt)` and returns the preview it rendered from that subject. On confirm, it applies to exactly that hash or version, and if the subject moved it refuses with `SND-RECIPE-009` or `SND-WF-009` ("the preview changed, run it again").
    - For `approve <file>`, the CLI uploads the content once, at challenge time, and the approval names the hash, never the path.
    - Tests: `Approve_applies_the_previewed_hash_only`, `Accept_refused_when_version_moved_after_preview`.

### A09 — Security Logging & Monitoring Failures
No findings. M-E and L-e are resolved.

### A10 — SSRF
N/A — nothing in v1 fetches outbound.

## STRIDE findings

- **S — Spoofing:** N-1 (the marker list decides `session` vs `human`); L-2 (the UI channel is unlisted).
- **T — Tampering:** N-2 (approve applies content or a state that wasn't previewed); N-3 (a generated recipe keeps a guard's id but weakens what it checks).
- **R — Repudiation:** No findings. `via: desk`, the actor on every event, and forwarding that keeps the actor cover it.
- **I — Information Disclosure:** L-1 (fact artifacts' storage mode and redaction).
- **D — Denial of Service:** L-1 (no size cap). Nonce cleanup is resolved.
- **E — Elevation of Privilege:** N-4 (a recipe `command.accept` at `human` class); N-5 (a revoke that doesn't land at stack depth 2, and a revoked child created anyway).

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 4 (N-1, N-2, N-3, N-4)
- **Low findings:** 3 (N-5, L-1, L-2)
- **Recommendation:** SHIP. Fold the findings into the phases that own the code:
  - N-1 into P4 (b), the actor table;
  - N-2 into P2 (c), the challenge;
  - N-3 into P1 (d);
  - N-4 into P1 (c) and P4 (b);
  - N-5 into P2 (c) and P7 (a);
  - L-1 into P4 (c);
  - L-2 in the doc now.

verdict: PASS
