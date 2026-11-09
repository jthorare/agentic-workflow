# House TDD design system (House TDD 1.0.0)

The look and the class vocabulary of every page the `tdd` skill builds. A white page (D2: "make the background white instead of offwhite"), Notion's calm with more colour and richer components (D3: "Notion with more color and richer components because it's html"), one token set with a light and a dark value per colour.

| File | Role |
|---|---|
| `tokens.json` | the source of every value: colours (both themes), type, spacing, radius, layout, the component list |
| `tdd.css` | the stylesheet `app/build.ts` inlines into the page. Its `FONTS` and `TOKENS` blocks are generated from `tokens.json`; everything after them is hand-written and is the class contract with the renderer (`app/src/components/*`) |
| `../../app/src/schema.css` | the schema drawer (D7), layered on top by the schema worker: `dialog.drawer.schema` and its parts |
| `README.md` | this reference |

## Keeping the CSS in step with the tokens

Edit `tokens.json`, never the generated blocks, then:

```bash
T="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/tdd"; S="${SYSTEM_DESIGN_SKILL:-$T/vendor/system-design}"; D="$T/templates/tdd-system"   # T and S as SKILL.md sets them
python3 -I "$S/scripts/build_doc.py" --sync-css "$D/tokens.json" "$D/tdd.css"    # rewrite the FONTS and TOKENS blocks from tokens.json
python3 -I "$S/scripts/build_doc.py" --check-css "$D/tokens.json" "$D/tdd.css"   # exit 1 if the blocks drifted from tokens.json
```

Run `--check-css` before every publish of a change to this folder. The generated block defines every token on bare `:root` (light), again under `@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { ... } }` and under `:root[data-theme="dark"]`, each with `color-scheme`, as the artifact contract requires.

## Colour tokens

The design system for technical design documents built by the tdd skill and published as claude.ai HTML artifacts. White page (D2, Alec 2026-11-08), more colour and richer components than Notion (D3), one token set with a light and a dark value per colour. Text tokens hold at least 4.5:1 on bg, surface and surface-2 in both themes. Swatch tokens are the tldraw export colours, the same in both themes because the diagram plate stays light.

| Token | Light | Dark | Use |
|---|---|---|---|
| `--bg` | `#FFFFFF` | `#0F1318` | page background (body): white, per D2 |
| `--surface` | `#FFFFFF` | `#161C24` | cards, tables, figures, dialogs |
| `--surface-2` | `#F3F5F8` | `#1E2631` | tinted panel: table head, rail buttons, the Today row, code |
| `--code-bg` | `#F1F3F6` | `#121820` | code blocks and inline code |
| `--fg` | `#1F2328` | `#E6EAF0` | body text (16.0:1 light, 14.3:1 dark on bg) |
| `--muted` | `#5A6675` | `#9AA6B5` | secondary text: captions, meta, summaries (5.9:1 light, 7.3:1 dark on bg) |
| `--line` | `#DCE1E8` | `#2B3441` | hairlines, card and table borders |
| `--line-strong` | `#94A0AF` | `#5B6A80` | control edges, swatch default border (at least 3:1 on bg) |
| `--accent` | `#2160C4` | `#7AB1F5` | links, reference tokens, the Actual size button, focus ring |
| `--brand` | `#1F4995` | `#9DBBF0` | the title, active rail number, step numbers, DONE chips, decided-card edge |
| `--secondary` | `#5B5CC2` | `#ADACF2` | PROPOSED chips, violet callouts, the second legend hue |
| `--good` | `#1E7A55` | `#7CD4A8` | NEW and RECOMMENDED chips, good callouts, new steps |
| `--warn` | `#A85017` | `#F0B27A` | DRAFT and OPEN chips, open-card edge, warn callouts |
| `--danger` | `#BE3A3E` | `#F09C9C` | HELD and BLOCKED chips, danger callouts, where-it-hurts |
| `--ruled` | `#1F2328` | `#E6EAF0` | RULED chip: solid ink, text in bg |
| `--plate` | `#FFFFFF` | `#E9EDF2` | diagram plate: light in both themes so tldraw strokes stay legible |
| `--plate-line` | `#DCE1E8` | `#C3CBD6` | plate border |
| `--media` | `#101418` | `#06090C` | letterbox behind video |
| `--scrim` | `rgb(20 24 32 / 0.5)` | `rgb(0 0 0 / 0.65)` | dialog backdrop, enlarged shot backdrop |
| `--shadow` | `rgb(20 24 32 / 0.14)` | `rgb(0 0 0 / 0.55)` | rail, popovers, dialogs, enlarged shots |
| `--sw-green` | `#D3E9E3` | `#D3E9E3` | legend swatch fill: new or changed (tldraw green) |
| `--sw-green-line` | `#099268` | `#099268` | legend swatch stroke: new or changed |
| `--sw-grey` | `#ECEEF0` | `#ECEEF0` | legend swatch fill: the app, existing context (tldraw grey) |
| `--sw-grey-line` | `#9FA8B2` | `#9FA8B2` | legend swatch stroke: the app |
| `--sw-blue` | `#DCE1F8` | `#DCE1F8` | legend swatch fill: the engine or another system (tldraw blue) |
| `--sw-blue-line` | `#4465E9` | `#4465E9` | legend swatch stroke: the engine |
| `--sw-amber` | `#F9F0E6` | `#F9F0E6` | legend swatch fill: needs a data source (tldraw yellow) |
| `--sw-amber-line` | `#F1AC4B` | `#F1AC4B` | legend swatch stroke: needs a data source |
| `--sw-red` | `#F4DADB` | `#F4DADB` | legend swatch fill: where it hurts (tldraw red) |
| `--sw-red-line` | `#E03131` | `#E03131` | legend swatch stroke: where it hurts |
| `--sw-orange-line` | `#E16919` | `#E16919` | legend swatch stroke: proposed engine change (dashed orange) |
| `--sw-violet` | `#E8E2F7` | `#E8E2F7` | legend swatch fill: flagged or secondary (tldraw violet) |
| `--sw-violet-line` | `#AE3EC9` | `#AE3EC9` | legend swatch stroke: flagged or secondary |

