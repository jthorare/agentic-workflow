# Critique: Sindri UI revision-8 mockups (11 files)

Target: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/` (home, workflows, workflow-project, workflow-incident, item-sheet, recipes, recipe-inspector, agents, context, context-filemap, system). Register: product. Context: `docs/sindri/PRODUCT.md`, `docs/sindri/DESIGN.md`.

Two independent assessments: A, an LLM design review (read every HTML file and every 1280-wide render); B, `impeccable detect` 4.5.2 over the directory (220 findings). Neither saw the other.

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | "● Healthy" beside "index failed" on ten pages, "◐ Degraded" on System for the same state; no loading, success or post-click state drawn anywhere |
| 2 | Match system / real world | 3 | Leaks: "needs your class", "accept at a term.", a literal "(state)" after the budget bar, "signal answer (grant)" |
| 3 | User control and freedom | 2 | No undo for "Mark all seen", "Accept all deterministic" (31 writes) or Cancel on a running agent |
| 4 | Consistency and standards | 2 | Same id, different titles across pages; one glyph set with three legends; five spellings of the protected label; two topbar variants; the Workflows list built twice (table vs div grid) |
| 5 | Error prevention | 2 | Take over pre-focused in the project sheet, Accept pre-focused on Home; Cancel X on an unmodified key on every Agents row with no confirmation |
| 6 | Recognition over recall | 3 | Per-page legends for ◐; the graph/tree toggle is a 10.5 px mini-segment; recipe rows show no open affordance |
| 7 | Flexibility and efficiency | 3 | Chords, ⌘K, J/K, bulk accept; no bulk select on Work or Agents; key hints live in four places |
| 8 | Aesthetic and minimalist | 3 | Stripes, the always-pulsing band on all eleven pages, four buttons on every Agents row, a nested box (sheet, panel, expand) |
| 9 | Error recovery | 2 | System's Impact panel is excellent; no failed-action state exists anywhere |
| 10 | Help and documentation | 2 | Nothing explains "no-progress", "desk", "continue-as-new"; the disabled symbols lens explains itself only to screen readers |
| **Total** | | **25/40** | **Acceptable: significant improvements needed** |

## Anti-Patterns Verdict

**LLM assessment.** Not slop in the product sense: a Linear-fluent engineer would trust the chrome. One sans and one mono, restrained colour, no gradients, glass, hero metrics, card grids or modals; rows say their situation and carry one action. Two shared bans are hit: **side-stripe accents** (3 px orange bars on needs-you, parked, escalated and changed rows, inherited from the base shell; 2 to 3 px lavender selection insets, new, in nine of eleven files) and **em dashes** in every protected-action label and some prose. The one second-order trap touched is the box-drawing tree in context-filemap.html (terminal cosplay).

**Deterministic scan (B).** 220 findings in 11 files. Cramped padding 107 (text flush against container edges: chips, kbd hints, search field, table cells). Undersized functional text 57 (10.5 px labels, concentrated in workflow-project and workflow-incident, 26 each: the states band captions and the facts eyebrow). Tiny body text 29 (11.5 px body on agents, context, system, home). Low contrast 9 (kbd on the primary button 3.6:1; `--ink-tertiary` on surface-1 about 3.3:1). Same text repeated 3 (Attach three times inside one panel on agents; home). Em-dash saturation 3 files (16 in item-sheet). Tight line height 1 (recipe-inspector). "Overused font: Inter" 11 is a false positive: Inter is the spec's substitute for Linear's face and the product register allows it. Where A and B agree: the dashes, the contrast of tertiary ink, the Agents row repetition. B caught what A under-weighted: the type scale has six sizes between 10.5 and 13 px and the smallest carry functional text.

## Overall Impression

The visual system is right and the semantic system is loose. The pages look like one product, but the numbers, titles and glyphs do not agree across them, the riskiest actions sit under the first keystroke, and several panels render on top of themselves. Biggest opportunity: one fixture, one vocabulary, one action-safety rule, applied to all eleven files.

## What's Working

1. **Answer, then act, with the class visible.** Needs-you rows, Agents rows and System's Impact panel state the situation, carry the resolving action, mark protected ones with the lock and the copyable command, and explain consequences in place.
2. **One recipe-driven frame renders three kinds of instance.** States, Facts, Children read identically for a project, an incident and an item; the inspector's numbers reconcile (112 = 81 + 12 + 4 + 6 + 9).
3. **Accessibility is designed in.** aria on rows and graph nodes, sparklines and meters as `role="img"` with words, a reduced-motion rule, lock buttons whose title explains the typed confirmation.

## Priority Issues

- **[P1] Cross-page data drift breaks "Nothing invented".** ITM-01JE7 is "Add needs-you ack route" on Home and "Sync status pill" elsewhere; ITM-01JE9 and ITM-01JC8 likewise; Home says 6 running, Work says 2; the strip says 7 open under a tab that says 12; the health word differs by page. *Why:* the mockups are the contract the implementing agents read. *Fix:* one fixture feeding every page; compute the health word one way. Files: home, agents, workflow-project, item-sheet, workflows, system. **/impeccable polish**
- **[P1] Rendering defects that destroy meaning.** context.html map-review row overlaps its evidence line and splits "map_override row" across lines; system.html Impact panel lines overlap (the grid treats inline code as items); recipe-inspector renders 1289 px wide and clips the OPEN rows under the lock buttons; workflow-project Children titles collapse to "Ba…"; workflow-incident's crumb truncates; item-sheet's page-head counts stack, "Create item" clips, the Step column clips. **/impeccable layout**
- **[P1] The topbar overflows at 1280 two ways.** The base's 220 px search pushes the health pill off the edge (eight files clip "◇ profile changed"); three files shrink the search to 192 px and clip the ⌘K kbd instead. *Fix once in the shell.* **/impeccable layout**
- **[P1] Initial focus and single keys put the riskiest action first.** Take over pre-focused in workflow-project (↵ ↵ from the list is a takeover); Accept pre-focused on Home; Cancel X as a bare ghost button on every Agents row; "Accept all deterministic" commits 31 writes with no undo. *Fix:* focus the read action, move Cancel behind the chevron or a confirm, draw an undo state. **/impeccable harden**
- **[P1] One glyph set, three legends.** ● is done, live, ok and Live; ◐ is in progress, with you, degraded and "2 in progress" (◐◐); ○ is not started and absent; with-you is ◎ on Home and Work and ◐ on Agents. *Fix:* one vocabulary and one legend, colour following the glyph identically. **/impeccable clarify**
- **[P2] Five spellings of the protected label, all with the banned dash.** `"Grant rounds — at a terminal"`, `"Grant rounds — terminal"`, `"Grant — terminal"`, "Approve at a terminal", "accept at a term." as passive text. *Fix:* one pattern without the dash, always a button. **/impeccable clarify**
- **[P2] Side-stripes and the always-pulsing band.** The stripes duplicate the chip and the word; the band pulses forever on all eleven pages, including three where nothing needs anyone. *Fix:* delete the bars, tint the row with `--attn-soft`, make the band static and present only while the needs-you count is non-zero. **/impeccable quieter**
- **[P2] The item sheet is 520 wide while its frame is 760.** Facts wrap to three lines, Children 0 occupies a 164 px column for one sentence, the list behind loses Step, Agent and Since. *Fix:* 760 for every instance sheet; Children 0 as a footer line. **/impeccable layout**
- **[P2] Type and padding floor (from B).** 10.5 px functional labels, 11.5 px body, text flush against chip and cell edges. *Fix:* floor functional text at 11 px and body at 12 px, two sizes below the title instead of six, 2 to 4 px inset on chips and kbd. **/impeccable typeset**

## Cognitive Load

4 of 8 failed (high); two are deliberate density per PRODUCT.md principle 4. Real failures: no single focus on Context (six equal-weight panels); no progressive disclosure (every Agents row shows all four actions, and the sheet repeats them). Decision points over four options: the nav (7, approved), the map-review proposal row (5), the inspector head (5).

## Persona Red Flags

**Alex (power user):** no bulk select on Work or Agents; A and X mean different things per page; two keys for Start; Recipes pre-focuses "Validate file…" while opening a recipe has no key; the sheet tabs sit at the bottom with nothing under them.
**Sam (screen reader, low vision):** `input:focus{outline:none}` on the anchor field with no replacement; `role="row"` divs without a grid ancestor in three files; selection contrast about 1.1:1 plus a stripe; `--ink-tertiary` fails AA on null cells.
**Riley (stress):** "triage.alert 6" header over three rows with no "more" line; no empty states for Needs you 0, Agents 0, Workflows 0; a recipe with more than six states overflows the band; mid-word truncation.
**The owner glancing between sessions:** the health pill contradicts itself; the id in their Warp prompt has a different title on Home; ↵ ↵ from Workflows is a takeover; the band pulses for hours so it never signals change.

## Minor Observations

Home "Show all 7 in Queue" beside Queued 4; the split-button chevron has no key hint; stage headers lowercase mono on Workflows and uppercase sans behind the sheets; "(state)" placeholder in workflow-project; `.expand` is a nested card; the States band wraps "closed" with a dangling link glyph on the incident; the with-you Agents row has no meter track; "Validate file…" is the primary on Recipes; the 30-day selector is a 22 px pill on Recipes and a 28 px button on Agents; ◐◐ invents a glyph; the inspector dims its list and the other sheets don't; Take over offered on model and tool steps where takeover is undefined; "$4.12 today" beside a 30-day window; "Neighbors N" collides with "N start"; "Build draft" primary while 46 proposals wait, with no word on whether it discards them; the cross-band edges and the conflict button are absolute-positioned to this content height; collapsed placeholder bands shipped as product UI in two files; the legend wraps mid-sentence on System; raw engine strings "1 (exit 1)" and "escalate TO fired"; the fixed caption covers content in tall captures; arrows mean three things; lavender used for seven roles where DESIGN.md allows four; `#fff` on the primary button and brand mark; no loading or error state in any file.

