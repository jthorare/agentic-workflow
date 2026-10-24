---
name: ui-evidence
description: Generate Playwright-based UI evidence for a web app PR — plan with qa-runner, run headless with no model, repair broken selectors via judge, lint for sloppiness, and publish (ask-first) to Linear and to the PR as a comment with its screenshots. Local stack only, never dev/prod.
allowed-tools: Bash(ui-evidence:*), Bash(git *), Bash(gh *), Bash(curl *), Bash(command -v *), Agent, Read, Write, Glob, Grep, AskUserQuestion
---

# UI evidence

This skill runs Sindri's `ui-evidence` binary. It is one tool for the skill, a human and the recipe's evidence agent steps; nothing here drives a browser by any other route. Preflight, once per session:

```bash
command -v ui-evidence >/dev/null || { echo "run ./setup.sh from the agentic-workflow checkout (the evidence tool ships with Sindri)"; exit 1; }
```

If `deployment.md` sits next to this file, read it first: it holds this deployment's conventions (preview hosts, roles, login form, where credentials come from). Deployment-specific values are settings, never code ([Configuration](#configuration)).

Every run directory is `$AW_STATE_DIR/ui-evidence/runs/<id>` (pick a fresh `<id>`, for example the PR number and a timestamp). The verbs take the stack lock and the heavy-job lock themselves (`--lock-timeout <seconds>`, default 120), so never wrap one in a lock of your own.

Exit codes, the same for every verb: 0 every check passed, 1 bad usage, invalid input or a guard refused, 2 a check failed (the document is still written and printed), 3 blocked on review, 4 the run itself errored, 75 a lock stayed busy, 130 interrupted. Every verb prints one JSON document on stdout and one-line `ui-evidence: ` messages on stderr.

