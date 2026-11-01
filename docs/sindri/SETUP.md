# Setting up Sindri (C#)

This guide takes a new box from nothing to a running Sindri: the control plane up, your sources connected, agent sessions able
to reach it, and the app open. It follows the setup the team did by hand on a real work repo (a 20k-file TypeScript
monorepo), with that repo's names left out. The same steps, one per screen and with pictures, are in the guided page
[`docs/sindri-setup.html`](../sindri-setup.html); open it from disk in any browser.

Operating Sindri once it is set up (the product map, recipes, running the line) is in [OPERATIONS.md](OPERATIONS.md).

Examples use `~/code/my-app` as the work repo and `my-app` as its name in the profile. Replace both with yours.

## Who runs what

Some commands only a person may run, at a real terminal. Sindri refuses them to a CLI that can't read its token with
`SND-AUTH-002` (see [Troubleshooting](#9-troubleshooting)). Each command below is marked:

- **Human-only**: you type it in your own terminal (Terminal, iTerm, Warp), not inside Claude Code, Codex or Cursor.
- **Agent OK**: read-only or harmless; an agent session may run it for you.

## 0. The pieces

```
                         you (terminal)            the Sindri app (sindri-ui)
                               │                              │
                               ▼                              ▼
  ┌──────────────────── control plane: `sindri serve`, 127.0.0.1:7410 ─────────────────────┐
  │  host lease · ledger.db (every event) · views.db · the API and event stream · scheduler │
  │  workflow engine ──► Temporal dev server (pinned, loopback, temporal.db)                 │
  │  context engine  ──► context.db: repo docs, product map; index.db: GitHub, Slack, Linear │
  │  MCP endpoint /sindri/mcp ◄── agent sessions in your work repo                           │
  └──────────────────────────────────────────────────────────────────────────────────────────┘
        ▲ reads only the approved profile             │ searches
        │                                             ▼
   profile.yaml ── `profile approve` ──►     sources: GitHub, Slack, Linear (read-only tokens)
   recipes/*.yaml ── `recipe approve` ──►    the registry (workflow.line and its children)
```

| Piece | What it is | Where it lives |
|---|---|---|
| Control plane | One process per box (`sindri serve`), holding the host lease. Every other command is a client of its API on `127.0.0.1` | `$AW_SINDRI_HOME/control.json`, `logs/serve.log` |
| Ledger | Append-only SQLite log of every event; the views are rebuilt from it | `ledger.db`, `views.db` |
| Temporal | The pinned `temporal server start-dev` that runs workflow instances. `serve` starts it and adopts it after a restart | `temporal.db`, `logs/temporal.log` |
| Profile | Your config: repos, services, the context section. Sindri runs only an approved snapshot of it | `profile/profile.yaml`, `approved/` |
| Record index | Sindri's own store of GitHub issues and PRs, Slack threads and Linear issues, fed by its own connectors with read-only tokens | `$AW_SINDRI_HOME` (one SQLite store), tokens in the OS keychain |
| Product map | The graph of features, surfaces and code that Sindri proposes from the repo and you review | `context/context.db` |
| Recipes | Declarative YAML workflows. `workflow.line` runs scoping, design, plan and a phase (build, prove, review, ship) as children | `recipes/sindri/` in this repo |
| The app | `sindri-ui`, an Avalonia desktop app. A window onto the control plane with no logic of its own | next to the `sindri` binary |

`$AW_SINDRI_HOME` defaults to `~/.agentic-workflow/sindri-app`.

## 1. Prerequisites

| Need | Version or size | How |
|---|---|---|
| .NET SDK | `10.0.401` exactly (`global.json`) | `scripts/install-cs.sh` (run by `setup.sh`) installs it into `~/.dotnet` when missing |
| Temporal CLI | the pin in `binaries.lock` | `scripts/binaries-lock.sh fetch temporal` (`install-cs.sh` runs it too) |
| Disk | room under `$AW_SINDRI_HOME` for the map build and the record index | |
| graphify and a sandbox | graphify at Sindri's pin; `sandbox-exec` (macOS, built in) or `bwrap` (Linux) | `scripts/install-deps.sh` (`AW_DRY_RUN=1` prints only) |
| `jq`, `git` | any recent | your package manager |

`scripts/install-deps.sh` installs host packages (Homebrew, or `sudo` with your Linux package manager), so run it
yourself (**Human-only**); `AW_DRY_RUN=1 scripts/install-deps.sh` only prints what it would do (**Agent OK**).

## 2. Install and publish

From the agentic-workflow checkout (**Human-only**: it publishes the builds, links `~/.local/bin/sindri` and may install the .NET SDK and Temporal):

```bash
cd ~/code/agentic-workflow
./setup.sh                       # everything; or just the binaries:
scripts/install-cs.sh            # --publish, then --link
sindri status
```

What `scripts/install-cs.sh` does:

1. Installs the pinned .NET SDK and fetches the pinned Temporal CLI when missing.
2. Hashes the build's source files, the SDK and the platform into a **stamp**. When no build exists for the stamp, it
   publishes `sindri` (framework-dependent: the Temporal SDK can't be trimmed), `sindri-ui` (NativeAOT) and the other
   binaries (`judge`, `aw-packets`, `bugfix-state`, `ui-evidence`, `aw-readiness`) into
   `~/.agentic-workflow/bin/cs/<stamp>/`. Any source change, committed or not, makes a new stamp; nothing else does.
3. `--link` points `bin/cs/current` at the new build and writes the wrappers in `~/.local/bin` (`sindri`, `judge`, ...),
   replacing any older wrapper of the same name.

| Flag or rule | What it means |
|---|---|
| `--publish` / `--link` | Only the first or only the second step; with neither it does both. `--publish` leaves `current` alone |
| `--rebuild` | Publish again even though a build for this stamp exists. Declined for the build a running control plane uses: `sindri stop` first |
| `--dry-run` | Print what it would do |
| The heavy-job slot | A publish runs under the box-wide heavy-job slot, so it never overlaps a test suite or map build |
| Kept builds | The newest two, plus any build a running control plane executes |

**Publish from the main checkout.** The wrappers run what the last `install-cs.sh` published. Keep that checkout up
to date (`git pull`) before you rebuild: a checkout that is behind gives you an older build, and a fix you expect is
missing from it. Running `scripts/install-cs.sh` from a linked worktree publishes that worktree's source and makes it
the current build for everyone on the box.

**Setup with Sindri already running.** `./setup.sh` also runs `scripts/cutover-clean-ts.sh`, which removes the retired
TS `sindri-app` builds. It finds the running control plane's pid in `${AW_SINDRI_HOME:-$AW_STATE_DIR/sindri-app}/control.json`
(the same file `install-cs.sh` reads). If that control plane runs from `bin/sindri-app/`, the first step refuses with
`cutover: refused: the Sindri control plane (pid N) runs from <state>/bin/sindri-app/. Nothing was changed. Run: sindri-app stop (or sindri stop), then re-run ./setup.sh`
and exits 1. If it gets past that, the last step keeps the build dir instead of deleting it and prints
``cutover: kept <state>/bin/sindri-app: Sindri is still running from it; run `sindri stop`, then re-run ./setup.sh``.
Either way: `sindri stop`, then `./setup.sh` again.

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
- `context-sync` (`services: { context-sync: { every: 15m } }`) syncs the older intent sources on a cadence. Without it you
  sync by hand ([OPERATIONS.md](OPERATIONS.md)). The record index syncs itself every 5 minutes while `serve` runs (step 5).
- `context.labeling`, `context.extract.graphify` and the rest are in `docs/csharp/sindri-profile.md` and
  `docs/sindri/context.md`. Unknown keys are errors.
- The sources (GitHub, Slack, Linear) go under `context.sources`; step 5 has the keys. `sindri index connect` writes each source's `token` pointer for you.

Check it (**Agent OK**):

```bash
sindri profile validate
```

```text
Profile valid: /Users/you/.agentic-workflow/sindri-app/profile/profile.yaml
hash 73ce99109634266e86f2ee0bb4ad01f18604e53d0ded764669241d13ce95470b, not approved
```

Approve it (**Human-only**). Without a hash it shows the hash and what changed; with the hash it asks you to type a
challenge back:

```bash
sindri profile approve              # preview: the live hash and the key paths changed since the last approval
sindri profile approve 73ce9910…    # the full 64-digit hash; type 'approve profile 73ce99' when asked
```

Approving starts the control plane if it isn't running. Every later change to the profile (a new repo, a deny path, a
limit) needs another `profile approve`; until then Sindri keeps running the previous approved snapshot, and `doctor`
shows `warn profile` with the fix.

**Add a repo** by adding an entry under `repos:` (and its name to `context.repos`), then validate and approve.
A repo's name must be letters, digits, `.`, `_` or `-`.

## 4. Start, status and doctor

| Command | Who | What it does |
|---|---|---|
| `sindri` | Human-only | Starts the control plane if it isn't running (detached, so closing the terminal doesn't stop it), opens the app, prints the startup summary, and prints the command that registers MCP for the repo you are in (step 7; it can't run it for you: no terminal) |
| `sindri --headless` | Human-only | The same without the app: for a box you reach over SSH or one that stays on |
| `sindri status` | Agent OK | The running control plane's summary; never starts it |
| `sindri doctor` | Agent OK | One line per check, each `ok`, `warn` or `fail` with a fix; works while the control plane is down |
| `sindri stop` | Human-only | Stops it: Temporal, then releases the lease |

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
ok    context:sources   2 of 2 sources ok; session reads on
ok    context:sync      sources sync: 1 intent source(s) synced
ok    context:mcp-scope no user-wide 'sindri' MCP entry for Claude Code, Codex or Cursor
ok    launcher          no launcher.json at …; this box's viewer is Warp (warp) (sindri launch init writes it); …
ok    launcher:agents   claude-code (builtin): …/claude; codex (builtin): …/codex; cursor (builtin): …/agent
warn  claude-code       re-probed: claude 2.1.303 differs from the pinned probe (2.1.299) (fix: …)
warn  codex:read-only   pending: owner probe (fix: …)
```

| Line | `ok` means | When it isn't |
|---|---|---|
| `lease` | One control plane holds the host lease | `warn` when nothing runs: start it |
| `ledger` | The ledger's schema matches this build | `fail` `SND-LEDGER-001/002`: a build from another branch, or a disk problem |
| `views` | The views have caught up with the ledger | `fail` stalled: `sindri views rebuild` |
| `profile` | The live profile is the approved one | `warn` changed since approval: `sindri profile approve` |
| `api` | The control plane answers on its port | not running: `sindri` |
| `temporal` | The dev server answers, at the pinned version, with the `sindri` namespace | `fail`: `sindri stop`, then start again; see `logs/temporal.log` |
| `context:map` | A current product map exists | none yet: [OPERATIONS.md](OPERATIONS.md) |
| `context:sources` | Every context source answered; whether sessions may read | `warn` some degraded (`SND-CONTEXT-010`), `fail` all down (`011`) |
| `context:sync` | Each intent source synced at least once | `warn` `sources sync: …` never synced or last run failed: [OPERATIONS.md](OPERATIONS.md). This row is the older sources sync (queries no longer search Linear live); the record index has its own `context:index-*` rows, whose sync row reads `record index: …` |
| `context:mcp-scope` | No user-wide `sindri` MCP entry | `warn` `SND-CONTEXT-026`: remove the user-wide entry it names |
| `index:onyx-leftover` | Nothing an old `onyx up` left on this box (no row when clean) | `fail` containers, volumes, a keychain entry or the `onyx/` directory are left: `sindri onyx down --purge`; `warn` once `context.index.acknowledgeOnyxLeftover` is true |
| harness probes (`claude-code`, `codex:*`, `cursor-*`) | The installed agent CLI matches the probe pinned in the build | `warn` "differs" or "pending: owner probe" is normal on a dev box and doesn't block a run |

**After a new build.** A control plane keeps running the build it started with. When `install-cs.sh` publishes a new
one it says `a control plane from an earlier build is running (pid N); 'sindri stop', then 'sindri', runs this
one`. `install-cs.sh --link` (so `./setup.sh`) says the same after it switches: ``install-cs: Sindri is still running build
<old>; run `sindri stop && sindri` to switch to <new>``, and exits 0. It never restarts the control plane for you, because a
restart can strand open agent runs. Do exactly that (**Human-only**):

```bash
sindri stop
sindri            # or: sindri --headless
sindri status     # a new lease pid and epoch
```

## 5. Sources

Sindri keeps its own index of your GitHub issues and PRs, Slack threads and Linear issues, and reads them with
read-only tokens you make. Sindri used to read these through Onyx; it now reads them itself, and Onyx's Confluence
and Drive connectors have no replacement. Full reference: [sources.md](sources.md).

**1. Name the sources in the profile**, then validate (step 3):

```yaml
context:
  sources:
    github: { repos: [your-org/my-app] }          # optional: host
    slack:  { workspace: your-workspace, channels: [{ id: C0123456789, name: eng-product }] }
    linear: { teams: [APP] }                      # key setup: linear.md
```

`context.index.windowDays` (default 183) is how many days back a first sync reads.

**2. Connect** (**Human-only**: an agent session is refused, `SND-AUTH-002`):

```bash
sindri index connect github
sindri index connect slack
```

Each asks for the token once, checks the token and every repo or channel you listed *before* saving anything, stores
it in the OS keychain (service `sindri-index`; an owner-only file under `$AW_SINDRI_HOME/secrets/` where there's no
keychain) and writes the secret pointer into the profile's `token` key. Make the tokens yourself, read-only:

- **GitHub**: a fine-grained personal access token with Contents, Issues, Pull requests and Metadata read, on the
  listed repos only.
- **Slack**: a bot token with `channels:read` and `channels:history` (add `groups:read` and `groups:history` only for
  listed private channels), in an app you install.
- **Linear**: see [linear.md](linear.md).

**3. Approve** (**Human-only**). Adding a token pointer or a source changes the profile, so approve again (step 3).

**4. Sync.** The built-in `index-sync` service runs it every 5 minutes while `serve` runs. By hand:

```bash
sindri index sync [<source>] [--detach] [--json] [--no-start]
sindri index status [--json]      # Agent OK: one line per source
```

A source's line reads `not started`, `syncing recent window`, `usable, filling older history`, `complete` or `last sync failed: <code>`. Embedding is a separate
resumable job (Ollama, local only): `sindri index embed pause|resume`. `sindri index purge <source>` (**Human-only**)
deletes one source's records after you type yes.

You should see, in `doctor`, `context:index-store`, `context:index-ollama`, `context:index-github`, `context:index-slack`, `context:index-linear`, `context:index-linear-projects` and `context:index-sync` rows
all `ok`.

**An old box that once ran `onyx up`.** Run `sindri onyx down --purge` once (**Human-only**). It lists the leftover
Onyx containers and volumes, `$AW_SINDRI_HOME/onyx/` and the keychain entries it recorded, asks for a yes, then
removes them. Those volumes hold a raw, unscrubbed copy of your Slack and Linear text, so don't leave them. `doctor`'s
`index:onyx-leftover` row fails while they exist. A box that never ran `onyx up` sees none of this.

## 6. Agent Steps

A Step whose runtime is `agent` launches an agent session (Claude Code, Codex or Cursor) in a terminal Sindri opens.
Sindri needs two things for that: a launcher file that says how this box opens a terminal, and the agent the profile
names. Without both, agent Steps fall to your desk too.

1. Write the launcher file (**Human-only** by convention: it writes a file in your home; Sindri doesn't refuse an agent here):

   ```bash
   sindri launch init              # detects the terminal (Warp, or another template) and writes ~/.agentic-workflow/launcher.json (0600)
   sindri launch init --stdout     # Agent OK: prints what it would write, writes nothing
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

After, it names the file instead of saying there is none. `sindri launch show` (**Agent OK**) lists the merged
agent catalog: each agent, its resolved binary, and whether Steps may run it.

## 7. MCP for agent sessions

Agent sessions in your work repo reach Sindri through its MCP endpoint (`/sindri/mcp`). Registration is per repo,
never user-wide, so work context never shows up in your personal repos.

```bash
cd ~/code/my-app
sindri mcp register                            # Agent OK: prints what it would change
sindri mcp register --apply --providers all    # Human-only: Claude Code (local scope), Cursor and Codex project files
```

A bare `sindri` run from the repo prints that `--apply` command (with `--repo` when it applies) once the control plane
is up. It can't run it for you: sindri only takes `--apply` from a terminal, and the watcher that waits for the plane has
none. Run it once, and again when the port changes. Each provider line is `registered`, `unchanged`, `skipped` (not installed), `registered, needs trust` or
`failed` with a fix. A tracked `.cursor/mcp.json` or `.codex/config.toml` that would change is `failed`, not edited:
add the entry by hand or keep those files out of git.

Sessions can read context only while `context.sessions.enabled` is `true`; otherwise every call answers
`SND-CONTEXT-020`. `doctor`'s `context:mcp-scope` warns on a user-wide `sindri` entry.

## 8. Open the app

```bash
sindri            # Human-only: starts the control plane if needed and opens the app
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
syncing sources, connecting and purging them, registering MCP, deciding, and starting or stopping the control plane are a person's
decisions. An agent that could run them could approve its own recipes, widen its own config, sign off its own desk
tasks and merge its own work. That is also why desk tasks are human-only by design: an agent may prepare a desk task's
output files, but only you submit them with `workflow complete`. (An agent Step's own launched session reports its
run with its launch secret; that is a different path, class `step-report`.)
The CLI calls as `human` whenever it can read Sindri's per-start token file, so an agent session you start uses it as you
would. A CLI that can't read the token calls as `session`, and the API refuses those classes with `SND-AUTH-002`.

Fix: run Sindri first (`sindri`), then run the command.

### The codes you're most likely to meet

| Code | What it means | Fix |
|---|---|---|
| `SND-API-001` | The control plane isn't running | `sindri` (Human-only) |
| `SND-API-002` | It didn't answer in time | `sindri status`; check `logs/serve.log` |
| `SND-AUTH-001` | The per-start human token is missing or stale | Rerun the command, or restart the app, after a restart of the control plane |
| `SND-LEASE-001` | Another live process holds the host lease | One control plane per box: use the running one, or `sindri stop` |
| `SND-CLI-002` | `serve` didn't come up, or Temporal didn't | `logs/serve.log`, `logs/temporal.log`; `scripts/binaries-lock.sh fetch temporal` |
| `SND-PROFILE-003` | The hash you approved isn't the live profile's | Run `profile approve` without a hash and copy the new one |
| `SND-PROFILE-006` | The approved snapshots can't be read or written | Check the permissions and free disk under `~/.agentic-workflow/sindri-app/approved/` |
| `SND-VIEW-001` | The views stalled | `sindri views rebuild` |
| `SND-INDEX-009` | A source's token pointer doesn't resolve, or the source refused the token | `sindri index connect <source>` again |
| `SND-INDEX-010` | A listed repo can't be read with that token | Give the token access to it, or take it out of the profile |
| `SND-INDEX-014` | `onyx down --purge` couldn't remove something | The message names it; remove it by hand and rerun |

The full catalogue is in `docs/csharp/sindri.md` ("Errors") and `docs/sindri/context.md`.

### Gotchas from the first setup

- **An old build after a merge.** A fix merged upstream isn't in your build until the main checkout has it and you
  rebuild and restart: `git pull`, `scripts/install-cs.sh --rebuild`, `sindri stop`, `sindri`.

## Where to go next

- Operating it: [OPERATIONS.md](OPERATIONS.md).
- The guided page: [`docs/sindri-setup.html`](../sindri-setup.html).
- How the pieces fit: [`docs/sindri-architecture.html`](../sindri-architecture.html).
- Every feature, with status: [`docs/sindri.html`](../sindri.html).
- References: `docs/csharp/sindri.md` (the CLI and control plane), `docs/csharp/sindri-profile.md`,
  `docs/sindri/context.md`, `docs/sindri/sources.md`, `docs/csharp/sindri-workflow.md`, `docs/csharp/sindri-ui.md`.
