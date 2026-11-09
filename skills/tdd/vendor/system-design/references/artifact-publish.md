# Publishing a page as a claude.ai artifact

The published artifact is the source of truth for every TDD and doc in a packet. `<slug>.md` is its editable text, `build_doc.py` builds `tdd.html` from it, and the Artifact tool publishes that page. Load the `artifact-design` skill (or take a quickstart result) before the first publish in a session: it carries the full page contract this file summarizes.

## Page contract points that matter here

`build_doc.py` with `doc-system.css` is meant to meet these, and `artifact_check.mjs` checks the rendered page. Read the built page and confirm the rest by hand.

- **No skeleton.** The tool wraps the page in `<!doctype html>`, `<head>` and `<body>`. The file has none of those tags of its own.
- **Title first.** `<title>` is the first thing in the file (only the first 8 KB is scanned): a name of two to four words, never "Name: explainer". The explanation goes in `description`. Keep the title the same across redeploys.
- **Both themes as tokens.** Every color is a token on bare `:root` (light values), redefined under `@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { ... } }` and again under `:root[data-theme="dark"]`, both with `color-scheme: dark`. `body` sets an explicit token background. No component rule uses a literal color, and no token exists only inside a dark block.
- **Gutter.** `padding-left: max(16px, env(safe-area-inset-left, 0px))` and the same on the right, set once on `body` or one outer wrapper; vertical padding uses `padding-block`. `<meta name="claude:viewport-fit-sides" content="cover">` goes beside the title only together with both paddings. A sticky side nav or header uses `top: env(safe-area-inset-top, 0px)`, never `0`.
- **No horizontal page scroll** at 390 px. Wide tables, code and diagrams scroll inside their own `overflow-x: auto` box; text, table and code columns in a grid get `min-width: 0`.
- **CDN allowlist (CSP).** Scripts only from `cdnjs.cloudflare.com`, `cdn.jsdelivr.net/npm/`, `unpkg.com`, `cdn.tailwindcss.com`, `code.jquery.com`, pinned to an exact version. Stylesheets only from Google Fonts, with a real fallback stack. Everything else, `doc-system.css` included, is inline. Any other host is blocked with no visible error.
- **Mermaid is native.** `<pre class="mermaid">` renders without a library; never load one. Packets embed the drawn tldraw SVGs; native mermaid is only for a diagram not drawn yet.
- **Media through `files`.** Diagrams, images, media-lane files and clips are published files referenced by relative path (`diagrams/system.svg`, `media/<file>`, `video/overview.mp4`), never data URIs (they count toward the page limit) and never another host. Give each `<video>` `controls`, `playsinline`, `preload="metadata"` and its poster still. Sound plays only after a click; the clips are silent.
- **Never publish a `licensed-reference` media entry.** `build_doc.py` leaves them out of the page and the manifest; check the manifest anyway.
- **Limits.** Page <= 16 MB, inline data URIs included. Each binary file <= 15 MB, each text file <= 16 MB, standard web media types only. One publish sends at most 255 files and 64 MB; a version holds at most 511 files and 256 MB. Clips past one publish's 64 MB go up over several publishes to the same `url`.
- **Sources stay local.** Every `files` source must sit under the working directory or the scratchpad. Copy clips into `$SD/video/` rather than pointing at `$DESIGN_HOME/videos/` from another working directory.
- **Files map on update.** Paths you pass are added or replaced, paths you leave out are kept, and `null` removes one.
- **Read before publishing.** The tool will not distribute a file you have not read, and a script wrote this one: read the built page in full first.
- **Frame limits.** No iframes, `window.print()`, download links or `alert()`; links out open a new tab.

## The publish call

First publish of a page:

```json
{
  "action": "publish",
  "file_path": "<SD>/tdd.html",
  "files": {
    "diagrams/system.svg": "<SD>/diagrams/system.svg",
    "media/<file>": "<SD>/media/<file>",
    "video/overview.mp4": "<SD>/video/overview.mp4",
    "video/overview.png": "<SD>/video/overview.png"
  },
  "icon": "diagram",
  "description": "Engineering design for <title>: modules, data model, rollout and PR order."
}
```

- `files` is the manifest `build_doc.py` wrote (`<slug>.files.json`), passed whole: a `{published path: source}` map as is, or the list form with `root` set to `$SD`.
- `icon`: one generic word, first publish only. `description`: one sentence.
- The result carries the URL. Write `artifact_url: <url>` near the top of `status.md`, then rerun `write_packet.py` (Process step 12).
- Each other persona page (`product-design.html`, ...) is its own artifact, published the same way; list its URL under `artifact_url`.

## Redeploy

- **Same session:** publish again with the same `file_path`, plus `files` for any media that changed. Omit `icon`.
- **Later session:** pass `url` from `status.md`. First `action: "read"` it (a publish to an artifact this session has not read is refused), and `action: "list"` with `scope: "files"` so the published files you replace count as seen.
- A new `file_path` without `url` makes a second artifact. Never do that for an existing design.
- A conflict hands back the newer version: merge your change into it and publish again. Pass `force` only when the owner says to discard that version.
- Small text fixes still go through the source: edit `<slug>.md`, rebuild, republish. Never patch the live page with `edits`.
- Open viewers get the new version on their own.

## Checklist

- [ ] Only `<slug>.md` and its inputs were edited by hand; `tdd.html` was rebuilt from them and PR cards rebuilt after.
- [ ] `artifact_check.mjs` exits 0, and its 1440/390 light/dark shots were read.
- [ ] The built page was read in full: `<title>` first, no skeleton tags, no literal colors, no host outside the allowlist.
- [ ] Every manifest source exists, no `licensed-reference` entry is in it, each binary is <= 15 MB and one publish is <= 64 MB.
- [ ] `icon` on the first publish only; one-sentence `description`; title unchanged.
- [ ] `artifact_url` is in `status.md` and `packet.json`.
- [ ] Any Claude Doc mirror was refreshed from `<slug>.md` with the artifact link as its first line.
- [ ] The owner got the URL; review happens on the live page.
