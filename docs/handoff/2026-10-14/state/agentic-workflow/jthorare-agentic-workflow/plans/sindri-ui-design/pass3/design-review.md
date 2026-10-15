# Plan Design Review (pass 3, slim): sindri-ui-design (revision 3)

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md (+ mockups/, shotgun/)
**Reviewed:** 2026-10-11

## Inputs available
| Input | Present? |
|---|---|
| spec (plan) | ✓ |
| design-brief.md | ✗ (the spec is the design doc) |
| product.md | ✗ |
| .impeccable.md | ✗ (docs/sindri/DESIGN.md used as the brand source instead) |
| design-tokens.json | ✗ (§14 token plan used instead) |
| mockups (12 HTML + PNG) | ✓ |

## Prior HIGH/MEDIUM fixes: do they hold?
| Prior | Holds? | Note |
|---|---|---|
| D1 mockups missing; P2-5 mockups lag the text | Mostly | All 11 mockups carry `#eb5757`, `#535fc6`, `#828fff` ring, reduced-motion CSS, urgency words; home has Mark all seen + "Show all 7 in Queue"; launch-flow no longer says "stops the worker". New drift below (N1) |
| D5/D6/D7 lifecycle, cleared sheet, endless "Sending" | ✓ | §4.1, §6.6 (15 s / 30 s) |
| D8/D9/D10 hover, Error token, pending ink | ✓ | §11.1, §14.1, §7 |
| D11/D12/D13 sheet focus, endless pulse, text scale | ✓ | §3.2, §5, §14.3 |
| D14/D15 key collisions | ✓ | §8 reserved keys (P2-9) |
| P2-7/P2-10 | ✓ | §3.2 pill words split by source; §7 and §14.1 reworded |

## Ratings
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 5/5 | — |
| Interaction states | 4/5 | Home's Running / Queued panels have no defined empty state (N2) |
| User flow completeness | 4/5 | Running rows offer Attach in the text and mockups; §6.2 says Take over (N1) |
| Accessibility | 5/5 | LOWs only |
| Brand/voice consistency | 4/5 (against DESIGN.md; .impeccable.md absent) | Health pill reads "Healthy" beside "index failed" (N4) |

## New findings
| # | Severity | Section | Finding | Recommended fix |
|---|---|---|---|---|
| N1 | MEDIUM | §3.2 ASCII, §4.2 item 2, §4.5 "Running"; mockups `home`, `needs-you`, `queue` | The text and the Home mockup put **Attach** on every *running* row, and the mockups add the hint "Attach pauses automation (a takeover)". §6.2 (and pass-1 arch F13) defines Attach as focus-only, offered only while the item is **with you** after a `launch.started`. A running item gets **Take over** ("pauses automation") or **Open**. The five-second screen and the design reviewer's reference (§19) teach the meaning that was fixed out | §3.2/§4.2/§4.5: the Running row's action is "the first mode in its `launch` block (Open, or Take over once the engine runs)"; Attach only on With-you rows. Fix the three mockups or add the delta to §16's known-differences list |
| N2 | MEDIUM | §4.2 "States", §4.5 "Empty", §12.4 | Home says "each panel is empty with its own view's `empty` sentence", but Running and Queued both come from one view (`GET /queue`, one `empty`). §4.5 hides empty groups. So with nothing running, Home's left panel either disappears (breaking the fixed four-panel layout and the five-second answer to "what's running") or needs a sentence the app may not own (§12.4) | Ask the API for per-group empty sentences on `GET /queue` (an E-edit), or add "Nothing running" / "Nothing queued" to §12.4 and say that Home's panels never hide |
| N3 | LOW | §3.2 "Needs-you cap" | "stay above the fold at 1280 × 800 with up to 6 rows" contradicts the cap of 5 and the fold rule, which lets the health strip sit below | "with up to 5 rows; the health strip may sit just below (the pill carries health)" |
| N4 | LOW | §3.2 pill, E-14, mockups | The fixture pill shows "Healthy" with "index failed" and "profile changed" beside it. E-14 computes the word server-side, but the reference fixture models a mixed signal | Make the fixture's word "Degraded", or state in E-14 which problems leave the word `healthy` |
| N5 | LOW | §5, mockup `needs-you` | The expanded Needs-you mockup has no **Mark all seen** button in its panel header (only `home` shows it); `launch-flow` lacks the "Warp hasn't confirmed" 15 s state; launch-flow frame 4 shows `liveSession {…}` where E-2 names it `attach` | Add them, or list them in §16's known differences |

## Surface × state coverage (spot check against §4.9; full matrix in the spec)
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you | §4.1 skeleton once / rebuild banner | §4.1 inline | §4.9 `empty` | §4.1 accepted |
| Home panels | §4.9 | — | **GAP (N2)** | — |
| Launch block | §6.6 "Sending to Warp…" | §6.6 table | — (no block when closed) | §6.6 "Sent to Warp" |
| Park answers | §4.1 sending | §9.1 WF codes | n/a | §4.1 + §4.4 |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Rows / panels | §8 | §11.2 names, §5 live region | §11.1 | §3.3 stable focus, §8 key guard |
| Detail sheet | Enter/Esc §8 | §11.2 | §11.1 | §3.2/§11.2 heading focus <1100 px |
| Palette/dialogs | ⌘K | §8 | §14.1 Surface3 | §11.2 trap + return |

## Verdict
No CRITICAL or HIGH. Prior fixes hold. Two new MEDIUMs: N1 (Attach on running rows contradicts §6.2) and N2 (no empty state for Home's Running and Queued panels). No dimension scores below 4.
verdict: PASS
