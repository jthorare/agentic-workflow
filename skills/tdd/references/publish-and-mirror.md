# Publish and mirror

The published claude.ai artifact is the source of truth for a TDD. `<slug>.md` and the packet files are its editable source; `build/tdd.html` is generated from them and published as one private artifact with its images and diagrams as published files. Notion carries a mirror row that points at it. A Claude Doc is made only when the owner asks, and only as a mirror.

## 1 · Before the first publish

- [ ] `build_tdd.py`, `build.ts`, `write_agents.py` ran on the current source (SKILL.md step 5); `lint_packet.py <packet>` reports 0 errors.
- [ ] `node "$S/scripts/artifact_check.mjs" build/tdd.html shots` exits 0 (`S`: the system-design scripts the skill resolves, see `sd_root` in `scripts/build_tdd.py`), and you opened its 1440 and 390 px, light and dark screenshots.
- [ ] `build/content.files.json` has an empty `missing` list and an empty `over_15mb` list.
- [ ] You read `build/tdd.html` in full. The Artifact tool does not publish a file Claude has not read, and a script wrote this one (it is a few hundred KB: the inline React bundle plus the content; read it with `allow_large` or in parts).
- [ ] The packet folder is inside this session's working directories (add `~/.system-design` as one if it is not). The tool takes `files` sources only from the working directories or the scratchpad.

The page is built to the artifact contract by `app/build.ts` (CONTRACTS.md, Page assembly): `<title>` first, the viewport-fit meta, one `<style>` whose only import is Google Fonts, no doctype/html/head/body, React inline (no CDN script), readable with JavaScript off. It needs no runtime capabilities. If the Artifact tool's quickstart suggests a document type for a TDD, publish this page anyway.

## 2 · The publish call

```json
{
  "action": "publish",
  "file_path": "<SD>/build/tdd.html",
  "files": {
    "diagrams/redesign.svg": "<SD>/build/diagrams/redesign.svg",
    "mockups/ranked-four-placed-one-not-ranked.jpg": "<SD>/build/mockups/ranked-four-placed-one-not-ranked.jpg"
  },
  "icon": "diagram",
  "description": "Technical design for <ticket>: <the finding in one sentence>."
}
```

- `files` is the `files` object of `build/content.files.json`, passed whole: published path (what the page references) to the copy `build_tdd.py` made under `build/`. Do not hand-pick; every entry is referenced by the page.
- `icon`: one generic word, on the first publish only. `description`: one sentence. The title comes from the page's `<title>` (`meta.title`); keep it the same across redeploys.
- Limits: page <= 16 MB; each binary <= 15 MB; one publish <= 255 files and 64 MB.
- The artifact starts private. Sharing it is the owner's call.

## 3 · Record the URL

The result carries the URL. Write it in two places (`build_tdd.py` does not read it; the linter does):

- `status.md`, the line under the title: `artifact_url: https://claude.ai/...` (the linter reads this exact form).
- `meta.json`: `"artifact_url": "https://claude.ai/..."`.

A doc whose `status` is no longer a draft must name its artifact (`META/artifact-url`).

## 4 · Redeploy

- **Same session:** publish again with the same `file_path`, plus `files` from the fresh `content.files.json`. Omit `icon`.
- **Later session:** pass `url` from `status.md`. Read it first (`action: "read"`) and list its files (`action: "list"`, `scope: "files"`); a publish to an artifact this session has not read is refused.
- A new `file_path` without `url` makes a second artifact: never do that for an existing TDD.
- A conflict hands back the newer version: merge your change into the source, rebuild, publish again. `force` only when the owner says to discard that version.
- Every change goes through the source: edit, rebuild, lint, check, republish. Never patch the live page.

## 5 · Revisions

Every published change is a new revision, however small:

| Field (`meta.json`) | On every published change |
|---|---|
| `rev` | +1 |
| `updated` | today (YYYY-MM-DD) |
| `rev_note` | what changed, in a few words: "as built through PR 7; R2 provisional", "D4 answered; options removed" |
| `status` | only when the state moves: "Draft for review" -> "In review" -> "Approved" -> "As built", or "Superseded" |

Add the same line to `status.md` under Revisions (`- rev 3 (2026-11-06): as built through PR 7; R2 provisional. Published.`). Edits that are never published fold into the next revision. The linter warns when the source changed after the last build without `rev` and `updated` moving (`META/rev-not-moved`) and when the build is older than its sources (`META/stale-build`).

## 6 · The Notion mirror

The mirror is one row in the project's **Systems** database, refreshed after every republish and never edited on its own. It points at the artifact; it does not replace it.

**Example project** (a Systems database): `<notion-database-url>`, data source `collection://<data-source-id>`. One row per system; the rows are generated from `<generator-file>` in `<org>/<repo>`, so a property set by hand may be overwritten by the next generation: tell the owner which ones you changed. For any other project, ask the owner which database holds the mirror; never create a database unasked.

How the rows carry a TDD (as on a system named "<system name>"):

| TDD field | Systems property | Value |
|---|---|---|
| title | `System` (title) | the system's name, as the row already has it |
| slug | `Slug` | the system's short slug |
| artifact URL | `Tracker URL` | `https://claude.ai/artifact/...` |
| rev, status, date | `Last change summary` | "TDD revision 3 published 2026-11-06 (Draft for review): <rev_note>" |
| maturity | `Ladder` | `L1 doc` while it is a design |
| state | `State` | keep the board's value (`Stub` until built; `Blocked` if held) unless the owner says otherwise |

The page body starts with one line saying it mirrors the published design and the artifact is the source of truth, then **Source of truth** (the artifact URL with its revision, any other persona page, the packet path), **What it is** (the redesign's lead), **Decisions so far** (D-numbers in the owner's words, with dates), **Open** (the Q-numbers), **Plan** (the PR order in a sentence or two).

Tools: the Notion MCP server in the session (its tool names carry a per-session prefix, such as `mcp__plugin_productivity_notion__`; load them with ToolSearch). Find the row with `notion-search` (the system's name or slug) and read it with `notion-fetch`; set properties and the body with `notion-update-page`; add a missing row with `notion-create-pages` under the data source, only when the owner agrees the system needs one. Record the row's URL as `notion_url:` in `status.md` and `meta.json`.

## 7 · Not used here

- `write_packet.py` (system-design's `packet.json` for `/system-design-video`) expects `tdd.html` at the packet root; a tdd packet keeps it in `build/`. Its manifests are `build/content.json`, `build/content.files.json` and the agents lane instead.
- `build_prs.py` is not run on the page: `build_tdd.py` reads `prs/` itself and the app renders the cards.
