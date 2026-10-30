# run-canvas: notes for the ui lane

Screen: run detail of one run of "Idea to merged pull request" (workflow.line), SuperPlane automation layout.
Files: run-canvas.html (open `#fail` for the failed state, `#ann` for annotations), run-canvas-1440.png, -900.png, -failed-1440.png.
The "Running run / Failed run" segmented control in the header is a mockup switch only (the real switch is clicking a run card in RUNS).

## Regions (1440 x 900)
| Region | Size / tokens |
|---|---|
| App rail | 56 px, `--card-warm`, 7 pages in order, Workflows active; 36 px icon buttons, 32 px workspace chip |
| Header | 52 px: item title 16/600, status badge (word + glyph), "Running for 9h 35m", grey mono copyable reference (11 px JetBrains Mono, `--field`), Cancel run (direct button, class desk) |
| RUNS sidebar | 280 px, `--card-warm`; search 32 px; SuperPlane factory run cards (`rc`, selected `#1e293b`); status word + glyph + elapsed |
| Canvas | flex, dots 22 px `--dots` on `--canvas`; fixed layout, centre column x=0, stage gap 104 px |
| Stage node | 320 px (step-list variant), radius 16, `--card`, header 14/12 + 28 px icon tile, `ol.steps`, 11/500 status footer |
| Side nodes | 280 px: Redesign (x +470), Merged (centre), Stuck (+470), Stopped (-470) |
| Edges | 2 px cubic S-curves, ports 10 px radius 3; colours `--wire`, `--wire-running` (#818cf8, into the active stage), `--wire-failed` (#f87171); dashed 6 5 at 70% for paths not taken yet |
| Channel pills | 11 px/500, radius 6, `--card` (SuperPlane uses 10 px; raised to our 11 px floor) |
| Zoom pill | bottom left, 28 px: minus, plus, %, fit, centre on active step |
| Minimap | bottom right, 168 x 132, our addition (SuperPlane has none); node rectangles by status + viewport rectangle, drag to pan |
| Inspector | 480 px at >= 1200 px (flex sibling), overlay 440 px below 1200; header `--panel` #101113, Why block, lists, timeline, details |

At 900: RUNS hides behind a "Runs" button (overlay 300 px), the inspector overlays the canvas and the canvas centres on the selected step in the visible part. The minimap hides while the overlay inspector is open.

## Stages and steps (real, from sample.json recipes)
Stages are the line's wait-child states: scoping, design, plan, phase (ends done / stuck / dropped; plan can send back to redesign, cap 2).
Each stage card lists the child workflow's main-path states as the numbered step list (plain names from PLAIN-WORDS.md section 4). Design shows workflow.design: intake, draft, check, round, lens (active), lens-log, evidence. The review loop is `lens` round 2 of 4 (from StateCell.round). Fixed outcomes shown on edges: scoping resolved, design resolved, plan resolved/unresolved, phase resolved/unresolved.
Step glyphs on the right of each row: the kind (AI agent sparkle, Automatic cog, You person, Only you lock, Sub-workflow, Waiting clock), then done check, spinner (running) or red X (failed).

## States shown
- Running: ITM-01JM8/line/1, Design stage running, step "Review the draft" selected (try 2 of 4, 5h 47m, AI agent), Take over button (pause class, direct).
- Failed: ITM-01JQ7 line, "Build and ship" stage red footer, step "Code review" failed (from the review.1 run that ended `wrong`, outcome failed), error block, what you can do next. Take over is direct; "Run again" is a `terminal` button.
- Pending protected step: "Approve the merge" renders `Approve the merge · terminal` (disabled until ready).

## Interactions and keys
Click a stage header or a step row: select, ring on the card (4 px `--sel-ring`, 2 px offset), inspector opens. Click the empty canvas or Esc: close. J / K: next / previous step. F: fit. Drag: pan. Wheel: pan; Ctrl or Cmd + wheel: zoom (30% to 150%). Hover a node: connected edges brighten. Annotations toggle (top right) outlines every `data-gap` element in dashed amber with its label. No wiring, no palette, no edit mode.

## Data gaps (data-gap attribute) and the API each needs
| data-gap | Needs |
|---|---|
| recipe-edges | Full normalized recipe (states, transitions with `on`/outcomes) served; today RecipeView has only states and loops. Edge pills and the in-card loop hints ("gaps found: draft again", "findings: fix, then check again") come from it, with a plain label per outcome |
| why-text | A `why` sentence per recipe state (and per recipe for stage nodes), in RecipeStateView and StepDeclaration. The text here is mockup-authored from PLAIN-WORDS |
| step-runs | Per-state start, end, actor, tries and tokens for the whole run: today only ledger events (sample `x_stepRuns`) and `InstanceDetail.since`. Timeline needs a served projection, including the live step's started time |
| display-names | A display name for the workflow, each stage and each end node ("Idea to merged pull request", "Design", "Merged"); only ids exist |
| failed-chip | A red chip and a Failed footer: StateChip never emits tone `fail`, a failed instance reads DONE. Also the failed run's stage-level status (the line row of a failed child) |
| run-list-by-workflow | Instances of one workflow with status word, elapsed and the item title; today instances list rows are per recipe and the line rows of other items are inferred from their children |
| failed-next-actions | Actions on a failed child's parent (take over, run again) for a stage that ended unresolved |
| minimap | UI only (own addition), no API |
Also unserved but not annotated: the failed run's scope, design and plan step durations (shown without times), the stage-level work allowance.

## Avalonia
Own `ZoomBorder` plus a `Canvas` of node `ContentControl`s; stage node = Border radius 16, header Grid, `ItemsControl` for the step list (rows are selectable, so keep them real controls for focus order), footer Border. Edges: one custom `Control.Render` with `StreamGeometry` cubic beziers built from port positions (same maths as `bez()`), pills as `Border`s at t = 0.5. Layout is deterministic: stages stacked by measured height + 104 px gap. Inspector = existing 520 px detail sheet re-templated to 480. Minimap = own control drawing node rectangles from the same view model plus a viewport rectangle bound to the ZoomBorder matrix. `details` blocks map to `Expander`. Dot grid via tiled `DrawingBrush`.

## Could not match
- Stage cards at the default 85% zoom render step text at about 10 px effective; the 11 px floor holds at 100% and above (the zoom pill reaches 150%). SuperPlane itself renders at 30 to 80%.
- No light theme (owner rule). No live app capture of SuperPlane's run view exists, so the inspector follows components.html, extended with our Why, needs and produced blocks.

## Update: one clickable prototype
- Rail links: Home needs-you.html, Work/Agents/System not-mocked.html, Workflows workflows.html, Context context.html, Settings setup.html, "?" questions.html, the AW chip index.html.
- `?run=<reference>` picks the state: ITM-01JM8 (line, design.1, scoping.1 select that step) running; ITM-01JQ7 (also the review.1 reference) failed; ITM-01JR5 didn't work out; ITM-01JS2 cancelled; ITM-01JK3 done. Hash `#fail` still works. Unknown references show the running canvas.
- End words, exactly: DONE, FAILED (red), DIDN'T WORK OUT (neutral), CANCELLED (neutral), used in the header, runs sidebar, stage footer and inspector. New `data-gap="sample-end-states"` marks the three runs sample.json lacks.
- Links: Take over goes to needs-you.html#takeover; "See its context" goes to context.html?item=<item id>; the sidebar cards for Needs you and You have this open needs-you.html. Stage name is "Scoping".
- Screens: run-canvas-unresolved-1440.png, run-canvas-cancelled-1440.png, run-canvas-done-1440.png added.
- Open decisions are in run-canvas.questions.md.

## Update 3
- Opens at 100% on the active stage (card fully visible, incoming edge and pill in view); F fits the run.
- Rail and logo copied from workflows.html (same markup, icon order, avatar, tooltips, `aria-current`).
- All board runs open as themselves by `?run=<reference>` (items 01JM8, 01JN2, 01JP4, 01JQ7, 01K1A..01K8J); the sidebar lists the same 12 runs and each card links to run-canvas.html?run=<ref>. Header "States" menu lists one run per state.
- Words are sentence case. Waiting for you (amber footer, only "needs you" uses amber), You took over (violet, person glyph), Paused, needs you.
- Failed run's earlier stages have INVENTED timings (Scoping 06:10 to 09:06, Design 09:06 to 10:14, Plan 10:14 to 11:48, phase start 11:48): sample.json has none. Generated runs (everything except 01JM8 and 01JQ7) have no step timings; finished steps say "Its start and end times are not served yet".
- The 4th stage is named "Build and prove" and the plan step "Draft the phase plan" to match the board.
