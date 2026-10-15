# Threat Model — sindri-workflow-engine (pass 4 of 5, revision 4)

**Mode:** plan · **Source:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 4), with `security-review-r3.md` and `consolidated-review-r3.md` · **Reviewed:** 2026-10-11

**Threat model applied (A §17.28.3, `docs/csharp/bugfix.md` "Threat model"):** gates guard against honest mistakes. Crafted bypasses are LOW or informational. The written answers in §13 aren't re-raised. Scope: (1) pass-3 findings that are still open, and (2) new issues in the revision-4 changes: the §5.3 step 5 exemptions, `onBehalfOf`, same-hash drift re-approval, claim identity by item, challenge binding, the probed `HarnessMarkers` and the parent-process check, the `ui` class, and `RCP-TRIG-002`.

## Summary

Revision 4 resolves N-1 through N-4, L-1 and L-2, and the stack half of N-5. No CRITICAL or HIGH findings. Four new MEDIUMs, all in the revision-4 mechanisms:
- the engine-owned `notify.desk` exemption is keyed by effect kind, which contradicts the P6 revoke proof;
- claim identity by item lets two live attempts both own the claim;
- the drift re-approval challenge doesn't bind the pin set it previewed;
- the forwarded `onBehalfOf` proof isn't bound to the child's version or to the action.

Five LOWs cover a human trigger under an approval mismatch, the parent-process heuristic, how the `ui` class ranks, the verb grammar, and the child half of N-5.

## Pass-3 status

| Pass 3 | Revision 4 |
|---|---|
| N-1 harness markers | **Resolved in design** (§6.2: probed golden, parent-process check, named refusals). A residual in the process heuristic is L4-2 |
| N-2 challenge binding | **Resolved** (§4.2 subject = recipe hash and version, or instance version; `SND-RECIPE-009`). The same gap reopens on the two new paths: M4-3 (drift) and M4-4 (forwarding) |
| N-3 `RCP-CAP-005` | **Resolved** (`with:`, `at`, `expect` compared) |
| N-4 command verbs | **Resolved** (`RCP-TRIG-002`, `workflow command`). The verb grammar is L4-4 |
| N-5 revoke and stack depth | **Stack half resolved** (§5.1 the revoke replaces the stack). **Child half still open** (L4-5) |
| L-1 fact size and storage mode | **Resolved** (§6.1 1 MiB, `0600`) |
| L-2 UI channel | **Resolved** (§6.2 `ui` row). How it ranks is L4-3 |

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 3 (1 MEDIUM, 2 LOW) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 1 (LOW) |
| A04 Insecure Design | findings: 2 (1 MEDIUM, 1 LOW) |
| A05 Security Misconfiguration | No findings. |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | findings: 1 (LOW) |
| A08 Software & Data Integrity Failures | findings: 2 (MEDIUM) |
| A09 Security Logging & Monitoring Failures | No findings beyond L4-5's audit note. |
| A10 SSRF | N/A — nothing in v1 fetches outbound (§13 S-L8) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **M4-1. The engine-owned exemption is keyed by effect kind, but recipes use the same kind, which contradicts the P6 revoke proof** [MEDIUM]
  - Where: §5.4 "Engine-owned commands (`notify.desk` for parks, escalations, desk tasks and `takeoverIdle`) are exempt from the recipe approval check at dispatch". §5.3 step 5 "engine-owned commands are exempt". §12 `workflow.dev-notify` uses `effect.notify.desk` as a **recipe** effect. P6 Signal 2 expects that recipe command to show `refused (SND-RECIPE-005)` after a revoke.
  - Attack (honest implementation): the natural reading exempts by kind (`kind == notify.desk`). Then a revoked recipe's own `effect.notify.desk` goes out after the revoke. The P6 proof fails, or gets "fixed" by special-casing the dev recipe. The same pattern carries over to any later effect kind the engine also emits. The revoke guarantee ("refused at dispatch") quietly loses one kind.
  - Mitigation:
    - Key the exemption by **origin**, not kind: an `origin: engine|recipe` column on `workflow_commands`, set by the writer. Only `engine` rows skip the approval recheck. They still pass the allowlist.
    - State it in §5.3 and §5.4.
    - Test: `Recipe_notify_effect_is_refused_after_revoke_but_engine_park_notice_is_sent`.

