# Builder brief: Setup screen + Questions for the owner + not-mocked page

First read /home/thor/.agentic-workflow/mockups/f3/BRIEF.md (all of it, including the UPDATE section) and follow it.
The frame (left rail, top bar, tokens, annotations toggle) must be copied EXACTLY from `screens/workflows.html`:
same logo, icon order, avatar, tooltips, CSS variables. Open it and copy the markup and CSS, don't redraw it.

## 1. `screens/setup.html` (the rail's Settings goes here)
Source of truth: the setup-once spec. Read it with
`git -C /home/thor/repos/agentic-workflow show origin/docs/sindri-setup-once:docs/superpowers/specs/2026-10-19-sindri-setup-once-design.md`
(read-only; §1, §4, §7.2, §7.6 matter most). The owner's rule: the owner types two commands ever (Sindri setup, Onyx
setup); everything else is a button or background work in the app. The owner calls the in-app part "the 2 clicks":
**approve the settings (profile)** and **approve all workflows**.
- Copy the reference apps' onboarding/setup checklist pattern (Linear's onboarding checklist and SuperPlane's
  integration setup; flat rows, a step number or glyph, one status word each, the primary action on the right).
- The six steps from `GET /setup`, in order: repo, profile, recipes, mcp, map, onyx, each with plain title, a status word
  (To do / Working / Done / Failed / Needs you), its `why` line in plain words, a detail line, progress where it has one,
  and its action. Plain titles, e.g. "Add your repository", "Approve your settings", "Approve the workflows",
  "Connect your coding agents", "Build the product map", "Connect Linear and Slack (Onyx)".
- **Approve your settings**: an in-app typed confirmation. Show the change summary (what the settings allow: repos,
  services, budgets, in plain words), a challenge like `approve 3f9a1c` and a field to type it; the button enables only
  when it matches. State after: Done, "approved just now".
- **Approve the workflows**: approve-all with a press-and-hold button (fill progress over ~1.5 s, release cancels).
  Before it: the plan list (each workflow by display name, e.g. "Idea to merged pull request", "Scoping", "Design",
  "Plan", "Build and prove", "Review loop", "Ship"...; children first; what each may do in one plain line; its checks
  "passed 9 of 9").
- **Connect Linear and Slack (Onyx)**: the Onyx login form (email, password; "used once and never stored").
- The map step builds itself in the background ("Building, 1,240 of 3,100 files") with no promote button (owner rule).
- States via hash: `#fresh` (all to do after the two terminal commands: show a small "You've run: ./setup.sh
  --with-sindri" done line), `#profile` (typing the challenge), `#approveall` (holding, mid-fill), `#done` (all done:
  a quiet "Sindri is set up" and a link to Workflows), `#failed` (the map step failed with a plain error and Retry).
  Use SND-SETUP texts from §7.6 rewritten in plain words (no codes on screen except in a grey details row).

## 2. `screens/questions.html` (the "?" at the bottom of the rail)
"Questions for the owner": gather every `screens/*.questions.md` file (read them at the END of your work, and again
just before you finish, since other builders are still adding). Also add these known ones:
- Words for finished runs are decided (Done, Failed, Didn't work out, Cancelled): show as "Decided" with the date.
- Light theme: SuperPlane ships dark; recommend dark only for now.
- Shadows: the current spec says none; SuperPlane is flat too; recommend keep flat.
- The node-graph canvas in Avalonia: build our own pan/zoom canvas (NodifyAvalonia supports Avalonia 11 only);
  recommend own control, spike first.
Layout: copy Linear's issue list look: one row per question (36 px), grouped by screen, each expandable to show the
options, the **recommended answer** highlighted, why (one line), which reference app it follows, and a link to the screen
and state it affects (e.g. `run-canvas.html#fail`). A count at top ("14 open, 1 decided"). Rows have a "Recommended"
chip. Plain words only.

## 3. `screens/not-mocked.html`
For Work, Agents, System: the frame plus a centred SuperPlane empty state: "<Page> isn't part of this review" and one
line on what it will show, read from `?page=`.

## Deliverables
setup.html, questions.html, not-mocked.html; PNGs `setup-1440.png` (#fresh), `setup-profile-1440.png`,
`setup-approveall-1440.png`, `setup-done-1440.png`, `setup-900.png`, `questions-1440.png`; `setup.notes.md`
(regions, states, data gaps with the API: `GET /setup`, `POST /setup/*`, the typed and held challenges). Look at every
PNG and fix what looks off. Finish with a 10-line report.
