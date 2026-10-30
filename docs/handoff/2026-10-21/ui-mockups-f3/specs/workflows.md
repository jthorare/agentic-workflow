# Spec: Workflows (Avalonia build spec)

Screen `workflows`. Inputs: `screens/workflows.html`, `workflows.notes.md`, `workflows.questions.md` (all Recommended answers are decisions), `data/CURRENT-UI.md`, `INTEGRATION.md`. Code read at origin/main (b6f564402): `src/Sindri/Aw.Sindri.Ui/{Api/Workflow,ViewModels/Workflows,ViewModels/Instance,ViewModels/Labels.Workflows.cs,Views/Workflows,Views/Controls/StateChipView.cs,Theme/Tokens.axaml,Navigation}`, goldens `tests/Aw.Sindri.Tests/Golden/Api/workflow-instances/{root,open,empty}.json`, `workflow-instance/*.json`, `workflow-recipe/*.json`, `commands/catalog.json`.

## 1. Goal and the approved decisions

Replace today's Workflows page (a panel of runs grouped by recipe, one 36 px `InstanceRow` each, finished runs collapsed, `Details` expander per row) with the approved page: a **Board** of stage lanes (default), a **Linear-density List**, and a page-local **480 px run inspector** drawer. The page still answers one question: which runs are open, what are they waiting on, and which need me.

| Q | Decision (Recommended) | Effect |
|---|---|---|
| 1 | Click opens the inspector; double-click, or Enter on the already open run, goes to the run canvas; "Open run" is always in the inspector | Single click `Select` + open drawer; Enter twice = `OpenRequested` (today Enter opens the frame at once: `WorkflowsKeysViewTests` moves) |
| 2 | The status word "Waiting for you" is a link to Needs you; nothing is approved on this page | No approve button; link = deep link `NeedsYou` |
| 3 | A list row's canvas link uses the row's own address; the inspector shows the item's five stages | Needs the line address of a child row: PENDING (section 4) |
| 4 | Mine = needs you plus you took over, marked as a data gap | Derived on the client from chips (no owner field) |
| 5 | Empty lanes stay, with "Nothing here." (Merge teaches, Done is plain) | Fixed 6 lanes |
| 6 | One status word per card; no "Needs you" chip; "Needs you" is only the filter (it also counts paused runs) | `StatusBadge` is the only status on a card/row |
| 7 | 12 runs in the prototype, 8 invented | Not data: the fixture for frames tests only (section 7) |

