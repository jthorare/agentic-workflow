---
name: ui-before-after
description: Use when a vitalizecare/web-app PR or branch changes what users see (an app screen or a Storybook component) and needs before|after proof - labelled main vs PR crops, recordings, accessibility or pixel checks, Figma parity, or staff and manager views. Use it whenever someone asks for screenshots, a before/after or proof table for a PR body, or wants to show a UI change to a reviewer, even if they don't name the skill.
argument-hint: "<pr-number-or-issue-id> [--storybook] [--axe] [--pixel] [--record] [--down]"
allowed-tools: Bash(bash *), Bash(git *), Bash(gh *), Bash(node *), Bash(curl *), Read, Write, Edit, Glob, Grep, AskUserQuestion
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

---

# ui-before-after

Proves a `vitalizecare/web-app` PR's UI claims from two local builds, `main` and the PR: the same clicks on both, labelled crops side by side, attached to the PR, with videos only when they show something a still can't.

```bash
SCRIPTS="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/ui-before-after/scripts"
OUT="$HOME/.agentic-workflow/vitalizecare-web-app/ui-before-after"   # UI_BEFORE_AFTER_OUT_ROOT overrides
```

Call scripts by absolute path, `bash "$SCRIPTS/<name>.sh"`, with no `cd` in front, and in the foreground: a cold run takes ~5 min, and a backgrounded one reports "done" at once. Write scenario files with the file-write capability, not a heredoc. Outputs: `$OUT/pr-<n>/` (an issue id `<n>` also gets the `pr-` prefix).

**Needs:** a web-app checkout at `$WEB_APP_ROOT` (default `~/work/web-app`) with `node_modules` (the runner loads Playwright and `sharp` from it), whose `scripts/worktree.sh` gives each worktree its own slot stack. `stack.sh` runs `bun install` in the PR worktree itself. Also Docker, `gh` 2.102+ (`gh pr edit --attach`), and an AWS SSO session for `vitalize-health`.

The PR body gets a "Claim | main \| PR" table, one row per claim, each with its labelled main | PR crop, plus "Not proven".

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Never

- Touch slot 0 (the main checkout at `$WEB_APP_ROOT`, :3000) or the shared dev database: slot 0 is the developer's own dev server and data. Only `dev:local` slot stacks sign in (admin, auto). Pointing local web at `dev-api.vitalize.care` loops to /login.
- Run on a PR worktree another session is working in or cleaning up (`/cleanup`): restarting its stack breaks that session. `proof.sh` refuses a dirty tree.
- Commit images or video; they go up with `attach.sh`, so the repo stays small.
- State a claim the pictures don't show: write "Not proven". Reviewers trust the table only if every row is backed.

## Steps

