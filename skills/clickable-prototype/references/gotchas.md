# Gotchas

Skim before Phase 4; search when something looks wrong.

## Stack

- **Re-seeding can change every id.** If the project's seed builds a fresh tenant, the URL, ids and logins change. Re-run the probe after every seed and hand back the new URL.
- **The dev server can dirty a tracked generated file** (a rewritten `.d.ts`, a route tree). Check `git status --short` after the stack comes up and after the first page load, `git checkout -- <file>` it, and never count it as part of the prototype diff.
- **Use the host the app's CORS allows.** Many APIs allow `localhost` but not `127.0.0.1`.
- **A port is already in use.** Another worktree's stack owns it. Look at the project's status command and ask before stopping it. Two worktrees often cannot run stacks at once.
- **Each worktree needs its own dependency install** if the project uses one. Without it the app resolves a stale or missing `node_modules` (and a pre-commit formatter does nothing).
- **Never use a shared database.** If a command prints a shared or remote database host, stop.

## Probe

- **"Unsupported browser" instead of the login page.** Headless Chromium's user agent says `HeadlessChrome` and some apps gate it. The template sets a plain `Chrome/<version>` user agent; keep that line. Symptom: the login step times out and `failure.png` shows the gate.
- **Some pages lock a headless browser.** If a page freezes Playwright, prototype another screen, or describe the limit in the hand-back and give the user the URL to click themselves. Do not claim it was probed.
- **Text-absent checks pass vacuously right after `goto`.** `expect(text).toHaveCount(0)` is true before the page has rendered. Wait for something that proves the screen loaded (`CONFIG.ready`) and that the new thing is visible first.
- **Waiting on the wrong thing.** `waitUntil: "networkidle"` never settles when the app holds a realtime socket. Wait for a specific testid.
- **A shot can show the previous option.** The probe opens a fresh tab per option and clears the stored choice in an init script before the first load; do not reuse one tab across options.
- **Screenshot too early.** Right after switching, a shot can show the previous option's highlight while the DOM is already right. Wait (`settleMs`) before the screenshot.
- **Client flags only.** Flags set through `localStorage` before the app boots work; a flag the server evaluates cannot be set this way.
- **Screenshots lie too.** A shot of the wrong option or a loading skeleton can pass the probe. Open each one and compare it with the brief.

## Prototype code

- **Whole-repo `type-check` is unreliable in a fresh worktree** (unbuilt project references fail before your code is looked at). Lint and format your changed files and rely on the probe.
- **The real header may overflow narrow viewports.** Look at where a new header element lands in the screenshot, not just that it exists.
- **The hook runs in every row.** A `usePrototypeOption()` call inside a cell renders hundreds of times; read it once in a parent.
- **Switching options leaves other UI open.** Reset each option's open/selected state when the selected option changes.
- **`localStorage` is per browser.** Another browser or a private window starts on `current`.
- **Do not mutate seeded data to make an option look right.** That is a data-path change. Fake it with constants in the prototype file.
