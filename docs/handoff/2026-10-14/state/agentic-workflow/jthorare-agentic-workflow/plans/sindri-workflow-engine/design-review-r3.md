# Plan Design Review (pass 3, final) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 3)
**Reviewed:** 2026-10-10 · slim · pass 3 of 3

The scope is the same as before: the `recipe`, `workflow` and `outbox` verbs, the §3.8 engine texts, the §4.2 challenge and previews, the §6 layouts, and what the later UI needs. This pass reports only (1) earlier design findings that are still open and (2) new problems in revision 3's surfaces (§3.8, §4.2, §6). Line numbers refer to revision 3.

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✗ (the spec is the plan) |
| design-brief.md | ✗ |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ |

Brand/voice is N/A because `.impeccable.md` is missing.

## Pass-2 findings: status
| Pass-2 | Status |
|---|---|
| N1 escalated per way in | Resolved for the rows (l.215–217). The timeout and child texts give no instruction (G4) |
| N2 budget park | Resolved (W7, layout 3) |
| N3 answer destination | Resolved (§3.4 `fact:answer`, l.164) |
| N4 layout 1 vs §5.6 | Replaced. The new layout 1 contradicts W5 and layout 2 (G3) |
| N5 missing layouts | Resolved for escalated (gate), taken-over and terminal. The event-wait and timeout-escalation layouts are still missing (LOW, G8) |
| N6 write-verb output | Resolved (§6.3 "Prints" column) |
| N7 eval and expiry codes | Resolved (SND-RECIPE-008/009) |
| N8 revoke preview | Resolved (l.261) |
| N9 child address | Resolved (l.428–429) |
| N10 SND-WF-010 text | Resolved in §3.8 (l.213). Layout 1 reintroduces the problem (G3) |
| N11 accept preview | Resolved (l.215, layout 4, l.481) |
| N12 empty states | Partly resolved. `recipe list` and `--needs-me` are covered; `events`, `outbox list` and eval-with-no-evals are not (LOW, G8) |
| N13 hash forms | Resolved (§3.1: shown as 12 hex characters, accepted as a prefix of 8 or more) |
| N14 takeover exits | Resolved for parks. It is still undecided for `escalated` (LOW, G8) |
| N15 engine variables | Partly resolved. The list at l.203 leaves out `{after}`, `{to}`, `{effects}` and `{block}`, which the §3.8 texts use (LOW, G8) |

## Ratings
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 4/5 | Two different things are both called "nonce", with different lifetimes and different expiry codes (G6) |
| Interaction states | 4/5 | Layout 1 is a golden layout that shows an impossible state (G3) |
| User flow completeness | 3/5 | Two park reasons, `timeout` (the desk floor, so the most common park in v1) and a non-revoke `spec-conflict`, have no text and no way to answer them (G1, G2) |
| Accessibility | 5/5 | Only LOW items |
| Brand/voice consistency | N/A — missing .impeccable.md | (unscored) see G4 and G8 |

## Surface × state coverage (changed surfaces only)
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| `parked: timeout` | n/a | — | n/a | **No answer line, no defined answer flag: GAP** (G1) |
| `parked: spec-conflict` (dispatch refusal, system mismatch) | n/a | — | n/a | **No text, and no way to retry after a fix: GAP** (G2) |
| `parked` mirroring a child | n/a | — | n/a | Layout 1 lists `answer (forwarded)` for a child that only allows `cancel` (G3) |
| `escalated` (timeout, child) | n/a | — | n/a | Rows exist (l.216–217). There are no layouts and no instruction (G4) |
| `recipe approve` / `revoke` / grant / accept challenge | n/a | 004 / 009 ✓. 004's fix text misleads inside a harness terminal (G5) | n/a | l.252–262 ✓ |
| Write verbs with `--nonce` | n/a | SND-WF-009's fix says "re-run without `--nonce`" to people who never passed one (G6) | n/a | ✓ |
| `workflow list` | n/a | — | `nothing needs you` is also printed for `--item` and `--state` (G8) | l.471 ✓ |
| `workflow start` (existing terminal attempt) | n/a | — | n/a | Returns the finished instance with no `--attempt next` hint (G7) |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| All read verbs | n/a | `--json`, ASCII and one duration format ✓ | No colour ✓ | n/a |
| Challenge | TTY typed text ✓. The typed text for `--grant-tokens` isn't given (LOW, G8) | Plain-text preview ✓ | n/a | n/a |
| Avalonia (later) | N/A | `allowedCommands` per actor, and what accept fires ✓ (l.479–481) | N/A | N/A |

## Findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| G1 | HIGH | Flow | §6.4 l.534–538; W19 l.43; W20 l.44; P5 signal l.1034–1036 | The answer-line list covers `rounds`, `no-progress`, `budget`, `needs-answer` and child mirrors, but not `timeout`. Under W20, every desk task ends in a `timeout` park after `deskTimeout`, so this is the park v1 users will hit most. W19 says "its answer re-arms them fresh", but no flag is named: `--resume` is defined only for `escalated` (l.216), and `--text` is for questions. The P5 signal stops at `parked: timeout`, with no next step | Add a `timeout` answer line: `answer --resume` (human) re-arms the full timeout and pops the frame. List `--resume` in the parked row's exits. Add a layout, and extend the P5 signal to resume and then complete |
| G2 | HIGH | Flow / Voice | §3.8 l.212, l.214; §5.4 l.363–364; §5.3 l.336 | `spec-conflict` has three ways in: a revoke or drift (row l.214), a dispatch refusal (`outbox.refused`) and a mismatch on a system trigger. Only the first has a text. The other two get the generic "Parked ({reason}): {question}." with no question and no answer line. Their exits (answer, takeover, cancel) don't include re-trying the refused effect after the profile allowlist is fixed. The person must cancel the item and start over, and nothing tells them the cause was the effect allowlist rather than the recipe | Add a row for "spec-conflict from a dispatch refusal": "Effect {kind} was refused at dispatch ({code}). Fix the profile, then `answer --resume` to re-emit it, or cancel." `--resume` re-emits with `try + 1`. Give the system-mismatch way in its own text, which can stay cancel-only |
| G3 | MEDIUM | States | §6.4 layout 1 l.493–498 vs layout 2 l.500–508, W5 l.29, §3.8 l.213, §5.7 l.413 | Layout 1 says `step.produce` has no runtime, but layout 2 runs `produce` as a desk task. It shows the parent as `parked: needs-answer (child)` with `commands answer (forwarded), cancel, takeover`, and the answer-line rule gives `--text` for `needs-answer`. Under §3.8 an SND-WF-010 park allows only `cancel`, so every forwarded answer is refused. The underlying gap: a mirroring parent shows the generic parked exits, not the child's | A mirroring frame copies the child's reason, its `next` text verbatim, and its `allowedCommands`, with `(forwarded)` added. Change layout 1 to a block that really isn't `deskCapable` (or to `resource: heavy`), and show `commands cancel` |
| G4 | MEDIUM | Flow / Voice | §3.8 l.216–217; §3.7 l.205 | The new timeout and child escalation texts ("Escalated: {state} timed out after {after}.", "Escalated in {child}.") describe the state but don't say what to do. That breaks the §3.7 rule that human-action states are instructions. The child row forwards `--accept`, `--reject` and `--resume`, but the parent's commands don't say which of them the child accepts: a timeout escalation refuses `--accept`, and a gate escalation refuses `--resume` | Timeout: "… Run `sindri workflow answer {instance} --resume`, or cancel." Child: copy the child's text and its filtered commands (the same rule as G3). Add both layouts to the golden list |
| G5 | MEDIUM | Flow | §6.2 l.449; §8 l.580, l.593 | Any harness marker makes the CLI `session`, and `CURSOR_TRACE_ID` and `CLAUDECODE` are set in IDE terminals and in Claude Code's `!` shell. A human in one of those terminals who runs `approve`, `complete` or `answer --grant` gets SND-RECIPE-004 or SND-WF-007, whose fix is "Run it yourself in a terminal", which is what they just did. The fix is right, but the text points them the wrong way | The refusal names the marker it found: `SND-WF-007 refused for session actor (CURSOR_TRACE_ID is set) (fix: run from a terminal outside the IDE or agent harness)`. The same applies to SND-RECIPE-004 |
| G6 | MEDIUM | IA / Voice | §4.2 l.243–249; §5.4 l.358–359; written answer l.863; §6.3 l.478; §8 l.595 | The challenge nonce (5 min, expiry SND-RECIPE-009) and the command nonce (10 min, SND-WF-009) share one name and one flag, and the written answer S2-L-a says 10 min for both. When the instance moves while someone is reading a preview, they get SND-WF-009, "Re-run without `--nonce`", although the CLI fetched the nonce for them | Call them a "challenge" and a "nonce", and give one lifetime for each in one place. SND-WF-009 for a version change says what moved: `instance changed (v7 -> v8); re-run to see the new preview` |
| G7 | LOW | Flow | §6.1 l.432–433 | `start` without `--attempt` returns the existing latest attempt even when it's `cancelled` or `done`, and prints its header. Someone who meant to start again sees a finished instance and no hint | When the returned instance is terminal, add `already done; use --attempt next to start a new attempt` |
| G8 | LOW | States / Voice | l.203, l.213, l.215–216; l.471; §4.2 l.244; N5/N12/N14 | `{after}`, `{to}`, `{effects}` and `{block}` aren't in the engine-variable list. The `nothing needs you` empty text also appears for `--item` and `--state` (use `no instances match`). `events`, `outbox list` and eval-with-no-evals have no empty lines. There's no event-wait layout. `takeover` from `escalated` is undecided. The typed text for `--grant-tokens` isn't given. The header's `since hh:mm` has no date once the age is a day or more. In `--needs-me`, a parent mirror and its child show up as two Decisions for one choice | Fix inline |

