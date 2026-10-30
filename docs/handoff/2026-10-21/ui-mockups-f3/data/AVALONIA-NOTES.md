# Avalonia 12 (MIT core only) for a SuperPlane-style canvas

Repo is on Avalonia 12.1.3 with NativeAOT for `sindri-ui` (trimming warnings IL2xxx/IL3xxx fail the AOT smoke), compiled bindings, no reflection. Anything below must also pass those two. License and version facts marked (verified) came from the library repo today; others are from memory and **must be rechecked before a dependency is approved** (new nuget packages need owner approval and a `Directory.Packages.props` pin).

## Summary table

| Need | Verdict | Approach |
|---|---|---|
| Pan/zoom canvas | Easy | `Avalonia.Controls.PanAndZoom` `ZoomBorder` (MIT, from memory), or own `Panel` + `MatrixTransform` + pointer-wheel/drag handlers (~150 lines, AOT-safe, no dependency) |
| Draggable nodes, ports, connections | Medium | Own: `Canvas` of `ContentControl` nodes (ItemsControl with `Canvas` panel, `Canvas.Left/Top` bound), edges as `Path`. Or NodifyAvalonia |
| Node-graph library | Medium risk | **NodifyAvalonia**, MIT (verified), port of WPF Nodify, pan/zoom, select, connect, auto-pan, dark+light themes, hundreds of nodes. Compat chart only lists Avalonia 11.1.0 (verified): **no 12.x support listed** and AOT/trim untested; "cutting lines" unsupported. Do not promise it; spike first |
| Bezier edges | Easy | `Path` with `PathGeometry`/`BezierSegment`, or `StreamGeometry` built in code: `M x1,y1 C x1+dx,y1 x2-dx,y2 x2,y2`; arrowheads as a second small Path; stroke dash for "pending" |
| Edge labels / health on edge | Easy | `TextBlock`/`Border` placed at curve midpoint (compute t=0.5 in the VM), colour from health token |
| Minimap | Medium | Own control: a second small `Canvas` drawing node rectangles from the same VM positions, plus a viewport rectangle bound to the ZoomBorder matrix; click/drag updates the matrix. No free library known; ~200 lines. Alternative: `VisualBrush` of the canvas in a thumbnail (works but redraws everything; fine under ~200 nodes) |
| Auto layout | Medium | Spec already wants a fixed layered drawing (System) and three fixed bands (Context), so layout is deterministic code in Core/VM (rank + order), no library. For arbitrary graphs: AvaloniaGraphControl wraps MSAGL (MIT, from memory; unmaintained risk) |
| Virtualized dense lists | Easy | `ItemsControl` with `VirtualizingStackPanel` (default in ListBox/ItemsRepeater). **`ItemsRepeater`** (in Avalonia core) for fixed-height rows; **`TreeDataGrid`** (Avalonia.Controls.TreeDataGrid, MIT since it was open-sourced; from memory) for sortable multi-column tables like Workflows/Recipes/Agents. Fixed row height 28-36 px (Linear density) is required for cheap virtualization |
| Drop shadows | Easy | `Border.BoxShadow="0 4 16 0 #66000000"` (Skia, supports multiple and inset). **Contradicts today's "no shadows anywhere" rule**: needs a spec edit and a token set (`Shadow.Popover`, `Shadow.Node`); not pinned by tests but stated in FluentOverrides and the UI spec |
| Blur / frosted glass | Hard | `Effect="blur(…)"` / `BlurEffect` blurs the control's own content, not its backdrop. Real backdrop blur = window-level `TransparencyLevelHint` (`AcrylicBlur`, `Mica` on Windows; `Blur` on macOS; none on Linux Wayland/X without compositor support) or composition `ExperimentalAcrylicBorder` (not in core 12 as stable; from memory). Treat blur as **window chrome only, with an opaque fallback**; do not design popovers that rely on it |
| Glow / halo on nodes | Easy | BoxShadow with a coloured, zero-offset large blur; or `DropShadowEffect`. Costs GPU per node; keep to selected/running nodes |
| Custom fonts | Easy | `FontFamily="avares://Aw.Sindri.Ui/Theme/Fonts#Name"` with `AvaloniaResource` (already done for JetBrains Mono); Inter via `Avalonia.Fonts.Inter` `fonts:Inter#Inter`. Fonts must be OFL/free and have their license shipped. Variable fonts: weights select works for Skia; no `font-feature` UI except `FontFeatures` (e.g. tabular nums `tnum`) on TextBlock, supported. No text-transform, no `letter-spacing` em (px only), no OpenType stylistic sets via XAML on all faces |
| Light + dark | Medium | Today dark-only and tests assume it. Approach: `ThemeVariant` dictionaries in `Tokens.axaml` (`ResourceDictionary.ThemeDictionaries` Light/Dark) with identical keys, `{DynamicResource}` everywhere (views already use DynamicResource for brushes), `RequestedThemeVariant` from Settings, a second `ColorPaletteResources x:Key="Light"` in FluentOverrides. Work: add Light values to TokenTests, a Light contrast table, and fix any `StaticResource` Brush uses (Tokens define Brush via StaticResource on Color: a variant switch needs the brushes to be `DynamicResource`-backed or defined per variant). Zero hex outside Theme/ still holds |
| Animation | Easy-Medium | `Transitions` (DoubleTransition, BrushTransition, TransformOperationsTransition) on pseudo-class changes; keyframe `Animation` with `Style.Animations` for pulses/spinners (already: Motion.Pulse, Motion.Spin). Must read `Motion.*` tokens and zero under reduced motion (stock controls need a line in FluentOverrides, as the ToggleSwitch). Animated flowing dashes on edges: animate `StrokeDashOffset` with a `Style.Animation` (works; keep to selected/running edges). Page/sheet slide: `TranslateTransform` animation. No spring physics built in; use cubic-bezier splines |
| Canvas performance | Medium | Skia retained visual tree: ~300-500 nodes with Paths ok; thousands need custom `Control.Render(DrawingContext)` drawing (one control drawing all edges via `StreamGeometry`). Hit-testing on edges: `Path` + `IsHitTestVisible` with a wider transparent stroke clone, or custom `HitTestCore` |
| Rich text / markdown / code | Medium | `TextBlock` + `Inlines`, `SelectableTextBlock`; AvaloniaEdit (MIT) for code/diff viewers (large, check AOT). Syntax highlight via TextMate needs reflection, avoid |
| Icons | Easy | Path geometries in `Theme/Icons.axaml` (existing `StateIcon`); an icon font or Projektanker/Material.Icons packages are options but add deps |
| Tooltips, popovers, context menus | Easy | `ToolTip`, `Flyout`, `ContextMenu` (themed through Fluent overrides); `Popup` overlay for the palette |
| Gradients, rounded images, masks | Easy | `LinearGradientBrush`, `RadialGradientBrush`, `OpacityMask`, `Clip`; dot-grid canvas background via `DrawingBrush` tile or `VisualBrush` with `TileMode=Tile` |
| Multi-window / floating panels | Medium | Extra `Window` per panel, or in-window `Panel` overlays with own drag Thumb. Linux Wayland positioning of windows limited |
| Native-looking title bar | Medium | `ExtendClientAreaToDecorationsHint`, `WindowDecorations`; macOS traffic lights stay; per-OS quirks; screenshots show the system title bar today |
| Platform reach | Note | Desktop (macOS, Linux, Windows) first; browser/iOS/Android heads later: avoid pointer-hover-only affordances, backdrop blur, multi-window |

