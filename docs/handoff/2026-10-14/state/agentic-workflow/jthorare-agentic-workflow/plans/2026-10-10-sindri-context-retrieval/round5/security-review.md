# Threat Model — 2026-10-10-sindri-context-retrieval (round 5, final)

**Mode:** plan (compact)
**Source:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (worktree `ctx-wt`, HEAD `e1a5f05`). I read it against `../round4/security-review.md` and the §14 "Round 4" answers, and diffed it from `6a56d84` to HEAD.
**Reviewed:** 2026-10-10

**Threat model (unchanged).** This is a single-user local tool on loopback. In scope: honest mistakes, data leaks of work data (into repos, public PRs, personal sessions or Prism, or model providers without scrubbing), and prompt injection. A hostile local user or an adversarial agent is out of scope and rated LOW with a note. Settled decisions are not reopened (D2, D11, D17, no bearer token, S-L5, L-b).

## Round-4 fixes verified

| Round-4 id | Status | Notes |
|---|---|---|
| R4-1 denylist read by agents | **Resolved** | `scripts/denylist-check.sh` prints only `file:line` and a count, fails when the denylist is missing or empty, and covers the PR title, body, commit messages, `evidence.md` and `owner-check.md`. Owners never open the file. It lands in S-1C. |
| R4-2 owner-check home | **Resolved** | The home is `${AW_STATE_DIR:-…}/sindri-app-ownercheck-<stage>`, created fresh, on port 7480, with its own `sindri-ownercheck` MCP name and a teardown that deletes the home and the build. Stage owners never read or write it. One leftover, the export file, is covered by R5-2. |
| R4-3 loopback `linear.url` | **Resolved** | Loopback is accepted only with `env:`/`file:` tokens. Otherwise `https` on Linear's API host. The fake servers never log `Authorization`. |
| R4-4 pending Linear keys | **Resolved** | The identifier comes only from the structured metadata of Linear-sourced Onyx documents and must match the pattern plus a profile team key. Decisions never come from a pending record, so a merge can't change a decision key. |
| R4-5 code-derived attribute strings | **Open (not fixed or accepted in §14)** | Carried over as R5-3. |

## Summary

Every round-4 fix holds except R4-5, which §14 neither fixes nor accepts. The new `context map export` and its round trip (R4-D) open two paths. **R5-1 (MEDIUM):** an import restores `human` provenance and `trust` from the file, and `POST /sindri/context/map/import` has no terminal confirmation. Rows that no human reviewed can therefore become `human`/`trusted`, which bypasses §3 "Decisions stay human" and D15. **R5-2 (LOW):** the export and `review.yaml` hold work identifiers and the owner's decisions. They go to any path outside a git worktree, no retention applies to them, and the owner-check teardown doesn't delete them. Result: 0 CRITICAL, 0 HIGH, 1 MEDIUM, 2 LOW.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 1 (R5-1, MEDIUM) |
| A02 Cryptographic Failures | No findings. |
| A03 Injection | findings: 1 (R5-3, LOW, carried over from R4-5) |
| A04 Insecure Design | findings: 1 (R5-2, LOW) |
| A05 Security Misconfiguration | No findings. (R4-2 resolved) |
| A06 Vulnerable & Outdated Components | No findings. |
| A07 Identification & Authentication Failures | No findings. (R4-3 resolved) |
| A08 Software & Data Integrity Failures | See R5-1 (provenance restored from the file). R4-4 resolved. |
| A09 Security Logging & Monitoring Failures | No findings. |
| A10 SSRF | No findings. |

## OWASP Top 10 findings

### A01 — Broken Access Control
- **R5-1. Map import restores `human` provenance and trust from the file, and the HTTP import route has no terminal confirmation** [MEDIUM]
  - Where: Phase 3 `ImportMap`/`ExportMap` ("importing an export restores both, so `human` stays `human`"), §14 R4-D, §8.5 `POST /sindri/context/map/import`, §3 "Decisions stay human", D15, §6.2 (`node.trust`, `decision.trust`).
  - Attack (honest mistake): import trusts the `provenance`, `trust` and timestamps written in the file. §3 records `human` provenance only from the CLI at an interactive terminal, and `--apply` keeps that rule. Import doesn't. Two cases:
    - An agent in a work-repo session is asked to "seed" or "fix" the map. It writes a YAML file, or edits an export, and calls `POST /map/import` (or the CLI verb without a TTY). The result is `human`, `confirmed` and `trusted` rows that no human reviewed.
    - Someone hand-edits an export and changes a model label (`untrusted`, possibly drawn from ticket text) to `trusted`. The label is then returned unfenced, which reopens the D15 injection path.
    
    Backdated timestamps in the file also skew the backtest's as-of filter.
  - Mitigation (a few lines in Phase 3 and §3):
    - **Recompute trust on import.** Import never reads `trust` from the file; it recomputes trust from provenance under the §7 table.
    - **Terminal rule for `human` rows.** A file that carries `human` provenance or override and rejection rows is accepted only through `map import` at an interactive terminal, under the same rule as `profile approve` and `--apply`: print the counts and confirm.
    - **HTTP route.** `POST /map/import` either drops these rows or imports them as `model`/unconfirmed. Alternatively, drop the HTTP route and keep import CLI-only.
    - **Owner-check seeding.** The owner-check seed step is human and at a terminal, so it keeps working.

