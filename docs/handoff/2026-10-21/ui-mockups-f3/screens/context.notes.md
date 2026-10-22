# context screen: notes for the ui lane

Files: `context.html` (self-contained), `context-1440.png`, `context-900.png`, `context-filemap-1440.png`, `context-selected-1440.png` (inspector open, extra).
URL hashes for screenshots: `#files`, `#select=<nodeId>`, `#loading`, `#empty`, `#ann`, `#live`, `#hover=id,id`.

## Regions (1440 x 900; stage = below the 40 px header and the 48 px control row)
- Rail: copied exactly from workflows.html (56 px, 40 px icon tiles, tooltips, "?" to questions.html, avatar), order Home, Work, Workflows, Context, Agents, System, Settings. Context active.
- Header (40 px): breadcrumb "Context > Link expiry", "Draft map" tag, mock-state switch (dev only, drop it), Annotations switch (dev only).
- Control row (min 48 px, wraps at 900): Starts at (button, popover of up to 20 starting points), As of (segmented: "Approval on Oct 18" pinned, "Live"), view switch (Map | Files), Coverage strip (4 bars + words; click opens a popover with the four areas).
- Canvas: fixed layout, world 1360 x 872 px, three lanes (`--lane` fill, 1 px `--lane-line`, radius 8): "Why we're doing this" y 0-252, "What it touches in the product" y 304-640, "What it touches in the code" y 688-872. Dotted background 22 px, moves with pan. Default zoom = fit, clamped 62%-100%.
- Nodes: factory card 250 or 280 px wide, radius 16, 28 px tile, status footer (anchor, the two decisions). Chips: 170 px (product), 150/176 px (code), 220 px (ticket), radius 12, 24 px tile, label 12/500 (2 lines max), caption 11 muted. Dashed border = suggested or new, not confirmed. Anchor: 2 px `--ring` ring plus a "Starts here" pill; selected: 4 px blue ring with 2 px offset.
- Edges: 2 px `--wire`, dashed 8 4 for suggested, ports 10 px radius 3 at each end, rounded elbows for same-band links, S-curves between bands. Label pills (11 px) always shown on the cross-band edges (mentioned in, decided by, quoted by, conflicts, implemented in, tested by) and on hover for in-band edges (part of, happens on, writes, reads).
- "Why this matters" card (HUD, not part of the world): top left, 400 px (300 px under 1100 px width), a factory card with numbered step rows. Three sentences; each cited phrase is underlined, hover highlights those nodes and dims the rest, click opens the first. Collapse chevron; collapsed by default under 1100 px wide because it covers the first lane at 900.
- Bottom right: legend (confirmed vs suggested), zoom pill (minus, plus, %, fit).
- Inspector: 480 px over the canvas (SuperPlane run inspector): chrome row (close, previous K, next J, Esc), header (kind badge, title, meta, copyable path), then caption bars "What it is", "Why it is in this map" (the path from the item, as numbered plain sentences with links), "A conflict" (decisions only), "Where it comes from" (table: Source, When, How sure with a glyph plus words), "Connected to", then actions (Open file / Open ticket / Open the decision record / Open in the product map; Accept and Reject on suggested ones). When a node would sit under the inspector the canvas pans it into view.
- Files view (second state): replaces the canvas. Panel (`--card-warm`, radius 8) with 36 px rows, columns File / What it is for / Tickets and decisions / action tag; folders expand, a suggested row is hatched with Accept and Reject. Row click opens the same inspector. Footer says function names are not available (code index not built).
- Third state: "Building" (skeleton lanes and cards with a shimmer, centred message "Building the map for this item..."), and "No map" (message plus Build the map).

## Interactions and keys
Drag to pan, wheel to pan, Ctrl/Cmd + wheel to zoom, zoom pill, fit. Hover a node: its edges and neighbours light up, the rest dims to 42%. Click a node: select and open the inspector; click empty canvas or Esc: close. J / K next and previous node, Enter or Space on a focused node opens it, F files view, M map view. No dragging nodes, no wiring, no edit mode.