**Swatch fill aliases.** Right after the generated block, `tdd.css` defines `--sw-green-fill`, `--sw-grey-fill`, `--sw-blue-fill`, `--sw-amber-fill`, `--sw-red-fill` and `--sw-violet-fill` as aliases of the swatch fills, and the legend rules use those names: `artifact_check.mjs` reads a variable name ending in a colour word (`--sw-green)`) as a literal colour.

**Colour in rules.** Component rules use tokens only, mixed with `color-mix(in srgb, <token> N%, var(--surface))` for tints (chip and callout backgrounds and borders, the new-step tint, the recommended option). No literal colour appears outside the token blocks.

## Type

| Family | Stack (first face) | Use |
|---|---|---|
| `--font-display` | Inter (Google Fonts, optical size 14 to 32, weights 400 to 700) | h1, h2, h3, card and decision titles (one family with the body, as the reference page: Notion-like) |
| `--font-body` | Inter | running text, tables, rail, chips |
| `--font-mono` | IBM Plex Mono (400, 500, 600) | code, path:line citations, D-numbers, PR numbers |

| Size token | Value |
|---|---|
| `--fs-xs` | `0.72rem` |
| `--fs-sm` | `0.8125rem` |
| `--fs-base` | `0.9375rem` |
| `--fs-md` | `1rem` |
| `--fs-lg` | `1.05rem` |
| `--fs-h3` | `1.0625rem` |
| `--fs-h2` | `1.375rem` |
| `--fs-h1` | `clamp(1.6rem, 1.2rem + 1.4vw, 2rem)` |

Leading: `--lh-tight` 1.15 (h1), `--lh-snug` 1.3 (h3), `--lh-body` 1.6. Tracking: `--tr-label` 0.07em (upper-case labels), `--tr-display` -0.01em (headings). Measure: `--measure` 96ch for running text; the lead runs to 120ch. Weights in use: 400 body, 500 to 600 labels and chips, 650 headings and strong.

## Space, radius, layout

Spacing (4 px unit): `--sp-1` 4px · `--sp-2` 8px · `--sp-3` 12px · `--sp-4` 16px · `--sp-5` 24px · `--sp-6` 32px · `--sp-7` 48px · `--sp-8` 56px · `--sp-9` 120px. Radius: `--r-sm` 4px · `--r-md` 8px · `--r-lg` 12px · `--r-pill` 999px. Layout: `--nav-w` 46px (the closed rail), `--content-max` 2200px, the rail switches to a phone toggle below 900px; side gutters are `max(16px, env(safe-area-inset-*))`, plus 76 px on the rail side at 900 px and up.

## Class vocabulary, section by section

The renderer uses these classes exactly; a new class goes into `tdd.css` (app-core) or `schema.css` (schema worker), never inline. Source syntax for each block: `templates/tdd-template.md` and the `build_tdd.py` docstring.

### Base

