# Pages and actions mockups (phase P2m, for the owner's pick)

Spec: `../../2026-10-29-sindri-pages-actions-design.md` (the owner's steps, §3.4 the section and Pages in no concept, §3.5 the marks, §3.6 the sheet and Accept all suggested, §5 the texts). Static mockups, no production code. Tokens, fonts and sheet styles are the concept pins mockup's, so it reads as the same app. Every UI string is a literal text of the spec. The pages, actions, concepts and files are a small sample web app, the spec's own scheduling example. The grey monospace captions (`mockup · ...`) are annotations.

`pages.html` draws every screen from URL parameters: `s=section|sheet|accept|none|today`, `v=a|b|c`. The switcher is in the bottom strip. `bash shots.sh` renders `screens/` with headless Chrome at 1440 and 1000 px wide.

## 1. Pages and actions in a concept's Why drill (`screens/section-<v>-<w>.png`)

- **A, grouped list.** Each page is a line with its marks and its button, with its actions under it in the spec's row text. Good: you see which page an action is on without reading, and 16 pages fit one scroll. Costs: the row's `on Schedule week` repeats the line above it.
- **B, page cards.** A card per page (dashed when `Suggested`), with its actions inside. Good: the pages that still need a name stand out. Costs: about half as many pages on screen as A, and the 760 px drill fits only 2 columns.
- **C, one row per action.** The spec's row as written, with the page's marks and button on its first action. Good: the most literal to §3.4's row. Costs: pages blur together, and an action on its own can't show the page's `Name this page`.

Recommended: **A**. It's the only one where both the page and its actions read at a glance.

## 2. Name this page (`screens/sheet-<v>-<w>.png`)

- **A, one column.** The app's 520 px sheet: Name, `From the map`, then each action's name with its concept ticks in a line under it. Good: the same sheet as the pin sheet's pick. Costs: 5 actions x 3 concepts already fill the sheet, and more scroll.
- **B, a block per action.** A bordered block per action headed `Action 2 of 5`, which matches what a screen reader reads. Good: the clearest grouping. Costs: the tallest of the three, so the 5th action is already scrolled past.
- **C, a grid.** Actions down, concepts across: ticking a second concept on every action is one column of clicks. Good: the fastest way to tick a concept across many actions. Costs: it needs a 900 px sheet, which covers the page below 1,400 px.

Recommended: **A**, to match the pin sheet. C is the one to take if you expect to tick many concepts per page.

## 3. Accept all suggested (14) (`screens/accept-<v>-<w>.png`)

- **A, table.** A dialog over the page, one row per page: tick, name, `From the map`, its actions, what it relates to. Good: compact, all 14 nearly fit. Costs: changing one action's concepts needs a row that opens, and a table cell can't hold that.
- **B, stacked rows in the sheet.** One row per page in the 520 px sheet. A row opens into the Name this page fields (the open row is shown). Good: §3.6's "a row opens into the single-page fields" as written, and the same fields as the single sheet. Costs: 6 rows per screen, so 14 is a scroll.
- **C, list and detail.** A 1000 px dialog with the list on the left and the picked page's fields on the right. Good: the fastest way to go down the list fixing names. Costs: a new dialog shape, and at 1000 px it covers the whole page.

Recommended: **B**. Untick a wrong page and rename a page without leaving the list, with the same fields as Name this page.

## 4. Pages in no concept (`screens/none-<v>-<w>.png`)

- **A, a sheet.** The head's `Pages in no concept (12)` link opens a sheet with the same rows as the concept section. Good: it's the same section in the same place, and Esc takes you back. Costs: a 520 px column for what may be a long list.
- **B, its own view.** The link swaps the concept cards for the list, with a `Context ›` crumb back. Good: the most room. Costs: the cards are gone while you work on the list.
- **C, a fold under the head.** The list opens in place above the cards, 3 rows and `Show all 12`. Good: in context, no new surface. Costs: it pushes the cards down the page.

Recommended: **A**. It's the same section and sheet as a concept's, so there's one thing to learn.

## 5. This repo's map today (`screens/today-<w>.png`)

The concept `Sindri desktop UI shell` on this repo's map (v7, 121 surfaces) shows the section's empty state. The head has no `Pages in no concept` link, because the count is 0. The card numbers are the map's real counts.
