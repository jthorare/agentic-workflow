# Plan Design Review (pass 2) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 2)
**Reviewed:** 2026-10-10 · slim · pass 2 of at most 3

Scope is the same as pass 1: the `recipe`, `workflow` and `outbox` verbs, their output and error lines, the `next` texts, the approval preview, and what the later Avalonia UI needs from `GET /workflow/instances/{address}`. Line numbers refer to revision 2.

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✗ (the spec is the plan) |
| design-brief.md | ✗ |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ |

Brand/voice is N/A because `.impeccable.md` is missing. As in pass 1, `planning/API_CONTRACT.md` is the reference for the CLI surfaces.

## Pass-1 findings: status
| Pass-1 | Status | Where |
|---|---|---|
| F1 engine-owned states | **Partly resolved.** §3.8 adds a table with entries, texts and exits. But `escalated` is described only as entered from a gate, and timeouts and children also enter it (N1) | §3.8 l.212–222 |
| F2 answer flow | Resolved for the CLI syntax (l.415) and for showing the question (l.452–453). Where the answer goes is still undefined (N3) | §6.2, §6.3 |
| F3 approval content | Resolved | §4.2 l.248–261, `recipe show` l.408 |
| F4 `status` layout | Resolved for the three examples. Other states are missing, and one example contradicts §5.6 (N4, N5) | §6.3 |
| F5 misleading `needs-answer` for SND-WF-010 | Resolved in W5's wording. The generic §3.8 park text still says "Answer with …" (N10, LOW). It is unreachable in v1, because every Step resolves to the desk runtime (W20) | W5 l.28 |
| F6 revoke | **Partly resolved.** The `spec-conflict` text names the exits (l.221). Revoke still doesn't show the affected instances or the cascaded recipes before it acts (N8) | §3.8, §6.2 l.410 |
| F7 namespaces and addressing | Resolved | §6.1, §6.2 |
| F8 empty and not-found states | **Partly resolved.** `recipe list` has an empty state. There's no terminal `status` layout, and no empty state for `workflow list`, `events`, `outbox list`, or `recipe eval` with no evals (N5, N12) | l.411 |
| F9 UI view model | Resolved | l.426–427 |
| F10 `--json` and exit codes | Resolved for read verbs. What write verbs print on success is still open (N6) | l.422–425 |
| F11 durations and arrows | Resolved | l.456 |
| F12 `next` mood and `{user}` | Resolved | §3.7 l.207–210 |
| F13 rule line and Fix column | Resolved. The success line for `validate` is still "prints its hash" (LOW) | §9.1, §8 |
| F14 `--version` | Resolved | l.397 |
| F15 `alreadySatisfied` | Resolved | §12 l.620, l.630 |

## Ratings
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 4/5 | Child address drops the parent entry that `InstanceId` carries (N9) |
| Interaction states | 4/5 | §6.3 layout 1 shows an impossible state, and there are no layouts for escalated, taken-over, terminal or event waits (N4, N5) |
| User flow completeness | 3/5 | `escalated` from a timeout or a child has no text and no meaning for `--accept` (N1). A `budget` park can't be answered usefully (N2) |
| Accessibility | 5/5 | Only LOW items |
| Brand/voice consistency | N/A — missing .impeccable.md | (unscored) engine texts use variables outside the §3.7 set (N15) |

