# House tech doc design system

The standard build path for every TDD and doc: a markdown source becomes a claude.ai HTML artifact page (the source of
truth), styled by one design system that works in light and dark, at phone and desktop widths.

| File | What it is |
|---|---|
| `tokens.json` | The tokens, in the Design System artifact-type list shape: `color.tokens[] {name, value:{light,dark}, usage}`, `type`, `spacing`, `radius`, `layout`. Edit this, never the CSS token block. |
| `doc-system.css` | The static stylesheet (no Python needed). Its `FONTS` and `TOKENS` blocks are generated from `tokens.json`; every rule below them uses tokens only. |
| `../../scripts/build_doc.py` | Markdown to artifact page, plus the `<out>.files.json` publish manifest. Python 3 stdlib, run with `python3 -I`. |
| `../../scripts/artifact_check.mjs` | Contract lint plus render check at 1440 and 390 px in light, dark and `data-theme="dark"`, with screenshots. |

## Build, check, publish

```bash
S=~/.claude/skills/system-design
python3 -I "$S/scripts/build_doc.py" "$SD/<slug>.md" "$SD/tdd.html" \
  --diagrams "$SD/diagrams" --diagram-map "$SD/diagram-map.json" --video-map "$SD/video-map.json" \
  --media "$SD/media.json" --meta "$SD/meta.json" [--project-tokens <design-tokens.json>]
python3 -I "$S/scripts/build_prs.py" "$SD/tdd.html" "$SD/prs"      # fills the PR slot (or pass --prs to build_doc.py)
node "$S/scripts/artifact_check.mjs" "$SD/tdd.html" "$SD/shots"   # exit 0 required; then open the screenshots
```

Order matters: `build_doc.py` first (it writes the `<!-- PR-CARDS -->` slot and an empty `PR-DETAIL` marker pair, since an
artifact page has no `</body>`), then `build_prs.py`. Or pass `--prs "$SD/prs"` to `build_doc.py` and skip `build_prs.py`:
the cards then render natively as `.pr-cards` with `dialog.pr-dive`, with no second `<style>` in the page. That is the
cleaner path; `build_prs.py` still works and doc-system.css restyles its `.milestones` / `.pr` / `dialog.prd` markup
(its inlined `pr-cards.css` carries one literal backdrop color, which doc-system.css overrides; `artifact_check.mjs`
reports it as a warning).

Publish with the Artifact tool: `file_path` = the built page, `files` = the `files` map of `<out>.files.json` (published
path to source). The manifest also lists `missing` (referenced, not found), `skipped` (licensed-reference media, never
published), `over_15mb` (binary files over the per-file publish limit; use a smaller cut) and `warnings`. Referenced files
are copied into the output folder at their published paths unless `--no-copy`, so the output folder renders as the
artifact will. `--inline` embeds images and posters as data URIs (videos stay files); keep the page under 16 MB.

## build_doc.py options

