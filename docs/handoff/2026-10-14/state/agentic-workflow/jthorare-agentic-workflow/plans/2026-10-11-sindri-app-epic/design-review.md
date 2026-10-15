# Plan Design Review (pass 2): 2026-10-11-sindri-app-epic

**Plan:** `plans/2026-10-11-sindri-app-epic/plan.md`, an index over four documents in the `docs/sindri-app-epic` worktree at `1c328de9`
**Reviewed:** 2026-10-11
**Pass:** 2. Pass 1 is in `pass1/` (`consolidated-review.md` E1–E29, `design-review.md` F1–F17); README X19–X34 record the fixes.
**Scope (from the caller):** this pass doesn't review UI design content, which belongs to the reserved UI design spec. It checks only whether the API, engine and context specs now hand the UI a coherent contract:
- whether pass-1 E11–E14 (and the related F-findings) are resolved;
- what contract gaps or contradictions the X19–X34 edits introduced, in four areas: the engine→item state map (API §3.6), the open, attach and takeover rules, recipe input metadata, and the `human` token.

Documents read as the plan (line numbers are at `1c328de9`):
- `docs/sindri/README.md` (README)
- `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (API)
- `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (WF)
- `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (CTX)

## Inputs available
| Input | Present? |
|---|---|
| plan.md | ✓ (an index; the four documents above stand in for it) |
| design-brief.md | ✗ (the UI design spec is the reserved slot, README 18) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ |

Brand/voice is N/A because `.impeccable.md` is missing. Accessibility is N/A in this pass: it is UI design content, which the caller excluded, and pass 1's F13 list carries over to the UI design spec unchanged.

## Summary
X19–X34 closed the ownership gaps that pass 1 found:
- the scoping recipe has a named home (X34);
- recipes carry `inputs` metadata;
- API §3.6 has an engine-to-item state table;
- open-takes-over, launch records and attach are written down (X29);
- Codex and Cursor get launch-time MCP (X22);
- a `human` token is promised before the app's first write (X31).

So none of E11–E14 is still unaddressed, but each fix is only partly done. Several fixes also introduced contradictions that the UI would build on:
1. **State map vs groups:** the new state map puts `parked` items in `waitingOnYou`, while the group table, unchanged, still puts them in `parked`.
2. **Attach:** API P10 says attach only focuses the live session, the engine spec says attach is the takeover, and the README says typing into it is.
3. **Launch lifecycle:** `OpenItem` must take over, launch and record a session in one command. That breaks API §2.1's rules: one event per command, and no slow work in a handler. No one emits `session.ended` for a human's session.
4. **The `human` token:** it has no phase, §11 still calls it "later", and a stale token silently downgrades the app to `session`, so the app gets a refusal that tells the user to "use the Sindri app".
5. **Answering parks:** needs-you park rows carry no structured reason, and `ApplyCommand:answer` covers text, resume and reject alike, so the UI can't choose which answer control to show from the contract.
6. **Desk forms:** X34 gives the app a start form to render, but no spec defines the engine write routes, their bodies or how a file is sent.

Each is a spec edit, not a redesign.

## E11–E14 and related pass-1 findings: status

| Pass-1 finding | Status | Evidence | What remains |
|---|---|---|---|
| **E11** (F1) Scoping unowned; no recipe input metadata for a start form | **Partly** | WF 136: `inputs` long form `{ key, label?, type?: text \| file \| item }`, returned by `recipe show` and `GET /workflow/recipes`. README 134 (X34): the scoping recipe's spec comes "next after the UI design"; until then "start offers whatever recipes are approved" | <ul><li>`inputs` has no `required`, `default`, description or size limit.</li><li>How `type: item` relates to `workflow start`'s mandatory `--item` (WF 558) isn't stated.</li><li>No route or body for `StartInstance` and no file encoding (D6).</li><li>Nothing says how the recipe list shows approval state.</li><li>The scoping spec has no row in the build-order table (README 81–91).</li><li>API 575 and WF 534/537 still promise "starts workflows (scoping included)" with no qualifier (D12)</li></ul> |
| **E12** (F2) Engine → item state map incomplete | **Partly** | API 479–493: a full table: desk task → `needs-answer`/`waitingOnYou`; any park and `escalated` → `parked`/`waitingOnYou`; waits → `running`; router-started → `queued` | <ul><li>Contradicts the group table at API 460–467 (D1).</li><li>`escalated` covers only the gate case (D7).</li><li>`needs-answer` still names two different things (D9).</li><li>Rows have no structured reason (D5)</li></ul> |
| **E13** (F3) Open vs takeover contradict | **Partly** | README 66 and API 1130 now agree: opening an item that has a live instance takes it over, and the app offers handback | <ul><li>Attach now contradicts across the three docs (D2).</li><li>P10's signal (API 1130) doesn't exercise takeover-on-open, the double-launch refusal or attach.</li><li>The desk-task row offers `OpenItem` as an alternative to `CompleteStep`, but the takeover then blocks `complete` (D10)</li></ul> |
| **E14** (F4) Attach has no data | **Partly** | API 1130: `session.launched`/`session.ended`, `ItemView.session?`, attach only when live, no double launch | <ul><li>`ItemView.session` has no shape, and API §3.1's `ItemView` (287) doesn't list it.</li><li>Neither event is in API §3.4's catalog, and API P10 and WF P8 (1285–1286) both claim them.</li><li>Nothing ends a human session.</li><li>No code is given for the double-launch refusal (D3)</li></ul> |
| E6 / F5: Codex and Cursor handoff | **Partly** | API 743–748 (X22): never registered; launch-time MCP config; doctor checks all three files | <ul><li>CTX 98–103 still says "Codex and Cursor get the equivalent entry" and doctor checks only `~/.claude.json` (D11).</li><li>The Cursor target (the IDE or `cursor-agent` in Warp) is still undefined.</li><li>P10's signal covers Claude Code only</li></ul> |
| E19: `human` token | **Partly** | API 696–700: minted per `serve` start, sent as `X-Sindri-Token` | No phase owns it; it contradicts §11; it has a silent-downgrade failure mode (D4) |
| E21: double launch | **Partly** | API 1130: "refused (fact key per item and session)" | There's no code, and no `session.ended` for human sessions, so a closed tab blocks every later open (D3) |
| E15: `answer-grant-tokens` named | **Partly** | Named in API 72–73 and WF 535 | API §4.1's `protected` list (559) still omits it and the context map routes (D13) |
| F6: one primary action for escalations | **Partly** | API 487 lists two actions for `escalated` | `NeedsYouRow` still has a single `primaryAction` (API 507) and no secondary actions (D5) |
| F7: catalog rows have no input schema | **Not resolved** | API 282 and 363–373 are unchanged; `ApplyCommand:command-<verb>` is still per recipe (WF 534) | Folded into D6 |
| F8: two action lists per item | **Not resolved** | `ItemActions.For(state)` (API 887) vs `allowedCommands` (WF 570); P11 (API 1132) doesn't fold them | D8 |
| F9: handback typed inside a session is refused | **Not resolved** | README 65 ("attach in Warp and type … until `sindri handback`"); WF 234 and 624 `next` text says "Run `sindri workflow handback`"; WF 653: a session can't hand back | D10 |
| F11: handoff failure states and targets query | **Not resolved** (deferred) | API 805: one `SND-LAUNCH-001`; P10 defers its "targets and their failure states" to the UI design spec | D3 (the API must leave room for them) |
| F12: map review has no needs-you source | **Not resolved** | CTX 488–499 and API §3.6 kinds (500): there's no `context` row kind | L3 |
| F14: P11 missing from the build order | **Partly** | README 88 puts P11 in wave 5 | API's own graph and waves (1153–1167) and "P6, P8 and P10 follow" (1115) still leave P11 out (L1) |
| F15, F16, F17 | **Not resolved** | README 39 still says "Claude Code sessions"; API §3.7 (444) still comes before §3.6 (456); the nonce-per-submit rule is still unwritten | L1, L2, L4 |

## Ratings
Anchors per `_shared/severity.md`.
| Dimension | Rating | Top gap |
|---|---|---|
| Information architecture | 3/5 | API §3.6's group table and its new state map disagree on where `parked` items go (D1). `needs-answer` names both an item state (a desk task) and a park reason that maps to item `parked` (D9) |
| Interaction states | 3/5 | Park rows carry no structured reason, so the UI can't tell a resume from a text answer or a reject (D5). The launch lifecycle has no "launched", "ended" or "failed" states it can render (D3) |
| User flow completeness | 3/5 | The attach semantics contradict (D2). The desk forms (start, answer, complete) have no wire contract (D6). The token can silently downgrade every app write (D4) |
| Accessibility | N/A — out of scope this pass (UI design content; F13 carries to the UI design spec) | — |
| Brand/voice consistency | N/A — missing .impeccable.md | — |

## Findings
| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| D1 | HIGH | IA | API 460–467 vs 479–493 | **The queue groups contradict the new state map.** The group table maps `ItemState` to groups (`parked` → group `parked`), while the X28 map sends `parked` (every park and escalation) to `waitingOnYou`. Line 492 then says the `parked` group holds only "items parked by a human's own choice (none in v0)", but no item state exists for that. A P3 implementer who derives group from state, as the table says, puts every engine park outside `waitingOnYou`, which is the bug X28 meant to fix | Move `parked` into the `waitingOnYou` row of the group table and delete the `parked` group (or give the human-parked case its own state when it exists). Add a P3/P11 unit test asserting group = f(state) for every value |
| D2 | HIGH | flow | API 333 and 1130 ("`mode: attach` focuses the recorded live session instead of opening a new one"); WF 658 ("A human may take the instance over at any time (`OpenItem` with `attach`)"); README 65 ("attach in Warp and type. That's a takeover") | **Attach means three different things.** API: attach only focuses the session, and the takeover comes from a plain open. WF: attach *is* the takeover. README: typing into the attached session is the takeover, detected by the hook (X3). The specs also don't say which rule wins for an item whose agent-runtime session is live: "a second open … is refused" or "opening an item with a live instance takes it over". So the UI can't tell what its Attach button does to engine state, or whether Open is enabled | State one rule in API P10 and quote it in WF §6.5 and the README. Suggested rule: when a session is live, only Attach is offered. Attach focuses the session and issues `ApplyCommand:takeover` itself (no wait for the hook) when the instance isn't already taken over. Open is offered only when no session is live, and it takes over when an instance is live. Add both cases and the refusal to P10's signal |
| D3 | HIGH | flow, states | API 92–93, 83 ("exactly one domain event"), 88–90 ("Handlers never do slow work"), 333 (`ItemOpened(accepted, launchedIn)`), 387–408 (event catalog), 805 (`SND-LAUNCH-001`), 1130; WF 649, 657, 1285–1286 | **The launch lifecycle has no consistent contract.**<ul><li>`OpenItem` must take over (an engine event), record `session.launched` and spawn Warp in one command. But a handler appends exactly one event and does no slow work.</li><li>`launchedIn` and a synchronous `SND-LAUNCH-001` (503) imply the launch runs inside the handler.</li><li>`session.launched`/`session.ended` aren't in §3.4's event catalog, and both API P10 and WF P8 list them as their own files.</li><li>Only the agent runtime defines `session.ended` (a session that exits without reporting). Nothing detects that a human's Warp tab closed, so `ItemView.session` stays "live" forever: Attach points at a dead tab and the double-launch guard blocks every later Open.</li><li>`ItemView.session?` has no fields, and no code is named for the double-launch refusal.</li></ul> | <ul><li>In API P10, split the command from the launch. `OpenItem` appends `session.launch-requested`, which is its one event; the takeover goes through the engine's own command, as engine commands may append one event per instance. A post-accept launcher then appends `session.launched` or `session.launch-failed {reason}`.</li><li>Register `session.*` in §3.4 under one owner (API P10; WF P8 reuses them).</li><li>Define `ItemView.session { id, target, mode, launchedAt, state: launching\|live\|ended }`.</li><li>Define how a human session ends: the launcher's process exit if it can see it, else a desk `EndSession` ("I closed it") plus a liveness timeout.</li><li>Name the refusal code (`SND-LAUNCH-002`, or `SND-ITEM-409` with the live session in `detail`). This also gives F11's failure states a field to land in</li></ul> |
| D4 | HIGH | flow | API 696–700 vs 664–670, 807, 848–852; API Phases 1120–1132 | **The `human` token has no phase, and a failure the app can't explain.**<ul><li>§6 brings `auth/human.token` forward "before the app's first command (its first write phase in the UI design spec)", but no API phase builds it.</li><li>§11 still lists the same file as "later", and `SND-AUTH-001` ("missing or stale token") is still reserved for that later phase.</li><li>§6's header table doesn't list `X-Sindri-Token`.</li><li>A `human` header without a valid token "counts as `session`". The token changes on every `serve` start, so after a restart the app's writes get `SND-AUTH-002` "… Ask the human to run it in a terminal or the Sindri app": the app is told to use itself.</li><li>Nothing says the app re-reads the token on an epoch change (API 711–712), or that `sindri-prove.sh human` sends it</li></ul> | <ul><li>Give the token a phase: P1, or a named P1b before the first UI write phase, and put it in the build-order table.</li><li>Add `X-Sindri-Token` to the header table and remove it from §11.</li><li>A `human` header with a missing or stale token answers `SND-AUTH-001` (401, "re-read the token"), not a silent downgrade.</li><li>The app re-reads `auth/human.token` whenever `/ping`'s epoch changes, and retries once.</li><li>Make it part of the P1 signal</li></ul> |
| D5 | HIGH | states, IA | API 486–487, 497–509 (`reason` is "Scrubbed text"; one `primaryAction`); WF 223–234 (§3.8), 534 (`ApplyCommand:answer (text, resume, reject)`), 560 (`--needs-me` splits **Decisions** and **Tasks**) | **Park rows can't drive the right answer control.** Every engine row is kind `park` with a free-text `reason` and one `primaryAction`. `ApplyCommand:answer` is a single catalog row covering `--text`, `--resume` and `--reject`, so neither `primaryAction` nor the catalog tells the UI whether a `timeout` park needs a Resume button or a `needs-answer` park needs a text box. WF's `--needs-me` must split Decisions from Tasks using that same API response, but the response has no field to split on. Escalations need two equal actions (accept is copy-CLI, reject is one click: F6), but the row has room for one | <ul><li>Add `parkReason` to `NeedsYouRow` and `ItemView`, as an enum: `rounds`, `no-progress`, `budget`, `needs-answer`, `timeout`, `spec-conflict:*`, `no-runtime`, `escalated:gate\|timeout\|child`, `desk-task`, `idle-takeover`.</li><li>Split `ApplyCommand:answer` into catalog variants `answer-text`, `answer-resume` and `answer-reject`.</li><li>Replace the single `primaryAction` with `actions[]` (id, `copy:<cli>` when `cli-only`, `primary` flag).</li><li>`--needs-me` groups by `parkReason == desk-task`</li></ul> |
| D6 | HIGH | flow | WF 136, 502 ("the CLI sends file contents … at most 1 MiB"), 558–563, 570–572, 667; API 282, 348–356, 363–373 | **The desk forms have no wire contract.**<ul><li>X34 lets the app render a start form, but no spec gives the HTTP routes or request bodies for `StartInstance`, `ApplyCommand` (answer `--text/--file`), `CompleteStep` (`--run`, `--outcome`, `--output fact=<file>`) or `ListRecipes`. API §3.2 says the engine spec owns them, but the engine spec lists only slices and CLI verbs.</li><li>Nothing says how file contents go over HTTP: base64 in JSON, multipart, or under the 64 KiB body cap (API 134) next to a 1 MiB fact.</li><li>The complete form needs the Step's declared outcomes and outputs (`requiredOn`); `GET /workflow/instances/{a}` returns only "the open question or task (with its input facts)".</li><li>`inputs` lacks `required`.</li><li>Nothing defines how the recipe list marks which recipes are approved and startable.</li><li>Recipe `command.<verb>` triggers still can't appear in a static catalog (F7)</li></ul> | Add an engine route table to WF §6.3, the same shape as API §3.2, with:<ul><li>bodies;</li><li>one file encoding (base64 in JSON, with the body cap raised to 1.5 MiB for those routes);</li><li>`inputs[].required`;</li><li>`ListRecipes` rows with `approved`, `latestApprovedHash` and `inputs`;</li><li>`GetInstance` returning `task { runToken, outcomes[], outputs[{fact, requiredOn}] }` and the recipe verbs available now.</li></ul>State that the UI renders engine forms from `GetInstance`, not from catalog fields |
| D7 | MEDIUM | states | API 487 vs WF 231–233, 478 | The map gives every `escalated` row "→ `answer-accept` (cli-only) / `answer` reject". That fits only `escalated (gate)`. `escalated (timeout)` is answered with `--resume`, and `escalated (child)` is answered with the child's own answer, forwarded | One row per escalation kind in the API §3.6 map, or point the row's actions at D5's `parkReason` |
| D8 | MEDIUM | IA | API 286–287, 887 (`ItemActions.For(state)`), 1132; WF 570–571 | The item row's `actions` comes from item state alone, while the instance's `allowedCommands` is the engine's actual list. After P11 the queue row can offer an action the engine refuses (`SND-WF-003`), or leave out one it allows (`command-<verb>`, `answer-resume`) | P11: `ItemActions` folds in the live instance's `allowedCommands`, so the item row has one list and the instance view stays the detail source |
| D9 | MEDIUM | IA, voice | API 462, 484, 486, 493; WF 225 | `needs-answer` still means two things. As an item state it now means "an open desk task" (API 484). As an engine park reason it means "answer with `--text`", but that park maps to item state `parked` (API 486). So an item whose park reason is `needs-answer` is in state `parked`, and an item in state `needs-answer` doesn't need an answer | Rename the item state for desk tasks (`needs-task`, or `desk-task`) and keep `needs-answer` as the park reason only |
| D10 | MEDIUM | flow | API 484 ("Desk task → `CompleteStep` … or `OpenItem`"); API 1130 (open takes over); WF 234 (taken-over: out only by `handback`, `cancel`); README 65; WF 234/624 `next` text; WF 653 | Two flow traps.<ul><li>Opening a desk-task item to work on it takes the instance over, and a taken-over instance accepts only `handback` or `cancel`, so the human must hand back before `complete`. The spec doesn't say so, or say whether `complete` is allowed while taken over.</li><li>The README and the `taken-over` `next` text both tell the human to type `sindri handback` in the session they attached to, where it is refused as `session` (F9, unchanged)</li></ul> | <ul><li>Either allow `complete` on a taken-over instance whose covered frame is a desk task (it implies handback), or have `OpenItem` on a desk task launch without a takeover.</li><li>Change README 65 and the `taken-over` `next` text to "hand back in the Sindri app, or run `sindri handback` in a plain terminal"</li></ul> |
| D11 | MEDIUM | flow | CTX 98–103 vs API 743–748 (X22); API 759 (`context.mcp.enabled`) vs CTX 106 (`context.sessions.enabled`, X25) | The X22 and X25 edits stopped short of CTX §3 and API §8's tool table. CTX still says Codex and Cursor "get the equivalent entry" and doctor checks only `~/.claude.json`. API §8 still gates `context_*` tools on the retired `context.mcp.enabled` | Change CTX §3 to X22's wording (never registered; launch-time config; doctor checks all three files). Rename the key in API 759 |
| D12 | MEDIUM | flow | API 575; WF 534, 537; README 102 (X2) vs README 134 (X34), 81–91 | The scoping workflow is still promised as an app action ("starts workflows (scoping included)") in three places. X34 says its spec comes after the UI design, and the build-order table has no wave for it. The UI design spec will read X2 as a v0 screen | Qualify the three sentences: "starts any approved recipe; scoping once its recipe ships". Give the scoping spec a row in the build-order table (after the UI design spec and engine P7) |
| D13 | MEDIUM | IA | API 559 vs 72–73, 453–454; API 248 (`kind issue`) vs 363–373 | The catalog contract the UI renders from is incomplete:<ul><li>§4.1's `protected` list omits `answer-grant-tokens` and the context map's `import`, `promote` and review routes;</li><li>§2.5 gives the nonce and challenge routes catalog rows "of kind `issue`", but §3.3's row has no `kind` field, so the UI can't tell them apart from buttons</li></ul> | Complete §4.1's `protected` list. Add `kind: command \| issue` to §3.3's row, and say the UI never renders `issue` rows |
| D14 | MEDIUM | states | API 333, 805, 1130 ("targets and their failure states from the UI design spec") | (Carried F11.) The UI design spec must settle the handoff's failure states, but the API gives it one code and no query for the installed targets or the profile default. Without D3's `launch-failed {reason}` and a targets list, those states can't be rendered | `GET /launch/targets`, or `launchTargets` on `GET /status`: installed targets, the default, and whether each can carry Sindri MCP yet (X22). Give the reasons as an enum on `SND-LAUNCH-001`/`launch-failed` |
| D15 | MEDIUM | IA | WF 689–690 ("The desk runtime (W20) gives way to [ILauncher]"); API 437 ("The engine replaces its desk runtime with it"); WF 646 ("For a Step whose descriptor names an agent runtime"); API 484 (desk tasks) | It isn't clear whether desk tasks survive the agent runtime. Two lines say the desk runtime is replaced, while §6.5 and the state map assume it remains for Steps without an agent runtime. The desk-task row and the complete form depend on the answer | Say in WF §6.5 and API §3.5 that the desk runtime stays for `DeskCapable` Steps the profile doesn't route to an agent, and that `ILauncher` replaces only the human handoff that W20 stood in for |
| L1 | LOW | IA (doc) | README 17 ("API P0–P10"), 39; API 1115, 1153–1167 | README's table row stops at P10, though P11 exists. README 39 still says agent work "happens in Claude Code sessions". API's own graph and waves omit P11 (the README table has it) | Sweep |
| L2 | LOW | IA (doc) | API 444, 456 | §3.7 still comes before §3.6, which the UI "builds on" (API 19) | Swap the two sections |
| L3 | LOW | flow | CTX 488–499; API 500 | Map review and promotion are now all `protected` (cli-only in the app), and there is still no needs-you row for a waiting draft map; doctor alone surfaces it | A `calm` needs-you row of kind `context` with `copy:sindri context map review`, or record in X30 that it isn't surfaced |
| L4 | LOW | flow | WF 384, 568; API 248 | Each engine write from the app needs a nonce first. Nothing says when the app fetches it, or that it fetches a new one and retries once on `SND-WF-009` | One line in WF §6.3 (or leave it to the UI spec, and say so) |

## Surface × state coverage
Contract only, not UI design. A cell cites where the spec gives the UI the data for that state, or says `GAP`.
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| Needs you | API 216 (`rebuilding`) | API 224 (`stalled`) | API 524–527 | API 512–517 (cleared when) |
| Queue groups | API 216 | API 224 | API 276 | API 479–493; GAP: contradicts 460–467 (D1) |
| Park / escalation answer | — | WF 485 (`SND-WF-003`) | — | GAP: no structured reason or answer kind (D5, D7) |
| Desk task (complete) | — | WF 716 (`SND-WF-008`) | — | GAP: no outcomes or outputs in `GetInstance`, no route or body (D6) |
| Start workflow | GAP: recipe list shape (D6) | WF 701–703 (`SND-RECIPE-003/005`) | API 575 / README 134 (approved recipes only); shape GAP | WF 558 (status header) |
| Open / attach / handback | GAP: no `launching` state (D3) | API 805 (one code); GAP: reasons, double-launch code (D3, D14) | GAP: no targets list (D14) | API 333 `ItemOpened`; GAP: `ItemView.session` shape (D3) |
| Take over / hand back | — | API 586 (`SND-AUTH-002`); handback inside a session is refused (D10) | — | WF 563–564 |
| App writes (any) | — | GAP: a stale token downgrades to `session` with a misleading message (D4) | — | API 92–93 (`CommandAccepted`), 238–241 |
| Protected (copy CLI) | — | API 586 | — | API 372–373, 693–694; incomplete list (D13) |

### Accessibility sub-table
N/A this pass. The caller excluded UI design content. Pass 1's F13 (desk forms, copy-CLI controls, focus after accept, contrast on the Linear canvas) carries to the UI design spec unchanged.

## Detailed findings
### Information architecture (3/5)
- Evidence:
  - The new map (API 479–493) answers pass 1's question of which state each engine frame becomes, but the group table above it (460–467) still says `parked` → group `parked`, and line 492 describes a `parked` group that has no state to fill it (D1).
  - `needs-answer` changed meaning instead of being renamed: item state = desk task (484), park reason = answer with text (WF 225 → item `parked`) (D9).
  - Two action lists per item remain (D8).
  - The catalog row has no `kind`, so the new `issue` rows look like buttons (D13).
- Recommended changes:
  - Make the group table a function of the new state column.
  - Rename the desk-task state.
  - Fold `allowedCommands` into `ItemActions` in P11.
  - Add `kind` to catalog rows.

### Interaction states (3/5)
- Evidence:
  - The views' loading, empty and error states (`asOf`, `EmptyText`, `SND-API-010/011`) are still solid.
  - Pass 1 found states that couldn't be rendered. Pass 2 finds ones that can't be told apart: a park row's `reason` is free text, and `ApplyCommand:answer` is one action for text, resume and reject (D5).
  - Escalations other than gate escalations map to the wrong answer (D7).
  - The launch has no `launching`, `failed` or `ended` state the UI can show (D3, D14).
- Recommended changes:
  - `parkReason` as an enum.
  - Answer variants in the catalog.
  - `actions[]` on rows.
  - A launch state machine on `ItemView.session`.

### User flow completeness (3/5)
- Evidence:
  - Opening now takes over (README 66, API 1130), but attach is defined three ways (D2).
  - The open → takeover → complete path dead-ends at a taken-over instance that only accepts handback or cancel (D10).
  - The start form can be rendered (WF 136) but not submitted: no engine route, body or file encoding exists (D6).
  - Every app write depends on a token that has no phase and fails silently after a restart (D4).
  - Codex and Cursor handoff is fixed in the API spec but contradicted in CTX §3 (D11).
- Recommended changes:
  - One attach rule.
  - An engine route table.
  - A token phase with `SND-AUTH-001`.
  - Allow `complete` while a desk task is taken over, or open without a takeover.
  - CTX §3 aligned with X22.

### Accessibility (N/A)
- Out of scope for this pass; F13 carries to the UI design spec.

### Brand/voice consistency (N/A — missing .impeccable.md)
- Not scored. One voice drift remains: the `taken-over` `next` text tells the human to run a CLI verb that is refused where they're most likely to type it (D10).

## What the UI design spec can now rely on, and what it can't yet
- **It can rely on:**
  - the catalog-driven buttons;
  - `asOf` and `EmptyText`;
  - read-your-writes;
  - one needs-you list;
  - copy-CLI for `protected`;
  - "open takes over";
  - recipe `inputs` with label and type;
  - launch-time MCP for Codex and Cursor;
  - the wave in which each feed lands (README 81–93).
- **Wait for the fixes above before designing:**
  - the queue grouping (D1);
  - the park-answer controls (D5, D7);
  - the Open, Attach and Hand back controls and their states (D2, D3, D10, D14);
  - the start and complete forms (D6);
  - the app's reconnect and re-auth behaviour (D4);
  - any scoping entry point (D12).

## Verdict
Six HIGH findings, none structural: each is an edit to API §3.6, P10 or §6, or to WF §6.3 and §6.5. They come from the X28, X29, X31 and X34 fixes being applied in one spec and not carried into the other parts that depend on them: the group table, the event catalog and command rules, the phase table, and the engine routes. So the verdict is NEEDS_WORK.
verdict: NEEDS_WORK
