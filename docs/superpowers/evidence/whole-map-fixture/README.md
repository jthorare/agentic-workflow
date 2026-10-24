# Context whole map on the UI fixture (whole-map spec section 8, W3 and W4)

The real app, not a test host: `sindri serve` (the fixture's guarded temp home, `scripts/sindri-ui-fixture.sh up`, `context up`,
`context whole over|under`) and `sindri-ui` (the Avalonia desktop app, a debug build of main 2fb7b2d (chip row removed, #880)) on an Xvfb display,
driven with XTEST key and mouse events by `scripts/sindri-ui-capture.py`. The app is launched with
`sindri://context?repo=aw-split-panes`; the frame run is Home, click Context, type a query, Enter, Esc.

| Frame | State |
|---|---|
| `over-<size>-1-whole.png` | 612 nodes, over the 500-node budget: the whole map by feature (12 areas) with its one line |
| `over-<size>-2-query-zoomed.png` | the query `area 3` zooms the camera to that feature, the rest stays dimmed behind |
| `over-<size>-3-after-esc.png` | Esc goes back to the whole map |
| `under-<size>-1-whole.png` | 14 nodes, under budget: every node drawn |
| `under-<size>-2-query-zoomed.png` | the query `tab groups` |
| `under-<size>-3-after-esc.png` | Esc goes back |

`<size>` is `1440x900` or `900x800` (the Xvfb screen; the window is resized to fill it).

## First-frame time (click on Context to the first changed frame below the header; 33 ms polling)

| Map | Size | Cold (fresh app) first frame / settled | Warm first frame / settled |
|---|---|---|---|
| over (612 nodes, by feature) | 1440x900 | 0.45 s / 0.83 s | 0.13 s / 0.21 s |
| over | 900x800 | 0.10 s / 1.5 s | 0.13 s / 0.13 s |
| under (14 nodes) | 1440x900 | 0.26 s / 0.58 s | 0.16 s / 1.4 s |
| under | 900x800 | 0.08 s / 1.4 s (the app was already open, so not cold) | 0.08 s / 0.08 s |

Software-rendered Xvfb on a shared box under memory pressure (about 20 of 32 GB in use), one run each: a rough figure, not a benchmark.
An earlier run on a78148552 (before the merge of main) measured 0.095 s cold and 0.14 s warm for over at 1440.

## Seen in the frames

At 900 the minimap overlaps the last row of cards on the over-budget whole map (over-900x800-1-whole.png, Area 6).

## Finding

Entering Context from Home in a fresh app launched without the deep link (no repo yet) stayed on the empty page (header and
anchor box only) for 8 s and longer; with `sindri://context?repo=...` or on a second visit the map opens. Not a regression of this PR.
