# needs-you: notes for the ui lane

Open `needs-you.html`; the top bar has a state switcher (Pending, Approve?, Send back, Approved, Paused run, Takeover) and an Annotations toggle. URL params for screenshots: `?state=<pending|confirm|sendback|approved|answer|takeover>`, `&ann=1`, `&reason=<text>`.

## Regions (1440 x 900; min 900)
- Rail 56 px (`--card-warm`, hairline `--border`): workspace chip, the 7 pages (Home active, count badge = needs-you count), avatar. Buttons 36 px, radius 8.
- Top bar 44 px: breadcrumb, state switcher (mockup only, not in the app), Annotations (mockup only).
- Inbox 400 px (330 below 1100 px): header, "N new" pill, grouped list, key hints. Group header 32 px sticky; row min 56 px, two lines (title, item, waiting time / question, "pauses in ..."), 24 px kind tile, hairline between rows, no card borders, no side stripes. Selected = `#1e293b` tint plus 1 px inset ring. New row pulses `rgba(180,83,9,.16)` to `.04` over 1.6 s (reduced motion: steady tint plus the word "new").
- Detail: dotted canvas background, one 760 px max card (radius 16, `--card`, SuperPlane node look): head (28 px tile, title 15/600, item, copyable grey reference, "Approval 1 of 3" kind word), sections separated by hairlines (caption 11/600 uppercase), "Why this needs you" first on a tinted section, and a status footer ("Waiting for you", waiting time, "pauses and asks again in ..."). Action bar is a fixed strip under the scroll area (not inside the card).
- Takeover view: canvas (280 px nodes, 64 px gaps, ports 10 px, 2 px wires, channel pills, zoom pill) plus a 480 px inspector (46vw below 1100 px).

## Four kinds, kept distinct (DATA-MODEL section 5)
Each has its own glyph, group header and kind word: your approvals (shield, "Approval n of 3"), paused runs (pause circle), agent decisions (speech bubble with ?), workflow and settings approvals (file with a dot). Amber (`--attn*`) is used only for "needs you".

## Keys
J/K move, Enter open, A approve (first press shows "Approve?" if the request was not scrolled to the end, second press approves), D send back (opens reason box; required; Ctrl+Enter sends, Esc cancels), E edit before approve (shown as "our addition", not wired). Protected requests (merge, workflow, settings): A or click copies the command, button reads `⌂ Verb · terminal`. Keys are ignored while a textarea has focus.

## Data gaps (`data-gap` attributes; turn on Annotations)
- `why text`: a `why` sentence per step (not served). Needed on the state in the recipe view and the step declaration.
- `stop number on row`: the needs-you row has `kind: park`, `label: decision`; it does not say "approval 1 of 3" or which stage. Needs stop number and plain title.
- `row timeout`: needs-you rows do not carry the timeout. Instance `status.timeout{inSeconds}` exists; add it to the row.
- `recommended on stop`: no `recommended` on stop options in real data (interview options do have it).
- `edit before approve`: no API to approve with changed output.
- `display names`: stage and step plain names and the "AI agent / Automatic" kind label (derive from runtime plus humanOnly).
- `per-step times and actors`: per-step start, end, who and outcome (today only in ledger events).
- `recipe edges`: transitions and their outcome labels ("gaps found: draft again") are not served.
- Not flagged but mockup-authored: rows 2, 3, 6, 7 (Notes archive, Tag colours, workflow and settings approvals), the paused run's try history, the PR summary, and the stop 2 decisions. Shapes follow the real ones. Stop 3 is a stub in the snapshot (`step.babysit.watch`: merged / rework / stuck); "Send back" maps to rework.
- Stop 1 has no open questions in real data (all 7 answered), so it shows the questions with options and the chosen one marked. Stop 2 shows genuinely open decisions.
- Takeover clock: sample.json says `paused.timeoutLeftSeconds` 10200 (2h 50m, consistent with the 3 h limit on that step), so the mockup shows "2h 50m left", not "1d 4h". Change in one place (`#left1`, `#left2`) if wanted. Time since takeover is 2h 35m (14:05 to 16:40), so the 2-hour nudge toast is correct.

## Avalonia
ItemsRepeater for the inbox with a sticky group header template; the detail card is a ScrollViewer with a plain StackPanel of section templates and a fixed bottom action strip. Textarea = TextBox AcceptsReturn with a Ctrl+Enter KeyBinding. Canvas = the shared ZoomBorder plus Path edges (see AVALONIA-NOTES.md); the back wire is a cubic with a right-hand control offset of 100 px. Pulse = a 1.6 s animation of Background between two brushes. Protected button = existing `ProtectedActionButton` (copy-CLI).

## Prototype update
- Hash states: `#pending #confirm #sendback #approved #answer #takeover #handback` (the old `?state=` still works). `#handback` = after Hand back: badge Running, node footer "clock running, 2h 50m left", toast gone. Clicking Hand back (inspector or toast) does the same.
- Rail: Home to needs-you.html, Workflows, Context, Settings to their files, Work, Agents and System to not-mocked.html, "?" to questions.html.
- Item titles link to `run-canvas.html?run=<reference>`; "See its context" (stops 1 and 2) goes to context.html. Workflow and settings rows link to workflows.html and setup.html.
- Open decisions: needs-you.questions.md.

## Review round 2
- One takeover word: "You took over" (node badge, node footer, inspector badge, step row), person glyph each time.
- "candidate" is now "draft". Rail markup copied from workflows.html (no count badge).
- Settings and workflow approvals: typed confirm (field plus Approve, enabled only on an exact match; `#set`, `#wf`). "Approve all new workflows": press-and-hold button, 1.5 s fill (`#all`, `?fill=55` freezes the fill for a screenshot). Approve the merge stays `⌂ Approve merge · terminal` (`#s3`). Row ids work as hashes (`#s1 #s2 #s3 #park #dec #wf #set #all`).
- Handback: node Running, clock running, quiet "Handed back" toast.
- Extra PNGs: needs-you-{confirm,answer,approved,handback,typed,hold}-1440.png.
