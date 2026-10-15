# Product Review: Sindri UI app design (revision 5), pass 5

_Reviewed by `/productReview` on 2026-10-11 | Mode: growth (slim)_

Plan: `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` (worktree `scratchpad/ui-design`), checked against `pass4/product-review.md` and §17 "Pass 4 → revision 5". Here "growth" means adoption and habit for one engineer on a loopback-only toolkit. The owner's decisions (layout, `#f2994a`, the deferred "Run in Warp" C4) are not re-raised.

## Verdict: SHIP

No CRITICAL or HIGH findings. Pass-4 H2 is fixed: §2 now names a source for each of the three numbers. Pass-4 H1 is mostly fixed. `launch.confirm` is served by the server (E-2) and appears in §6.2, §4.9, §12.4, §16 and U6a, so the app no longer decides it. Only the time bound H1 asked for is still missing; that remainder is I1 (MEDIUM). One new LOW (I2).

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Holds. One-click Open in W3 (U6a on P10a). The pulse, tray and badge loop lands in U3. Copy-the-CLI blocks and the palette teach the CLI |
| Activation funnel | The re-open step is now server-driven. Remaining drop-off risk: before the engine, `confirm` never clears, so every later Open of an item asks a confirm, for days. Users learn to click through it (I1) |
| 10x usage | Holds: caps of 5 (4 at scale 1.3), "Show all N", the `--large` fixture, the five-second test on both fixtures, Mark all seen |
| Retention hooks | Holds: the per-row pulse, close-to-tray, the `takeoverIdle` reminder, `notify.desk`. The success numbers each have a source now. The launch number counts "Sent to Warp", not a session that actually started work (I2) |
| Viral coefficient | N/A: one user, loopback only. Pull-through is unchanged: sessions file `sindri_request` rows that come back as Needs you |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| I1 | MEDIUM | **`launch.confirm` has no time bound before the engine.** §6.2 and U6a set it "while a launch started for the item that hasn't since been taken over or handed back". Before S2 there is no instance, so nothing ever takes over or hands back: once an item has been opened, every later Open asks a confirm until the item closes, and the row reads "Sent to Warp 3d ago". A confirm that always appears teaches the user to dismiss it, which defeats it. Pass 4 suggested a window, and E-2 and §6.2 don't have one | In `LaunchOptions.For`, before the engine, set `confirm` only within a window after `lastStarted.at` (for example 2 h, as a named constant). After that, show plain **Open** and keep or age out the "Sent to Warp" line. State the window in §6.2's row and E-2. Add a `FakeTimeProvider` test |
| I2 | LOW | **The launch success rate measures the hand-off, not activation.** `launch.started` means Warp accepted the URI. The prover already uses a better signal: the session's first `sindri_item` call | Optionally count launches whose session made its first Sindri MCP call (via `AW_SINDRI_LAUNCH`) as the activation number once that call is recorded. Otherwise note in §2 that the rate is an upper bound |

Fewer than 3 concerns because the pass-4 items are resolved and the lenses found nothing new above LOW besides I1.

verdict: PASS
