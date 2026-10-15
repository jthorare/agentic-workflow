# Consolidated Plan Review — sindri-api-surface

**Plan:** docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md (rev 1, commit 831409c)
**Reviewed:** 2026-10-10
**Lenses applied:** product · arch · design · devex · security

## Normalized findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| C-01 | HIGH | arch, devex | §9 | `SND-API-003` already used by S1 (port taken) | Shift new API codes to 004+ |
| C-02 | HIGH | security | §6 | Missing `X-Sindri-Caller` = human, so an agent's curl/script/WebFetch gets human rights | Missing header = `session`; CLI (TTY, no agent env) and the app send `human` |
| C-03 | HIGH | security | §10 C3 | Local browser page can drive body-less commands (loopback Origin allowed, no header = human) | Refuse any `Origin` on commands in P1; C-02 also blocks it (custom header needs preflight) |
| C-04 | HIGH | arch, devex, security | §2.1 | Audit decorator is innermost: refusals never audited; P1 signal can't pass | Audit outermost |
| C-05 | HIGH | arch, security | §2.1, §3.4 | First-decision-wins / itemVersion race: checks on lagging views, per-client keys, concurrent appends | Fact keys (`decision.recorded:<id>`, `item…:v<n+1>`), commands serialized and wait for projector head |
| C-06 | HIGH | arch, security | §2.1 | Idempotency-Key replay underspecified (no lookup by key; created ids not stable; not scoped per command) | Drop client Idempotency-Key from v0; natural fact keys only |
| C-07 | HIGH | arch, devex, product | §3.2 | No `POST /profile/validate` in S1; validate is CLI-local | Mark CLI-local verbs; not commands |
| C-08 | HIGH | product | P7, P9 | Park/takeover/handback/start duplicated with the engine spec | Engine owns instance commands; this spec owns classes, registry rows, ledger read |
| C-09 | HIGH | design | §3.1 | needs-you is a queue field; dashboard needs any-source list | `GET /needs-you` with source-neutral rows |
| C-10 | HIGH | design | §3, Phases | 4 of 5 dashboard views unscoped; "unblocks UI" overclaims | Scope table; collector-owned queries named as Later |
| C-11 | HIGH | design, arch | §2.2 | No freshness/rebuilding signal: empty vs loading | `asOf {cursor, ledgerHead, rebuilding, stalled}` on every view |
| C-12 | HIGH | devex | §9 | Error docs path wrong (TS-generated `docs/sindri/errors.md`) | `docs/csharp/sindri.md` |
| C-13 | HIGH | devex | Parallel delivery | `/new-slice` anchors missing in S1 tables; `--surface cli` refused | `--files-only` + one hand-added line; P1 adds anchors |
| C-14 | HIGH | devex | Phases | Agents can't run human proof steps (CLAUDECODE ⇒ session; TTY approve) | Prover recipe |
| C-15 | HIGH | devex | Parallel delivery | Parallel agents share home/port; `sindri` on PATH is TS | Per-agent `AW_SINDRI_HOME`, `AW_SINDRI_PORT=0`, `$SINDRI` = the built binary |
| C-16 | HIGH | devex | P8 | Mid-session `claude mcp add` not seen by a spawned subagent | Fresh `claude -p --mcp-config … --strict-mcp-config` |
| C-17 | MEDIUM | arch | §3.4 | Two events per command in separate transactions | One event per command; the other side is derived in projections |
| C-18 | MEDIUM | arch | §10 | S1 EventFactory: fixed actor, new correlation per event, null item | Conflict C6, fixed in P1 |
| C-19 | MEDIUM | arch | §2.3 | Projector failure unhandled | Stall flag, status, doctor |
| C-20 | MEDIUM | arch | Phases | Missing deps: P6→P2, P8→P6, event registry before P5 | EventCatalog in P1; deps fixed |
| C-21 | MEDIUM | product, arch | §7 | Agent CLI auto-starts but mcp doesn't | Agent-session CLI never starts |
| C-22 | MEDIUM | product | §3.4 | Accepted requests do nothing pre-engine | Label recorded-only |
| C-23 | MEDIUM | product | Phases | Build ahead of clients; ILauncher with engine | v0 milestone = P1–P5; P10 marked engine-shared |
| C-24 | MEDIUM | design | §3 | `age` vs `since`; per-row actions; `cli-only` status; states→groups; empty text; fix line | Adopt |
| C-25 | MEDIUM | security | §8 | Cross-repo data to sessions | MCP scoped to session repo by default |
| C-26 | MEDIUM | security | P10 | `sindri open` injection via item text | Launcher gets only the item id |
| C-27 | MEDIUM | devex | various | Stale S1 facts (/ping, stop exit, Confirmed field, GUIDv7 ids, --json re-serialize), unnamed env vars, docs per phase, metrics | Fix inline |
| C-28 | LOW | security, design, arch | various | Request text caps, flood, stream slots, control.json URL check, guard logging, escaping, 429 fallback, SSE param order, bounded channel, progress growth | Answered in §13 |

