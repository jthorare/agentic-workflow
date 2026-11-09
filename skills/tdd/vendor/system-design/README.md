# Vendored system-design scripts (for the tdd skill)

The tdd compiler reuses a newer, downstream variant of the system-design scripts: `build_doc.py` (the markdown subset,
the inline renderer, assets and `--sync-css`), `build_prs.py` (the `prs/pr-NN.md` loader), `artifact_check.mjs` (the
page contract and the 1440/390, light/dark renders) and the house doc design system in `templates/doc-system/`. That
variant differs from this repo's `skills/system-design`, which stays exactly as upstream ships it. So the files the tdd
skill needs live here, as a copy, and nothing under `skills/system-design` is changed.

## Origin

Byte-identical copies of the files that commit `27a51cda10` placed under `skills/system-design/` on `feat/tdd-skill`,
taken before that directory was restored to the fork's `main` (`5aa8577b3a`):

| Here | Was |
|---|---|
| `scripts/build_doc.py`, `scripts/build_prs.py` | `skills/system-design/scripts/` (`build_doc.py` loads `build_prs.py` from its own folder) |
| `scripts/artifact_check.mjs`, `scripts/verify_page.mjs` | same |
| `scripts/draw_diagrams.mjs`, `scripts/svg_to_png.mjs`, `scripts/tokens_to_css.py` | same |
| `scripts/env.sh`, `scripts/doctor.sh` | same |
| `templates/doc-system/**` | same |
| `templates/pr-cards.md`, `templates/pr-cards.css`, `templates/doc-base.css` | same (`build_prs.py` inlines `pr-cards.css`, `tokens_to_css.py` reads `doc-base.css`, the tdd template points at `pr-cards.md`) |
| `references/artifact-publish.md` | `skills/system-design/references/` |

Refresh a file by copying the newer version over it whole; never edit one here by hand, so a byte compare against its
source stays meaningful.

## How the skill finds these files

`sd_root()` in `scripts/build_tdd.py` resolves the system-design scripts in this order: `$SYSTEM_DESIGN_SKILL`, the
sibling `skills/system-design`, the provider install (`~/.claude/skills/system-design`), and last this folder. A
candidate is used only when it has the script being asked for, so upstream's `skills/system-design` (which has no
`build_doc.py` or `artifact_check.mjs`) falls through to here. `scripts/eval/ev_artifact.py` mirrors the same order
for `artifact_check.mjs`. Set `SYSTEM_DESIGN_SKILL` to a checkout of the downstream variant to use it instead.
