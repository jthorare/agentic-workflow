# Spec: Run detail, the stage canvas (Avalonia build spec)

Reads: `screens/run-canvas.html` (+ notes, questions, PNGs), `specs/context.md` (owns `ZoomBorder` and the edges control), `data/*`. Verified against `origin/main` @ b6f564402. Paths below are under `src/Sindri/Aw.Sindri.Ui/` unless they start with `tests/`.

## 1. Goal and the approved decisions

A page for ONE run of a line workflow ("Idea to merged pull request"): RUNS sidebar, a fixed-layout canvas of stage cards (each lists its steps) joined by labelled edges, a zoom pill, a minimap, and a 480 px inspector for the selected step. Read-only: no wiring, no palette, no node dragging, no edit mode.

| Q | Approved answer | Effect here |
|---|---|---|
| 1, 9 | Sentence case end words: Done, Failed, Didn't work out, Cancelled, Waiting for you, You took over, Paused, needs you. Same word in header badge, runs card, stage footer, inspector badge | one `RunWords` map from chip word to display word (section 4) |
| 2, 10 | Runs for end states and generated canvases are mockup samples | nothing to build; fixtures only (section 7) |
| 3 | Runs sitting at a stop or with you link to Needs you, not the canvas | runs card click for a row whose chip is Decision/Parked/Escalated/With you opens `needs-you/<row>` |
| 4 | Take over goes to the takeover view, the clock pauses there | button navigates; it does not send the command from here |
| 5 | "See its context" goes to the item's map from every step, stage and failure | navigation to Context with the item as start (context spec 1a, breadcrumb) |
| 7 | Plain edge labels from a lookup on the outcome | `EdgeLabels` table; raw outcome shown only for an unknown outcome |
| 8 | Open at the active stage at 100%, card fully visible, incoming edge and pill in view; `F` fits the run | `RunCanvasViewModel.FocusActive()`, `Fit()` |
| 6 | State switcher in header is a mockup control | NOT built |

## 2. Regions and layout

Page = the `Workflows` page's detail route `workflow/<id>` for an instance whose recipe is a line (has wait-child states). Rail/top-bar chrome is the shared shell spec's (workflows spec), not this one. Today's equivalent: `Views/Instance/InstanceFrameView.axaml` (header, `StatesBand`, `ExpandedStateView`, `FactsPanel`, `ChildrenPanel`, `FrameTabs`) in the 520 px detail sheet. The frame stays for every other recipe and for the sheet; the run page is a NEW view for line runs and REUSES: `InstanceFrameViewModel` (loading, SSE reread via `workflow.instance-changed`, commands), `StateChipView`, `CommandControl`/`ControlViewModel` (Cancel, Take over, Hand back), `ProtectedActionButton`, `FactsPanel` rows, `ChildGroupViewModel`.

New view `Views/Run/RunPage.axaml` (VM `RunPageViewModel` in `ViewModels/Run/`), `Grid ColumnDefinitions="Auto,*,Auto" RowDefinitions="52,*"`:

| Region | Size, spacing, tokens | Avalonia |
|---|---|---|
| Header (row 0, spans) | 52 px, `Surface1`, bottom `Hairline`; item title 16/600 (`Type.DetailTitle` closest), status badge, "Started 9h 35m ago" (12, `InkSubtle`, tabular), reference chip (11 mono `InkSubtle`, `Surface2`, `Hairline`, `Radius.SM`, copy on click), spacer, Cancel run | `Grid`; badge = `StateChipView` restyled per section 3; Cancel = `CommandControl` for `ApplyCommand:cancel` |
| RUNS sidebar (col 0) | 280 px, `Surface1`, right `Hairline`; header "Runs" eyebrow; search 32 px (`Surface2`); cards 8 px gap, radius `Radius.SM`, padding 10/12, selected `Surface3` + `HairlineStrong` | `ItemsRepeater` (fixed card height 56) in `ScrollViewer`; below 1200 hides behind a "Runs" button (overlay 300 px) |
| Canvas (col 1) | flex; `Canvas` token background with 22 px dots in `HairlineStrong` (`DrawingBrush` tile that moves with the transform, as context 2) | context's `ZoomBorder` hosting `RunCanvasWorld` (below) |
| Zoom pill | bottom-left 16/16, 28 px, `Surface3`, `Radius.SM`: minus, plus, "100%" (tabular, min 44 wide), fit, centre on active step | `StackPanel` of `Button`s (as context 2c) |
| Minimap | bottom-right 16/16, 168 x 132, `Surface1`, `Hairline`, `Radius.MD`; caption 11 `InkSubtle` | `MiniMapControl` (NEW own control, below) |
| Inspector (col 2) | 480 px at >= 1200, a flex sibling; 440 px overlay below 1200 (canvas re-centres on the selected step in the visible part; minimap hidden while it is open); `Surface1`, left `Hairline` | `RunInspectorView` (NEW; replaces `ExpandedStateView` for this page, not for the frame) |

