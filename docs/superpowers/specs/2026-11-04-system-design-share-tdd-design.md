# /system-design: shareable TDD and eng-review deck

Owner: Joi, 2026-11-04. Status: approved in chat.

## Why

A `/system-design` packet's `tdd.html` is a working doc: every review pass, re-scope and owner feedback lands as one more
section, so after a few rounds a reader can't tell the current design from the history of it. One real packet had 13
numbered sections followed by six more rounds of appended addendum headings, plus a separate solver brief in four
versions. Nobody outside the work can read that cold.

The owner presents designs live in an eng review and drops a link as the pre-read. Both need the current design, told
as one story, at a level an engineer from another team can review.

## What the skill writes (new, required, engineering only)

| File | Who | Use |
|---|---|---|
| `share/story.json` | the run | the one source both pages are built from |
| `tdd-share.html` | engineers outside the work | the pre-read: one self-contained page, the link in the channel |
| `deck.html` | the owner, presenting | the eng-review deck, driven live; same story, cut to slides |

`tdd.html` stays the deep working doc (PR cards, deep dives, `path:line` tables). `product-design.html` is unchanged.

### The shareable TDD's story

Hero (the one-line pitch, chips, up to 4 stat tiles), then per chapter, in this order where it applies: the problem
today, why it keeps happening (symptom, whys, root, each with code evidence), the fix (a thesis, 2-4 pieces, "not
building / building instead"), a step-through walkthrough, a lifecycle with a clickable state machine, a before/after
comparison, the data model (new tables beside the existing ones they join), diagrams, locked decisions as cards, the
build plan as numbered slices with done-means and open questions.

### Multi-part designs

One shareable TDD with each part as a chapter (owner, 2026-11-04). A design with one part has one unnamed chapter and
no chapter chrome. The deck follows the same chapters, with a divider slide per chapter.

### Only the current answer

When an addendum overrides something earlier, the shareable TDD and the deck show only the current answer (owner,
2026-11-04). No "superseded", no strike-through, no "updated on" notes, no addendum sections. History stays in
`status.md`. `build_share.py` refuses a story that carries history markers.

### The deck

One self-contained HTML file: a 16:9 slide canvas scaled to the window, arrow keys / space / Home / End, `N` toggles
speaker notes, `P` opens a presenter window (current slide, next slide, notes, timer) kept in step over
`BroadcastChannel`, `F` full screen, `#3` deep-links a slide, printing gives one slide per page. It opens on the title,
walks the chapters, and ends on the decisions the owner wants made in the room (`asks`).

## How it is built (my calls)

- Everything is HTML; no PowerPoint or Google Slides.
- The run writes `share/story.json` (typed blocks, schema in `templates/share-story.md`); `scripts/build_share.py`
  renders both pages, inlining the theme, the project's design tokens (the `doc-style.css` `tokens_to_css.py` already
  writes) and every image, so each page is one file. Python stdlib only, like the other scripts.
- Light and dark themes; phone width with no horizontal scroll; `verify_page.mjs` runs on both pages.
- Readability limits are errors, not advice: a headline over 110 characters, a text field over 420, a list over 8
  items, a chapter over 10 sections.
- `write_packet.py` records the two pages as top-level `share` and `deck` keys, not personas, so `/system-design-video`
  doesn't film the deck.
- Generic: no workplace content in templates, tests or examples.

## Non-goals

- Replacing `tdd.html` or the product page.
- Fetching anything new: the story is built from what the packet already holds.
- A slide editor or hosted deck.