1. **PR worktree.** Find it in `git worktree list` by head branch; create one if missing. When the PR exists, stop if `HEAD` != `gh pr view <n> --json headRefOid`. The `PR @sha` label is the worktree's HEAD. The PR need not exist yet: prove after tests pass, then push, open the PR, attach.
2. **Scenario.** First list the affected pages (step 6 says how): a reviewer checks each one, so each needs a state. Then write `$OUT/pr-<n>/scenario.mjs`, one state per claim per affected page, from the PR diff and the issue's "ui before and after". Put every claim in this one scenario, using a state `route` for other screens, so one folder holds all the proof and attaches in one call. Before writing it, check the seed has the data the claim needs (rows, units, shifts this week). "Not proven" is only for what the seed cannot show or what would change the shared main database (see Scenario); "not run" or "same component as another page" is not a reason, since one screen can wrap the component differently.
3. **Run.** `bash "$SCRIPTS/proof.sh" <n> <pr-worktree> <scenario.mjs> [options]` (`<n>` is a folder name; use the issue id before the PR exists). Add `--record` only when a claim needs motion to prove it: focus moving, keyboard flow, a transition, or several clicks in a row. Otherwise no video. Brings main then the PR up, runs both sides in parallel, composes, lists output files. Stacks stay up: repeat after each fix. App: first `prepare` ~4 min, then ~1 min. Storybook: ~2.5 min cold, then ~10 s.
4. **Figma**, for every visual claim: reviewers judge "matches the design" against Figma, not against main. Find the frame in the Linear issue (the PR body names it), its parent and project resources, and, for design system components, `<side>/<state>.figma.txt` from a `--storybook` run. Follow `references/figma.md`, then run step 3 again so the panels compose in. If no frame turns up, the row says "Not checked (no Figma frame in <where you looked>)".
5. **Look at every output**: each `<state>.png`, `<side>/<state>.aria.txt`, `.axe.json`, `pixel-diff.txt`, and each `.webm` when recorded. Both halves must show the same screen, data and state. Measure size, gap and colour claims in the browser (Scenario says how) rather than reading them off a picture. Paired "picked" images have exposed real bugs.
6. **Write `body.md`.** Start with an **Affected pages** list: every route that renders a changed component, found by following its importers up to `apps/web/src/routes/`. A design system primitive with dozens of importers is proven in Storybook plus the 2-3 screens that use it most; list the rest as one "Not proven (N more importers)" line. Each line gives a path a reviewer can open on any database, with no seed ids (`/admin-hospital-configuration?tab=staffing-groups`, `/manager-unit-settings/self-scheduling-constraints`), then the clicks that reach the changed control. Before writing the rest, check each Affected pages line has a state, or a "Not proven" reason from the seed or the shared main database. A line that only says "not run" means go back to step 2 and add its state. Then evidence per row: `![<claim>](./<state>.png)`, aria text, axe counts, the `pixel-diff.txt` line, or "Not checked (why)". When more than one page is affected, start each row's claim with its route. With `--record`, each recording is a bare `./main/main.webm` / `./pr/pr.webm` line under its "Recording" heading.
7. **Attach** (PR must exist). `bash "$SCRIPTS/attach.sh" <pr> <folder>` uploads every `./...png`/`./...webm` in `body.md`, moves video links under their headings, checks video order by size, prints the PR URL. Exit 6: size mismatch; fix first.
8. **Stop stacks at the end only:** `--down` on the last `proof.sh`, or `bash "$SCRIPTS/release.sh" <n>` plus `bash "$SCRIPTS/stack.sh" down <pr-worktree>`. Only stops what this skill started.

`proof-main` is shared by all sessions. Each `proof.sh` run holds a lease on it (`<n>`, renewed per call, expires after 2 h, `UI_BEFORE_AFTER_LEASE_TTL_S`). With another live lease, main is not refreshed, reseeded or stopped, so the `main @sha` label may lag `origin/main`. The last lease released stops main. Leases live in `UI_BEFORE_AFTER_STATE_DIR`; two copies of this skill on one machine must share it, or one copy can stop main under the other.

## Options

| Option        | Use for                                                                 | Output                                                                 |
| ------------- | ----------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| `--storybook` | Component PRs: runs each worktree's Storybook, no sign-in               | `<side>/<state>.figma.txt`: the story's Figma link                     |
| `--axe`       | Accessibility rows                                                      | `<side>/<state>.axe.json`: violations in the crop                      |
| `--pixel`     | "Unchanged" rows, mainly Storybook (seed data makes app screens differ) | `pixel-diff.txt`: `identical`, `N px differ`, size or side differences |
| `--down`      | The last run                                                            | stops stacks                                                           |

`--axe` and `--pixel` cover every state; quote only the lines for the rows they prove. A violation on both sides already exists on main; say so, don't count it against the PR.

## Scenario

Default export `{ route, ready?, flags?, impersonate?, states }`.

