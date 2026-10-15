# Threat Model — 2026-10-10-sindri-context-retrieval (round 3, final)

**Mode:** plan
**Source:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (worktree `ctx-wt`), the round-2 review `../round2/security-review.md`, and the §14 "Round 2" answers
**Reviewed:** 2026-10-10

**Threat model (unchanged).** This is a single-user local tool on loopback. In scope: honest mistakes, and data leaks of ring-1 work text into repos, public PRs, personal sessions or Prism, or model providers without scrubbing. Prompt injection from untrusted records is also in scope. A hostile local user or an adversarial agent is out of scope, so those findings are rated LOW with a note. Settled decisions are not reopened: D2, D11, D17, no bearer token, and S-L5.

## Summary

The three round-2 findings that needed action (N1 HIGH, N2 and N3 MEDIUM) are resolved in the text.

- **N1:** stage owners now work only in an isolated fixture environment (brief item 3). Every signal part that touches real sources is an owner check that the human runs in the work repo, and it is recorded in the PR as pass/fail and counts only. The lead "never runs anything against work data". The PR body, `evidence.md` and `owner-check.md` are checked for work identifiers before a PR is marked ready.
- **N2:** each stage gets its own `AW_SINDRI_HOME` and port (7491 and up), plus a fixture profile. Migrations are named and numbered per branch, and "only throwaway homes ever ran an unmerged migration".
- **N3:** §8.8 now shows `mcp: { enabled: false }`, which matches §3.

L-c is resolved (S-FX is stated as synthetic, "never captured from a real workspace"). L-a and L-b are still open as LOW, and nothing in §14 records them as accepted. There is one new LOW: the identifier check needs inputs that the stage owner is told not to touch. Result: 0 CRITICAL, 0 HIGH, 0 MEDIUM.

### Round-2 findings: verification

| Id | Round-2 finding | Text now | Status |
|---|---|---|---|
| N1 [HIGH] | Stage owners hand-test against real sources from a personal repo | Brief item 3: hand tests run only in `<scratch>/home-<id>`, on their own port, with a fixture profile and the fake Linear and Onyx servers, and never touch "the owner's home, port, profile, work repo, Linear or Onyx". Item 4 and "Work data stays with the owner": real-source parts are **owner checks** run by the human in the work repo, recorded as pass/fail and counts only. S-7B and S-8's arm decision are fully owner-run. The Team table bars the lead and owners from work data. The PR, evidence and owner-check text is grepped for team keys, the Slack domain and `linear.app/<workspace>`. The Phases preamble repeats the owner-check rule for every signal | **Resolved.** Mitigations 1, 2 and 4 are present. Mitigation 3 (a profile with no real sources) is met by the fixture profile in a throwaway home. See new LOW N4 |
| N2 [MEDIUM] | Parallel builds share the owner's real home and port | Per-stage `AW_SINDRI_HOME` and port, recorded by the lead in `stages.md`. Migrations are named and renumbered on rebase, and their number and content freeze at merge. Only throwaway homes run unmerged migrations | **Resolved.** No startup check refuses an unknown higher migration number. That check is now defense in depth only, because no branch build opens the real store. §6.2 and §10 keep overrides through a rebuild, which removes the `SND-CONTEXT-004` "rebuild" risk |
| N3 [MEDIUM] | `context.mcp.enabled` default contradicted §3 | §8.8 `mcp: { enabled: false } # off until approved (§3)`, and "Defaults are what's shown" | **Resolved** |
| L-a [LOW] | Labels copied from code strings count as `trusted` | The §7 trust table is unchanged ("Graph nodes and edges from deterministic extraction … `trusted`") | **Open, LOW.** See A04 |
| L-b [LOW] | Doctor `context:onyx` doesn't check providers or telemetry | §8.8: the doctor still checks only the bind, the version and that the key can search | **Open, LOW.** See A05 |
| L-c [LOW] | Fixture recordings captured from a real workspace | S-FX: "all **synthetic**, never captured from a real workspace". §14 Round 2: "fixtures stated synthetic" | **Resolved** |

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | No findings. (N1 resolved) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | No findings. (§7 escaping, the permalink allowlist and §5.1 argument validation are unchanged) |
| A04 Insecure Design | findings: 1 (L-a, carried) |
| A05 Security Misconfiguration | findings: 1 (L-b, carried). N3 resolved |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | No findings. |
| A08 Software & Data Integrity Failures | No findings. (N2 resolved; L-c resolved) |
| A09 Security Logging & Monitoring Failures | findings: 1 (N4, new LOW) |
| A10 SSRF | No findings. |

