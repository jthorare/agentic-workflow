# Spec: Context map (Avalonia build spec)

Screen `context`. Inputs: `screens/context.html`, `context.notes.md`, `context.questions.md` (all Recommended answers are decisions), `data/CURRENT-UI.md`. Code read at origin/main: `src/Sindri/Aw.Sindri.Ui/{Api/Context,ViewModels/Context,Views/Context,Theme}`, goldens `tests/Aw.Sindri.Tests/Golden/Api/context-{lenses,filemap}/`.

## 1. Goal and the approved decisions

Replace the text list in `LensStackHost` with a pannable, read-only map: three bands on one canvas, the edges between them, a pinned "Why this matters" card, a 480 px inspector, and a Files view. No dragging nodes, no wiring, no edit mode.

| Q | Decision (Recommended) | Effect |
|---|---|---|
| 1 | "Why this matters" pinned card in the intent band's empty left side, collapsible, collapsed under 1100 px | HUD overlay, sibling of the canvas |
| 2 | Cross-band labels always on; in-band labels on hover | label pills, two modes |
| 3 | Caption shows kind plus technical detail ("route · POST /api/links") | chip caption line |
| 4 | At 900 px: fit with a 62% floor, card collapsed, the right edge pans | zoom clamp 0.62 to 1.0 |
| 5 | Conflict footer plus a dashed "conflicts" edge; inspector links to Needs you | see 4 (served: `conflicts[]`) |
| 6 | Edges are served (`LensesView.edges[]`, `bands.product.edges[]`) | draw both, no overlap |
| 7 | `?item=` shows the item's own map (the prototype shows one sample map for every id) | the page is entered with anchors from the item; see Open items |
| 8 | Files is a sub-view (Map or Files), not part of the inspector | view switch |

## 2. Regions and layout

Page grid (replaces the `StackPanel` in `Views/Context/ContextPage.axaml`; keep the page `UserControl`, VM `ContextPageViewModel`): `Grid RowDefinitions="Auto,Auto,*"`.

| Row | Region | Size and tokens | Avalonia |
|---|---|---|---|
| 0 | Page head | existing `ContextTitle` (page-title) + `ContextHead`; right side: "Draft map" tag only when `LensesView.draft` (Radius.Pill, `Surface2`, caption); breadcrumb "Context > <item title>" when entered from an item | existing `Grid ColumnDefinitions="Auto,*,Auto"`; keep x:Names |
| 1 | Control row | min height `Size.TopBar` (48), `Space.SM` gaps, wraps below 900 | `WrapPanel` |
| 1a | Starts at | label (Eyebrow) + `AnchorBox` (existing TextBox, `MinWidth` 220, keeps x:Name and `OnAnchorKey`) + a `Button` "N starting points" opening a `Flyout` of up to 20 rows (36 px, `Size.Row`) from `LensesView.anchors[]`, each with a tick for the current set and a kind caption | `TextBox`, `Flyout` |
| 1b | As of | segmented (`ListBox` styled as segments): "Approval on <date>" (pinned) and "Live"; 30 px (`Size.Control`), `Radius.MD`, selected = `Surface3` | `ListBox` + `RadioButton` style |
| 1c | View switch | segmented "Map" / "Files" | same style |
| 1d | Coverage strip | right aligned: 4 bars (34 x 6, `Radius.XS`) + words; bar fill `Ink`, "limited" bar hatched (`DrawingBrush` tile of `InkSubtle`/`Surface3`); click opens a `Flyout` with the four areas, one 36 px row each | own `CoverageStrip` control, `Flyout` |
| 2 | Stage | fills the rest; clips; hosts layers below | `Grid` (single cell, layers stacked) |
| 2a | Canvas (`LensStackHost`, now a `Panel` kept with this x:Name) | contains one `ZoomBorder` | see below |
| 2b | "Why this matters" card | top 16 px, left 16 px, 400 px wide (300 px under 1100 px); a factory card (`Surface1`, `Radius.LG`, `Hairline` border; NO shadow, the app has none): header row (28 px tile, title "Why this matters", sub "Written from this map", collapse chevron) then numbered rows (16 px circle `Surface3`, 13/19.5 text, row divider `Hairline`) | `Border` + `ItemsControl`; each row is a `SelectableTextBlock` with `Inlines`/`Run` styled as cite (underline, `InkMuted` to `Ink` on hover) via `InlineUIContainer`-free `Run` + pointer handlers on a custom `CiteText` control |
| 2c | Zoom pill | bottom 14, right 16: minus, plus, "91%" (tabular), fit; 28 px high, `Surface3`, `Radius.SM` | `StackPanel` of `Button`s |
| 2d | Legend | bottom 14, right 150: "Confirmed" (solid line sample) and "Suggested by AI, not confirmed" (dashed 8 4); 28 px, `Surface1`, `Hairline` border | `Border` |
| 2e | Inspector | right edge, 480 px, full stage height, `Surface1`, left `Hairline` border; slides in over the canvas (`Motion.Base`, zero under reduced motion) | existing `NodeHost` (`NodeCard`) becomes this panel |
| 2f | Files view | replaces the canvas; `Surface1` panel, `Radius.MD`, padding 16 | existing `FileMapHost` (`FileMapTree`) |
| 2g | Building / No map overlays | centred message over skeleton lanes | `Border` over the canvas |

