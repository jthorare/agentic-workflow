# Integration notes (F3 prototype)

Entry: `index.html` (guided tour, 12 stops). Each stop page shows a "Tour N of 12 · Next" pill when the URL has `tour=N`
(same inline script, marked `/*aw-tour*/`, on workflows, run-canvas, needs-you, context, setup, questions).
Tour screenshots: `tour/00-index.png` and `tour/01..12-*.png` at 1440x900.

## Changes per page
- index.html: new. Title, intro, look references, Start the tour, 12 numbered stops, Annotations note and toggle.
- workflows.html: "You have this" callout title is now "You took over". Tour pill.
- run-canvas.html: end words are sentence case ("Done"); stage footers, history lines and the legend use "Done" instead of
  "Finished"; "Phase packet" on screen is now "Phase plan"; top bar 56 px with the 22 px title and the shared Annotations
  switch; below 1100 px the crumb and elapsed time hide so the run title stays visible. Every board run opens as itself
  (all 12 ids resolve; the sidebar lists the board's 12 runs; Done holds Webhook retry backoff, Export to PDF, Dark mode toggle).
- needs-you.html: inbox now matches the board. The invented "Notes archive" plan approval is removed; the merge approval is
  Bulk archive (ITM-01K5F); the paused run is Team invite links (ITM-01K2C, used all 3 tries). Approvals read "1 of 2" and
  "2 of 2". Header reads "7 waiting, 3 runs": the 3 runs are the same 3 the board's Needs you filter counts (Link expiry,
  Bulk archive, Team invite links); the other 4 are agent decisions and workflow or settings approvals, which are not runs.
  `#stop-<run id>` hashes from the board now select the matching inbox row. The takeover badge reads "You took over".
  Top bar 56 px, shared Annotations switch, hand-back toast lifted so the tour pill does not cover it.
- context.html: reads `?item=<run id>` (crumb and "Open the run" point at that run; the sample map is the same for every
  run) and `?anchor=` (selects the starting point). Top bar 56 px, shared switch.
- setup.html, questions.html, not-mocked.html: frame already matched; setup title set to "Settings"; tour pill added to setup
  and questions.
- All pages: left rail markup is byte-identical apart from the active icon (checked by script). `--muted` unified on
  workflows.html's #26251e in run-canvas, needs-you and context (the rail's active tile now matches).

## Link and word check (scratch script, Playwright, 120 page states crawled)
- Links: 1845 internal links resolved, 0 broken. Every `?run=`, `?open=`, `?view=`, `?filter=`, `?state=`, `#stop-...`, `#state`
  and setup hash target applies its state (0 param failures).
- Console errors: 0 (Google Fonts requests are blocked in the run and ignored).
- Text scan of every crawled state: 0 em dashes, 0 text under 11 px outside the canvas, 0 internal terms outside grey
  monospace references, 0 ALL CAPS status words, 0 "With you / You have this / Finished".

## Left as is
- Context map shows the Link expiry map for every `?item=`; only that run has real map data.
- The board's Needs you count is runs only (3); the inbox adds 4 non-run requests, stated in its header.