- **L4-1. The approval check for a non-system trigger isn't specified (§5.3 step 5)** [LOW]
  - Where: step 5 says "a mismatch on any other **system** trigger turns the commit into a `spec-conflict` park". Human triggers are left out: `complete`, `answer`, `workflow command`, `start`.
  - Attack (honest):
    - If an implementer reads step 5 as covering only system triggers, a human `complete` or `workflow command` on a drifted instance applies before the drift wake parks it. The window opens at startup and at the revoke-wake commit. Dispatch still re-checks effects, but the transition and its gate overrides land.
    - If instead the refusal is recorded under the trigger key (step 2), a desk report's `run:{entry}.{n}` key is burned. After re-approval, the same `complete` replays the refusal (exit 2) forever, and the only way out is `cancel`.
  - Mitigation: a human trigger that hits a mismatch is refused with `SND-RECIPE-003`, `005` or `006`, and the refusal **isn't** recorded under its key (as with an actor refusal), so it can be retried after re-approval. Alternatively it parks `spec-conflict` with the trigger stored for `reevaluate`, like a system trigger. Pick one and add `Human_complete_on_drifted_instance_is_retryable_after_reapproval`.

- **L4-3. The `ui` class has no place in the class order, and its channel is self-reported** [LOW]
  - Where: §6.2 `ui` "read verbs only". §3.5 "actor ≥ `human`". "The CLI reports its channel". The architecture test covers only `human-terminal` routes.
  - Attack (honest): the UI reuses the CLI's HTTP client and reports `cli` with a TTY, so it gets `human` and can call `complete`, `cancel`, `start` and `answer --text`. Nothing tests "read verbs only". And since `ui` isn't ranked, `≥ human` comparisons are undefined for it.
  - Mitigation:
    - Define the order (`ui` is read-only and not comparable to `script` or `human`; every write route refuses it with `SND-WF-007`).
    - Give the UI its own client identity, set by its client library, for the server to stamp from.
    - Widen `Ui_and_mcp_reference_no_terminal_route` to "no write route" for `ui` until the UI design adds forms.

### A02 — Cryptographic Failures
No findings.

### A03 — Injection
- **L4-4. `RCP-TRIG-002` reserves names, but recipe verbs have no grammar and some approval-tier words aren't reserved** [LOW]
  - Where: §3.5 reserved list (`answer`, `accept`, `reject`, `resume`, `grant`, `cancel`, `takeover`, `handback`, `complete`, `start`).
  - Attack (honest authoring): `command.Accept`, `command.grant-tokens`, `command.approve`, `command.override` or `command.revoke` pass, and in `allowedCommands` and `status` they read like approval-tier actions, yet a plain `human` fires them. With no grammar, verbs can also contain spaces or `/`, which garble the address-based CLI line.
  - Mitigation: verb grammar `[a-z][a-z0-9-]{0,31}`, compared lowercase. Add `approve`, `revoke`, `grant-tokens`, `override`, `freeze`, `register` and `command` to the reserved set. Fixture `RCP-TRIG-002-approval-word.yaml`.

### A04 — Insecure Design

- **M4-2. Claim identity by item lets a second live attempt take the first attempt's claim, and §12 contradicts it** [MEDIUM]
  - Where: §5.4 "`effect.claim.ref` uses the **item** as its identity, so a later attempt of the same item finds its own claim and gets `created`". §12 "A ref that already names **this instance's effect identity** counts as `created`".
  - Attack (honest): attempt 1 of T-3 is parked or taken over, and still live. The owner runs `workflow start workflow.item --item T-3 --attempt next`. Attempt 2 reaches `claimed`, finds the ref with the item's identity, and gets `created`. Two live instances now work T-3 at once. That is exactly what `claimExists` exists to stop. The two sections also disagree on what the identity is, so the implementation will follow whichever one is read.
  - Mitigation:
    - The ref records the owning instance.
    - `created` only when the owner is this instance, or the owner attempt is terminal (`cancelled`, `dropped`, `done`), in which case the ref is re-pointed in the same effect.
    - Otherwise `claimExists`.
    - Alternatively, `start --attempt next` refuses while an earlier attempt of the item is live (fix: cancel it).
    - Align §12 with whichever rule is chosen. Test: `Second_live_attempt_gets_claim_exists`.