### World and bands (inside the `ZoomBorder`)
`ZoomBorder` is a new own control (about 150 lines, `Aw.Sindri.Ui/Controls/ZoomBorder.cs`): holds a `MatrixTransform`; drag pans (pointer capture on empty canvas), wheel pans, Ctrl or Cmd plus wheel zooms around the cursor, clamp 0.62 to 1.0, `Fit()` = min(stageW/1360, stageH/872) clamped. The world is 1360 x 872 and holds, in z order:

1. Dotted background: 22 px grid in `HairlineStrong`, drawn by a `DrawingBrush` tile that moves with the transform.
2. Three band `Border`s (fill `Surface1` at 40% via `LaneFill` token, 1 px `Hairline`, `Radius.MD`): "Why we're doing this" Top 0 H 252; "What it touches in the product" Top 304 H 336; "What it touches in the code" Top 688 H 184. Band title: 15/22 semibold `TextBlock`, Canvas.Left 10, Top 10, background = band colour so edges pass behind it; band meta right (12 px, `InkSubtle`): "1 ticket · 2 decisions · 1 conflict" etc., built from `bands.*.counts`.
3. `ContextEdgesControl` (own, one `Render` override): per edge a `StreamGeometry` (same-band: rounded elbows with `ArcTo` radius 12; between bands: `CubicBezierTo` S-curves), 2 px stroke `Wire`, dashed `8 4` when `!confirmed` or `provenance=="model"`; 10 px ports (radius 3) at both ends; hit test on a 12 px wide invisible stroke; hovered or neighbour edges use `WireHi`. Ends at node edges; the edge list is the union of `LensesView.edges[]` and `bands.product.edges[]` (de-duplicated on `link`, else on from,to,type).
4. Nodes on one shared `Canvas` (Canvas.Left/Top from the layout):
   - Card (anchor, decisions): width 280 (anchor) or 250 (decision), `Surface1`, `Radius.LG` (12; the prototype's 16 is open item 2), 28 px kind tile, 14 semibold title (2 lines), 12 caption, status footer row (36 px): "In the map" with tick and "starts here" (anchor), "Conflict" with warning glyph and the date (decision).
   - Chip: 150 or 176 wide (code), 170 (product), 220 (ticket); `Surface1`, `Radius.LG`, 24 px tile, label 12/500 max 2 lines (`TextBlock MaxLines=2 TextTrimming=CharacterEllipsis`; mono for code), caption 11 `InkSubtle` one line ellipsis.
   - Suggested or new: no fill, dashed `Wire` border, drawn by an inner `Rectangle` with `StrokeDashArray` (Border has no dash); caption ends "· New" or "· suggested 81%".
   - Anchor: 2 px `RingAnchor` ring with 2 px canvas gap, plus a "Starts here" pill. Selected: 4 px `PrimaryHover` ring, 2 px offset (reuse the app focus ring geometry, `Size.FocusRing` 2, so FocusRingTests still hold).
5. Label pills: `Border` (11/16, `Surface3`, `Hairline`, `Radius.SM`, padding 1/7) centred on the path midpoint (t = 0.5 computed in the VM). Dashed pill border for suggested. Always shown: cross-band edges. Hover only (opacity 0 to 1, `Motion.Fast`): in-product edges (also shown when one end is hovered or selected).

Hover rule: pointer over a node sets pseudo-class `:dim` on every node, edge and pill outside {node, its edges, its neighbours} (`Opacity` 0.42; pill of dimmed hover-only edges 0). Selection keeps the same highlight.

### Layout (deterministic, in the VM: `ContextMapLayout`, pure, unit-tested)
Columns on a 190 px pitch from x 109 (chip centre). Rows by kind; ordering inside a row by the mean x of the nodes it links to (barycenter), ties broken by key. A row that would collide with a vertical link in the next row offsets by half a column (95 px) so vertical links pass between chips.

| Band | Row (centre y) | Contents |
|---|---|---|
| Intent | 80 (ticket chips, 220 wide); 44 to 252 for decision cards (250 wide, 330 pitch) | `bands.intent.records[]`, `bands.intent.decisions[]` (own order: newest first) |
| Product | 350 (anchor cards 280 wide, then `neighbors`/`affects` chips 170 wide) | `bands.product.own[]`, `neighbors[]`, `affects[]` |
| Product | 482 (feature-owned surfaces: route, screen, dialog, email, API) | product nodes whose `part-of` target is on row 350 |
| Product | 568 (actions, data) | nodes whose edge type is `on`, `reads`, `writes` |
| Code | 732 (files, 150 or 176 wide) and test chips | `bands.code.files[]`; files with `tests` set draw a test glyph; suggested file (`suggested`) uses the dashed style |

Heights above are the prototype's N array values (`context.html` lines 400 to 440): the ui lane reproduces that sample's frame as its golden layout test. More than about 9 chips per row wraps to a second row 86 px lower and the world and band heights grow by that amount (bands never overlap; `Truncated` bands add a "more not shown" chip, existing `Labels.ContextTruncated`).

### Tokens
The app is dark Linear-derived (`Tokens.axaml`); the prototype is a warm dark. Map by role, reuse existing tokens wherever the role exists. Any palette change from the shared token pass wins; only roles with no token below are NEW.

| Prototype role | Token | Note |
|---|---|---|
| page `--bg` | `Brush.Canvas` | |
| card, chip, inspector, popover | `Brush.Surface1` | |
| field, segmented track | `Brush.Surface2` | |
| menus, pills, zoom pill | `Brush.Surface3` | |
| `--border`, band line | `Brush.Hairline` | |
| text / muted / subtle | `Ink` / `InkMuted` / `InkSubtle` | `InkTertiary` stays disabled-only |
| selected ring, cite hover | `Brush.PrimaryHover` (ring), `Brush.PrimarySoft` (cite fill) | |
| conflict, suggested warn | `Brush.Attention` + glyph + word | never colour alone |
| ok (In the map) | `Brush.Success` + tick + word | |
| NEW `Color.Wire` | `#4a4a52` | edge, port, dashed border; vs Canvas contrast must be pinned (decoration, 1.3+ like Hairline) |
| NEW `Color.WireHi` | `#8a8f98` | hover and selected edge (equal to InkSubtle value; add anyway for the role, or alias) |
| NEW `Color.LaneFill` | `#ffffff` at alpha 0.018 | band fill; alpha token |
| NEW `Color.RingAnchor` | `#d0d6e0` | the anchor ring (equal to InkMuted; alias is acceptable) |

Adding `Color.*` keys changes `TokenTests` (exact key set and the `Brush.*` mirror), `ContrastTests` (Wire/Canvas, Wire/Surface1 as decoration, InkSubtle/Surface1 for captions on chips) and `FluentOverrideTests` only if a Fluent override reads them (none planned). Hex lives only in `Theme/Tokens.axaml`. Shadows: the prototype's node and popover shadows are dropped (the app rule is no shadows); the 1 px outline carries the edge.

## 3. States

| State | Prototype hash | How the VM reaches it |
|---|---|---|
| Map | `#map` | `ContextPageViewModel.HasStack` and a successful `GET /sindri/context/lenses` |
| Files | `#files` / `#filemap` | `ViewMode = Files` (key F or the switch); calls `GET /sindri/context/filemap` (existing `FileMapViewModel`) |
| Selected (inspector open) | `#select=<nodeId>` | `SelectedKey` set by click, J/K, Enter; `GET /sindri/context/node?key=` fills `NodeCardViewModel` |
| Hover | `#hover=a,b` | `HoverKeys` (pointer over node, or a cite in the card) |
| Building | `#loading` / `#building` | a map build job for the item is running (the item's build job, `GET /sindri/jobs`; BuildContextMap), or `lenses` answers the served "building" shape if one exists; skeleton lanes, shimmer (`Motion.Pulse`, static under reduced motion), message "Building the map for this item..." |
| No map | `#empty` | the served no-map answer (existing `ContextNoMap`): message plus a "Build the map" button (action `BuildContextMap`, job) |
| Refusal | none in prototype | existing `Problem`/`Choices` (SND-CONTEXT-003 / -037 suggestions, -006 "At most 20 topics", -044 draft lenses): drawn as a banner row above the stage, canvas hidden when no `bands`; keep `ContextProblem`, `ContextChoices`, `ContextMissing` x:Names |
| 900 px | `context-900.png` | stage under 1100: card collapsed (`HudCollapsed` default), width 300 when opened; control row wraps; canvas zoom = fit clamped at 0.62, right side pans |
| Annotations / mock switch | `#ann`, `#live` | dev only, NOT built |

## 4. Data

| Region | Route / fields | Status |
|---|---|---|
| Whole map | `GET /sindri/context/lenses?anchor=…&depth&mapVersion&draft` → `LensesView` (`Api/Context/LensesView.cs`), golden `Golden/Api/context-lenses/map-tabs.json`, `set-two-anchors.json` | SERVED |
| Intent nodes | `bands.intent.records[]` (`key,label,source,trust,state,kind`), `decisions[]` (`node,label,text,at,decides,supersedes,quote,confirmed`), `counts` | SERVED |
| Product nodes | `bands.product.own/neighbors/affects[]` (`node,label,kind,confidence,confirmed,provenance`), `counts`, `truncated` | SERVED |
| Code nodes | `bands.code.files[]` (`path,implements,tests,suggested,linkId,confidence,kind`), `symbols` | SERVED (function names: `symbols.state` unavailable shows "function names not available yet") |
| Cross-band edges | `edges[]` (`from,to,type,provenance,link,confirmed,confidence`). Label map: `decides`,`supersedes` "decided by"; `mentions`,`quotes` "mentioned in"; `implements` "implemented in"; `tests` "tested by" | SERVED |
| In-product edges | `bands.product.edges[]` types `part-of` "part of", `on` "happens on", `reads` "reads", `writes` "writes", `depends-on` "depends on", `step-of` "step of", `moves-to` "moves to" | SERVED |
| Conflicts | `conflicts[]` (`kind supersedes|contradicts, node, decision, other, summary`). The footer and the dashed "conflicts" edge (decision to `other`) draw from it | SERVED (the sample has one decision, so the second decision card is a fixture, not data) |
| Top bar | starts at = `anchors[]`; as of = `asOf` + `mapVersion` + `draft`; coverage = `coverage.{scope,intent,product,code,gaps,map}` and `searched.sources[]` | SERVED (words built on the client) |
| Pinned vs Live | pinned = `GET /sindri/context/lenses?anchor=…&mapVersion=<v>` with the version the approval saw: each lens entry in the frozen scoping packet's `lenses.json` carries `mapVersion` (e.g. `v12@a41b07d`, rule PKT-SCP); Live = no `mapVersion`. Only retained versions answer: a pruned version needs a "That version of the map is no longer kept. Showing the live map." state | SERVED for the read (context lane, 2026-10-20). PENDING: api lane, a route from an item to its frozen scoping packet's anchors and `mapVersion` (see Open items 1). Until then the pinned button is disabled with the plain "not served yet" line |
| Why this matters text | a per-anchor `why: [{text, cites:[nodeKey]}]`, optional on `LensesView` (absent when not written) | PENDING: context lane, proposed new "explain" stage (model-written, fenced as untrusted, cached per anchor and mapVersion), needs a short spec and the owner's nod (`LensesView.why[]`; new field means golden + `SindriUiJsonContext` change). Fallback: card shows title and "No summary written for this map yet." (plain "not served yet" pattern); cites and hover-to-cite are inert |
| Inspector, what it is | `GET /sindri/context/node?key=` → `NodeView` (`node.label,kind,provenance,confidence`, `out[]`,`in[]`) | SERVED (the one-sentence "what it is" description is not a field: show kind word plus label; PENDING context lane for prose) |
| Inspector, why in the map | shortest path from the anchor over `edges[]` and `product.edges[]`, computed on the client (pure function `MapPath`) | SERVED inputs |
| Inspector, conflict | `conflicts[]` filtered by node | SERVED |
| Inspector, where it comes from | `GET /sindri/context/sources` → `SourcesView.sources[]` (`source,kind,state,detail,lastSync,records`) joined with the node's record `source`; "How sure" from `provenance`/`confidence`/`confirmed` | SERVED. The mockup's per-node source rows ("synced 2 h ago") come from `SourceRow.lastSync`; a per-node citation list is not served (PENDING context lane); show one row per distinct source of the node's record and edges |
| Connected to | `NodeView.out[]` + `in[]` | SERVED |
| Accept / Reject on suggested | `POST` of the review route with `DeskVerb.Accept` / `Reject` and the edge `link` (`linkId`), `Api/Context/ReviewDecisionRequest.cs` | SERVED |
| Files view | `GET /sindri/context/filemap?path=` → `FileMapView.entries[]` (`name,path,type,status,files,features,intent`), `suggested[]`, `unmapped[]`, `totals`, `symbols` | SERVED. Column "Tickets and decisions" uses `FileMapEntry.intent` (existing `FileIntent`): per-file counts if the field holds counts, else the plain "not served yet" dash. If counts are missing: PENDING context lane |
| Starts-at list | `anchors[]` of the current read | SERVED; a search list of other starting points: use the existing 003/037 suggestions only |

## 5. Actions and keys

| Control | Action |
|---|---|
| Build the map (empty state), Rebuild | `BuildContextMap` (existing id, job) via the existing job path; this page never starts it silently |
| Accept / Reject (inspector, files row) | `ReviewContextMap:accept` / `ReviewContextMap:reject` (existing ids, desk class: one click, no confirm) |
| Accept all in a row (none) | not built |
| Open file / Copy path | local actions (open in editor via the platform launcher, `Clipboard`); no catalog id (local) |
| Open ticket in Linear | open URL from the record (`LensRecord.source` + key); local |
| Open the decision record | deep link `decision/<node>` (existing deep-link kind) |
| Open in Needs you (conflict) | deep link `needs-you`; no new id |
| Open in the product map | selects the node on the map (local) |
| Open the run (breadcrumb) | deep link `workflow/<address>` |
| Promote, Import, Propose, Backtest, Sync sources | untouched; stay on the existing review and side hosts (ReviewHost, SourcesHost). Protected ones keep terminal-copy (`PromoteContextMap`, `ImportContextMap`) |
| NEW action ids | none |

Keys (active when the page has focus and no text box is focused; they do not clash with G-chords or F6):

| Key | Effect |
|---|---|
| Drag, wheel | pan; Ctrl or Cmd + wheel zoom; `+`, `-`, `0` zoom and fit |
| Click node | select, open inspector; click empty canvas or Esc closes |
| Enter or Space | open the focused node (nodes are focusable in layout order: intent, product, code) |
| J / K | next / previous node (inspector follows, canvas pans the node out from under the inspector) |
| F / M | Files view / Map view |
| Tab | move between the HUD, the nodes, the zoom pill |

Every node gets `AutomationProperties.Name` = "<kind word>: <label>" and the ring is never the only selected cue (the inspector opens).

## 6. Copy (`Labels.Context.cs`; no em dashes, no internal terms)

| Key | Text |
|---|---|
| `ContextCrumb(title)` | "Context > {title}" |
| `ContextDraftTag` | "Draft map" |
| `ContextStartsAt` | "Starts at" |
| `ContextStartingPoints(n)` | "{n} starting points" |
| `ContextAsOf` | "As of" |
| `ContextAsOfPinned(date)` | "Approval on {date}" |
| `ContextAsOfLive` | "Live" |
| `ContextViewMap` / `ContextViewFiles` | "Map" / "Files" |
| `ContextCoverageWords(n,of,limited)` | "{n} of {of} areas searched. {Area} is limited." |
| `ContextBandIntent` / `Product` / `Code` | "Why we're doing this" / "What it touches in the product" / "What it touches in the code" |
| `ContextBandMeta*` | "{n} ticket(s) · {n} decision(s) · {n} conflict(s)"; "Starts at {n} feature(s) · {n} knock-on effect(s) · {n} areas in all"; "{n} files · {n} test(s) · function names not available yet" |
| `ContextStartsHere` | "Starts here" |
| `ContextInMap` | "In the map" |
| `ContextConflict` | "Conflict" (exists as lowercase "conflict": add a title-case key, do not change the existing one if `LabelsTests` pins it) |
| `ContextLegendConfirmed` / `LegendSuggested` | "Confirmed" / "Suggested by AI, not confirmed" |
| `ContextEdgeDecidedBy` etc. | "decided by", "mentioned in", "quoted by", "conflicts", "implemented in", "tested by", "part of", "happens on", "reads", "writes", "depends on", "step of", "moves to" |
| `ContextSuggestedPct(n)` | "suggested {n}%" |
| `ContextNew` | "New" |
| `ContextKind*` | data (not "thing"), ticket (not "record"), feature, screen, route, dialog, API, action, test, decision, file |
| `ContextWhyTitle` / `WhySub(date)` | "Why this matters" / "Written from this map · {date}" |
| `ContextWhyNone` | "No summary written for this map yet." |
| `ContextCollapse` / `Expand` | "Collapse summary" / "Show summary" |
| `ContextInspWhat` / `Why` / `Conflict` / `Source` / `Connected` | "What it is" / "Why it is in this map" / "A conflict" / "Where it comes from" / "Connected to" |
| `ContextPathStart(item,node)` | "This item, {item}, starts at {node}." |
| `ContextSrcCols` | "Source", "When", "How sure" |
| `ContextSure*` | "confirmed by a person", "found by the tool", "suggested by AI, {n}% sure, not confirmed" (glyph plus words) |
| `ContextConflictBody(other)` | "This decision disagrees with {other}. Both apply to {node}. Settle which one wins before approving." plus link "Open in Needs you" |
| `ContextOpenFile` / `OpenTicket` / `OpenDecision` / `OpenInMap` / `CopyPath` | "Open file" / "Open ticket in Linear" / "Open the decision record" / "Open in the product map" / "Copy path" |
| `ContextAccept` / `Reject` | "Accept" / "Reject" |
| `ContextFilesCols` | "File", "What it is for", "Tickets and decisions" |
| `ContextFilesFoot` | "Function names are not available yet: the code index isn't built." and "Accept or Reject saves one change to the draft map." |
| `ContextBuilding` / `BuildingBody` | "Building the map for this item..." / "Reading the ticket, the product map and the code for {item}. This usually takes under a minute." |
| `ContextEmptyTitle` / `Body` / `Build` | "No map for this item yet" / "Build it to see why this work exists and what it touches." / "Build the map" |
| `ContextZoomIn/Out/Fit` | "Zoom in" / "Zoom out" / "Fit the map" |
| `ContextCovAreas` | "Tickets and decisions", "Product", "Code", "Function names" (the four areas in the popover, each with "searched" / "limited" / "not available") |
| `ContextPinnedNotServed` | "Not served yet: the map as it was at approval." |

Unknown kind, group or conflict words render raw (`ContextWords.Known` false): tile uses a generic dot glyph, label is the served string.

## 7. Tests the ui lane must add or change

| Test class | Change |
|---|---|
| `TokenTests`, `ContrastTests` | NEW (add keys Wire, WireHi, LaneFill, RingAnchor; key set and Brush mirror; Wire and ring pairs) |
| `FluentOverrideTests` | unchanged unless a Fluent override is added (none) |
| `NamedControlsTests` | keep `LensStackHost, FileMapHost, ReviewHost, NodeHost, SourcesHost, AnchorBox`; add `ContextCanvas`, `ContextHud`, `ContextInspector`, `ContextZoom`, `ContextCoverageStrip`, `ContextViewSwitch` |
| `ContextPageViewModelTests`, `ContextPageWindowTests`, `LaneAnchorTests` | update: seams still present; the old text band lines (`Bands`, `Edges`, `Coverage`) are replaced by the map VM, so assertions on those lists move to the new `ContextMapViewModel`. The "More about this answer" `Expander` (`ContextMore`) is removed; its content is the coverage flyout (record this in the PR) |
| NEW `ContextMapLayoutTests` | pure: positions for the `map-tabs` golden and for the prototype sample (frame of 1360 x 872), barycenter order, half-column offset, wrap at 10 chips |
| NEW `ContextEdgeLabelTests` | label map for every served `type`, unknown type draws raw, cross-band always on, in-band hover only |
| NEW `ContextHoverTests` | neighbour set, dim set, cite hover highlights the cited keys |
| NEW `MapPathTests` | shortest path sentences from the anchor to each node, disconnected node, multi-anchor |
| NEW `ZoomBorderTests` | clamp 0.62 to 1.0, wheel zoom around cursor, fit, pan into view |
| NEW `ContextMapFramesTests` | headless frames: map, selected, files, building, empty at 1440 and 900 (BannerLayout-style 900 px check: no horizontal page scroll, card collapsed) |
| `LabelsTests` (Context) | every new key above, no em dash, no internal term |
| `ActionLabelsTests`, catalog | unchanged (no new ids) |
| API golden tests | unchanged unless `why` / per-file intent counts land; then add them to the golden, `SindriUiJsonContext` and `FakeSindriApi` |
| `SpecTextTests`, `docs/csharp/sindri-ui.md` | update the Context section to the map; add the F3 words |

## 8. Build order (each slice red test first)

1. Tokens and own controls: new tokens (with Token/Contrast tests), `ZoomBorder`, `ContextEdgesControl` skeleton, `CoverageStrip`. Proves: zoom/pan math, edge geometry from fixed coordinates, token pins. No page change.
2. Map VM and layout: `ContextMapViewModel` (nodes, edges, pills) from `LensesView`, `ContextMapLayout`, the label map, band meta, drawing into `LensStackHost` (old text lists removed), control row with Starts at, As of (Live only), view switch. Proves: layout and label tests on the `map-tabs` golden and frame at 1440/900.
3. Selection and inspector: click, J/K, hover dim, the 480 px inspector into `NodeHost` and `SourcesHost`, shortest path, conflict section, Accept/Reject (`ReviewContextMap:accept|reject`). Proves: node route per key, desk verbs, Esc.
4. Files view and states: the `FileMapHost` restyle (36 px rows, hatched suggested rows), Building and No map overlays with `BuildContextMap`, refusal banner. Proves: file map golden rows, build action, empty and building frames.
5. Pending data (separate PRs after other lanes land): "Why this matters" card text and hover-to-cite (needs `why`), pinned "Approval on" (needs the approval's `mapVersion`), per-file intent counts.

## 9. Open items for the ui lane

1. `?item=` (Q7): the app opens Context from an item with its anchors, but no item route lists them; no route lists them today (context lane confirmed); the anchors live in the item's frozen scoping packet (`lenses.json` `anchors`, plus the scope map). api agreed a route (2026-10-20, PENDING api, ~1 day): `GET /sindri/items/{id}/scope` → 200 `{item, scope: {anchors:[{key, mapVersion}], mapVersion: <common or null>, packetHash, frozenAt} | null}`; unknown item 404 `SND-ITEM-404`; `scope: null` = no frozen scope (show Live only). Note: until engine's `step.scope.freeze` exists (it emits `scoping.approved` and keeps the packet), every item answers `scope: null`, so in practice the typed anchor box stays the entry for now. An anchor read at two versions appears once per (key, mapVersion). UI: pin when `mapVersion` is set; when null, show "These starting points were read at different times" and offer each anchor's own pinned view from the Starts at list. Until then Context takes anchors from the typed `AnchorBox` and deep link `node/<key>`.
2. Card radius: prototype 16 for cards, app max is `Radius.LG` 12. Use 12 (no new token) unless the token pass adds 16; chips already 12.
3. Closed `Flyout` content is not in the visual tree, so `AnchorBox` stays inline in the control row to keep `NamedControlsTests` green; the flyout only lists the anchors.
4. `conflicts[].kind == "supersedes"`: the cross-band `supersedes` edge reads "decided by" per the notes; for the conflict itself choose a word ("replaced by") with the owner; unknown kinds draw raw.
5. Node prose for "What it is" and per-node source rows beyond `SourceRow` are not served; asked of the context lane.
6. The prototype `Radius`/shadow differences (no shadow, dropped) and the warm palette: confirm the shared token pass; this spec uses roles so it survives either choice.
7. Minimap is out of scope here (not in the approved prototype).

## Answered by ui (2026-10-20, ui digest ~/.agentic-workflow/digests/sindri-ui-f3-spec-context.md)
- Pinned read (frozen packet per-lens `mapVersion`, pruned-version state) and `GET /sindri/items/{id}/scope` are recorded by ui.
- The typed `AnchorBox` stays the entry until scope serves non-null (engine `step.scope.freeze`).

## Update (api, 2026-10-20)
- `GET /sindri/items/{id}/scope` is PR #503, ready with meta. Goldens in tests/Aw.Sindri.Tests ApiGoldens: item scope `scope-found`, `scope-mixed-versions`, `no-scope`, `not-found`. Until engine's `step.scope.freeze` lands every item still answers `scope: null`, so the typed `AnchorBox` stays the entry.

- 2026-10-20 (#522, context slice 1): Wire 2.4:1 on Canvas = decoration, as the spec intends (a suggested edge is told apart by the dash 8 4, the legend and the caption words, never by colour). Builder choices accepted: zoom step 1.2, wheel pan 40 px per notch, pan limit keeps 120 px of the map on stage.
