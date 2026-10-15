# Plan Design Review (pass 4) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 4)
**Reviewed:** 2026-10-11 · slim · pass 4 of at most 5

Scope: (1) earlier design findings that are still open, and (2) new problems in revision 4's surfaces (the §3.8 rows, §5.8, the §6.3 verbs, the §6.4 layouts and §8). Resolved items and the §13 written answers are not raised again. Line numbers refer to revision 4.

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✗ (the spec is the plan) |
| design-brief.md | ✗ |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ |

Brand/voice is N/A because `.impeccable.md` is missing.

## Pass-3 findings: status
| Pass-3 | Status |
|---|---|
| G1 `timeout` park has no answer | Resolved (l.217, §5.8 `rearm` l.466, answer line l.607) |
| G2 dispatch `spec-conflict` has no text or retry | Resolved (l.220, `reemit` l.468). The text has a new problem (J4) |
| G3 layout 1 shows an impossible state | Resolved. Layout 1 is now a forwarded `no-progress` park (l.564–570). The forwarding scope is ambiguous (J3) |
| G4 escalation texts aren't instructions | Timeout: resolved (l.223). Child: partly resolved. The text still says "Answer here" without saying which answer, and `{question}` is undefined when the child escalated on a gate (J2) |
| G5 harness-terminal users are sent the wrong way | Resolved (l.259, l.517–520, SND-WF-007 l.669) |
| G6 two things called "nonce" | Resolved (challenge 5 min l.252, nonce 10 min l.381, SND-WF-009 l.671) |
| G7 `start` on a finished attempt | Resolved (l.533) |
| G8 LOW items | Mostly resolved (variables l.205–206, empty states l.554, dedupe l.553, takeover from escalated l.225, grant-tokens text l.255). Still open: `since hh:mm` has no date once the age is a day or more, and there's no event-wait layout (J6) |

## Ratings
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 4/5 | §3.8 texts use variables that aren't in the list, and the workflow challenge expiry reuses a `SND-RECIPE` code (J6) |
| Interaction states | 4/5 | `escalated (child)` has no defined text for a gate escalation and no layout (J2) |
| User flow completeness | 3/5 | A `reevaluate` resume replays the stored trigger. Under W12 and §5.3, that returns the recorded refusal or reuses the saved gate answers, so a `--grant-tokens` or `--text` answer can park again right away (J1) |
| Accessibility | 5/5 | Only LOW items |
| Brand/voice consistency | N/A — missing .impeccable.md | (unscored) see J5 and J6 |

## Surface × state coverage (revision-4 surfaces only)
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| `parked: budget` from `gate.budget.*`, then `--grant-tokens` | n/a | — | n/a | **The resume may replay the recorded refusal: GAP** (J1) |
| `parked: needs-answer` from a wait refusal, then `--text` | n/a | — | n/a | **Same replay. The text doesn't say what to change: GAP** (J1) |
| `escalated (child)` | n/a | — | n/a | Row l.224. `{question}` is undefined for a gate escalation. No layout (J2) |
| Parent mirroring a child: `cancel`, `takeover` | n/a | — | n/a | §5.7 l.451 forwards every command, but layout 1 implies `cancel` acts on the parent (J3) |
| `parked: spec-conflict` (dispatch) | n/a | The allowlist refusal code isn't in §8. Precedence against a revoke park is undefined (J4) | n/a | l.220, `reemit` ✓ |
| `escalated (gate)`, then `--accept` | n/a | Later gates can still refuse. The text says "Accept to fire" (J5) | n/a | l.222 ✓ |
| `parked: timeout`, `SND-WF-010`, drift | n/a | — | n/a | Rows and answer lines ✓. No golden layout drawn (J6) |
| `taken-over` | n/a | — | n/a | Layout 5 ✓. `0m 40s` breaks the duration format (J6) |
| Empty states | n/a | — | l.554 ✓ | — |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| All read verbs | n/a | `--json`, ASCII and one duration format (with one exception, J6) ✓ | No colour ✓ | n/a |
| Challenge | TTY typed text, including `grant-tokens` (l.255) ✓ | Plain-text preview ✓ | n/a | n/a |
| Layout 4 (`escalated: gate`) | n/a | `commands` lists `answer --accept` without `(at a terminal)` and without an answer line (J6) | n/a | n/a |
| Avalonia (later) | N/A | `allowedCommands` with actor classes ✓ (l.545) | N/A | N/A |

## Findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| J1 | HIGH | Flow | §5.8 l.463, l.465, l.467; §5.3 l.352–353, l.364–366; W12 l.36; W14 l.38; §3.8 l.215–216 | `reevaluate` runs "the withheld transition with its stored trigger". The refusal already consumed that trigger's key (W14), so `Commit` step 2 returns the recorded refusal. The saved gate answers are also keyed by trigger and block (l.364), so even a fresh decision would reuse the refusing `gate.budget.*` answer. The result: a human types a `--grant-tokens` challenge, or answers `--text` on a wait refusal, and the instance parks again with nothing changed. For a wait refusal, the text "Answer with --text." also suggests that the answer text fixes things. Usually it doesn't, because the gates don't read `fact:answer` | Say in §5.8 that a resume commits under the **answer's** key and re-runs every gate and evidence entry at the current head. Saved answers are reused only for the l.365 version-conflict retries of the same trigger. Give the wait-refusal park its own text: "{gate} refused {event}: {reasons}. Fix it, then answer with --text to re-check at the current head, or cancel." Add an eval: grant tokens on a `gate.budget` park, and the transition fires |
| J2 | MEDIUM | States / Flow | §3.8 l.224; §5.7 l.450–451; §6.4 | `escalated (child)` reads "Escalated in {child}: {question}. Answer here; it goes to {child}." When the child escalated on a gate there's no `{question}`. The child's real choice is accept or reject, and "Answer here" doesn't say that. Nothing says that the parent's `allowedCommands` copy the child's, or what the `accept` preview shows for a parent address (the child's `{to}` and `{effects}`). There is no layout | A mirroring frame renders the child's own `next` text, prefixed with "In {child}:", and copies the child's `allowedCommands` marked `(forwarded)`, which is the rule layout 1 already follows for parks. The accept preview shows the child's transition. Add an `escalated (child)` golden layout |
| J3 | MEDIUM | Flow | §5.7 l.451; layout 1 l.569 | §5.7 forwards "a command to the parent while the frame mirrors a child". Read literally, that includes `cancel` and `takeover`. Layout 1 lists `answer (forwarded), cancel, takeover`, which implies that `cancel` cancels the parent (and so every child, l.452). If `cancel` is forwarded, cancelling `T-3/item/1` cancels only the child, and the item recipe moves on with outcome `cancelled`. That isn't what the person asked for | Limit forwarding to the `answer` variants. `cancel`, `takeover` and `handback` act on the address given. Say so in the l.451 row |
| J4 | MEDIUM | Flow / IA | §3.8 l.220; §8; P6 signal 2 l.1206–1208 | The dispatch text says "Fix the approval or the allowlist" and renders `{code}`, but §8 has no code for "this effect isn't allowlisted in `workflow.effects`". So the person can't tell which of the two fixes applies, or that the allowlist is a profile key that needs `sindri profile approve`. A revoke in the dispatch window produces two parks: the revoke park, then `outbox.refused` (SND-RECIPE-005) a few seconds later. The spec doesn't say which one wins. If the dispatch park is pushed on top, its "resume to send it again" fails with SND-RECIPE-005 | Add `SND-WF-011 forbidden: effect not allowlisted (fix: add it to workflow.effects, then sindri profile approve)`. Pick the text by code: approval codes point to `recipe approve`, and SND-WF-011 points to the profile. Rule that an `outbox.refused` for a revoked recipe is absorbed by the revoke park, which is what P6 signal 2 already expects |
| J5 | MEDIUM | Voice / Flow | §3.8 l.222; layout 4 l.591; §4.2 l.273 | "Accept to fire {to} with {effects}" and the "what will run next" preview promise a firing. But `--accept` overrides only that gate and then runs the remaining gates and evidence at the current head, and any of those can still refuse. Someone who typed a challenge gets a refusal the preview didn't mention. The text also doesn't say what `reject` does | When later guards remain: "Accept to override {gate}; {n} more checks then run before {to}." Otherwise keep "Accept to fire …". Add "Reject re-runs the Step with the findings" (or "parks", following §5.5). The accept preview lists the remaining guards |
| J6 | LOW | States / Voice / IA | see each item | (a) The §3.8 texts use `{effect}`, `{code}` and `{recipe}` (l.219–220), which aren't in the l.205 list. `{marker}` is listed but only appears in error text. (b) Layout 3 (l.584) drops row 215's "Grant tokens at a terminal, or cancel.", so the golden file and the template disagree. The `commands` order differs between layouts (l.569 vs l.586). (c) Layout 4 has no answer line and doesn't mark `--accept` as `(at a terminal)`. (d) `paused timeout 0m 40s left` (l.598, P5 signal l.1180) breaks the `1d 22h 48m` format (l.614). (e) The header's `since hh:mm` has no date once the age is a day or more (from G8). (f) "Golden files pin every layout" (l.614), but only six are drawn. The `timeout`, `needs-answer`, three `spec-conflict`, `escalated (timeout/child)`, event-wait and takeover-over-a-park layouts aren't. (g) A workflow challenge expiry (grant or accept) is `SND-RECIPE-009`, so an area code is used outside its area. SND-WF-003's §8 fix is generic, while l.473 says that its fix names the right answer. (h) A revoke ends a running takeover (l.320), but the revoke preview (l.272) doesn't list the instances taken over. A drift re-approval doesn't list the instances still waiting for `--resume`. (i) The P5 signal's bare `T-2` (l.1182) resolves to the latest attempt. If the second instance is `T-2` attempt 2, the command targets the wrong one | Fix inline. For (f), list the golden layouts by name in §6.4. For (h), have `approve` print `N instances parked on drift; resume each with sindri workflow answer <addr> --resume` |

