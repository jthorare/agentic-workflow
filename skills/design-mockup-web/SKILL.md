---
name: design-mockup-web
description: "Use when a screen should be drafted as an HTML page: served locally, revised on feedback until approved, then screenshotted at small, medium and large viewports as the reference images. Not for SwiftUI or simulator drafts (design-mockup-ios)."
argument-hint: <screen-name>
allowed-tools: Bash(mkdir *), Bash(ls *), Bash(SHARED_DIR=*), Bash(source *), Bash(python3 *), Bash(LOCK_NAME=*), Write, Read, Edit, Glob, AskUserQuestion, mcp__plugin_playwright_playwright__browser_navigate, mcp__plugin_playwright_playwright__browser_resize, mcp__plugin_playwright_playwright__browser_take_screenshot, mcp__plugin_playwright_playwright__browser_close, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing. Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble-extended.md` (component inventory, pipeline overview).

---

# Design Mockup — Generate HTML Mockup from Design Language

Generate an HTML mockup informed by the design language, serve it locally, iterate with user feedback until approved (capped loop), then capture viewport baselines and register the screen in `screens.json` for `/design-verify-web`.

Artifact names and the `screens.json` schema come from `_shared/design-artifact-paths.md` — read it via `SHARED_DIR` if unsure.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 0: Shotgun Seed (optional)

Check for a picked shotgun variant:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
ls "$AW_DIR/design/shotgun/picked.json" 2>/dev/null || echo "no shotgun pick"
```

If `picked.json` exists: Read it, Read the variant HTML it points to (`$AW_DIR/design/shotgun/variant-<N>.html`), and use that HTML as the **starting point** for Step 3 instead of generating from scratch. Note the seed variant in the Step 8 report.

## Step 1: Validate Arguments

The user must provide a screen name (e.g., "dashboard", "login", "settings", "onboarding").

If no screen name provided, stop:
> "Usage: `/design-mockup-web <screen-name>`
> Example: `/design-mockup-web dashboard`"

## Step 2: Load Design Context

Read `.impeccable.md` and `design-tokens.json` — palette, typography scale, spacing system, and brand personality must drive every visual decision in the mockup.

## Step 3: Generate HTML Mockup

The mockup must be a **single HTML file** with inline CSS (no external deps except CDN fonts), use **exact token values** from `design-tokens.json`, reflect the `.impeccable.md` personality (not generic Bootstrap/Tailwind defaults), be **responsive** (viewport meta + breakpoints), and use **realistic content** (no "Lorem ipsum").

Save to the canonical path and **echo it** so the user and `/design-implement-web` can find it:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/design"
echo "mockup file: $AW_DIR/design/mockup-<screen-name>.html"
```

Write the HTML to `$AW_DIR/design/mockup-<screen-name>.html`.

## Step 4: Serve the Mockup

Serve the file with a real local server and **record the URL** (`MOCKUP_URL`) — it is reused for every capture in Step 6:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
python3 -m http.server 8721 --directory "$AW_DIR/design" &
echo "MOCKUP_URL=http://localhost:8721/mockup-<screen-name>.html"
```

- If port 8721 is busy, pick another free port and re-echo the URL.
- Alternative: if the mockup must render inside the project's dev server (e.g., it uses project assets), start that server instead and record its URL.

Tell the user to open `MOCKUP_URL` in their browser for review.

## Step 5: Approval Loop (max 5 rounds)

Iterate via **Ask the user** with exactly two options: **Approve** / **Revise**.

- **Revise:** gather specifics (layout, color emphasis, typography, content density, missing elements), Edit the HTML file, tell the user to refresh, and ask again.
- **Approve:** record the current UTC timestamp as `approved_at` — it is written into `screens.json` in Step 7.
- **Cap: 5 rounds.** If round 5 ends without approval: stop, keep the latest HTML, write **no** baseline and **no** `screens.json` entry, and report the mockup as unapproved with the outstanding feedback listed.

## Step 6: Capture Viewport Baselines

Only after approval. First, the overwrite check — if any existing baseline matches, confirm before capturing:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
ls "$AW_DIR"/design/mockup-web-<screen-name>-*.png 2>/dev/null || echo "no existing baselines"
```

If baselines exist, **Ask the user**: "Baselines already exist for `<screen-name>`. Overwrite? (yes/no)". On "no", skip to Step 7 keeping the old baselines.

Acquire the browser lock in a **single bash invocation** (shell state does not persist between Bash calls):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; acquire_lock
```

Then capture **all three** viewports inline (no subagent), holding the lock throughout. For each viewport in `mobile` 375×812, `tablet` 768×1024, `desktop` 1440×900:

1. `mcp: playwright/browser_navigate` → `MOCKUP_URL`
2. `mcp: playwright/browser_resize` → the viewport's width × height
3. `mcp: playwright/browser_take_screenshot` → save to `$AW_DIR/design/mockup-web-<screen-name>-<viewport>.png` — producing exactly the three `mockup-web-<screen-name>-{mobile,tablet,desktop}.png` baselines.

Finally — success or failure — close the browser (`mcp: playwright/browser_close`) and release the lock in a single invocation:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; release_lock
```

## Step 7: Write/Update screens.json

Determine the **route** where this screen will live in the real app: infer it from the repo's routing (e.g., `app/` / `pages/` directories); if not inferable, **Ask the user** for it (a route of `null` is allowed for screens with no URL yet).

Read `$AW_DIR/design/screens.json` if it exists, merge this screen's entry (Read → mutate → Write; never clobber other screens), following the CD4 schema in `_shared/design-artifact-paths.md`:

```json
{ "schema": "screens/v1",
  "viewports": {"mobile":"375x812","tablet":"768x1024","desktop":"1440x900"},
  "screens": { "<screen-name>": {
      "route": "/path-or-null",
      "nav": null,
      "baselines": {
        "mobile":  "mockup-web-<screen-name>-mobile.png",
        "tablet":  "mockup-web-<screen-name>-tablet.png",
        "desktop": "mockup-web-<screen-name>-desktop.png" },
      "approved_at": "<ISO timestamp from Step 5>",
      "baseline_stale": null,
      "source": "design-mockup-web" } } }
```

## Step 8: Report

```
Mockup Approved
===============

Screen:       <screen-name>
Seed:         shotgun variant-<N> | none
File:         $AW_DIR/design/mockup-<screen-name>.html
Served at:    <MOCKUP_URL>
Baselines:    mockup-web-<screen-name>-{mobile,tablet,desktop}.png
screens.json: entry written (route: <route>, approved_at: <ISO>)
```

## Rules

- Every color, font size, and spacing value must come from `design-tokens.json` — no hardcoded values
- The mockup is a design artifact, not production code — optimize for visual fidelity, not code quality
- Include hover states and interactive affordances in the HTML/CSS
- If `.impeccable.md` doesn't exist, warn but still allow creation with manual style guidance
- One baseline set per screen name — overwriting requires the Step 6 confirmation
- Never write baselines or a `screens.json` entry for an unapproved mockup

## Next steps

- `/design-implement <screen-name>` — generate production code from the approved mockup
- `/design-mockup <another-screen>` — mockup additional screens
- `/design-refine` — iterate the HTML mockup before implementation

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-mockup-web-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`design-mockup-web`), `artifacts` (absolute paths: `$AW_DIR/design/mockup-<screen>.html`, the baseline screenshots and the updated `screens.json`), `summary` (one line),
plus `screen` (screen name); `viewports` (viewports captured).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
