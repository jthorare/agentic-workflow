# workflows (Workflows page) notes for the ui lane

Files: `workflows.html` (open with `?view=list`, `?open=ITM-01JN2`, `?filter=need|mine`, `?focus=N`, `?annot=1` to preset a state),
`workflows-1440.png` (board), `workflows-900.png` (board plus inspector at the minimum width),
`workflows-list-1440.png` (list), plus extras: `workflows-list-inspector-1440.png`, `workflows-inspector-1440.png` (board, inspector open, annotations on).

## Regions (1440 x 900)
| Region | Size | Tokens |
|---|---|---|
| Left rail | 56 px wide, 40 px icon buttons (20 px icons), tooltip on hover, current page chip `--muted` + 1 px `--border` | `--card-warm`, `--border` |
| Top bar | 56 px: title 22/600/-0.02em, search (max 360, h 32, `/` focuses), `⌘K` hint, Annotations switch | `--field`, `--border` |
| Toolbar | 48 px: Board/List segmented, workflow chip, key hints (hidden under 1180 px), All / Needs you / Mine segmented with counts | `--seg`, `--seg-on` |
| Board | lanes `flex 1 1 0`, min 200 px (Done min 150), gap 10, lane radius 8, header 15/600 + 12 px why-text (2 lines reserved) + 20 px approval row, body padding 8, card gap 8. Board min width 1230, scrolls horizontally | `--lane-run` (blue 8%) on stage lanes, `--lane-idle` on Merge and Done, `--lane-head` |
| Card | radius 6, padding 10, title 13/500, status badge, "time in stage" 12 px tabular, current step 12 px (2-line clamp), "Needs you" chip | `--card-warm`, `--border`; selected `--panel-card` |
| List | 28 px column header, 32 px group rows, 36 px rows, 13 px text, hairlines only, no cards | `--hover`, `--focus`, `--select` |
| Inspector | `min(480px, 48vw)`, min 360; 40 px chrome row (close, previous, next, copy reference), header, optional callout, "STAGES" caption bar, 40 px stage rows | `--panel #101113`, `--panel-card`, `--panel-border` |

All colours are `:root` variables (SuperPlane dark tokens). Status colours: running blue, waiting amber, finished green, failed red, you-took-over violet, not started grey. Amber is used for "Needs you" and "Paused, needs you" only.

## States shown
Status words with glyphs: Running (spinning arc), Waiting for you (clock), You took over (person), Paused, needs you (pause, defined in `STATUS`, no sample run has it), Failed (circle X), Done (check), Didn't work out (neutral, X circle) and Cancelled (neutral), the owner-approved end words next to red Failed; the last two have no sample run, Not started (dashed circle). Board: four cards from `instances` (Saved search sharing in Design, Link expiry in Scoping with the "Needs you" chip, Bulk tag rename in Plan, CSV import limits in Build and prove, Failed). Empty lane: Merge (teaching text) and Done (plain). Filtering with no match shows "Nothing here. No runs match this filter." in each lane or in the list. Inspector variants: needs-you callout, running, you-took-over (Hand back), failed callout.

## Interactions and keys
- Click a card or row: opens the inspector (same run, whichever row of the item). Esc or the close button closes it.
- `J` / `K` (or arrows) move through visible cards in lane order, or list rows; with the inspector open it follows the focus. `Enter` opens. `V` toggles Board and List. `/` focuses search.
- Filters: All = every run; Needs you = waiting for you or paused; Mine = needs you plus you took over (needs the run-owner gap). Search filters on title, step and reference.
- The reference (e.g. `ITM-01JN2/line/1`) is a small grey mono copy button; "Or run in a terminal" copies the `sindri workflow complete ...` command.
- "Open run" links to `run-canvas.html?run=<reference>`. "Review and decide" links to `work.html?open=<item>` (the approvals screen). Approving is not done here, so no direct approve button exists on this page. "Take over" and "Hand back" are pause-class (sessions may run them); "Cancel run" is desk-class with a confirm in the real app (not drawn).
- At 900 px wide the inspector is 432 px (48vw), the list hides Stage, Waiting on and finally Step and Updated through container queries (900, 560 px of list width).