### A02 — Cryptographic Failures
No findings.

### A03 — Injection
- **R5-3. Code-derived attribute strings are still outside the identifier-character rule** [LOW, carried over from R4-5]
  - Where: the §7 trust table, row 2; §6.2 `node.attributes (JSON)`.
  - Attack (honest mistake): the extractor copies a settings description, help text or command title into an attribute. That attribute is then returned `trusted` and unfenced. §14 round 4 neither fixes this nor records it as accepted.
  - Mitigation (one line in §7): the rule covers every string value taken from code text, labels and attributes alike. A fenced value reports `trust: "untrusted"`. Alternatively, record it as accepted in §14.

### A04 — Insecure Design
- **R5-2. Map exports and `review.yaml` have no fixed location, no retention, and no teardown in the owner check** [LOW]
  - Where: §3 State ("exports" under `$AW_SINDRI_HOME/context/`) against the §8.5 CLI `map export <file>` (any path); §8.7 `review --export review.yaml` ("outside repos"); Phase 3 signal `~/…/product-map.yaml`; the owner-check outline steps 4 and 7; the stage-owner brief item 3.
  - What the file holds: an export has nodes and labels from the work code, aliases (Linear identifiers and UUIDs), override payloads in the owner's wording, rejections and the OS user. `review.yaml` also has proposals with evidence refs to Linear documents.
  - Attack (honest mistake): the only guard refuses paths inside a git worktree. Several paths still leak:
    - **Location.** Exports land anywhere outside a worktree: `$HOME`, a synced `~/Documents` or iCloud folder, `/tmp`, or an agent's scratch directory.
    - **Teardown.** Owner-check step 4 doesn't say where to write the export, and step 7 deletes only the home and the build. The real-map export from each owner check therefore stays behind.
    - **Retention.** The export is outside both the retention service and `doctor context:retention`.
    - **Never-list.** It isn't on the stage owners' never-list, so a later owner looking for "the product map" in `~/` may open it in the personal session.
    - **File mode.** Writing over an existing file keeps that file's mode, so `umask 077` doesn't make it 0600.
  - Mitigation (one paragraph):
    - **One location.** `map export` and `review --export` write only under `$AW_SINDRI_HOME/context/exports/` (0700) or the owner-check home, and refuse anything else (`SND-CONTEXT-005`).
    - **Safe write.** Create the file with a temp name and rename it, with mode 0600 forced.
    - **Owner-check outline.** Step 4 writes the export into the scratch home, so step 7's delete removes it.
    - **Retention.** Purge exports after 14 days and report them in `context:retention`.
    - **Never-list.** Add the exports directory to the never-list.

### A05 — Security Misconfiguration
No findings.

### A06 — Vulnerable & Outdated Components
No findings.

### A07 — Identification & Authentication Failures
No findings.

### A08 — Software & Data Integrity Failures
- R5-1 (above): provenance and timestamps are restored from an unverified file.

### A09 — Security Logging & Monitoring Failures
No findings.

### A10 — SSRF
No findings.

## STRIDE findings

### S — Spoofing
- R5-1: an imported row claims `human` provenance.

### T — Tampering
- R5-1: an edited export upgrades trust or backdates rows.

### R — Repudiation
- R5-1: an override imported over HTTP is indistinguishable from a decision made at the terminal. Recording the import id and the client (`cli` or `http`) on each imported row fixes this.

### I — Information Disclosure
- R5-2 (above). `GET /map/export` sits behind `LoopbackGuard` and isn't exposed over MCP, so no other finding.

### D — Denial of Service
No findings. (An all-or-nothing import into a new map version is assumed. If an import replaces `current` in place, the HTTP route can also overwrite review decisions, which is one more reason for the R5-1 terminal rule.)

### E — Elevation of Privilege
- R5-1: an unreviewed, model-written label is raised to `trusted` without a human accepting it. (A deliberate bypass by an agent is out of scope. LOW with a note.)

## Verdict

- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 1 (R5-1). Before S-3 merges, add the trust recomputation and the terminal rule for `human` rows to Phase 3.
- **Low findings:** 2 (R5-2, R5-3). Each has a one-line fix or can be recorded as accepted in §14.
- **Recommendation:** SHIP

verdict: PASS