## What SuperPlane-style canvas needs, concretely

SuperPlane's canvas (as the reference): infinite dotted canvas, rounded nodes with a header (icon, name, kind chip), ports on edges, curved edges with state colour, a left component palette, a right inspector, a minimap, run history at bottom.

Mapping to Sindri (read-only canvases first; Sindri's graphs are layout-deterministic, not user-authored):
1. **Canvas control** (`GraphCanvas : Control`): `ZoomBorder` child hosting an `ItemsControl` (nodes, `Canvas` panel) layered over one edges control (custom `Render` with `StreamGeometry` per edge, or `ItemsControl` of `Path`). Node = `ContentControl` with a DataTemplate per node kind (recipe state, doctor check, context node). Min/Max zoom 0.25-2, wheel+ctrl zoom, space-drag pan, fit-to-view.
2. **Interaction depth to promise**: select, focus (keyboard reading order, tab ring: existing rule "nodes focusable in reading order"), open detail in the right sheet, hover highlight of upstream/downstream, collapse bands. **Do not promise** drag-to-wire, user-authored graphs, or freeform node dragging without a separate editor spec: recipes are YAML validated by the engine (R8-3), and the UI holds no logic.
3. **Minimap**: own control, fixed 160x100 in a corner, node dots + viewport rect.
4. **Edge style**: bezier with horizontal tangents for layered left-to-right (System, recipe states), straight vertical for the Context bands between lenses; health = stroke colour from Success/Attention/Error tokens **plus** a glyph/word on the edge label (never colour alone, §11.3). Dashed + animated = in-flight.
5. **Right inspector**: reuse the existing detail sheet (520 px, 420-760), already draggable and focus-managed.
6. **Left palette**: only for a future authoring surface; today a filter/lens-toggle rail (Context lenses) is the real use.
7. **Run history bottom dock**: `GridSplitter` + virtualized list (ItemsRepeater), mirrors the existing frame tabs/timeline.

## Hard limits to design around
- No true backdrop blur inside the window; no CSS-like `backdrop-filter`.
- No `text-transform`, no `line-clamp` (use `TextTrimming` + `MaxLines`, supported), no CSS grid auto-fit (use `UniformGrid`, `WrapPanel`, or custom Panel), no `position: sticky` (use DockPanel/Grid rows with ScrollViewer inside).
- Rich per-node live content (sparklines, mini-charts): no chart control in core; draw with `Polyline`/`StreamGeometry` (cheap) or LiveCharts2/ScottPlot packages (extra deps, AOT unverified). Sparkline in a row = own control, fine.
- Accessibility: custom-drawn canvases give no automatic peers. Nodes as real controls get automation peers; edges drawn by `Render` need an off-screen text summary or a list view alternative (the spec already requires colour-never-alone and focus order).
- Headless tests render no pixels unless `AW_UI_FRAMES=1`; visual regressions come from frame PNGs captured by `FrameFile.Save`, not golden bitmaps, so mockups are judged by owner review not pixel diff.
- Light theme costs a contrast re-proof for every token pair.

## Recommended build order if the redesign proceeds
1. Token pass (colour/radius/shadow tokens, light variant) in one contract unit with the three pinning tests updated.
2. Dense list/table components (fixed-row `ItemsRepeater` or TreeDataGrid) for Workflows, Work, Recipes, Agents.
3. `GraphCanvas` (own, ZoomBorder) with System layered graph first (fixed layout, 10-20 nodes), then Context bands, then recipe states band as a canvas variant.
4. Minimap and animated edges last.