### Information architecture (4/5)
- Evidence: the addressing (l.426–431), the 12/8 hash rule (§3.1) and the actor table (§6.2) close N9 and N13.
- Evidence: two different nonces under one name (G6).
- Recommended changes: G6.

### Interaction states (4/5)
- Evidence: there are six layouts with a fixed header (l.489–532), so N5 is mostly closed.
- Evidence: layout 1 contradicts W5 and layout 2, and mirrored exits are generic (G3). The escalation layouts are missing (G4).
- Recommended changes: G3, G4.

### User flow completeness (3/5)
- Evidence: grants (W7), the accept preview (l.215, l.481), the revoke preview (l.261) and the desk `complete` line (layout 2) make the main paths complete.
- Evidence: `timeout` parks, the default end of every desk task, have no answer (G1). Dispatch-refusal `spec-conflict` parks have no text and no way to retry (G2). Harness-terminal users are misdirected (G5).
- Recommended changes: G1 and G2 (one §3.8 row and one §6.4 line each), then G5.

### Accessibility (5/5)
- Evidence: `--json` on every read verb, ASCII, the plain-text challenge preview and `allowedCommands` for the UI. Only LOW items (G8).

### Brand/voice consistency (N/A — missing .impeccable.md)
- Unscored: the new escalation texts break the §3.7 mood rule (G4), and the variable list is incomplete (G8).

## Verdict
User flow stays at 3/5 because of two HIGH dead ends in §3.8 and §6.4. A `timeout` park, which every desk task reaches under W20, has no defined answer (G1). A `spec-conflict` park from a dispatch refusal has no text and no way to retry (G2). Each needs one table row and one answer line. Everything else from passes 1 and 2 is resolved or LOW.
verdict: NEEDS_WORK
