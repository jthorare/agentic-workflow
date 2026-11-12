# The mockup fixture (one scenario across every screen)

Every mockup in `mockups/` renders this one state. A number, id, title or word that differs from this file is a
defect. Times are on 2026-10-12; "now" is 12:26.

## Health (one word, worst verdict wins)

`health.word` = **Degraded** on every page: the `index` service failed and the profile changed since approval.
The pill reads `◐ Degraded · updated 2s ago · index failed · profile changed` everywhere (System's own status line
repeats it). "Healthy" appears nowhere in this fixture.

## Navigation counts

Home · Workflows **12** · Work **12** · Recipes **8** · Context · Agents **4** · System

## Items (Work = 12 items)

| Id | Title | Repo | Source | State | Step | Agent | Since |
|---|---|---|---|---|---|---|---|
| ITM-01JB4 | Context map review for tabs.layout | agentic-workflow | Linear ENG-490 | ▲ escalated · gate (gate.verify.judge 0.62 < 0.80) | gate.verify.judge | — | 09:10 (2h 56m) |
| ITM-01JD2 | Re-scope requested: drop the iOS head from this item | sindri-app | request REQ-01JD4 from a session | ? needs decision | — | — | 11:40 (26m) |
| ITM-01JA1 | Onyx adapter secret pointers | sindri-app | Linear APP-212 | ◎ with you · taken over 12:01 | produce | Claude Code (attached in Warp) | 12:01 (25m) |
| ITM-01JC8 | Retry storm in outbox dispatcher | agentic-workflow | Linear ENG-503 | ▌ parked · no-progress (checks failed twice with the same findings) | step.2 · round 3/3 | — | 11:54 (12m) |
| ITM-01JE7 | Add needs-you ack route | agentic-workflow | Linear ENG-511 | ● running · live | produce · round 2/3 | Claude Code in Warp · snd-01JE7.2 | 12:20 (6m) |
| ITM-01JE9 | Tester: decision race integration test | agentic-workflow | Linear ENG-514 | ● running | checks · round 1/1 | Codex · snd-01JE9.1 | 12:24 (2m) |
| ITM-01JD9 | Judge outbox drain on shutdown | agentic-workflow | Linear ENG-508 | ● running | produce · round 1/3 | Claude Code in Warp · snd-01JD9.1 | 12:25 (1m) |
| ITM-01JD6 | Services page job panel | sindri-app | packet task t4 of PRJ-sync (ws-2) | ● running | checks | factory worker · snd-01JD6.1 (tool Step, no terminal) | 12:26 (0m) |
| ITM-01JF0 | Stream limits 429 fallback | agentic-workflow | triage sentry #8809 | ○ queued | — | — | queued 1h |
| ITM-01JF3 | doctor views line | sindri-app | packet task t6 of PRJ-sync (ws-3) | ○ queued | — | — | queued 52m |
| ITM-01JF4 | Tray badge on Linux | sindri-app | packet task t7 of PRJ-sync (ws-3) | ○ queued | — | — | queued 40m |
| ITM-01JF8 | Receipt verify route | sindri-app | packet task t8 of PRJ-sync (ws-4) | ○ queued | — | — | queued 12m |

Summary lines: Home `6 need you · 4 running · 1 with you · 4 queued`; Work `12 items · 3 need you · 1 with you ·
4 running · 4 queued` (3 + 1 + 4 + 4 = 12; the parked item is inside the 3, see "The human queue"). Home's Running panel shows the four running rows and the with-you row
(with-you first), then "Show all 5 in Work". Home's Queued panel shows the four queued rows.

Needs you (6, most urgent first): ITM-01JC8 park · REQ-01JD4 decision (re-scope ITM-01JD2) · ITM-01JB4 escalated ·
service `index` failure · profile changed (2 keys: `services.index.every`, `context.maxBudget`) · sentry #8812 `DECISION`
(a workflow row, drawn in the Home Workflows strip; see "Resulting counts").

ITM-01JE7's item sheet: subject `Linear ENG-511 · repo agentic-workflow · no project · feature:needs-you.ack`;
facts `fact:item` (ENG-511 intake record), `fact:spec` (spec.md sha256:1c0e…), `fact:output` (round 1: 4 files,
+182 −9; round 2 running), `fact:findings` (round 1: 2 findings: second ack returns 500; route missing from the
catalog), `fact:evidence` (after checks pass). Children: none.

