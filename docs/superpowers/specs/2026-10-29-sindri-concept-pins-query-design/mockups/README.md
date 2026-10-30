# Concept pins mockups (phase Q7m, for the owner's pick)

Spec: `../../2026-10-29-sindri-concept-pins-query-design.md` (the owner's steps, §3.6 the sheet and Add several, §3.7 the card lines, §3.8 the layer head, §5 the texts). Static mockups, no production code. Tokens, fonts, sheet and card styles are the Projects page mockup's, so it reads as the same app. Every visible string is a literal text of the spec; the grey monospace captions (`mockup · ...`, `approved, waiting` above a card) are annotations. Numbers are the spec's own (9,111 files, 120 of 300 features, about 41 more records, About 1,240 more records); the card size lines and feature keys are sample values.

`pins.html` draws every screen from URL parameters: `s=sheet|cards|several`, `v=a|b|c`, `mode=pin|edit` (sheet), `other=1` (button reads **Pin** / **Save** / **Pin all** because other changes wait), `step=1|2` (several, C). The switcher top right flips surface and variant. `bash shots.sh` renders `screens/` with headless Chrome at 1440 and 1000 wide (Add several shots are 1100 high, the dialog is taller).

## 1. The Pin sheet (`screens/sheet-<v>-<1440|1000>.png`; Edit pin: `sheet-<v>-edit-...`; other changes waiting: `sheet-<v>-other-1440.png`)

- **A, one long column.** Same 520 px sheet as the Projects page, fields top to bottom. Good: the least new UI, reads in order, fits the Projects sheet pattern. Costs: with 5 words and 4 features plus read-only keys it is the tallest, and a 20-word list scrolls.
- **B, grouped sections.** Three bordered groups (name and definition, words, features). Good: each part is scannable and the words and features can each scroll or collapse on their own. Costs: more borders and chrome for the same content.
- **C, two panes.** A wider sheet (880 px; covers the page below 1,300 px): words on the left, features on the right. Good: shortest, words and features visible together at once. Costs: it breaks the fixed 520 px sheet width the app has, and at 1000 px it covers the whole page.

Recommended: **A**. It is the one that needs no new sheet width or grouping widget, and 20 words fit a scroll.

## 2. The pinned card states (`screens/cards-<v>-<1440|1000>.png`)

Shown side by side: waiting, rebuilding with progress, what moved, not approved, held, and a derived card with **Pin this**, under the layer head's coverage line.

- **A, inline line.** A dot and the status line under the terms. Good: quietest, a calm layer stays calm; cards keep one height. Costs: a held or not-approved card needs the owner's eye, and only the **Open review** button hints at it.
- **B, footer strip.** The status sits in a band at the card's foot, tinted when it needs the owner (not approved, held) or is running (blue, with progress bar). Good: the cards that need you stand out from across the page. Costs: the strongest, and a layer of 120 pinned cards with strips is loud.
- **C, chip plus line.** The `Pinned` / `Pinned, not approved yet` part is a chip, the rest a line. Good: the state is a scannable word and the line holds the detail. Costs: the tallest card, and the `·` sentence is split in two, so a screen reader needs the chip and the line read as one.

Recommended: **B**. The cards that need the owner (**Review and approve**, **Open review**) are the point of §3.7, and a tinted strip is the only variant that shows them without reading.

## 3. Add several (`screens/several-<v>-<1440|1000>.png`; C step one: `several-c-step1-...`)

- **A, table.** One row per line: tick, line number, the split, the match picker, its words. Good: compact, compare rows in columns. Costs: the notes (`Updates your pin Billing`, `Same name as line 3; only one is kept`) share the split's cell, and the words column gets narrow at 1000 px.
- **B, stacked rows.** Each line is a small card: the split, its note, then the picker and words. Good: room for a refusal reason under the word, reads like the sheet's rows. Costs: about 2x the height of A; 100 lines is a long scroll.
- **C, two steps.** Step one is only the text box and **Read the list**; step two is the matched rows without the box. Good: each screen has one job, so the rows get the space and nobody edits the text under results. Costs: an extra step, and fixing a line means going back, which re-reads.

Recommended: **B**. It keeps the box and the rows on one screen, so a wrong split is fixed in the row without losing the box, and notes get full width.
