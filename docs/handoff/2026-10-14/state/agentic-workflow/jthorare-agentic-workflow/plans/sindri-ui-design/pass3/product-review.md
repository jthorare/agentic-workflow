# Product Review: Sindri UI app design (revision 3), pass 3

_Reviewed by `/productReview` on 2026-10-11 | Mode: growth (slim)_

Plan: `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` (worktree `scratchpad/ui-design`), checked against pass-1 `product-review.md`, `pass2/consolidated-review.md` and §17. "Growth" means adoption and habit for one engineer on a loopback-only toolkit. The owner's decisions (layout, `#f2994a`, the deferred "Run in Warp" C4) are not re-raised.

## Verdict: SHIP

No CRITICAL or HIGH. The prior product fixes hold: U6a (Open, on P10a = P3 + P8, wave W3) ships before the engine, and Attach and Resume moved to U6b (P2-2). The Running cap of 5 and the `--large` fold fixture are in §3.2 and §18.2 (C3). The success numbers are in §2 (C5), X24 records the deferred "every agent" goal (C6), and **Check again** is in E-3 (C7). One new MEDIUM (G1) and two LOWs remain.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Holds. (1) One-click Open in Warp now lands in W3 next to Needs you. (2) The pulse, tray, dock and title loop is complete at U3. (3) Copy-the-CLI blocks and the catalog palette teach the CLI. Pass-1 C1 is resolved |
| Activation funnel | The pre-engine funnel is acceptable (decisions, failures, profile drift, the queue and launching). The new drop-off comes after the click: before S2, a launched item stays in Queued with the same Open button, so the engineer can't see what they launched (G1) |
| 10x usage | Fixed: Running and With you are capped at 5 with "Show all N", and the `--large` fixture (30 items, 12 needs-you) runs the five-second test. §3.2 still contradicts itself on the needs-you cap and the fold (G2) |
| Retention hooks | Strong: per-row pulse, Mark all seen, close-to-tray, the `takeoverIdle` reminder, `notify.desk` as the only notifier. §2 defines the three outcome numbers, but nothing reads them yet (G3) |
| Viral coefficient | N/A in the usual sense: single user, loopback-only, no sharing surface. Pull-through is present: `C` Create item, `R` File a request, and launched sessions file `sindri_request` rows that land back in Needs you |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| G1 | MEDIUM | **Before the engine, launched work disappears.** §6.4 says that before S2 "the item records the launch and nothing pauses", and §6.6 says the item moves to With you "once the engine runs". In U6a, then, an opened item stays in Queued with **Open** as its action, and U6a's own text says "a second Open on the same item starts another session". From U6a until WF P5, the five-second question 2 ("what's running and where") has no answer for the app's main action, and double-launching one item is a single click away | Extend E-2's `launch` block with `lastStarted?: {target, at, tabTitle}` (server-computed from the `launches` projection). The row and the item header then show "Sent to Warp 12m ago · Claude Code · tab `sindri ITM-…`", and the server labels the mode **Open another session** (with a confirm) while a recent `launch.started` exists. Add both to U6a's observable signal. This adds no client logic |
| G2 | LOW | **§3.2 contradicts itself.** The "Needs-you cap" bullet says Home shows at most 5 rows, "so Running, Queued and Health stay above the fold at 1280 × 800 with up to 6 rows". The fold rule above it (revised after the `/impeccable` critique) says the health strip may sit just below the fold and the cap is 5 (4 at scale 1.3). The prover and the design reviewer read these lines | Delete the bullet's last sentence, or make it "…Running and Queued start above the fold with up to 5 rows; health stays visible in the pill" |
| G3 | LOW | **The success numbers have no reader.** §2 says "the scorer can report them once the events exist", but the scorer is a later Sindri block rebuilt from its TS reference (README step 4), and no phase or view owns these numbers | Name the owner: either the scorer block's first recipe or a later Health/Today row in §10.2. Or state that the owner reads them ad hoc from the ledger until then |

verdict: PASS
