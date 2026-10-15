# Plan Design Review (pass 5, final) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 5)
**Reviewed:** 2026-10-11 · slim · pass 5 of 5

Scope: (1) design findings from earlier passes that are still open, and (2) new problems in revision 5's surfaces (the §3.8 rows, §4.1 drift approval, §5.3, §5.4 dispatch routing, §5.7, §5.8, §6.4, §8). Resolved items and the §13 written answers are not raised again. Line numbers refer to revision 5.

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✗ (the spec is the plan) |
| design-brief.md | ✗ |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ |

Brand/voice is N/A because `.impeccable.md` is missing.

## Pass-4 findings: status
| Pass-4 | Status |
|---|---|
| J1 `reevaluate` replays the consumed key and the saved gate answers | Resolved: §5.3 l.381–383, §5.8 l.503–504, §3.8 l.218, test l.779. One leftover: the answer-line rule doesn't match the new wait text (K2) |
| J2 `escalated (child)` text and commands | Resolved: l.226 (`{childNext}`, the child's `allowedCommands`), §4.2 challenge bound to the child, per-row golden files l.657 |
| J3 forwarding scope | Resolved: l.226, l.481 |
| J4 dispatch code, text, revoke collision | Resolved for `SND-WF-011` and revoke (l.222, l.418, §8 l.717). A dispatch refusal for **drift** isn't covered (K1) |
| J5 accept over-promises | Resolved in the §3.8 row (l.224). Layout 4 still uses the old text (K5 a) |
| J6 LOWs | (a) variables, (e) `since` date, (f) golden list, (g) `SND-WF-013`, (i) `T-8`: resolved. Still open: (b) layout 3 drops "Grant tokens at a terminal, or cancel.", and layouts 1 and 2 break the l.656 `commands` order; (c) layout 4 has no answer line or `(at a terminal)`; (d) the P5 signal l.1282 still says `0m 40s`; (g) §4.2 l.271 still gives `SND-RECIPE-009` for every expired challenge; (h) the revoke preview doesn't list the takeovers it ends (K5) |

## Ratings
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 4/5 | The revoked-child park says "This recipe was revoked" when the parent's recipe isn't the revoked one, and the fix it gives doesn't help (K4) |
| Interaction states | 4/5 | A drift recorded on an instance that is already parked doesn't show in `status`. The human types a challenge and gets `SND-RECIPE-006` (K3) |
| User flow completeness | 4/5 | When dispatch refuses an effect for drift, the `rearm` resume never re-emits it (K1). The wait park's answer line suggests `--text`, but the row says `--resume` (K2) |
| Accessibility | 5/5 | Only LOW items (K5 c) |
| Brand/voice consistency | N/A — missing .impeccable.md | (unscored) see K5 a |

## Surface × state coverage (revision-5 surfaces only)
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| `parked: needs-answer` (wait), then `--resume [--text]` | n/a | — | n/a | Row l.218 ✓. The answer line l.652 suggests `--text`: **GAP** (K2) |
| `parked: spec-conflict` (drift) at dispatch, `SND-RECIPE-006` | n/a | No absorption rule for an instance the drift wake already parked (l.418 only absorbs revokes) | n/a | `rearm` (l.497) doesn't re-emit the dead command: **GAP** (K1) |
| A park with a drift recorded on its frame (l.505) | n/a | `SND-RECIPE-006` comes only after the challenge is typed. `status` doesn't show the drift: **GAP** (K3) | n/a | It isn't clear whether the drift approval (l.221, l.252) resumes it |
| `parked: spec-conflict` (revoked child, l.256) | n/a | Text l.220 points at the wrong recipe and the wrong fix: **GAP** (K4) | n/a | `cancel` ✓ |
| `escalated (gate)`, then `--accept` | n/a | — | n/a | Row l.224 ✓. Layout 4 l.632 disagrees with it (K5 a) |
| `escalated (child)` | n/a | — | n/a | l.226 ✓ |
| `parked: spec-conflict` (dispatch, `SND-WF-011`) | n/a | l.222, §8 ✓ | n/a | `reemit` ✓ |
| Drift approval with batch resume | n/a | — | n/a | §4.1 l.252 preview lists the instances ✓ |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| All read verbs | n/a | `--json`, ASCII, one duration format ✓ | No colour ✓ | n/a |
| Challenge | Typed TTY text ✓ | Plain-text preview ✓ | n/a | n/a |
| Layout 4 (`escalated: gate`) | n/a | Still no answer line, and `--accept` isn't marked `(at a terminal)` (K5 c) | n/a | n/a |
| Avalonia (later) | N/A | `allowedCommands` with actor classes ✓ | N/A | N/A |

## Findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| K1 | MEDIUM | Flow | §5.4 l.416–420; §5.8 l.497; §3.8 l.221 | `outbox.refused` with `SND-RECIPE-006` parks the instance with drift. The drift resume is `rearm`, which restores the frame and re-evaluates a Step trigger, but it never re-emits the command that dispatch marked dead. After re-approval the instance waits for an event that never arrives (for example `dev-notify`'s `sent`, or `claimed` until its 10m retry). There's a second problem: the startup drift wake usually parks the instance first, and l.418 absorbs only revoke refusals. So the second park has no rule (the stack rules at l.329–330 allow one park) | Absorb a drift `outbox.refused` into an existing drift park the way revokes are absorbed, and record the refused command on that frame. Make the drift resume `rearm` plus `reemit` for every command recorded there. Add `Drift_refused_effect_is_reemitted_after_reapproval` |
| K2 | MEDIUM | Flow / States | §6.4 l.652; §3.8 l.217–218; §5.8 l.509 | The answer-line rule says "`needs-answer` suggests `--text`" for both variants. The wait variant's way out is `answer --resume [--text]` (l.218), and l.509 makes an answer that doesn't fit the reason `SND-WF-003`. So the suggested command may be refused, or it may do something the row doesn't describe | Split the rule: needs-answer (Step) suggests `--text`, and needs-answer (wait) suggests `--resume`. Say whether a bare `--text` on a wait park counts as `--resume --text` or is `SND-WF-003` |
| K3 | MEDIUM | States / Flow | §5.8 l.505–506; §4.1 l.248–252; §6.4 | Take an instance parked on `budget` that then gets a drift wake. The wake is recorded on its suspended frame, but nothing in `status` shows it. The human reads `parked: budget`, types a `grant-tokens` challenge, and only then gets `SND-RECIPE-006`. It's also unclear whether the drift approval that "resumes every instance it lists" pops this park (which would skip the grant), or only clears the drift | Add a `drift` line to the layout (`drift  {block} changed; re-approve {recipe}@{hash12} first`), and put the re-approve command first in the answer line. Say in §4.1 that a drift approval clears a recorded drift without popping a park of another reason, and that its preview lists those instances separately ("still parked on budget") |
| K4 | MEDIUM | IA / Flow | §4.1 l.254–257; §3.8 l.220 | A parent that parks because its pinned child hash was revoked shows "This recipe was revoked. Cancel, then start a new attempt from an approved version." The parent's recipe is still approved, so the text names the wrong recipe. The fix also loops: a new attempt pins the same revoked child, makes the claim effect, and parks again at `running`. That only stops once the parent is re-approved (l.255) | Give the child case its own text: "{child recipe} (pinned by {recipe}) was revoked. Re-approve {recipe} to pin the latest approved child, then cancel and start a new attempt." Its preview should show the child pin change |
| K5 | LOW | Voice / IA / A11y | see each item | (a) Layout 4 l.632 still says "Accept to fire verify -> done with no effects, or reject", which contradicts row l.224, and golden files pin both. (b) Layout 3 l.625 drops "Grant tokens at a terminal, or cancel." Layout 1 (l.610: `cancel, takeover`) and layout 2 (l.620) break the l.656 order `answer, complete, handback, takeover, cancel`. (c) Layout 4 has no answer line, and `--accept` isn't marked `(at a terminal)`. (d) The P5 signal l.1282 still says `0m 40s`, but layout 5 uses `40s`. (e) §4.2 l.271 gives `SND-RECIPE-009` for every expired challenge. §8 splits this into 009 (recipe) and `SND-WF-013` (workflow). (f) The revoke preview (l.283) doesn't list the takeovers the revoke ends (l.331). (g) The notifications path is `$AW_SINDRI_HOME/notifications.jsonl` at l.589 and `$AW_STATE_DIR/sindri/notifications.jsonl` at l.1088. The P6 signal checks "the notifications file". (h) The drift text l.221 renders `sindri recipe approve {recipe}`. `approve` takes `<file\|id@hash>`, so `{recipe}` should render as `id@hash12`. (i) `--reject` "treat it as a refusal" (l.224) still doesn't say whether that re-runs the Step or parks | Fix inline |

### Information architecture (4/5)
- Evidence: `SND-WF-011` and `SND-WF-013` complete the catalogue (l.717, l.719). The template variables now cover every §3.8 row (l.205–207).
- Evidence: the revoked-child park reuses the parent-revoke text (K4). `{recipe}` renders as a bare id in a command that needs a hash (K5 h), and the notifications file has two paths (K5 g).
- Recommended changes: K4, then K5 (e, g, h).

### Interaction states (4/5)
- Evidence: every §3.8 row has a resume action in §5.8, and golden files cover each row (l.657–658).
- Evidence: a drift recorded on a parked frame doesn't show (K3). Layout 4 contradicts its row (K5 a).
- Recommended changes: K3 (a layout line), K5 (a, b, c).

### User flow completeness (4/5)
- Evidence: J1 is closed. Resumes commit under the answer's key and re-run the guards fresh (l.503–504), and the wait text now says what to do (l.218). Forwarding scope is limited to answers (l.481).
- Evidence: a dispatch refusal for drift loses the effect, and two drift parks collide (K1). The wait park's answer line suggests the wrong flag (K2).
- Recommended changes: K1 (one absorption rule plus `reemit` on the drift resume), K2 (split one rule line).

### Accessibility (5/5)
- Evidence: unchanged and sound. ASCII, `--json`, plain-text previews and typed challenges. Only K5 (c) remains.

### Brand/voice consistency (N/A — missing .impeccable.md)
- Unscored: the revision-5 rows follow the §3.7 mood rule. Layout 4 keeps the old over-promising text (K5 a).

## Verdict
No HIGH remains. J1, which held pass 4 at NEEDS_WORK, is resolved, and every dimension is at 4 or above. The four MEDIUMs are each a row or a sentence: re-emitting an effect after a drift refusal (K1), the wait-park answer line (K2), showing a recorded drift (K3) and the revoked-child text (K4). Fix them before implementation, but they don't block approval.
verdict: PASS
