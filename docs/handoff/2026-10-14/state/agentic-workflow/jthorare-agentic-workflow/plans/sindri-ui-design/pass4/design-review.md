# Plan Design Review — sindri-ui-design (pass 4, revision 4)

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md (+ mockups/)
**Reviewed:** 2026-10-11

## Inputs available
| Input | Present? |
|---|---|
| spec (plan) | ✓ |
| design-brief.md | ✗ (the spec is the design doc) |
| product.md | ✗ |
| .impeccable.md | ✗ (docs/sindri/DESIGN.md used as the brand source) |
| design-tokens.json | ✗ (§14 token plan used instead) |

## Prior HIGH/MEDIUM fixes: do they hold?
| Prior | Holds? | Note |
|---|---|---|
| N1 Attach on running rows | ✓ | §3.2 ASCII (l.106), §4.2 item 2, §4.5 "Running" give Open ▾ + Take over; Attach only on With-you rows; mockup delta listed in §16 |
| N2 No empty state for Running / Queued | ✓ | E-18 per-group `empty`, every group returned; §4.2 l.207; §18.1 lands it in P3 for U2/U3 |
| N3 fold rule (LOW) | ✓ | §3.2 l.124–127 "up to 5 rows", health strip may sit below |
| N4 pill word (LOW) | ✓ | §3.2 ASCII now "Degraded · … · index failed"; §16 lists the mockup delta |
| Pass 1–2 items (D1–D26, P2-*) | ✓ | Spot-checked §4.1 lifecycle, §6.6 15 s/30 s, §11.1 hover/ring, §8 reserved keys |

## Ratings
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 5/5 | — |
| Interaction states | 4/5 | The pre-engine "Open another session" state exists only in §18 U6a (P4-1) |
| User flow completeness | 4/5 | Same: second-launch flow has no row in §6.2, no confirm copy, no mockup (P4-1) |
| Accessibility | 5/5 | — |
| Brand/voice consistency | 5/5 (against DESIGN.md) | LOW: Home summary sentence not in §12.4 (P4-4) |

## New findings
| # | Severity | Section | Finding | Recommended fix |
|---|---|---|---|---|
| P4-1 | MEDIUM | §6.2 mode table, §6.6, §4.9 "Launch flow", §12.4, §16; defined only in §18 U6a and §17 G1 | The pass-3 fix for product G1 ("Sent to Warp 12m ago · … · tab", button becomes **Open another session** with an in-app confirm) lives only in the phase table. §6.2 (the authoritative mode list) still says Open is offered whenever no launch is `requested`, with no change of label or confirm; the confirm's wording isn't in §12.4; no mockup shows it. As written, the app would choose the label and the confirm by checking `launch.lastStarted` itself, which is the kind of client-side rule §2 "No logic" forbids | Add the state to §6.2 (Open, when `lastStarted` is set and the engine isn't running: label "Open another session", confirm), have E-2 carry it explicitly (e.g. `open` mode with `confirm: {text}` or a `label` hint) so the server decides, add the "Sent to Warp Nm ago" line and the confirm sentence to §12.4, add a §4.9/§6.6 row, and add the frame to `launch-flow` or list it in §16's known differences |
| P4-2 | LOW | §4.2 l.211 | "Each panel is empty with its own view's `empty` sentence" still says *view*; l.207 and E-18 say *group* | "Needs you uses its view's `empty`; Running and Queued use their group's (E-18)" |
| P4-3 | LOW | §6.7 intro l.512, E-18 placement | The intro's ranges (E-1…E-11, E-12…E-16, E-17) omit E-18, and E-18 sits between E-15 and E-16 | Move E-18 after E-16 and add it to the intro's "UI-driven edits elsewhere" range |
| P4-4 | LOW | §3.2 l.96, §4.2, §12.4 | Home's summary line ("5 need you · 3 running · 4 queued") and the panel header "RUNNING 3 · WITH YOU 1" are app-composed text not listed in §12.4 | Add them to §12.4 as count templates (counts from the views' rows) |

## Surface × state coverage (spot check of changed areas)
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you | §4.1 skeleton / rebuild banner | §4.1 inline | §4.9 `empty` | §4.1 accepted |
| Home Running / Queued | §4.9 | — | E-18 group `empty` | — |
| Launch block | §6.6 "Sending to Warp…" | §6.6 table | no block when closed | "Sent to Warp"; **GAP: second-launch state (P4-1)** |
| Park answers | §4.1 sending | §9.1 WF codes | n/a | §4.1 + §4.4 |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Rows / panels | §8 | §11.2 names, §5 live region | §11.1 | §3.3 stable focus, §8 key guard |
| Detail sheet | Enter/Esc §8 | §11.2 | §11.1 | §3.2/§11.2 heading focus <1100 px |
| Palette / dialogs (incl. the launch confirm) | ⌘K | §8 | §14.1 Surface3/4 | §11.2 trap + return |

## Verdict
No CRITICAL or HIGH; all prior fixes hold. One new MEDIUM (P4-1: the pre-engine "Open another session" state is specified only in the phase table) and three LOWs; no dimension below 4.
verdict: PASS