- **L4-5. A revoked pinned child is still not checked when the child is created (N-5, second half, unresolved)** [LOW]
  - Where: §4.3 `RCP-CAP-003` ("the hash is approved and not revoked, and its pins match the build"). §5.7 "Created: a row inserted in the parent's `Commit`". §4.2 the revoke preview counts "running instances that will park".
  - Attack (honest): revoke `workflow.step@X`. A new `workflow.item` instance (which pins `step: X`) still creates the child in its own `Commit`, because the parent's approval and build pins still match. The child's first desk-task notice goes out (engine-owned, exempt), and a human can do desk work on it (see L4-1) until a system trigger parks it. The revoke preview's count also leaves out parents whose pinned child is revoked.
  - Mitigation:
    - `RCP-CAP-003` also checks that each pinned child hash is approved and not revoked, in the parent's `Commit` that creates the child. A failure parks the parent `spec-conflict`.
    - The `recipe.revoked` wake also targets the parents of instances of the revoked hash.
    - Test: `Parent_parks_when_pinned_child_is_revoked`.

### A05 — Security Misconfiguration
No findings.

### A06 — Vulnerable & Outdated Components
No findings.

### A07 — Identification & Authentication Failures

- **L4-2. The parent-process check matches by name, which risks the same IDE-terminal false positive and misses `node`-hosted harnesses** [LOW]
  - Where: §6.2 "A marker that humans also see … is excluded, and the parent-process check covers it instead". `HarnessMarkers` "the process names".
  - Attack (honest):
    - (a) To catch Cursor's agent, the list includes `cursor` or `Cursor Helper`. A human in Cursor's integrated terminal descends from the same app, so the false positive that the marker exclusion removed comes back through ancestry.
    - (b) A Claude Code install that runs as `node` (an npm install rather than the native binary) has no `claude` ancestor by `comm`. With no env marker, that session classifies as `human`.
    - (c) The golden pins one snapshot. A provider update that renames a variable or binary drifts silently.
  - Mitigation:
    - The P2 probe records the **ancestry chain** (`comm` and argv[0] or the exe path) for each harness and mode, and for each IDE's human terminal, next to the env.
    - Match agent binaries (the `claude` script or binary path, `codex`, `cursor-agent`), never the IDE app.
    - Add a `sindri doctor` check that re-runs the probe when a provider's version changes. A disagreement with the golden is `warn`.

### A08 — Software & Data Integrity Failures

- **M4-3. A drift re-approval binds the challenge to the hash and version, which don't change. The pins the human previewed aren't bound** [MEDIUM]
  - Where: §4.1 "Re-approving the **same hash** records the new pins (a drift approval), and the preview shows which blocks changed". §4.2 subject = "the recipe hash and version".
  - Attack (honest race): in a drift approval the hash and version are the same before and after, so the only thing previewed is the pin set: the descriptor hashes of the running build. The owner previews, then rebuilds and restarts `sindri serve` (the dev loop is `dotnet run`) inside the 5-minute window, or a second block changes. The typed `approve workflow.step 1` still matches, and the server records pins the human never saw. A descriptor change can also add capabilities to a block, but the preview promises only "which blocks changed". It doesn't promise a rerun of the validator (`RCP-CAP-*`, wiring) against the new catalog, or a capability-use diff.
  - Mitigation:
    - The challenge subject for an approve includes `pinsDigest` (the hash over the descriptor and child pins the preview used). At confirm, recompute it, and if it differs, refuse with `SND-RECIPE-009`.
    - A drift approval re-runs the full validator and the evals against the current catalog, and its preview shows the capability-use diff, not just the changed block ids.
    - Test: `Drift_reapproval_refused_when_pins_moved_after_preview`.

