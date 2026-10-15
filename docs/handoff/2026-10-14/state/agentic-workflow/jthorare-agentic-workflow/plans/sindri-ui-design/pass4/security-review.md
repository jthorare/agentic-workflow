# Threat Model — sindri-ui-design (revision 4, pass 4)

**Mode:** plan (slim) · honest-mistake threat model, loopback-only v0
**Source:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md
**Reviewed:** 2026-10-11

## Summary
The pass-3 fixes hold in the text. N1: one `launch.outcome:<launchId>` key, first writer wins, with a race test (E-1). N2: an instance started during an open launch starts taken over (§6.4). N3: a park row appears at once after `launch.failed` (§6.4). N4: `answer-text` in §4.4. N5: a per-launch `mcp-<launchId>.json` (§6.3 step 5). Earlier HIGHs F1 (`AW_SINDRI_CHILD=1`) and P2-1 (deadline, freshness re-check) are unchanged. One new MEDIUM: the "claim" that keeps the expiry timer away from an in-flight `ILauncher.Launch` is never defined, so the first-writer-wins key can still record `failed` for a launch that opened Warp. Three LOWs. No CRITICAL or HIGH.

## Coverage
| Category | Status |
|---|---|
| A01 Broken Access Control | No new findings (F1, U2 caller headers hold) |
| A02 Cryptographic Failures | N/A — loopback, no secrets handled by the app; no token in the per-launch MCP file |
| A03 Injection | findings: 1 LOW (P4-3); F3, F4, E-16 hold |
| A04 Insecure Design | findings: 1 MEDIUM (P4-1), 1 LOW (P4-2) |
| A05 Security Misconfiguration | No new findings |
| A06 Vulnerable & Outdated Components | No findings |
| A07 Identification & Authentication Failures | No new findings |
| A08 Software & Data Integrity Failures | findings: 1 LOW (P4-4) |
| A09 Security Logging & Monitoring Failures | No new findings (§9.1 logging; launch metrics from the ledger, §2) |
| A10 SSRF | No new findings |

## Findings

| # | Sev | Where | Finding | Fix |
|---|---|---|---|---|
| P4-1 | MEDIUM | §6.7 E-1 | **"Claim" is undefined, and first-writer-wins goes the wrong way during `Launch`.** E-1 says the launcher "claims a request before calling `ILauncher.Launch`" and writes `launch.started` "only if it wins" the `launch.outcome` key. But the claim is not one of the "three events" and has no stated storage. If the launcher calls `Launch` at 29.9 s and the 5 s timer appends `launch.failed SND-LAUNCH-007` at 30 s, the timer wins the key. The launcher's `launch.started` is dropped while Warp is open. `LaunchView` says `failed`, the human clicks Open again, and two sessions run on one item: N1's original problem from the other direction. The race test only asserts "exactly one outcome", not which one. | Define the claim. Either an in-process claimed-set that the timer skips (the start-up sweep only sees requests no live launcher holds), or a durable `launch.claimed` fact under its own key that the timer and the start-up sweep both respect. Then make the race test assert the single outcome is `launch.started` when `Launch` began before the deadline. |
| P4-2 | LOW | §6.4 second bullet, E-6 | An instance that **starts** taken over because of an open launch (N2's fix) was not taken over by the request (no `open-request` causation). If that launch then fails or expires, the "takeover its request caused" fold may not match, and the instance waits 2 h for `takeoverIdle`. Separately, launches started before the engine (U6a) never get a handback, so "started and not handed back" makes every item opened before the engine start taken over. | Make the needs-you fold match "a takeover caused by, or started under, launch L". Bound the N2 rule to launches that are `requested`, or `started` after the engine's first deploy (or within `takeoverIdle`). |
| P4-3 | LOW | §6.3 step 1, §6.2 Resume | The pattern check names `ITM-…`, `LCH-…` and a UUID. Codex and Cursor session ids used by `codex resume <id>` and `agent --resume=<id>` have no named pattern. They are shell-quoted, so this is not injection, but a garbled id would build a broken exec line. | Give each target's `sessionId` a pattern in `launch.started`'s schema, and reject a non-matching id before writing (fall back to Open). |
| P4-4 | LOW | §6.3 step 4 | The file is deleted 60 s after launch. A Warp that is slow to read it (cold start right at the deadline) could find no file. | U6a's spike records Warp's read latency on a cold start. Delete on `launch.started` plus 60 s, which the text already implies; state it. |

## STRIDE
S: no new findings · T: P4-1 (outcome contradicts reality) · R: no new findings (actors `service:launcher`, `service:engine`, `human:ui`) · I: no new findings · D: P4-2 (an instance stuck taken over with no prompt) · E: no new findings.

## Verdict
- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 1 (P4-1)
- **Recommendation:** SHIP. P4-1 is a one-sentence definition plus a stronger test assertion in E-1, and should land before P10a.

verdict: PASS
