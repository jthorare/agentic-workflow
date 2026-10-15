# Threat Model — 2026-10-10-sindri-context-retrieval (round 4)

**Mode:** plan (compact)
**Source:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (worktree `ctx-wt`, HEAD `6a56d84`). I read it against `../round3/security-review.md` and the §14 "Round 3" answers, and diffed it from `d26dbf5` to HEAD.
**Reviewed:** 2026-10-10

**Threat model (unchanged).** This is a single-user local tool on loopback. In scope: honest mistakes, and data leaks of ring-1 work data into repos, public PRs, personal sessions or Prism, or model providers without scrubbing. Prompt injection from untrusted records is also in scope. A hostile local user or an adversarial agent is out of scope. Settled decisions are not reopened: D2, D11, D17, no bearer token, S-L5, and the accepted L-b.

## Summary

The round-3 fixes hold. L-a is resolved by the identifier-character rule, N4 by the denylist file, and R3-1 by the owner-check home. L-b is accepted in §14. One new MEDIUM comes from the N4 fix itself. The denylist check is done by the stage owner, so an agent in the personal toolkit session reads team keys, the workspace slug and project names. That breaks the spec's own rule that "no agent on the team ever reads work data". Four new LOWs are tightenings of the round-3 text: how the owner-check home is defined and cleaned up, the real token plus a loopback `linear.url`, where `linear:pending/…` identifiers come from, and code-derived attribute strings. Result: 0 CRITICAL, 0 HIGH, 1 MEDIUM, 4 LOW.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | No findings. |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 1 (R4-5, LOW) |
| A04 Insecure Design | findings: 1 (R4-1, MEDIUM) |
| A05 Security Misconfiguration | findings: 1 (R4-2, LOW) |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | No findings. |
| A08 Software & Data Integrity Failures | findings: 1 (R4-4, LOW) |
| A09 Security Logging & Monitoring Failures | No findings. (N4 resolved; see R4-1 for the leak the fix creates) |
| A10 SSRF | findings: 1 (R4-3, LOW) |

## OWASP Top 10 findings

### A01 — Broken Access Control
No findings.

### A02 — Cryptographic Failures
No findings.

### A03 — Injection
- **R4-5. The identifier-character rule covers labels but not code-derived attribute strings** [LOW]
  - Where: the §7 trust table, row 2, and the §6.2 `node.attributes (JSON)`.
  - Attack (honest mistake): the extractor copies a setting description, help text or command title from code into a node attribute. The row's `trusted` tag then returns it unfenced, so the L-a path stays open through attributes instead of labels. The row also doesn't say what `trust` a fenced label reports.
  - Mitigation (one line in §7): the same rule applies to every string value taken from code text, labels and attributes alike. A fenced value is reported as `trust: "untrusted"` until a human accepts it.

### A04 — Insecure Design
- **R4-1. The denylist check makes an agent in the personal session read work identifiers** [MEDIUM]
  - Where: "Work data stays with the owner", third bullet ("the stage owner checks … against a denylist file … the lead passes its path to each owner").
  - Attack (honest mistake): the stage owner runs the check in the toolkit session, where Prism is registered and the transcripts are personal. To run it, the owner opens the file, or the grep echoes the matched terms. Team keys, the Slack workspace slug and project names then enter that session's context, its transcript, the model provider and any Prism ledger or handoff save. That contradicts "no agent on the team ever reads work data" and §3's "No Prism".
  - Mitigation: ship the check as a script, for example `scripts/pr-denylist-check.sh <denylist> <files…>`. It runs `grep -F -f` and prints only `file:line` and a count, never the matched term or the denylist's contents. It also fails on a missing or empty denylist. The brief tells owners to run only the script, never to read the file, and to remove hits by line number. Add the PR title and commit messages to the checked inputs, since both are public.

### A05 — Security Misconfiguration
- **R4-2. The owner-check home is loosely defined: relative path, no port, no cleanup, not in the never-touch list** [LOW]
  - Where: "Work data stays with the owner", first bullet (`$AW_SINDRI_HOME/../sindri-app-ownercheck/`), and brief item 3's never-list.
  - Attack (honest mistake): there are three paths. (a) `AW_SINDRI_HOME` is usually unset, because the default is derived, so the path resolves to `/sindri-app-ownercheck`. If the variable leaks in from a stage's shell, the path lands in `<scratch>/` next to an agent's throwaway home. (b) No `AW_SINDRI_PORT` is named, so the PR build can take the default port that the work repo's `--scope local` MCP registration points at. (c) Nothing wipes the home. It keeps the real map import, scrubbed snippets, decision wording, eval reports and the `query_log`. The retention service runs only while that build runs, so this data outlives its 14- and 90-day limits. Stage owners aren't told to stay out of it, so an owner debugging a failed owner check may open it.
  - Mitigation (one paragraph): give the path as `$AW_STATE_DIR/sindri-app-ownercheck/` (0700) with its own port. Each owner check does `rm -rf` of the home and of the `map` export it imported, both before and after the run. Add "the owner-check home and the denylist file" to brief item 3's never-list.

### A06 — Vulnerable & Outdated Components
No findings.

### A07 — Identification & Authentication Failures
No findings.

### A08 — Software & Data Integrity Failures
- **R4-4. `linear:pending/<identifier>` has no defined source or shape** [LOW]
  - Where: §5.1 `Canonical` comment and §14 R3-4.
  - Attack (honest mistake / injection-adjacent): the identifier might come from a document's text, or from a non-Linear Onyx document that mentions `ENG-482`. A Slack message would then dedup into the Linear issue's canonical key, so its untrusted text is cited under the issue's citation and permalink. Separately, an identifier that was moved or re-keyed could merge into the wrong UUID.
  - Mitigation (one line in §5.1): take the identifier only from the structured link or metadata of documents whose Onyx source type is Linear. Validate it against `^[A-Z][A-Z0-9]{0,9}-[0-9]+$` and the profile's teams before keying. A pending key merges only when the native sync resolves the identifier to a UUID, never from Onyx data.

### A09 — Security Logging & Monitoring Failures
No findings.

### A10 — SSRF
- **R4-3. A loopback `linear.url` accepts the real keychain token** [LOW]
  - Where: §8.8 `linear` comment and §14 S-L2 ("accepts only that host or a loopback URL").
  - Attack (honest mistake): the real profile ends up with a loopback `linear.url`, for example copied from the fixture profile or left over from a test. Sindri then sends `keychain:linear/work-read` to whatever is listening, such as the S-FX fake server. If that server logs request headers, the token lands in fixture output in the public toolkit repo.
  - Mitigation (one line): `profile validate` refuses a loopback `linear.url` when the token is a `keychain:` pointer. Loopback is allowed only with the `env:` or `file:` fixture tokens. A non-loopback URL must be `https` on Linear's exact API host. The fake servers never log `Authorization`.

## STRIDE findings

### S — Spoofing
No findings.

### T — Tampering
- R4-4 (above).

### R — Repudiation
No findings.

### I — Information Disclosure
- R4-1 and R4-2 (above).

### D — Denial of Service
No findings.

### E — Elevation of Privilege
No findings. (The PR build in the owner-check home resolves the real keychain token by design. That is in line with the non-adversarial agent model and needs no change.)

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 1 (R4-1). Fix it before the first stage owner runs the check. It is a short script and one brief line.
- **Low findings:** 4 (R4-2 to R4-5). Each has a one-line fix or can be recorded as accepted in §14.
- **Recommendation:** SHIP

verdict: PASS
