# Plan Design Review — sindri-ui-design (pass 5, revision 5)

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md (+ mockups/)
**Reviewed:** 2026-10-11

## Inputs available
| Input | Present? |
|---|---|
| spec (plan) | ✓ |
| design-brief.md | ✗ (the spec is the design doc) |
| product.md | ✗ |
| .impeccable.md | ✗ (docs/sindri/DESIGN.md used as the brand source, as in passes 1–4) |
| design-tokens.json | ✗ (§14 token plan used instead) |

## Prior HIGH/MEDIUM fixes: do they hold?
| Prior | Holds? | Note |
|---|---|---|
| P4-1 "Open another session" decided by the app | ✓ (mostly) | Server-owned `launch.confirm?: {reason}` in E-2 (l.520); rows in §6.2 (l.404), §4.9 (l.328), §12.4 (l.789); §16 lists the missing mockup. Clearing rule has a pre-engine hole, see P5-1 |
| P4-2 "view" vs "group" (LOW) | ✓ | §4.2 l.212 |
| P4-3 E-18 order and intro (LOW) | ✓ | §6.7 intro "E-12…E-19" (l.514); E-18 after E-17 |
| P4-4 Home summary copy (LOW) | Partly | Summary template in §12.4 (l.785), but it disagrees with §3.2 and the panel header is still unlisted (P5-3) |
| Pass 1–3 items (D1–D26, P2-*, N1/N2) | ✓ | Spot-checked: Attach only on With-you rows (§4.2 l.207, §4.5 l.268), E-18 per-group `empty`, §4.1 lifecycle, §11.1 ring/hover, §8 reserved keys |

## Ratings
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 5/5 | — |
| Interaction states | 4/5 | P5-1: pre-engine confirm never clears; §4.9 shows it unconditionally |
| User flow completeness | 4/5 | P5-1: every later Open on a once-launched item confirms until Close (U6a) |
| Accessibility | 5/5 | — |
| Brand/voice consistency | 5/5 (against DESIGN.md) | LOW: summary-line template drift (P5-3) |

## New findings
| # | Severity | Section | Finding | Recommended fix |
|---|---|---|---|---|
| P5-1 | MEDIUM | §6.2 l.404, E-2 l.520, §18 U6a l.1054, §17 pass-4 row l.1035 | `launch.confirm` is set "because a launch for the item started and nothing has taken it over or handed it back", and the row applies only "Before the engine". Before the engine neither a takeover nor a handback can exist, so the confirm never clears: in U6a every Open after the first on that item asks for confirmation until the item closes. Pass-4 product H1 asked for a bound (e.g. a window or until Close); the resolution kept the engine-only clearing condition | Give `LaunchOptions.For` a pre-engine bound and state it in §6.2 and E-2: e.g. "set while the newest `launch.started` is under 2 h old (or until the item closes); once the engine runs, cleared by the takeover/handback". Say so in U6a's signal (the label reverts to **Open in <agent>** after the window) |
| P5-2 | LOW | §4.9 Launch flow row l.328 | "after one: 'Sent to Warp <age> ago' and **Open another session**" reads as unconditional, contradicting §6.2's "while the server sets `launch.confirm`" | "after one, while `launch.confirm` is set: …; otherwise **Open in <agent>**" |
| P5-3 | LOW | §12.4 l.785 vs §3.2 l.97, §4.2 | §12.4's template is "N need you · N running · N with you · N queued"; the §3.2 window and mockups show "5 need you · 3 running · 4 queued". The panel header "RUNNING 3 · WITH YOU 1" is app-composed and still not in §12.4 | Pick one summary template and use it in §3.2 and §12.4; add the panel-header count template ("RUNNING N · WITH YOU N", "QUEUED N", "NEEDS YOU N") to §12.4 |
| P5-4 | LOW | §16 l.928 | "no mockup shows Open another session (§6.2 and the prototype do)": "the prototype" isn't in §16's file table or anywhere else in the spec | List the prototype file in §16's table once it exists, or drop the parenthetical |
| P5-5 | LOW | §6.2 table l.401–407 vs E-1 l.519 | "Open another session" is a row in the *Mode* table, but E-1's `mode` enum is `open\|attach\|resume` and E-2 `modes` has no such mode; a reader could add a fourth mode | Label the row "Open (with `confirm`)" or add a note: it is the `open` mode with `launch.confirm` set, sent as `mode: open` |

## Surface × state coverage (changed areas)
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you | §4.1 skeleton / rebuild banner | §4.1 inline | §4.9 `empty` | §4.1 accepted |
| Home Running / Queued | §4.9 | — | E-18 group `empty` (§4.2 l.208) | — |
| Launch block (first open) | §6.6 "Sending to Warp…", 15 s text | §6.6 table | no block when closed | "Sent to Warp" (§6.6 l.508) |
| Launch block (second open, pre-engine) | as above | as above | n/a | "Sent to Warp <age> ago" + confirm (§6.2 l.404); **clearing GAP (P5-1)** |
| Park answers | §4.1 sending | §9.1 WF codes | n/a | §4.1 + §4.4 |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Rows / panels | §8 | §11.2 names, §5 live region | §11.1 | §3.3 stable focus, §8 key guard |
| Detail sheet | Enter/Esc §8 | §11.2 | §11.1 | §3.2/§11.2 heading focus <1100 px |
| Palette / dialogs (incl. the launch confirm) | ⌘K | §8 | §14.1 Surface3/4 | §11.2 trap + return |

## Verdict
No CRITICAL or HIGH; all prior HIGH/MEDIUM fixes are in the text. One new MEDIUM (P5-1: the pre-engine "Open another session" confirm has no clearing condition) and four LOWs; no dimension below 4.
verdict: PASS