## Surface × state coverage (new or changed surfaces)
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| `recipe show` / approve preview | n/a | l.493 SND-RECIPE-002 | n/a | l.250–257. **Failing or missing evals: GAP** (N7) |
| `recipe approve` | n/a | l.260 SND-RECIPE-004. Challenge expiry reuses 004: GAP (LOW) | n/a | l.258, Phase 2 signal |
| `recipe revoke` | n/a | l.493 | n/a | **Preview of affected instances and cascade: GAP** (N8) |
| `workflow start` | n/a | §8 | n/a | Returns the existing instance on a repeat (l.398). **Output line: GAP** (N6) |
| `workflow status` | n/a | SND-WF-001, 006 | Terminal: GAP (N5) | §6.3 has 3 layouts. Escalated, taken-over and event-wait layouts: GAP. Layout 1 is inconsistent (N4) |
| `workflow list [--needs-me]` | n/a | — | GAP (N12) | Layout and columns: GAP (N12) |
| `workflow answer` | n/a | SND-WF-003, 007 | n/a | Syntax at l.415. **Effect of `--text` on the instance: GAP** (N3). On a `budget` park: dead end (N2) |
| `answer --accept/--reject` | n/a | SND-WF-007 | n/a | §3.8 l.217 for a gate. **Timeout or child escalation: GAP** (N1). No preview of what accept fires (N11) |
| `workflow complete` | n/a | SND-WF-005. Unknown outcome: GAP (LOW) | n/a | **Output and exit when the result is refused, re-run or parked: GAP** (N6) |
| `workflow cancel` / `takeover` / `handback` | n/a | SND-WF-002, 003 | n/a | Output: GAP (N6). Phase 5 shows `timeout paused (0m 40s left)` |
| `workflow events [--follow]` | streaming, l.419 | — | GAP (N12) | Phase 4 and 7 signals |
| `outbox list` | n/a | — | GAP (N12) | Phase 6 signal |
| notifications (`notify.desk`) | n/a | Budget exceeded: GAP (LOW) | n/a | Only `takeoverIdle`'s text is given (l.360) |
| `GET /workflow/instances/{address}` | n/a | §8 | n/a | l.426–427 ✓ |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| All read verbs | n/a | `--json` everywhere (l.424) ✓; ASCII `->` ✓; one duration format ✓ | No colour named ✓ | n/a |
| `recipe approve` | TTY typed text `approve <id> <version>` ✓ | Preview is plain text ✓ | n/a | n/a |
| `status` layouts | n/a | Aligned label columns read linearly ✓. The short hash `@3f1c09` vs `id@sha256:…` (N13, LOW) | n/a | n/a |
| Avalonia UI (later) | N/A | `allowedCommands` per actor class gives the needed data ✓ | N/A | N/A |

## Findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| N1 | HIGH | Flow / IA | §3.8 l.217; §3.4 l.161; §5.6 l.383; §12 l.610, l.612 | §3.8 says `escalated` is entered only by "an uncertain or below-threshold top-rung gate", and its text renders `{gate} scored {score}, below {threshold}`. But `timeout: { then: escalate }` also enters it, and `workflow.item` uses that on `triaged` and `startable`. A child's escalation propagates too. For those entries the text has no gate or score to show, and `--accept` has no "escalated transition" to apply. A person who opens an item escalated by a 7-day router timeout gets a broken sentence and two options with no defined meaning. The parent's answer is forwarded to the child for a park (l.384), but that rule isn't stated for an escalation | Add rows to §3.8: entry by timeout (text "Escalated: no {event} after {after}." with exits `answer` to re-arm the wait, or `cancel`, and no `--accept`) and entry by a child ("Escalated in {child}.", with the answer forwarded as for parks). `allowedCommands` then differs by entry kind. A `gate.uncertain` with no score renders "returned uncertain" |
| N2 | HIGH | Flow | §3.7 l.205; §5.4 l.345; §5.5 l.361; W7 l.30 | A `budget` park can't be answered usefully. `grant` only applies to `rounds` and `no-progress`, and an answer only re-arms the timeout. `spent` is unchanged, so the next Step or `gate.budget.*` check parks again. This is the dead-end loop F5 described in pass 1, now on the most likely park for `workflow.step` (400k tokens). Neither the §3.8 text nor `allowedCommands` says that cancel is the only exit | Either allow `--grant-tokens <n>` (human-terminal, capped, recorded the way `grant` is), or give `budget` parks their own text: "Budget spent ({spent} of {budget}). Cancel, or start a new attempt with `--attempt next`.", and leave `answer` out of `allowedCommands` |
| N3 | MEDIUM | Flow | §3.4 l.169; §6.2 l.415; §6.3 l.453 | Where `answer --text/--file` goes is undefined: is it a fact (`fact:answer`), findings for the re-run Step, or only a ledger note? The rounds layout invites `--text "..."` without `--grant`, but at the cap a re-entered Step parks again right away. Also, `waitFor: { answer: <questionId> }` has no field for the question text, so `status`'s `question` line has no source for an answer wait | State that the answer is stored as `fact:answer` (or as findings) and say which Step input receives it. Add `question: <text>` beside `answer:` in the wait form. Without a grant, the rounds layout should only show the commands that change something |
| N4 | MEDIUM | States | §6.3 l.434–439 vs §5.6 l.381, §12 l.617–618, l.656 | Layout 1 shows the parent `running` while its child is `parked (needs-answer)`. Under `onParked: propagate` (§12) the parent parks too, so the layout is impossible. The example's `next` ("T-3 is running; see its Step…") isn't `workflow.item`'s text ("Waiting for {child}."), and `budget tokens 0 / 400k` belongs to `workflow.step`, not `workflow.item`. Golden files pin these layouts (l.456), so the error would be fixed into the tests | Rebuild layout 1 from the real recipe: either the child is `produce` with an open desk task, or the parent shows `parked: needs-answer (child …)`. Generate the golden examples from an eval run, not by hand |
| N5 | MEDIUM | States | §6.3; Phase 4 l.890, Phase 5 l.906, Phase 7 l.944; §3.8 l.216 | Layouts are missing for `escalated`, `taken-over`, a terminal instance and an event or resource wait. The parked layout drops the `next` and `commands` lines that the other two layouts have, and swaps in `question`/`answer`. That disagrees with §3.8's single parked `next` sentence. The header state is written three ways: `parked: rounds` (§6.3), `parked (no-progress, child …)` (Phase 7) and `done (resolved)` (Phase 4) | Fix one header form (`<state>[: <reason>][ (child <addr>)]`, terminal `done: resolved at <time>`). Keep `next` and `commands` on every layout. Add the four missing layouts to §6.3 and the golden list |
| N6 | MEDIUM | States | §6.2 l.407–418, l.422 | What the write verbs print on success isn't defined: `start` (the address? the version?), `complete`, `answer`, `cancel`, `takeover`, `handback`, `register`. Nor is what `complete` prints, or which exit code it returns, when the result is refused and the Step re-runs or parks (§5.4). That is an engine decision, not `SND-WF-004` | One line per write verb: `<address> <from> -> <to> v<n>`. When `complete` causes a refusal, it prints the refusal reasons and the new state and exits 0, because the report was accepted. Add these lines to the golden files |
| N7 | MEDIUM | Flow | §4.2 l.257; Phase 2 l.848 | The preview shows "the eval result", but nothing says whether a failing or missing eval blocks approval, or which code it returns. A 5-minute challenge that expires comes back as `SND-RECIPE-004` ("needs the typed challenge at an interactive terminal"), which misdirects someone who was simply reading slowly | Failing or missing evals refuse with a new `SND-RECIPE-008` ("evals failed: <case> at line n"). An expired challenge gets its own message ("challenge expired; run approve again") |
| N8 | MEDIUM | Flow | §6.2 l.410; W11 l.34 | Revoke (still open from pass-1 F6) parks every running instance on `spec-conflict` and cascades to generated recipes. It needs a terminal but shows no preview of what it will touch | Reuse the challenge. The preview lists the running instances that will park and the generated recipes that will be revoked, and the person types `revoke <id> <version>` |
| N9 | MEDIUM | IA | §5.1 l.284 vs §6.1 l.395 | A child's `InstanceId` carries the parent entry (`{parent}/{child recipe}.{parent entry}`), but its address doesn't (`T-3/item/1/step`). If a parent enters a child wait twice, two children share one address, and nothing says which one the address resolves to | The address resolves to the child of the parent's current entry. Older children are reached as `…/step.<entry>`. Print `.entry` in `status` and `events` when there is more than one |
| N10 | LOW | Voice / Flow | W5 l.28 vs §3.8 l.216 | W5 says the `SND-WF-010` park's `next` names only a build. §3.8's generic parked text says "Answer with …" | Add a row for `needs-answer` with SND-WF-010 to §3.8, or note that it can't happen while the desk runtime is the fallback |
| N11 | MEDIUM | Flow | §3.8 l.217 | `--accept` applies the escalated transition, which may carry effects and capability uses, but `status` for `escalated` doesn't show which transition will run or which effects it fires | The escalated layout shows `accept -> <to>, effects: <kinds>` and the gate's reasons |
| N12 | LOW | States | §6.2 l.414, l.419–420; §9.2 | No layout or columns for `workflow list`. No empty state for `workflow list`, `--needs-me` ("nothing needs you"), `events` or `outbox list`. `recipe eval` on a recipe with no evals isn't covered | One line each, exit 0. Add them to the §11 golden list |
| N13 | LOW | IA | §6.3 l.434 vs §6.2 l.408–410, Phase 2 l.853 | `status` shows a 6-hex short hash, but `show` and `revoke` take `id@hash`, and `list` prints `sha256:…`. Whether a short prefix is accepted isn't stated | Accept a unique prefix of at least 6 hex characters, and print the same form everywhere |
| N14 | LOW | IA | §3.8 l.216–217 vs W16 l.39 | The parked row's "Ways out" leaves out `takeover`, which W16 makes the main path. Takeover isn't defined for `escalated` | Add `takeover` to the parked exits and decide for escalated. `allowedCommands` follows |
| N15 | LOW | Voice (unscored) | §3.8 vs §3.7 l.207 | The engine texts use `{reason}`, `{question}`, `{gate}`, `{score}`, `{threshold}` and `{since}`, which aren't in the §3.7 variable set. `{instance}` has to render as the address. The desk runtime appends the `complete` command line (layout 2), but §2 l.99 doesn't say so | List the engine-only variables and say `{instance}` renders as the address. State in §2 that the desk runtime adds the `complete` hint built from the descriptor's outcomes and outputs |

