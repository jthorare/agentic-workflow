# Diagram conventions

Diagrams are drawn in tldraw from mermaid sources, embedded as SVG figures, and made interactive with hover hotspots. D4 (Alec, 2026-11-08): "Keep the organization, the language and layout of the system design diagrams however we can make the boxes more ui rich. They can have on hover, should hold pngs that show full image on hover."

## 1 · Draw

The tools are system-design's, from the copy the skill resolves (`S`; see `sd_root` in `scripts/build_tdd.py`); this skill does not change them.

```bash
T="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/tdd"; S="${SYSTEM_DESIGN_SKILL:-$T/vendor/system-design}"; SD="<packet>"   # as SKILL.md sets them
node "$S/scripts/draw_diagrams.mjs" --spec "$SD/diagrams" --out "$SD/diagrams" --name "<slug>-diagrams" [--only <key>]
node "$S/scripts/svg_to_png.mjs" "$SD/diagrams/<key>.svg" "$SD/shots/diagram-<key>.png"     # Read the PNG: the Read tool shows PNG, not SVG
python3 -I "$T/scripts/diagram_hotspots.py" "$SD/diagrams/<key>.svg" --write                # writes or merges diagrams/<key>.hotspots.json
```

- One `diagrams/<key>.mmd` per diagram (`flowchart TB|LR`, `stateDiagram-v2`); prefer top-to-bottom when a graph is wide. `draw_diagrams.mjs` needs the tldraw offline app running (`open -a "tldraw offline"`; `doctor.sh` checks it), draws into your own tldraw doc (never another session's), redraws a page from scratch on every run, forces the sans font, solid strokes and a semi fill, runs the canvas lints, and writes `<key>.svg`. Fix overlapping-text lints by shortening labels or splitting the diagram.
- **Read every drawing** as a PNG before embedding it: every node and label from the `.mmd` is there, no arrow crosses a label. A diagram nobody looked at is not done.
- A redraw changes the SVG: rerun `diagram_hotspots.py --write` after it (the linter flags `DIAG/stale-svg` when the `.mmd` is newer than the SVG, and `DIAG/viewbox` when the hotspots no longer match).

## 2 · Organisation, language and layout (kept from system-design, D4)

- **Order.** The whole-system diagram opens the page (section 1, The redesign); then one diagram per mechanism where it is explained: the technical flow and the schema in section 4, today's map and facts diagrams inside section 6 folds. Embed a figure once and link to it elsewhere (`DIAG/duplicate`).
- **Boundaries as frames.** Each system or lane is a subgraph: one lane per person for journeys, then the app, then the engine or other systems, left to right in the order a request travels. The reference's redesign diagram has three frames: user journeys, holds in the web app, the ranking engine.
- **Box text.** First line: the name of the thing (a table, a service, a screen, a step), unique within the diagram, because hotspots are matched to boxes by it. Second line: what changes, in a few words ("unchanged", "new: ranks after save"). Tables list their key columns, one per line.
- **Edges.** A short verb phrase for what moves ("after save: rank all requesters", "saved ranked list with reasons"). Arrows on a schema diagram point at the referenced table.
- **Language.** The page's own words: the names the TDD uses for screens, services and tables, never a second vocabulary.
- **Title.** The `[diagram: key · Title]` title is the finding, as a sentence ("Redesign: the engine ranks a hold's requesters, and the librarian still decides").

## 3 · Tones and the legend

One meaning per colour, on every TDD. Colour boxes per diagram in `diagrams/colors.json`: `{ "<key>": [["substring of the box text", "<tldraw colour>"], ...] }`, first match wins, default grey. These roles differ from system-design's defaults (there blue is new); in a TDD, green is new.

| Role | tldraw colour (`colors.json`) | Legend tone word | Swatch (`tdd.css`) | Meaning |
|---|---|---|---|---|
| new or changed | `green` | `new` (or `changed`, `green`) | `.sw.green` | what this design adds or changes |
| the app, existing context | `grey` | `app` (or `existing`, `grey`) | `.sw.grey` | unchanged parts of the product |
| the engine, another system | `blue` | `engine` (or `blue`) | `.sw.blue` | a system this design calls but does not own |
| needs a data source | `yellow` (exported as amber `#F1AC4B`) | `data` (or `amber`) | `.sw.amber` | a value nothing supplies yet |
| where it hurts | `red` | `alert` (or `hurts`, `red`) | `.sw.red` | today's failure, a risk |
| proposed, not decided | `orange` | `proposed` (or `orange`) | `.sw.orange.dashed` | a change someone else owns, or not yet decided |
| flagged, secondary | `violet` | `violet` | `.sw.violet` | rarely: a second highlight |

The legend line under a diagram maps labels to those words; `build_tdd.py` (`LEGEND_TONES`) turns each into swatch classes:

```markdown
[diagram: flow · Technical flow: rank after the request is saved {fit; #flow-diagram}]
[legend: new or changed = new | the engine = engine | needs a data source = data | proposed = proposed]
[reading: Ranking never runs inside the request's transaction.]
```

A legend word outside that list warns (`DIAG/legend-tone`). Known gap: `draw_diagrams.mjs` forces every box to a solid stroke, so a drawn "proposed" box is orange but not dashed; the legend swatch is dashed. A dashed stroke set by hand in tldraw is erased by the next run.

## 4 · Embedding

`[diagram: <key> · <Title> {attrs}]`, then optionally `[legend: ...]` and `[reading: ...]` on the next lines (they belong to that figure).

| Attribute | Effect |
|---|---|
| `fit` | the drawing fits the viewport height (`max-height: 82vh`); "Actual size" in the figure bar zooms it (`figure.diagram.zoomed`) |
| `#id` | the figure's anchor, and an entry under its section in the contents rail |
| `alt: ...` | alt text (default: the title) |

A missing `diagrams/<key>.svg` (or `.png`) renders a placeholder and warns; the linter makes it an error (`DIAG/missing`).

A new packet starts with a placeholder for each of the template's three figures: `diagrams/redesign.svg`, `flow.svg` and `schema.svg`, copied from `templates/packet/` by `new_packet.py` (and `tdd new`) and marked "PLACEHOLDER · REPLACE ME", so the page builds and renders before anything is drawn. Until you replace it, the linter reports each one the page still shows as a `FILES/placeholder` error (`references/validators.md`): expected in a draft, and it blocks the publish run (`lint_packet.py <packet> --strict` must exit 0). `draw_diagrams.mjs` writes over a placeholder when it draws the `.mmd` of the same key. A placeholder you no longer embed (you renamed the key) is a `FILES/orphan` warning: delete it. Never publish a page that still shows one.

## 5 · Hotspots

`diagram_hotspots.py` lists every shape of a tldraw SVG export in `diagrams/<key>.hotspots.json`. `build_tdd.py` turns the boxes you fill in into positioned `<a class="hs">` links over the drawing (percent coordinates, so they scale with it), with a hover card (`.hs-pop`: label, note, image, a "go" link). One entry from the reference's schema diagram, with its four fields filled in for illustration:

```json
{
  "svg": "schema.svg",
  "viewBox": [-32.0, -182.0, 2364.2, 1084.0],
  "boxes": [
    {"label": "hold_ranking  (new) · the post's latest saved ranking", "kind": "box", "tone": "green",
     "x": 780.0, "y": 0.0, "w": 720.0, "h": 330.0, "text": "hold_ranking (new) ...",
     "href": "#schema-sql", "image": "", "note": "Derived state: rebuilt by rankPost, no audit trigger.", "table": "hold_ranking"}
  ]
}
```

- `label`: the box's first bold line, else its first line. A rerun of `--write` keeps your `href`, `image`, `note` and `table` by matching labels, so keep first lines unique and stable. A label that no longer matches a box is `DIAG/hotspot-label`.
- `kind`: `box` (a node), `frame` (a boundary or the title band), `text` (a free label, an edge label), `title`. `tone`: read from the stroke colour (green, grey, blue, amber, red, orange, violet, plus `dashed`).
- `x, y, w, h`: viewBox units. Leave them as written; a redraw rewrites them.
- Only entries with at least one of `href`, `image`, `note`, `table` render.

Which boxes get what:

| Field | Give it to | Value |
|---|---|---|
| `href` | a box explained elsewhere on the page | an anchor: `#schema-sql`, `#fe-<moment>`, `#steps`, `#q1`, `#engine`. It must exist (`REFS/hotspot-anchor`). |
| `image` | a box that stands for a screen or a state | a packet path, `mockups/<file>.png`; it shows in the hover card (D4). Must exist (`DIAG/hotspot-image`). |
| `note` | a box whose key fact does not fit on it | one or two sentences: the invariant, the failure mode, the number with its date |
| `table` | a table box on the schema diagram | the table name, with a `schema/<table>.json` beside it (D7); a click opens the schema drawer. Must exist (`DIAG/hotspot-table`). |

Leave frames, titles and edge labels empty. Most diagrams need hotspots on the new or changed boxes and the screens; every table box on a schema diagram gets `table`.
