# Visual-critique cases for `judge check images`

Twenty synthetic screenshots, embedded in the judge binary (`Aw.Judge.Infrastructure.csproj`, `judge-images/*.png`). A file named
`<name>.<label>.png` is the case `name` that visual-critique should call `<label>`:

- `looks-right`: 10 pages that render as designed.
- `looks-off`: 10 pages with one visible break each (clipped, cut off, overlapping, missing or wrong-colored).

`dashboard`, `invoices`, `overlap-cards` and `wrong-color-button` are rendered at device pixel ratio 2 (3024x1964). The rest are
rendered at 1512x982, ui-evidence's default viewport.

To render them again, run `bash src/render.sh <out-dir>`. `src/pages.py` writes the HTML and headless Chrome screenshots it
(`CHROME` overrides the browser). A new case is a new page in `pages.py`. Keep the 10 / 10 split, since the bar in
`docs/csharp/judge.md` counts broken pages judged `looks-right`.