## Questions to Consider

1. The chip says "park" and the word says "needs you now"; what is the orange bar for, and why does the same orange pulse on Recipes, Context and System?
2. Every live Agents row carries four actions and the sheet repeats them. Which surface is the control surface? Would one Open per row plus one hovered action row be faster and safer than twenty-eight buttons?
3. "● Healthy" sits beside "index failed" on ten pages and System says "Degraded". Is "healthy" the process or the system? One word computed the same way everywhere, or three verdicts?


---

# Critique run 2: Sindri UI revision-8 mockups, after the fix passes

Target: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/` at b84eddee (11 files, re-captured 1280-wide renders). Register: product. Independent assessment A (did not read run 1, fixes.md or the snapshots) plus the detector (B): 9 files clean, 2 with the em-dash advisory firing only on tabular nulls; "Overused font: Inter" ignored as a false positive.

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | No loading or refreshing state; the decision row's live-looking Accept beside "acted on once the workflow engine runs" |
| 2 | Match system / real world | 3 | Five phrasings for human-blocked; "open" means three things |
| 3 | User control and freedom | 3 | Undo on bulk accept and Mark all seen; none on a single map-review Accept/Reject, Drop, or the decision Accept |
| 4 | Consistency and standards | 2 | Band on 3 of 11 pages; three behind-sheet treatments; Attach primary in some places, secondary in others; hint placement varies; fixture drift |
| 5 | Error prevention | 3 | Cancel behind a menu plus confirm, read-first focus, budget marks; re-scope Accept and escalation Reject unguarded |
| 6 | Recognition rather than recall | 3 | Legends on three pages only; ◆ ? ◇ ░ ⚠ unexplained elsewhere; two adjacent ▾ on Agents waiting rows |
| 7 | Flexibility and efficiency | 3 | Chords, ⌘K, ⌘N, ⌘Z, bulk accept; no multi-select on Work or Agents |
| 8 | Aesthetic and minimalist design | 3 | "needs you now" repeats the panel title; spec-note parentheticals in Agents group headers; Context's Node panel stretched |
| 9 | Error recovery | 3 | Home's failure row and System's Impact name cause, fix and blast radius; refusal and Reload states exist only in comments |
| 10 | Help and documentation | 2 | "? Shortcuts" on four pages; no help surface, no "what is a park" |
| **Total** | | **28/40** | **Good: address weak areas, solid foundation** |

## Anti-Patterns Verdict

Passes the product slop test: one shell, one button vocabulary, one glyph set with a legend, Restrained palette, no stripes, gradients, hero metrics, card grids or modals; every id, hash and command plausible. The machine shows in uniformity: every section is the same surface-1 panel with an 11 px eyebrow and a 13/12/11 ladder, so hierarchy rides on four greys. The detector agrees: nothing left but the tabular-null em dashes.

## What's Working

- Home passes the five-second test: `5 need you · 4 running · 1 with you · 4 queued · Degraded`, most urgent first, every tinted row carrying its resolving action with the class visible.
- One instance frame for a project, an incident and an item; the wrapped states band keeps links attached to their nodes; the `next` sentence is the best copy in the set.
- The inspector's numbers reconcile; the lens stack's cross-band edges are computed from the node boxes.

## Priority Issues

- **[P1] Attach means three things.** Home says Attach pauses automation; Agents says the first keystroke is the takeover; the sheets make Attach the read action beside a separate Take over. One sentence everywhere. **clarify**
- **[P1] Not-yet-wired actions rendered live.** The decision row's Accept is a brand primary beside "acted on once the workflow engine runs", while Run again on the next row is disabled with a pending chip. One pattern. **harden**
- **[P1] Fixture drift.** `workflow.project` runs three instances but is not among the seven recipes; the engine table lacks ITM-01JD9 and ITM-01JD6 and says "activities 2 running" beside four live sessions; the index failed at 03:00 on Home and 12:21 on System and Context; INC-14's NOW differs behind the sheets. **audit**
- **[P1] Three behind-sheet treatments.** Work reflows live; Workflows is sliced with headers cut mid-word; Recipes dims with a scrim. One rule. **layout**
- **[P2] Human-blocked vocabulary and counts.** "need you", "waiting on you", "running at a desk", "to look at", each count with a different scope. **clarify**
- **[P2] Shell drift.** Band, sheet tab, hint placement and legends differ page to page. **polish**
- **[P2] The protected button has no defined click** on six pages; only Home shows the command line. **harden**
- **[P2] Decision points above four**: two chevrons on Agents waiting rows, five verbs on the Context proposal row, five controls on Home's escalated row. **distill**

## Cognitive Load

3 of 8 failed (moderate): single focus on Context, chunking on the lens row and the Recipes and Agents tables, minimal choices on the three rows above.

## Persona Red Flags

**Alex:** no multi-select; two chevrons on one row; the terminal hop with no command on Recipes, Context, Agents; hints move page to page. **Sam:** `● done` and `● live` differ by colour alone; ◐ is in-progress and degraded in one screen; the red "Cancel item" button is under AA on surface-3; the typed-confirmation note lives in `title` only; fixed px grids will clip at 1.3 text scale. **Riley:** "12 open" includes done rows; the index-failure time differs by page; the bulk-accept button stays enabled above its own toast; `workflow.project` missing from Recipes. **The owner:** Accept on the re-scope does nothing; Attach's semantics unclear at a glance; "import the design output" has no Import action on any screen.

## Minor Observations

"needs you now" repeated under three chips; the failed-service row tinted orange beside a red ✕; two "since" values on the item sheet with no label; the behind-lists kept pre-fix column widths; "Pause in the menu" promised but not drawn; destructive before Keep in confirms; press transform on two pages only; no legend on Recipes; the Node panel stretched with empty surface; "Accept all deterministic" enabled above the toast; "212 queries / 7 d" on the SVG edge; the vertical sheet-tab grip is the one invented affordance; `--ink` and `--ink-muted` near-indistinguishable on dark.

## Questions to Consider

1. If Attach is read-only until a keystroke, why does Home call it a takeover; if it is a takeover, why is it the pre-focused read action? Which sentence should the owner believe at 2 am?
2. Is a `⌂ · terminal` button that does nothing the honest rendering of the protected class, or should it open Warp with the command typed?
3. Has "dense, calm, exact" become "uniform"? Where on each page is the one element allowed to be louder than 13 px?


---

# Critique run 3: Sindri UI revision-8 mockups, after the calm pass

Target: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/` at c613c97e (11 files, re-captured renders). Independent assessment A (blind to runs 1 and 2 and to the fix list) plus the detector (B): 10 files clean, the inspector with the em-dash advisory on tabular nulls only; "Overused font: Inter" ignored as a false positive.

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | The same index failure is a disabled pending control on Home and a live, pre-focused Run again on System; no loading or refusal state drawn |
| 2 | Match system / real world | 3 | "2 running at a desk"; `gate.verify.judge ≥ 0.80` as a state name |
| 3 | User control and freedom | 3 | Attach's first-keystroke takeover has no undo but Hand back; 10 s to undo 31 writes |
| 4 | Consistency and standards | 2 | Running is green on three pages and grey on three; chip words change between a page and its behind-sheet list; the band on 4 of 11 files; "pagerduty" vs "PagerDuty"; group order differs between Workflows, Home and the behind lists |
| 5 | Error prevention | 3 | Protected verbs locked, read-first focus, pending controls dashed; Attach's keystroke and the bulk undo window are the holes |
| 6 | Recognition rather than recall | 2 | Ids hidden where the engineer types them; Live vs Running undefined; the Agents meter hidden at rest; Take over and Cancel behind unlabeled chevrons; no legend for the glyphs that remain |
| 7 | Flexibility and efficiency | 3 | Chords, palette, copyable commands, three bulk verbs; no multi-select |
| 8 | Aesthetic and minimalist design | 3 | Home, the sheets, Context and System earn their pixels; Workflows and Recipes reserve empty action columns and Recipes leaves 45 % of the viewport blank |
| 9 | Error recovery | 3 | The incident Cancel confirm and System's Impact column are exemplary; Home's failure row names no doctor check |
| 10 | Help and documentation | 2 | `? Shortcuts` on three pages only; Recipes' footnote defines columns the table no longer has |
| **Total** | | **27/40** | **Acceptable, top of the band** |

