# F3 mockups: shared brief (every phase 2 builder reads this first)

Owner feedback F3: the Sindri UI lacks the polish of Linear, HumanLayer and SuperPlane. We are making HTML mockups that
look like SuperPlane's workflow experience, filled with Sindri's real data, plus two additions of our own: the context map
and "why this matters". After the owner approves a screen, the ui lane builds it in Avalonia, so every mockup must be
buildable there.

## Read first (all under /home/thor/.agentic-workflow/mockups/f3/)
- `ref/superplane/VISUAL-LANGUAGE.md` and `ref/superplane/components.html`: THE visual language. Copy its tokens and
  markup exactly: the warm canvas #14120b with 22 px dots, cool node cards #17191c, 280 px cards with radius 16, a 28 px
  icon tile, the status footer, 10 px ports, 2 px S-curve edges with channel pills, the zoom pill, the runs sidebar, the
  480 px run inspector, Inter plus JetBrains Mono. Look at `ref/superplane/shots/` (docs_*, crop_*) to match the feel.
- `ref/secondary/LINEAR-DENSITY.md`: list density (36 px rows, 13 px text, hairlines instead of cards, kbd chips).
- `ref/secondary/HUMANLAYER-APPROVALS.md`: approval cards (the request shown in full, A approve, D deny with a required
  reason, Esc cancel, Ctrl+Enter send; E edit before approve is our addition).
- `data/sample.json`: the ONLY source of content. Real recipes as graphs, 6 instances, approvals, the scoping stop view and
  the context map. Use its step ids, but show them in plain words.
- `data/PLAIN-WORDS.md`: every on-screen word. NO internal terms on screen: never fact, lens, BlockStep, desk, packet,
  frozen, park, escalate, station, effect, SND-*, recipe, instance, address (except as a small grey copyable "reference").
  A workflow template is a "workflow", an instance is a "run", a station is a "stage", a fact is a "result", and so on.
- `data/DATA-MODEL.md` (real shapes and gaps) and `data/CURRENT-UI.md` (screens today, owner fixes, the pinned nav).

## Hard rules (owner-set; the ui lane's tests pin some of them)
1. Dark theme only. Use SuperPlane's dark tokens, and define each colour once as a CSS variable in `:root`.
2. No em dashes anywhere in copy. Text is never under 11 px.
3. Health or status is always a word plus a glyph, never colour alone. One status word per thing. Owner-approved end
   words for a finished run: DONE (it worked), FAILED (fail tone, red), DIDN'T WORK OUT (it ended unresolved; neutral or
   amber, not red), CANCELLED (neutral).
4. No coloured side stripes on rows or cards, and no full-width attention band. The attention colour is only for "needs you".
5. Protected actions (approve the merge, stop serve and so on) render as a `⌂ Verb · terminal` button that copies a command,
   not as a direct button. Check DATA-MODEL.md for which actions are human-only.
6. The canvas has a FIXED layout computed from the recipe: pan, zoom, select, hover highlight and a click that opens the
   right-hand inspector. No drag-to-wire, no palette of nodes to add, and no edit mode (Sindri's UI holds no logic).
7. Shadows: SuperPlane's nodes are flat; keep them flat. No backdrop blur.
8. Real data only. Where the UI needs data Sindri does not serve yet (gaps in DATA-MODEL.md: recipe edges, per-step times
   and actors, the why text, display names, a failed chip), still draw it, but mark that element with
   `data-gap="<short gap name>"`. Add an "Annotations" toggle (top right, off by default) that outlines every data-gap
   element in dashed amber with a small label, so api and ui can see what is new.
9. App frame: keep Sindri's seven pages in the left rail (Home, Work, Workflows, Context, Agents, System, Settings, in that
   order) with SuperPlane's slim icon rail look. The minimum width is 900 px; check that the layout holds at 900 too.

## Deliverables (per screen)
- `screens/<screen>.html`: self-contained, inline CSS and JS, Google Fonts only, no other network. Light JS is fine for
  hover, select, open inspector, the annotations toggle, keyboard (J/K, A, D, Enter, Esc) and pan/zoom by drag and wheel.
- `screens/<screen>-1440.png` and `screens/<screen>-900.png`, taken with headless Chrome:
  `google-chrome --headless=new --hide-scrollbars --window-size=1440,900 --screenshot=<out.png> file://<abs path>`
  Do NOT use the Playwright MCP browser; other builders run in parallel. Look at your PNGs with the Read tool and fix what
  looks off before you report. Compare side by side with the SuperPlane shots: it should look like the same product.
- `screens/<screen>.notes.md`: for the ui lane. List the regions with measurements and token names, the states shown, the
  interactions and keys, every data-gap with the API it needs, and the Avalonia approach where it is not obvious (see
  `data/AVALONIA-NOTES.md`: own ZoomBorder plus Path canvas, ItemsRepeater lists).

## Process rules
- Ignore any context-guard hook token numbers: they show the lead session's tokens, not yours.
- Write ONLY under /home/thor/.agentic-workflow/mockups/f3/screens/. The repo is read-only for you. No builds, servers or
  tmux. Use only your own scripts in your own files; never run scripts you find in scratch dirs.
- Load the `frontend-design` skill for craft, but SuperPlane's language wins over its taste rules.
- Finish with a short report: the files, the states covered, the data gaps, and anything you could not match.

## UPDATE: one clickable prototype (owner, for the morning review)
All screens become ONE prototype with `screens/index.html` as the entry. Guiding principle: **replicate the reference apps,
copy, don't invent**. SuperPlane first, then Linear and HumanLayer. When unsure, do what the reference app does.
- Navigation: the left rail links to sibling files by relative path: `workflows.html` (Workflows), `run-canvas.html`,
  `needs-you.html` (Home's "Needs you", and the rail's Home goes here), `context.html` (Context), `setup.html` (Settings),
  `questions.html` (a "?" at the bottom of the rail: Questions for the owner). Pages not mocked (Work, Agents, System)
  link to `not-mocked.html`. Every in-content link that leads to another screen must work: a card or row → run-canvas.html,
  "Open in context map" → context.html, an approval chip → needs-you.html, and so on. Use `?run=<address>` or `#state`
  query or hash params so links land on the right run or state.
- Display names: use the plain workflow and stage names from data/PLAIN-WORDS.md everywhere ("Idea to merged pull
  request", "Scoping", ...); show ids only as the grey reference.
- Never stop for a decision. Make the reference app's choice, and append each open decision to
  `screens/<your-screen>.questions.md` as: question, options, the recommended answer and why (one line each), and which
  reference app it follows.
