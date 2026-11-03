# Image cases for `judge check images`

The labeled screenshots `judge check images` scores Jev and the agent CLIs on. Every `*.png` here is embedded in the `judge` binary
(`src/Judge/Aw.Judge.Infrastructure/Aw.Judge.Infrastructure.csproj`), so the installed judge does not need a checkout.

Layout: one file per case, `<name>.<label>.png`, where `<label>` is what `visual-critique` should answer:

- `home-plain.looks-right.png`: a page that looks right.
- `home-overlap.looks-off.png`: a page with a visible break (overlap, clipped text, missing element, wrong color, cut-off content).

The cases are rendered from small synthetic HTML pages, never captures of a real app. With no `.png` here the command prints
`no image cases embedded` and exits 1. Each case is 280 to 1568 px a side: until downscaling lands, Jev does not take a bigger one.