- `route`: go through redirects (`/manager-schedule`); seed ids and names differ per database. Storybook: `/iframe.html?id=components-select--md-multi&viewMode=story`; a docs page: `/iframe.html?id=foundations-iconography--docs&viewMode=docs`.
- `ready`: `{ testId }` (preferred) or `{ text }`. Storybook waits for the story or docs page by itself. First load compiles chunks; crops wait 30 s.
- `flags`: PostHog flags forced on both builds; others off. App only.
- `impersonate`: a seed email prefix (`general-staff-feature-user`, `manager`, `central-staffer`, `charge-rn`, `pca`). The admin switches to that seeded profile before the states, for staff, manager and central staffer screens. Exit 1 when the profile or the impersonation banner is missing. The seeded profile may have nothing to show on that screen (no shifts this week): pick another prefix or page, or "Not proven".
- `states`: `{ name, claim, route?, ready?, act?(page, { sideDir }), crop(page) -> Locator, aria? }`. `route` opens another page or story first, then waits for the state's `ready`, or the scenario's `ready` when it has none. In the app, give every state with a `route` its own `ready` unless the scenario's target is on that page too; Storybook needs none. A story missing on one build is recorded as missing there. Crop the smallest element showing the claim; for menus, the whole open menu; in Storybook, the control, not the full-width root. `aria: true` for non-visual claims. Keyboard claims: press keys in `act` and run with `--record`.
- Swaps: every locator matches both builds, `old.or(new).first()`. DS components often lack test ids. If the PR keeps the old test id on the new wrapper, `.or()` matches two elements and a click without `.first()` fails silently.
- Pickers moving to the DS Select: copy `examples/select-swap.mjs`. The DS Menu's popover and search have no test ids; reach them from an option (`getByRole("menuitem")`, then its `role=dialog` ancestor). Skip disabled options when clicking the first one, or the click waits out its timeout.
- **Same starting data on both sides.** `proof-main`'s database is shared and is not reseeded while other leases are live, so earlier runs may have saved rows there. Make `act` reach the same state on both builds and make it safe to repeat (add a row only if it is missing; open a panel only if it is closed, since open/closed state persists). Forms that autosave write to that shared database: do edits on the PR side only (`basename(sideDir)` in `act` is `main` or `pr`), or mark the claim "Not proven".
- **Measured claims** (size, gap, colour): in `act`, read `getBoundingClientRect()` and `getComputedStyle()` with `page.evaluate` and write them to `join(sideDir, "measure", "<state>.json")`; quote main -> PR values in the row. Each run clears `<side>/`, so write measurements from `act`, never copy them in by hand. A gap in a `justify-between` row only shows with a long value: set `width: fit-content` on the control in `act` (narrowing the root does not help).
- **Crops.** In Storybook, many roots are full width: set the control's width in `act` so the crop is readable. Scroll a trigger to the middle of the screen before opening its popover, or a popover near the bottom is cut off.
- The runner closes the feature-update dialog and local-DB banner and disables animations.

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes.
- `readonly`: make no branch, commit, push, PR, PR body edit or PR comment, and change no remote ref. Create no worktree: use a PR worktree that already exists, and run `proof.sh` only when the `proof-main` worktree already exists (`main-worktree.sh` adds it with `git worktree add` on the first run). Do steps 2 to 6 and the local runs as described, skip step 7 (`attach.sh` edits the PR body with `gh pr edit`), and print each git or gh command you would have run, `attach.sh` included, under a `Would run:` heading.

## Exit codes

- `stack.sh`: 1 slot 0 or prepare failed; 2 AWS SSO expired (**Ask the user** to run `aws sso login --profile vitalize-health`; stop); 3 port held by something else (report, don't touch); 4 not ready in 300 s; 5 another start never finished.
- `proof.sh` passes those through; 1 = dirty PR worktree, or a run failed ("auto sign-in did not happen", impersonation, or `ready-timeout.png`: fix scenario or stack, not the picture); 64 = unknown option.

## A state is "missing"

Line 2 of `<side>/<state>.missing.txt` names the failed step (`route`, `act` or `crop`) and its error; `<side>/missing/<state>.png` shows the page at that moment. Find why before re-running. Re-run with `--record` only if those two don't explain it.

- Strict-mode double match: add `.first()`.
- Late modal over the page.
- Ready target too early on a cold start: use a test id.
- `route: Timeout` on a state: the scenario's `ready` is not on that page; give the state its own `ready`.
- Locator matches one build only: add the other half with `.or()`.
- Story only exists on one build: expected; "not on this build" panel.

Stacks with different seed days: the older is reseeded on bring-up. Stacks left up from another day: `stack.sh down` both first.

## Tests

From this directory: `bash scripts/{stack,main-worktree,proof,attach,lease,release,figma-frame}.test.sh` (CI runs these through `scripts/ci/bash-tests.sh`), and `node --test "scripts/lib/*.test.mjs" "examples/*.test.mjs"`, which loads `sharp` from `$WEB_APP_ROOT`, so it runs only beside a web-app checkout.

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/ui-before-after-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`ui-before-after`), `artifacts` (absolute paths: the proof folder `$OUT/pr-<n>/`, its `scenario.mjs` and `body.md`), `summary` (one line),
plus `pr` (the PR number or issue id `<n>`); `folder` (absolute path of `$OUT/pr-<n>/`); `not_proven` (count of "Not proven" rows in `body.md`, `null` when it was not written); `attached` (`true` only when `attach.sh` exited 0, else `false`).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