## Data and gaps (each is `data-gap="..."`, shown by the Annotations switch)
| data-gap | What the screen needs | API |
|---|---|---|
| stage display names | Plain names Scoping, Design, Plan, Build and prove, Merge, Done for the line's states and for the child workflows | `name` on each recipe state and recipe (today id only, DATA-MODEL display name note) |
| why-text | One sentence per stage (lane header) and per approval ("Nothing moves on until you approve...") | new `why` field on states and Step descriptors, served in `RecipeStateView` |
| workflow display name | "Idea to merged pull request", "Scoping", "Design", "Plan", "Code review" as group headers and the chip | `name` on `RecipeView` |
| per-stage times | Time in the current stage and per finished stage durations | per-state `enteredAt`/`leftAt` on the line instance (today only `x_stepRuns` of each child); the card uses the child's `since` |
| failed chip | A red Failed status for a run that ended with `failed` or `unresolved` | chip tone `fail` is reserved and never produced; terminal runs read DONE today |
| run owner | Who a run is with, for the Mine filter | owner or assignee on the instance (today derived from needs-you and taken-over) |
| line stage statuses | The five stages of a run with their statuses | derived from the line instance `states[].glyph` plus child state; needs a `stages` projection |
| stage of a run | Stage column and the lane a card sits in; the item to line-run link (the list returns children as separate rows) | `lineStage` and root address on each `InstanceListRow` |
| waiting-on | Who or what a run waits for (You, AI agent, a stage) | derive from step kind (`runtime`, `humanOnly`) and `wait` target; not served as one field |

Not a gap but mockup-authored: "Paused, needs you" has no sample run; the "Merge" lane copy, the 2 h 14 m scoping duration (from `x_stepRuns`), and started times are from the sample or derived from it.

## Avalonia
- Board: horizontal `ScrollViewer` of lanes; each lane a `Border` with a header grid and an `ItemsRepeater` (`StackLayout`, spacing 8) of card `ContentControl`s. Lanes use `*` columns with `MinWidth` 200 (Done 150) inside a `UniformGrid`-like panel; `MinWidth` 1230 on the host gives the scroll.
- List: `ItemsRepeater` over a flattened list of group rows and run rows with a `DataTemplate` selector; fixed row height 36, grid columns as in the CSS, hide columns by a `Width` binding from a size-band view model (container queries have no Avalonia equivalent).
- Inspector: a right `Border` of `MinWidth` 360, `Width` = min(480, 48% of the window), a `GridSplitter`-style grip optional. Status badge is one `UserControl` taking a status enum (glyph path plus word); the spinner glyph is an animated `RotateTransform` that respects reduced motion.
- Segmented controls: `ListBox` with `ToggleButton` items styled as in the CSS. Key handling at the page `KeyDown` (J, K, Enter, Esc, V, `/`).
- Annotations are a mockup-only device; do not build.

## Update: volume, statuses, one status word
- Card status word is the only status; "Waiting for you" and "Paused, needs you" are links to `needs-you.html#stop-<item>`. The "Needs you" chip is gone; "Needs you" is only the filter (it counts paused runs too).
- Invented runs (not in sample.json, not marked on screen): Email digest settings, Team invite links (Paused, needs you: used all 3 tries), Audit log filters, Rate limit headers, Bulk archive (Waiting for you: approve the merge), Webhook retry backoff (Done), Export to PDF (Didn't work out), Dark mode toggle (Cancelled). Their times, steps and references are made up. The list adds one "Idea to merged pull request" row for each.
- Ended runs show "ended 5h 20m ago" instead of "in stage". Extra PNGs: `workflows-list-900.png`.
