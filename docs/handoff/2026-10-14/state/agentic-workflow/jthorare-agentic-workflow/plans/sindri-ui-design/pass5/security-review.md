# Threat Model — sindri-ui-design (revision 5, pass 5)

**Mode:** plan (slim) · honest-mistake threat model, loopback-only v0
**Source:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md
**Reviewed:** 2026-10-11

## Summary
The pass-4 fixes hold in the text. P4-1: E-1 defines the claim as an in-process claimed set held under the timer's lock. Nothing is claimed after its deadline, a claim lost to a crash is swept at start-up, and the race test now asserts the one outcome is `launch.started`. P4-2: §6.4 and E-19 limit starts-taken-over to `requested` launches, with the request as causation, so the failed-launch fold matches. P4-3: per-target session-id patterns, with a fall back to Open (§6.3 step 1). P4-4: the file is deleted 60 s after `launch.started`, and the spike measures Warp's cold-start read latency. Earlier HIGHs F1 and P2-1 are unchanged. One new MEDIUM comes from revision 5's own 10 s `Launch` limit: a timed-out launch is recorded as failed and told to retry, though Warp may already have opened. Its launch file is never cleaned up. One LOW. No CRITICAL or HIGH.

## Coverage
| Category | Status |
|---|---|
| A01 Broken Access Control | No new findings (F1 `AW_SINDRI_CHILD=1`, caller headers, protected actions copy-only, all still hold) |
| A02 Cryptographic Failures | N/A — loopback, the app handles no secrets, no token in the per-launch MCP file |
| A03 Injection | No new findings (YAML serializer, shell-quoted argv, id patterns incl. per-target session ids, E-16 hold) |
| A04 Insecure Design | findings: 1 MEDIUM (P5-1) |
| A05 Security Misconfiguration | findings: 1 LOW (P5-2) |
| A06 Vulnerable & Outdated Components | No findings |
| A07 Identification & Authentication Failures | No new findings |
| A08 Software & Data Integrity Failures | Covered by P5-1 (an outcome that contradicts reality; a launch file left behind) |
| A09 Security Logging & Monitoring Failures | No new findings (doctor's `launcher` line, §9.1 logging) |
| A10 SSRF | No new findings |

## Findings

| # | Sev | Where | Finding | Fix |
|---|---|---|---|---|
| P5-1 | MEDIUM | §6.7 E-1 ("10 s limit on `ILauncher.Launch`"), §6.6 `SND-LAUNCH-001` row, §6.3 step 4 | **A timed-out `Launch` is recorded as a plain failure, and its file stays on disk.** E-1 now writes `launch.failed SND-LAUNCH-001` when `Launch` takes more than 10 s. The step that is slow is usually `open warp://launch/…` while Warp cold-starts, and Warp may still read the file and open the tab. §6.6 tells the human to "Retry" for `-001`. With an outcome now written, `SND-LAUNCH-008` no longer blocks, so a retry starts a second session on the item. This is N1/P4-1's double-session problem again, through the new timeout path. Also, step 4 deletes the file only "60 s after `launch.started`". On any `launch.failed` written after the YAML/MCP files exist (the timeout, a non-zero `open` exit, or a refusal after writing), the files stay until the launcher next starts. Until then the launch can be re-run from Warp's palette, which is what the sweep is meant to stop. | Give the timeout its own outcome: either a separate code (for example `SND-LAUNCH-009 "Warp may have opened"`) or `-007`'s wording ("look for `sindri ITM-…` before opening again", never "Retry"). Set `launch.confirm` for it, as for a started launch. Change step 4 to "delete 60 s after the launch's outcome, whatever it is". Have the recording-`ILauncher` integration test assert the files are gone after a failed outcome. |
| P5-2 | LOW | §6.2 "Open another session" row, U6a; §6.7 E-4 | `launch.confirmWindow` is called a profile value, but E-4 adds only `launch.default` and `launch.repos.<repo>.default` to S1's **closed** profile schema. As written, the window either can't be set or gets rejected. | Add `launch.confirmWindow` (a duration, default 2 h) to E-4. |

## STRIDE
S: no new findings · T: P5-1 (a `failed` outcome while a Warp tab may be open; a launch file left behind) · R: no new findings (actors `service:launcher`, `service:engine`, `human:ui`) · I: no new findings · D: no new findings · E: no new findings.

## Verdict
- **Critical findings:** 0
- **High findings:** 0
- **Medium findings:** 1 (P5-1)
- **Recommendation:** SHIP. P5-1 is a code or wording choice plus one cleanup sentence, and should land before P10a.

verdict: PASS