## Root workflows (Workflows = 8 open · 4 done today)

| Address | Recipe | Subject | State | Since | Now |
|---|---|---|---|---|---|
| INC-14 | incident.response@5d90e1b2 v4 | PagerDuty P-2211 · Outbox retry storm | mitigate r1/3 ⇄ verify; comms child running | 11:02 (1h 24m) | agent rolling back in ITM-01JG2 · comms posting |
| sentry #8812 | triage.alert@d41f2a07 v2 | sentry #8812 TypeError in tabs.layout | classify · desk | 12:22 (4m) | needs your severity (desk answer) |
| sentry #8809 | triage.alert | sentry #8809 429s on stream | done 12m → ITM-01JF0 queued | 11:58 | done |
| pagerduty P-2211 | triage.alert | PagerDuty P-2211 | ▲ escalated: gate.severity 0.91 → INC-14 opened | 11:02 | accept at a terminal |
| sentry #8801, #8797, #8790 | triage.alert | three done rows (collapsed as "… 3 more") | done | — | — |
| PRJ-sync | workflow.project@a8c21e7f v12 | Linear APP · Offline sync · PRD-sync.md | build 4/9 (design done, packet 3f1c09 frozen) | 09:14 (3h 12m) | ITM-01JD6 checks · 4 done · 1 running · 1 parked · 3 queued |
| PRJ-exports | workflow.project | Linear ENG · Billing exports · PRD-exports.md | design · desk (awaiting import) | 10-11 16:02 (20h) | with you: import the design output |
| PRJ-tabs | workflow.project | Linear ENG · Tab manager v2 · PRD-tabs.md | scoping r2/3 (challenge) | 11:40 (46m) | challenger round 2 · 84k / 600k |
| LIN-ENG, LIN-APP | intake.linear | the two Linear teams | polling every 15m | — | last 12:15 · 2 new items |