## Anti-Patterns Verdict

Passes every ban: no stripes, gradient text, glass, hero metrics, card grids, modals or em dashes. The remaining tells are structural: subtitle bead chains of `·`-separated fragments, one rhythm (32/24/40) applied regardless of content so Workflows and Recipes read half-loaded, and a glyph vocabulary that travels without its legend. It reads as a Linear-family tool whose density dial was set once and never per page.

## What's Working

- The instance frame: states path, the expanded state's declaration, the NEXT sentence in plain English, Facts as key · line · action, Children, four keyed tabs; identical on the project, the incident and the item.
- Home's needs-you band: five rows, one verb each, the verb's class visible before the click; the five-second test passes.
- System and Context as engineering surfaces, both keeping ids visible, which is the right call there.

## Priority Issues

- **[P1] State chips drift across pages.** Running is `chip ok` on Home, Workflows, System and plain on Work, Agents, the inspector; JE7 is "Live" and JD9 "Running" though both have Attach; behind a sheet the chip word becomes the stage; the Home strip rows have no chip. *Fix:* one chip table in fixture.md, applied verbatim; define Live or merge it. **clarify**
- **[P1] Run again disabled on Home, live and pre-focused on System** for the same failure. One page lies. **harden**
- **[P1] Recipes no longer answers its question.** Runs, done-without-you and median only; the footnote defines columns that are gone; the action column is empty on 7 of 8 rows; 45 % of the viewport blank. *Fix:* Failed, tokens/run p50 and a four-week spark back as mono columns, action column `auto`, footnote rewritten. **layout**
- **[P1] Hidden ids break sentences and shift layout.** `ITM-01JD6` inside the NOW sentence is hidden, so the sentence loses its subject; hovering inserts text before the title; Work and Agents hide ids while System and the sheets show them. *Fix:* ids inside prose are never hidden; a fixed-width id slot per row in `--ink-tertiary` at rest, brighter on hover; hide only on Home's strip and needs-you band. **polish**
- **[P2] The inspector under-counts the fixture** (JD9 and JA1 are workflow.item instances too; "◐ 2" promised in a comment). **harden**
- **[P2] System text defects**: "last 8 of 88" above five rows; "4 engine instances open" against a 12-row table. **clarify**
- **[P2] Agents hides the token meter at rest**, leaving a blank column. **polish**
- **[P2] Workflows reads sparse and drifts**: empty action column, group order, Start… without its ⌘N, "Other recipes" folded into one line. **layout**

