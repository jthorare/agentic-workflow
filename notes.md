# Notes per frame (for the issue comment)

**canvas-16**: Fit is the state on open and is the largest zoom at which the whole graph fits (100% here: 4 columns, 4 rows). Layers flow left to right and wrap; both If arms stack. Every label is at least 12 px. About 200 px stay empty below the graph: Fit is width-bound.

**canvas-4**: Fit caps at 100%: a small graph is not blown up. 4 steps fit in one row (4 columns). The empty space below is expected: a small graph is not blown up, and Fit never scales above 100%. No minimap below 12 steps. The graph returns {"sum":3}.

**canvas-40**: Fit would push labels under 12 px, so it stops at the 12 px floor (86%) and the canvas scrolls; the scrollbar and the minimap box show where you are.

**canvas-16-1000**: At 1000 px the rows wrap sooner (2 columns; Fit 86%). The step sheet (380 px) is closed on open; opening a step slides it over the canvas as an inset and the canvas keeps its zoom.

**canvas-16-130**: Text size 130% (#3231), open on Fit (85%). Cards are laid out at 130% and scaled by Fit; every label stays inside its card and nothing clips. Wrapping changes only on a window resize or a text-size change.

**canvas-zoomed**: From Fit (100%) press + twice: 110%, then 125%. Zoom scales the same layout (same columns and rows as canvas-16); it never re-wraps. Panned onto the If branch; the minimap box moved with it. Hovering Fit shows its tooltip "Fit  F".

**cards**: Cards at 2x zoom (24 px labels). Icons: pointer = run by hand, clock = schedule, braces = Code, fork = If, globe = HTTP, bell = Notify, tray = Output. A step with no title shows its block name ("Code"), never its id. A settings line that does not fit wraps to 2 lines on its own card (the schedule card); the next run is a chip on line 1.