PRJ-sync's nine children (packet 3f1c09, four workstreams): ws-1 UI: ITM-01JC3 Sync status pill (● done, PR #241
merged), ITM-01JC5 Conflict resolver dialog (● done, PR #243 merged); ws-2 Store: ITM-01JC1 PUT /sync/changes
(● done, PR #239), ITM-01JC2 Outbox queue (● done, PR #240), ITM-01JD6 Services page job panel (● running checks);
ws-3 Mobile: ITM-01JD7 Background sync (iOS) (▌ parked no-progress, mirrored to the parent),
ITM-01JF3 doctor views line (○ queued), ITM-01JF4 Tray badge on Linux (○ queued); ws-4 Rollout: ITM-01JF8 Receipt
verify route (○ queued). 4 done · 1 running · 1 parked · 3 queued = 9.
(ITM-01JD7 is a child only; it is not in Work's 12 because PRJ-sync's parked child is mirrored on the parent row.)

Open means not terminal (owner, 2026-10-14): the 12 root rows above are 8 open and 4 done (sentry #8809, #8801, #8797,
#8790). Home's Workflows strip: `WORKFLOWS 8 open`, rows INC-14, sentry #8812, PRJ-sync, PRJ-exports, PRJ-tabs, then
"… 3 more", and a collapsed "4 done today" line.

## Agents (4 live · 1 with you · 2 need you)

| Session | Agent | Item · step | Round | Tokens | Since | State |
|---|---|---|---|---|---|---|
| snd-01JE7.2 | Claude Code | ITM-01JE7 · produce | 2/3 | 84k / 200k | 6m | ● live, nobody attached |
| snd-01JD9.1 | Claude Code | ITM-01JD9 · produce | 1/3 | 22k / 200k | 1m | ● live |
| snd-01JA1.3 | Claude Code | ITM-01JA1 · produce | — | 41k / 200k | with you 25m | ◎ with you |
| snd-01JE9.1 | Codex | ITM-01JE9 · checks | 1/1 | 12k / 200k | 2m | ● live (tool Step, no terminal) |
| snd-01JD6.1 | factory worker | ITM-01JD6 · checks | — | 3k / 200k | 0m | ● live (tool Step, no terminal) |
| (model run) | challenger | PRJ-tabs · scoping / challenge | 2/3 | 84k / 600k | 46m | ◌ model call, no session |

"4 live" counts the four tmux sessions (JE7, JD9, JE9, JD6); the with-you session and the model run are listed
separately. Need you: ITM-01JC8 (parked), ITM-01JB4 (escalated). Budget today: Claude Code $3.10 / $8.00,
Codex $1.02 / $4.00, Cursor $0.00 / $2.00 (cursor not found on this host), total $4.12 / $14.00.

## Recipes (8)

As drawn in the spec §4.3 plus **`workflow.project`** (the root recipe the three Linear projects run: `● approved · open 3 · runs 9 · done 5 · failed 1 · human 67% · median 2d 4h · tokens/run 1.9M · last change 10-06 v12`), listed first. `scoping.project` is "changed since approval" (pinned block step.scope.draft v2 → v3). The page title reads `Recipes 8 · 30 days · 193 runs · 68% done without you`. The numbers for every recipe are in "Recipes: the numbers" below; the workflow.project line above agrees with that table (9 runs = 5 done + 1 failed + 3 cancelled; human 67% = 6 interventions ÷ 9).

## The human queue (one definition)

Three words, three sets, never mixed. Every count in a page head is derived from the rows under it.

- **needs you** = the union of *parked* + *escalated* + *decision* + *failed service* + *profile changed* rows
  (every row whose chip is `PARKED`, `ESCALATED`, `DECISION`, `PROFILE` or `FAILED`). A person has to act.
- **with you** = instances a person took over (`WITH YOU` chip). Neutral chip, never counted as needs you.
- **waiting on you** is retired: no page, heading, tooltip or table cell uses it.

Resulting counts (each equals the attention-chip rows on that page):

| Page | Needs you | Rows |
|---|---|---|
| Home | **6** | ITM-01JC8 parked, REQ-01JD4 decision, ITM-01JB4 escalated, service `index` failed, profile changed, sentry #8812 `DECISION` (drawn in the Workflows strip) |
| Workflows | **2** | sentry #8812 `DECISION`, PagerDuty P-2211 `ESCALATED` (PRJ-sync's parked child is a child, not a root row; INC-14, PRJ-sync, PRJ-tabs run, PRJ-exports is `WITH YOU`). Head: `8 open · 2 need you · 1 with you`, plus a collapsed "4 done today" line |
| Work | **3** | ITM-01JB4 escalated, ITM-01JD2 decision, ITM-01JC8 parked (the parked section folds into Needs you) |
| Agents | **2** | ITM-01JC8 parked, ITM-01JB4 escalated (sessions wait on them; failed service and profile are not sessions) |
| Sheets | their own | An item or workflow sheet's head counts only the attention-chip rows inside that sheet; an incident or project head names those rows |

Home counts every attention chip it draws (owner, 2026-10-14), so sentry #8812's `DECISION` chip in the Workflows strip makes Home's count 6: the five item-level, service and profile rows plus that workflow-level row, which Workflows also counts in its 2.


## Recipes: the numbers (30 days, one source for Recipes and the inspector)

Formulas, written once. **Done without you %** = done without you ÷ runs. **Human rate** = interventions ÷ runs,
where an intervention is a park answered, an escalation decided or a takeover (a run can have more than one, so the
rate is a per-run average, not a share of distinct runs). **Page total** = the run-weighted mean: sum of the
done-without-you column ÷ sum of the runs column. Runs = done + failed + cancelled.

| Recipe | Runs | Done | Done without you | Failed | Cancelled | Interventions (parks / escalations / takeovers) | Human rate | Median | Tokens p50 | 4 weeks (runs, oldest first) | Done without you % |
|---|---|---|---|---|---|---|---|---|---|---|---|
| workflow.project | 9 | 5 | 3 | 1 | 3 | 6 (4 / 1 / 1) | 67% | 2d 4h | 1.9M | 1 · 2 · 3 · 3 | 33% |
| workflow.item | 112 | 97 | 81 | 6 | 9 | 34 (16 / 11 / 7) | 30% | 1h 40m | 310k | 22 · 27 · 30 · 33 | 72% |
| workflow.step | none (child only) | | | | | | | | | | none |
| builder.feature | 38 | 34 | 29 | 2 | 2 | 10 (5 / 3 / 2) | 26% | 2h 05m | 420k | 6 · 9 · 11 · 12 | 76% |
| triage.alert | 24 | 22 | 14 | 0 | 2 | 10 (8 / 2 / 0) | 42% | 6m | 18k | 5 · 6 · 6 · 7 | 58% |
| incident.response | 3 | 2 | 0 | 1 | 0 | 3 (2 / 0 / 1) | 100% | 3h 10m | 190k | 1 · 1 · 0 · 1 | 0% |
| scoping.project | 7 | 6 | 5 | 0 | 1 | 2 (2 / 0 / 0) | 29% | 48m | 540k | 0 · 2 · 2 · 3 | 71% |
| intake.linear | none (draft) | | | | | | | | | | none |
| **Page total** | **193** | 166 | **132** | 10 | 17 | 65 | 34% | | | | **68%** (132 ÷ 193) |

Checks: 9 + 112 + 38 + 24 + 3 + 7 = 193; 166 + 10 + 17 = 193; 3 + 81 + 29 + 14 + 0 + 5 = 132.
workflow.item, the inspector's recipe: 81 done without you + 12 done after a park + 4 done after a takeover = 97
done; 97 + 6 failed + 9 cancelled = 112; human rate (16 + 11 + 7) ÷ 112 = **30%** (the earlier 19% was wrong); done
without you 81 ÷ 112 = **72%**. Its weekly human rate (4 weeks, oldest first): 5 ÷ 22 = 23%, 8 ÷ 27 = 30%,
9 ÷ 30 = 30%, 12 ÷ 33 = 36%. `Failed` on the Recipes table counts failed runs only; cancelled runs appear in the
inspector's Outcomes.

## Context

Map v13 current, draft v14 (46 proposals: 31 deterministic, 15 model; 3 orphaned links). Anchor
`feature:tabs.tab-manager`. Sources: linear ● synced 2m ago; linear:doc ● 2m ago; slack ◐ 2h behind; onyx ◐ 2h
behind; git ● 12:21 (last fetch). The **code index failed at the 03:00 nightly build** (SND-INDEX-012, graphify timed out): every page says `code index ✕ 03:00`, never 12:21; the symbols lens reads "index not built until CTX phase 9".

## System

Degraded: code index ✕ (nightly 03:00, SND-INDEX-012), Onyx ◐ 2h behind, cursor ○ not found. Engine: the worker line reads `activities 4 running` (JE7, JD9, JE9, JD6); the engine table has eight rows: ITM-01JE7, ITM-01JD9, ITM-01JE9, ITM-01JD6, ITM-01JC8, ITM-01JB4, PRJ-tabs, INC-14 (the other roots are collapsed as `… 4 more open`).

## Glyph vocabulary (one legend, every page)

- Work state: `●` done or live · `◐` in progress · `○` not started or queued · `◎` with you · `▌` parked ·
  `▲` escalated · `◌` model call, no session · `⇄` loop.
- Sessions (Agents): `●` RUNNING · `◐` ASKING · `○` IDLE · `○` UNKNOWN. The word is always there; the glyph is never the only signal.
- Health: `✓` ok · `◐` degraded · `✕` failed · `○` absent.
- Colour follows the glyph identically everywhere: `▌` and `▲` and the needs-you chip in `--attn`; `✕` in
  `--error`; live `●` and `✓` in `--success`; every other glyph in ink shades. Always with a word.

## The protected-action label (one pattern)

A locked secondary button: lock glyph, the verb, a middle dot, "terminal": `⌂ Grant rounds · terminal`,
`⌂ Accept · terminal`, `⌂ Approve · terminal`, `⌂ Re-approve · terminal`, `⌂ Promote · terminal`. Its `title` is
"Needs a typed confirmation at a terminal"; the copyable `$ sindri …` line sits under it where the row has room.
Never prose, never a dash.

## Sentences that must read the same on every page

- **Attach** (the spec's X55 rule): `Attach joins the live session; your first keystroke takes it over.` That line sits under
  every Attach button, on Home, Agents and the sheets. Take over is a separate button that pauses the automation at once.
- **The decision row** (REQ-01JD4): `Accept records your decision now; the engine acts on it when the workflow runs.` Accept
  stays a live primary (recording is API P4, served); a control whose API phase hasn't landed uses the pending pattern
  (disabled, with the dashed "Available with API Pn" chip), never a live-looking button.
- **The health sentence** (generated from the pill's reasons, verbatim on Home and System): `Sindri is degraded: the code
  index failed at the 03:00 build, the profile changed since approval, and Onyx is 2h behind. Updated 2s ago.` No
  attention band on any page; the pill carries the state.
- **INC-14's NOW**: `agent rolling back in ITM-01JG2 · comms posting`, everywhere the row appears (Home strip, Workflows,
  and the lists behind the sheets).
- **PRJ-exports' input**: `PRD-exports.md`, never `PRD-exports`.
- **Behind a sheet** (one rule): the list reflows to a narrow live list (id, title, state) with no scrim and no slicing;
  headers never cut mid-word. The same on Workflows, Work and Recipes.

## State chips: one mapping of word to colour (owner, 2026-10-14)

Every state chip on every page uses exactly this table: the word, the colour class, nothing else. A row keeps its
page's chip when it appears behind a sheet (the stage goes in the tag, never in the chip). No page invents a word.

| Chip word | Class | Colour | Meaning | Where |
|---|---|---|---|---|
| `PARKED` | `chip attn` | `--attn` text | an instance waits for a person's answer | needs-you rows, Work, Agents needs you, children, engine table |
| `ESCALATED` | `chip attn` | `--attn` | a gate or timeout escalated; a person decides | same |
| `DECISION` | `chip attn` | `--attn` | a request or decision waits for a person | needs-you rows, Workflows triage rows |
| `PROFILE` | `chip attn` | `--attn` | the profile changed since approval | needs-you row |
| `CHANGED` | `chip attn` | `--attn` | a recipe changed since approval | Recipes |
| `ORPHANED` | `chip attn` | `--attn` | a map link lost its node | Context map review |
| `DEGRADED` | `chip attn` | `--attn` | a source or service runs behind | Context sources, System |
| `FAILED` | `chip fail` | `--error` | a service, job or check failed; also a workflow instance that ended with outcome `failed` | needs-you row, System, sources, Workflows, children |
| `RUNNING` | `chip ok` | `--success` | an agent or tool is working now (regardless of whether a terminal exists; Attach shows that) | Home, Work, Workflows, Agents, children, inspector, engine table |
| `ASKING` | `chip` | ink (not `--attn`, until owner Q3) | a live session is waiting on the human; glyph `◐`; the word reads `ASKING · approval · Bash` when the kind is served (Agents sessions only) | Agents, session sheet |
| `IDLE` | `chip` | neutral | a live session that is not working and not asking; glyph `○` (Agents sessions only) | Agents, session sheet |
| `UNKNOWN` | `chip` | `--ink-subtle` | a live session whose state can't be read; glyph `○`; `estimated` follows the chip when the state is a guess (Agents sessions only) | Agents, session sheet |
| `OK` | `chip ok` | `--success` | healthy | System services, sources |
| `WITH YOU` | `chip` | `--ink-muted` on `--s2` | a person holds the instance (taken over) | Home, Work, Workflows, Agents |
| `QUEUED` | `chip` | neutral | waiting for a run slot | Home, Work, children |
| `DONE` | `chip` | neutral | finished well (any outcome but the three below) | Workflows, children, Recipes stats |
| `DIDN'T WORK OUT` | `chip attn` | `--attn` | a workflow instance that ended with outcome `unresolved` (owner, 2026-10-19) | Workflows, children |
| `CANCELLED` | `chip` | neutral | a workflow instance that ended with outcome `cancelled` (owner, 2026-10-19) | Workflows, children |
| `DRAFT` | `chip` | neutral | never approved | Recipes, Context map version |
| `APPROVED` | `chip` | neutral | approved recipe | Recipes |
| `DETERMINISTIC`, `MODEL` | `chip` | neutral | a map proposal's provenance; also MODEL for a model call with no session on Agents | Context map review, Agents |
| `CHILD` | `chip` | neutral | a state waiting on children | engine table |
| `POLLING` | `chip` | neutral | the worker polls | System worker line |
| `ABSENT` | `chip` | `--ink-subtle` | a dependency not found on this host | System |

Retired words: `LIVE` (now `RUNNING`; the Attach action says a terminal exists), `CLASSIFY`, `BUILD 4/9` and every
other stage name used as a chip (stages are tags). The page's health word uses the same colours: `Degraded` in
`--attn`, `Healthy` in `--success`, `Stalled` and `Not running` in `--error`.
