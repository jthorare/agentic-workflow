# Setting up Sindri (C#)

This guide takes a new box from nothing to a running Sindri: the control plane up, Onyx connected, agent sessions able
to reach it, and the app open. It follows the setup the team did by hand on a real work repo (a 20k-file TypeScript
monorepo), with that repo's names left out. The same steps, one per screen and with pictures, are in the guided page
[`docs/sindri-setup.html`](../sindri-setup.html); open it from disk in any browser.

Operating Sindri once it is set up (the product map, recipes, running the line) is in [OPERATIONS.md](OPERATIONS.md).

Examples use `~/code/my-app` as the work repo and `my-app` as its name in the profile. Replace both with yours.

## Who runs what

Some commands only a person may run, at a real terminal. Sindri refuses them in an agent session with
`SND-AUTH-002` (see [Troubleshooting](#9-troubleshooting)). Each command below is marked:

- **Human-only**: you type it in your own terminal (Terminal, iTerm, Warp), not inside Claude Code, Codex or Cursor.
- **Agent OK**: read-only or harmless; an agent session may run it for you.

Type `sindri-app`, never `sindri`, until the switch (step 2 says why). Sindri's own hints say `sindri …`: read them as
`sindri-app …`.

## 0. The pieces

```
                         you (terminal)            the Sindri app (sindri-ui)
                               │                              │
                               ▼                              ▼
  ┌──────────────────── control plane: `sindri serve`, 127.0.0.1:7410 ─────────────────────┐
  │  host lease · ledger.db (every event) · views.db · the API and event stream · scheduler │
  │  workflow engine ──► Temporal dev server (pinned, loopback, temporal.db)                 │
  │  context engine  ──► context.db: repo docs, product map, intent records                  │
  │  MCP endpoint /sindri/mcp ◄── agent sessions in your work repo                           │
  └──────────────────────────────────────────────────────────────────────────────────────────┘
        ▲ reads only the approved profile             │ searches
        │                                             ▼
   profile.yaml ── `profile approve` ──►     Onyx (Docker, 127.0.0.1:3000): Linear, Slack
   recipes/*.yaml ── `recipe approve` ──►    the registry (workflow.line and its children)
```

| Piece | What it is | Where it lives |
|---|---|---|
| Control plane | One process per box (`sindri serve`), holding the host lease. Every other command is a client of its API on `127.0.0.1` | `$AW_SINDRI_HOME/control.json`, `logs/serve.log` |
| Ledger | Append-only SQLite log of every event; the views are rebuilt from it | `ledger.db`, `views.db` |
| Temporal | The pinned `temporal server start-dev` that runs workflow instances. `serve` starts it and adopts it after a restart | `temporal.db`, `logs/temporal.log` |
| Profile | Your config: repos, services, the context section. Sindri runs only an approved snapshot of it | `profile/profile.yaml`, `approved/` |
| Onyx | Onyx Community Edition in Docker, pinned (`context/onyx.lock`). It indexes Linear and Slack; Sindri searches it with its own dedicated user | `$AW_SINDRI_HOME/onyx/` |
| Product map | The graph of features, surfaces and code that Sindri proposes from the repo and you review | `context/context.db` |
| Recipes | Declarative YAML workflows. `workflow.line` runs scoping, design, plan and a phase (build, prove, review, ship) as children | `recipes/sindri/` in this repo |
| The app | `sindri-ui`, an Avalonia desktop app. A window onto the control plane with no logic of its own | next to the `sindri` binary |

`$AW_SINDRI_HOME` defaults to `~/.agentic-workflow/sindri-app`. It is separate from the TypeScript Sindri's
`~/.agentic-workflow/sindri`.

## 1. Prerequisites

| Need | Version or size | How |
|---|---|---|
| .NET SDK | `10.0.401` exactly (`global.json`) | `scripts/sindri-app.sh` installs it into `~/.dotnet` when missing |
| Temporal CLI | the pin in `binaries.lock` | `scripts/binaries-lock.sh fetch temporal` (`sindri-app.sh` runs it too) |
| Docker | Docker Desktop, OrbStack or Docker Engine, with Compose v2 at 2.24.0 or later | install it yourself |
| Docker memory | about 5.5 GiB free for Onyx | Docker Desktop: Settings → Resources |
| Disk | about 10.5 GiB free under `$AW_SINDRI_HOME` for Onyx, plus room for the map build | |
| graphify and a sandbox | graphify at Sindri's pin; `sandbox-exec` (macOS, built in) or `bwrap` (Linux) | `scripts/install-deps.sh` (`AW_DRY_RUN=1` prints only) |
| `jq`, `git` | any recent | your package manager |

`scripts/install-deps.sh` installs host packages (Homebrew, or `sudo` with your Linux package manager), so run it
yourself (**Human-only**); `AW_DRY_RUN=1 scripts/install-deps.sh` only prints what it would do (**Agent OK**).

Check what this box can run for Onyx (**Agent OK**; it needs no control plane). `sindri-app` exists after step 2, so
before that run it through the script from the checkout:

```bash
scripts/sindri-app.sh onyx status   # docker, compose, memory, disk and port, each ok / warn / fail with the fix
```

## 2. Install and publish

From the agentic-workflow checkout (**Human-only**: the first run publishes the build, links `~/.local/bin/sindri-app` and may install the .NET SDK and Temporal):

```bash
cd ~/code/agentic-workflow
scripts/sindri-app.sh status
```

What the script does on every run:

1. Links `~/.local/bin/sindri-app` to itself (only from the main checkout, never from a linked worktree; set
   `AW_SINDRI_APP_NO_LINK=1` to skip). From then on, type `sindri-app`.
2. Installs the pinned .NET SDK and fetches the pinned Temporal CLI when missing.
3. Hashes the build's source files, the SDK and the platform into a **stamp**. When no build exists for the stamp, it
   publishes `sindri` (framework-dependent: the Temporal SDK can't be trimmed) and `sindri-ui` (NativeAOT) into
   `~/.agentic-workflow/bin/sindri-app/<stamp>/`. Any source change, committed or not, makes a new stamp; nothing
   else does.
4. Runs `sindri` with your arguments.

| Flag or rule | What it means |
|---|---|
| `--rebuild` | Publish again even though a build for this stamp exists. Declined for the build a running control plane uses: `sindri-app stop` first |
| The publish lock | One publish at a time per state dir (`bin/sindri-app/.lock` holding the owner's pid). A second run prints `waiting for another sindri-app publish (pid N)`; a dead owner's lock is taken over |
| The heavy-job slot | A publish runs under the box-wide heavy-job slot when `bin-r1b/heavy` exists, so it never overlaps a test suite or map build |
| Kept builds | The current and the previous build, plus any build a running control plane executes |
| Publish log | `~/.agentic-workflow/cache/sindri-app-build/publish.log`; its tail is printed when a publish fails |

You should see, on a first run:

```text
sindri-app: linked /Users/you/.local/bin/sindri-app -> /Users/you/personal/agentic-workflow/scripts/sindri-app.sh
sindri-app: publishing sindri (framework-dependent) and sindri-ui (NativeAOT, osx-arm64), build 1538a9f30cfc7caf; log: …
sindri-app: built /Users/you/.agentic-workflow/bin/sindri-app/1538a9f30cfc7caf
```

**Why `sindri-app` and not `sindri`.** Until the C# build is complete, `~/.local/bin/sindri` is still the
TypeScript Sindri, with its own launchd jobs, state and profile. The C# build has a side name so the two never meet.
Always type `sindri-app`.

> **Before the switch, read every `sindri` in a hint as `sindri-app`.** The C# app's help text, fix hints and approve
> prompts say `sindri …`. Copy one as it is and you run the TypeScript CLI against its own profile. A copied
> `sindri profile approve <hash>` then answers `SND-PROFILE-006 <hash> does not match the current profile (…); it
> changed since you viewed it`, because it compares the C# hash with the TypeScript profile. Retype it as
> `sindri-app profile approve <hash>`.

**Publish from the main checkout.** `sindri-app` publishes from the checkout the link points at. Keep that checkout up
to date (`git pull`) before you rebuild: a checkout that is behind gives you an older build, and a fix you expect (for
example, the dedicated Onyx user) is missing from it. Running `scripts/sindri-app.sh` from a linked worktree publishes
that worktree's source and makes it the current build for everyone on the box.

## 3. The profile

The profile is `~/.agentic-workflow/sindri-app/profile/profile.yaml` (`AW_SINDRI_PROFILE` moves it). Write it by
hand. A minimal one for one work repo:

```yaml
schemaVersion: 1
user: you
repos:
  - name: my-app
    path: /Users/you/code/my-app
    index:
      denyPaths: ["db/migrations/**/*.json", "**/*.png", "**/*.pdf"]   # never read by map extraction
services:
  context-docs-refresh: { every: 5m }      # indexes the repo's docs; nothing is indexed without it
context:
  repos: [my-app]
  docs:
    globs: ["AGENTS.md", "README.md", "docs/**/*.md"]
  sessions: { enabled: true }              # lets agent sessions read context (over MCP); off by default
```

Notes:

- `denyPaths` are repo-relative globs that extraction never reads, added to the built-in secret globs (`.env*`,
  `*.pem`, `**/secrets/**`). Deny generated data and binaries so the map build stays small. The profile-wide
  `index.denyPaths` applies to every repo.
- `context-sync` (`services: { context-sync: { every: 15m } }`) syncs Onyx's records on a cadence. Without it you
  sync by hand ([OPERATIONS.md](OPERATIONS.md)).
- `context.labeling`, `context.extract.graphify` and the rest are in `docs/csharp/sindri-profile.md` and
  `docs/sindri/context.md`. Unknown keys are errors.
- `onyx up` (step 5) adds `context.sources.onyx` for you. Don't write it by hand.

Check it (**Agent OK**):

```bash
sindri-app profile validate
```

```text
Profile valid: /Users/you/.agentic-workflow/sindri-app/profile/profile.yaml
hash 73ce99109634266e86f2ee0bb4ad01f18604e53d0ded764669241d13ce95470b, not approved
```

Approve it (**Human-only**). Without a hash it shows the hash and what changed; with the hash it asks you to type a
challenge back:

```bash
sindri-app profile approve              # preview: the live hash and the key paths changed since the last approval
sindri-app profile approve 73ce9910…    # the full 64-digit hash; type 'approve profile 73ce99' when asked
```

Approving starts the control plane if it isn't running. Every later change to the profile (a new repo, a deny path, a
limit) needs another `profile approve`; until then Sindri keeps running the previous approved snapshot, and `doctor`
shows `warn profile` with the fix.

**Add a repo** by adding an entry under `repos:` (and its name to `context.repos`), then validate and approve.
A repo's name must be letters, digits, `.`, `_` or `-`.

## 4. Start, status and doctor

| Command | Who | What it does |
|---|---|---|
| `sindri-app` | Human-only | Starts the control plane if it isn't running (detached, so closing the terminal doesn't stop it), opens the app, prints the startup summary, and prints the command that registers MCP for the repo you are in (step 7; it can't run it for you: no terminal) |
| `sindri-app --headless` | Human-only | The same without the app: for a box you reach over SSH or one that stays on |
| `sindri-app status` | Agent OK | The running control plane's summary; never starts it |
| `sindri-app doctor` | Agent OK | One line per check, each `ok`, `warn` or `fail` with a fix; works while the control plane is down |
| `sindri-app stop` | Human-only | Stops it: a managed Onyx's containers and Temporal, then releases the lease |

A first start on a box without Onyx asks "Set up Onyx (Sindri's business-context search) now? [Y/n]". Answer `n` if
you want to finish this step first; the needs-you list keeps a "Set up Onyx" row until you run step 5.

`status` when all is well:

```text
  profile   /Users/you/.agentic-workflow/sindri-app/profile/profile.yaml (approved, hash 73ce99)
  services  context-docs-refresh every 5m
  views     cursor 809 of 809
  health    healthy
  api       127.0.0.1:7410
  lease     held by pid 53290, epoch 6
  ready     running
```

`doctor` on a set-up box, and what each line means:

```text
ok    lease             held by my-mac/53290 since 2026-10-19T18:32:02Z, epoch 6
ok    ledger            schema 6/6
ok    views             cursor 809 of head 809
ok    profile           approved, hash 73ce99
ok    api               127.0.0.1:7410 answers (pid 53290, epoch 6)
ok    temporal          127.0.0.1:53876 answers (pid 53346), version 1.9.1 as pinned, namespace sindri present
ok    context:map       map v2 current (my-app, 412 nodes, 655 edges)
ok    context:retention no expired exports; retained map versions are within the newest 20 and the 90-day eval window
ok    context:onyx      Onyx v4.9.0 answers on 127.0.0.1:3000 (pinned v4.9.0); key: admin, Sindri's dedicated user
ok    context:sources   2 of 2 sources ok; session reads on
ok    context:sync      1 intent source(s) synced
ok    context:mcp-scope no user-wide 'sindri' MCP entry for Claude Code, Codex or Cursor
ok    launcher          no launcher.json at …; this box's viewer is Warp (warp) (sindri launch init writes it); …
ok    launcher:agents   claude-code (builtin): …/claude; codex (builtin): …/codex; cursor (builtin): …/agent
ok    onyx:stack        9 containers running, healthy, bound to 127.0.0.1:3000, digests match the pins
warn  claude-code       re-probed: claude 2.1.303 differs from the pinned probe (2.1.299) (fix: …)
warn  codex:read-only   pending: owner probe (fix: …)
```

| Line | `ok` means | When it isn't |
|---|---|---|
| `lease` | One control plane holds the host lease | `warn` when nothing runs: start it |
| `ledger` | The ledger's schema matches this build | `fail` `SND-LEDGER-001/002`: a build from another branch, or a disk problem |
| `views` | The views have caught up with the ledger | `fail` stalled: `sindri-app views rebuild` |
| `profile` | The live profile is the approved one | `warn` changed since approval: `sindri-app profile approve` |
| `api` | The control plane answers on its port | not running: `sindri-app` |
| `temporal` | The dev server answers, at the pinned version, with the `sindri` namespace | `fail`: `sindri-app stop`, then start again; see `logs/temporal.log` |
| `context:map` | A current product map exists | none yet: [OPERATIONS.md](OPERATIONS.md) |
| `context:onyx` | Onyx answers and the key is Sindri's dedicated user | `warn` `SND-CONTEXT-053` "not a dedicated service account": rerun `onyx up` on a current build (step 5) |
| `context:sources` | Every context source answered; whether sessions may read | `warn` some degraded (`SND-CONTEXT-010`), `fail` all down (`011`) |
| `context:sync` | Each intent source synced at least once | `warn` never synced or last run failed: [OPERATIONS.md](OPERATIONS.md) |
| `context:mcp-scope` | No user-wide `sindri` MCP entry | `warn` `SND-CONTEXT-026`: remove the user-wide entry it names |
| `onyx:stack` | Every Onyx container runs healthy, on loopback, at the pinned digests | `warn` Docker not answering or stack stopped; `fail` a container down or a port not on 127.0.0.1 |
| harness probes (`claude-code`, `codex:*`, `cursor-*`) | The installed agent CLI matches the probe pinned in the build | `warn` "differs" or "pending: owner probe" is normal on a dev box and doesn't block a run |

**After a new build.** A control plane keeps running the build it started with. When `sindri-app.sh` publishes a new
one it says `a control plane from an earlier build is running (pid N); 'sindri-app stop', then 'sindri-app', runs this
one`. Do exactly that (**Human-only**):

```bash
sindri-app stop
sindri-app            # or: sindri-app --headless
sindri-app status     # a new lease pid and epoch
```

## 5. Onyx

Onyx gives Sindri business context: Linear issues with their comments, and Slack. Sindri runs a pinned Onyx v4.9.0
for you in Docker, bound to `127.0.0.1` only. Connecting Linear and Slack uses your company's OAuth: get sign-off
first. Sindri never connects a source itself. Full reference: [onyx.md](onyx.md).

```bash
sindri-app onyx status     # Agent OK: where Onyx stands, and whether this box can run it
sindri-app onyx up         # Human-only: everything below, idempotent and resumable
```

`onyx up` does, in order (`state.json` records each step, so a rerun repeats nothing that is already right):

1. **Preflight**: Docker, Compose, memory, disk, port (3000, or the first free of 3001-3010).
2. **Fetch**: the pinned deployment files from GitHub, each checked against its sha256.
3. **Config**: `.env` and a compose override (nginx on `127.0.0.1` only, every image by digest, telemetry off).
4. **Pull** by digest, then **start**, waiting up to 15 minutes for Onyx to answer.
5. **Your account**: it opens Onyx's sign-up page. Create your own account (the first one is Onyx's admin) and press
   Enter.
6. **Sindri's user**: it asks once for *your own* Onyx admin login and password (not shown, never stored). With it,
   Sindri sets up its own dedicated Onyx user, `sindri@example.com` (an address nobody can receive mail at), makes it
   an admin, and makes a personal access token named `sindri-onyx`. You never type that address or its password.
7. **The key** goes in the macOS keychain (`keychain:onyx/sindri-onyx`), or a 0600 file under
   `$AW_SINDRI_HOME/secrets/` where there's no keychain.
8. **The profile**: it writes `context.sources.onyx` (`url`, `token`, `version`, `managed: true`) and shows the
   profile approval inline. Type the challenge.
9. It opens Onyx's connectors page and prints the checklist.

Then, in Onyx's web UI (connectors page), connect:

- **Linear**: the product's teams, with a start date far enough back to cover the history you want Sindri to learn
  from.
- **Slack**: the product's channels.

Indexing runs inside Onyx and can take a while on a large workspace.

You should see, in `doctor`:

```text
ok    context:onyx      Onyx v4.9.0 answers on 127.0.0.1:3000 (pinned v4.9.0); key: admin, Sindri's dedicated user
ok    context:sources   2 of 2 sources ok; session reads on
ok    onyx:stack        9 containers running, healthy, bound to 127.0.0.1:3000, digests match the pins
```

With a managed Onyx, `sindri-app` starts its containers and `sindri-app stop` stops them. `onyx down` (**Human-only**)
stops them by hand. Neither deletes Onyx's data.

## 6. Agent Steps

A Step whose runtime is `agent` launches an agent session (Claude Code, Codex or Cursor) in a terminal Sindri opens.
Sindri needs two things for that: a launcher file that says how this box opens a terminal, and the agent the profile
names. Without both, agent Steps fall to your desk too.

1. Write the launcher file (**Human-only** by convention: it writes a file in your home; Sindri doesn't refuse an agent here):

   ```bash
   sindri-app launch init              # detects the terminal (Warp, or another template) and writes ~/.agentic-workflow/launcher.json (0600)
   sindri-app launch init --stdout     # Agent OK: prints what it would write, writes nothing
   ```

   An existing file is refused without `--force`.

2. Name the agent in the profile:

   ```yaml
   workflow:
     agent: { target: claude-code }    # or codex, cursor, or an id you added to launcher.json
   ```

3. Validate (**Agent OK**) and approve (**Human-only**) the profile, as in step 3.

`doctor`'s `launcher` line shows the change. Before:

```text
ok    launcher          no launcher.json at /Users/you/.agentic-workflow/launcher.json; this box's viewer is Warp (warp) (sindri launch init writes it); …
```

After, it names the file instead of saying there is none. `sindri-app launch show` (**Agent OK**) lists the merged
agent catalog: each agent, its resolved binary, and whether Steps may run it.

## 7. MCP for agent sessions

Agent sessions in your work repo reach Sindri through its MCP endpoint (`/sindri/mcp`). Registration is per repo,
never user-wide, so work context never shows up in your personal repos.

```bash
cd ~/code/my-app
sindri-app mcp register                            # Agent OK: prints what it would change
sindri-app mcp register --apply --providers all    # Human-only: Claude Code (local scope), Cursor and Codex project files
```

A bare `sindri-app` run from the repo prints that `--apply` command (with `--repo` when it applies) once the control plane
is up. It can't run it for you: sindri only takes `--apply` from a terminal, and the watcher that waits for the plane has
none. Run it once, and again when the port changes. Each provider line is `registered`, `unchanged`, `skipped` (not installed), `registered, needs trust` or
`failed` with a fix. A tracked `.cursor/mcp.json` or `.codex/config.toml` that would change is `failed`, not edited:
add the entry by hand or keep those files out of git.

Sessions can read context only while `context.sessions.enabled` is `true`; otherwise every call answers
`SND-CONTEXT-020`. `doctor`'s `context:mcp-scope` warns on a user-wide `sindri` entry.

## 8. Open the app

```bash
sindri-app            # Human-only: starts the control plane if needed and opens the app
```

The startup summary's `ui` line says what opening the app did: `opened (sindri-ui, pid N)`, or where it looked when it
didn't find `sindri-ui` (it sits beside the `sindri` binary in the build directory; `AW_SINDRI_UI` points elsewhere).
The app shows Home (what needs you), Health (the same checks as `doctor`), and the Workflows, Context and Agents pages
as they land. It is a window onto the control plane: closing it doesn't stop Sindri.

You are set up. Operating Sindri (syncing context, building and reviewing the product map, approving recipes and
running the line) is in [OPERATIONS.md](OPERATIONS.md) until those move into the app.

## 9. Troubleshooting

### `SND-AUTH-002`: a command only a person may run

```text
SND-AUTH-002 … (this CLI ran as a session: CLAUDE_CODE_ENTRYPOINT is set | parent process claude | no terminal)
```

Sindri splits its commands into classes. Approving config and recipes, starting, answering and completing workflows
(`workflow start`, `answer`, `complete`, `command`, `cancel`, `handback`), building, reviewing and promoting the map,
syncing sources, registering MCP, managing Onyx, deciding, and starting or stopping the control plane are a person's
decisions. An agent that could run them could approve its own recipes, widen its own config, sign off its own desk
tasks and merge its own work. That is also why desk tasks are human-only by design: an agent may prepare a desk task's
output files, but only you submit them with `workflow complete`. (An agent Step's own launched session reports its
run with its launch secret; that is a different path, class `step-report`.)
So the CLI checks two things before it calls as `human`: stdin and stdout are a terminal, and there is no agent signal
(an agent variable such as `CLAUDECODE` or `CODEX_SANDBOX`, or an agent harness among its parent processes). Anything
else calls as `session`, and the API refuses those classes with `SND-AUTH-002`, naming what it saw.

Fix: run the command yourself, in your own terminal. Don't try to get around the check: unsetting variables doesn't
help (the parent-process check still sees the agent), and the check is there on purpose.

### The codes you're most likely to meet

| Code | What it means | Fix |
|---|---|---|
| `SND-API-001` | The control plane isn't running (or an agent session tried to start it) | `sindri-app` (Human-only) |
| `SND-API-002` | It didn't answer in time | `sindri-app status`; check `logs/serve.log` |
| `SND-AUTH-001` | The per-start human token is missing or stale | Rerun the command, or restart the app, after a restart of the control plane |
| `SND-LEASE-001` | Another live process holds the host lease | One control plane per box: use the running one, or `sindri-app stop` |
| `SND-CLI-002` | `serve` didn't come up, or Temporal didn't | `logs/serve.log`, `logs/temporal.log`; `scripts/binaries-lock.sh fetch temporal` |
| `SND-PROFILE-003` | The hash you approved isn't the live profile's | Run `profile approve` without a hash and copy the new one |
| `SND-PROFILE-004` | Approving needs a terminal and the typed confirmation | Run it in your own terminal and type the challenge exactly |
| `SND-PROFILE-006` "does not match the current profile" | You ran the TypeScript `sindri` (copied from a hint), which checks its own profile | Before the switch, read every `sindri` in a hint as `sindri-app`: rerun as `sindri-app profile approve <hash>` |
| `SND-PROFILE-006` from `sindri-app` | The approved snapshots can't be read or written | Check the permissions and free disk under `~/.agentic-workflow/sindri-app/approved/` |
| `SND-VIEW-001` | The views stalled | `sindri-app views rebuild` |
| `SND-ONYX-001` | Docker isn't usable (not installed, not running, still starting) | Start Docker, then rerun |
| `SND-ONYX-003` | Not enough disk or Docker memory for Onyx | The message says how much; raise Docker's memory in its settings |
| `SND-ONYX-007` | The login you gave `onyx up` isn't an Onyx admin | Use the first account you signed up with |
| `SND-ONYX-008` | Login, registration or the token failed | Read the message: invite-only Onyx needs `sindri@example.com` invited once |
| `SND-CONTEXT-053` | Onyx's key isn't Sindri's dedicated user | Update the checkout, rebuild, restart, rerun `onyx up` |

The full catalogue is in `docs/csharp/sindri.md` ("Errors") and `docs/sindri/context.md`.

### Gotchas from the first setup

- **An old build after a merge.** A fix merged upstream isn't in your build until the main checkout has it and you
  rebuild and restart: `git pull`, `scripts/sindri-app.sh --rebuild status`, `sindri-app stop`, `sindri-app`.
- **A first Onyx key from an older build** was an admin person's token, so `doctor` warned `SND-CONTEXT-053`. Rerunning
  `onyx up` on a current build moves Sindri to its dedicated user and revokes the old token.

## Where to go next

- Operating it: [OPERATIONS.md](OPERATIONS.md).
- The guided page: [`docs/sindri-setup.html`](../sindri-setup.html).
- How the pieces fit: [`docs/sindri-architecture.html`](../sindri-architecture.html).
- Every feature, with status: [`docs/sindri.html`](../sindri.html).
- References: `docs/csharp/sindri.md` (the CLI and control plane), `docs/csharp/sindri-profile.md`,
  `docs/sindri/context.md`, `docs/sindri/onyx.md`, `docs/csharp/sindri-workflow.md`, `docs/csharp/sindri-ui.md`.
