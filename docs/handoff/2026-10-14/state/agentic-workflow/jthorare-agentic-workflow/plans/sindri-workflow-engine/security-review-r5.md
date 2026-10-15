# Threat Model — sindri-workflow-engine (pass 5 of 5, revision 5)

**Mode:** plan · **Source:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 5), with `security-review-r4.md` and §13 (H1–H20) · **Reviewed:** 2026-10-11

**Threat model:** honest mistakes only (A §17.28.3). Crafted bypasses are LOW or informational. §13's written answers aren't re-raised. Scope: (1) earlier findings that are still open, and (2) new issues in the revision-5 changes.

## Summary

Revision 5 resolves all four pass-4 MEDIUMs:
- M4-1 by `origin`;
- M4-2 by the owner in the ref and `SND-WF-012`;
- M4-3 by the pins digest, re-validation and append-only drift approvals;
- M4-4 by synchronous forwarding with a challenge bound to the child.

It also resolves L4-1, L4-2, L4-3 and L4-5, and the A09 audit note. No CRITICAL, HIGH or MEDIUM findings remain. Nine LOWs are left. They are mostly spec text that disagrees with itself, or bindings that are implied but not stated, plus tests missing from §10 for the pass-4 fixes. One LOW is about the new "Prover runs read verbs outside the sandbox" path.

## Pass-4 status

| Pass 4 | Revision 5 |
|---|---|
| M4-1 exemption by kind | **Resolved** (§5.3 step 6, §5.4 `origin`, P6 signal 2). Residual: L5-1 |
| M4-2 claim by item | **Resolved** in §5.4 (`SND-WF-012`, owner check, two §10 tests). §12 still has the old text: L5-2 |
| M4-3 drift pins unbound | **Resolved** (§4.2 pins digest, §4.1 re-validate plus evals, capability diff, append, no re-approval of a revoked hash). Residual: L5-3 |
| M4-4 forwarded proof | **Resolved** (§5.7 synchronous, challenge bound to the child's address and version, forwarded grants not stored). Residual: L5-4 |
| L4-1 human trigger on mismatch | **Resolved** (step 6: refused, nothing recorded). Code nit: L5-5 |
| L4-2 process-name heuristic | **Resolved** (§6.2 ancestry pattern, `doctor` re-probe). Residual: L5-6 |
| L4-3 `ui` class | **Resolved** (server-stamped, ranked). Test gap: L5-9 |
| L4-4 verb grammar | **Mostly resolved** (§6.3 grammar plus 4 words). §3.5 disagrees: L5-7 |
| L4-5 revoked child creation | **Resolved** (§4.1 child pins, revoke wake reaches parents) |
| A09 drift audit | **Resolved** (§4.1 appends a row and an event) |

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 3 (LOW) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 1 (LOW) |
| A04 Insecure Design | findings: 2 (LOW) |
| A05 Security Misconfiguration | findings: 1 (LOW) |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | findings: 1 (LOW) |
| A08 Software & Data Integrity Failures | findings: 1 (LOW) |
| A09 Security Logging & Monitoring Failures | No findings. (The drift audit note is resolved. Test gaps are in L5-9) |
| A10 SSRF | N/A — nothing in v1 fetches outbound (§13 S-L8) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **L5-1. The engine origin isn't limited to the engine's own notification kinds** [LOW]
  - Where: §5.4 "Origin decides the exemption, not the kind". §5.3 step 6.
  - Attack (honest): `origin` is set by the writer. Today only notices are `engine`. A later engine feature, such as a cleanup effect or a claim release on cancel, is naturally written `origin: engine`. It would then skip the revoke and drift recheck at dispatch without anyone choosing that. It's also unstated whether `engine` rows still pass the allowlist (pass 4's mitigation said they should).
  - Mitigation:
    - State that `origin: engine` is allowed only for the engine-owned kinds (`notify.desk` today), asserted when the row is written.
    - State that engine rows still pass the allowlist.
    - Test: `Recipe_notify_effect_is_refused_after_revoke_but_engine_park_notice_is_sent` (unit, P6). Today only the owner signal covers this.