Element defaults for `body` (the `--bg` background, body font), `h1` (brand colour), `h2` (a top rule), `h3`, `h4`, `p` and `li` (the measure), `a`, `code` (`--code-bg` pill), `pre` (a bordered surface that scrolls sideways), lists (a grid with 4 px gaps), `hr`, `::selection`, `:focus-visible` (a 2 px accent ring). Utilities: `.muted`, `.small`, `.eyebrow` (upper-case label), `.sr-only`. Inside `pre`, the compiler's highlighter emits `.c` (comment: muted italic) and `.k` (SQL keyword: brand, 600); `pre.mermaid` is for an undrawn diagram.

### Page column: `.wrap`, `section`

`.wrap` is the one reading column (grid, `--sp-8` between sections, `--content-max`), with the gutters above. Each `section` is a grid with 14 px gaps and `scroll-margin-top`; its `h2` holds `.sec-no` (the muted number, followed by " · ") and an optional `.chip`. `.lead` is the section's first paragraph (`--fs-lg`, up to 120ch); `.lede` holds paragraphs before the first section. `.expand-line` with `.linkish` buttons is the "Expand all · Collapse all" line (hidden when JavaScript is off: `html:not(.js) .expand-line`).

### Contents rail: `.fnav`

A fixed, floating rail on the left, vertically centred: `.fnav` > `.fnav-panel` > `.fnav-h` (heading) and an `ol` of `a` links, each with `.n` (the section number in a 26 px tile) and `.l` (the label), and a `.sub` list of subsection links. Closed it is `--nav-w` wide and shows numbers only; on `:hover`, `:focus-within` or `.fnav.open` it widens to 290 px and shows labels and subsections. `a.active` marks the section in view (`.n` filled with `--brand`). Below 900 px the rail becomes a bottom-right `.fnav-toggle` button; `.fnav.open` shows the panel (at most 300 px wide, 70vh tall).

### Masthead and footer

`.masthead` (bottom rule) holds `.eyebrow`, the `h1`, `.meta` (a wrapping row of `b` label + value pairs: project, people, updated and rev, evidence sha) and the status `.chip`. `footer` is muted small text above a rule.

### Chips: `.chip`

A pill whose colour comes from `--chip`: tinted background and border, coloured text.

| Class | Tone | Used for |
|---|---|---|
| `.draft`, `.open`, `.default` | `--warn` (`.default` has a dashed border) | drafts, open questions and decisions, pending |
| `.new`, `.rec`, `.built`, `.good` | `--good` | new or changed, the recommended option, built or merged |
| `.done`, `.brand` | `--brand` | decided |
| `.proposed`, `.violet` | `--secondary` | proposals, planned |
| `.held`, `.blocked`, `.danger` | `--danger` | held, blocked, rejected |
| `.ruled` | solid `--ruled` ink, text in `--bg` | a binding ruling |
| no tone | `--muted` | `[chip:plain ...]` |

Chips come from `[chip:<tone> text]` in the source, the `chip:` attribute of sections and persona boxes, decision states, and the words in a table column named Status or State (`ruled`/`binding`/`accepted` = ruled; `merged`/`built`/`done`/`shipped`/`landed`/`live` = built; `draft`/`proposed`/`open`/`wip`/`planned` = proposed; `held`/`blocked`/`rejected`/`closed` = held; `default`/`pending`/`backlog` = default).

### Callouts and cards: `.callout`, `.cards`, `.card`

`.callout` is a tinted box whose `--tone` is set by `.note` or `.blue` (accent), `.warn`, `.good`, `.danger`, `.violet`; `.plain` drops the tint. A leading bold phrase takes the tone colour. `.cards` is an auto-fit grid of `.card` (230 px minimum; `.cards.four` 260 px, four across on a wide screen); a card's title is an `h4`, or in `.cards.stat` the big figure `.card .k` (1.5rem, brand, tabular numbers); `.card .chip` sits at the start of its line.

### Tables: `.tbl`

`.tbl` wraps every table: a bordered surface that scrolls sideways. `th` sits on `--surface-2`; `td.num` right-aligns tabular numbers (the compiler marks numeric columns); `td.tok` is a reference-token first cell (mono, brand); a row with an id lights up when targeted (`tr[id]:target`, or `tr.lit` after a "Go to it" jump).

### Diagrams: `figure.diagram`, legend, hotspots

`figure.diagram` > `.bar` (`.fig-title` and the `.zoom` "Actual size" button) > `.view` (the light `--plate`, scrolls) > `.stage` (the image and its hotspots) > `figcaption` (the `.key` legend and the reading). `figure.diagram.fit` limits the drawing to 82vh; `figure.diagram.zoomed` shows it at actual size inside an 85vh scroll box. The legend `.key` is a row of spans, each a `.sw` swatch plus its label; swatch tones `.green`/`.new`, `.grey`/`.app`, `.blue`/`.engine`, `.amber`/`.data`, `.red`/`.alert`, `.violet`, `.orange` (stroke) and `.dashed` (border style). Hotspots: `a.hs`, absolutely positioned in percent of the drawing, transparent until `:hover` or `:focus-visible` (accent border and tint); `.hs-pop` is the hover card (`.show` displays it; `b` label, `img`, `.go` link; `.hs-pop.has-image` is wider, up to 560 px).