1. Doctor the stack: `ui-evidence doctor`. Refuse to proceed if unhealthy or foreign (RF-1). In a git worktree that has its own stack slot, use that slot's stack, not `:3000` ([Worktree stacks](#worktree-stacks)). If the browser is missing, run `ui-evidence install-browser` once.
2. **Spawn a subagent** — the `qa-runner` custom agent (installed per provider) — with the PR diff and the app's route map (if the repo documents one) to write a script (the shape in [Step actions and login](#step-actions-and-login-for-the-plain-runner), [Step targets](#step-targets) and [Step checks](#step-checks-and-selector-repair)). For an API or database change it writes an `api-run` or `db-run` script instead ([API and database evidence](#api-and-database-evidence)).
3. `ui-evidence run <script.json> <run-dir> [--baseline main.png] [--app-build <sha>] [--fixtures <id>] [--cache <verdict-cache.json>]` (exit 0 all steps passed, 2 a step failed or broke, 1 bad usage or an invalid script; the summary JSON is printed and written to `<run-dir>/summary.json`, including `appBuild` — pass `--app-build $(git rev-parse HEAD)` so the run is tied to a commit; reuse one `--cache` file across runs of the same build so unchanged verdicts hit the cache).
4. Check DB provenance with `ui-evidence provenance` (it prints `{provenance: seeded|unknown, reason}`) before deciding whether Linear upload is even offered.
5. Visual verdict, cheapest first (inside `run`): (a) deterministic pixel compare of the last passed screenshot against the approved main baseline — identical pixels and all steps passed ⇒ `unchanged`, **no model call**; (b) verdict cache keyed on the actual after+baseline image hashes plus prompt/model version, app build, script, fixtures, viewport and browser version — a hit reuses the prior critique and is recorded as a cache hit; (c) otherwise one `judge visual-critique` call on the cropped changed region (image-capable CLI only — Jev is text-only and never sees images). `summary.json`'s `visual` is an object (the verdict and how it was reached). Any failure, timeout, or out-of-enum result leaves the verdict `unchecked` — never a default `looks-right`. A run with a failed/broken step always gets a critique. The browser run and every step check always execute; a cached or `unchanged` visual never stands in for a behavior check.
6. Put the evidence on the PR ([Publishing to the PR](#publishing-to-the-pr)): every write asks first, per this repo's policy. Screenshots and the diff overlay go up only when DB provenance is `seeded`; otherwise the comment carries text and local paths and the images stay on this machine.

## Publishing to the PR

Every run that produced evidence for a PR ends with a comment on that PR. Use the `gh` CLI (`mcp: github/*` works too if `gh` is unavailable); always pass `--repo <owner/name>` explicitly, never rely on the cwd or `GH_REPO`.

1. **Review first:** open every screenshot and the diff overlay (the Read tool shows PNGs) and reject bad captures (mid-animation, focus rings, see-through popovers) before anything leaves the machine.
2. **Ask the user** to approve the upload, naming the PR and the provenance.
3. Push the images: `ui-evidence gh-publish <run-dir> --repo <owner/name> --pr <n> --provenance seeded --reviewed <file>,<file>` (`<run-dir>/parity.json` for a parity run, else `summary.json`; a parity run carries its own provenance, so leave out `--provenance`). It commits the PNGs through `gh api` (the git data API: no checkout, your working tree is untouched) to the PR's evidence branch `ui-evidence/pr-<n>`, under `pr-<n>/<run-id>/`, on top of earlier runs and never force-pushed, and writes `<run-dir>/pr-comment.md` with each image embedded by a commit-pinned `https://github.com/<repo>/blob/<sha>/…?raw=true` URL, which renders for anyone who can read the repo. Exit 0 pushed (stdout: `branch`, `commit`, `urls`, `commentFile`, `post`), 1 bad usage or input, or provenance not `seeded` (nothing pushed), 3 an image isn't in `--reviewed` (nothing pushed; stdout lists `unreviewed`), 4 `gh` failed.
4. Post it: run the `post` argv it printed, `gh pr comment <n> --repo <owner/name> --body-file <run-dir>/pr-comment.md`. For a run that isn't `seeded`, post the text-only comment instead: `ui-evidence publish-plan <run-dir> --provenance unknown` prints the plan with its text-only comment (for a parity run, `ui-evidence parity-comment <run-dir> --for github`), which you write to a file and post the same way.

The evidence branch is never merged and holds only evidence; delete it once the PR closes (`gh api -X DELETE repos/<owner/name>/git/refs/heads/ui-evidence/pr-<n>`, after asking). Deleting the branch doesn't erase pushed images from GitHub right away, which is why only `seeded` data is ever pushed.

## Worktree stacks

The main checkout's stack owns `localhost:3000`. Evidence must come from the code under review, so when you work in a git worktree, never drive `:3000` (the doctor passes there, but that is the main checkout's code). Give the worktree its own stack slot instead. How a slot is claimed, prepared and started is the app's own convention (`deployment.md` names it); the toolkit relies only on this contract, set up through the slot settings ([Configuration](#configuration)):

1. The worktree's gitignored slot file (`UI_EVIDENCE_SLOT_FILE`, in the worktree root) holds `<UI_EVIDENCE_SLOT_KEY>=N`, N 1 to `UI_EVIDENCE_SLOT_COUNT`.
2. Slot N serves the web app on `localhost:3000 + STEP * N` and the API on `UI_EVIDENCE_API_PORT + STEP * N`, where STEP is `UI_EVIDENCE_SLOT_PORT_STEP`; its database is `UI_EVIDENCE_SLOT_DATABASE` with `{slot}` replaced by N, in `UI_EVIDENCE_DB_CONTAINER`.
3. Run `ui-evidence doctor` **from the worktree** (or set `UI_EVIDENCE_WORKTREE=<path>`). It reads the slot file, probes the slot's ports and database, refuses a slot whose ports are served from another checkout, and prints the `--host` and database to use.
4. Run the script (`run`, `parity`, `api-run`) with that `--host` (with a step of 10, slot 2 is `--host http://localhost:3020`). A slot host is a **local** run: no `--allow-preview-host`, no write guard, the same as `:3000`. Only a configured slot's web port on `http://localhost` is accepted, as a bare origin; `127.0.0.1` is refused because dev CORS allowlists name `localhost`. With no slots configured, `localhost:3000` is the only local host.
5. Check provenance against the slot's database, not the main one, and sign in with the slot's own seeded logins.

## Host override (guard rails)

`localhost:3000` stays the default, and `--host http://localhost:<slot web port>` targets a configured worktree slot ([Worktree stacks](#worktree-stacks)). `--host https://<preview-host> --allow-preview-host` runs against a PR living preview and nothing else: the hostname must fully match `UI_EVIDENCE_PREVIEW_HOST_PATTERN` (a regex such as `pr-[0-9]+\.preview\.example\.com`; write it so dev, staging and prod hosts never match), over https with no port, path or credentials. With no pattern configured every non-local host is refused. `goto` targets must be paths on that host (one leading `/`; `@evil.com/x`, `//evil.com` and backslashes are refused), and the parity browser aborts any navigation to another origin. A host override is read-only on a best-effort basis: click steps (and `goto` paths) whose words read like save/submit/update/delete/create/confirm/publish/send/apply/approve/post/add/invite/archive/cancel/reset/assign/import/upload/enable/disable/complete/sync/logout and similar — including `saveBtn`/`delete-button` forms — are refused unless `--allow-writes`, and so is every drag and every `press` of a non-navigation key (`Enter`, `Space`). The check is a keyword heuristic on test ids: on the accessible name or text of `role:`/`text:` targets (so `role:button|Save` is refused without `--allow-writes`): it cannot see what an opaque id does, and a `fill` into an autosaving input is not covered, so only point it at pages whose controls you know. Selector repair can click a model-chosen element; keep host-override scripts to stable test ids or explicit `role:`/`text:` targets (which are never repaired). Credentials come only from environment variables (e.g. run under your secrets manager's `run --` wrapper); they are never printed, and error text is scrubbed of them. A preview is a scrubbed customer extract: `parity` records provenance `scrubbed` (an explicit `--db-provenance unknown` stays `unknown`). A non-local host is recorded in `summary.json` (`host`), and `gh-publish` and `publish-plan` treat any non-local host as `unknown` whatever the caller passes, so those runs stay local; use `parity` for scrubbed-data uploads to Linear. An agent session launched by a Sindri recipe never passes `--allow-writes` or a non-loopback `--host`: those are a person's call.

### Step actions and login for the plain runner

Script steps (`run`, and the same step shape inside a parity frame) are `goto`, `click`, `fill`, `expect-visible`, `scroll`, `drag` and `press` ([Step checks](#step-checks-and-selector-repair) has `drag`, `press` and the script-level `localStorage`); each asserts its `expectedState` afterwards and takes a per-step screenshot. `scroll` moves a scroll container (`target` is its `data-testid`) so a scrolled state can be captured: `value` is `end` (default; `scrollLeft = scrollWidth`), `start` (`scrollLeft = 0`), `bottom` / `top` (vertical) or a non-negative integer horizontal pixel offset; anything else fails the schema. It waits for layout to settle, is read-only (the write guard never flags it), and a missing target is `broken`; like `expect-visible` it is never selector-repaired.

A step may carry an optional `index` (non-negative integer; `click`, `fill` and `scroll` only, rejected at parse time on `goto` and `expect-visible`) to pick the nth match (0-based) of a repeated `data-testid`, e.g. `{"action":"click","target":"timeline-window-row-open-list","index":1,...}`. Without `index` the locator stays strict: several matches fail the action and go to selector repair as before; with `index` a broken target is repaired as before. `index` is part of the step, so it is in the scenario hash.

An optional script-level `login` signs in before the first step of each viewport, on previews that need a session: `{"emailEnv": "ADMIN_EMAIL", "passwordEnv": "ADMIN_PASSWORD"}` plus optional `emailSelector`, `passwordSelector`, `nextSelector`, `submitSelector` (defaults fit a one-step email + password form: `input[type="email"]`, `input[type="password"]`, `button[type="submit"]`; set `nextSelector` for a two-step form) and `loginPath` (default `/login`). It is strict like the parity login: env var names must be `UPPER_SNAKE`, unknown keys fail the parse. Credentials come only from the environment; a missing variable stops the run before a browser starts, values never reach `summary.json` or logs, and error text is scrubbed of them. Only the env var names and selectors enter the scenario hash. Without `login` nothing changes.


## Design-parity mode (Figma pixel diff)

Compares the implementation with a design export instead of with a main-branch screenshot. Existing usage is unchanged. Run it with `ui-evidence parity <manifest.json> <run-dir>`:

```bash
ui-evidence parity <manifest.json> <run-dir> \
  [--host https://<preview-host> --allow-preview-host] [--allow-writes] [--app-build $(git rev-parse HEAD)] [--db-provenance seeded|unknown]
```

Exit 0 all geometry checks passed, 1 bad usage/manifest/guard, 2 a geometry check failed, 4 the run itself errored (anchor missing, step failed, region outside the viewport), 75 a lock stayed busy. `<run-dir>/parity.json` is the summary; it is deleted at the start of every run, so a failed run never leaves an earlier summary for `parity-plan` to publish.

1. **Design PNGs at 1x.** Get each frame at its native size with the Figma MCP `get_screenshot` (frame node, scale 1) and save it under the run, or accept any PNG export. The default viewport is the Figma frame (1512x982, DPR 1) — set `viewport` to match your frame.
2. **Manifest** (worked example in `examples/design-parity/example-manifest.json`): `route`, `ready` (an element that must be visible before anything is asserted — a text-absent check straight after `goto` passes vacuously on the loading spinner), optional `login` (env var names + CSS selectors only), `settleMs`, `threshold` (default 0.1), `includeAA` (default false), and `frames[]`. Every object is strict — a misspelt key (`expectBox`) fails the parse instead of dropping an assertion. `viewport` must contain every crop (`anchor origin + designRegion` size); otherwise the run stops with the viewport size it needs (raise `viewport.h`). `timeoutMs` (default 60s) bounds `ready`, `waitFor` and the anchor lookup. `neutralClick` (default: 22px in from the viewport corner, or `false`) is skipped when that spot holds a control. `login` also takes `nextSelector` (two-step forms) and `loginPath`. The write guard applies to `parity` steps on localhost too. Each frame has `designPng`, `designRegion {x,y,w,h}`, an `anchor` (`{testId}` or `{text}`) whose bbox origin locates the implementation crop (plus optional `anchorOffset`), optional `steps` (same shape as script steps, run in order on the same page so states accumulate), `formValues` (typed to match the design's sample data, never saved), `expectBoxes`, `knownDifferences` and `designNode` (this frame's Figma node; it wins over the manifest-level one in the Linear subtitle).
3. **Output per frame:** `<name>-design.png`, `-implementation.png`, `-diff.png`, `-side-by-side.png` (design | implementation | diff, 12px gaps), `-full.png`, and a `parity.json` entry: `diffPercent`, `diffPixels`, `total`, anchor bbox, `sizeDelta` vs the design region, `boxChecks`, `knownDifferences`.
4. **Geometry assertions:** `expectBoxes: [{target, relativeTo?, expected:{w,h,x,y}, tolerance?}]` (tolerance default 0; x/y are relative to `relativeTo` when given). A mismatch, or an element that cannot be found, fails the run (exit 2) and is printed.
5. **Known differences:** list intentional differences (copy, data) per frame. They are carried into the report, the Linear subtitle and the PR text, so a non-zero diff is explained, never hidden. Do not list a difference you have not looked at.
6. **Capture hygiene** is built in: `document.fonts.ready`, `settleMs` before each screenshot (a popover mid fade-in gives a see-through shot), blur of the active element plus a click on a neutral spot (a Radix select restores focus after closing and leaves a blue ring), scroll containers reset to the top, and `formValues` applied before capture. Raise `settleMs` for slow animations.

### Publishing parity evidence (images to Linear and the PR)

- **Policy:** Linear upload is allowed for `seeded` and `scrubbed` provenance after an explicit ask; `unknown` never leaves the machine. GitHub gets the side-by-side images only for `seeded` data, through `gh-publish` ([Publishing to the PR](#publishing-to-the-pr)); a `scrubbed` run's PR comment stays text-only (images on Linear). Every write still asks first.
- **Review gate:** before any upload, open every image in the plan (the Read tool shows PNGs) and compare it with its `expectation`. Reject mid-animation shots, focus rings and see-through popovers and re-capture. `parity-plan` stays blocked (exit 3) until the files are listed: `ui-evidence parity-plan <run-dir> --reviewed <file>,<file>`. The list is self-attested — nothing enforces that the images were really opened, so do it.
- **Verbs:** `ui-evidence parity-plan <run-dir> --reviewed f,f` prints the attachment plan; `ui-evidence parity-put --upload <upload.json> --file <png>` prints the argv of the signed PUT from the `{url, headers}` Linear returned; `ui-evidence parity-stale --attachments <attachments.json> --keep <id>,<id>` prints the stale `Pixel diff: ` attachments; `ui-evidence parity-comment <run-dir> --for linear|github` prints the comment as markdown.
- **Per image, one at a time** (the signed URL lives 60s): `mcp: linear/prepare_attachment_upload` (issue, filename, contentType `image/png`, exact size) → PUT the bytes with every returned header verbatim (`parity-put` gives the argv; `curl --data-binary`) → `mcp: linear/create_attachment_from_upload` with `assetUrl`, the plan's `title` (`Pixel diff: <state> (Figma | implementation | diff)`) and `subtitle` (design node, build, viewport, diff %, known differences). Never pass `--app-build` a commit you did not verify; an unverified build prints as "unverified".
- **Replacing stale evidence:** list the issue's attachments (`get_issue`) before attaching, attach the new ones first, then show the user the IDs of earlier attachments whose title starts with `Pixel diff: ` (`parity-stale`) and delete them (`mcp: linear/delete_attachment`) only after the user approves those IDs.
- **Optional summary:** one `mcp: linear/save_comment` from `ui-evidence parity-comment <run-dir> --for linear` (regions, percentages, geometry, known differences).

## Step targets

`click` and `fill` targets (in script steps and parity `steps`) are one of:

- a bare test id (`save-renamed`), resolved with `getByTestId`; unchanged;
- `role:<role>|<accessible name>` (`role:button|Save`, `role:option|Unit 4200`), resolved with `getByRole(role, { name, exact: true })`;
- `text:<text>` (`text:Unit 4200`), resolved with `getByText(text, { exact: true })`.

Use the prefixed forms for controls that have no test id (top-bar switcher, sidebar tabs, Radix Select, combobox options). An optional trailing `#<n>` (1-based, prefixed targets only, e.g. `role:option|07:00-19:30 Day#1`) picks the nth match for genuine duplicates; it fails clearly when fewer than n exist, and the write guard reads the name without it. The suffix must touch the name (`Day#2`); a name with a space before `#` (`Shift #12`) is taken literally, and a name ending `#<digits>` with no space is targeted by adding an index (`Room#4#1`). `text:` counts hidden matches toward `n`, and an out-of-range index costs one step timeout before it fails. Without the suffix a prefixed target must match exactly one element: an ambiguous locator throws (Playwright strict mode) and the step is `broken`; it never clicks the first match. Names and text are exact, case-sensitive and whitespace-normalized; the role is split at the first `|` (the name may contain `|`) and must be lower-case; `text:` also counts hidden matches, so it can be ambiguous where `role:` is not. A malformed or near-miss prefix (`role:button`, `text:`, `Role:button|Save`, a leading space) fails the schema parse. The write guard reads only the name/text, so a toggle with no write word (`role:switch|Notifications`) or a confirm like `role:button|Yes` is not caught; keep such steps for pages you know. `expectedState` kinds still use test ids. Prefixed targets are not sent to selector repair: a miss or ambiguity is `broken`, and the plan should be fixed. `goto` and `expect-visible` targets are unchanged.

## Step checks and selector repair

Every script step carries a machine-checkable `expectedState` (`text-visible`, `text-absent`, `testid-visible`, `testid-absent`, `url-path`, `url-search`, `input-value`) asserted after the action. `url-path` compares the path only; `url-search` (`{"kind":"url-search","param":"timeFilter","value":"Night"}`) waits for one query parameter to equal `value`, for URL-backed filters. `testid-absent` passes when no visible element has that test id; unlike the others it looks once, immediately, with no wait, so a leftover element (a drag ghost) right after the action fails the step. A step whose action works but whose expectation fails is `failed`. A step whose selector is broken (click/fill only) goes to `run`'s selector repair (`judge ui-element-repair` with the actual broken target); the judge's chosen index is validated against the currently visible DOM, the same action is executed on that candidate, and the expected state is re-checked — only then is the step `passed`. Two attempts max, then `broken`.

**Drag.** `{ "action": "drag", "target": "<source testid>", "to": "<destination testid>", "grabOffset"?: {x,y}, "dropOffset"?: {x,y}, "holdAlt"?: true, "expectedState": … }` drags with real mouse events (press, a 10px first move to clear dnd-kit's 6px PointerSensor activation, 12 steps to the drop point, a 150ms hover, release), so pointer-sensor libraries see a genuine drag. Offsets are pixels from each element's top-left (default: center); `holdAlt` holds Alt through the release. Each test id resolves to its first visible match; both elements must be on screen together (the source is scrolled into view). A missing source or destination is `broken` — drags get no selector repair. The step screenshot is taken straight after the check, so it shows the state right after the drop. Every drag counts as a write: a preview host (and `parity`) refuses it without `--allow-writes`.

**Press.** `{ "action": "press", "key": "ArrowRight", "target"?: "<testid | role:… | text:…>", "index"?: n, "expectedState": … }` focuses the target (when given) and presses a Playwright key name (`ArrowRight`, `Home`, `Shift+Tab`, `Enter`); with no target the key goes to whatever already has focus, so a run of presses can walk a tablist. A missing target is `broken`, with no key sent and no selector repair. Navigation keys (arrows, `Home`, `End`, `PageUp`, `PageDown`, `Tab`, `Escape`, each optionally with `Shift+`) are read-only; any other key (`Enter`, `Space`, a letter, `Control+S`) counts as a write, because it can activate a focused Save button whatever the target says, so a preview host refuses it without `--allow-writes`.

**localStorage.** A script-level `"localStorage": { "<key>": "<value>" }` (strings, written verbatim) is set on the host origin by an init script before every page load, so the app sees it on its first render and after each navigation. Use it for feature-flag overrides and similar client toggles; it is part of the verdict-cache scenario key.

## API and database evidence

For a change that has no screen, or whose proof is below it, the same binary checks the API or the database. The planner writes the script; these verbs only read and check it. Targets (named HTTP origins, CLI programs, databases and their credentials by environment-variable name) come from `<repo>/.agentic-workflow/evidence.json`, or from `--config <file>`; nothing app-specific is baked in.

- `ui-evidence api-run <script.json> <run-dir> --at base|head [--repo <dir>] [--app-build <sha>] [--host <loopback origin>]` sends the script's HTTP requests, then its CLI calls, in order, and checks every expectation. `--host` names the origin for requests (`http://localhost:<port>`; `api-run` has no default host and refuses a script with requests without one). `--at` records which side of the change the app you started is on; checking out and starting that side is the caller's job, `api-run` only records it. The script is vetted (host, redirects, CLI targets, environment) before any lock is taken.
- `ui-evidence db-run <script.json> <run-dir> --at base|head [--repo <dir>] [--app-build <sha>]` opens the database read-only (SQLite: a copy of the file in the run directory; Postgres: after the server identity and role checks), runs each query and checks its rows. A role that can write, a non-disposable database and a write statement are refused (exit 1).

Both write `<run-dir>/summary.json` (`evidence.api.summary/1`, `evidence.db.summary/1`) and print it on stdout. Exit 0 every expectation held, 2 one did not (the summary is still written), 1 invalid or refused, 4 the run errored (a request failed, a database could not be reached), 75 a lock stayed busy. Run once with `--at base` and once with `--at head` when the proof is a before and after. A recipe run combines the UI, API and database summaries into one; the files are the contract, so keep each in its own run directory.

## Cost baseline

Each run writes `summary.json` with every model invocation (planning, selector-repair, visual-critique): elapsed time, and token counts that are `null` (unknown) unless the provider reported them. `scorer` renders per-phase and per-PR/route tables and prints `unknown`, never `0`. To link the planner's single call to its run, add `planning: { pr, model, elapsedMs, inputTokens?, outputTokens? }` to the script. See `docs/ui-evidence-cost.md` for the measurement protocol and status of the planner-model pilot.

## Configuration

Deployment-specific values are settings, never code. Each is read from, highest precedence first:

1. the environment;
2. a local, uncommitted env file: the one `UI_EVIDENCE_CONFIG` names, else `.ui-evidence.local.env` next to this SKILL.md (gitignored);
3. `deployment.env` next to this SKILL.md: a deployment's committed defaults. It is absent here; a fork that adapts the toolkit to one app commits its own (with a `deployment.md` for the conventions settings can't express), so syncing from upstream never conflicts.

The files are `NAME=value` lines (`#` comments, optional `export ` and quotes); they are parsed, never sourced. `ui-evidence run`, `parity` and `doctor` read them the same way, and a variable already set in the environment is never overridden. Unset settings fail closed.

- `UI_EVIDENCE_APP_TITLE` — the app's `<title>` text, used by `ui-evidence doctor` to
  confirm `:3000` is this app and not a foreign process. Required; `doctor`
  fails with a clear message if it's unset.
- `UI_EVIDENCE_PREVIEW_HOST_PATTERN` — a regex the whole hostname of a `--host`
  preview must match (see [Host override](#host-override-guard-rails)). Unset: no
  preview host is accepted.
- `UI_EVIDENCE_SLOT_FILE`, `UI_EVIDENCE_SLOT_KEY`, `UI_EVIDENCE_SLOT_COUNT`,
  `UI_EVIDENCE_SLOT_PORT_STEP`, `UI_EVIDENCE_SLOT_DATABASE` — the worktree slot
  convention ([Worktree stacks](#worktree-stacks)): the slot file in a worktree's
  root, the name in it that holds N, the highest slot, the port step per slot, and
  the slot's database name with `{slot}` (optional: unset skips the doctor's slot
  database check). The runner needs only the count and step. Unset: no slots, and
  `localhost:3000` is the only local host.
- `UI_EVIDENCE_DB_CONTAINER` / `UI_EVIDENCE_DB_USER` — the Docker container running the
  local Postgres and its user (default `postgres`). `ui-evidence doctor` runs `pg_isready` there,
  and the provenance query runs `psql` there. Unset: `doctor` skips the database check.
- `UI_EVIDENCE_API_PORT` — a localhost port (an API server) `ui-evidence doctor` must reach. Unset: skipped.
- `UI_EVIDENCE_SEED_TABLE` — the `[schema.]table` with an `email` column that
  `ui-evidence provenance` reads. Unset: provenance is `unknown` (evidence stays local).
- `UI_EVIDENCE_SEED_DOMAINS` — comma-separated list of email domains that
  count as seed/synthetic data for `ui-evidence provenance`. Default: `example.com`.
- `UI_EVIDENCE_SEED_EMAIL_PATTERN` — the SQL `LIKE` pattern used to find a
  seed marker row in the seed table. Default: a generic example.com pattern.
  Unconfigured, both defaults only match fictional example.com addresses, so
  provenance fails closed ("unknown") against any real database.
- `UI_EVIDENCE_USER_AGENT` — the browser's user agent. Default: desktop Chrome at the
  bundled Chromium's version, because headless Chromium's own UA says "HeadlessChrome"
  and apps that gate on browser support (an "unsupported browser" page) block it.
