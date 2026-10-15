# Product Review: Sindri UI app design (revision 4), pass 4

_Reviewed by `/productReview` on 2026-10-11 | Mode: growth (slim)_

Plan: `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` (worktree `scratchpad/ui-design`), checked against `pass3/product-review.md` and §17 "Pass 3 → revision 4". Here "growth" means adoption and habit for one engineer on a loopback-only toolkit. The owner's decisions (layout, `#f2994a`, the deferred "Run in Warp" C4) are not re-raised.

## Verdict: SHIP

No CRITICAL or HIGH findings. All three pass-3 product fixes are in the text. G1: E-2 has `lastStarted`, and U6a's goal and signal both include "Sent to Warp … ago" and **Open another session** with a confirm. G2: §3.2's cap bullet now defers to the fold rule. G3: §2 names the owner's ledger read and the Today view. One new MEDIUM (H1) and one LOW (H2) remain.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Holds. One-click Open lands in W3 (U6a on P10a). The pulse, tray and badge loop lands in U3. Copy-the-CLI blocks and the palette teach the CLI. Nothing regressed |
| Activation funnel | The pre-engine drop-off after the click is fixed: `lastStarted` shows what was launched. New gap: the spec doesn't say who decides "Open another session" and the confirm, or when the label stops applying (H1) |
| 10x usage | Holds: caps of 5 (4 at scale 1.3), "Show all N", the `--large` fixture with 30 items and 12 needs-you rows, and the five-second test on both fixtures. The §3.2 contradiction is gone |
| Retention hooks | Holds: the per-row pulse, Mark all seen, close-to-tray, the `takeoverIdle` reminder, `notify.desk`. The success numbers now have a reader, but the stated ledger command covers only one of the three (H2) |
| Viral coefficient | N/A in the usual sense: one user, loopback only. Pull-through is unchanged: launched sessions file `sindri_request` rows that come back as Needs you |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| H1 | MEDIUM | **The app decides "Open another session".** U6a says the button reads **Open another session** and asks an in-app confirm whenever `lastStarted` exists. That is a client-side rule deriving a label and a confirmation from whether a field is present, which conflicts with U1, §2 "No logic" and E-2's "the app decides neither". Nothing bounds `lastStarted` either. Read literally, any item ever opened asks for a confirm on every later Open, including after U6b's handback, where §6.2 offers Resume next to **Open new**. §6.2's mode table, §6.6 and §12.4 don't mention the label, the confirm text or the "Sent to Warp 12m ago · Claude Code · tab …" line | Have the server say it. Add `confirm?: {reason}` (or a mode-label field) to E-2's `launch` block, set by `LaunchOptions.For` only while a `launch.started` has no takeover or handback after it (before the engine: within one window, for example 2 h, or until Close). Add the label, the confirm sentence and the "Sent to Warp … ago" line to §12.4, and add a row to §6.2's table |
| H2 | LOW | **One success number has no command.** §2 says to read all three numbers with `sindri ledger --type launch.*`. That covers the launch success rate only. Time to clear needs-you and the `human:ui` vs `human:cli` share need other event types or actor filters | Give the right filter for each number (for example `--actor human:ui`, and the needs-you appear and clear events), or say which two wait for Today |

verdict: PASS
