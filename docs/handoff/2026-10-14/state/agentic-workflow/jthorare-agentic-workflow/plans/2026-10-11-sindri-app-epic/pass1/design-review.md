# Plan Design Review — 2026-10-11-sindri-app-epic

**Plan:** `plans/2026-10-11-sindri-app-epic/plan.md` (an index over four documents in the `docs/sindri-app-epic` worktree)
**Reviewed:** 2026-10-11
**Lens focus (from the caller):** the UI as a full desk client; what API §3.6 views, the command catalog and needs-you rows give the UI; the one-click handoff to Warp with Claude Code, Codex or Cursor; what the reserved UI design spec must still settle.

Documents read as the plan:
- `docs/sindri/README.md` (README): "The application", the design-spec table, X1–X18
- `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (API)
- `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (WF)
- `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (CTX)
- Background: `docs/superpowers/specs/2026-10-09-sindri-dashboard-design.md` (DASH), the README's "what the UI shows" input

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✓ (index; the four specs above stand in for it) |
| design-brief.md | ✗ (the UI design spec is a reserved slot, README line 18; DASH is the nearest brief) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (`docs/sindri/DESIGN.md`, the Linear design language, exists but isn't a token file) |

Absent `.impeccable.md` ⇒ Brand/voice is N/A and not scored. No design-brief: the review rates what the API, engine and context specs hand the UI, and lists what the reserved UI design spec must settle.

## Summary
The API spec gives a UI more than most backend specs do: one catalog of actions with class, callers, CLI form and UI status (API 353–380); per-item `actions` (API 281); one needs-you shape with urgency, `primaryAction` and `cli` (API 472–499); one `EmptyText` table for empty, rebuilding and stalled (API 501–507); and read-your-writes and staleness rules written for the UI (API 233–237). Those are real wins. The problems are where the owner's 2026-10-11 decision (X2: the app starts workflows "scoping included", answers parks, takes over and hands back, and hands off to Warp with Claude Code, Codex or Cursor) promises flows that no spec owns or can deliver yet:
- No spec defines a scoping workflow.
- Opening a session and taking over disagree across the docs.
- "Attach" has no source of live sessions.
- Cursor and Codex as Warp handoff targets conflict with DASH §7 and with per-repo MCP registration.
- The engine's states (escalated, desk task, waits) don't all map to the API's `ItemState`.

Each fix is a short ownership or mapping line in X1–X18 or the named spec, plus items for the UI design spec. None needs a redesign.

## Ratings
Anchors per `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 3/5 | Engine states don't all map to `ItemState` or queue groups: `escalated`, open desk tasks and wait states have no item state, and `parked` sits outside `waitingOnYou` though every park needs the human (F2) |
| Interaction states | 3/5 | The same mapping gap leaves escalations and desk tasks with no item state to render. The handoff's success and failure states are deferred to a spec not yet written (F2, F11) |
| User flow completeness | 3/5 | Promised flows with no owner or no data: start scoping (F1), open vs takeover (F3), attach (F4), Codex and Cursor handoff (F5) |
| Accessibility | 4/5 | DASH's a11y rules carry over, but nothing covers the new desk forms, copy-the-CLI controls, or the attention colour on the Linear near-black canvas (F13) |
| Brand/voice consistency | N/A — missing .impeccable.md | (`docs/sindri/DESIGN.md` exists; feed it to `/design-language` when the UI spec is written) |

## Findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| F1 | HIGH | flow | API 550–552; WF 533, 536; README X2 (86); CTX 627–628; WF §12 (785–884) | "Starts workflows (scoping included)" has no recipe behind it. WF's first recipes are `workflow.item`, `workflow.step` and dev fixtures. CTX §12 says the scoping workflow "is rebuilt on this engine later", and no spec or phase owns it. The UI also has nothing to build a start form from: `workflow start` takes `--fact <key>=<file>` (WF 557), the CLI sends file contents (WF 501), and no route or catalog row describes a recipe's inputs | Add an X19 row naming the owner and phase of `workflow.scope` (or the scoping recipe) and its inputs (brief file, `linear:<project>`). Until it exists, narrow X2 to "starts any approved recipe; scoping once X19's phase lands". Have `recipe list`/`recipe show` return each recipe's `inputs` so the UI can render the start form |
| F2 | HIGH | IA, states | API 451–470, 455–459; WF 217–233; API P11 (1090) | P11 folds `workflow.*` into `needs-answer`, `taken-over`, `parked`, `running` and `queued` only. Engine states with no item state: `escalated` (gate, timeout, child), an `active` state running a desk task (W20, `run.runtime: desk`), and `wait` states such as `triaged`, `startable` and `claimed`. Every engine park needs a human answer, yet the `parked` group sits below `withYou` and outside `waitingOnYou` while its needs-you row pulses. `needs-answer` is both an item state and a park reason (WF 223), so the same words mean two things | Add a P11 mapping table, engine state → `ItemState` → queue group, covering every WF §3.8 state, desk tasks and waits. Either add `escalated` and `desk-task` item states or say which existing state each folds into. Make `waitingOnYou` hold every state whose needs-you row pulses, or document why parks are grouped apart. Rename one of the two `needs-answer` uses |
| F3 | HIGH | flow | README 65–66; DASH 43–45; API P10 (1088); X15 (99) | The docs disagree on whether opening a session takes over. README and DASH: "open in Warp … the item is taken over until handback". API P10: only `mode: attach` "marks the takeover", and only "once S2 runs". If `open` doesn't take over, engine timers and Steps keep running while the human edits in the new session. Nothing says what `open` does before S2, when the item has no instance | In API P10 (and X15), state that `OpenItem` in either mode issues `ApplyCommand:takeover` when the item has an active instance, and is a plain launch with no takeover when it has none. Add the takeover to P10's observable signal |
| F4 | HIGH | flow, IA | API P10 (1088) `mode: attach`; API §11 (825–834); DASH §7 (311–330); README X15 | "Attach for a live session" (X15) needs to know whether a live session exists and which one. `ItemView` (API 282) has no session field. `GET /sessions` and `POST /sessions/…/attach` are §11 "later" and "need their own spec". DASH §7's attach rules depend on the collector, which is unspecified in C#. The UI can't tell when to offer Attach instead of Open, and the server can't pick the session | Add to `ItemView` a `liveSession?` field (provider, id, host) fed by the launcher's own launch record. That's enough for P10 attach without the full collector. Or move attach out of P10 and out of X15 until the collector spec exists, and say so in X2 |
| F5 | HIGH | flow | X15 (99), X18 (102); API 328, 706–709; CTX 95–100; DASH 321–322; `.agents/rules/mcp-servers.md` | Two of the three handoff targets aren't designed. **Cursor:** DASH §7 says Cursor opens with `cursor <cwd>` and "a specific chat can't be opened directly". That's the IDE, not a Warp tab, and X15 says "Warp plus … Cursor" without naming `cursor-agent` or anything else. **Codex and Cursor context:** OpenItem passes only the item id, and "the session pulls its context through the MCP endpoint" (P10). The registration rule is "per repo … never user-wide", but for Codex and Cursor the spec only says they "get the equivalent entry". The repo's own MCP rule registers Codex servers with `codex mcp add` into `~/.codex/config.toml`, which is user-wide. A Codex or Cursor session launched this way either gets no Sindri context or leaks work context into personal repos | In X15, give each target its launch command (Claude Code `claude`; Codex `codex`; Cursor: name `cursor-agent` in Warp, or say it opens the Cursor IDE instead). Give each its per-repo MCP registration and how that carries `X-Sindri-Repo`, or record that the target has none and launches without context. Make P10's signal cover every target the UI offers, not only Claude Code |
| F6 | MEDIUM | flow, IA | API 484, 417–419; WF 221–222, 229, 534 | A needs-you `park` row's `primaryAction` is "the engine's action id". For `rounds` and `budget` parks the only answer is `answer-grant`/`answer-grant-tokens` (protected), and for gate escalations, `accept` is protected while `reject` is `desk`. So the row's one button is either a copy-CLI or, for escalations, a one-click "reject" next to a copy-only "accept". That nudges the human toward rejecting | State in API §3.6 that `primaryAction` is `copy:<cli>` whenever the reason's natural answer is protected. Have the row carry `secondaryActions` (cancel, takeover, open). Let the UI spec decide how accept and reject stay visually equal for escalations |
| F7 | MEDIUM | IA | API 353–380; WF 190, 533, 569–571 | Catalog rows carry class, route and CLI form but no input schema, so every desk form (`StartInstance`, `answer --text/--file`, `CompleteStep` outcome and outputs, `CreateItem`, `CloseItem` reason) is hand-built in the UI. `ApplyCommand:command-<verb>` is defined per recipe (`command.<verb>` triggers), so a static per-build catalog can't list those verbs | Either add a small field list to catalog rows, or say the UI renders engine forms from `GET /workflow/instances/{address}`'s `allowedCommands`, `open question/task` and `input facts` (WF 569–571), and that recipe verbs come only from there |
| F8 | MEDIUM | IA | API 281–282; WF 569–571 | Two "what can I do now" lists: an item's `actions` (API) and an instance's `allowedCommands` with actor class (WF). Neither spec says which one the UI uses for an item that has an instance, or whether `actions` includes the engine's ids after P11 | In API P11, say that `ItemActions` folds in the active instance's allowed engine action ids, so the item row has one list, and the instance view stays the detail source |
| F9 | MEDIUM | flow | API 554–556, 646–656; WF 540–543, 560–563; README 65 | Handback and `CompleteStep` are `desk`. Inside a launched Claude Code, Codex or Cursor session (`CLAUDECODE`, `CODEX_SANDBOX` and similar are set), `!sindri handback` and `sindri workflow complete` are `session` calls and are refused. README 65 tells the human "attach in Warp and type … until `sindri handback`", where the obvious place to type it refuses. Completing a desk task also needs output files (`--output fact:output=<file>`, 1 MiB) that the human produced in that session | Say in README and the UI spec that handback and complete happen in the app, or in a plain terminal tab, not inside the agent session. The UI spec must give taken-over items a prominent Hand back action, and give desk tasks a complete form with file upload. Have the `SND-AUTH-002` text for handback name the app |
| F10 | MEDIUM | IA | DASH 214, 222, 262, 283–294; API §6 (629–673), 5.1 (573–574); X5 | DASH is listed as the UI's input (README 21) but is stale against the API. It still describes bearer-token auth with "run `sindri dashboard --url`" (v0 has no tokens). It fires OS notifications for session `approval`/`input`, while X5 makes `notify.desk` the only notifier, and the engine doesn't see sessions. It lands on **Agents** when needs-you is empty, but v0 doesn't feed Agents (API 522). Its nav (Needs you, Agents, Repos, Automatic work, Today) has no Queue, Item or Workflow view | Add a dated amendment to DASH, or a README row, listing what the API and X5 replaced. Name the owner of session-state notifications once the collector exists. Leave the v0 nav and landing view to the UI spec (see the list below) |
| F11 | MEDIUM | states | API P10 (1088) "targets and their failure states from the UI design spec"; API 328 | The handoff's states are deferred to an unwritten spec, and P10 can't be built without them: Warp missing, the target CLI missing, the repo path missing or not onboarded, the MCP entry missing, an item already taken over (by whom), a launch timeout. `SND-LAUNCH-001` is one code for all of them. `ItemOpened(accepted, launchedIn)` has no "launched but not confirmed" state. No query lists the available targets or the profile default | List these in the UI spec, with one code each (or one code with a reason field), and add `GET /launch/targets` (or a `launchTargets` field on `GET /status`) giving installed targets and the default |
| F12 | MEDIUM | flow | CTX 461–490, 101–102; API 447 | Map review is the context engine's main human chore. It is interactive at a terminal, and `--apply` is `protected`, so the UI can't do it. No needs-you source covers "a draft map is waiting for review" (only a doctor warning, CTX 478) | Add a needs-you `context` row kind (calm), fed by draft age and size, whose `primaryAction` is `copy:sindri context map review`, or record in X9 that map review is CLI-only and not surfaced |
| F13 | MEDIUM | a11y | DASH 254, 264–269; README 132 (DESIGN.md: near-black canvas, one lavender-blue accent); API 501–513 | The a11y rules come from the browser era (DASH §5.1, mapped to Avalonia at DASH 28–30). Not covered: keyboard order and focus return for the new desk forms (start, answer, complete with file, decide, target picker); an accessible name and a "copied" announcement for every copy-the-CLI control; where focus goes after a command is accepted and the view re-queries; a contrast check of the amber-red attention colour and the lavender accent on Linear's near-black panels | Make these an explicit section of the UI design spec, with Avalonia headless tests (focus after accept, live-region text, reduce-motion outline) |
| F14 | MEDIUM | flow (build order) | API 1111–1129, 1090; X17 | P11 (engine item states), which the UI's live states depend on, isn't in API's dependency graph or wave table. `OpenItem` (P10) is last in the chain (W4, after the MCP tools), so the headline one-click handoff is the last thing to land, and before S2 it can't take over (F3) | Add P11 to the graph and waves (after P4 and engine P4). In X17, state which UI screens S1b may show before P10 and P11 (copy `sindri open` until then) |
| F15 | LOW | voice | README 39, 66 | README prose still says agent work "happens in Claude Code sessions" and `open` "starts a Claude Code session", after X15 and X18 made the launcher target-neutral | Say "an agent session (Claude Code, Codex or Cursor)" |
| F16 | LOW | IA (doc) | API 437, 449 | §3.7 appears before §3.6, and §3.6 is the section the UI design "builds on" (API 16) | Swap the two sections |
| F17 | LOW | flow | WF 567–568, 383 | Every engine write takes a nonce from `POST /workflow/instances/{a}/nonce`, so each UI write is two calls. Nothing says the UI fetches one per form open or per submit, or what it shows when a nonce expires | One line in the UI spec: fetch the nonce on submit, and retry once on expiry |