- **L5-4. In forwarding, the nonce owner and the action binding are implied, not stated** [LOW]
  - Where: §5.7 "the CLI's command commits on the child, as the child's own trigger". §5.3 step 3 "nonce … for the current version". §4.2 "a challenge for one action".
  - Attack (honest implementation):
    - (a) The CLI addressed the parent, so it fetches the nonce for the parent. Step 3 on the child then fails, or gets special-cased, with "skip the nonce check for forwarded commands".
    - (b) Nothing says the request's verb and amount must equal the challenge's action. A request with `--grant 3` that carries a `grant 1 …` challenge passes if only the typed text is compared with the challenge.
  - Mitigation:
    - The server resolves the mirrored child before issuing the nonce and the challenge, and both are minted for the child's address and version.
    - §4.2 adds a rule: the request's verb, flags and amount must equal the challenge's action, or the request is refused with `SND-RECIPE-009` or `SND-WF-013`.
    - Tests: `Forwarded_grant_refused_when_child_moved` and `Challenge_amount_must_match_request`.

- **L5-5. A human trigger on a mismatch always returns `SND-RECIPE-006`** [LOW]
  - Where: §5.3 step 6 "A mismatch on a human trigger returns `SND-RECIPE-006`".
  - Attack (honest): between `recipe revoke` and its wake commit, a human `complete` on a revoked instance is told "drift, re-approve". The re-approval is then refused with `SND-RECIPE-005`, because a revoked hash can't be re-approved, and the user goes around in a loop. An unapproved hash is in the same position (`003`).
  - Mitigation: return the `RCP-CAP-003` code that matches: `003` unapproved, `005` revoked (fix: cancel), `006` drift.

### A02 — Cryptographic Failures
No findings.

### A03 — Injection

- **L5-7. `RCP-TRIG-002` is defined differently in §3.5 and §6.3** [LOW]
  - Where:
    - §3.5 reserves the ten engine verbs only, with no grammar.
    - §6.3 adds `[a-z][a-z-]{1,31}` and `approve`, `revoke`, `grant-tokens`, `override`.
    - §9.1 says only "reserved verb".
  - Attack (honest): P1 (c), the validator, implements from §3.5. `command.approve` then validates, and the grammar is enforced only by the CLI. The grammar also allows a trailing or doubled `-`, and `freeze`, `register` and `command` aren't reserved.
  - Mitigation:
    - Put one definition in §3.5 (the grammar, `[a-z][a-z0-9]*(-[a-z0-9]+)*` up to 32 characters, and the full reserved set including `freeze`, `register` and `command`). §6.3 references it.
    - Fixture: `RCP-TRIG-002-approval-word.yaml`.

### A04 — Insecure Design

- **L5-2. §12 still states the old claim identity** [LOW]
  - Where: §12 `effect.claim.ref` row: "A ref that already names this instance's effect identity counts as `created`". §5.4 now keys the identity by item and requires the owner, or an ended owner.
  - Attack (honest): the P6 (d) implementer reads the block table in §12 and skips the owner check. `SND-WF-012` at `start` still prevents two live attempts, so the impact is small. But the next path that creates attempts, such as a tracker re-route, inherits the gap.
  - Mitigation: §12 row: "`created` when the ref's recorded owner is this instance or an ended attempt, which re-points the ref to this instance; else `claimExists`".

- **L5-3. The batch resume after a drift approval doesn't say its key, its actor or its scope** [LOW]
  - Where: §4.1 "the instances parked on that drift, which the same approval resumes". §5.8 `spec-conflict: drift` `rearm` "re-evaluates that trigger". §5.8 "A drift wake on an instance that is already parked is recorded on its suspended frame".
  - Attack (honest implementation):
    - (a) One approval fans out N commits, but §5.8 says "every resume commits under the answer's own key". If all N commits reuse the approval's key, they can collide, or replay as `already applied`.
    - (b) An instance whose drift mark sits on a `rounds` or `budget` park could be popped by the batch, which performs the withheld loop transition with no grant. Only frames that are themselves `spec-conflict: drift` should be popped. For the others, the approval clears the mark only.
    - (c) A first (non-drift) approval also records the build's pins, but its subject is only the hash and version. That is safe only if challenges die on restart, and the doc doesn't say they do.
  - Mitigation:
    - Key each batch commit `recipe.approved:{seq}:{instance}`, with actor `system`, causation the approval event.
    - Pop only `spec-conflict: drift` frames, and clear the mark on other parks.
    - Either include the pins digest in every approve subject, or state that challenges are in memory and invalidated on restart.
    - Test: `Drift_approval_clears_mark_but_keeps_rounds_park`.

### A05 — Security Misconfiguration

