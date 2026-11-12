# Sindri's first run: from a fresh box to a working Sindri without reading a doc

Status: first draft (ideas lane, 2026-11-12). Serves #3019 (first run) under #2978 (one app that replaces the others). It builds the "What we take" lines of the prior-art ledger `docs/sindri/prior-art/first-run.md` (#2989).
It amends the setup-once spec (`docs/superpowers/specs/2026-10-19-sindri-setup-once-design.md`) where §2 says so, and sits beside the Home, Workflows and Reviews specs of 2026-11-12, reusing their row grammar and keys.
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

Code facts are read at `origin/main` 68ae49380a. "Ledger N" is problem N of the ledger. A call with no source is marked *agent default (lead, 2026-11-12)*.

## 0. Why

The owner's own box is the proof it isn't there. On 2026-11-12 the owner's serve on port 7410 ran a build older than the installed one, the home had no `profile.yaml`, and `sindri doctor` threw (#2954). The fix was `sindri stop && sindri`, `sindri profile init` and `sindri repo add <path>`, typed from memory.

ux walked a fresh scratch home on main 7c81646696 with this repo (15,223 files), t0 = bare `sindri` (#3019, comment of 10:39Z):

| Step | Time | What broke |
|---|---|---|
| serve up | 9.9 s | bare `sindri` exits 1 and points at `profile init` and a doc (#3114) |
| app ready | +3 s | Home shows `Profile ✗ unreadable`, an Obsidian error and two "didn't load" lines (#3113) |
| repo added, settings done | t+81 s | setup copy names `setup.sh --with-sindri` and an approval that no longer exists (#3115) |
| first `BuildCodeIndex` (82,825 symbols) | about 18 min | setup says "To do" for the whole build (#3112); System says "needs a look" (#3183) |
| first map | never on its own | nothing started `BuildContextMap` (#3126, fixed in #3220); doctor's fix is `context map import <file>` (#3216) |
| embeddings | stuck at 32 of 82,825 | long bodies pass Ollama's context; doctor says "start Ollama" (#3128) |

What main does today:

- **Setup is one step model already.** `SetupStep` (`Core/Setup/SetupStep.cs:14`) has `Id`, `Title`, `State`, `Why`, `Detail`, `Error`, `Action`, `Progress`, `Items`. `SetupStepId` (`Core/Setup/SetupStepId.cs`) has five ids: `repo`, `profile`, `mcp`, `map`, `sources`. `SetupSteps` (`Core/Setup/SetupSteps.cs`) holds the pure rule per step.
- **`sindri setup` needs serve.** `SetupVerbs.Run` (`Host/Commands/SetupVerbs.cs:26`) reads `GET /sindri/setup` and never starts serve. With serve down it prints `SND-API-001` and exits 1, which is the first-run case.
- **Adding a repo writes the profile.** `IProfileRepoWriter` (`Core/Setup/IProfileRepoWriter.cs:16`) returns `Created` when no file exists. So "no profile" and "no repo" are one state: one Add writes both.
- **The app shows CLI lines where a button should be.** `OpensInApp` (`Ui/ViewModels/Shell/ShellViewModel.Setup.cs:20`) returns `false` for every action, so every step falls back to its CLI form. qa's sweep filed 13 pages (#3054 to #3066).
- **The app never starts serve.** No `Process.Start` is in `Aw.Sindri.Ui` or `Aw.Sindri.Ui.Desktop` (#3055, ui's comment). The "Not running" card says "Start it from a terminal:" (`Labels.Setup.cs:62`).
- **A stale serve is a sentence, not a button.** `StaleBuild.Is` (`Core/Doctor/StaleBuild.cs:13`) compares two stamps and its fix text is `sindri stop && sindri`. The only route is `POST /sindri/stop` (`Host/Catalog/ShellRows.cs:15`).
- **Stamps are hashes, not numbers.** A stamp is 16 hex characters (`scripts/install-cs.sh:126`). Each build folder holds `build-info` with `commit=` and `built=` (`scripts/install-cs.sh:384`). So "newer" is a compare of the two `built=` times, and "build 1112 → 1118" in the drawings has no source.
- **No account step.** GitHub, Linear and Slack connect from the Connections page with a pasted key or a client id the owner registers (`config/connections/presets/github.json`, `linear.json`). Slack refuses a loopback redirect (`slack.json`, `loopbackAllowed: false`).

The job: **open the app on a fresh box, answer at most 2 questions (which repos, which accounts), and land on Home with a real row from your repo. Every state on the way has one button. An agent can do the same setup through `sindri setup`, except the owner's own verbs.**

## 1. The bar

| Step of first run | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Start without commands | 1 | Tailscale `up`, then the browser | the app is the command; a stopped serve is a screen with Start Sindri | §4.1 |
| Pick repos | 2 | GitHub Desktop drag, Conductor buttons | a streamed scan with ticks; drag a folder as the fallback | §4.2 |
| Connect accounts | 3 | Linear's integration page (six clicks, an admin) | one button per account, state on the row | §4.3 |
| See the first map build | 4 | Cursor's progress view (came and went), Docker's Starting page (sticks) | n/N files, an age, a way out, a timeout that turns into a button | §4.4 |
| Fix a stale thing | 5 | Docker's Troubleshoot icon, Tailscale's Reauthorize | a fixed line under the top bar, Restart Sindri; Reconnect on an expired account | §4.5 |
| Not start in an empty room | 6 | Zed's import, with what it skips | skip import; a zero-setup workflow makes the first real row | §4.4 |
| Land on a useful screen | 7 | Superhuman's Dashboard | Home with a real row, timed in CI | §8 |

**The bar in one line:** from no home at all to Home with one real row from this repo in under 3 minutes, with at most 2 questions, no YAML, no CLI line on screen, and no doc opened; the map can still be building.

Done-checks. Each is a test or a timed run in the real app (`aw-ui-driver`) on a scratch home, never the owner's home or the owner's serve on 7410.

| # | Check | Measured as |
|---|---|---|
| F1 | **Time to Home.** Fresh home to Home showing the hello-sindri run row and the item it made in this repo. | Seconds per step and in total, printed by the timed run (§8) and recorded in CI. Target under 180 s. Today: never, because no step leads there. |
| F2 | **Two questions.** The walk asks for repos, then accounts, and nothing else. | `aw-ui-driver` counts screens that take input before Home: at most 2. Skip on accounts counts as an answer. |
| F3 | **One button per state.** Every state in §6's table has exactly one primary button and no CLI line or raw code outside Details. | One frames test per state row; qa's sweep of #3054 to #3066 closes each issue. |
| F4 | **Same steps everywhere.** `sindri setup --json` and the Setup page list the same ids in the same order with the same titles. | One golden for the route; one Ui test compares the page's rows to the route's steps. |
| F5 | **Setup without serve.** With serve down, `sindri setup` prints the steps it can read locally and exits 1 with "Sindri is not running" as the first step. | A CLI test on a scratch home with no `control.json`. |
| F6 | **A stale serve fixes itself on one click.** | Install a second build, click Restart Sindri: serve runs the installed stamp within 15 s; open runs are named and kept. |
| F7 | **Doctor never throws on an older home.** | A home whose `views.db` and `ledger.db` an older build wrote: doctor prints every row, one fail row per store that is behind, exit 1, no unhandled exception (#2954). |
| F8 | **An agent can set up.** An agent session runs `sindri setup --apply --repo <path>` and reaches every step but the accounts and the restart. | A CLI test with session headers; the refused verbs exit 2 with `SND-AUTH-002`. |
| F9 | **First paint.** The first window paints from the local home in under 100 ms, before serve answers. | Window open to first frame, measured by perf. |
| F10 | **Scan speed.** The first 10 repos show in under 2 s. | Timed on this box's home and on a scratch home with 200 folders. |

## 2. Decisions

1. **The app is the first command.** Bare `sindri` starts serve and opens the app, as today. With no profile it says `profile   not set up yet: finish setup in the app (or 'sindri setup' to see the steps)` and `ready     running; setup isn't finished`, and exits 0 (#3114). Doctor and `profile set` keep their `profile init` fix line. The `then and` typo in `YamlProfileSource.cs:17` and `YamlProfileIndexWriter.cs:136` goes.
2. **The app starts serve.** "Start Sindri" runs the installed `sindri --headless` beside `sindri-ui`, detached, the way a bare `sindri` does. It polls `GET /sindri/status` for up to 30 s. serve's own single-serve guard refuses a second one. This is the Reviews spec's §5.7 "Start Sindri" rule, owned once here. *agent default (lead, 2026-11-12)*: the Desktop host owns the process start, not `Aw.Sindri.Ui`, so the views stay free of `Process`.
3. **Two questions, no third.** Question 1 is repos (§4.2), question 2 is accounts (§4.3). The profile is written by the first Add. The agent target is a done line, not a question (engine plan on #3019). Model, label and surface choices leave setup until each is a toggle (#3115 item 4).
4. **No profile and no repo are one state.** Adding the first repo writes the profile (`IProfileRepoWriter` `Created`). The "Create my profile" panel in the drawings goes; its button is "Find my repos". *agent default (lead, 2026-11-12)*: one click fewer, and `profile init` stays a CLI verb for agents.
5. **One step list, from Core.** `SetupStep` stays the model. The app renders the steps the route sends and adds none of its own, so `Labels.Setup*` step copy is dropped where the route sends the text (ui plan item 1). `sindri setup` renders the same list.
6. **Eight steps, in this order:** `serve`, `repo`, `profile`, `agent`, `mcp`, `sources`, `map`, `first-workflow` (§3.1). The drawings' 11 rows fold into these. "Repos found" and "Repos added" are one `repo` step. "Profile saved" and "Profile checked" are one `profile` step. The failed first read of pull requests and docs is an item of `sources`. *agent default (lead, 2026-11-12)*.
7. **`sindri setup` works without serve.** When serve is down, the CLI computes `serve`, `repo`, `profile` and `agent` in process from the same `SetupSteps` rules and reports the other four as "Waits for Sindri to run". `--json` keeps its shape. Meta's answer 3 on #3019.
8. **`sindri setup --apply` does what needs no human.** It starts serve when it is down, adds the repos named by `--repo` (or, with none named, the scan's pre-ticked ones), and lets the setup service set the agent target. It never signs in, never restarts a serve, never registers MCP for a session caller. With no repo named and none pre-ticked it refuses with `SND-SETUP-007`. Meta's answer 3 on #3019; the defaults are *agent default (lead, 2026-11-12)*.
9. **Restart is the owner's, and keeps open runs.** `POST /sindri/restart` runs the open-runs check first (the rule of `scripts/lib/sindri-open-runs.sh`). With none open it stops serve through the `/sindri/stop` path, and the app or CLI starts the installed build. With runs open it refuses with `SND-SETUP-006` and names them; "Restart when they finish" sends `?wait=true`. serve only stops itself (meta's answer 1). The row is a Desk action like `StopProcess`, so a session gets `SND-AUTH-002` (P0 confirms the Desk class refuses a session).
10. **"Newer" comes from `build-info`.** The serve step compares the `built=` times of the running and the installed build. Installed later: "A newer Sindri is installed (7c81646, built today 10:31)". Installed earlier: "A different Sindri is installed". Commit names show as 7 characters; stamps never show outside Details. *agent default (lead, 2026-11-12)*.
11. **GitHub uses the gh login first.** When `gh auth status` passes, the GitHub row's one button is "Use the gh login (ada)". It sets `review.credential` to `command:gh auth token`, so no token is copied or stored. With no gh login, the button is "Sign in to GitHub" (device flow, a shipped client id). Meta's answer 6 on #3019.
12. **One GitHub account feeds every reader.** The accounts step writes the GitHub credential once and points each reader at it: `review.credential`, the `github` index source and the `github` connection. *agent default (lead, 2026-11-12)*; §11 gap 2 says what P0 must confirm.
13. **Linear signs in through the browser; Slack is a guided paste.** Linear uses OAuth with PKCE and a client id that ships in the build (meta's answer 6). Slack shows three numbered lines and a Paste field until the #2979 probe says which token Messages needs.
14. **The map step follows the first code index.** It is Running from the moment `BuildCodeIndex` starts, with that job's progress ("Indexing files: 7,000 of 15,223"), then Running with the map's progress (#3112). It never says "Sindri builds it on its own" while nothing is building.
15. **Home never waits on the map.** hello-sindri needs no map, no account, no model and no network (engine plan on #3019), so the last setup step offers it as soon as a repo is added. This answers design's open call 4. *agent default (lead, 2026-11-12)*.
16. **Failed is not empty.** A failed read never shows the empty state. It shows the inline state line with Retry and keeps the last list under it (design's grammar piece on `pr-assets/grammar-2978`, c73f521779).
17. **One shared line for an older serve.** "The X list can't be read from this Sindri. It is older than the app." with Restart Sindri, on every page (qa's pattern across 8 pages).
18. **Bare letters never write.** Ticking a repo is local (`x`, `a`); Add is `Ctrl+Enter`; a sign-in is a button press. No step asks for a typed confirmation.
19. **Setup is a nav tab until every step is done.** "Setup 5/8" sits in the nav; when all 8 are done or skipped it moves into Settings. Design's open call 2, taken.
20. **Proof only on scratch homes.** Nobody touches the owner's home or the owner's serve (#3019, "Rule").

## 3. The model

### 3.1 The steps

| # | Id | Title | Done when | Action (catalog id) | Computed without serve |
|---|---|---|---|---|---|
| 1 | `serve` (new) | Sindri is current | serve answers and runs the installed stamp | `RestartProcess` (new), else `StartServe` (app-local, new) | yes |
| 2 | `repo` | Add your repos | the profile names a repo | `AddRepo` | yes |
| 3 | `profile` | Profile saved | the file exists and validates | none; a failed check opens the line it names | yes |
| 4 | `agent` (new) | Agent for workflows | `workflow.agent.target` is set | none: the setup service sets it from the first installed agent | yes |
| 5 | `mcp` | Agents can use Sindri's tools | every repo is registered | `RegisterMcp` | no |
| 6 | `sources` | Connect your accounts | GitHub is connected or skipped, and each named source read once | `ConnectAccount` (new), with the account as an argument | no |
| 7 | `map` | Build the map | every repo has a current map | `RefreshContextMap` on a failed build | no |
| 8 | `first-workflow` (new) | Run hello-sindri | one hello-sindri run succeeded | `RunWorkflow` with `hello-sindri` | no |

Rules:

- **Order is fixed.** The list is sorted by this table, whatever state each step is in.
- **Skipped counts as done.** The accounts step is Skipped after Skip, and comes back as Todo when a reader of that account is turned on.
- **`sources` keeps its id.** The app reads `sources` today, so the step is retitled, not renamed. Its `Items` become one line per account: GitHub, Linear, Slack, each `not connected`, `connected as X`, `reading`, `read failed` or `expired`.
- **A new field, `Cli`.** Each step carries the exact command an agent runs for its action, from the catalog row's form, or null. The app shows it only under Details.

### 3.2 What the serve step reads

| Fact | Source | Shown as |
|---|---|---|
| running stamp, commit, built | `InstalledBuildStamps.Running()` (`Infrastructure/Setup/InstalledBuildStamps.cs`), then that folder's `build-info` | "running 80f69df, built yesterday 22:10" |
| installed stamp, commit, built | `bin/cs/current` and its `build-info` | "installed 7c81646, built today 10:31" |
| open runs | the same read as `sindri_open_runs_guard` | "2 runs are open: Nightly gate (run 14), Review loop on #3120 (run 3)" |

The app reads the step once on connect and again after each restart. It never polls (#3052).

### 3.3 The scan

`GET /sindri/setup/repos?root=~&depth=3` streams NDJSON, one line per checkout as the walk finds it: `{path, origin, slug, known, inProfile, worktrees}`. The route is read-only and writes nothing. The walk skips dot folders, `node_modules`, `bin`, `obj` and `target`. A folder whose `.git` is a file is a worktree: the walk folds it into its main checkout's `worktrees` count and never lists it as a row. `known` is true when Sindri already sees open PRs or Linear issues for that slug. The last line is `{done: true, folders: 41, repos: 6}`. Confirm sends the ticked paths through the existing `AddRepo` job, one profile write.

The same path already holds `POST /sindri/setup/repos` (`AddRepo`, `Ui/Api/Setup/SetupRoutes.cs`), so the scan is a GET on it.

### 3.4 The doctor rule for an older home

Every store doctor opens gets the guard `views.db` has (`SqliteViewReader.Ready()`, added for #2881): all migrations this build ships are applied. A store that is behind gives one fail row: "ledger.db is older than this build (schema 14, this build needs 15); fix: Restart Sindri (`sindri stop && sindri`)". Any `SqliteException` inside a doctor check becomes a fail row with the store's name, never an unhandled throw. Other rows still print; exit 1. #2954's red test comes first and names the store that throws today. Its stack trace names `SqliteViewReader.State` at line 59, which may already be guarded; P1 proves it red on `origin/main` before any fix.

## 4. The screens

All screens use the Home spec's row grammar, the design grammar piece "Empty and error states (#3019)" and the 24 px inset. While no repo is added, the top bar is reduced to the wordmark and the status pill (design's open call 1, taken). After the first Add, the full nav shows.

### 4.1 Sindri isn't running

Frame `not-running`. The page empty state: glyph, "Sindri isn't running", one sentence "Start it and Home loads your reviews, runs and messages. This takes about 10 seconds.", and **Start Sindri** (Enter), focused. Details holds "Nothing answered on 127.0.0.1:7410. To start it yourself, run: `sindri`".

The drawing says "about 2 seconds"; ux measured 9.9 s to serve up, so the sentence says 10 until F1 measures less.

| State | What shows | Primary | Focus |
|---|---|---|---|
| Not running | as above | Start Sindri (Enter) | the button |
| Starting | "Starting Sindri…", Details "Waiting for 127.0.0.1:7410 · 4.2 s" | none; the button reads "Starting…" and is off | the status line |
| Started | Home loads by itself; no toast | Home's own | Home's row 1 |
| Didn't start in 30 s | "Sindri didn't start: <first line of its error>." Details: the log path and `sindri serve` | Retry (Enter) | Retry |
| A second serve refused | "Sindri is already starting on this machine." It keeps polling | none | the status line |
| No installed `sindri` beside `sindri-ui` | "The Sindri program isn't installed next to this app." Details: `scripts/install-cs.sh --link` | none: this is a broken install, not a user step | Details |

### 4.2 Question 1 of 2: where are your repos?

Frame `setup-repos`. The page opens with the scan already running from `~`, depth 3. The header says "Where are your repos? Question 1 of 2". A status line says "Scanning ~ · 3 levels · 41 folders · 6 repos so far" with Stop scan. Rows arrive as the scan finds them, with no reorder once shown: rows append at the end.

Each row: tick box, path, slug, `worktrees: N` when N > 0, and a `known` chip. Known rows come pre-ticked; their sub-line says "Sindri already sees pull requests for this repo". Placeholder rows hold the space of the next ones while the scan runs.

The footer: "2 of 6 ticked · worktrees fold into their main checkout", **Look in another folder…** (a folder picker; the scan restarts from that root), and **Add 2 repos** (Ctrl+Enter), the primary. Dragging a folder onto the window adds it as a ticked row (GitHub Desktop's fallback, ledger 2).

| State | What shows | Primary |
|---|---|---|
| Scanning | rows arrive; first 10 in under 2 s (F10) | Add N repos, off until one is ticked |
| Done, repos found | "6 repos under ~, 3 levels deep" | Add N repos |
| Done, none found | "No git repos under ~, 3 levels deep." | Look in another folder… |
| Scan failed | inline line "Couldn't read ~: <reason>." | Retry |
| Adding | "Adding 2 repos…", the rows read-only | none |
| Add failed for one repo | that row turns red with `SND-SETUP-001` or `-002`'s sentence; the others are added | Add the rest, or Retry on that row |
| Added | the page moves to question 2 | question 2's |

### 4.3 Question 2 of 2: connect your accounts

Frame `setup-connect`. Header "Connect your accounts · Question 2 of 2", sub-line "this is the last question; Home opens next". Three rows, each with one button where row actions sit:

| Row | Sub-line | Button | What the click does |
|---|---|---|---|
| GitHub, gh login found | "pull requests, reviews and CI · gh login found: ada" | Use the gh login (ada) (Enter) | sets the credential to `command:gh auth token`; the row turns `connected as ada` |
| GitHub, no gh login | "pull requests, reviews and CI" | Sign in to GitHub | device flow: shows the 8-character code with Copy and opens github.com/login/device |
| Linear | "issues and projects · opens your browser" | Sign in to Linear | loopback OAuth with PKCE through `POST /sindri/connections/{name}/signin`; creates the connection when it is missing |
| Slack | "messages and mentions · needs a token, 3 steps" | Paste a token | opens three numbered lines: 1 "Open Slack's app page and create the app from Sindri's manifest" (Open in browser), 2 "Install the app to your workspace", 3 "Copy the user token Slack shows you, then paste it here" with a masked field and Save token |

The footer: "You can connect these later from Connections." and **Skip** (no key). After the last row connects, or on Skip, Home opens.

| State | What shows |
|---|---|
| Not connected | a todo ring, the button |
| Signing in | "Waiting for your browser…" with Cancel; the other rows still work |
| Connected | `linked` glyph, "connected as X", no button |
| Sign-in failed | "GitHub refused the sign-in: <reason>." with Retry |
| Token refused (Slack) | the field is cleared and focused; "Slack refused this token: <reason>. Paste a new one." |
| Store failed | "Couldn't store the token: <reason>. Nothing was saved." with Retry (`SND-REVIEW-025`'s rule, Reviews spec §9) |
| Expired, later | the same row with **Reconnect GitHub**, on the Setup page and as a Needs you row (#3050 item 3) |

### 4.4 The Setup page

Frame `setup-steps`. Header "Setup · 5 of 8 steps done". One RowList of §3.1's steps, the same order as `sindri setup`. Done rows are plain. The next step to do holds the one primary button. A failed row holds Retry.

Example rows at §5.0's numbers:

| Row | Sub-line | Right side |
|---|---|---|
| ✓ Sindri is current | running 7c81646, the installed one | |
| ✓ Add your repos | jthorare/agentic-workflow and ledgerly-app/exports | 2 repos |
| ✓ Profile saved | every setting read, none wrong | |
| ✓ Agent for workflows: claude-code | found installed; workflows launch their agent steps with it | |
| ✓ Agents can use Sindri's tools | registered in agentic-workflow and exports | |
| ◆ Connect your accounts | GitHub is connected as ada · Linear and Slack are not connected | 2 of 3 to do · Connect accounts (Enter) |
| ◌ Build the map | Building the map: 4,720 of 15,223 files in agentic-workflow | 31% |
| ○ Run hello-sindri | a first workflow that needs no account and no network | Run hello-sindri |

The map row has two phases (decision 14): "Indexing files: 7,000 of 15,223" while `BuildCodeIndex` runs, then "Building the map: 4,720 of 15,223 files". Progress shows at most 5 characters (`31%`); the full count sits in the sub-line (design grammar). A build that shows no new progress for 10 minutes turns into a failed row: "The map build stopped at 4,720 of 15,223 files 10 minutes ago." with Retry (ledger 4: a building state needs a timeout). *agent default (lead, 2026-11-12)* for the 10 minutes.

The page's own states:

| State | What shows | Primary |
|---|---|---|
| Loading | the last read from the local home, painted in under 100 ms (F9) | as read |
| Serve down | step 1 is "Sindri isn't running" with Start Sindri; steps 5 to 8 say "Waits for Sindri to run" | Start Sindri |
| Steps can't be read (older serve) | the shared line of decision 17 | Restart Sindri |
| Steps read failed | inline line "Setup can't be read right now." | Retry |
| All done | the tab leaves the nav; Settings holds the list | none |

### 4.5 The stale serve line

Frame `states`, panel "Stale serve under the top bar". One 32 px line under the top bar, tinted `.att`, on every page: "A newer Sindri is installed (7c81646, built today 10:31)." with **Restart Sindri**. It shows while the serve step is not Done. Details holds both stamps and `sindri stop && sindri`.

| State | What shows | Primary |
|---|---|---|
| Stale | as above | Restart Sindri |
| Runs open | "2 runs are open: Nightly gate (run 14), Review loop on #3120 (run 3). Restarting now stops them." | Restart when they finish; a secondary **Open runs** |
| Waiting for runs | "Restarts when 2 runs finish." with Cancel | none |
| Restarting | "Restarting Sindri…"; pages keep their last read and writes are off | none |
| Restarted | the line goes; one toast "Sindri restarted on 7c81646." | none |
| Didn't come back in 30 s | "Sindri stopped but didn't start again: <first line>." Details: the log path | Start Sindri |

A session never sees a Restart button: the app is the owner's. An agent reading `sindri setup` sees the serve step with `cli: "sindri stop && sindri"` and stops there.

### 4.6 Every page's empty and failed state

These follow design's `states` frame, one panel each. Each row has exactly one primary.

| Page (issue) | State | Words | Primary |
|---|---|---|---|
| Shell (#3055) | not running | §4.1 | Start Sindri |
| Shell (#3055) | not responding | "Sindri is not responding." | Refresh |
| Shell (#3055) | unreadable | the shared line | Restart Sindri |
| Home (#3056, #3113) | no profile | the setup line "Finish setting up Sindri: find your repos" | Find my repos |
| Home (#3056) | queue read failed | "What needs you can't be read right now." with the last list kept | Retry |
| Home (#3056) | truly empty | "Nothing needs you and nothing is running" | Open Workflows |
| Home (#3113) | health strip, no profile | "Profile: not set up", no red mark; the pill does not say Healthy beside it | Find my repos |
| Home (#3127) | services count | the count `sindri services` lists ("16 services") | none |
| Health (#3057, #3183) | first build running | Code index card "building", with its progress; the headline does not name it | none |
| Health (#3183) | source never read | "waiting for the first read" | none |
| Health (#3057) | different versions | the stale line of §4.5 | Restart Sindri |
| Context (#3060, #3015) | no map, nothing building | "No map of agentic-workflow yet" | Build the map |
| Context (#3015) | building | "Building the map of agentic-workflow · 4,720 / 15,223 files" | Stop building (secondary only) |
| Connections (#3059) | first read failed | "Connections can't be read right now." shown whatever `HasOthers` is | Retry |
| Projects (#3063), Reviews (#3065), Queue (#3064) | read failed | "<Page> can't be read right now." | Retry |
| Projects | Linear expired | "Your Linear sign-in expired. Projects show the last read, 3h ago." | Reconnect Linear |
| Agents (#3058) | empty | "No agent sessions running" | Open Workflows |
| Workflows (#3066) | empty | "No workflows yet. hello-sindri runs in a few seconds and needs no account, no model and no network." | Run hello-sindri |
| Jobs, Memory, Resources, Needs you (#3061, #3062) | can't be read from this Sindri | the shared line | Restart Sindri |

The inline state line is ui's `FailedReadLine` control (ui's comment on #3019): plain words, Retry, and a fix button when the step names one. The raw code and any CLI line sit behind Details.

### 4.7 Keyboard and focus

| Screen | Keys | Focus on open |
|---|---|---|
| Not running | Enter starts | Start Sindri |
| Question 1 | `j`/`k` move, `x` ticks the row, `a` ticks all, `Space` peeks the path, `/` filters, `Ctrl+Enter` adds, `Esc` stops the scan | row 1 once it arrives; the status line before |
| Question 2 | `j`/`k` move, Enter presses the focused row's button, `Ctrl+V` pastes in the Slack field, `Esc` goes back to question 1 | the first row that is not connected |
| Setup page | Home's `RowKeys`: `j`/`k`, Enter presses the row's primary, `.` opens actions | the first row that is not done |
| Stale line | `Ctrl+Shift+R` presses Restart Sindri from any page | does not take focus |

Every primary is a button with its key chip. The screen reader names each step row "Build the map, running, 31 percent, 7 of 8". A state change on the step list is announced once, by the page's live region.

## 5. Numbers and agents

### 5.0 The fixture numbers

Every frame and example uses these. A frame with other numbers is wrong.

| Fact | Value |
|---|---|
| Scan | root `~`, depth 3; first frame "41 folders · 6 repos so far"; done "6 repos" |
| Repos found | `~/repos/agentic-workflow` (jthorare/agentic-workflow, worktrees: 104, known), `~/repos/exports` (ledgerly-app/exports, worktrees: 3, known), `~/work/ledgerly-core` (ledgerly-app/core), `~/repos/dotfiles` (ada/dotfiles), `~/repos/prism-mcp-fork` (ada/prism-mcp, worktrees: 1), `~/src/old-site` (no origin) |
| Ticked | 2 of 6 (the two known); button "Add 2 repos" |
| Repo size | agentic-workflow: 15,223 files, 82,825 symbols (ux walk) |
| Index progress | "Indexing files: 7,000 of 15,223" |
| Map progress | "Building the map: 4,720 of 15,223 files", chip `31%` |
| Builds | running `80f69df`, built 2026-11-11 22:10Z; installed `7c81646`, built 2026-11-12 10:31Z |
| Open runs | 2: Nightly gate (run 14), Review loop on #3120 (run 3) |
| Accounts | gh login found as `ada`; Linear and Slack not connected; header "2 of 3 to do" after GitHub |
| Setup page | 8 steps, 5 done; nav tab "Setup 5/8" |
| Services | 16 built-in services (#3127) |
| Today's times | serve up 9.9 s, app ready +3 s, repo added t+81 s, first code index about 18 min (ux walk) |

### 5.1 Agent parity

| Action | CLI | MCP | A session may |
|---|---|---|---|
| read the steps | `sindri setup [--json]` (works with serve down) | `setup_get` (new) | yes |
| scan for repos | `sindri setup scan [--root DIR] [--depth N] [--json]` (new) | `setup_scan` (new) | yes |
| add repos | `sindri repo add <path>` (exists) | `setup_add_repo` (new) | yes (a job row, `SetupRows.cs`) |
| do every automatic step | `sindri setup --apply [--repo PATH]...` | `setup_apply` (new) | yes, without the owner's verbs |
| start serve | bare `sindri` / `sindri serve` (exist) | none (no serve to answer) | yes |
| restart serve | `sindri restart [--wait]` (new) | none | no: `SND-AUTH-002` |
| connect an account | `sindri setup connect github\|linear\|slack` (new) | none | no: `SND-AUTH-002` (the owner's browser and keychain) |
| run hello-sindri | `sindri wf run hello-sindri` (exists) | `workflow_run` | yes |

Results that hold repo text (paths, slugs, origins) are fenced as untrusted, as every `sindri_*` result is.

## 6. Errors, in words

Codes `SND-SETUP-005` to `-009` are free on `origin/main` 68ae49380a (`SindriErrors.cs:233-243` stops at `-004`); P0 confirms. The app shows the sentence and puts the code in Details. The CLI prints `code message (fix: ...)` as today.

| Code | When | Sentence | Button / command | Exit |
|---|---|---|---|---|
| `SND-API-001` | serve is down (exists) | "Sindri isn't running." | Start Sindri / `sindri` | 1 |
| `SND-BUILD-001` | serve runs another stamp (exists, `HealthSummary.cs:71`) | "A newer Sindri is installed (7c81646, built today 10:31)." | Restart Sindri / `sindri stop && sindri` | 1 |
| `SND-SETUP-001` | the folder isn't a repo's top folder (exists) | "That folder isn't the top of a git repo." | Look in another folder… / `sindri repo add <path>` | 2 |
| `SND-SETUP-002` | the repo is already in the profile (exists) | "That repo is already added." | none | 2 |
| `SND-SETUP-005` (new) | the scan root is missing or unreadable | "Couldn't read <root>: <reason>." | Look in another folder… / `sindri setup scan --root DIR` | 2 |
| `SND-SETUP-006` (new) | restart refused: runs are open | "2 runs are open: <names>. Restarting now stops them." | Restart when they finish / `sindri restart --wait` | 1 |
| `SND-SETUP-007` (new) | `--apply` with no repo named and none known | "Name a repo to add: no repo here is one Sindri already sees." | `sindri setup --apply --repo <path>` | 2 |
| `SND-SETUP-008` (new) | restart: the installed build has no `build-info` | "The installed Sindri isn't finished installing." | `scripts/install-cs.sh --link` | 2 |
| `SND-SETUP-009` (new) | serve didn't answer 30 s after a start or restart | "Sindri didn't start: <first line of its error>." | Retry / `sindri serve` | 1 |
| `SND-AUTH-002` | a session calls restart or connect (exists) | "This is for the person at the keyboard." | none | 2 |

Doctor's new fail row (§3.4) has no code of its own: it is a `DoctorCheck` row named after the store, as `build` is today (`DoctorRules.cs:90`). Its text change updates `src/Sindri/aot-smoke/expected.txt`.

## 7. Data and API

| # | Need | Lane | Notes |
|---|---|---|---|
| A1 | `SetupStepId` gains `serve`, `agent`, `first-workflow`; `SetupStep` gains `Cli`; `sources` retitled with one item per account | api | golden and API scenario row; the app reads the new ids in the same PR (`SetupStepView`) |
| A2 | `sindri setup` computes steps 1 to 4 in process when serve is down; `--apply` and `--repo` | api | shares `SetupSteps` rules, no second copy |
| A3 | `GET /sindri/setup/repos` (NDJSON stream), `sindri setup scan`, MCP `setup_scan` | api | read-only; worktree fold; `known` from the PR and Linear feeds |
| A4 | `POST /sindri/restart[?wait=true]`, `RestartProcess` catalog row (Desk), `sindri restart [--wait]` (new) | api | the open-runs read in C#, one rule with `scripts/lib/sindri-open-runs.sh`'s bash test cases |
| A5 | serve step reads `build-info` of both stamps | api | `IBuildStamps` gains the two `built=` times |
| A6 | Doctor guard per store; `SqliteException` becomes a fail row (#2954) | api | red first; aot-smoke line |
| A7 | Bare `sindri` words and exit 0 on a missing profile (#3114) | api | aot-smoke and `Usage.cs` if text changes |
| A8 | `ConnectAccount` row; `POST /sindri/setup/accounts/{github|linear|slack}`; gh login detection; device flow; sign-in that creates a missing connection | api | Desk class; one credential feeds the readers (decision 12) |
| A9 | Status counts the same services as `GET /sindri/services` (#3127) | api | `GetStatusService.cs:69` |
| C1 | Map step reads `BuildCodeIndex` too (#3112); `MapSetupReader.cs:22` watches one kind today | context | progress through `MapBuildProgress.Parse` |
| C2 | Context page reads the same running fact (#3015) | context, ui | `MapSetupReader.cs:71` already has it |
| C3 | Doctor's no-map row says what it waits on (#3216) | context | `MapReader.cs:87`, `CheckStructureService.cs:49`; aot-smoke |
| C4 | Six services read a null `context:` as defaults (#3131) | context | one helper, one test per service |
| C5 | Embeddings: split long bodies, one 400 never parks the layer, doctor names the real cause (#3128) | context | `OllamaEmbedder.Embed`, `CodeIndexDoctor.cs:168` |
| E1 | Setup service sets `workflow.agent.target` from the first agent `launcher:agents` finds | engine | the `agent` step; a line, not a question |
| E2 | `hello-sindri` template: manual, code, notify, `create_item` in the first repo | engine | needs no map, account, model or network; a test-run case in CI (Workflows spec W4) |
| U1 | Desktop host starts serve (decision 2) | ui | `Aw.Sindri.Ui.Desktop`, never `Aw.Sindri.Ui` |
| U2 | `FailedReadLine` and the page empty state; `OpensInApp` true for every action with a route | ui | `ShellViewModel.Setup.cs:20` |

## 8. The timed run

`scripts/context-first-run.sh` writes the profile itself in step 2, so it never walks what a new user walks. A new flag `up --as-new-user` skips steps 2 and 3, then:

1. starts serve with bare `sindri --headless` and records the seconds;
2. runs `sindri setup --apply --repo <checkout>` and records the seconds to the `repo` step Done;
3. runs `sindri wf run hello-sindri` and records the seconds to its run row;
4. polls Home's read route until one row names the item hello-sindri made in this repo;
5. prints one line per step and a total, `first-run total 142 s`, and exits 1 when the total is over 180 s.

CI runs it in the scratch-home job and stores the line, so a regression shows as a number going up (#3051). The map keeps building after the run ends; the run does not wait for it. The owner's box runs the app walk with `aw-ui-driver` as well, which adds the two questions.

## 9. Goals and non-goals

Goals:

- Fresh home to Home with a real row in under 3 minutes, 2 questions at most (F1, F2).
- Every state with one button (F3).
- An agent can set up a box through the CLI, except restart and sign-in (F8).

Non-goals:

- Import of other tools' settings or history (ledger 6: skip).
- A Slack one-click sign-in. It needs an https relay; it waits on #2979.
- Upgrades beyond the rule below. A new install migrates the home on serve start; each thing it can't migrate becomes a `setup` Needs you row with its fix as a button, through `SetupNotices` (`Features/GetNeedsYou/SetupNotices.cs`). Known cases: an older store (§3.4) and Onyx leftovers (`sindri onyx down --purge`, the owner's verb).
- A setup wizard window. The questions are pages of the one app.

## 10. What happens to today's views

| Today | After |
|---|---|
| `SetupRepoForm` path field | the streamed scan list; the path field stays behind "Look in another folder…" |
| `Labels.Setup*` step copy (`Labels.Setup.cs:41`, `:62`, `:139`) | dropped; the route's text shows |
| `OpensInApp` always false | true for every action with a route |
| "Start it from a terminal:" card | the Not running page with Start Sindri |
| Doctor's `build` warn row | stays; the app adds the stale line |
| `sources` step "needs a token" with `sindri index connect` | the accounts step with one button per account |
| Model, label and surface choices on the setup page (#3115) | move out of setup until each is a toggle |

## 11. Gaps found

1. **The drawings show build numbers that don't exist.** Stamps are hashes (`install-cs.sh:126`). The stale line uses the commit and the `built=` time.
2. **Three GitHub credentials exist.** The `github` connection (OAuth), the `github` index source (`sindri index connect github`, a read-only token in the keychain) and `review.credential` (`env:` or `command:`, `PrReviewRules.cs:79`). Decision 12 points all three at one entry. P0 must confirm whether the index source can take a `command:` pointer; if it can't, the accounts step stores the token once and points the other two at that entry.
3. **The device flow needs a GitHub OAuth app and Linear needs one too.** Meta decided the owner registers both once and the client ids ship in the build. Until then the accounts step reads the client id from the profile.
4. **`--apply` picks repos when none is named.** Ticking "known" repos without asking is a guess at the owner's intent. It only runs when an agent was asked to set up and named no repo.
5. **#2954's throw may already be guarded.** The api comment says `views.db` has a guard since #2881, while the issue's stack names `SqliteViewReader.State`. The red test decides.
6. **The 10-minute "stopped" timeout for the map** has no measured source; the first code index ran 18 minutes with steady progress lines, so the rule counts progress, not total time.

## 12. Proof

Proof is the app run on scratch homes with `aw-ui-driver`, not tests alone.

1. **The walk, timed.** Fresh home, bare `sindri`, the two questions, Home. Shots of each screen before and after. This is F1 and F2.
2. **The CLI walk.** `sindri setup` with serve down, `--apply`, then `sindri setup` again (F5, F8).
3. **State matrix.** One frames test per row of §4.6 (F3).
4. **Stale serve.** Two builds in a scratch `bin/cs`; Restart with 0 and with 2 open runs (F6).
5. **Older home.** A home written by an older build, doctor run (F7).
6. **Adversarial design review** by the ideas lane on the shots, judged against inspora.design (owner rule, 2026-11-10).

## 13. Phases and parallel plan (for /specToProvenPR)

Phases are the PRs. Every lane has work from day one; each phase lands its red and stubs first.

| Phase | What | Lane | Depends on | First red test |
|---|---|---|---|---|
| P0 | Contract: new step ids and `Cli`, the scan line record, `POST /sindri/restart` and `RestartProcess` row, `ConnectAccount` row, `SND-SETUP-005` to `-009`, goldens, all as stubs | api | none | `Setup_lists_eight_steps_in_order` |
| P1 | #2954 doctor on an older home; #3114 bare `sindri` words and exit; aot-smoke lines | api | none (start now) | `Doctor_prints_a_fail_row_for_a_ledger_db_one_migration_behind` |
| P2 | Stale serve: `build-info` read, serve step, restart route with the open-runs guard (#3052) | api | P0 | `Restart_refuses_and_names_two_open_runs` |
| P3 | Headless `sindri setup` and `--apply` | api | P0 | `Setup_with_serve_down_reports_not_running_first` |
| P4 | Scan route and `setup scan` (#3049) | api | P0 | `Scan_folds_a_worktree_into_its_main_checkout` |
| P5 | Accounts step: gh login, device flow, Linear sign-in that creates the connection, Slack paste, one credential (#3050) | api | P0; gap 2 answered | `Gh_login_sets_review_credential_to_the_gh_command` |
| P6 | Map step follows `BuildCodeIndex` (#3112), Context page building state (#3015), doctor text (#3216) | context | none (start now) | `Map_step_is_running_while_the_first_code_index_builds` |
| P7 | #3131 six services on a default profile; #3128 embeddings | context | none (start now) | `Context_sync_reads_default_sources_on_a_profile_with_no_context_section` |
| P8 | Agent target set by the setup service; `hello-sindri` template and its CI case | engine | P0 for the step ids | `Setup_sets_agent_target_from_the_first_installed_agent` |
| P9 | `FailedReadLine`, page empty state, `OpensInApp`; then #3056, #3055, #3063, #3064, #3065, #3066, #3062, #3059, #3061, #3057, #3058, #3060, #3113, #3183 | ui | X1 for visuals; structure starts now | `Failed_queue_read_never_shows_nothing_running` |
| P10 | Start Sindri in the Desktop host; the Not running page | ui | none | `Start_sindri_launches_the_installed_binary_once` |
| P11 | Question 1 and question 2 screens; Setup page on the step list; the stale line; #3054, #3115 | ui | P0 stubs; P2, P4, P5 for live data | `Setup_page_rows_match_the_route_steps` |
| P12 | Timed run `--as-new-user` and its CI job (#3051) | tools, ux | P3, P8 | bash test with a stub `sindri` |
| X1 | Design: redraw the frames of §14 at §5.0's numbers | design | none | |
| X2 | UX: the walk at each phase's end, timed; one issue `Part of #3019` per place it had to read a doc | ux | each phase | |
| X3 | QA: one issue per page that still has a state without a button; F1 to F10 | qa | each phase | |
| X4 | Perf: F9, F10 in the real app | perf | P4, P11 | |

Day one: api P0 and P1; context P6 and P7; engine P8 on the P0 stubs; ui P9 and P10; design X1; ux X2 baseline walk. Nothing waits on a dependency PR to start its red.

Gate notes: P0, P2, P3, P4 and P5 add catalog rows, action ids and API goldens, so each gate runs the Sindri integration lane, `EndpointCqrsTests` for the new POST routes, and `Aw.Sindri.Ui.Tests`. P1 and P6 change doctor text, so they update `src/Sindri/aot-smoke/expected.txt` and run aot-smoke. A `Usage.cs` change runs the whole `Aw.Sindri.Tests` unit lane. P10 adds a `ProjectReference` or process start in the Desktop host only. This spec adds no environment variable; `scripts/check-env-example.sh` still runs.

## 14. Frames for design

Redraw at §5.0's numbers. Each frame shows every state row of its section.

1. `not-running`: Not running, Starting, Didn't start, Already starting (§4.1). The sentence says "about 10 seconds".
2. `setup-repos`: Scanning, Done, None found, Scan failed, Adding, one row failed (§4.2). Slugs `ada/...` for the made-up repos.
3. `setup-connect`: each row's states, gh login found and not found, the device code, the Slack paste open (§4.3).
4. `setup-steps`: 8 steps, 5 done; the map in both phases; serve down; the all-done move to Settings (§4.4).
5. `stale-line`: Stale, Runs open, Waiting, Restarting, Restarted, Didn't come back (§4.5). Commits and times, no build numbers.
6. `states`: every row of §4.6, with the "Create my profile" panel replaced by "Find my repos".

## 15. Open questions for the owner

1. **Register the GitHub and Linear OAuth apps once?** Meta decided you do this one time and the client ids ship in the build. It's about 2 minutes per app. I'd pick yes; the other pick is the device flow only for GitHub and a pasted Linear key, which breaks "one click".
2. **Restart with open agent runs: wait, or offer to stop them?** I'd pick wait ("Restart when they finish") with no stop button, because a stopped run loses an agent's work. The other pick adds "Stop them and restart" as a second button.
3. **`sindri setup --apply` with no `--repo`: add the pre-ticked repos, or refuse?** I'd pick add the pre-ticked ones, since an agent was told to set up. The other pick always refuses with `SND-SETUP-007` until a repo is named.
4. **Slack in question 2, or after setup?** I'd keep it in question 2 as the guided paste, with Skip beside it. The other pick drops Slack from first run until the #2979 probe lands.
