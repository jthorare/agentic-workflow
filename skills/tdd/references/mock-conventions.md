# Mock conventions

Section 2 (Front end) shows each person's moment as mocks of the new screens beside today's screen. A mock is a design, not a screenshot: it is built from the project's real components so the engineer can build exactly what is shown, and its caption says which state it shows.

## 1 · Build mocks from the real components

The method is system-design's (SKILL.md, Design context and Process step 7); in short:

1. **Inventory first.** Read the project's tokens (`design-tokens.json`, `.impeccable.md`, the theme the repo's `AGENTS.md` names) and its component library (`package.json`, then the `components/` folders). Note "Library: ..., Primitives: ...".
2. **Copy, don't invent.** Each mock is a static HTML file in `mockups/<name>.html` that copies the real markup and class names of the components it uses (read their source) and links or inlines the project's compiled CSS. The doc-system tokens fill in only what the project does not style. Reuse filters, dialogs and creation flows that exist; never re-mock them.
3. **Real-looking data.** Plausible names, the units and formats the product uses, numbers consistent with the counts in the TDD (a list of four requesters if posts get at most seven). No lorem ipsum, no real personal data.
4. **Compare before showing.** Render each mock beside a real screenshot of the same screen and fix what differs (spacing, type, colour, density).
5. **Render to an image.** Screenshot the mock's component, not the whole browser window, with the Playwright that `doctor.sh` checks (`playwright-core` under `$DESIGN_DEPS`), at device scale 2. Save PNG (or JPEG for large, photo-heavy screens) in `mockups/`; the compiler reads the size of PNG, JPEG and SVG only (anything else is treated as `mid`). The reference packet's mocks are JPEGs of 15 to 102 KB each.
6. **Name files by moment and state**: `mockups/<moment>-<state>.png`, and `mockups/today-<moment>.png` for today's screen. A render the page does not use is an orphan (`FILES/orphan`, a warning).

**Today's screens** are screenshots of the running product (or of the reference page being rebuilt), never mocks. If you cannot get one, say so in the text; do not mock "today".

**The scaffold's placeholders.** A new packet's persona box points at four placeholder images, `mockups/moment-a-1.svg`, `moment-a-2.svg`, `moment-a-3.svg` and `today-moment-a.svg`, copied from `templates/packet/` by `new_packet.py` (and `tdd new`) and marked "PLACEHOLDER · REPLACE ME", so the page renders with no broken image before any mock exists. Until you replace them, each placeholder the page still shows is a `FILES/placeholder` error, and each line of the persona box that still carries a template prompt (`<Role>`, `<Moment>`, `<Caption>` ...) is a `FILES/placeholder-text` error (`references/validators.md`): expected in a draft, and they block the publish run. Replace each one: render the state to `mockups/<moment>-<state>.png` (today's screen: a screenshot saved as `mockups/today-<moment>.png`), point the image line at it, and delete the placeholder; one left in `mockups/` but no longer on the page is a `FILES/orphan` warning. With no screenshot of today, delete the image line and say so in the text. Never publish a page that still shows a placeholder.

## 2 · The persona box

```markdown
::: persona Librarian · Opens the request {chip: changed; #fe-ranked; shots: cols3}
What this person sees and does, in two or three sentences. {small}

**Stale:** what the screen shows in that state, and why. {small}

![Ranked by Main Branch holds v3 at 14:02: 1 Patron One ... not ranked: Patron Four, would go over 6 loans.](mockups/ranked-four-placed-one-not-ranked.jpg "Ranked: four placed, one not ranked")

![...](mockups/no-ranking-rules.jpg "No ranking rules: time order, no reason")

![...](mockups/stale-someone-asked.jpg "Stale: someone asked after the ranking")

:::: today Newest first, no ranking
![Today's Requests column: full cards newest first with Deny and Approve.](mockups/today-one-full-card-per-request.jpg "Today: one full card per request, newest first")
::::
:::
```

- **Title** `Role · Moment`: who, and the moment in their task ("Staff · Taps 'I'm available'"). `chip:` labels it (`new`, `changed`); `tone:` sets the chip tone (default `new`, green); `#fe-<moment>` is its anchor and rail entry.
- **Text first**, every paragraph `{small}`: the screen, the component, what it reads, then one bold-labelled paragraph per notable state. A note that is not part of the design (a copy change, what was not run) is `{small; muted}`.
- **Shots** follow the text, in the order a user meets the states, **one image per paragraph**: put a blank line between two shots. Two image lines in a row become one paragraph of raw markdown, not two shots (`STRUCT/raw-markdown`). Everything that is not an image or a `today` fold is text.
- **One `today` fold** per persona, last: `:::: today <summary> {label: ...}`. The summary says in a few words what today's screen does; the label defaults to "Today, for comparison". It holds today's screenshot(s) and may hold a sentence of text. It inherits the persona's shot size and grid.
- **`::: duo`** puts two short persona boxes side by side (each `::::`), for two moments of one person or two people at one moment.
- After the boxes, one muted line lists what is **not mocked yet** and which built component each of those states reuses.

## 3 · Shot sizes

`build_tdd.py` (`node_shot`, `box_persona`) gives each shot a size class from its pixel size unless the persona says otherwise:

| Size | Chosen automatically when | Rendered (`tdd.css`) |
|---|---|---|
| `phone` | width <= 420 px and taller than wide | 230 px wide |
| `col` | height / width >= 1.2 | the full column width |
| `wide` | height >= 600 px (and neither of the above) | at most 520 px tall |
| `mid` | anything else, or a size it could not read | at most 420 px tall |

The persona's `shots:` attribute overrides it: `phone`, `mid`, `wide` or `col` forces that size on every shot; `cols3` or `cols2` lays the shots out in a grid of three or two equal columns and makes each one `col`. With no `shots:` and three or more shots that are all `col` or `phone`, the compiler picks `cols3` itself. In a `cols3` grid each shot is cropped from the top at 640 px tall; below 1000 px wide the grids stack into one column.

Use `cols3` for three states of one screen (the reference's ranked, no rules and stale lists): crop all three to the same width and a similar height. Use the default for a single dialog or a phone screen.

## 4 · Captions and alt text

- **Caption** = the quoted title in `![alt](src "Caption")`: the state the shot shows, as a short phrase ("Skipping the top-ranked person"). Today's captions start with "Today:" and say what today's screen does or fails to do ("Today: Approve records nothing about who else was waiting").
- **Alt text** = what the shot shows, with the real names and numbers on it, in a sentence or two. The lightbox (click a shot), screen readers and the agents lane all read it, so it has to stand alone.
- A caption never claims more than the shot shows; a mock is never captioned as production.

## 5 · Publishing

Every image the page references is copied into `build/` at its published path and listed in `build/content.files.json`, which is the `files` map of the publish (`references/publish-and-mirror.md`). Each file must be at most 15 MB; a missing one renders broken and fails the linter (`FILES/missing`).