## OWASP Top 10 findings

### A01 — Broken Access Control
No findings.

### A02 — Cryptographic Failures
No findings.

### A03 — Injection
No findings.

### A04 — Insecure Design
- **L-a. Labels copied from code strings are `trusted`** [LOW, carried from round 2]
  - Where: the §7 trust table, and §8.7's bulk accept of deterministic proposals.
  - Attack: a registry title or setting description is copied verbatim into a node label, and that label is returned unfenced. The path needs the owner's own repo to contain text that reads like an injection, so it is unlikely.
  - Mitigation (one line in §7): node keys and structure from deterministic extraction are trusted. A label copied from source text is either fenced like code text until a human accepts it, or limited to an identifier character class. Alternatively, record it in §14 as accepted.

### A05 — Security Misconfiguration
- **L-b. Doctor `context:onyx` doesn't verify the no-cloud-provider or telemetry settings** [LOW, carried from round 2]
  - Where: §8.8, the Onyx setup paragraph.
  - Attack: an Onyx upgrade or an admin change turns on a cloud embedding provider or LLM, or turns on telemetry, and nothing in Sindri notices.
  - Mitigation: when the key's role can read the provider settings, `context:onyx` warns on any non-local provider. Otherwise it prints a manual-check line on every version change. Alternatively, record it in §14 as accepted.

### A06 — Vulnerable & Outdated Components
No findings.

### A07 — Identification & Authentication Failures
No findings.

### A08 — Software & Data Integrity Failures
No findings.

### A09 — Security Logging & Monitoring Failures
- **N4. The PR identifier check has no defined input that a stage owner may read** [LOW, new]
  - Where: "Work data stays with the owner", third bullet (the owner checks text "for the profile's Linear team keys, the Slack workspace domain and `linear.app/<workspace>`"), and brief item 3 / the Team table (owners never use "the owner's … profile").
  - Attack (honest mistake): the owner can't read the real profile, so it either skips the check, greps only for generic shapes, or reads the real profile and so breaks its own isolation rule. Generic shapes mostly work, since `[A-Z]+-[0-9]+` catches issue keys. Project names and the workspace slug would still get through.
  - Mitigation (one line): the human writes a small denylist file outside every repo, for example `$AW_STATE_DIR/sindri-app/pr-denylist.txt`, holding the team keys, the workspace slug, the Slack domain and project names. The lead passes its path in the brief, and the check uses that file plus the generic `[A-Z]{2,}-[0-9]+` shape. If the file is missing, the check fails rather than being skipped.

### A10 — SSRF
No findings.

## STRIDE findings

### S — Spoofing
No findings. (`LoopbackGuard` is unchanged. A pseudo-terminal `expect` approval happens only in throwaway homes, and S-L5 is accepted.)

### T — Tampering
- **L-a** (above). N2 is resolved.

### R — Repudiation
No findings.

### I — Information Disclosure
- **L-b** and **N4** (above). N1 and N3 are resolved.

### D — Denial of Service
No findings.

### E — Elevation of Privilege
No findings.

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 0
- **Low findings:** 3 (L-a and L-b carried from round 2; N4 new). Each has a one-line fix or can be recorded as accepted in §14. None blocks.
- **Recommendation:** SHIP

verdict: PASS