- **L5-8. Running the Prover's read verbs outside the sandbox has no scope rule, and the probe doesn't cover that mode** [LOW]
  - Where: §6.1 "If its sandbox blocks loopback, the owner approves running the Prover's read verbs outside the sandbox". §6.2 the probe covers "Codex in each sandbox mode".
  - Attack (honest):
    - (a) To stop the prompts, the owner approves a prefix rule for the binary path, or approves the whole compound `export …; sindri() {…}; sindri …` line. Every verb, including in-process `recipe validate <any file>`, whose rule lines can quote file content, and `serve`, then runs unsandboxed for the whole phase. Write verbs are still refused, but only if the run is still classified `session`.
    - (b) Codex's escalated (unsandboxed) runs may not set the sandbox env marker the probe recorded. If the ancestry pattern doesn't catch it either, the run is classed `script`, not `session`. The impact is small (`script` can't `complete` or approve), but the P4 (agent) step "exits 2 with `SND-WF-007`" would then print `SND-RECIPE-004`, and the classification would be wrong without anyone noticing.
  - Mitigation:
    - Prefer allowing loopback to `sindri serve`'s port in the sandbox's network allowlist.
    - If commands must run unsandboxed, the runbook says to approve each exact read-verb command (`status`, `events`, `list`, `outbox list`) one by one, never a prefix rule or a compound line.
    - Add "outside the sandbox (escalated)" to the P2 probe matrix for each harness, and assert `session` in the golden.

### A06 — Vulnerable & Outdated Components
No findings.

### A07 — Identification & Authentication Failures

- **L5-6. The ancestry re-probe for Codex and Cursor can't run automatically** [LOW]
  - Where: §6.2 "`sindri doctor` re-probes when a provider's version changes". The Codex and Cursor probes are **(owner)** steps.
  - Attack (honest): `doctor` can see that the Codex or Cursor version changed, but it can't re-run their probes. If it reports `ok` because the Claude probe still matches, an old golden for Codex or Cursor silently misclassifies a renamed binary.
  - Mitigation: when a provider whose probe is an owner step changes version, `doctor` reports `warn: re-run the <provider> probe (owner step)`. It reports `ok` only after a probe recorded at that version.

### A08 — Software & Data Integrity Failures
Covered by L5-3 (c). No separate finding.

### A09 — Security Logging & Monitoring Failures
No findings. (The drift approval is append-only with its event. Test gaps are in L5-9.)

- **L5-9. §10 has no tests for the pass-4 fixes** [LOW, counted here]
  - Where: §10 doesn't list the origin exemption (L5-1), the drift pins digest, forwarded-proof refusal (L5-4), a human trigger on a drifted instance being retryable after re-approval, or `ui` refused on every write route. The table still says `Ui_and_mcp_reference_no_terminal_route`, not "no write route".
  - Mitigation: add `Drift_reapproval_refused_when_pins_moved_after_preview` (P2), `Human_complete_on_drifted_instance_is_retryable_after_reapproval` (P4), `Forwarded_grant_refused_when_child_moved` (P7), and `Ui_is_refused_on_every_write_route` (P2 or P4) to the §10 rows they belong to.

### A10 — SSRF
N/A — nothing in v1 fetches outbound.

## STRIDE findings

- **S — Spoofing:**
  - L5-6 (stale owner-step probes);
  - L5-8 (b) (an unsandboxed mode outside the probe).
- **T — Tampering:**
  - L5-3 (c) (first-approval pins rely on challenges dying on restart);
  - L5-4 (b) (the amount isn't bound to the challenge).
- **R — Repudiation:** No findings. `onBehalfOf` keeps the actor, and drift approvals append.
- **I — Information Disclosure:** L5-8 (a) (an unsandboxed in-process `recipe validate` can quote files outside the sandbox).
- **D — Denial of Service:** L5-5 (a misleading code sends the user around a re-approve loop).
- **E — Elevation of Privilege:**
  - L5-1 (a future engine-origin effect skips the revoke);
  - L5-2 (stale claim text);
  - L5-3 (b) (a batch resume pops a `rounds` park);
  - L5-7 (approval-word verbs).

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 0
- **Low findings:** 9 (L5-1 to L5-9)
- **Recommendation:** SHIP. Every earlier MEDIUM is resolved. The LOWs are spec-text alignments and test rows that can be folded in during the phase that owns them:
  - P1 (c): L5-7;
  - P2: L5-3, L5-6, L5-8 and the L5-9 rows for P2;
  - P4: L5-5;
  - P6: L5-1, L5-2;
  - P7: L5-4.

verdict: PASS
