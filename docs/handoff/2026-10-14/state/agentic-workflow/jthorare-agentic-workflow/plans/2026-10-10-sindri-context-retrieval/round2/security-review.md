# Threat Model — 2026-10-10-sindri-context-retrieval (round 2)

**Mode:** plan
**Source:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (revised spec, worktree `ctx-wt`), round-1 review `../security-review.md`, §14 answers
**Reviewed:** 2026-10-10

**Threat model (unchanged from round 1).** This is a single-user local tool on loopback. In scope: ring-1 work text (Linear, Slack, PRDs, TDDs) reaching repos, PRs, personal sessions or model providers, and prompt injection from untrusted records. A hostile local user or an adversarial agent is out of scope, and findings that need one are rated LOW with a note. Settled decisions are not reopened: Onyx CE (D2), MCP in Phase 1 (D11), no bearer token (S1's threat model, §14 S-H2), and Linear before Onyx (D17).

## Summary

All four round-1 HIGH findings are resolved in the text (see the table below). The round-1 MEDIUM and LOW fixes listed in §14 are present in the sections they name. I spot-checked §5.1 argument validation, §6.2 HMAC hashes and the purge, §7 per-field egress and the permalink allowlist, §8.3 caps, and §9.1 corpus retention.

The new "Parallel delivery" section adds one real leak path, **N1 [HIGH]**. Stage owners are agents working in worktrees of the toolkit repo, which is a personal repo: it is public and `prism-mcp` is registered user-wide there. Each one must run its phase signal "in the running app", and the Phase 5 and 6 signals (`--item ENG-482`, Slack threads through Onyx) hit real company sources. The `$SINDRI` CLI doesn't pass through the per-repo MCP scope that fixed H1. Nothing in the section limits stage-owner hand tests to the fixture app, or keeps work text out of PR bodies and evidence summaries.

There are two new MEDIUM findings. N2: parallel dev builds share the owner's real store and port. N3: the `context.mcp.enabled` default contradicts §3.

0 CRITICAL, 1 HIGH.

### Round-1 HIGH findings: verification

| Id | Round-1 finding | Text now | Status |
|---|---|---|---|
| H1 | MCP registered user-wide | §3: `--scope local` inside the work repo, never user-wide. Doctor `context:mcp-scope` warns on a user-scope `sindri` in `~/.claude.json`. The Phase 1 signal uses `--scope local` | **Resolved.** The residual CLI path is covered by N1 |
| H2 | No Host or Origin checks | §3: S1's `LoopbackGuard` on every `/sindri/context/*` route and on `/mcp` (foreign `Host` gives 400, foreign `Origin` gives 403). §10 integration tests cover `/mcp` and every POST | **Resolved** |
| H3 | Model text laundered as trusted | D15 and the §7 trust table: model output from untrusted input stays `untrusted` until a human accepts it. Review is human-only at a TTY (§3, §8.7, `SND-CONTEXT-041`), and MCP has no review tool | **Resolved.** See residual L-a |
| H4 | Onyx's own exposure | §8.8 Onyx setup: pinned tag, ports bound to `127.0.0.1`, local embeddings only, no cloud LLM, telemetry off, scoped connectors, narrow key. Doctor `context:onyx` and `SND-CONTEXT-070` | **Resolved.** The doctor checks bind, version and key, but not providers or telemetry. See residual L-b |

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 1 (N1) |
| A02 Cryptographic Failures | No findings. (Round-1 M8 and L1 resolved: umask 077 plus a test on the db, WAL and SHM files; FDE assumed; HMAC query hashes) |
| A03 Injection | No findings. (M3 and M4 resolved: §7 escaping and permalink allowlist, §5.1 argument validation) |
| A04 Insecure Design | findings: 1 (L-a) |
| A05 Security Misconfiguration | findings: 2 (N3, L-b) |
| A06 Vulnerable & Outdated Components | No findings. (M11 resolved: Onyx tag pinned and checked by doctor; MCP package reviewed in `Directory.Packages.props`) |
| A07 Identification & Authentication Failures | No findings. (M6 resolved: read-only keys and scope checks, `SND-CONTEXT-050`) |
| A08 Software & Data Integrity Failures | findings: 2 (N2, L-c) |
| A09 Security Logging & Monitoring Failures | No findings. (M7 resolved: no text in logs, with a planted-text test; `client` column in the query log) |
| A10 SSRF | No findings. (§14 S-L2: the only outbound targets are Linear's host and the loopback Onyx URL from the profile; no URL from record text is ever fetched) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **N1. Parallel stage owners run work-data phase signals from a personal-repo session, past the per-repo MCP scope** [HIGH]
  - Where: "Parallel delivery" → The team (a stage owner runs `/specToProvenPR` steps 1–9, including "hand test in the running app" and "evidence"). Stage owners are started with `isolation: worktree` in the toolkit repo. Phase 5 signal: `--item ENG-482` returns the PRD and TDD. Phase 6 signal: Slack threads with `onyx:` citations. Phase 4 signal: `map build` on the work repo. "Prep while blocked" includes "the eligible-sample count query for S-7B". "Gates that stay sequential" assigns only the S-7B work-data runs, plus Onyx setup before S-6's hand test, to the owner.
  - Attack (honest mistake): the S-5B or S-6 stage owner follows `/specToProvenPR` and proves its signal against the owner's real Linear workspace or Onyx, because those are the configured sources in `$AW_SINDRI_HOME` and Onyx was set up "before S-6's hand test". The `$SINDRI context query` output then lands in four places:
    - the agent's transcript in a toolkit-repo session, where `prism-mcp` (user-wide) is told to save ledgers and handoffs;
    - `scorer`'s transcript readers, and `scorer audit --label` samples;
    - the draft PR body, evidence summary and review replies on the **public** toolkit repo, as issue keys, PRD excerpts, Slack quotes and sample counts with project names;
    - the `/review` fan-out agents that read that evidence.

    The §3 per-repo MCP scope doesn't help, because the CLI is reachable from any shell. "Evidence written outside repos" covers the evidence files, not what is quoted from them into the PR.
  - Mitigation:
    1. Add a rule to the section: **stage-owner hand tests and phase signals run against the fixture app and its recorded sources only** (`S-FX`). Every signal that needs real sources becomes an owner-run step listed under "Gates that stay sequential". That covers Phase 1 in the work repo, Phase 4 `map build` on the work repo, Phase 5 `--item` against Linear, Phase 6 against real Onyx, and S-7B. The owner runs them in a work-repo session, or with `$SINDRI` and output under `$AW_SINDRI_HOME`.
    2. Only **aggregate, identifier-free** results cross into a toolkit PR or `stages.md`: pass or fail, counts, recall numbers, and exit codes. No issue keys, titles, quotes, permalinks or project names.
    3. Give stage owners a profile and `$AW_SINDRI_HOME` with no `linear` or `onyx` source configured, so a mistaken real query fails with `SND-CONTEXT-002` instead of returning work text. This also covers N2.
    4. Optional and cheap: before the lead marks a PR ready, a check greps the PR body and evidence summary for the profile's team keys (`ENG-[0-9]+`, `APP-…`), `linear.app/<workspace>` and the Slack workspace domain.

### A02 — Cryptographic Failures

No findings.

### A03 — Injection

No findings.

### A04 — Insecure Design

- **L-a. "Deterministic" labels taken from code strings are `trusted`** [LOW]
  - Where: §7 trust table ("graph nodes and edges from deterministic extraction (registries, routes, paths): `trusted`"), §8.7 bulk accept of deterministic proposals.
  - Attack: a command title or setting description in a registry is copied verbatim into a node label. That label is code text, which the same table calls `untrusted`, but it comes back unfenced and in Sindri's voice. In the owner's own work repo this is a low-likelihood path.
  - Mitigation: one line in §7. Node *keys* and *structure* from deterministic extraction are trusted. A *label* copied from source text is fenced like code text until a human accepts it, or is limited to an identifier character class.

### A05 — Security Misconfiguration

- **N3. `context.mcp.enabled` defaults to `true`, against §3** [MEDIUM]
  - Where: §3 ("Model egress sits behind profile switches, **all off until approved**: … `context.mcp.enabled`"); §8.8 example `mcp: { enabled: true }` with "Defaults are what's shown"; §14 marks round-1 S-M9 fixed by this switch.
  - Attack: the owner approves a `context:` section that doesn't mention `mcp`. By §8.8 the default is on, so the sessions in the registered work repo start sending intent text to the model provider with no explicit approval. This partly reopens round-1 M9.
  - Mitigation: default `mcp.enabled: false` in §8.8, so it matches §3. `profile validate` notes "MCP off; set `context.mcp.enabled: true` and approve". The Phase 1 signal sets it explicitly.
- **L-b. Doctor `context:onyx` doesn't verify the no-cloud-provider and telemetry settings** [LOW]
  - Where: §8.8. The doctor checks the loopback bind, the version and that the key can search. The setup doc *says* there is no cloud LLM or embedding provider and that telemetry is off, but nothing checks it.
  - Attack: a later Onyx admin change, or a pinned-tag upgrade that brings new default settings, enables a cloud embedding provider or telemetry. Company text then leaves the machine through Onyx, and no Sindri check sees it.
  - Mitigation: where the Onyx key's role can read them, `context:onyx` also reads the configured LLM and embedding providers and warns on any non-local one. Otherwise it prints those two items as a manual checklist line on each version change.

### A06 — Vulnerable & Outdated Components

No findings.

### A07 — Identification & Authentication Failures

No findings.

### A08 — Software & Data Integrity Failures

- **N2. Parallel dev builds share the owner's real `$AW_SINDRI_HOME` and API port** [MEDIUM]
  - Where: "Parallel delivery": up to 3 stages build at once, and each one hand-tests "in the running app". The reserved migration numbers (S-1A `001`, S-3 `002`, S-5A `003`, …) go into one `context.db`. §3: the home defaults to `$AW_STATE_DIR/sindri-app`. The work repo's local MCP registration points at a fixed `127.0.0.1:<port>/mcp`.
  - Attack (honest mistake):
    - S-5A, which has `003` but not `002`, migrates the owner's real store. The next S-3 or merged build then sees a schema it doesn't expect (`SND-CONTEXT-004`), and the suggested fix, "rebuild with `context map build`", risks the owner's `map_override` rows (human decisions).
    - Separately, a stage build left running on the default port is what the owner's work-repo session talks to. That build is unreviewed, so it can carry a scrubber or egress bug straight to the model provider.
  - Mitigation: each stage owner runs its app with its own `AW_SINDRI_HOME` (a temp directory) and its own port, both recorded in `stages.md`. Only merged `main` builds open the owner's real store or bind the registered port. A startup check refuses an unknown higher migration number instead of rebuilding.
- **L-c. Fixture "recordings" must be synthetic** [LOW]
  - Where: §10 fixture app ("a recorded Linear project …, recorded Onyx responses"), S-FX in wave 0, committed under `tests/` in a public repo.
  - Attack: an agent builds the "recording" by capturing real GraphQL or Onyx responses from the owner's workspace. Those carry the org `urlKey`, user names and ids, which then get committed.
  - Mitigation: state that the recordings are hand-authored for the synthetic `split-panes` app, never captured from a live workspace. Reuse the N1(4) grep on `tests/Aw.Context.Testing/Fixtures/`.

### A09 — Security Logging & Monitoring Failures

No findings.

### A10 — SSRF

No findings.

## STRIDE findings

### S — Spoofing
No findings. H2 is resolved. A local process calling the API as the owner, or an agent getting past the TTY gate with a pseudo-terminal, needs a hostile or adversarial actor: LOW, out of scope (§14 S-L5).

### T — Tampering
- **N2** (above): a branch migration or rebuild in the shared real store can lose override rows.
- **L-a** (above): code-derived labels are trusted without review.

### R — Repudiation
No findings. `map_override.who` is the OS user at the terminal, and the query log records `client`.

### I — Information Disclosure
- **N1** (above): work text reaches personal-repo agent sessions (Prism, scorer) and public toolkit PRs through stage-owner hand tests.
- **N3** (above): MCP egress is on by default.
- **L-b** (above): Onyx provider and telemetry drift goes unchecked.
- **L-c** (above): captured fixtures could carry workspace identifiers.

### D — Denial of Service
No findings. The §8.3 caps, one map build at a time, and the two-slot heavy lock with a concurrency cap of 3 cover it. (N2 also has an availability side.)

### E — Elevation of Privilege
No findings. H3 is resolved: untrusted-derived text stays fenced until a human accepts it.

## Verdict

- **Critical findings:** 0
- **High findings:** 1 (N1: stage-owner hand tests and phase signals against real sources from personal-repo worktrees; a text-only fix in "Parallel delivery")
- **Medium findings:** 2 (N2 shared real store and port across parallel builds; N3 `context.mcp.enabled` default)
- **Low findings:** 3 (L-a, L-b, L-c)
- **Recommendation:** FIX-CRITICAL-FIRST

verdict: NEEDS_WORK
