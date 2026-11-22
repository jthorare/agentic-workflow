# Closing Sweeps (shared)

The one copy of the two phases that close every project map: system-design's `doc.md` (written by
`skills/system-design/scripts/write_packet.py`, which carries both exit checks word for word) and officeHours'
`TASKS.md`. Referenced via: SHARED_DIR pattern (CD2).

## The rule

Both sweeps run after the last build phase, in this order: the performance sweep, then the bug sweep. Each one is a
phase of its own with an exit check. The project is not done until both exit checks pass.

## Performance sweep

One pass over the finished project in the real app, not in tests or a mockup.

1. List every data or interactive surface: each screen, list, form, search, chart and command the project built or
   touched. The map's surfaces section is the starting list.
2. Give each surface its budget. A budget the design states (tagged `owner decision`) wins. A surface with no stated
   budget gets the agent default, tagged `agent default`. An interaction shows its response within 100 ms. A data view
   shows its first rows within 1 s.
3. Profile each surface in the real app on realistic data, and record the measured time beside the budget.
4. List every surface that misses its budget, with the measured time, the budget and the slowest step.
5. Fix each miss and measure it again.

**Exit check:** every data or interactive surface has a measured time in the real app at or under its budget. A miss
passes only when the owner accepts it in writing (a D-number).

## Bug sweep

One pass after the performance sweep. Walk the finished project in the real app as its users would.

1. Walk each surface from the performance sweep's list: the main path, empty data, bad input, a slow network and a
   narrow window.
2. File each finding as an issue in the repo's tracker. Search the open issues first, and add to a match instead of
   filing a copy.
3. Route each issue as a fix through the project's fix workflow (for example `/bugFixOrchestrator`), so every fix
   starts from a check that fails.
4. Walk the surfaces again once the fixes merge.

**Exit check:** every finding is an issue in the repo's tracker, and a merged fix or a written reason closes each one.
A second walk of every surface finds nothing new.
