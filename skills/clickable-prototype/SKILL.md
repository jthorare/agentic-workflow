---
name: clickable-prototype
description: "Use when a designer or PM wants to click through 2-3 options of a screen inside the real running app, built from its real components and seeded data, switchable from a small dev-only pill. Local only: the prototype branch is never pushed. Not for HTML drafts outside the app (design-mockup-web), proof that a real change works (verify-web, ui-evidence), or code that will merge."
argument-hint: "<feature-slug> [--route <path>]"
allowed-tools: Bash(git *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(SHARED_DIR=*), Bash(source *), Bash(node *), Bash(bun *), Read, Write, Edit, Glob, Grep, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

---

# Clickable Prototype: live design options in the real app

Build 2-3 options of one screen **inside the project's own app**, running on the user's machine with seeded data, and hand back a local URL, a login and how to flip between the options. The options are made from the app's real components and tokens, so what the user clicks is what the product would feel like.

Five phases, each ending at a gate. Do not start a phase before the previous gate passes. At each gate **Ask the user** (approve / change X / stop), unless they said to run autonomously.

What you hand back: a `localhost` URL, a login, a small floating switcher with every option reachable, and screenshots you opened and compared yourself. No published link, no PR preview, no screenshot-only click-through.

Output lives in `$AW_DIR/prototypes/<slug>/` (brief, probe, shots). Templates are in `references/`:

- `references/switcher-template.md`: the dev-only switcher and the mount pattern (React/TypeScript worked example, notes for other frameworks)
- `references/probe-template.md`: the Playwright probe that selects each option, screenshots it and counts page errors
- `references/gotchas.md`: what went wrong on the first real run

## Hard rules

1. **Never push the prototype branch and never open a PR for it.** The branch is `prototype/<slug>`, local only. Phase 2 points its push remote at a name that does not exist, so a plain `git push` fails; that stops accidents, not a deliberate push to a real remote, so the rule is on you.
2. **Gate everything on the dev build.** The switcher, the option bodies and every mount edit must render exactly the real UI when the dev flag is off (`import.meta.env.DEV` in Vite, `process.env.NODE_ENV !== "production"` elsewhere).
3. **No data-path changes.** No edits to the API, shared packages, queries, schema or migrations, and no flags the server reads. Options are display only. Fake the data an option needs as constants inside the prototype file, never in the database or a request.
4. **One prototype file per feature plus minimal mount edits.** All option bodies live together in one `<Feature>Prototype` file next to the real screen. Edits to real files are a few lines: an import, a swap, one switcher mount. Every option, including the real screen as `current`, is reachable from the switcher.
5. **Build from the app's real components and tokens.** Use its component library, icon set and token classes or variables. No hand-picked hex, no new primitives, no look-alike markup.
6. **Never touch a shared or non-local database or service.** Run only the stack the project documents for local seeded use. If a command points at a shared database, stop.
7. **Never work in the user's main checkout.** Everything runs from the prototype worktree.

## Design rules

- **Match the real screen first.** Before drawing any option, screenshot the real screen and compare it with the prototype's `current` option. If they differ, the prototype is not built from the real thing yet.
- **Low on-screen noise.** Number chips and badges on every row get rejected. Prefer one clear affordance per idea, shown where the user already looks.
- **Motion over static badges for anything new.** A new or changed affordance announces itself with a short animation (shimmer, slide, settle), not a permanent badge.
- **No recovery UX for states the design makes impossible.** If an option's rules prevent a state, do not build an error or undo path for it.
- **Name options by what they do** ("Inline in the cell", "Side panel"). The switcher shows the letter and the name.
- **2-3 options.** More than 3 is a different exercise (`design-shotgun`, with mockups).

## Phase 1: Brief

Goal: write down what will be built so the user can correct it cheaply. Ask only for what you cannot read from the repo.

Write `$AW_DIR/prototypes/<slug>/brief.md`:

- **Slug**: kebab-case, e.g. `schedule-header-actions`.
- **Screen and route**: the real route or page file and the URL shape (read the router, and the nearest `AGENTS.md` / `CONTEXT.md`). Which view or tab.
- **Persona and login**: which seeded account the screen needs.
- **The 2-3 options in words**: for each, what changes on screen, which real component is swapped or wrapped, and the one interaction to click through.
- **Seed needs**: what the project's seed provides, and anything an option needs that it lacks. Prefer constants inside the prototype over changing the seed when the data is only decoration.
- **Flags**: client-side flags the screen needs (set from the probe).

**Gate 1:** the user approves the brief.

## Phase 2: Stack

Find how this project runs locally with seeded data, in this order:

1. The project's own run/dev skill, if it has one (check the skill list, `.agents/skills/`, `.claude/skills/`).
2. `AGENTS.md`, `CLAUDE.md`, `README`, `package.json` scripts, `docs/` (the same places `verify-web` looks for the app URL and port).
3. If neither gives a local stack with seeded data and a login, **Ask the user** for: the start command, the seed command, the login, the route.

Record what you found in `brief.md` under **Stack**: start command, seed command, URL, login, where credentials live. Never invent a login or a port.

Create the worktree and keep it from being pushed (see Git mode):

```bash
MAIN=$(git rev-parse --show-toplevel)           # the checkout you were started in; never edit it
SLUG=<slug>
WT="$(dirname "$MAIN")/$(basename "$MAIN")-proto-$SLUG"
BASE=$(git -C "$MAIN" symbolic-ref --short refs/remotes/origin/HEAD 2>/dev/null || echo origin/main)
git -C "$MAIN" fetch origin
git -C "$MAIN" worktree add -b prototype/$SLUG "$WT" "$BASE"
git -C "$WT" config branch.prototype/$SLUG.pushRemote no-push-prototype   # a push now fails by design
```

Then, from the worktree, install dependencies only if the project needs them (its own install, never another checkout's `node_modules`), start the stack and seed it with the commands you recorded.

Rules for this phase:

- Run installs, the stack and type-checks one at a time. Heavy jobs in parallel crash the machine.
- If a port is already in use, a stack is already running (maybe another worktree's). Ask before stopping it.
- Re-seeding can change every id and URL. Re-run the probe after every seed.
- After the stack is up and again after the first page load, run `git -C "$WT" status --short`. Dev servers sometimes rewrite a tracked generated file; `git checkout -- <file>` it before you diff.

**Gate 2:** open the route in the probe (`current` only) and confirm the real screen renders with data. Show the user the screenshot.

## Phase 3: Build

1. Copy the switcher from `references/switcher-template.md` into the prototype file's folder, set the storage key and the option list. It is a copy inside the worktree, never a component added to the app on the main branch.
2. Write the option bodies in one `<Feature>Prototype` file next to the screen. Read the real component you replace and reuse its markup and tokens. Fake data is a set of typed constants at the top of that file.
3. Mount with minimal edits: read the selected option at the mount point; when it is `current` or `null` (not a dev build) return the real element untouched. Mount the switcher once, at the screen root.
4. Make the options feel alive: the one interaction in the brief must work (open, expand, dismiss, accept), with a short animation where something appears.
5. Keep dev-only chrome out of the way: a small fixed pill at a corner, not over primary controls.
6. Check scope: `git -C "$WT" add -N . && git -C "$WT" diff HEAD --stat` lists only the prototype files and the mount edits (`-N` makes new files show up). Anything in the API, shared packages or schema breaks hard rule 3; revert it. Do not commit or push.
7. Lint and format only your changed files with the project's own tools. Do not run the whole-repo type-check: in a fresh worktree it is often unreliable (unbuilt project references) and heavy, and a prototype that is never merged does not need it. The dev server compiling plus the probe's zero page errors is the runtime check.
8. Check nothing is clipped: look at where your new element lands in the screenshot at the probe's viewport, not just that it exists.

**Gate 3:** tell the user which options exist and where they mount. Proceed to proof unless they want changes.

## Phase 4: Prove

Use `references/probe-template.md`. Copy it to `$AW_DIR/prototypes/<slug>/probe.mjs`, set the route, login, ready selector, options and each option's click-through, and run it.

The probe, per option: select it through the switcher, run its click-through, assert the new thing is visible, wait for animations, screenshot, and record page errors. It screenshots `current` first as the baseline.

Done when:

- Every option in the brief appears in the switcher and the probe selected each one.
- 0 page errors across the whole run.
- **You opened every screenshot yourself**, compared the `current` shot with the real screen, and each option's shot with its description in the brief. Say what you saw, not that the probe passed. Reject stale, wrong-option or visibly broken shots and fix the cause.
- Text-absent assertions are never the only proof: assert the new thing is visible first (see `references/gotchas.md`).

If the probe cannot reach a state (a page that locks headless browsers, a server-set flag), say so in the hand-back and do not claim it.

**Gate 4:** the user approves the hand-back, or asks for changes (loop to Phase 3 and re-run the probe).

## Phase 5: Hand back

Give the user, in this order:

1. **URL**: the seeded route on the local app (`localhost` unless the project's CORS says otherwise).
2. **Login**: the account from Phase 1 and where its credentials are. Say it is a local-only account.
3. **How to flip**: the switcher pill; name each option and the one thing to click in it. The choice is remembered in browser storage for that browser.
4. **Screenshots**: paths under `$AW_DIR/prototypes/<slug>/shots/` (a reference, not a deliverable).
5. **How to stop and discard**: the stack's stop command from Phase 2, then `git -C "$MAIN" worktree remove --force "$WT" && git -C "$MAIN" branch -D prototype/$SLUG`.
6. State plainly: nothing was pushed, no PR exists, the prototype is local code.

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes.
- `readonly`: work in the current directory. Make no branch, worktree, commit, push, PR, PR comment or review, and change no remote ref. Do the file edits and local checks the skill describes (in the current checkout, and say so), and print each git or gh command you would have run under a `Would run:` heading.

## Related skills

- `design-mockup-web`: HTML drafts outside the app, approved and saved as baselines. Use it when the idea does not need the real app yet.
- `design-shotgun`: 4-6 look variants as mockups on one contact sheet.
- `verify-web`: prove a real change works in a live browser. Not for prototypes.
- `ui-evidence`: screenshots and checks for a PR. A prototype is never a PR.

## Rules

- Prototype code is never pushed, merged or opened as a PR.
- Never claim a state the probe did not reach.
- Open and compare every screenshot yourself before saying it passed; never ask the user to verify for you.
- Stop and ask when the project has no documented local seeded stack.

## Next steps

- `/design-mockup-web <screen>`: turn the picked option into an approved HTML mockup and baselines
- `/design-implement <screen>`: build the picked option as real code on a normal branch, after the user picks
- `/verify-web`: prove the real change once it exists

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/clickable-prototype-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`clickable-prototype`), `artifacts` (absolute paths: `$AW_DIR/prototypes/<slug>/brief.md`, `probe.mjs`, `probe-result.json` and the screenshots), `summary` (one line),
plus `slug`; `options` (option ids in the switcher); `url` (the local URL handed back); `page_errors` (count from the probe); `pushed` (always `false`).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