### Front end: persona boxes and shots

`.fe-box` is a persona: a two-column grid, `.fe-text` (an `h3` with its chip, small paragraphs) beside `.fe-media` (the shots, wrapping). `.fe-media.cols3` and `.fe-media.cols2` are equal-column grids (1180 and 900 px max); the `details.today` fold spans the box on `--surface-2`. `.fe-duo` sets two persona boxes side by side (540 px minimum each), each a single column. Below 1000 px every box and grid is one column. `figure.shot` frames one image with its `figcaption`; sizes `.phone` (230 px wide), `.mid` (420 px tall at most), `.wide` (520 px), `.col` (the column's width; cropped from the top at 640 px in `cols3`). A click enlarges a shot: `.shot-scrim` behind `figure.shot.big` (fixed, nearly full screen).

### Steps and columns: `.steps`, `.two-col`

`ol.steps` is a list of bordered cards, each numbered by a counter in a brand circle; `li.new` is tinted good (a `[new]` step). `.two-col` is two equal columns (one column below 1100 px), used for `cols`.

### Decisions: `.dec-grid`, `.dec`, `.opts`, `.opt`

`.dec-grid` is an auto-fit grid (460 px minimum; one column below 1100 px). `.dec` is one decision card (`article`): `.head` holds `.id` (the D- or Q-number, mono), the `h3` question and the status `.chip`. State classes set a 4 px left edge: `.dec.open` (warn), `.dec.done` (brand), `.dec.held` (danger); `.dec.wide` spans the grid. `.opts` lists the options (in a wide card, side by side), each an `.opt`; `.opt.rec` is the recommended one (good tint).

### Folds and groups: `details.more`, `.group`

`.group` gathers folds under a `.group-h` heading and a small muted summary. `details.more` is a fold: its `summary` shows a rotating triangle, `.t` (the title) and `.one` (the one-line summary); `.more-body` holds the content. `details.more.today` is the "Today, for comparison" fold of a persona.

### PR cards: `.prs`, `details.pr`

`.prs` lists the cards under `.ms` milestone headings. `details.pr` > `summary` with `.n` (the PR number, mono brand), `.t` (title) and `.one` (the change); `.body` holds the deep dive (its headings are flattened to h3 size) and `.rb`, the rollback box (warn tint).

### Reference tokens: `a.ref`, `.ref-pop`, `dialog.ref-panel`

`a.ref` is a linked token (accent, dotted underline; `.ref-family` and `.ref-abbr` mark family and glossary links). `.ref-pop` is the shared hover card (`.show`; `.ref-id`, `b` title, `.ref-text` clamped to five lines, `.ref-open` button). `dialog.ref-panel` is the mobile-sized definition window (at most 420 by 720 px, on a `--scrim` backdrop): a sticky `.ref-panel-head` (`.ref-id`, `.acts` with "Go to it" and close) above `.ref-panel-body`, which shows a clone of the defined element with grids collapsed to one column. A token the page does not define renders as `span.ref-missing` (the linter fails it).

### Citations and glossary

`code.cite` is a `path:line` citation; `a.cite-link` wraps it when `repo_url` and `sha` are set (dotted accent underline on the code). `.glossary` is a definition list: each `div` row has `dt` (the term, mono brand) and `dd`.

### States at a glance

| State | Class | Set by |
|---|---|---|
| decision open / decided / held | `.dec.open`, `.dec.done`, `.dec.held` | the decision's status (a date makes it done; held, blocked or rejected make it held) or `state:` |
| chip tone | `.chip.<tone>` | the table above |
| diagram at actual size | `figure.diagram.zoomed` | the "Actual size" button (`aria-pressed`) |
| contents rail open | `.fnav.open` | the phone toggle |
| section in view | `.fnav a.active` | scroll position |
| hover cards shown | `.ref-pop.show`, `.hs-pop.show` | hover or focus on `a.ref` and `a.hs` |
| enlarged shot | `figure.shot.big` + `.shot-scrim` | a click on a shot |
| targeted row | `tr:target`, `tr.lit` | a link or "Go to it" |
| fold open | `details[open]` | the reader (native, uncontrolled) |

### Narrow screens and motion

At 640 px and below the column gap shrinks, PR summaries wrap to two columns, table cells and boxes lose padding, and decision chips wrap. `prefers-reduced-motion: reduce` turns transitions and smooth scrolling off.