| Option | Meaning |
|---|---|
| `--diagrams <dir>` | Where `<key>.svg` (or `.png`) lives. Default: `diagrams/` beside the output, then beside the source. |
| `--diagram-map <json>` | `{ "<caption substring or key>": "<key>" }` for `[embedded content: caption]` markers, first match wins. A caption whose first part (before ` · `) slugs to an existing diagram file maps itself. Unmapped markers render as a muted placeholder line and a warning. |
| `--video-map <json>` | `{ "<section or h3 id>": {"src": "video/<file>.mp4", "poster": "video/<file>.png", "caption": "..."} }`. The clip renders under that section's diagram. Key `"_top"` is the overview clip in the glance block. `src` may be absolute: it publishes as `video/<basename>`. |
| `--videos <dir>` | Extra folder to find video files and posters by basename. |
| `--media <media.json>` | The media lane (below). |
| `--modules <json>` | Module grid entries, when `meta.json` has no `modules`. A `modules.json` beside the source is picked up on its own. |
| `--meta <json>` | Header, glance, module grid and callout settings (below). |
| `--prs <dir>` | Render `pr-NN.md` deep dives natively (uses `build_prs.py`'s parser). |
| `--css <file>` | Stylesheet, default `doc-system.css` here. |
| `--project-tokens <json>` | A project's W3C DTCG `design-tokens.json`; see Precedence. |
| `--inline`, `--no-copy`, `--strict` | Embed images; leave files where they are; exit 1 on any warning. |
| `--sync-css [tokens.json [css]]` / `--check-css` | Regenerate / verify the FONTS and TOKENS blocks of the CSS. |

### meta.json

```json
{
  "title": "Ledger TDD",
  "eyebrow": "acme/ledger · engineering",
  "lede": "Is the ledger buildable and safe to ship in order?",
  "byline": "Oct 30, 2026 · @alec",
  "links": [{"label": "Status doc", "href": "https://claude.ai/artifact/..."}],
  "facts": [{"label": "Sections", "value": "9"}],
  "glance": {"diagram": "system-overview", "caption": "whole system · every module in its own boundary",
             "title": "System at a glance", "lede": "One sentence."},
  "modules": [{"id": "ledger-service", "title": "Ledger service", "summary": "One line.", "status": "built",
               "status_label": "L2 · source", "diagram": "ledger", "clip": "#clip-ledger-service"}],
  "modules_title": "Modules", "modules_lede": "One card per module.",
  "callouts": {"where it stands": "good", "waiting on": "warn"},
  "prs_section": "rollout-gates-and-pr-order", "prs_heading": "PR deep dives"
}
```

Every key is optional. Defaults: `title` is the H1 cut to a 2 to 4 word name ("Pulo ng Lamat Technical Spec" becomes
"Pulo ng Lamat Spec"); `byline` is a short dated or `@` line right under the H1; other preamble paragraphs become the
lede; `facts` are Sections, Diagrams, PRs, reading time and the byline's date; `glance.diagram` is the first of
`system-overview`, `overview`, `system-map`, `architecture` found in the diagrams folder. `modules` may be a list, a
path to a JSON file (relative to meta.json), or absent: then cards come from the `###` subsections of the section whose
title starts "Game systems" plus the sections starting Architecture, Data model, Rendering, Content pipeline, Rollout,
with the summary from each one's first sentence and the status from its `**Where it stands:**` line (`L0`/`L1`
proposed, `L2` to `L4` built, `L5` ruled, labelled by the doc's own "proof ladder" line when it has one; no level gives a
neutral chip). `status` is one of `ruled proposed built held default` (or a status word like `merged`, `draft`,
`blocked`), or `none`. `diagram` is a key or `#anchor`, `clip` an `#anchor` or a `[video:]` name; by default both point
at that module's first diagram and clip.

### media.json (the media lane)

```json
[{"id": "m-wall", "kind": "still", "module": "<section or h3 id>", "file": "media/wall-run.png",
  "caption": "Wall run, frame 212", "source": "Mac Build07", "rights": "internal-proof", "captured": "2026-10-08"},
 {"id": "m-proof", "kind": "clip", "module": "posting-path", "file": "media/proof.mp4", "poster": "media/proof.png", "...": "..."}]
```

`kind` is `still`, `concept` (labelled "Concept art") or `clip` (a `<video>`); `rights` is `original`, `internal-proof` or
`licensed-reference`. Files resolve against media.json's folder and publish at the same relative path. Each module's
entries render as one `.gallery` right after its diagram and whiteboard clip, before prose. `[media: <id>]` in the
source places that entry inline instead. `licensed-reference` entries are never rendered or copied; they are listed in
the manifest's `skipped`. An entry whose `module` matches no id is a warning.

## Source markdown

