# Builder brief: integrate the F3 prototype into one clickable whole

First read /home/thor/.agentic-workflow/mockups/f3/BRIEF.md (all of it, including the UPDATE section). The screens in
`/home/thor/.agentic-workflow/mockups/f3/screens/` were built by four builders and must now feel like ONE app. You own
consistency and click-through; you do not redesign any screen.

## 1. `screens/index.html`: the entry
A short start page in the app frame: the title "Sindri: workflow redesign prototype", one paragraph on what this is (the
look copies SuperPlane, Linear for dense lists, HumanLayer for approvals; real Sindri data; plain words), and a **guided
tour** list of 10 to 12 numbered stops, each a link to a screen and state with one line on what to look at:
board → list → open a run → the step inspector's "why this step matters" → a failed run → Didn't work out / Cancelled →
Needs you: approve the scope → send back with a reason → take over and hand back → context map → file map → Setup's two
approvals → Questions for the owner. A note on the Annotations toggle ("outlines what needs new data from Sindri"). A
"Start the tour" primary button to stop 1. Copy Linear's changelog/onboarding page feel inside SuperPlane's frame.
Each tour stop page shows a small fixed "Tour 3 of 12 · Next →" pill bottom centre when the URL has `tour=N` (add a tiny
shared script inline in each page; same markup everywhere).

## 2. One frame
- The left rail must be byte-identical across every page (logo, icon order Home, Work, Workflows, Context, Agents,
  System, Settings, then "?" and the avatar at the bottom; same tooltips, same active-state style). Source of truth:
  `workflows.html`. Fix each page's copy to match; set the right active icon per page.
- Same top bar height, title style, Annotations toggle position and behaviour, same CSS variable names and values (diff
  the `:root` blocks; unify on workflows.html's values; where a page added a token, keep it with the same naming scheme).
- Same status words and glyphs everywhere: Running, Waiting for you, You took over, Paused, needs you, Failed, Didn't
  work out, Cancelled, Done, Not started. Grep every page and fix drift ("With you", "You have this", "Finished",
  "Needs you" used as a status).
- No em dashes, no text under 11 px (except canvas text scaled by zoom), no internal terms (grep for fact, lens,
  packet, frozen, park, escalat, station, recipe, instance, BlockStep, desk, candidate, SND-; references in grey
  monospace are allowed).

## 3. Click-through
Write a small node script in your own scratch dir (NOT in the screens dir, NOT in any repo) that loads each page with
Playwright from `/home/thor/repos/agentic-workflow/skills/ui-evidence/node_modules/playwright` (import it by absolute
path; launch chromium headless; close the browser at the end, always, in a finally) and:
- collects every `<a href>` and every element with `data-href`/onclick navigation, resolves relative links, and fails
  on any target file that doesn't exist or any `#state`/`?run=` value the target page doesn't handle (check that the
  target renders something different from its default when the param is set, or that its script reads that param);
- clicks through the tour stops 1..N and screenshots each at 1440x900 to `screens/tour/NN-<name>.png`;
- reports console errors per page.
Fix every broken link (in the page that links, or by adding the missing state to the target with the smallest change
that copies that page's existing pattern). Do not run the Playwright MCP browser tools.

## Deliverables
index.html, the consistency fixes, `screens/tour/*.png`, `screens/INTEGRATION.md` (what you changed per page, the link
check result: N links, 0 broken; console errors: 0). Look at the tour PNGs and fix what looks inconsistent. Finish with a
10-line report.

## Known drift to fix (from the screen builders' reports)
- run-canvas.html uses ALL CAPS end words (DONE, FAILED, DIDN'T WORK OUT, CANCELLED); unify on the board's sentence case
  ("Done", "Failed", "Didn't work out", "Cancelled"), which copies SuperPlane's badges.
- run-canvas.html sidebar says "You have this"; use "You took over".
- run-canvas.html falls back to the running run for `?run=ITM-01JN2/...` (Link expiry, waiting at Approve the scope) and
  `?run=ITM-01JP4/...` (Bulk tag rename, you took over), and for the board's 8 invented runs. Every run the board or
  needs-you links to must open as itself: add those runs to run-canvas (copy its existing run definitions; Link expiry
  sits in Scoping at "Approve the scope", waiting for you; Bulk tag rename sits in Plan at "Draft the phase plan",
  you took over with the clock paused, 2h 50m left). Keep run ids consistent with workflows.html's data.
- Three screens invented end-state runs separately (board: 8 runs; canvas: Export audit log ITM-01JR5, Rename saved views
  ITM-01JS2, Dark mode toggle ITM-01JK3). Make one consistent set: the board's Done lane and list must contain the
  canvas's three, and the canvas sidebar must list the board's runs.
- workflows.html's "Needs you 3" counts a paused run; needs-you.html's inbox should then list the same paused run (Team
  invite links, used all 3 tries) with the same count. Make the counts agree across the rail badge, the filter and the
  inbox.

## Running alongside f3-setup
Another builder is writing `setup.html`, `questions.html` and `not-mocked.html` right now. Do NOT edit those three
files until they exist AND have not changed for 3 minutes (check with `stat`). Do everything else first: index.html,
the frame and word fixes on workflows, run-canvas, needs-you and context, and the link checker. Then wait for the three
files (poll every 60 s, up to 40 minutes), bring their frame in line, and run the full link check and the tour last.
Questions page: f3-setup builds it from the *.questions.md files; if you change wording that a question refers to,
update the matching questions file line too.