### Information architecture (4/5)
- Evidence: one `workflow` group with addressing (l.395–399) and `allowedCommands` per actor (l.426) resolve pass-1 F7 and F9.
- Evidence: child addressing loses the entry that the id has (N9), and the hash forms differ (N13).
- Recommended changes: N9, N13.

### Interaction states (4/5)
- Evidence: the read verbs now have `--json` and exit codes (l.422–425), plus three layouts (§6.3).
- Evidence: layout 1 contradicts §5.6 (N4). Four state layouts are missing and the header forms differ (N5). Write verbs print nothing defined (N6).
- Recommended changes: N4, N5, N6.

### User flow completeness (3/5)
- Evidence: the approval preview (§4.2) and the answer syntax (l.415) fix pass-1 F2 and F3.
- Evidence: escalation by timeout or by a child, which the first recipe has, has no text and no `--accept` meaning (N1). A budget park is a dead end for `answer` (N2). Where the answer goes is undefined (N3). Revoke has no preview (N8). Failing evals at approval are undefined (N7).
- Recommended changes: N1, N2 first, then N3, N7, N8, N11.

### Accessibility (5/5)
- Evidence: `--json` on every read verb, ASCII output, one duration format and a plain-text preview. Only N13 is LOW.

### Brand/voice consistency (N/A — missing .impeccable.md)
- Unscored: the §3.7 mood rule is applied consistently in §12. Engine variables and the desk hint aren't documented (N15).

## Verdict
Two new HIGH findings, neither structural, keep User flow at 3/5: `escalated` entered by a timeout or a child has no text or `--accept` meaning (N1), and a `budget` park has no useful answer (N2). Each is fixed by adding rows to §3.8. Most pass-1 findings are resolved; F1, F6 and F8 are partly resolved.
verdict: NEEDS_WORK
