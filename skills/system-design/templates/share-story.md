# The share story (`share/story.json`)

One JSON file per packet, the only source of `tdd-share.html` (the pre-read engineers get as a link) and `deck.html`
(the eng-review deck). `scripts/build_share.py <packet>` renders both; `--check` validates only. A full example with
every block type and two chapters: `share-story.example.json` next to this file.

## Writing it

- **Current answer only.** Write what the design is now. When a later round changed something, write the new answer
  and drop the old one: no "superseded", "addendum", strike-through, "(was ...)" or "updated on <date>". The history
  stays in `status.md`. The build refuses a story with those markers.
- **Story order.** Problem, why, the fix, then the demos (walkthrough, lifecycle, before/after), then data, decisions
  and the build plan. Skip a block that has nothing real to say; never pad one.
- **Headings are claims.** "Status is read, never written", not "Lifecycle". The eyebrow names the topic (defaults per
  type below).
- **Evidence is grep-able.** Whys carry `evidence` with `path:line`, table names or a count with its query.
- **Several parts, one page.** A design with several parts (a shared backend and two experiences, say) is one story
  with one chapter per part. One part: one chapter, no title needed, no chapter chrome.
- **Notes are for the deck.** `notes` on the story, a chapter, a section, a walkthrough step or an ask become speaker
  notes; without them a slide uses its `lede`.
- **Limits are errors:** headline 110 characters, any text field 420, notes 900, a list 8 items (steps 10, decisions
  12, table columns 14, chapters 6, sections per chapter 10). Over a limit means cut it or split it.

Text fields take `` `code` ``, `**bold**`, `==highlight==` (the accent colour, for the fix's thesis) and
`[text](https://...)`; everything else is escaped. Only `html` (a problem visual or a walkthrough mock) is raw HTML,
and you write it. Image paths (`src`, `image`) are relative to the packet and are inlined.

## Top level

| Key | Required | What |
|---|---|---|
| `title` | yes | short name: nav brand, page and deck titles |
| `headline`, `headline_em` | headline yes | the one-line pitch; `headline_em` is the accent-coloured tail |
| `lede` | yes | two sentences: what changes, for whom |
| `eyebrow` | | `TDD · <area> · <ticket>` |
| `chips` | | `[{text, href?}]`: lead, team, design file, tracker |
| `stats` | | up to 4 `{value, label, goal?}`; `goal` paints the target green |
| `chapters` | yes | `[{id?, title?, nav?, summary?, notes?, sections: [...]}]` |
| `asks` | | `[{q, text?, options?, notes?}]`: what the review must decide; the deck's last slides and the page's "For the review" |
| `links` | | footer `[{text, href}]`: the working TDD, the design file |
| `fonts_href` | | an `https://` stylesheet for webfonts named in the tokens; leave it out for an offline page |
| `notes` | | speaker notes for the title slide |

## Section types

Every section has `type` and `title`, and may have `id`, `eyebrow`, `nav` (short nav label), `lede` and `notes`.

| `type` | Default eyebrow | Fields | Deck |
|---|---|---|---|
| `problem` | The problem today | `pains: [{title, text}]`, optional `image: {src, alt, caption?}` or `html` beside them | 1 slide |
| `whys` | Why it keeps happening | `rows: [{text, evidence?, label?}]`: first is the symptom, last is the root | 1 slide |
| `fix` | How we solve it | `thesis`, `pieces: [{label?, title, text, where?}]` (2-4), `not_building`, `building`, `not_note`, `building_note` | thesis + pieces; versus |
| `walkthrough` | Walkthrough | `steps: [{who?, title, sub?, note, image? or html?, notes?}]` | 1 slide per step |
| `lifecycle` | Lifecycle | `states: [{name, desc?, tone?}]` (tone: good, warn, bad, mute), `start`, `actions: [{actor?, label, from?: [state or "*"], to}]`, `rule` (monospace) | states + actions |
| `compare` | Before and after | `before`, `after`: `[{name, sub?, hi?}]`; `before_label`, `after_label`; `side: [{label?, title, sub?}]` | side by side |
| `model` | Data | `tables: [{name, status: new, changed or exists, note?, cols: [{name, type?, key?, added?}]}]`, `rules: [{title, text}]` | tables; rules |
| `diagram` | Diagram | `src` (`diagrams/<key>.svg`), `alt`, `caption?` | 1 slide |
| `decisions` | Locked decisions | `items: [{q, a, text?}]`: the owner's current answers, `a` in a few words | 6 per slide |
| `slices` | Build plan | `items: [{title, text, tags?}]` (each a reviewable PR), `done`, `questions` | 5 per slide; done + questions |
| `text` | Notes | `paragraphs`, `table: {head, rows}` | 1 slide |

The lifecycle block is a small state machine people can click: each action button is live only in its `from` states.
Use it when a status is derived; skip it when nothing has states.