## Data gaps (each is marked `data-gap` in the HTML; the Annotations switch outlines them)
| data-gap | what is drawn | API it needs |
|---|---|---|
| `context-conflicts` | the second decision, the "conflicts" edge and "Conflict" footers | `LensesView.conflicts[]` (sample.json has one decision only; the second one is mockup-authored) |
| `context-conflicts` | the second decision, its dashed "conflicts" edge and the Conflict footers | `LensesView.conflicts[]`; sample.json has one decision only, the second is mockup-authored |
| `suggested-link` | revoke.ts and its dashed edge to the Revoke route (74%) | a suggested file link in `context-code.files[]` (suggested, confidence, linkId); mockup-authored |
| `why-text` | the three "Why this matters" sentences | a served `why` text per item (a model-written summary with the node keys each sentence cites); none exists today |
| `coverage-words` | the coverage strip words | `LensesView.coverage` (served) turned into a sentence on the client; the words are ours |
| `filemap-intent-counts` | the "Tickets and decisions" column of the files view | `context-filemap` entries with their intent citations per file (the view has `cites`, not counts per file) |
Edges are NOT gaps: `LensesView.edges[]` serves the cross-band ones (mentions, decides, quotes, supersedes, implements, tests) and `bands.product.edges[]` the in-product ones (part-of, on, reads, writes, step-of, depends-on, moves-to), with no overlap; draw both. Labels: decides or supersedes = "decided by", mentions or quotes = "mentioned in", implements = "implemented in", tests = "tested by"; in-product hover labels "part of", "happens on", "reads", "writes", "depends on". The shortest-path sentences in the inspector are computed on the client from those edges.

Not gaps but mockup-authored: ticket and decision dates, the "synced 2 h ago" source rows, the 20-item starting-point list, "Live" showing identical data.

## Plain-word mapping used
anchor = "Starts at"/"starts here"; thing = "data"; record = "ticket"; implements = "implemented in"; tests = "tested by"; decides = "decided by"; mentions = "mentioned in"; part-of = "part of"; on = "happens on"; provenance = "confirmed by a person", "found by the tool", "suggested by AI, N% sure, not confirmed"; lens coverage = "areas searched". Open strings (unknown kind, group or conflict word) must draw raw rather than fail (`ContextWords.Known`).

## Avalonia
- Lane bands and the world: one `ZoomBorder` (own, ~150 lines) hosting a `Canvas` with `Canvas.Left/Top` from the fixed layout; edges as one custom `Render` control with `StreamGeometry` (rounded elbows: `ArcTo`; S-curves: `CubicBezierTo`), hit-testing by a wider invisible stroke. Label pills are `Border` controls at the path midpoint.
- Layout is deterministic code in the view model: columns on a 190 px pitch, one row per node kind; the mockup's per-node x and y are in the `N` array in the HTML. Rows that would collide must offset by half a column (see actions) so vertical code links pass between chips.
- Chips: `Border` radius 12 with `TextBlock MaxLines=2 TextTrimming`. Hatched suggested row in the files view: `DrawingBrush` tile. Dashed borders: `Border` has no dash, so draw a `Rectangle` with `StrokeDashArray`.
- Hover dimming: set `Opacity` 0.42 by a pseudo-class on every node outside the neighbour set. Files view: `ItemsRepeater` with fixed 36 px rows, folders expanded by the view model.
- The "Why this matters" card and the inspector are overlays in the page grid (not in the zoomed world); the card uses `Inlines` with `Run`s styled as links (cite spans).
- Existing seams: `LensStackHost` (canvas), `FileMapHost` (files view), `NodeHost` (inspector body), `SourcesHost` (the sources table), `AnchorBox` (the Starts at button/popover).

## Update (review fixes)
Rail copied from workflows.html; links: Link expiry crumb, Open the run button and the anchor's "starts here" go to run-canvas.html?run=ITM-01JN2%2Fline%2F1; ticket and decisions link to needs-you.html#dec. Hashes #map, #filemap (= #files), #loading (= #building), #empty. The "Why this matters" card now sits in the intent band's left side (band is 252 px high, world 1360 x 872). `?item=` is accepted but every id shows the Link expiry map.

## Update 2 (final state)
- Taller intent band: 252 px (was 208) so the pinned "Why this matters" card (top left, 400 px) sits inside the band's empty left side, below the band label, covering no label or node. World is 1360 x 872, default zoom 91% at 1440, 62% floor at 900.
- Rail and links: see the Update section above. `?item=<id>` is read but every id shows the Link expiry map; fine for the prototype.
- Served edges (no data-gap): cross-band from `LensesView.edges[]`, in-product from `bands.product.edges[]`. Cross-band labels always on; in-product labels on hover: part of, happens on, reads, writes (depends on, step of, moves to would use the same pill). Remaining gaps: `context-conflicts`, `suggested-link`, `why-text`, `coverage-words`, `filemap-intent-counts`.
- Avalonia, three bands: `LensStackHost` holds one `ZoomBorder`; inside, three band `Border`s (fixed Canvas.Top and Height from the layout code: 0/252, 304/336, 688/184) drawn first, then one edge-drawing control (`StreamGeometry`, rounded elbows via `ArcTo`, S-curves via `CubicBezierTo`) above the bands, then the node controls (cards and chips) on a shared `Canvas`, then the label pills. Band titles are `TextBlock`s with an opaque band-coloured background so edges pass behind them. The summary card and inspector are siblings of the `ZoomBorder` in the page grid, not inside the zoomed canvas.
