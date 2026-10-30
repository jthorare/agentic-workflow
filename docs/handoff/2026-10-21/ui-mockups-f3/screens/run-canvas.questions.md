# Open decisions: run-canvas

1. Question: which words end a run, and in which tone?
   Options: the four owner-approved words (DONE, FAILED, DIDN'T WORK OUT, CANCELLED), or reuse the old chip words.
   Recommended: the four owner-approved words, in caps, 11 px/600; FAILED red, DONE green glyph, DIDN'T WORK OUT and CANCELLED neutral grey. The same word is used for the header badge, the runs sidebar card, the stage footer and the inspector badge. Follows the owner's note, SuperPlane's footer tones.
2. Question: runs for DIDN'T WORK OUT, CANCELLED and DONE do not exist in sample.json. Which items show them?
   Options: invent clearly labelled sample runs, or leave the states unreachable.
   Recommended: three sample runs (Export audit log ITM-01JR5, Rename saved views ITM-01JS2, Dark mode toggle ITM-01JK3), outlined as `data-gap="sample-end-states"`; the unresolved one follows the real review loop (rounds used up, then the re-check still finds a problem, which routes to the line's "Stuck" end). Follows SuperPlane, which shows one run per end state.
3. Question: `?run=` for items with no mocked canvas (ITM-01JN2 at the scoping stop, ITM-01JP4 with you).
   Options: open the running canvas anyway, or open needs-you.html.
   Recommended: in the runs sidebar these two cards link to needs-you.html (the stop view and #takeover); a `?run=` for them falls back to the running canvas. Follows HumanLayer (an approval opens the approval card).
4. Question: where does "Take over" lead?
   Options: act in place, or open the takeover screen.
   Recommended: link to needs-you.html#takeover, as the owner said; the clock pauses there.
5. Question: "See its context" scope.
   Options: link to the item's map, or to the selected step.
   Recommended: link to context.html?item=<item id> from every step, stage and failure (one place to look). Follows Linear's "open the linked thing" row actions.
6. Question: the state switcher in the header (Running, Failed, Didn't work out, Cancelled, Done).
   Options: keep it, or rely only on the runs sidebar.
   Recommended: keep it as a mockup-only control, shown at 1500 px and wider (hidden below so the title fits); the runs sidebar switches the same states. SuperPlane has no such control.
7. Question: edge labels now that engine confirms edges are (from, on, outcome, to) without plain labels.
   Options: show the raw outcome, or keep the mockup's plain labels.
   Recommended: keep the plain labels (marked `recipe-edges`); the plain label table comes later and is a text lookup on the outcome. Follows SuperPlane's channel pills (a short word per output).

## Round 3
8. Question: open at the active stage at 100%, or fit the whole run like SuperPlane's 30 to 80%?
   Options: active stage at 100%, or fit to screen.
   Recommended: active stage at 100% with the stage card fully visible and its incoming edge and pill in view (step text at least 11 px); F fits the whole run. Follows SuperPlane's run view (selected node in focus), keeps our 11 px floor.
9. Question: end words in caps or sentence case?
   Options: caps chips, or sentence case.
   Recommended: sentence case everywhere ("Done", "Failed", "Didn't work out", "Cancelled", plus "Waiting for you", "You took over", "Paused, needs you"), the same as workflows.html. Follows SuperPlane's badges.
10. Question: how do runs with no hand-built canvas open?
    Options: one generator from a short spec, or hand-built canvases for all 12.
    Recommended: one generator (stages before the current one are finished, later ones not started), with all board runs by id; only the Saved search sharing and CSV import limits runs are hand-built because sample.json has their steps. Step timing for generated runs is not served, so finished steps say so.