Headings: one `# ` title; every `## ` is a numbered `<section id="<slug>">` with its `<h2>`; every `### ` is an
`<h3 id="<section>--<slug>">` (ids slug to 48 characters, as the earlier converter did, so anchors survive). Both get
side-nav entries. Paragraphs with `**bold**`, `*italic*`, `` `code` ``, `[links](...)`, `~~strike~~`, backslash escapes;
bullet, numbered, nested and `- [ ]` / `- [x]` check lists; GFM tables (alignment row honoured); fenced code
(` ```mermaid ` becomes `<pre class="mermaid">`, which the artifact viewer renders); `> ` quotes and `> [!NOTE]`,
`[!TIP]`, `[!WARNING]`, `[!CAUTION]` alerts; `![alt](path "title")` images; `---`; `<!-- comments -->` pass through.

Markers, each on its own line (Claude Docs exports write `&#91;...\]`; both forms work):

| Marker | Renders |
|---|---|
| `[diagram: <key> · <caption>]` | `figure.diagram` from `diagrams/<key>.svg`, numbered "Fig. N"; text before ` · ` in a caption is its bold title. |
| `[embedded content: <caption>]` | The same, key from `--diagram-map`. |
| `[video: <name> · <caption>]` | `figure.video` from `video/<name>.mp4`, poster `video/<name>.png` when that file exists. |
| `[media: <id>]` | That media.json entry, inline. |
| `[image: <caption>]` | A muted placeholder line (an image the export could not carry). |
| `<!-- PR-CARDS -->` | The PR slot. Without it the slot goes at the end of the section whose title starts "Rollout" (or `meta.prs_section`). |

House order inside every section and subsection: the first diagram marker renders directly under the heading even when
the source has it later, then the section's clip (`--video-map`, else its first `[video:]` marker), then its media
gallery, then the prose in source order. A `**Contents**` paragraph followed by a list of `(#...)` links is skipped (the
page has its own nav) and its anchors are rewritten to the new ids. A paragraph that is only `**Label**` becomes a small
run-in label. A paragraph opening with a bold label becomes a callout when the label starts with: `Where it stands`
(good), `Waiting on`, `Open` (warn), `Blocked`, `Held`, `Caution`, `Danger` (danger), `Refused`, `Note` (note), `Tip`
(good); override with `meta.callouts`. A table column headed `State` or `Status` renders known words (merged, draft,
held, ruled, default, blocked ...) as chips; all-numeric columns align right with tabular figures. A section titled
Sources or References renders its paragraphs as the `.sources` list.

## Class contract

| Class | Markup build_doc.py writes |
|---|---|
| `.doc` | Outer wrapper; the only element with the side gutter: `padding-left/right: max(16px, env(safe-area-inset-*, 0px))`, `padding-block`. Grid of `.sidenav` + `main.doc-main` at >= 1100 px. |
| `.sidenav` | `nav` with `.nav-label` and `ol.nav-list > li.nav-top > a (.nav-no, .nav-text) + ol.nav-sub`. Sticky, full height, scrolls on its own; `a.active` (with `aria-current`) and `li.nav-top.current` set by the page script. Hidden below 1100 px. |
| `.topnav` | `details` under the header with the same list; shown below 1100 px. |
| `.doc-header` | `.eyebrow`, `h1`, `.lede`, `.byline` (text + `ul.links`), `dl.meta` (`div > dt + dd` rows). |
| `.glance` | `.block-head` (h2 + p), `figure.diagram.glance-fig`, `figure.video.glance-clip`; side by side at >= 900 px, stacked below; `.solo` when only one figure. |
| `.modules` | Grid of `article.module`: `.module-top` (`.chip`, `h3.module-title`), `p.module-summary`, `ul.module-links` (diagram, clip, section; a missing one is `span.off`). |
| `section.doc-section` | `.sec-head` (`.sec-no` + `h2`), content; `h3[id]`; scroll margins set. `.sources-section` for Sources. |
| `figure.diagram` | `.plate` (light in both themes, drafting grid, its own scroll box) > `img`; `figcaption` (`.cap` with `.fig-no`, `.fig-title`, `.fig-text`; `button.zoom` toggles `.zoomed` = natural size). |
| `figure.video` | `.frame` (16:9, `--media` letterbox) > `video controls preload="metadata" playsinline poster` > `source`. |
| `.gallery` | Grid (auto-fill, min 240 px) of `figure.still`, `figure.concept` (`.media-frame > img`) and `figure.clip`; caption `.kind`, `.cap`, `.src`. |
| `.tbl` | Scroll container (`role="region" tabindex="0"`) > `table[data-cols]` (`[data-wide]` at 4+ columns sets a min width so phones scroll instead of squeezing); `.num` cells. |
| `pre code` | Code block, its own horizontal scroll; `pre.mermaid` for diagrams the viewer draws. |
| `.callout` | `.note` `.warn` `.danger` `.good`: tinted panel with a tone square, no side rail. |
| `.chip` | `.ruled` (solid ink) `.proposed` `.built` `.held` `.default` (dashed: a running default, ruling owed); no variant = neutral. |
| `.pr-slot` | Holds the PR heading and the cards; hidden until it holds cards. `.pr-cards > .pr-ms > (.pr-ms-head, ol.pr-list > li > button.pr-card)`; `dialog.pr-dive` with `.pr-dive-head`, `.pr-dive-body`. build_prs.py's `.milestones`, `.ms`, `.pr`, `dialog.prd` get the same look. |
| `.checks`, `.sources`, `.placeholder`, `.lead-label`, `.sr-only` | Check lists, source list, muted stand-ins, run-in labels, screen-reader text. |

## Tokens

Color tokens (`--<name>`), light / dark. Text tokens hold at least 4.5:1 on `bg`, `surface`, `surface-2` and `code-bg`
in both themes, and status colors hold at least 4.6:1 on their own chip tint.

| Token | Light | Dark | Use |
|---|---|---|---|
| `bg` | #F2F6F3 | #0C1613 | page |
| `surface` / `surface-2` | #FAFCFA / #E5EEE8 | #14231E / #1B2E27 | cards, tables / table head, nav active |
| `code-bg` | #E9F0EB | #0F1C18 | code |
| `fg` / `muted` | #13221D / #4B5F57 | #DCE8E2 / #94AAA1 | text (15.1 and 14.6:1) / secondary (6.3 and 7.5:1) |
| `line` / `line-strong` | #C6D6CD / #6F887C | #2A3F37 / #5E766B | hairlines / control edges (3:1) |
| `accent` | #1B7559 | #7ED8B4 | links, active nav number, focus |
| `secondary` `good` `warn` `danger` `ruled` | #3B5A86 #1B7559 #7C6210 #9C4430 #13221D | #A3BFE6 #7FE9BF #E3C66C #F0A28C #DCE8E2 | proposed, built, default, held, ruled |
| `plate` / `plate-line` | #FFFFFF / #D3DFD8 | #E6EEEA / #BDCCC4 | diagram plate (light in both themes) |
| `media`, `scrim`, `shadow` | | | video letterbox, dialog backdrop, shadows |

Type: display Bricolage Grotesque (headings, optical sizing), body IBM Plex Sans, mono IBM Plex Mono (code, numbers,
labels), all from Google Fonts with system fallbacks. Scale `--fs-xs` 12 px to `--fs-h1` clamp(34 to 52 px); body 16 px
on 1.62 leading, 70ch measure. Spacing `--sp-1` to `--sp-9` (4 to 96 px), radius `--r-sm/md/lg/pill`, layout `--nav-w`
17rem and `--content-max` 66rem.

To change the system: edit `tokens.json`, run `build_doc.py --sync-css`, then `--check-css`. The CSS shape the artifact
contract wants is generated for you: every token on bare `:root`, then the same names under
`@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {...; color-scheme: dark} }` and again under
`:root[data-theme="dark"]`.

## Precedence: project tokens

The user's words first, then the project's tokens, then this system. Pass the project's DTCG file with
`--project-tokens`: build_doc.py appends override blocks of the same three-block shape that redefine house tokens, so
dark mode keeps working. Mapping (first hit wins): `color.background` bg, `color.surface` surface, `color.surface-alt`
surface-2, `color.text` fg, `color.text-muted` muted, `color.border` line, `color.primary`/`accent` accent,
`color.secondary` secondary, `color.success` good, `color.warning` warn, `color.danger` danger, `color.ruled` ruled;
dark values from `color.dark.background`, `.surface`, `.text`, `.text-muted`, `.accent`, `.built`, `.proposed`,
`.default`, `.held`; fonts from `font.family-body`, `-display`, `-mono` (a family the house does not load gets its own
Google Fonts import); `radius.md`. It prints what it mapped, which house values it kept, and any pair under 4.5:1; ask
the owner rather than inventing a missing value. `tokens_to_css.py` (with `doc-base.css`) is the legacy light-only path
for hand-written pages; do not pass its output as `--css`.

## Artifact contract the build guarantees

Page content only (no doctype, html, head or body); `<title>` first, then
`<meta name="claude:viewport-fit-sides" content="cover">`, one `<style>` (Google Fonts via `@import`), the markup, one
`<script>` (nav highlight on scroll without IntersectionObserver, contents disclosure, diagram zoom, PR dialog; no
alert/confirm). Body background from a token; colors only as tokens; tables, code and diagrams scroll in their own
containers; flex and grid children `min-width: 0`; images `max-width: 100%`; visible focus rings; motion off under
`prefers-reduced-motion`. No iframes, no libraries: mermaid is drawn by the viewer, diagrams are tldraw SVG files
published beside the page, clips are `<video>` with `<source>`.

`artifact_check.mjs` enforces the static half and renders the rest: horizontal scroll, broken images, missing video
files or posters, console errors, text matching its background (under 1.5:1 fails, under 4.5:1 warns), side-nav
highlighting at the top and mid-page, the contents disclosure on phones, and an explicit light choice over a dark OS.
It writes `<page>-<width>-<theme>-top.png`, `-full.png` (8000 px segments on long pages), `-at50.png` and a JSON report.
Read the screenshots: a passing metric is not a review.