## Surface × state coverage
Cells cite where the plan addresses the state, or `GAP`.
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you list | API 211 (`rebuilding`), DASH 245 | API 219 (`stalled` banner), 507 | API 271, 502 | API 491–494 (cleared when …) |
| Queue (groups) | API 211 | API 219 | API 271, 280 | API 451–459 (escalated, desk task: GAP, F2) |
| Item detail + timeline | API 211 | API 294 (`SND-ITEM-404`), 507 | API 271 | API 282–283 |
| Decide / file request | — | API 112 (`SND-ITEM-409`), 195 (`SND-API-010` retry) | — | API 235–236 (done kept, view marked stale) |
| Services / jobs | API 211 | API 493 (`failure` row) | API 278 | API 399, 289 |
| Start workflow form | GAP | GAP (no input schema, F1/F7) | GAP (no recipes) | WF 557 (status header) |
| Answer a park / escalation | — | WF 484 (`SND-WF-003`) | — | WF 560 |
| Take over / hand back | — | GAP (handback refused inside a session, F9) | — | WF 563, 623 |
| Open in Warp (handoff) | GAP | GAP (one `SND-LAUNCH-001`, F11) | GAP (no targets list) | API 328 `ItemOpened` |
| Attach to live session | GAP | GAP | GAP | GAP (F4) |
| Complete desk task | — | WF 562 (`refused: …`) | — | WF 562 |
| Protected action (copy CLI) | — | API 561 (`SND-AUTH-002` text) | — | API 667 (`cli-only`); "copied" feedback GAP |
| Tray badge / pulse | — | DASH 248 ("sindri not responding") | DASH 271–277 | API 573–574 |
| Process not running / lease elsewhere | GAP (app vs `control.json`, API 678–681) | DASH 248 | — | — |
| Map review | — | CTX 452 (`SND-CONTEXT-041`) | GAP (no needs-you source) | CLI only (F12) |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| Needs you list | DASH 269 | DASH 267 (live region) | DASH 254 | DASH 257, 268 |
| Queue / item detail | DASH 269 | DASH 266 (state in words) | GAP (Linear canvas, F13) | DASH 268 |
| Desk forms (start, answer, complete, decide) | GAP | GAP | GAP | GAP (after accept, F13) |
| Copy-CLI control | GAP | GAP ("copied" not announced) | GAP | GAP |
| Target picker / Open in Warp | GAP | GAP | GAP | GAP (the app loses focus to Warp) |
| Tray badge | GAP | DASH 266 | DASH 254 | n/a |