**Reused from today:** `WorkflowsViewModel` (refresh, `Select`, `Walk`, `Press`, done read, `Catch` on `workflow.instance-changed`, `RecipeFilter`/`SubjectFilter`), `InstanceRowViewModel` (chip, `Who`, `Stage`, `Protected`, `ChildAnswer`, `CanAnswerAtDesk`, `CanCancel`, `Link`, `CopyAddress`), `RecipeGroupViewModel` (the list's group header), `WorkflowNames.Of`, `StageTag`, `ProtectedActionViewModel`/`ProtectedActionButton`, `InstanceFrameViewModel` (stays the canvas/"Open run" target), the Home `WorkflowsStrip*` (untouched), the page head x:Names (`WorkflowsHead`, `WorkflowsStart`, `RecipeFilter`, `SubjectFilter`, `WorkflowGroups`, `WorkflowDoneToggle`, `WorkflowDoneRows`, `WorkflowsKey`, `WorkflowsPageEmpty/Unreadable/Stale`).
**Replaced:** the single `StackPanel` + `WorkflowsPanel` body of `Views/Workflows/WorkflowsPage.axaml`; `InstanceRow.axaml` (columns now Item, Stage, Status, Step, Waiting on, Updated; `Details` expander removed, its content moves to the inspector); `StageCellViewModel` strip per row (replaced by the inspector stage list); `WorkflowsLegend` expander (the status words are written out, so no key is needed; keep `WorkflowsKey` x:Name on the shortcuts sheet entry or retire it in the PR and say so).
**Not built:** the prototype's left rail, 56 px top bar, search in the top bar, `⌘K` hint and the Annotations switch. The app shell keeps its 48 px top bar, nav, `⌘K` palette and the Linear tokens; the shell pass owns any change to them. This spec is the page body only.

## 2. Regions and layout

Page grid replaces the `StackPanel`: `Grid RowDefinitions="Auto,Auto,*"` with the drawer as a second column of a wrapping `Grid ColumnDefinitions="*,Auto"`. Window min stays 900x600 (`Size.WindowMinWidth`).

| Row/col | Region | Size and tokens | Avalonia |
|---|---|---|---|
| r0 | Page head | existing `Classes="page-title"` "Workflows" (20/24, not the prototype's 22/600: the app ramp wins), `WorkflowsHead` caption, `Start workflow  N` button (`WorkflowsStart`) right | existing `Grid` |
| r1 | Toolbar | height `Size.TopBar` 48, padding `Space.MD` sides, hairline bottom (`Brush.Hairline`), gap `Space.SM` | `Grid`/`WrapPanel` |
| r1a | View switch Board / List | segmented: track `Surface2`, `Radius.MD`, pad 2; item 28 high, `Radius.SM`, 12/500; selected = `Surface3` + 1 px `Hairline`; glyph 14 | `ListBox` styled as segments (the app's segmented style, same as Context spec 1b) |
| r1b | Workflow chip | 28 high, `Surface2`, 1 px `Hairline`, `Radius.SM`, 13/500 name + chevron; Board: the line workflow ("Idea to merged pull request"); List: "All workflows" | the existing `RecipeFilter` `ComboBox` restyled (keep x:Name, `OnRecipeChosen`, `RecipeChoices`); first choice `AllRecipes` |
| r1c | Search | max 360, h 30 (`Size.Control`), `Surface2`, `Radius.MD`, left glyph, `/` kbd hint; filters title, current step, reference | the existing `SubjectFilter` `TextBox` (keep x:Name); client filter on step/reference added to the served `subject` filter |
| r1d | Key hints | "J K move, Enter open, V board or list"; `Kbd` ramp; hidden under 1180 px | `StackPanel`, `IsVisible` bound to `PageWidthBand` |
| r1e | Filter segments All / Needs you / Mine | same segmented style, count in 11 px tabular `InkSubtle` | `ListBox` + count `TextBlock` |
| r2 | Body | board or list in `*`, drawer in `Auto` column | `Grid` |

### Board (`WorkflowsBoard`, new `Views/Workflows/BoardView.axaml`)
`ScrollViewer` (horizontal auto, vertical off) holding a `Grid` of 6 `*` columns, `MinWidth` `Size.BoardMin` 1230, padding 16/16/12, lane gap 10 (`Grid.ColumnSpacing`), each lane `MinWidth` `Size.LaneMin` 200 (Done 150, star width 0.6). Lane height fills the viewport (min 420).

| Part | Spec | Avalonia / tokens |
|---|---|---|
| Lane | 1 px `Hairline`, `Radius.MD`; stage lanes fill `Surface1`; Merge and Done fill `Canvas` (idle) | `Border` |
| Lane header | `Surface2`, bottom hairline, padding 12/10/12/8: row 1 lane name in the `DetailTitle` ramp 18/22.5 medium (prototype 15/600, see open item 5) + count 12 `InkSubtle` tabular right | `Grid` |
| Why line | 12 `Caption`, `InkMuted`, reserved 2 lines (32) only when any lane has text; row hidden when none | `TextBlock MaxLines=2` |
| Approval row | 20 high, glyph 14 + 12 text "You approve the scope", `InkMuted` | `StackPanel` |
| Lane body | padding 8, card gap 8, vertical scroll | `ScrollViewer` + `ItemsControl` (`StackPanel Spacing=8`) per lane |
| Card (`RunCard`, new `UserControl`) | `Radius.SM`, 1 px `Hairline`, padding 10, fill `Surface2`; hover border `HairlineStrong`; selected fill `Surface3` + border `PrimaryHover` 1 px (not colour alone: the drawer opens); keyboard focus = 2 px `PrimaryHover` ring (`Size.FocusRing`, FocusRingTests) | `Border` + `Button` base for focus |
| Card rows | title 13/500 one line ellipsis (`BodyStrong`); row 2: `StatusBadge` left, time right 12 tabular `InkMuted` ("in stage 1h 30m" or "ended 5h 20m ago"); row 3: current step 12 `InkMuted`, `MaxLines=2` | `Grid` |
| Empty lane | dashed 1 px `HairlineStrong` box, `Radius.SM`, padding 20/12, centred 12; bold first line 13 `Ink` | `Border` + inner `Rectangle StrokeDashArray` (Border has no dash, as Context spec) |
| Filter-empty | "Nothing here. No runs match this filter." in every lane | same box |

### List (`WorkflowsList`, `Views/Workflows/RunListView.axaml`)
Columns (grid, gap 12, side padding 16): Item `2.2*` (min 200) | Stage 128 | Status 148 | Step `1.8*` (min 150) | Waiting on 124 | Updated 80 right. Column header 28 high (`Surface1`, 12 `InkSubtle`, sticky: first row outside the scrolled `ItemsControl`). Group row 32 high (`Surface2` at the group, bottom hairline, chevron 14, workflow name 13/500 `Ink`, count `InkSubtle`; the app's `RecipeGroup` collapse behaviour, `Labels.GroupExpanded/Collapsed` names kept). Run row `Size.Row` 36, 13 `Body`, hairline bottom only, no cards. Item cell: title 500 + mono reference 11 (`Mono` 12, `InkSubtle`). Hover `Surface2`; focused (J/K) `Surface2` + ring; selected `Surface3` + inset 1 px `HairlineStrong`.
Narrow bands (no container queries in Avalonia): `PageWidthBand` in the VM from the list's `Bounds.Width`: under 960 hide Stage and Waiting on (grid 2fr/148/1.4fr/72); under 560 hide Step and Updated and the reference (1fr/148). Widths via `ColumnDefinition.Width` bindings, in a pure `ListColumns.For(width)` (tested).
Finished runs: the existing collapsed "Show finished runs" line and `DoneRows` stay under the list (`WorkflowDoneToggle`); on the Board they fill the Done lane.

### Inspector drawer (`RunInspector`, new `Views/Workflows/RunInspectorView.axaml`)
Page-local right panel (not the shell detail sheet, so it never fights the 520 px sheet or its Esc/edge-tab rules). Width = `Clamp(window x 0.48, Size.InspectorMin 360, Size.Inspector 480)`, `Surface1`, left 1 px `Hairline`; opens over nothing: it takes its own column, so the board narrows and its horizontal scroll takes over. No slide under reduced motion (`Motion.Base`, 0 when reduced).

| Part | Spec |
|---|---|
| Chrome | 40 high, padding 8, bottom hairline: close, previous, next (28 px icon buttons, `Radius.SM`, hover `Surface2`), "3 of 12" mono 12 `InkSubtle`, copy reference icon |
| Header (padding 16, hairline) | row: `StatusBadge` + workflow pill ("Idea to merged pull request", `Radius.Pill`, 1 px `Hairline`, `InkMuted` 11); title `DetailTitle` 18/22.5 (prototype 16/22: nearest ramp step); meta 12 `InkMuted`: "Design for 1h 30m" or "Ended 5h 20m ago", reference button (mono 11, 1 px `Hairline`, `Radius.XS`, copy glyph 12); action row (margin-top 14, gap 8, 32 high): **Open run** (primary: `Primary` fill, `OnPrimary` text, open glyph), **Take over** (only running, waiting, paused), **Cancel run** (not when ended) |
| Callout (margin 16 16 0, `Radius.MD`, padding 12) | Needs you / Paused: `AttentionSoft` fill, 1 px `Attention` border, title 13/600 `Attention` with clock glyph, body 12 `InkMuted`, why 12, buttons "Review and decide" (primary) and "Or run in a terminal" (copy chip). Failed / Didn't work out: `ErrorSoft`, `Error`. You took over: `Surface2`, 1 px `Hairline`, violet person glyph, "Hand back" button. Running: `Surface2`, spinner glyph + current step + detail |
| STAGES caption bar | sticky, 8/16 padding, Eyebrow ramp 11/medium +0.4 with the capitals written in the label ("STAGES"), right "2 of 5 done" 12 tabular `InkSubtle`, `Surface1`, hairline bottom |
| Stage row | min 40, padding 8/16, hairline bottom: 16 px index circle (`Surface3`, 11 semibold `InkMuted`), name 13/500 (not started: `InkMuted`) with the current step under it (12, `InkMuted`), right: duration 12 tabular + `StatusBadge`; current row `Surface2` |
| Footer | 12 `InkSubtle`: link glyph + "Reference ITM-01JN2/line/1 is for pasting into a terminal." (mono run) |

The inspector shows the item's **line** run always (Q3). Content is a `RunInspectorViewModel` fed by `GET /sindri/workflow/instances/{address}` (section 4).

### Tokens
Reuse the app's Linear dark tokens by role; the prototype's warm dark is the shell pass's call. Status colours map to existing soft pairs; only the violet "you took over" has no token.

| Prototype role | Token | Note |
|---|---|---|
| `--bg` | `Brush.Canvas` | |
| card-warm, lane (stage), inspector, callout neutral | `Surface1` / `Surface2` (card on lane) / `Surface3` (selected) | |
| `--field`, `--seg` track | `Surface2` | |
| `--border` | `Hairline`; strong hover `HairlineStrong` | |
| `--fg` / `--muted-fg` / `--faint` | `Ink` / `InkMuted` / `InkSubtle` | `InkTertiary` stays disabled-only |
| running (blue) fg, bg, dot | `PrimaryHover` on `PrimarySoft` (glyph `PrimaryHover`) | `Primary` as text fails by design |
| waiting (amber) | `Attention` on `AttentionSoft`; "Needs you", paused and didn't work out (owner 2026-10-20) | didn't work out keeps its own glyph |
| done (green) | `Success` on `SuccessSoft` | |
| failed (red) | `Error` on `ErrorSoft` | |
| cancelled, not started | `InkMuted` on `Surface3`; not started `InkSubtle` on `Surface2` | neutral, glyph differs |
| you took over (violet) | NEW `Color.Violet` `#a78bfa` on NEW `Color.VioletSoft` `#170f2a` | glyph and text `Violet`; `ContrastTests` pairs Violet/VioletSoft (about 7:1) and Violet/Surface1 |
| lane tint (blue 8%), lane head | stage lane `Surface1`, head `Surface2` (no alpha token) | |
| selected ring | `PrimaryHover` | |
| NEW `Size.Inspector` 480, `Size.InspectorMin` 360, `Size.BoardMin` 1230, `Size.LaneMin` 200, `Size.LaneMinLast` 150 | sizes | |

Adding `Color.Violet`, `Color.VioletSoft` (plus their `Brush.*` mirrors) and the `Size.*` keys changes `TokenTests` (exact colour key set, Brush mirror, size values), `ContrastTests`, `ThemeResourcesTests`; `FluentOverrideTests` unchanged (no Fluent override reads them). Hex only in `Theme/Tokens.axaml`. The prototype's card `box-shadow` is dropped (no shadows in the app). Every status keeps glyph plus word (never colour alone): spinner arc (animated `RotateTransform`, `Motion.Spin` 0.9 s, 0 under reduced motion, static arc), clock, person, pause, circle-x, check, neutral circle-x, dashed circle.

## 3. States

| State | Prototype param | How the VM reaches it |
|---|---|---|
| Board | `?view=board` (default) | `ViewMode = Board` (key V, switch); remembered in `ui/preferences.json` |
| List | `?view=list` | `ViewMode = List` |
| Inspector open | `?open=ITM-01JN2` | `SelectedAddress` set by click, Enter, J/K with it open; `RunInspectorViewModel` reads the detail; closed by Esc, close, or selecting nothing |
| Needs you / Mine filter | `?filter=need|mine` | `Filter` enum; counts from the loaded rows |
| Keyboard focus | `?focus=N` | `FocusedIndex` over `Walk` (lane order on Board, row order on List) |
| Filter with no match | none | every lane or the list shows "Nothing here. No runs match this filter." |
| Empty lane | Merge, Done | plain empty box; Merge has teaching text |
| No runs at all | none | existing `IsEmpty`/`EmptyText` (`WorkflowsPageEmpty`) in place of the board |
| Unreadable, stale | none | existing `IsUnreadable` and `IsStale` rows, unchanged |
| Run status variants (badge) | `STATUS` map | the 9 kinds of section 4's word map; unknown served word renders raw, neutral |
| Inspector variants | needs-you, running, took over, failed callouts | by `StatusKind` of the line run |
| 900 px | `workflows-900.png` | drawer 432 (48vw), board scrolls horizontally, list hides Stage, Waiting on, then Step and Updated, hints hidden |
| Annotations | `?annot=1` | dev only, NOT built |

## 4. Data

Status words: the API serves `chip.word` in capitals (`StateChip.cs` in Core: RUNNING, QUEUED, DECISION, PARKED, ESCALATED, WITH YOU, DONE, FAILED, DIDN'T WORK OUT, CANCELLED; tones attn/fail/ok/neutral). The failed/unresolved/cancelled words **merged** (8902105d6, 6adfb1f1c, 9c0724344: SERVED). New pure `RunStatus.From(StateChip)` maps word to the prototype's `StatusKind` and sentence-case text: RUNNING Running; DECISION and ESCALATED "Waiting for you" (a choice or decision is waiting on you); PARKED "Paused, needs you" (the run stopped itself: used all its tries, ran out of allowance, timed out); WITH YOU "You took over"; QUEUED "Not started"; DONE Done; FAILED Failed; DIDN'T WORK OUT "Didn't work out"; CANCELLED Cancelled; any other word raw, neutral. (The existing `StateChipView` keeps showing words as served elsewhere; `StateChipTests` stays as is.)

| Region | Route / fields | Status |
|---|---|---|
| Board cards, list rows | `GET /sindri/workflow/instances?root=true&open=true` (`InstanceQuery`) → `InstanceList{instances[],counts{open,needsYou,withYou,running,done},empty,asOf}`; `InstanceSummary{address,id,itemId,recipe{id,displayName},state,chip{word,tone},since,engineOwned,subject{label,key},inputs[],states[],now,answer,actions[]}`; goldens `workflow-instances/root.json`, `open.json` | SERVED |
| Card title | `subject.label` (fallback `subject.key`) | SERVED |
| Time in stage, "ended ... ago", Updated | `since` (time the instance entered its current state; terminal state = end time) | SERVED |
| Current step | `now` (text); fallback the state word | SERVED |
| Finished runs / Done lane | existing done read (`WorkflowsViewModel.Done.cs`, `open=false`); counts `counts.done` | SERVED |
| Live updates | SSE `workflow.instance-changed` (`InstanceChange`, `docs/csharp/sindri.md` section "workflow.instance-changed"); `WorkflowsViewModel.Catch` re-queries | SERVED (#467) |
| Workflow chip and group names | `recipe.displayName` on `InstanceSummary`; `WorkflowNames.Of` fallback | SERVED (#468). `RecipeView.name` and the setup-once recipe names: still id-only: fallback capitalised id |
| Lane of a card (stage) | line recipe `workflow.line` root row: `state` (scoping, design, redesign, plan, phase per `recipes/sindri/workflow.line.yaml`) and `states[]` glyphs; other recipes: lanes from `GET /sindri/workflow/recipes/{id}` `states[]` (`id`,`order`,`branchOf`) | SERVED for the five line states. A **Merge** stage is not a line state | 
| Merge lane, stage of a run for any list row, line address of a child row | `lineStage` + root address on `InstanceListRow` | PENDING: api lane (item F5/engine). Fallback: runs in `phase` stay in "Build and prove"; Merge lane stays empty with its teaching text; the list is root runs only (as today), so no child rows and the Q3 address question does not arise yet |
| Stage display names | per-state `name` | PENDING: engine F5. Fallback: a small `LineStageNames` table for the five `workflow.line` state ids (Scoping, Design, Plan, Build and prove; `redesign` shares Design), `WorkflowNames.Of(id)` for any other; deleted when `name` is served |
| Why-text per stage, approval row ("You approve the scope"), callout "why" | `why` on states and step descriptors | PENDING: engine F5. Fallback: lane why line and approval row hidden; callout body uses the served step text (below) |
| Line stage statuses (inspector) | `GET /sindri/workflow/instances/{address}` → `InstanceDetail{title,states[] (glyph done|active|pending, desk, outcome, children{done,total}),current,children[] (ChildGroup rows with chip),stepRuns[],actions[],counts}`; goldens `workflow-instance/project-build.json`, `item-produce.json` | SERVED: glyph + child chip give each stage's badge (running / waiting from the child chip / done / not started) |
| Per-stage times (finished stages) | per-state `enteredAt`/`leftAt` on the line instance | PENDING: engine lane. `stepRuns[]` (`startedAt,endedAt,durationMs,step,state`) is served but covers the line's own steps, not each stage. Fallback: current stage shows time from `since`; finished stages show no time |
| "started 11:02" | start time of the run | PENDING: api lane (`startedAt` on the summary). Fallback: omit the clause |
| Needs-you callout text | `current.step` description, `current.desk` (`DeskForm`), `answer` on the row | SERVED (words as served); "why" PENDING above |
| Failed callout | `states[].outcome`, `current.status` | SERVED; fallback sentence `RunFailedBody` |
| "Or run in a terminal" | `InstanceRowViewModel.Protected.Cli` (`ProtectedActionViewModel`) | SERVED for protected answers; the chip is hidden when `Protected` is null |
| Needs you filter, Mine | derived: chip words DECISION, PARKED, ESCALATED (Needs you); plus WITH YOU (Mine). `counts.needsYou`/`withYou` are root-wide and may differ from rows loaded, so counts shown are from loaded rows | SERVED inputs; owner field PENDING: api lane (`owner` or assignee on the instance), keep as a gap note in the PR |
| Waiting on | derived: `InstanceRowViewModel.Who` (`engineOwned`, `Protected`, desk): You / AI agent / the stage word | SERVED (`Who` exists); a single served field is PENDING: api lane |
| Needs-you link target | `GET /sindri/needs-you` row whose `itemId` matches; else deep link `Item` | SERVED |
| Search | `subject` query param + client filter on `now` and `address` | SERVED |

Fallback pattern for PENDING fields is CURRENT-UI's plain "not served yet": the element is hidden or reads the neutral fallback above, never invented data.

## 5. Actions and keys

| Control | Action |
|---|---|
| Open run (inspector, double-click, Enter on the open run) | no catalog id: deep link `Workflow/<address>` (existing frame); `OpenRequested` |
| Review and decide, the "Waiting for you" word | deep link `NeedsYou/<row>` (or `Item/<itemId>`); no id; nothing is approved here |
| Or run in a terminal | terminal-copy of `Protected.Cli` (`ApplyCommand:answer-accept` and the other protected answers are class `protected`, `ProtectedActionButton`; the UI cannot send them) |
| Take over | `ApplyCommand:takeover` (class pause) through the frame's command path (`IssueNonce`, then `ApplyCommand`): extract it from `InstanceFrameViewModel` into a shared `RunCommands` service the drawer and frame both call, or open the frame (open item 2) |
| Hand back | `ApplyCommand:handback` (class desk), same path. Today `Labels.HandBackArrives` says it is not in the app: this spec turns it on; the key `B` note is retired in the same PR |
| Cancel run | `ApplyCommand:cancel` (class desk) with the confirm of the frame: drawer button opens the frame with `FrameIntent.AskCancel` (existing); the prototype did not draw the confirm |
| Start workflow (head, N) | `StartInstance` (existing) |
| Answer at desk (key A on a desk-waiting row) | existing: `CompleteStep` via `FrameIntent.PendingOutput`, or the protected popover |
| Copy reference (chrome, header) | local `Clipboard` of the address; no id |
| Close, previous, next, filters, view switch | local |
| NEW action ids | none (the 60 catalog rows, `commands/catalog.json`, are unchanged; `ActionLabelsTests` unchanged) |

Keys (page focused, no text box focused; no clash with G-chords or F6):

| Key | Effect |
|---|---|
| J / K, Down / Up | next / previous visible card (lane order) or row; inspector follows when open |
| Enter | opens the inspector; on the already open run, opens the run canvas |
| Esc | closes the inspector |
| V | toggles Board and List |
| / | focuses search (`SubjectFilter`) |
| A, X (existing) | answer, ask to cancel the focused run |
| N (existing) | Start workflow |
| Left / Right on Board | next lane's first card (new, small) |

Every card and row gets `AutomationProperties.Name` = `InstanceRowViewModel.AccessibleName` (subject, workflow, chip word, state, who, missing inputs), the board lanes are `Group` with the lane name and count, and the inspector is a `Group` named "Run details".

## 6. Copy (`Labels.Board.cs`, new partial of `Labels`; no em dashes, no internal terms)

| Key | Text |
|---|---|
| `ViewBoard` / `ViewList` | "Board" / "List" |
| `AllWorkflows` | "All workflows" |
| `FilterAll` / `FilterNeedsYou` / `FilterMine` | "All" / "Needs you" / "Mine" |
| `SearchRuns` | "Search runs" |
| `KeyHintsLine` | "J K move, Enter open, V board or list" |
| `RunStatusRunning` / `WaitingForYou` / `Paused` / `TookOver` / `NotStarted` | "Running" / "Waiting for you" / "Paused, needs you" / "You took over" / "Not started" |
| `RunStatusFailed` / `Done` / `DidntWorkOut` / `Cancelled` | "Failed" / "Done" / "Didn't work out" / "Cancelled" |
| `LaneScoping` / `Design` / `Plan` / `Build` / `Merge` / `Done` | "Scoping" / "Design" / "Plan" / "Build and prove" / "Merge" / "Done" (stopgap table, section 4) |
| `LaneEmpty` / `LaneEmptyBody` | "Nothing here." / "No runs in this stage." |
| `LaneMergeEmpty` | "Runs wait here for CI and docs, then for your approval." |
| `LaneDoneEmpty` | "No finished runs yet." |
| `NoRunsMatch` | "Nothing here. No runs match this filter." |
| `InStage(time)` / `EndedAgo(time)` | "in stage {time}" / "ended {time} ago" |
| `ListColItem` / `Stage` / `Status` / `Step` / `WaitingOn` / `Updated` | "Item" / "Stage" / "Status" / "Step" / "Waiting on" / "Updated" |
| `WaitYou` / `WaitAgent` | "You" / "AI agent" (existing `WhoWaitingForYou`, `WhoInProgress` stay for the old strip) |
| `RunPosition(n,of)` | "{n} of {of}" |
| `RunClose` / `Previous` / `Next` / `CopyReference` | "Close" / "Previous run" / "Next run" / "Copy reference" |
| `RunOpen` / `TakeOver` / `HandBack` / `Cancel` | "Open run" / "Take over" (catalog label) / "Hand back" / "Cancel run" (catalog label) |
| `RunStagesEyebrow` / `StagesDone(n)` | "STAGES" / "{n} of 5 done" |
| `RunStageFor(stage,time)` / `RunEnded(time)` | "{stage} for {time}" / "Ended {time} ago" |
| `RunStarted(time)` | "started {time}" |
| `RunNeedsTitle(t)` | "Needs you: {t}" |
| `RunNeedsBody` | "Open the request to read it, then decide." (used when no served text) |
| `RunReviewDecide` / `RunTerminal` | "Review and decide" / "Or run in a terminal" |
| `RunTookTitle` | "You took over" |
| `RunFailedBody` | "This run ended without finishing. Open the run to see why." |
| `RunReferenceNote(ref)` | "Reference {ref} is for pasting into a terminal." |
| `RunDetails` | "Run details" |

The existing `Labels.ColumnSubject/Inputs/Now`, `StageDesk`, `LegendLoop`, `Mark*` stay while the Home strip and the instance frame use them. A `LabelsTests` row scans the new keys for em dashes and the internal terms list.

## 7. Tests the ui lane must add or change

| Test class | Change |
|---|---|
| `TokenTests`, `ThemeResourcesTests`, `ContrastTests` | CHANGE: add `Color.Violet`, `Color.VioletSoft`, the five `Size.*` keys; key set + Brush mirror; Violet/VioletSoft and Violet/Surface1 pairs; every status pair of section 2 listed |
| `FluentOverrideTests`, `FocusRingTests` | unchanged (the card and row focus ring is the 2 px `Size.FocusRing`) |
| `NamedControlsTests` | keep the existing x:Names; ADD `WorkflowsBoard`, `WorkflowsList`, `WorkflowsInspector`, `WorkflowsViewSwitch`, `WorkflowsFilter`, `InspectorOpenRun`, `InspectorClose` |
| `WorkflowsFramesTests` | CHANGE: frames become Board, List, Board with inspector, List with inspector at 1440 and 900; no horizontal page scroll at 900 (board scrolls inside its host) |
| `WorkflowsKeysViewTests`, `StripKeysTests` | CHANGE: Enter opens the inspector first and the frame on the second Enter; add V, `/`, Esc, arrows; the Home strip keys stay |
| `WorkflowsStripFramesTests`, `StripFoldTests` | unchanged (Home strip untouched) |
| `InstanceRowViewModelTests`, `ChildAnswerTests`, `ShellChildAnswerTests` | unchanged unless `InstanceRowViewModel` loses `Stages`/`Details` (then move those assertions to the inspector VM) |
| NEW `RunStatusTests` | every served chip word maps to its kind and sentence-case words; unknown word raw and neutral; each kind has a glyph and a word |
| NEW `WorkflowLanesTests` | line state to lane (scoping, design and redesign, plan, phase); other recipe uses `RecipeView.states`; Merge empty; Done lane from done read; empty-lane teaching text |
| NEW `WorkflowFiltersTests` | All, Needs you (DECISION, PARKED, ESCALATED), Mine (adds WITH YOU), counts from loaded rows, search on title, step, address |
| NEW `ListColumnsTests` | `ListColumns.For(width)` at 1440, 960, 959, 560, 559 hides the right columns |
| NEW `RunInspectorViewModelTests` | stage badges from detail golden (`project-build`, `item-produce`); current-stage time from `since`; finished-stage time hidden while PENDING; callouts per kind; buttons per kind (Take over hidden when ended; Cancel hidden when ended; Hand back only when taken over); Next/Previous follow `Walk` |
| NEW `WorkflowsBoardLayoutTests` (headless) | 6 lanes, min widths 200/150, host min 1230, drawer width clamp 360 to 480 at 900 and 1440, card text fits (`RealFontTextFitTests` style: title ellipsis, step 2 lines) |
| `LabelsTests`, `ActionLabelsTests`, `LabelsAreWordsTests` (where present) | NEW keys scanned; `ActionLabelsTests` unchanged (no new ids; `ApplyCommand:handback` label already unique) |
| `ShellWorkflowsTests`, `InstanceLiveTests` | CHANGE only if selection or `Catch` signatures move; the instance-changed catch must still re-query once per frame |
| `SpecTextTests`, `docs/csharp/sindri-ui.md` | update the Workflows section (board, list, inspector) and the keys line; add the F3 words |

## 8. Build order (each slice red test first)

1. **Status and tokens.** Red: `RunStatusTests` (every served word maps; unknown raw) and `TokenTests` (Violet, VioletSoft, Size keys). Adds the tokens, `RunStatus`, `StatusBadge` control (glyph path + word, animated spinner, reduced motion) and the `Labels.Board.cs` status and lane words. Proves the one-word-per-run mapping and the contrast pins. No page change.
2. **Board and toolbar.** Red: `WorkflowLanesTests` (line states land in their lanes; Merge empty) and `WorkflowFiltersTests`. Adds `BoardView`, `RunCard`, the toolbar (view switch, workflow chip as the existing `RecipeFilter`, search, filter segments with counts), view mode and filter state in `WorkflowsViewModel`, `LineStageNames`. Old grouped panel stays behind `ViewMode = List` until slice 3. Proves lanes, counts, empty and filter-empty states, frames at 1440 and 900.
3. **List.** Red: `ListColumnsTests` and the `WorkflowsFramesTests` List frame. Replaces `InstanceRow.axaml` with the six-column row, sticky header and group rows, the width bands, J/K/V keys across both views, finished-runs line. Proves the list, hiding at 960 and 560, key walk order.
4. **Inspector drawer.** Red: `RunInspectorViewModelTests` (stage badges from the `project-build` golden, callout per kind) and the `WorkflowsKeysViewTests` Enter-twice change. Adds `RunInspectorView`, the detail read, stage list, callouts, Open run, Needs you link, terminal copy, Take over, Hand back, Cancel run (frame intent), previous/next, copy reference, drawer width clamp, Esc. Proves Q1, Q2, the action ids and that nothing is approved here.
5. **Pending data (separate PRs after other lanes land).** Red per field: stage `name`/`why`/approval row (engine F5), `lineStage` and root address (Merge lane, child rows), per-stage `enteredAt`/`leftAt`, `startedAt`, `owner`. Each removes its fallback and adds the field to the golden, `SindriUiJsonContext` and `FakeSindriApi`.

## 9. Open items for the ui lane

1. **Shell.** The prototype has a left rail and a 56 px top bar with the title, search and an Annotations switch; the app has a 48 px top bar with nav. This spec keeps the shell; confirm with the shell pass whether the search lives in the page toolbar (as here) or the shell, and that `/` is free there.
2. **Take over, Hand back from the drawer.** Cleanest is a shared `RunCommands` service (nonce then `ApplyCommand`) extracted from `InstanceFrameViewModel`; if that is too large for slice 4, ship Open run and Cancel run first and open the frame (new `FrameIntent.TakeOver`/`HandBack` values, an enum add) for the other two. Either way no new catalog id.
3. **Settled by ideas (2026-10-20):** PARKED reads "Paused, needs you" and DECISION/ESCALATED read "Waiting for you", matching the prototype (the paused run Team invite links used all its tries). DIDN'T WORK OUT: OVERRIDDEN by the owner 2026-10-20, it is AMBER (served `attn` tone, the 2026-10-19 table); see the note at the end.
4. **Merge lane.** The five line states have no Merge stage, so the lane is empty until `lineStage` is served; confirm the stopgap with the api lane and that "Build and prove" absorbing the ship step is acceptable meanwhile.
5. **Lane title size.** Prototype 15/600; ramp has 13 and 18. This spec uses `DetailTitle` (18). If it crowds a 200 px lane at 130% text, use `BodyStrong` 13 and say so in the PR.
6. **Mine** is a data gap by decision (Q4); keep the gap in the PR body so the owner field lands later.
7. **Child rows in the list (Q3).** Needs the api's child to line address; until then the list is root runs only and the inspector address equals the row address. The prototype lists child runs as rows; reproduce them only when served.
8. **Count mismatch.** `counts.needsYou` (root-wide) and the loaded rows can disagree during a refresh; this spec shows loaded-row counts. Confirm that is acceptable beside `WorkflowsHead` ("12 open · 3 need you · 1 with you", unchanged).
9. **The 480 px drawer is page-local**, not the shell sheet. If the shell later unifies sheets, move it then; deep links (`workflow/<address>`) still open the frame sheet.

## Answered by ui (2026-10-20, ui digest ~/.agentic-workflow/digests/sindri-ui-f3-spec-workflows.md)
- Shell stays 48 px; search = the `Ctrl+K` palette plus the page toolbar filter (`/` focuses it).
- Take over / Hand back go through one shared `RunCommands` service (open item 2: the service, not `FrameIntent` values).
- Lane title 18 px (`DetailTitle`), as specified (open item 5).

## Owner override (2026-10-20)
- OWNER 2026-10-20 (via meta): DIDN'T WORK OUT is AMBER, per the 2026-10-19 table; this overrides the earlier "neutral" settlement. FAILED stays red, CANCELLED stays neutral. ui uses the served `attn` tone (api #504 matches). It keeps its own glyph (minus in circle) and word, so it never reads as "needs you" by colour alone.
