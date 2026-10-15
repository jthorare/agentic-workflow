# Threat Model — sindri-ui-design (revision 3, pass 3)

**Mode:** plan (slim) · honest-mistake threat model, loopback-only v0
**Source:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md
**Reviewed:** 2026-10-11

## Summary
Prior HIGHs hold: F1 (`AW_SINDRI_CHILD=1` on the exec line, §6.3 step 2) and F5 / P2-1 (5 s expiry timer, E-6 wait inside the 30 s deadline, freshness re-check before `ILauncher.Launch`, `-008` counts only fresh requests, E-1 and §6.6). Prior MEDIUMs F2–F4 hold (key guard and hover-freeze §8; YAML serializer and shell-quoted argv §6.3; E-16 plus the client backstop §7). Two new MEDIUMs, both races in the revision-3 launcher text: the expiry timer can contradict a launch that did happen, and "no active instance" is checked without fencing a concurrent instance start. No CRITICAL or HIGH.

## Coverage
| Category | Status |
|---|---|
| A01 Broken Access Control | No new findings (F1 holds) |
| A02 Cryptographic Failures | N/A — loopback, no secrets handled by the app |
| A03 Injection | No new findings (F3, F4 hold) |
| A04 Insecure Design | findings: 2 MEDIUM, 1 LOW |
| A05 Security Misconfiguration | No new findings |
| A06 Vulnerable & Outdated Components | No findings |
| A07 Identification & Authentication Failures | No new findings |
| A08 Software & Data Integrity Failures | findings: 1 LOW |
| A09 Security Logging & Monitoring Failures | No new findings (§9.1 logging) |
| A10 SSRF | No new findings |

## Findings

| # | Sev | Where | Finding | Fix |
|---|---|---|---|---|
| N1 | MEDIUM | §6.7 E-1, §6.6 | **The expiry timer and the launcher can both write an outcome.** The launcher re-checks freshness at about 29.9 s and then calls `ILauncher.Launch`. The 5 s timer then writes `launch.failed SND-LAUNCH-007` at 30 s, and the launcher writes `launch.started`. The two keys differ (`launch.failed:<id>` and `launch.started:<id>`), so the ledger takes both. Warp opened, but `LaunchView` can say `failed`, and the human clicks Open again: two sessions on one item, which is what E-1's "at most once" forbids. | Give each launch one outcome key (`launch.outcome:<launchId>`, first writer wins), or have the launcher append `launch.launching` (a claim) before `Launch` that the timer skips. Define how the `launches` projection treats a second outcome. Add a test with a `FakeTimeProvider` that puts the launch on the deadline. |
| N2 | MEDIUM | §6.4 first bullet, E-6 | **"At once when the item has no active instance" isn't fenced against a start that runs at the same time.** If `StartInstance` (or S2's auto-start) commits between the launcher's check and the launch, automation runs beside the new session. §6.4 says "automation never runs beside the new session" without a mechanism for this case. | The engine treats a `launch.started` with no handback since as a takeover: an instance started on that item begins `taken-over`, or `StartInstance` refuses while a launch is in flight. Or the launcher records the ledger sequence it checked and the engine applies `open-request` to any instance started after that sequence. Add an integration test. |
| N3 | LOW | §6.4, E-6, §6.6 | A takeover recorded at, say, 28 s, followed by an expired or failed launch, leaves the instance `taken-over` with no session. The 2 h `takeoverIdle` row is the only prompt to hand back. | When `launch.failed` follows the takeover it caused, the needs-you `park` row "Hand back ITM-…?" appears at once, not after 2 h. |
| N4 | LOW | §4.4 `needs-answer` row vs E-12 / §7 | §4.4 names `ApplyCommand:answer` (desk, ready). E-12 and §7 name `answer-text`. Under the fail-closed rule an unknown variant becomes `protected`, so the form would turn into copy-the-CLI. | Use `answer-text` in §4.4. |
| N5 | LOW | §6.3 step 5 | The `<AW_SINDRI_HOME>/launch/mcp.json` file is shared by all launches. If X8 picks HTTP with any token, an honest stale file points older sessions at a moved address. | Write it per launch (`mcp-<launchId>.json`, 0600) and clean it up with the YAML file, or state that it holds no token and is rewritten atomically. |

## STRIDE
S: no new findings · T: N1 · R: no new findings (actors are `service:launcher` and `service:engine`) · I: no new findings · D: N3 · E: N2 (automation acts beside a human session).

## Verdict
- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 2 (N1, N2)
- **Recommendation:** SHIP. The MEDIUMs are small edits to E-1 and E-6 and should land before P10a and P10b.

verdict: PASS