### Stage card (`StageNodeView`, 320 px wide)
`Border` radius `Radius.LG` (12; the prototype's 16 is context open item 2, same answer), `Surface1`, 1 px `Hairline`, no shadow (app rule). Inside: header Grid (padding 14/12; 28 px icon tile `Surface3`, `Radius.SM`; title 14/600; sub 12 `InkSubtle`), `ItemsControl` of step rows on a `Surface2` strip (padding 10/14, top `Hairline`), status footer (padding 8/14, 11/500, tinted per section 3). Step row = real `Button`-templated control (focus order, automation peer), min height 28, padding 6, gap 8: number circle 16 (11/600), name 12/500 (+ optional 11 `InkSubtle` second line), try caption 11 on the right, kind glyph 13, status glyph 14. Selected card or row: 4 px `PrimaryHover` ring with 2 px offset (reuse the app focus ring geometry, context 4). Ports: 10 px squares radius 3, 1 px `Wire`, fill `Surface1`, drawn by the edges control, not child controls. Side nodes (end and redesign cards) 280 px, same card without the step strip.

### World, edges, pills
- `ZoomBorder` (context 2, `Controls/ZoomBorder.cs`) is REUSED with two additions: `MinZoom`/`MaxZoom` properties (context hard-codes 0.62 to 1.0; this page sets 0.30 to 1.50) and `FitBounds` (a `Rect` property, today a fixed 1360 x 872 world). Pointer, wheel (pans), Ctrl/Cmd+wheel (zooms around cursor) and `Fit()` stay as context defines them.
- Edges: REUSE context's `ContextEdgesControl` (renamed `GraphEdgesControl` by whichever slice lands first; this spec's slice 1 does it if context slice 1 has not). Required by this page and added to its edge record if missing: start and end `PortSide` (Top, Bottom, Left, Right) so tangents are horizontal or vertical, stroke brush key, dash pattern, hit-test width. Curve rule (from the prototype's `bez()`): cubic, control offset `k = clamp(dist/2, 40, 110)` along each port's outward normal; label pill at t = 0.5, computed in the VM (`EdgeGeometry.Midpoint`, pure).
- Pills: `Border` 11/500, `Surface3`, `Hairline`, `Radius.SM`, padding 2/6 (prototype `--card`); failed-path pill text `Error`. Always shown.
- Edge colour rule (VM computes `EdgeTone`): see table.

| EdgeTone | When | Stroke |
|---|---|---|
| Normal | both ends reached, or neither reached and edge is on the main path | `Wire` 2 px solid |
| Active | source done and target is the active stage | `PrimaryHover` 2 px solid |
| Failed | an end edge whose source stage is failed | `Error` 2 px solid; pill text `Error` |
| Dim | not taken, not reachable yet, loop and stop edges, or source ended failed/didn't work out/cancelled | `Wire`, dash `6 5`, opacity 0.7 |
| Hover | pointer over a card: its edges | `WireHi` (context token) |

Hover rule: `:dim` pseudo-class as context 2 (opacity 0.42 outside the node, its edges and neighbours).

### MiniMapControl (NEW, `Controls/MiniMapControl.cs`, ~200 lines)
`Render` override: scale = min((W-16)/worldW, (H-16)/worldH), centred; one rounded rect (radius 2) per node filled by section 3's state brush; selected node outlined 1 px `Ink`; viewport rect 1.5 px `Ink` with `Ink` at 6% fill from the `ZoomBorder` matrix and the stage size. Pointer down or drag centres the viewport on the point (sets the matrix). Not focusable; the zoom pill and `F` are the keyboard path. Has an `AutomationProperties.Name` only.

### Deterministic layout (`RunCanvasLayout`, pure, in `ViewModels/Run/`, unit tested)
Input: `RunGraph` (nodes with id, kind Stage|Side|End, order, branchOf; edges with from, to, outcome), and `Dictionary<NodeId, Size>` of measured heights (Avalonia `Measure` of each card in a first pass; the function itself never measures). Output: `Dictionary<NodeId, Rect>` plus the world `Rect`.
Constants: `StageW 320`, `SideW 280`, `Gap 104`, `Pitch = StageW/2 + SideW/2 + 170 = 470`.
1. Main path = nodes with `order != null && branchOf == null`, sorted by `order` then id. Column 0 (centre x = 0), `y0 = 0`, `y(i) = y(i-1) + h(i-1) + Gap`. Width `StageW`.
2. Loop nodes = non-main nodes that have an edge back to a main node of lower or equal order, found in recipe-id order. Place in column +1 (centre x = +Pitch), `y = source.bottom - 60`. (Prototype: Redesign.)
3. End nodes = nodes with no outgoing edge that are not on the main path. Row y = `last main bottom + Gap`. Columns by outcome class, ties by recipe order then id: resolved/alreadySatisfied at column 0; unresolved and failed at +1, +2, ...; cancelled/stopped (and any end every stage can reach, such as Stopped) at -1, -2, ... Width `SideW`.
4. Ports: forward main edge Bottom to Top; edge to a column +n node from the source's Right to the target's Top (to a loop node: Right to Bottom); the return edge from a loop node Left to the main target's Top; edges to a column -n node from the source's Left to the target's Top.
5. World rect = union of node rects, padded 120 px. `FocusActive()` sets zoom 1.0 and centres the active stage horizontally in the visible width (inspector subtracted below 1200) with its top at `max(84, (stageH - h)/2)`; `Fit()` zoom = clamp(min((w-60)/worldW, (h-60)/worldH), Min, 1.0).
Golden test: the prototype's running sample (4 stages, Redesign, Merged, Stuck, Stopped) yields the exact rects the prototype computes with its `layout()` for the same heights.

## 3. States

State words and tokens, one table for header badge, runs card, stage footer, step glyph, minimap and inspector badge. Existing brush names only except the NEW row.

| Word | Reached from | Glyph | Text/glyph | Fill (soft) | Minimap |
|---|---|---|---|---|---|
| Running | chip RUNNING, glyph Active, StepRun `running` | spinner (`Motion.Spin`; steady ring under reduced motion) | `PrimaryHover` | `PrimarySoft` | `PrimaryHover` |
| Done | chip DONE (outcome resolved / alreadySatisfied), glyph Done, StepRun `passed` | check | `Success` | `SuccessSoft` | `Success` |
| Failed | chip FAILED (tone fail), StepRun `failed` or `refused` | cross | `Error` | `ErrorSoft` | `Error` |
| Didn't work out | chip DIDN'T WORK OUT (tone attn) | minus in circle | `Attention` | `AttentionSoft` | `Attention` |
| Cancelled | chip CANCELLED, StepRun `cancelled` or `abandoned` | cross | `InkMuted` | `Surface2` | `InkSubtle` |
| Waiting for you | chip DECISION, or an Active cell with `desk` and no badge | clock | `Attention` | `AttentionSoft` | `Attention` |
| Paused, needs you | chip PARKED / ESCALATED (badge reason in the sub line) | clock | `Attention` | `AttentionSoft` | `Attention` |
| You took over | chip WITH YOU, badge `taken-over` | person | `Color.Violet` `#a78bfa` (NEW in workflows.md) | `Color.VioletSoft` `#170f2a` (NEW in workflows.md) | `YouTookOver` |
| Not started | glyph Pending, chip QUEUED | empty ring | `InkSubtle` | `Surface2` | `HairlineTertiary` |

No new colour tokens of its own: "You took over" uses `Color.Violet` `#a78bfa` and `Color.VioletSoft` `#170f2a`, added by workflows.md slice 1 (one token pair for one status, settled by ideas 2026-10-20). Also consumed from the context spec (NEW there; whichever PR lands first adds them): `Color.Wire` `#4a4a52`, `Color.WireHi` `#8a8f98`. Not new: running edge is `PrimaryHover` (prototype `#818cf8`), failed edge `Error` (prototype `#f87171`), dots `HairlineStrong`, selected ring `PrimaryHover`, inspector header `Surface1` (prototype `#101113`). Tone words and glyph always appear together (never colour alone). Colour change means `TokenTests` (key set, `Brush.*` mirror), `ContrastTests` (`Violet` on `VioletSoft` checked in workflows.md, `Wire` pairs as context), `FluentOverrideTests` untouched.

Note on the chip tone: DIDN'T WORK OUT is amber (owner 2026-10-20, the 2026-10-19 table): the run page uses the served `attn` tone; no word override.

Page states (VM `RunPageState`):

| State | How reached | What shows |
|---|---|---|
| Loading | route opened, `Instance(id)` pending | skeleton cards in the layout positions, runs list loads in parallel |
| Running | default | active stage in focus at 100%, the active step selected only if `?step=` or a click |
| Waiting / Paused / You took over | chip word per table | active step row and stage footer use that row's tone; inspector acts per section 5 |
| Failed | chip FAILED | failed stage footer red with word; the failed step row red; the edge from the failed stage to its end card is `Failed`; the end card reads the end word |
| Didn't work out / Cancelled | chip words | footers amber for Didn't work out, neutral for Cancelled; Cancel run hidden; header "Run took 11h 02m"; edge to the reached end card solid `Wire`, other ends not drawn (section 4 fallback) |
| Done | chip DONE | all stages Done; the resolved end card "Merged" (display name) Done |
| Not found / unreadable | refusal SND-ITEM-404, SND-UI-002/003 | the existing frame's not-found and unreadable cards (`InstanceFrameViewModel.Failures`) |
| Stale | SSE gap | existing stale banner; page keeps its last frame |

Narrow (< 1200 px, down to the 900 px window min): runs behind a button, inspector overlays. Keys section 5.

## 4. Data

Run model: the page is the LINE instance (`workflow.line`). Each stage = a state of the line with `StateCell.Child == true`; its child instance(s) are `Children[group.State == stage].Rows[]` (`ChildRow.Address`, `Id`); the step list of a stage = `InstanceDetail.States` of that child (read with `GET /sindri/workflow/instances/{childId}`), run facts from the child's `StepRuns`.

| Region / field | Source | Status |
|---|---|---|
| Run header: title, ref, started, status word | `GET /sindri/workflow/instances/{id}` (`Ui/Api/Workflow/InstanceDetail.cs`): `Title`, `Address`, `Since`, chip words from the ROW (`ChildRow.Chip`, `InstanceSummary.Chip`) | SERVED: golden `tests/Aw.Sindri.Tests/Golden/Api/workflow-instance/project-build.json`, rows `tests/Aw.Sindri.Tests/Golden/Api/workflow-instances/root.json` |
| Chip words FAILED / DIDN'T WORK OUT / CANCELLED, tone `fail` | `Core/Workflow/Views/StateChip.cs` (`StateChip.For`, merged, owner table 2026-10-19); golden `tests/Aw.Sindri.Tests/Layout/Golden/instance-summary-failed.json` | SERVED. The UI's `Api/Workflow/StateChip.cs` already carries `Word` and `ChipTone.Fail`; `RunWords` maps the word to the sentence-case display word |
| Workflow display name ("Idea to merged pull request"), stage names | `RecipeView.DisplayName`, `InstanceDetail.DisplayName`, `ChildGroup.DisplayName` (#468 and follow-ups; `Ui/Api/Workflow/RecipeView.cs`, `ChildGroup.cs`) | SERVED, but nullable (served as null when none is known). Fallback: `WorkflowNames` title-cases the id (existing `ViewModels/Workflows/WorkflowNames.cs`) |
| End card names ("Merged", "Stuck", "Stopped", "Redesign") | none: display names exist for recipes, not for states | PENDING: engine, `RecipeStateView.displayName`. Fallback: the existing `Labels.OutcomeWord(outcome)` for the reached end; a state id title-cased for the rest |
| Stage cards and order | parent `States[]` (`StateCell`: `State`, `Order`, `BranchOf`, `Glyph`, `Badge`, `Round`, `Child`, `Terminal`, `Outcome`) | SERVED (same golden) |
| Step rows (names, status glyph) | child `States[]`, `Glyph` Done/Active/Pending, `Badge`, `Desk`, `Round{N,Max}` | SERVED. Step display names: PENDING (engine `RecipeStateView.displayName`); fallback title-cased id (`produce` -> "Produce") |
| Step kind glyph (AI agent, Automatic, You, Only you, Sub-workflow) | partial: `StateCell.Child` = Sub-workflow, `StateCell.Desk` = You, `StepRun.Runtime` `agent` = AI agent, `desk` = You | PARTIAL. Automatic and Only you: PENDING (engine `RecipeStateView.kind`). Fallback: no glyph, no invented kind |
| Step runs: tries, start, end, elapsed, actor, tokens, outcome | `InstanceDetail.StepRuns` (`Ui/Api/Workflow/StepRun.cs`: `Token, Step, Attempt, MaxAttempts, State, Runtime, StartedAt, EndedAt, DurationMs, Outcome, Tokens, Actor`); #469; goldens `workflow-instance/item-produce.json` (a `refused` and a `running` run). The UI client reads it, no view model uses it yet | SERVED. A step with no run shows the "no times" copy |
| `runs[]` for a step | `StepRuns.Where(r => r.Step == stateId)` ordered by `Token` | SERVED (client filter) |
| Elapsed on the live step | `StartedAt` (served) to the response's `AsOf` plus the local clock since receipt | SERVED |
| Why this step matters (per state) and per stage | none | PENDING: engine F5, `RecipeStateView.why`, and a recipe-level `why` for stage cards. Fallback: the block stays with `Labels.RunWhyNotServed` |
| Edges with plain labels | `RecipeView` serves `States[]` and `Loops[]` only (`Ui/Api/Workflow/RecipeView.cs`, goldens `Golden/Api/workflow-recipe/*.json`): no transitions | PENDING: api + engine F5, `RecipeView.transitions[] {from, on, outcome, to}` and the `EdgeLabels` table (lookup on outcome, ui-owned text). Fallback: the main chain from consecutive `order`, loops from `Loops[] {From, To, Cap}` drawn Dim with pill `Labels.RunLoopPill(cap)` ("again, up to 4 times"), only the REACHED end card drawn (from `StateCell.Terminal` and `Outcome`), plus one line under the canvas legend `Labels.RunBranchesNotServed`. No pill text is invented |
| What it needs / What it produced | the stage's facts: child `Facts[]` (`FactRow`: `Label`, `Direction`, `Status` produced/in-progress/pending, `Summary`, `At`); the current step's inputs `Current.Status.Inputs[]` (`StatusInput`) | SERVED per workflow; per step: PENDING (engine F5, step inputs/outputs on `RecipeStateView`). Until then both lists show the stage's facts, headed "For the whole stage" (`Labels.RunFactsWholeStage`) |
| What went wrong (failed or ended step) | `GET /sindri/workflow/instances/{id}/events` (`ISindriApi.InstanceEvents`, `InstanceEventFields.Reason`, trigger `step.failed`) for the run's token; park reasons from `StateCell.Badge.Reason` | SERVED (no success golden for the events route yet: the ui lane adds `SpecShaped/workflow-instance-events/failed-run.json`). Fallback: the outcome word and `Labels.RunNoDetail` |
| Next actions on a failed or ended run | `InstanceDetail.Actions[]`, `Current.Status.Commands[]` | SERVED as lists; whether takeover or a re-run is OFFERED on a terminal instance is PENDING: engine (F5 failed-next-actions). Fallback: only "See its context" |
| RUNS sidebar: runs of this workflow, status word, elapsed, item title | `GET /sindri/workflow/instances?root=true&recipe=<recipeId>` (`InstanceQuery`, `InstanceSummary`: `Address`, `Id`, `ItemId`, `Chip`, `Since`, plus item title via `Title` on detail) | SERVED for open rows; finished rows: PENDING: api, `recipe` filter with `open=false` history (`run-list-by-workflow`). Fallback: list the open runs plus the current one, `Labels.RunsOpenOnly` line under the list |
| Work allowance (tokens) | step: `StepRun.Tokens` summed; limit: `Current.Status.Budget {limit}` for the CURRENT step and instance only | PARTIAL. Other steps show used only ("61k used") |
| Live updates | SSE `workflow.instance-changed` (#467, `Ui/Api/InstanceChange.cs`, `InstanceListener`): reread the line and the child whose address matches | SERVED |
| Minimap, zoom, layout | UI only | no API |

Not-served rule (CURRENT-UI.md pattern): never fake data; a field marked PENDING renders its fallback or the not-served line in `InkSubtle`, never an empty gap. Sample-only text from the prototype ("06:10 to 09:06", the failed-run timings, `x_stepRuns`) is NOT used.

## 5. Actions and keys

| Button / key | Action id (catalog) | Notes |
|---|---|---|
| Cancel run (header) | `ApplyCommand:cancel` (desk, ready) | `CommandControl`; the served confirm sentence (`StatusCommand.confirm`) in the existing confirming phase; hidden unless the instance's `Actions` carries it |
| Take over (step, stage, failed block) | none sent here; navigates to the takeover view of the instance that owns the step. The control there sends `ApplyCommand:takeover` (class `pause`) | follows the needs-you spec; this page only opens `needs-you/<row>` |
| Hand back (You took over) | navigates to the same view, which holds `ApplyCommand:handback` (desk) | same |
| Decide in Needs you (Waiting for you) | none; opens `needs-you/<row>` | |
| Approve the merge (an "Only you" step) | the step's own `Current.Status.Answer.ActionId` + `Class` + `Cli` (`StatusAnswer`) | protected class: `ProtectedActionButton`, "terminal" copy block, disabled until the step is the current one; typed approvals are not on this page |
| Run again (failed or didn't work out) | Settled (api + engine, 2026-10-20): a PARKED failed step uses the existing answer-text / answer-resume / grant rows (no new id); a top-level run that ENDED failed = a new `workflow start` with the same inputs (works today; engine's optional `again: <address>` only adds the "again of <old run>" link). The gate is whether ui can rebuild the old run's inputs from what's served (facts may be refs, not values): ui settles that with engine at run-canvas slice 5; a failed child gets no button | parked: SERVED rows; ended-failed top-level: not drawn until slice 5 settles the inputs; child: never |
| See its context | navigation, not a catalog action: Context page, start = the item (context spec 1a) | from every step, stage and failure |
| Copy reference, Copy link | local (no catalog id) | |
| Runs card | navigation: `workflow/<id>` or `needs-you/<row>` per Q3 | |
| Zoom minus / plus / fit / centre | local | step 1.15; centre = `FocusActive()` |
| Click step row or stage header | select; inspector opens | |
| Click empty canvas, `Esc` | clear selection; inspector closes (existing sheet Esc rule) | |
| `J` / `K` | next / previous step in reading order (stages top to bottom, steps within) | active only with canvas or inspector focus, so Workflows' row `j/k` keeps working elsewhere |
| `F` | fit the run | |
| `+`, `-`, `0` | zoom in, out, fit (context 5) | |
| Drag, wheel, Ctrl/Cmd+wheel | pan, pan, zoom (0.30 to 1.50) | |
| `Tab` | header, runs, canvas steps in reading order, zoom pill, inspector | step rows are real controls; the edges control is excluded and summarised by `AutomationProperties.HelpText` on each card ("Leads to Design when the scope is approved") |

Add the page's keys to `ShortcutsSheet` and `ChordKeysTests`' expected list.

## 6. Copy (new `ViewModels/Labels.RunCanvas.cs`; no em dashes, no internal terms)

| Key | Text |
|---|---|
| `RunsTitle` / `RunsSearch` | "Runs" / "Search runs" |
| `RunsOpenOnly` | "Only runs that are still open are listed here." |
| `RunWordDone` | "Done" |
| `RunWordFailed` | "Failed" |
| `RunWordDidntWorkOut` | "Didn't work out" |
| `RunWordCancelled` | "Cancelled" |
| `RunWordWaiting` / `RunWordPaused` / `RunWordTookOver` | "Waiting for you" / "Paused, needs you" / "You took over" |
| `RunWordRunning` / `RunWordNotStarted` | "Running" / "Not started" |
| `RunStarted(ago)` / `RunTook(span)` | "Started {ago} ago" / "Run took {span}" |
| `StageOf(n, total)` | "Stage {n} of {total}" |
| `StepOf(stage, i, n)` | "{stage} stage, step {i} of {n}" |
| `StepTry(n, max)` | "Try {n} of {max}"; no max: "Try {n}" |
| `KindAgent` / `KindAutomatic` / `KindYou` / `KindOnlyYou` / `KindSub` | "AI agent" / "Automatic" / "You" / "Only you" / "Runs a sub-workflow" |
| `RunWhy` | "Why this step matters" |
| `RunWhyNotServed` | "Why this step matters isn't written down yet." |
| `RunNeeds` / `RunProduced` / `RunTimeline` | "What it needs" / "What it produced" / "Step timeline" |
| `RunFactsWholeStage` | "For the whole stage" |
| `FactReady` / `FactInProgress` / `FactNotYet` / `FactNotProduced` | "Ready" / "In progress" / "Not yet" / "Not produced" |
| `RunNoTimes` | "Its start and end times aren't recorded." |
| `RunNotStartedYet` | "It starts when the steps before it finish." |
| `RunWentWrong` / `RunHowItEnded` | "What went wrong" / "How it ended" |
| `RunNoDetail` | "No more detail was recorded." |
| `RunNext` | "What you can do next" |
| `RunTakeOverTitle` / `RunTakeOverHelp` | "Take over and fix it yourself" / "Make the change, then hand it back. The clock pauses while you have it." |
| `RunSeeContext` / `RunSeeContextHelp` | "See its context" / "The feature, screens and files this change touches." |
| `RunAgain` / `RunAgainHelp(stage)` | "Run again" / "Starts the {stage} stage from the beginning." |
| `RunTakeOver` / `RunHandBack` / `RunDecide` / `RunCancel` | "Take over" / "Hand back" / "Decide in Needs you" / "Cancel run" |
| `RunTerminal(label)` | "{label} · terminal" |
| `RunRawOutput` / `RunTechnical` | "Raw output" / "Technical details" |
| `RunBranchesNotServed` | "Branches and their labels aren't shown yet." |
| `RunLoopPill(cap)` | "again, up to {cap} times" |
| `EdgeLabels` | outcome to text lookup, ui-owned: `scoped` "scope approved", `designed` "design approved", `planned` "plan approved", `resolved` "merged" (per recipe end), `unresolved` "could not finish", `redesign` "needs a redesign", `cancelled` "stopped"; unknown outcome shows the raw word |
| `ZoomIn` / `ZoomOut` / `ZoomFit` / `ZoomCentre` | "Zoom in" / "Zoom out" / "Fit the run" / "Centre on the active step" |
| `MinimapName` | "Overview of the run" |
| Close / previous / next | "Close (Esc)" / "Previous step (K)" / "Next step (J)" |

`EdgeLabels` keys are provisional until the engine serves transitions; the table lives in one `Labels` method and a test pins that no key leaks an internal term.

## 7. Tests the ui lane must add or change

| Test class | Change |
|---|---|
| `TokenTests`, `ContrastTests` | `Violet`/`VioletSoft` come from workflows.md; add `Wire`, `WireHi` only if this lands before context slice 1; key set and `Brush.*` mirror; text pairs >= 7:1; `InkTertiary` stays disabled-only |
| `FluentOverrideTests` | no change (nothing reads the new keys) |
| NEW `RunCanvasLayoutTests` | prototype golden rects (4 stages, 3 end cards, loop node); a taller card pushes every card below it; end column rule per outcome; a recipe with no terminal draws no end row; deterministic on repeat |
| NEW `EdgeGeometryTests` | control offset clamp 40 to 110; midpoint; each `PortSide` pair gives the expected tangent |
| NEW `ZoomBorderTests` (context's; extend) | `MinZoom/MaxZoom` 0.30 to 1.50 clamp; `FitBounds` fit; zoom around cursor |
| NEW `RunWordsTests` | every chip word (Running, Done, Failed, DIDN'T WORK OUT, CANCELLED, DECISION, PARKED, ESCALATED, WITH YOU, QUEUED) maps to the table's word, glyph and brush keys; DIDN'T WORK OUT maps to `Attention`/`AttentionSoft` (served tone attn) |
| NEW `RunPageViewModelTests` | state per fixture (running, waiting, took over, failed, didn't work out, cancelled, done, loading, not found, stale); runs list narrows to the recipe; `workflow.instance-changed` rereads only the matching instance; step selection by click, `J`/`K`, `Esc`; `FocusActive` and `Fit` values |
| NEW `StepRunsProjectionTests` | StepRuns to rows: attempt "Try 2 of 3"; `refused` reads Failed with the check sentence; `running` elapsed from `StartedAt`; step with no run shows `RunNoTimes`; actor `agent:session` reads "AI agent", `human:*` reads "You" |
| NEW `RunFallbackTests` | with `RecipeView` carrying no transitions: main chain, loop pill from `Loops`, only the reached end card, the not-served line; `displayName` null falls back to `WorkflowNames` |
| NEW `RunInspectorViewModelTests` | blocks present per state; Why not-served line; facts headed "For the whole stage"; actions per section 5 (Take over only navigates; protected answer renders `ProtectedActionButton`) |
| NEW `RunPageFramesTests` (headless, `AW_UI_FRAMES=1`) | frames at 1440 and 900 for running, failed, didn't work out, cancelled, with the inspector open; `RealFontTextFitTests` row for 11 px captions |
| `NamedControlsTests` | add `RunPage`, `RunsList`, `RunCanvas`, `RunZoom`, `RunMiniMap`, `RunInspector`, `RunHeader` |
| `AutomationPeerTests` | step rows and stage cards expose name, state word and kind; minimap has a name only |
| `ActionLabelsTests` | no new ids from this page unless "Run again" lands (then its catalog golden row and a unique label with no ':') |
| `ShellNavTests`, `ShellChordTests`, `ChordKeysTests` | the `workflow/<id>` deep link opens the run page for a line instance, the frame sheet for others; shortcut rows added |
| `InstanceFramesTests`, `InstanceFrameViewModelTests` | unchanged for non-line recipes; a line instance no longer opens in the sheet from the Workflows page (it opens the run page) |
| `WorkflowGoldenTests`, `SindriApiClientWorkflowTests` | add `SpecShaped/workflow-instance-events/failed-run.json` for the events read; later `recipe-transitions` golden when the engine serves them |

Fixtures: `tests/Aw.Sindri.Ui.Testing/FixtureInstances.cs` gets a line run for each state above, built from the engine's real goldens (`project-build.json`, `item-produce.json`), not from the prototype's invented timings.

## 8. Build order

1. Tokens, `ZoomBorder` extensions and the edges control. Red first: `RunWordsTests.Every_chip_word_maps_to_one_state_style` (does not compile until `RunWords` exists as a stub), `TokenTests.Every_colour_is_one_of_the_spec_tokens` (fails on the new keys), `EdgeGeometryTests.Control_offset_is_clamped_to_40_110`, `ZoomBorderTests.Zoom_clamps_to_the_configured_range`. Proves: tokens pinned, the shared controls take the run page's ranges and port sides, no page change. If the context slice 1 already merged, this slice only adds the extensions.
2. Layout and graph model. Red first: `RunCanvasLayoutTests.Running_sample_matches_the_prototype_rects`, `RunFallbackTests.Missing_transitions_draw_the_main_chain_and_the_reached_end`. Proves: `RunGraph` built from `InstanceDetail` + `RecipeView` + child details, `RunCanvasLayout`, edge tones, the fallbacks; no view yet.
3. Page, stage cards, runs list, zoom pill, minimap, selection and keys. Red first: `RunPageViewModelTests.Opens_at_the_active_stage_at_100_percent`, then `RunPageFramesTests.Running_frame_1440`. Proves: a line run opens the new page from Workflows, SSE rereads, J/K/Esc/F, the minimap drives the matrix; header Cancel works through `CommandControl`.
4. Inspector with the step timeline from `StepRuns`. Red first: `StepRunsProjectionTests.A_refused_run_reads_failed_with_the_check_sentence`, `RunInspectorViewModelTests.Take_over_navigates_and_sends_nothing`. Proves: all blocks, Why and facts fallbacks, protected approve, details expanders (`Expander`), narrow overlay behaviour.
5. Failed, didn't-work-out and cancelled states plus the events read. Red first: `RunPageViewModelTests.A_failed_step_shows_the_reason_from_the_events_and_a_red_edge_to_the_end`. Proves: end words, the failed edge tone, "What went wrong" from `InstanceEvents`, next options limited to served actions.

## 9. Open items for the ui lane

1. DIDN'T WORK OUT tone: SETTLED by the owner 2026-10-20: amber, the served `attn` tone; no override.
2. **Settled by ideas:** a phase with several children lists the first child that is not Done, shows `ChildGroup.Done/Total` in the footer, and adds a quiet "Show the other N phases" row that opens the existing `ChildrenPanel` (today the line runs one phase, so this rarely shows).
3. "Approve the merge" and every other protected step: the exact catalog id comes from `StatusAnswer.ActionId` per instance; verify it exists for the line's last step (`CompleteStep` is `desk` in the catalog, but `HumanOnlySteps` makes it terminal-only).
4. Whether a failed (terminal) instance serves `takeover` in `Actions[]`; the prototype offers it. Engine F5 decides; the UI draws only what `Actions` carries.
5. Card radius 12 vs the prototype's 16, same as context open item 2; and the prototype's 85% default zoom is not built (we open at 100%, Q8).
6. A line run with many stages beyond the recipe's four (a different recipe): the layout handles any count, but the minimap and `Fit` floor at 30%; confirm that is readable.
7. The finished-runs list needs the api `recipe` + history query (`run-list-by-workflow`); until then only open runs appear and the user reaches finished ones from Workflows.
8. Shell chrome (rail, header 56 vs 52 px, page title) belongs to the workflows and shell specs; this spec assumes a 52 px page header inside the existing top-bar shell.

## Answered by ui (2026-10-20, ui digest ~/.agentic-workflow/digests/sindri-ui-f3-spec-run-canvas.md)
- No hard-coded merge id: render the served `StatusAnswer`, else terminal copy (open item 3).
- Card radius 12 (open item 5); `Fit` floor 30% accepted (open item 6).
- ui adds the `InstanceEvents` fixture.
- Run again: see the Actions row (parked = existing rows; ended-failed top-level = new start with the same inputs, gated on rebuilding them from served facts, settled at slice 5 (`again:` only adds the link); child none).

## Owner override (2026-10-20)
- OWNER 2026-10-20 (via meta): DIDN'T WORK OUT is AMBER, per the 2026-10-19 table; this overrides the earlier "neutral" settlement. FAILED stays red, CANCELLED stays neutral. ui uses the served `attn` tone (api #504 matches). It keeps its own glyph (minus in circle) and word, so it never reads as "needs you" by colour alone.