## Cognitive Load

3 of 8 failed (moderate): minimal choices (the nav plus Context's eight-control lens row), working memory (Live vs Running, the chip remapping behind sheets, ids hidden on the pages that need them), single focus on Context (two incompatible states drawn at once).

## Persona Red Flags

**The owner:** the id they need in Warp is three interactions away on Home; Run again is fixable on one page and not the other; Live vs Running on Agents; Recipes doesn't answer "where do my recipes stall". **Alex:** Attach's reflexive keystroke; the bulk accept with no preview; no multi-select. **Sam:** `display:none` ids and `visibility:hidden` meters survive only through aria-labels; Context's graph nodes are rects, not links; the 10 s undo is a timed action; the incident mockup draws focus on Attach while the comment says Keep. **Riley:** every empty state is a comment; no needs-you cap at 40 rows; Context draws two incompatible states.

## Minor Observations

Take over offered on a children state with no session; `checks ● r1 failed` uses the done glyph; the Home strip's "… 7 more" vs "all 12 →"; `.key` vs `.id.keep` for the one kept id; the sheet edge tab on two pages only; hint placement varies; the caption pill bottom-left on the inspector only; Cursor's 3 sessions against "not found"; the Build draft hint under two buttons; `✕ 03:00` without a word; the adapt story lives in comments; the Services arithmetic excludes Onyx.

## Questions to Consider

1. The engineer's job ends with an id in a terminal; the pass hid ids on the pages where that happens. What is lost by drawing them at rest in tertiary ink?
2. Calm was implemented as one rhythm. Is calm a rhythm, or a density target per page?
3. Live and Running both mean an agent is working, and Running is green on three pages and grey on three. Is there a third state, or should there be one word?


---

# Critique run 4: Sindri UI revision-8 mockups, after the consistency pass

Target: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/` at cbae68b6 (11 files, re-captured renders). Independent assessment A (blind to runs 1-3 and the fix list) plus the detector (B): clean except the em-dash advisory on tabular nulls in two files; "Overused font: Inter" ignored.

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 4 | Every row carries age, chip and bar; the pending pattern names its API phase; only the sheets' Timeline shows 2 of 41 with no "more" |
| 2 | Match system / real world | 3 | `desk`, `no-progress`, `Child`, `Polling`, "2 running at a desk" need context nobody is given |
| 3 | User control and freedom | 3 | The decision Accept is a one-click primary with no confirm or undo; filters have no active or clear state |
| 4 | Consistency and standards | 2 | "need you" and "waiting on you" name different sets on Home, Workflows, Work and Agents; the band on 3 of 11 pages; Home's health sentence names index and profile, System's names index and Onyx |
| 5 | Error prevention | 3 | Protected locks, inline confirms, read-first focus, bulk undo; the decision Accept is the hole |
| 6 | Recognition rather than recall | 3 | Home's needs-you ids hidden until hover on exactly the rows resolved by a terminal command |
| 7 | Flexibility and efficiency | 3 | Chords, palette, bulk verbs; no multi-select, no filter persistence |
| 8 | Aesthetic and minimalist design | 3 | The calm pass landed; noise remains where a page draws several states at once (Agents, Context) |
| 9 | Error recovery | 3 | System's Impact column is the best moment in the set; no refused or failed action is drawn |
| 10 | Help and documentation | 2 | `? Shortcuts` on three pages; no glossary for `desk` or `no-progress` |
| **Total** | | **29/40** | **Good: solid foundation, address the weak areas** |

## Anti-Patterns Verdict

Not slop: no ban fires, and a Linear-fluent user would trust it. The content is unmistakably a control plane, which clears the "generic Linear clone" anti-reference on information architecture. The second-order reflex ("engineer's control plane, therefore dark Linear") stands as an accepted decision from DESIGN.md, not a failure.

## What's Working

- One frame for the project, the incident and the item: reading one sheet teaches the other two.
- System's Impact column and engine table: cause, consequences, the fix with its API phase and copyable command, retries matching the history rows.
- Numbers that reconcile where checked: Work 2+1+1+4+4 = 12; Recipes' runs sum to 193; engine 8 + 4 = 12; PRJ-sync's children sum to 9.

## Priority Issues

- **[P1] One label, two sets.** "5 need you" (Home) is the union; "3 need you" (Workflows) has two attention rows; "2 waiting on you" is escalated + decision on Work and parked + escalated on Agents. *Fix:* one definition in the fixture; every header count derived from its rows. **clarify**
- **[P1] Recipes' percentages don't reconcile with the inspector.** 81% on the row is 81 of 112 (72%) in the inspector; the page total isn't the weighted mean. *Fix:* one source in the fixture. **harden**
- **[P1] The band and the health sentence differ by page.** Band on three pages while needs-you is 5 everywhere; Home and System give different reasons. *Fix:* band on all or none (drop it: the pill carries the state); one sentence from the pill's reasons. **polish**
- **[P1] The Attach sentence is missing under the primaries.** Present only in menus or under one group. *Fix:* one `.attach-note` under every control row that offers Attach. **clarify**
- **[P2] Render defects:** Home's queued tag shrinks to one letter; fixed sheets stop at 900 px in full-page captures; Timeline shows 2 of 41 with no count link. **polish**
- **[P2] Drawn-open states contradict their page:** Context's toast beside "31 pending" and "46"; Agents' open menu, confirm and focused primary at once. **distill**
- **[P2] Needs-you ids hidden at rest** on the rows resolved by a command. **polish**
- **[P3] "soft 80%" in attention colour on a 29% fill.** **quieter**

## Cognitive Load

3 of 8 failed (moderate): chunking (Context's ten-control lens row, Recipes' nine columns), progressive disclosure (Context shows everything at once, 1,894 px), working memory (two "since" values per item; "waiting on you" changing membership; 81% vs 81 of 112).

## Persona Red Flags

**Alex:** hover-only ids on needs-you rows; "Open instance ▾" is a menu to reach its default; no multi-select; Accept has no key; Run evals only inside the inspector. **Sam:** `role="row"` without a table ancestor behind the sheets; `display:none` ids outside aria-labelled rows; the vertical sheet tab has no landmark; the open menu has no focus management. **Riley:** title truncation order undefined; a ten-state path can start a line with a bare link; no pager for 460 proposals or 41 events; empty states only in comments. **The owner:** five-second test passes; the red row has the weakest affordance; "3 need you" against two chips; pill vs System reasons; "$4.12 / $14 soft 80%" reads as over the limit.

## Minor Observations

Lowercase chip words in source on Agents; "all 12 →" vs the fixture's "… 7 more"; a 76 px empty action column on the Home strip; the P-2211 row's empty id slot; the item sheet's `checks ◐` while the trace says checks finished; "workflow.step · open 6 · no runs yet"; "human rate 19%" not derivable; "$14" vs "$14.00"; `◐` in ink on the graph and in attention in the pill; the middle lens band on s1 reads as selected; the Build draft helper under two buttons; "No intent" as an attention word outside a chip; tabs' kbd 1-4 not listed in Shortcuts; Google Fonts in the mockups, so the Avalonia build must bundle Inter and JetBrains Mono or the renders shift.

**Calm or sparse?** Home, Workflows, Agents, System and the three sheets read calm and scannable. Recipes reads sparse. Context reads long rather than loud.

## Questions to Consider

1. If "need you", "waiting on you" and "with you" cannot share one definition across four pages, does the owner have one queue of human work or four, and which one does the pulse count?
2. The Recipes table says 81% and the inspector says 81 of 112. What in the pipeline guarantees they are the same number, and why is that not already a check like `sync-rules.sh --check`?
3. Every page draws its most useful state open at once. When the Avalonia build shows one state at a time, which affordances will the owner discover without the mockup: a reason to keep the keyboard model, or to put the verbs back on the rows?


---

# Critique run 5: Sindri UI revision-8 mockups, after wave 6

Target: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/` at 423354d9. Independent assessment A (blind to runs 1-4 and the fix list) plus the detector (B): clean except the em-dash advisory on tabular nulls in two files.

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | No in-flight state for any write; no skeleton state |
| 2 | Match system / real world | 3 | `desk` never explained; Temporal and ledger internals leak into prose |
| 3 | User control and freedom | 3 | The decision Accept describes a window with no Undo |
| 4 | Consistency and standards | 2 | Attach has four shapes; the with-you row's actions differ by page; one timestamp reads 2h, 3h and 2h 56m; ITM-01JD6 is `workflow.item` on System and a `builder.feature` child on the project sheet |
| 5 | Error prevention | 3 | System pre-focuses Sync now (a write); Home pre-focuses a chevron; the 10 s undo window |
| 6 | Recognition rather than recall | 3 | The command behind a protected button is visible on Home and System only |
| 7 | Flexibility and efficiency | 3 | No bulk action on Work or Agents; shortcut hints on three pages |
| 8 | Aesthetic and minimalist design | 3 | Context is a wall; Recipes is 60 % empty; a dead "0 proposals · 31 accepted" row |
| 9 | Error recovery | 3 | The Impact panel is the best piece in the set; refusal states only in comments |
| 10 | Help and documentation | 2 | Nothing explains desk, gate, parked, lens or depth where they appear |
| **Total** | | **28/40** | **Good: solid foundation; close the consistency and recall gaps before building** |

## Anti-Patterns Verdict

Not slop; every ban passes in source. Three things would make a Linear-fluent engineer pause: the vertical sheet grip on Home and Workflows, the dashed "Available with API P6" chip in product chrome, and Attach appearing in four shapes across five surfaces.

## What's Working

- The one frame: item, project and incident sheets share States, Facts, Children and tabs; the NEXT sentence is the product's voice.
- System's Impact panel and the pill-to-sentence chain; every graph node a focusable link with a doctor-check label.
- Chips and glyphs are one vocabulary: every chip resolves to the fixture's table; the Recipes and inspector numbers reconcile to the fixture to the digit; nav counts agree on all eleven pages.

## Priority Issues

- **[P1] Attach has four shapes**: a split primary on the sheets, a plain secondary plus a chevron on Agents, a menu-only button on Home's Running rows, a menu item on the incident's child row. *Fix:* one split component in two sizes, main = Attach, chevron = Take over, Request, Cancel. **polish**
- **[P1] Open counts and ITM-01JD6's recipe are not derived from one source.** JD6 is `workflow.item` on System and a `builder.feature` child on the project sheet; the inspector's narrow list shows "open N" the wide table lacks; the Open list omits JD6 and JD2. *Fix:* settle JD6 in the fixture; derive every open count from the instance tables. **harden**
- **[P1] Protected actions with no path to the command** on Workflows, Recipes, Agents and a project child. *Fix:* the lock button opens a popover with the command, Copy and Open in an agent. **clarify**
- **[P2] Three duration formats** for one timestamp. *Fix:* one formatter in the fixture. **polish**
- **[P2] Title loses to the repo tag** in narrow rows; a tag cut mid-word behind the sheet. **layout**
- **[P2] The states band orphans its last node** ("── done ○" alone on a second line). **layout**
- **[P2] Initial focus** on a chevron (Home) and on a write (System); the vertical sheet tab is an invented affordance. **harden**
- **[P2] "Available with API P6" in product chrome**: honest for a reference, wrong in the app; keep the phase in a comment. **clarify**

## Cognitive Load

2 of 8 failed (moderate): minimal choices (Context's lens row, the inspector head) and working memory (protected actions with nothing to copy on three pages).

## Persona Red Flags

**Alex:** Attach is two clicks on Home; no bulk actions; hints on three pages. **Sam:** Home's first focus announces a menu; Context's graph nodes are rects inside one image, unreachable by keyboard, while System's are links; the toast's countdown is unannounced. **Riley:** six attention chips on Home, a count of five (the fixture's open point); "Workflows 12 open" counts four done rows; JD6 changes recipe between pages. **The owner:** the first row's resolution is a terminal round trip with the command behind a chevron; JB4's age differs by page; the degraded sources sit 1,600 px below the fold on Context.

## Minor Observations

The strip's "… 7 more" absent; the health sentence coloured on Home and plain on System; "mitigate ⇄ verify" vs "mitigate r1/3"; a Subject filter with no subject column; a 240 px void on the Other recipes rows; the with-you row's primary Attach on Agents; three heading-count treatments; "Request" vs "File a request"; the Recipes footnote; twenty null cells in the inspector's per-state table; the Build draft helper under two buttons; the dead post-accept row; "◀── anchor"; the saturated "N ev" column on System; 40 px rows against the brief's 36.

## Questions to Consider

1. If "needs you" has one definition, why does Home draw six attention chips and count five? Count the desk answer, or don't chip it.
2. "Workflows 12 open" includes four done triage rows. What does "open" mean, and what does the owner do with a done row that is still open?
3. The headline is one click into a live session, yet the first row of Home, the escalated row on Workflows and the changed recipe all resolve by leaving the app for a terminal. Why not let the protected confirmation be typed into ⌘K inside the app?