- **M4-4. The forwarded `onBehalfOf` proof isn't bound to the child's version or to the action** [MEDIUM]
  - Where: §5.7 "Forwarded to the child as a system trigger carrying `onBehalfOf` (the original actor and the consumed challenge's id). The child accepts that as proof". §10 `Forwarded_grant_keeps_the_terminal_actor`.
  - Attack (honest race and honest implementation):
    - (a) Child propagation is asynchronous (the router and inbox run on 5 s loops). The human previews the parent's mirrored `rounds` park and types `grant 1 T-7`. Meanwhile the child has moved on, for example re-parked on a later round, or with a different reason. The parent's version hasn't moved yet, so the challenge is valid. The child applies the grant to a park the human never saw. This is N-2(b) again, one hop down.
    - (b) "Accepts that as proof" doesn't say what the child checks. An implementer who checks only that the challenge id exists and is consumed lets a mismatched action through, for example a forwarded `accept` that carries the id of a consumed `grant` challenge. And a `system` trigger with `onBehalfOf` becomes a path to terminal-class actions that nothing else limits.
    - (c) If the child has drifted, step 5 parks it `spec-conflict` and stores the forwarded grant as the trigger to `reevaluate`. A later plain `--resume` (human, no challenge) then replays a terminal-class grant that was authorized before the drift.
  - Mitigation:
    - When the parent mirrors a child, the challenge subject includes the child's address and version (from the mirror).
    - The child checks the proof: the challenge's action and amount equal the forwarded command, its subject's child version equals the child's current version (else `SND-WF-009`), and it was consumed by the parent's commit for this trigger key.
    - Only `system` triggers that carry a verified `onBehalfOf` may fire terminal-class actions.
    - A stored forwarded terminal-class trigger isn't replayed by `reevaluate`. The resume asks for a fresh challenge.
    - Tests: `Forwarded_grant_refused_when_child_moved`, `Forwarded_proof_must_match_action`.

### A09 — Security Logging & Monitoring Failures
- Note (folded into L4-5's area, not counted): a drift approval "records the new pins". Write it as a new `recipe.approved` event and approval row with `via: drift` and the previous pins referenced, never as an in-place update, so the ledger shows which pins were approved when. Also state whether a **revoked** hash can be re-approved through the same-hash path. It shouldn't be: `SND-RECIPE-005`, "approve a new version".

### A10 — SSRF
N/A — nothing in v1 fetches outbound.

## STRIDE findings

- **S — Spoofing:**
  - L4-2 (process-name heuristic);
  - L4-3 (`ui` channel self-reported).
- **T — Tampering:**
  - M4-3 (drift pins not bound to the preview);
  - M4-4 (forwarded proof not bound to the child's state or the action).
- **R — Repudiation:** the drift-approval audit note (A09). There's no other finding: `onBehalfOf` keeps the original actor.
- **I — Information Disclosure:** No new findings.
- **D — Denial of Service:** L4-1 (a burned desk-report key leaves only `cancel`).
- **E — Elevation of Privilege:**
  - M4-1 (a recipe effect skips the revoke via the kind exemption);
  - M4-2 (a second attempt takes the claim);
  - L4-1 (a human trigger on a mismatched instance);
  - L4-4 (approval-tier verb names);
  - L4-5 (a revoked child created anyway).

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 4 (M4-1, M4-2, M4-3, M4-4)
- **Low findings:** 5 (L4-1 to L4-5)
- **Recommendation:** SHIP. The four MEDIUMs are one-line spec fixes plus one test each. Fold them in:
  - M4-1 into §5.3, §5.4 and P6 (a);
  - M4-2 into §5.4, §12 and P6 (d);
  - M4-3 into §4.1, §4.2 and P2 (c);
  - M4-4 into §5.7 and P7 (a);
  - L4-1 into §5.3 and P4 (b);
  - L4-2 into P2 (e);
  - L4-3 into §6.2 and P2 (d);
  - L4-4 into P1 (c);
  - L4-5 into §4.3 and P7 (a).

verdict: PASS
