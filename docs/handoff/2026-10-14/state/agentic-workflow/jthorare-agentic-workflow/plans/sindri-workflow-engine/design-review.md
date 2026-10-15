# Plan Design Review — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (single-file spec; no plan dir)
**Reviewed:** 2026-10-10

Scope: the user-facing surfaces of the engine: the `sindri recipe …` and `sindri workflow …` verbs (plus `takeover`, `handback`, `ledger`, `outbox list`), their output and error lines, the `next` texts that `status` and `resume` show, and the later Avalonia UI, which is a pure client of the HTTP API. The engine internals (Commit, the wiring check, idempotency) are out of scope for this lens except where they decide what a human sees. Line numbers refer to the spec.

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✗ (the spec itself is the plan) |
| design-brief.md | ✗ |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ |

Brand/voice is N/A because `.impeccable.md` is missing. The repo's own CLI contract (`planning/API_CONTRACT.md`: plain-word states, `--json` on every read, exit 0/1/2, `SND-<AREA>-<NNN>` codes) and the `docs/sindri/errors.md` style (Code | Meaning | Fix) were used as the reference for the CLI surfaces instead. Voice observations are listed but not scored. The Avalonia UI isn't designed yet (it's later and a pure client), so its visual and a11y cells are N/A. Only what the API must give it is reviewed.

## Summary
The spec is strong where it is a machine contract. The closed `when` grammar, the closed outcome set (W6), the named validator rules with a one-line `RCP-…: <what> at <path>; <fix>` format (l.322), and fail-closed behaviour (W5) all give the human surfaces a sound base. The gaps are in what a person sees and does after something stops. The engine-owned states (`parked`, `escalated`, `cancelled`, `taken-over`) have no `next` text, no outcome mapping, and in the case of `escalated` no stated way out. `workflow status`, the most-read surface, has no defined layout; it appears only through scattered examples whose formats disagree. The answer flow doesn't say how a human sees the question or passes a `grant`. The approval prompt doesn't say what the approver is shown. The first real signal parks with `needs-answer` for a missing runtime that no answer can fix. All of these can be fixed by adding sections. None needs the design reworked.

## Ratings
Anchors per `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 3/5 | Engine-owned states have no outcome, `next` text or (for `escalated`) exit, so receipts and status can't place them (F1) |
| Interaction states | 3/5 | `workflow status` output and the empty/not-found states of every read verb are undefined (F4, F8) |
| User flow completeness | 3/5 | Answer flow (question display, `grant` syntax), approval review content, and the `needs-answer` park for an unfixable `SND-WF-010` (F2, F3, F5) |
| Accessibility | 4/5 | `--json` is promised only for `recipe validate`; `→` arrows and the `0:40` time in plain output (F10, F11) |
| Brand/voice consistency | N/A — missing .impeccable.md | (unscored) `next` texts mix imperative and descriptive mood, and one renders a literal `<user>` (F12) |

## Surface × state coverage
Most verbs are short and synchronous, so their Loading cell is `n/a (sync)`. That isn't a gap.

| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| `recipe validate` | n/a (sync) | l.322 (one line per failure, exit 1); l.468–470 example | n/a | l.470 ("exits 0 and prints its hash"); line format GAP |
| `recipe register` | n/a | l.308 SND-RECIPE-001 | n/a | l.197 ("reports `unapproved`"); output format GAP |
| `recipe approve` | n/a | l.311 SND-RECIPE-004, l.486–487 | n/a | l.485–486; **what the prompt shows before the typed confirmation: GAP** |
| `recipe revoke` | n/a | l.309 SND-RECIPE-002 | n/a | GAP (effect on running instances is l.30 but nothing tells the user how many instances it parks) |
| `recipe list` | n/a | GAP | GAP (no recipes registered) | l.486 (`workflow.step@sha256:… approved (desk)`) |
| `recipe eval` | GAP (many cases, no progress or summary line) | l.499–500 (diverging state, exit 1) | GAP (recipe with no `evals`) | l.499 (pass/fail per case) |
| `workflow start` | n/a | l.310, l.317 | n/a | GAP (prints the instance id? the version?) |
| `workflow status` | n/a | l.313 SND-WF-001 | GAP (item with no instance; terminal instance) | l.529–530, l.561 examples only; **layout GAP** |
| `workflow answer` (+ `grant`) | n/a | l.315 SND-WF-003 | GAP (answer when not parked) | GAP (flag syntax, what the question is, what changes) |
| `workflow cancel` | n/a | l.514–516 SND-WF-002 | n/a | l.516 (idempotent), l.562 (cascades); output GAP |
| `takeover` / `handback` | n/a | l.257 (timeout refused) | n/a | l.530 (`taken-over, timers paused (remaining 0:40)`) |
| `ledger --item` | n/a | GAP | GAP (no events) | l.513–514 |
| `outbox list` | n/a | GAP | GAP | l.545–546 |
| `next` texts (status/resume) | n/a | GAP (parked, escalated, refused) | GAP (terminal `next` optional, l.123) | l.394–429 for recipe states |
| Avalonia UI (later) | N/A — not designed yet | N/A | N/A | N/A; its API needs are F9 |

### Accessibility sub-table
CLI keyboard use and focus are those of a terminal, so they are n/a except for the typed confirmation.
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| `recipe validate` / `register` / `list` / `eval` | n/a | Plain-word states per API_CONTRACT ✓; `--json` only promised for `validate` (l.360) — GAP for the rest | No colour named; NO_COLOR handling GAP (LOW) | n/a |
| `recipe approve` (typed confirmation) | TTY input (l.198) ✓ | What is read out before confirming: GAP (F3) | n/a | n/a |
| `workflow status` / `ledger` / `outbox list` | n/a | `→` arrows (l.559) and `0:40` (l.530) in plain output — GAP (F11); `--json` GAP | n/a | n/a |
| `next` texts | n/a | One sentence per state ✓; literal `<user>` (l.399) — GAP | n/a | n/a |
| Avalonia UI (later) | N/A | N/A (A §17.21 phone-a11y rules apply when designed) | N/A | N/A |

## Detailed findings

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| F1 | HIGH | IA | §3.7 l.167–174, W6 l.25, §3.6 l.164 | Engine-owned states (`parked`, `escalated`, `cancelled`, `taken-over`) have no `next` text and no outcome mapping. Receipts count outcomes (W6), but `cancelled` and `escalated` map to none. `escalated` has no stated exit: it's unclear whether it's terminal or answerable. `{reason}` is a legal template variable (l.164), but recipe `next` texts only exist on recipe states, where `reason` is never set | Add a §3.7 table: each engine state → outcome (if terminal), the engine's `next` text (e.g. parked: "Parked ({reason}). Answer with `sindri workflow answer {item} …` or cancel."), and its exits. Say whether `escalated` is terminal (outcome `failed`?) or parked-like. Drop `{reason}` from recipe templates or define where it renders |
| F2 | HIGH | Flow | W7 l.26, §3.3 `answer` l.128, Phase 4 l.510 | Answer flow: a `wait` on `{ answer: <questionId> }` and a `needs-answer` park both need a human to see the question and answer it. The spec defines neither how the question text is shown (status? a questions list?) nor the CLI form of `answer` and `grant` (`--grant 2`? payload file?). The human can't tell what to type | Specify `sindri workflow answer <item> [--grant n] [--text … \| --file …]`, and that `status` prints the open question and the reason. Add the question text to `GetInstance` |
| F3 | HIGH | Flow | W9 l.28, §4 l.198, Phase 2 l.485 | `recipe approve` asks for typed confirmation but doesn't say what it shows first. Approval is the invariant-13 decision. Without a summary of the capability-bearing uses (blocks with capabilities, effect kinds, capability `with:` values) and the diff against the last approved version, it becomes a rubber stamp | Before the prompt, print id, version, hash, the capability-bearing uses grouped by `from → to`, effect kinds, and the diff from the previous approved version. Confirm by typing the recipe id or version, not an opaque string |
| F4 | HIGH | States | Phases 4, 5, 7 l.513, l.529–530, l.561; §9 l.360 | `workflow status` has no defined layout. It appears only as examples whose formats disagree (`parked (timeout)`, `taken-over, timers paused (remaining 0:40)`, `parked (needs-answer, child workflow.step/…)`). Golden files are planned (l.360) with no contract to pin them to | Add a "CLI output" section: the `status` fields in a fixed order (instance, recipe@version, state[, reason], loop n/cap, budget left, open question, `next`, allowed commands) and the `--json` shape. Pin the golden files to it |
| F5 | HIGH | Flow | W5 l.24, §10 l.382–385, Phase 4 l.514 | An unavailable Step runtime parks with `needs-answer`, but no answer can supply a runtime. Answering re-arms and re-parks (W15), which sends the human round a loop. The reason tells the user the wrong action | Add a park reason such as `unavailable` (or reuse `spec-conflict`) whose `next` says "Block `<id>` has no runtime; cancel, or wait for it to be installed". Or keep `needs-answer` but make the generated question state that the only useful answers are cancel or retry-after-install |
| F6 | MEDIUM | Flow | W11 l.30, §4 l.199 | After `recipe revoke`, running instances park `spec-conflict`. The recipe hash is frozen at start (l.218), so an answer resumes into the same revoked hash and re-parks. Cancel is the only real exit, and nothing says so. `revoke` also doesn't report how many instances it will park | `revoke` prints the count of affected instances and asks for confirmation when it's more than 0. The `spec-conflict` `next` names cancel (and restart on the new version) as the way out |
| F7 | MEDIUM | IA | Phases 4–7 l.510, l.525, l.513, l.545 | Verb namespaces are inconsistent: `workflow start\|status\|answer\|cancel`, but top-level `takeover\|handback`, `ledger` and `outbox list`. Instances are addressed by item (`T-3`), but an item can have several attempts and children (`workflow.step/…`), so `<item>` is ambiguous | Put `takeover`/`handback` under `workflow` (keep top-level aliases if D §9.3 already uses them). Define the addressing: `<item>` means the latest root instance, and `--instance <id>` or `<item>/<child>` reaches the others. `status` prints the children |
| F8 | MEDIUM | States | §4 l.200, Phase 3 l.499, S1-D6 | Empty and not-found states are unspecified: `recipe list` with nothing registered, `recipe eval` on a recipe with no evals (allowed for a draft, l.99), `ledger --item` with no events, `status` on a terminal instance | One line each, in plain words, exit 0 (e.g. "No recipes registered. `sindri recipe register <file>`"). Terminal `status` shows the outcome and when it was reached |
| F9 | MEDIUM | IA | CLAUDE.md UI row; §6.1 l.280 `GetInstance` | The Avalonia UI must be "a client with no logic of its own", but `GetInstance` isn't specified to return the allowed commands per state, the open question, or the rendered `next`. Without them the UI has to re-derive transition rules | `GetInstance` returns `allowedCommands[]`, `parkReason`, `question?`, `next` (rendered), loop and budget figures, and children. The CLI `status` renders the same object |
| F10 | MEDIUM | A11y | §9 l.360; API_CONTRACT | `--json` is promised only for `recipe validate`. API_CONTRACT requires it on every read command (`recipe list`, `recipe eval`, `workflow status`, `ledger`, `outbox list`). Exit codes aren't stated for the workflow verbs: is a gate refusal (SND-WF-004) or a park exit 1 (attention) or 2 (error)? | List `--json` and the exit code for each verb. Suggested: refusals and parks → 1, tool errors → 2, per API_CONTRACT |
| F11 | LOW | A11y | l.530, l.559 | `remaining 0:40` is ambiguous (minutes:seconds or hours:minutes) and differs from the recipe duration grammar (`<n>m`). `→` in plain output is read as "right arrow" | Use the duration grammar (`40s`, `1h20m`). Use `->` or "to" in plain output, and keep `→` for docs |
| F12 | MEDIUM | Voice (unscored) | §10 l.394–429; A §17.28.1 "Next" | `next` is defined as "the one allowed next step, which skills read", but the texts mix descriptive ("Waiting for the router…", "The verifier is reviewing…") with imperative ("Produce the Step output…"). `claimed` renders a literal `<user>` (l.399), which isn't a template variable | Pick one audience and mood. For example: `next` is addressed to whoever acts next and starts with the actor when that's a human ("You: …"); otherwise it's descriptive. Add `{user}` to the variable set or write the ref without it. Let the validator lint the mood |
| F13 | LOW | States | §7 l.322 vs Phase 1 l.469 | The rule line format includes `; <fix>`, but the Phase 1 example has no fix part. Success output ("prints its hash") has no format. The error catalogue (§6a) has no Fix column, though `docs/sindri/errors.md` has one | Make the example match the format. Define the success line (`ok workflow.step v1 sha256:…`). Add a Fix column to §6a |
| F14 | LOW | Flow | Phase 4 l.515 | `workflow cancel … --version <stale>` implies the user may pass a version. Whether it's required isn't stated, and requiring it from a human is friction | Make `--version` optional (default: the current version). Document it as the optimistic-concurrency guard for scripts and the UI |
| F15 | LOW | IA | W6 l.25, §10 l.413 | `workflow.item` maps every non-`resolved` child outcome to `dropped`/`unresolved`, so a child that ended `alreadySatisfied` reads as unresolved | Map `alreadySatisfied` to `done` (or its own terminal) in `workflow.item`. Count it in an eval |

### Information architecture (3/5)
- Evidence: the closed outcome set (W6, l.25) and the six park reasons (l.171–172) are a good, small vocabulary. But the four engine states sit outside it: no outcome for `cancelled`/`escalated`, no exit for `escalated`, and no engine `next` text (F1).
- Evidence: verbs are split between `workflow …` and the top level (l.510, l.525), and `<item>` addressing is ambiguous once attempts and children exist (l.216, l.561) (F7).
- Evidence: the UI needs `allowedCommands` and the open question from `GetInstance`, or it will hold logic (F9).
- Recommended changes: add the engine-state table (F1); unify the namespaces and define addressing (F7); specify the `GetInstance` view model (F9).

### Interaction states (3/5)
- Evidence: errors are well covered by codes (§6a l.306–317) and the validator line format (l.322).
- Evidence: success and empty states are given only as examples or not at all. `status` has no layout (F4), and list, eval and ledger have no empty states (F8).
- Recommended changes: a "CLI output" section with the field order for `status` and the success lines (F4, F13); one-line empty states (F8).

### User flow completeness (3/5)
- Evidence: the happy paths are traced end to end in the Phase signals (l.468–562). That's good practice.
- Evidence: the recovery paths stall. There's no answer syntax or question display (F2). Approval is shown without content (F3). An unfixable `needs-answer` loops (F5). Revoke leaves cancel as the only, unstated exit (F6).
- Recommended changes: specify `answer`/`grant` and the question display (F2); the approval summary (F3); a park reason for unavailable blocks (F5).

### Accessibility (4/5)
- Evidence: API_CONTRACT already makes state readable in plain words without colour. The typed confirmation is a plain TTY prompt (l.198).
- Evidence: `--json` coverage and exit codes for the workflow verbs aren't stated (F10). Ambiguous time and arrow glyphs appear in plain output (F11).
- Recommended changes: a per-verb `--json` and exit-code table (F10); use the duration grammar in output (F11). For the later UI, carry over A §17.21's screen-reader actions and focus-after-decision rules when it's designed.

### Brand/voice consistency (N/A — missing .impeccable.md)
- Unscored observations: the `next` texts mix moods and one has a literal `<user>` (F12). The §6a catalogue lacks the "Fix" column that the TS catalogue's voice relies on (F13). Otherwise the copy is plain and specific, which matches the repo's writing.

## Verdict
Five HIGH findings, none structural (F1 engine-state IA, F2 answer flow, F3 approval content, F4 status layout, F5 the misleading `needs-answer` park), put three dimensions at 3/5. Each is fixed by adding a section, not by rework.
verdict: NEEDS_WORK