## What the reserved UI design spec must settle
Beyond the fixes above, these are the UI spec's to decide; no other spec decides them:
1. **Nav and landing view for v0** (P1–P5 data only): Needs you, Queue, Item (with its instance and timeline), Decisions/Requests, Services. Agents, Repos and Today stay hidden until the collector spec exists. Landing: Needs you when not empty, else Queue (DASH's "else Agents" has no data in v0).
2. **The action map screen by screen**, keyed by catalog action id (X2 asks for it): which button, which form, which copy-CLI control, and what's disabled with `pending:<phase>`.
3. **The handoff:** the target picker (per item, or the profile default), the Open vs Attach rule (F4), every failure state (F11), and what the app shows while the item is taken over (with a prominent Hand back, F9).
4. **Starting a workflow:** the recipe picker, the input facts form with file upload, `--attempt next` for a finished attempt (WF 557), and the scoping entry once F1 is owned.
5. **Park and escalation answers:** one control per WF §3.8 reason, the protected answers as copy-CLI with the `cli` text, and accept and reject kept visually equal (F6).
6. **Connection states:** process not running, starting, restarted (a new epoch), lease held by another host, and SSE fallen back to 5 s polling (API 619). DASH 248's banner text is the starting point.
7. **Live refresh rules:** re-query on events without moving focus or scroll (DASH 268), `X-Sindri-After` handling (API 235–237), the 30 s stale mark.
8. **Accessibility for the new surfaces** (F13), with Avalonia headless tests.
9. **Tokens:** run `docs/sindri/DESIGN.md` through `/design-language` to produce `design-tokens.json` and `.impeccable.md`, including the single attention colour checked against the near-black canvas, so the next design review can score brand and voice.

## Detailed findings
### Information architecture (3/5)
- Evidence: the queue groups and their order are explicit (API 451–460), and needs-you is "one list across every source" (API 472). But P11's fold names only five states (API 468–469) against WF's state table (WF 217–233), so `escalated` and desk tasks have no group (F2). Two action lists exist for one item (API 281 vs WF 569–571, F8). DASH's nav predates the queue (DASH 217–242, F10).
- Recommended changes: a P11 state-mapping table (F2); one action list per item row (F8); the v0 nav in the UI spec (list item 1).

### Interaction states (3/5)
- Evidence: strong at the API level. `asOf` distinguishes empty, loading and broken (API 181). There's one `EmptyText` table that "the UI never keeps its own copy" of (API 501–504), a retry-not-done rule during a rebuild (API 195), and done-but-stale after `SND-API-004` (API 235–236). Gaps: the handoff's states are deferred, "targets and their failure states from the UI design spec" (API 1088, F11), escalations and desk tasks can't be rendered as item states (F2), and there's no connection-state design (table row "Process not running").
- Recommended changes: F11's states and targets query; F2's mapping; list item 6 in the UI spec.

### User flow completeness (3/5)
- Evidence: X2 promises "starts workflows (scoping included) … takes over and hands back, and hands work off to Warp with Claude Code, Codex or Cursor" (README 86, API 550–552, WF 536). Scoping has no recipe owner (CTX 627–628, F1). Open vs takeover disagree (README 66 vs API 1088, F3). Attach has no session source (API 825–834, F4). Cursor and Codex as targets conflict with DASH 321–322 and per-repo registration (API 706–709, F5). Handback can't be typed where README tells the human to type it (F9).
- Recommended changes: an X19 ownership row for scoping; one sentence each in P10 for takeover-on-open and attach scope; a per-target launch and registration table in X15.

### Accessibility (4/5)
- Evidence: DASH carries keyboard reach, a polite live region, reduced motion, words beside colour, and focus kept still while polling (DASH 264–269), mapped to Avalonia (DASH 28–30), and "UI renders text as plain text only" (API 513). Nothing covers the desk forms, copy-CLI controls, focus after an accepted command, or contrast on the Linear palette (F13).
- Recommended changes: an a11y section in the UI spec with headless tests (F13).

### Brand/voice consistency (N/A — missing .impeccable.md)
- Not scored. Voice is consistent where it is written: WF's rule "waits are descriptions; human-action states are instructions" (WF 214), and API's `EmptyText` sentences (API 502–503). F15 is the one voice drift found.

## Verdict
Five HIGH findings, all ownership or mapping gaps between X2's full-desk-client promise and what the specs deliver: scoping has no owner (F1), engine states don't map to item states (F2), open vs takeover disagree (F3), attach has no live-session source (F4), and the Codex and Cursor handoff targets aren't designed (F5). Each is fixable with a short X-row or phase edit before the UI design spec is written.
verdict: NEEDS_WORK