### Information architecture (4/5)
- Evidence: the challenge and nonce now have separate names and lifetimes (l.252, l.381), which closes G6. Addressing and hashes are unchanged and sound.
- Evidence: undeclared template variables, and `SND-RECIPE-009` used for workflow challenges (J6 a, g).
- Recommended changes: J6 (a, g). Catalogue the allowlist refusal (J4).

### Interaction states (4/5)
- Evidence: every park reason now has a §3.8 row and an answer-line rule (l.606–612), which closes G1 and G2.
- Evidence: `escalated (child)` has no defined text for gate escalations and no layout (J2). Several layouts are named but not drawn, and one drawn layout disagrees with its template (J6 b, f).
- Recommended changes: J2, then J6 (b, f).

### User flow completeness (3/5)
- Evidence: §5.8 gives every suspended frame a resume action, and the parked rows say what each answer does (l.214–225).
- Evidence: the `reevaluate` resumes replay a consumed trigger key and the saved gate answers, so the budget-grant and wait-refusal answers may park again at once (J1). The forwarding scope can turn a parent `cancel` into a child cancel (J3). Dispatch parks don't say which fix applies, and they collide with revoke parks (J4).
- Recommended changes: J1 (one §5.8 sentence plus one text), J3 (one table row), J4.

### Accessibility (5/5)
- Evidence: `--json`, ASCII, the plain-text preview and typed TTY text for every human-terminal action. Only LOW items (J6 c, d).

### Brand/voice consistency (N/A — missing .impeccable.md)
- Unscored: the accept text promises more than the engine does (J5). Otherwise the §3.7 mood rule holds in every revision-4 row.

## Verdict
User flow stays at 3/5 because of J1. As written, a `reevaluate` resume replays a trigger whose refusal is already recorded, and it reuses that trigger's saved gate answers. So a typed `--grant-tokens` on a `gate.budget` park, or a `--text` on a wait refusal, can re-park with nothing changed. One §5.8 sentence (resumes commit under the answer's key and re-run the guards fresh) and one text fix close it. Pass 3's G1–G7 are resolved. J2–J5 are one-row fixes.
verdict: NEEDS_WORK