## Top tensions (cross-lens)
1. **Simplicity vs safety of caller defaults** — product (owner: no auth in v0) vs security C-02. Resolution: flip the default to `session`; still no tokens, so both hold.
2. **Idempotency rigor vs v0 scope** — arch C-06 vs product/devex (defer SND-API-005). Resolution: drop client keys from v0, keep natural fact keys (also fixes C-05).
3. **Dashboard completeness vs build-ahead** — design C-10 vs product C-23. Resolution: name collector queries as Later, keep v0 to P1–P5 and say exactly which views it feeds.
4. **Agent-team proof vs session classification** — devex C-14 vs security C-02. Resolution: provers act as human via explicit `X-Sindri-Caller: human` from a pty, documented as the prover's stand-in.
No tension between arch and devex beyond shared fixes (C-01, C-04): both point the same way.

## Per-lens highlights
### Product
- Wins: settled owner decisions respected; W1 can start early.
- Gaps: C-08, C-21, C-22, C-23. File: product-review.md
### Architecture
- Wins: CQRS shape, authorization table, phase plan.
- Gaps: C-04, C-05, C-06, C-07, C-17–C-20. File: arch-review.md
### Design
- Wins: registry, decision options, conflicts, byte-match JSON, reset.
- Gaps: C-09, C-10, C-11, C-24. File: design-review.md
### Devex
- Wins: thorough spec.
- Gaps: C-12–C-16, C-27. File: devex-review.md
### Security
- Wins: honest-mistake model applied; single fix covers both HIGHs.
- Gaps: C-02, C-03, C-25, C-26. File: security-review.md

## Recommended next actions
1. Flip the caller default and refuse Origin on commands (C-02, C-03) — security.
2. Fact keys + serialized commands + audit outermost; drop client idempotency keys (C-04–C-06) — arch.
3. Reconcile with the engine spec and S1's current code (C-07, C-08, C-01, C-12) — product, devex, arch.

## Lens status
| Lens | File | Status |
|---|---|---|
| Product | product-review.md | ✓ NEEDS_WORK |
| Architecture | arch-review.md | ✓ NEEDS_WORK |
| Design | design-review.md | ✓ BLOCKED |
| Devex | devex-review.md | ✓ NEEDS_WORK |
| Security | security-review.md | ✓ NEEDS_WORK |

## Overall verdict
No CRITICAL findings; HIGH findings C-01–C-16 drive NEEDS_WORK (design's BLOCKED is its HIGH-structural C-09/C-10, normalized to HIGH).
verdict: NEEDS_WORK

## Pass history (owner cap: 3 passes)
| Pass | Rev reviewed | product | arch | design | devex | security | Outcome |
|---|---|---|---|---|---|---|---|
| 1 | rev 1 (831409c) | NEEDS_WORK | NEEDS_WORK | BLOCKED | NEEDS_WORK | NEEDS_WORK | 16 HIGH fixed in rev 2 (pass1/) |
| 2 | rev 2 (78d3c41) | PASS | NEEDS_WORK | NEEDS_WORK | NEEDS_WORK | PASS | HIGH + MEDIUM fixed in rev 3 (pass2/) |
| 3 | rev 3 (eca342e) | PASS | NEEDS_WORK | PASS | PASS | PASS | arch R1/R2 HIGH + all MEDIUM fixed in the final edit, no 4th pass (cap) |

Final: every HIGH from all three passes is resolved in the text; LOWs answered in spec §13.
verdict: PASS
| 4 | final edit (0617aae) | PASS | PASS | PASS | PASS | PASS | MEDIUMs fixed (pass4/) |
| 5 | pass-4 edit (11500b8) | PASS | PASS | PASS | PASS | PASS | MEDIUM/LOW text fixes, no further pass (pass5/) |

Owner raised the cap to 5. Final: all lenses PASS in passes 4 and 5; no open HIGH or MEDIUM.
verdict: PASS
