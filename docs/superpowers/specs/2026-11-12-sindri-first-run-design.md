# Sindri's first run: from a fresh box to a working Sindri without reading a doc

Status: draft, after /autoplan round 1 (ideas lane, 2026-11-12). Serves #3019 (first run) under #2978 (one app that replaces the others). It builds the "What we take" lines of the prior-art ledger `docs/sindri/prior-art/first-run.md` (#2989).
It amends the setup-once spec (`docs/superpowers/specs/2026-10-19-sindri-setup-once-design.md`) where §2 says so, and sits beside the Home, Workflows and Reviews specs of 2026-11-12, reusing their row grammar and keys.
Reviews: 3 /autoplan rounds plus the screenshot review, before ready (no owner read).

Code facts are read at `origin/main` 9483c8d8c0. "Ledger N" is problem N of the ledger. Every call carries its source: *owner* (with where), *meta decision* (meta decides direction for the owner, comment on #3019 of 2026-11-12 08:17Z, cited as "meta, #3019"), *design call* (design's post on #3019 of 08:24Z), or *agent default (lead, 2026-11-12)*. §2.1 lists every number with its source.

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
- **Stamps are hashes, not numbers.** A stamp is 16 hex characters (`scripts/install-cs.sh:126`). Each build folder holds `build-info` with `commit=` and `built=` (`scripts/install-cs.sh:384`). So "newer" is a compare of the two `built=` times, and the drawings' build numbers have no source: every frame shows the commit and the `built=` time instead ("7c81646, built today 10:31").
- **A Desk row does not refuse a session, and an agent the owner started is the owner.** `Authorization.cs` (`Core/Commands/Authorization.cs:25`) lets `Terminal`, `App`, `Session` and `Launched` run a Desk row such as `StopProcess`. The owner-only verbs (`profile set`) check the per-start human token instead (`CallerHeaders.cs:36-50`): a CLI that can read it is a human caller "whatever the terminal or the agent around it", and only a session Sindri launched never gets it (`AW_SINDRI_LAUNCH`). So the line on main between owner and agent is launched or not, by design (agent-first PR 2).
- **No account step.** GitHub, Linear and Slack connect from the Connections page with a pasted key or a client id the owner registers (`config/connections/presets/github.json`, `linear.json`). Slack refuses a loopback redirect (`slack.json`, `loopbackAllowed: false`).

The job: **open the app on a fresh box, answer at most 2 questions (which accounts, which repos), and land on Home with a real row. Every state on the way has one button. An agent can do the same setup through `sindri setup`, except the owner's own verbs.**

## 1. The bar

| Step of first run | Ledger | Best tool today | What we take | Here |
|---|---|---|---|---|
| Start without commands | 1 | Tailscale `up`, then the browser | the app is the command; a stopped serve is a screen with Start Sindri | §4.1 |
| Connect accounts | 3 | Linear's integration page (six clicks, an admin) | one button per account, state on the row | §4.2 |
| Pick repos | 2 | GitHub Desktop drag, Conductor buttons | a streamed scan with ticks; drag a folder as the fallback | §4.3 |
| See the first map build | 4 | Cursor's progress view (came and went), Docker's Starting page (sticks) | n/N files, an age, a way out, a timeout that turns into a button | §4.4 |
| Fix a stale thing | 5 | Docker's Troubleshoot icon, Tailscale's Reauthorize | a fixed line under the top bar, Restart Sindri; Reconnect on an expired account | §4.5 |
| Not start in an empty room | 6 | Zed's import, with what it skips | skip import, and say what is not imported; a zero-setup workflow makes the first row | §4.4, §9 |
| Land on a useful screen | 7 | Superhuman's Dashboard | Home with a real row, timed | §8 |

**The happy path is 2 actions:** open the app, then press Add 2 repos. With a gh login on the box, the app starts serve itself, connects GitHub through gh and starts hello-sindri itself (decisions 2a, 13, 19). *meta (UX pass, 2026-11-12)*.

**The bar in one line:** from no home at all to Home with a row in under 3 minutes, with at most 2 questions, no YAML, no CLI line on screen, and no doc opened; and, with GitHub connected, a row from the owner's own pull requests within 1 more minute. The map can still be building.

Two numbers, because the first one alone can pass on a box where Home shows nothing the owner cares about (round 1, product C1):

- **Time to Home** (F1): Home shows the hello-sindri run row. CI measures it.
- **Time to own data** (F1b): Home shows a row from the owner's GitHub (a PR waiting on review, or one of the owner's own). The owner's box measures it in the app walk, because CI holds no GitHub account.

Done-checks. Each is a test or a timed run in the real app (`aw-ui-driver`) on a scratch home, never the owner's home or the owner's serve on 7410.

| # | Check | Measured as |
|---|---|---|
| F1 | **Time to Home.** Fresh home to Home showing the hello-sindri run row and the item it made in the first repo. | Seconds per step and in total, printed by the timed run (§8) and recorded in CI. Target under 180 s. Today: never, because no step leads there. |
| F1b | **Time to own data.** Same walk with the gh login (question 1 skipped, decision 13); Home shows a row from the owner's pull requests. | The app walk's `first-run own-data N s` line. Target: under 60 s after F1, a guess to confirm on the first walk. The row comes from the Reviews queue feed, which reads `context.sources.github` for the added repos; it needs no map. |
| F1c | **Accounts leg.** With a gh login: 0 actions, question 1 never shows; seconds from app open to question 2 shown. With no gh login: seconds from question 1 open to GitHub connected (device flow). | The app walk's `first-run accounts N s` line. Targets: under 5 s with a gh login, a guess to confirm on the first walk, *agent default (lead, 2026-11-12)*; under 60 s with the device flow, the Workflows spec's "Connect GitHub under 1 minute". |
| F2 | **At most two questions, two actions.** The walk asks for accounts (only with no gh login, or after Change), then repos, and nothing else. | `aw-ui-driver` counts screens that take input before Home: at most 2, and 1 with a gh login; it counts actions on the gh-login path: 2 (open the app, Add). Skip for now counts as an answer. |
| F3 | **One button per state.** Every state in §4 has exactly one primary button and no CLI line or raw code outside Details. | One frames test per state row; qa's sweep of #3054 to #3066 closes each issue. |
| F4 | **Same steps everywhere.** `sindri setup --json` and the Setup page list the same ids in the same order with the same titles. | One golden for the route; one Ui test compares the page's rows to the route's steps. |
| F5 | **Setup without serve.** With serve down, `sindri setup` prints the steps it can read locally and exits 1 with "Sindri is not running" as the first step. | A CLI test on a scratch home with no `control.json`. |
| F6 | **A stale serve fixes itself on one click.** | Install a second build, click Restart Sindri: serve runs the installed stamp within 15 s; open runs are named and kept; the old serve's pid comes from the 202 body. |
| F7 | **Doctor never throws on an older home.** | A home whose `views.db` and `ledger.db` an older build wrote: doctor prints every row, one fail row per store that is behind, exit 1, no unhandled exception (#2954). |
| F8 | **An agent can set up.** An agent session runs `sindri setup --apply --repo <path>` and reaches every step but the browser and paste sign-ins; `sindri setup` then exits 3 ("only the person at the keyboard can finish"). | A CLI test as an owner-started session (it may restart and use the gh login) and as a launched session (`sindri restart`, `sindri setup connect` and `sindri setup scan` exit 2 with `SND-AUTH-002`). |
| F9 | **First paint.** The first window paints from the local home in under 100 ms, before serve answers. | Window open to first frame, measured by perf. |
| F10 | **Scan speed.** The first 10 repos show in under 2 s. | Timed on this box's home and on a scratch home with 200 folders. |

## 2. Decisions

1. **The app is the first command.** Bare `sindri` starts serve and opens the app, as today. With no profile it says `profile   not set up yet: finish setup in the app (or 'sindri setup' to see the steps)` and `ready     running; setup isn't finished`, and exits 0 (#3114). Doctor and `profile set` keep their `profile init` fix line. The `then and` typo in `YamlProfileSource.cs:17` and `YamlProfileIndexWriter.cs:136` goes.
2. **The app starts serve.** "Start Sindri" and the second half of a restart run the installed build, `$AW_STATE_DIR/bin/cs/current/sindri`, never one from `PATH`, as `sindri --headless`, detached. When there is no `current` link (a dev run), they run the `sindri` beside `sindri-ui`. So an app older than the install still starts the installed build, and the stale line does not come back. Its stdout and stderr go to `<home>/logs/serve.log`, the file `ServeLauncher` already appends to (`Infrastructure/Control/ServeLauncher.cs:11`). The app polls `GET /sindri/status` for up to 30 s. serve's own single-serve guard refuses a second one. This is the Reviews spec's §5.7 "Start Sindri" rule, owned once here. *agent default (lead, 2026-11-12)*: the Desktop host owns the process start, not `Aw.Sindri.Ui`, so the views stay free of `Process`.
2a. **The app starts serve by itself when it opens and serve is down.** It runs the same start as decision 2, with no click, and shows the Starting state while it waits. The Not running page shows only after a start fails: Didn't start, Already starting, Still stopping, or a broken install (§4.1). If serve goes away later in a session, the shell's Not running card offers Start Sindri, as #3055 plans. *meta (UX pass, 2026-11-12)* (pick a).
3. **Two questions, no third.** Question 1 is accounts (§4.2), question 2 is repos (§4.3). The agent target is a done line, not a question (engine plan on #3019). Model, label and surface choices leave setup until each is a toggle (#3115 item 4). *meta, #3019* for the cap of 2 (the direction said "3 at most" and meta's plan cut it to 2).
4. **Accounts come first, so the repo scan can pre-tick.** A repo is `known` when the connected GitHub account has an open PR, a review request or a push in the last 30 days on that slug. That needs the account, so the account question comes first. With no account connected (Skip), no row is pre-ticked and the Add button stays off until the owner ticks one. This reverses the drawings' order. *agent default (lead, 2026-11-12)*; round 1 product C2, arch F2, devex 2.
5. **The first write creates the profile.** Whichever question writes first (a credential key from question 1, or the first repo from question 2) creates `profile.yaml` from `DefaultProfile` (`Core/Setup/DefaultProfile.cs`) when it is missing. So no screen asks to create a profile; the drawings' "Create my profile" panel goes, and its button is "Connect your accounts". `profile init` stays a CLI verb for agents. *agent default (lead, 2026-11-12)*.
6. **One step list, from Core.** `SetupStep` stays the model. The app renders the steps the route sends and adds none of its own, so `Labels.Setup*` step copy is dropped where the route sends the text (ui plan item 1). `sindri setup` renders the same list.
7. **Eight steps, in this order:** `serve`, `sources`, `repo`, `profile`, `agent`, `mcp`, `map`, `first-workflow` (§3.1). The drawings' 11 rows fold into these. "Repos found" and "Repos added" are one `repo` step. "Profile saved" and "Profile checked" are one `profile` step. The failed first read of pull requests and docs is an item of `sources`. *agent default (lead, 2026-11-12)*.
8. **`sindri setup` works without serve.** When serve is down, the CLI computes `serve`, `repo`, `profile` and `agent` in process from the same `SetupSteps` rules and reports the other four as "Waits for Sindri to run". `--json` keeps its shape. *meta, #3019* (answer 3).
9. **`sindri setup` has an exit for "only the person at the keyboard can finish".** Exit 0: every step is done or skipped. Exit 1: a step is to do, running or failed, or serve is down; an agent re-reads after the step's job or the setup service's pass ends. Exit 3 (new): every step that is not done waits on the person: a browser sign-in, a device code, a pasted token, or a verb this caller is refused. An agent stops on 3 and tells the owner; it does not loop. Such a step has `State = needs-you` and `Cli = null`. *agent default (lead, 2026-11-12)*; round 1 devex 4, round 2 devex 1.
10. **`sindri setup --apply` does what needs no human.** It starts serve when it is down, adds each repo named by `--repo` through the `AddRepo` job, and waits for those jobs to end (seconds), not for onboarding or the code index. A repo already in the profile counts as `already`, not as an error. The setup service then sets the agent target, creates and activates hello-sindri, and registers MCP in each repo on its own (`service:setup`, setup-once SO-22). `--apply` runs that pass at once (`sindri services run setup`, which exists) and waits for it, up to `--timeout` (default 120 s), so the next `sindri setup` is current and never waits for the 60 s tick. `--apply` never signs in and never restarts. With no `--repo`, it adds the pre-ticked (`known`) repos and prints each one it added; with none known it refuses with `SND-SETUP-010`. *meta, #3019* (answer 3) for `--apply`; *meta (§15 pick 3, 2026-11-12)* for the no-`--repo` default; the rest *agent default (lead, 2026-11-12)*.
11. **Restart is the owner's, the app holds the wait, and serve keeps no restart state.** `POST /sindri/restart` reads the open set first. The open set is every launched agent session (the rule of `scripts/lib/sindri-open-runs.sh`), every workflow run that is `running` or `parked`, and every job that is `running`; the C# read is a superset of the bash guard, so a workflow run in a code node or an approval wait is never stopped. With none open, serve refuses any new run or job start (503, in memory) for the seconds until it stops, then stops through the `/sindri/stop` path and answers 202 with `StopProcessResponse`, whose `Pid` names the old serve. The caller (app or CLI) takes the pid from that body, not from `control.json`, which `StopProcessService` removes before it releases the lease. It waits until that pid has exited and the port stops answering, up to 30 s, then starts the installed build (decision 2), so the new serve never meets the old one's single-serve guard. If the old pid is still alive after 30 s, the caller does not start a second serve: the app shows "Sindri is still stopping" with Start Sindri, and the CLI exits 1 with `SND-SETUP-012`. With runs open, it refuses with `SND-SETUP-009` and names them. A restart with runs open only waits: no button stops them. Each run in the line is a link to its run page, where Stop already is, so a stuck run is not a dead end (*meta, §15 pick 2, 2026-11-12*). "Restart when they finish" is the app's: it fixes the set of runs open at the click, reads `GET /sindri/runs/open` every 5 s while that state shows, and sends a plain restart once that set has ended. A run that starts during the wait is never stopped either: the final restart refuses again with `SND-SETUP-009`, and the line names the new run and waits for it (§4.5). Close the app during the wait, and the wait ends and serve keeps running the old build. The CLI does the same with `sindri restart --wait [--timeout 30m]`: it fixes the set at the call, polls every 5 s, and on a `SND-SETUP-009` at the final call prints the new run's name and keeps waiting until `--timeout`. Closing the app between the 202 and the new start leaves serve down; the next app start or a bare `sindri` starts it (the Not running page). The route checks the per-start human token (`CallerHeaders`, as `profile set`), so a session Sindri launched gets `SND-AUTH-002`; an agent the owner started may restart, because on main it is the owner's caller. *meta, #3019* (answer 1: serve only stops itself); the rest *agent default (lead, 2026-11-12)*; round 1 arch F3, devex 1, security 1.
12. **"Newer" comes from `build-info`.** The serve step compares the `built=` times of the running and the installed build. Installed later: "A newer Sindri is installed (7c81646, built today 10:31)". Installed earlier: "A different Sindri is installed". Commits show as 7 characters; stamps never show outside Details. A process with no stamp (dotnet run, a test) has no serve finding, as `StaleBuild.Is` says today. *agent default (lead, 2026-11-12)*.
13. **With a gh login, question 1 never shows.** When `gh auth status` passes, the app connects GitHub through gh with no question: it sets `review.credential` to `command:gh auth token`, so no token is copied or stored (`command:` credentials run as argv with no shell, `ForgeCredentialSpec`). Question 2's header then carries one line, "Signed in as ada through gh · Change", with "uses the gh login's scopes" under Details. Change opens question 1. Question 1 shows only with no gh login, or after Change. There, the GitHub button is "Sign in to GitHub": the device flow of Sindri's own OAuth app, so the box holds no client secret. A work org that blocks the app gets the fallback row "Paste a GitHub token" (a fine-grained token). On a gh-login box, Linear and Slack are on the Accounts page (decision 28). *meta (UX pass, 2026-11-12)* (pick b); *meta, #3019* (answer 6), *meta (§15 pick 1, 2026-11-12)*.
14. **One GitHub account feeds every reader.** The accounts step writes the GitHub credential once and points each reader at it: `review.credential`, the `github` index source and the `github` connection. Only an owner caller (human token) writes these keys. *agent default (lead, 2026-11-12)*; §11 gap 2 says what P0 must confirm.
15. **Linear signs in through the browser; Slack is a guided paste.** Linear uses OAuth with PKCE, the existing `state` check and loopback sign-in (`POST /sindri/connections/{name}/signin`), and the client id of Sindri's own OAuth app. A work org that blocks the app gets the same "Paste a token" fallback row (a Linear personal API key). Slack is a guided token paste: three numbered lines and a Paste field, until the #2979 probe says which token Messages needs. Slack is never required: Continue skips it in one click, and the `sources` step is Done without it (*meta, §15 pick 4, 2026-11-12*). *meta, #3019* (answer 6).
15a. **Sindri owns one OAuth app per provider; the owner registers each once.** The client id ships in the build as the default of a profile key, `setup.oauth.github` and `setup.oauth.linear` on the existing `setup` section, so the owner can swap in an app of their own. Neither key holds a secret. This sign-in is what makes the repo pre-tick work on a fresh box: with accounts first (decision 4), a one-click GitHub sign-in lets question 2 read the repos the owner worked on, even on a box with no gh login. *meta (§15 pick 1, 2026-11-12)*; the key names are *agent default (lead, 2026-11-12)*. The one-time owner step, per provider:

| Provider | Where | App name | Callback | Flow | Scopes |
|---|---|---|---|---|---|
| GitHub | github.com, Settings, Developer settings, OAuth Apps, New OAuth App | Sindri | `http://127.0.0.1` (GitHub requires one; the device flow never calls it) | "Enable Device Flow" ticked; no client secret is generated for Sindri's use | `repo`, `read:org` (the preset's `defaultScopes`, `config/connections/presets/github.json`) |
| Linear | linear.app, Settings, API, OAuth applications, New | Sindri | `http://127.0.0.1` (the loopback sign-in adds the port) | public client with PKCE | `read`, `write` (the preset's `defaultScopes`, `config/connections/presets/linear.json`) |

**Why GitHub `repo`, and why it is the smallest.** An OAuth app has no read-only scope for private repos: `public_repo` covers public repos only, and `repo` is the one scope that reads a private repo's PRs, issues and files. The first run needs that read (the index source and the `known` pre-tick on private repos), and the Reviews page also writes with the same account: it posts reviews (`POST .../pulls/{n}/reviews`, `GitHubForge.Review.cs:144`), replies and merges (`GitHubForge.Write.cs:122`). `read:org` is needed to see a review requested of a team the owner is in. So `repo` plus `read:org` is the floor for an OAuth app. A smaller grant needs a GitHub App or a fine-grained token, not an OAuth app. **The fine-grained set** is: Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read. It is the Reviews spec's §8 table (merge and update branch write Contents; the CI panel reads Checks) plus Issues read for the index source; Metadata read is always on. One constant in Core holds this list, and the paste fallback's copy, `SND-SETUP-013` and `-014` read it, so the screen and the errors never drift from it. A GitHub App also supports the device flow, but an org admin must install it on each org, which breaks one click on a work org. So v1 keeps the OAuth app, and the paste fallback takes a fine-grained token with the fine-grained set; a Sindri GitHub App is listed for after v1 (gap 9). The gh login carries whatever scopes the owner gave `gh`, usually `repo`, `read:org` and `gist`; the row says so (decision 13). *agent default (lead, 2026-11-12)*, checked by the round 2 security addendum.

The owner pastes each client id into the build's defaults once (P5 names the file); until then the profile key holds it.

16. **An account an admin must approve has its own state.** A Slack app from a manifest in a company workspace, and a GitHub or Linear org that gates OAuth apps, can need an admin. The row then says "Waiting on your workspace admin to approve Sindri." with Check again, and question 1 still finishes with Continue. *agent default (lead, 2026-11-12)*; round 1 product C4.
17. **No secret leaves the secret store or memory.** One rule for every secret in the flow: a pasted GitHub, Linear or Slack token, a device code, a PKCE verifier, and every access or refresh token. Each is write-only: it goes straight to `ISecretWriter` (`Infrastructure/Secrets/KeychainSecretWriter.cs`) or stays in memory for one sign-in, and is dropped after the answer. None reaches the ledger, the command audit, a job row, error text, `--json`, telemetry, a log or a command line. The CLI reads a token from stdin or `--token-env NAME`, as `index connect` does. One test per paste path (GitHub, Linear, Slack) checks the ledger, the job rows and the log after a save. *agent default (lead, 2026-11-12)*; round 1 security 2, round 2 security 1.
18. **The map step follows the first code index.** It is Running from the moment `BuildCodeIndex` starts, with that job's progress ("Indexing files: 7,000 of 15,223"), then Running with the map's progress (#3112). It never says "Sindri builds it on its own" while nothing is building.
19. **hello-sindri starts by itself when the first repo is added.** The setup service creates the `hello-sindri` workflow from its template, activates it, and runs it once, when the profile first names a repo, as it sets the agent target. It needs no map, no account, no model and no network (engine plan on #3019). Home and the Setup row show it running, then its result; **Run again** stays as a secondary rerun. A ledger event marks it made once (E2). The owner can dismiss the step (§3.1). *meta (UX pass, 2026-11-12)* (pick c); round 1 arch F4, design 1.
20. **Failed is not empty.** A failed read never shows the empty state. It shows the inline state line with Retry and keeps the last list under it (design's grammar piece on `pr-assets/grammar-2978`, c73f521779).
21. **One shared line for an older serve.** "The X list can't be read from this Sindri. It is older than the app." with Restart Sindri, on every page (qa's pattern across 8 pages).
22. **One primary per screen, picked by order.** Where several rows could hold a primary (the Setup page, question 1), the first row in order that is not done and has a button holds it; every other button on screen is secondary. A running row with no button is passed over. *agent default (lead, 2026-11-12)*; round 1 design 3 and 4, round 2 design 2.
23. **Bare letters never write.** Ticking a repo is local (`x`, `a`); Add is `Ctrl+Enter`; a sign-in is a button press. No step asks for a typed confirmation.
24. **Setup is a nav tab until every step is done or skipped.** "Setup 6/8" sits in the nav; when all 8 are done or skipped it moves into Settings. *design call* 2.
25. **The first-run agent path is the CLI.** The `sindri` MCP server is registered only after the profile names a repo, so a session on a fresh box has no MCP tools yet. The only new MCP tool is `setup_get`, for sessions after setup. *agent default (lead, 2026-11-12)*; round 1 devex 6.
26. **Proof only on scratch homes.** Nobody touches the owner's home or the owner's serve (#3019, "Rule").
27. **One profile writer gate.** First run writes the profile from several places within seconds: the account route, `AddRepo`, the skip route and the setup service. Every in-process write goes through one shared gate (one `SemaphoreSlim` in a `ProfileWriteGate` singleton, replacing each writer's own `lock`). Each writer also keeps main's re-read check (`SND-PROFILE-002` when the file "changed twice while writing") and retries once, which covers `profile set` from the CLI process. An older serve reads the new `setup` keys as an unreadable profile (`SetupSettings` disallows unknown members); that is a stale serve, and the app's stale line with Restart Sindri is its fix. *agent default (lead, 2026-11-12)*; round 2 arch M5.
28. **Question 1 outside first run is the Accounts page.** Opened from the Setup page, a Reconnect or a Needs you row, the same screen shows as "Accounts" with no "Question 1 of 2", the sub-line "Connect or reconnect the accounts Sindri reads", and a footer button **Done** that returns to the page it came from and never opens question 2. The primary depends on the entry: from a Reconnect or a Needs you row, that row's button is the primary and takes focus, and once it connects, Done becomes the primary and takes focus; from the Setup page, the first row not connected holds the primary and Done is secondary until no required row is left to do: once GitHub is connected, Done is the primary, because Linear and Slack are optional (decision 15). Esc presses Done. Question 2 shows only in first run, while the profile names no repo. *agent default (lead, 2026-11-12)*; round 2 design 3, round 3 design 1.
29. **An agent the owner started acts for the owner.** No new caller class: the setup gates refuse only a session Sindri launched, as main does. They guard against honest mistakes, not a hostile agent, and refusing owner-started agents would break "set it up for me". *owner via lead (2026-11-12)*.
30. **Start routing: the app opens on the first question not done.** On every start, while the profile names no repo, the app opens on question 1 until GitHub is connected or skipped (with a gh login it connects by itself, decision 13), then on question 2. Nothing else shows until then. Resume after a quit: the app reads the state again, never a saved screen. A quit during a device-code wait reopens question 1 with GitHub "not connected" (the code lived in memory only, decision 17). A quit during Adding reopens question 2 in its Adding state when the `AddRepo` job still runs, or Home when it has finished and the profile names the repo. Skip for now on question 2 holds for that app session; the next start opens question 2 again, because a repo is the one thing first run needs. *agent default (lead, 2026-11-12)*; meta UX pass MUST 1.
31. **Every dead end gets "Skip for now".** A first-run screen with no way forward gets a secondary **Skip for now**: question 1 before GitHub connects (it was "Skip"), and question 2 when no repo is added (none found, scan failed, every add failed). It opens the next question, or Home with the "add your repos" setup line. *meta (UX pass, 2026-11-12)* (pick d).
32. **Re-adding repos is the Repos page.** The done "Add your repos" row on the Setup page has a secondary **Add repos**. It opens question 2 as a "Repos" page, the pattern of decision 28: no "Question 2 of 2", repos already added are hidden, Add N repos is the primary once one is ticked, else **Done** is the primary; Done and Esc return to the page it came from. *agent default (lead, 2026-11-12)*; meta UX pass MUST 5.
33. **One top bar per stage.** While the profile names no repo, every screen (Not running, the questions) shows the reduced bar: the wordmark and the status pill, labelled "Sindri running" rather than "Connected". After the first Add, every screen shows the full bar: nav with counts, Ctrl+K search, and "Setup 6/8". *agent default (lead, 2026-11-12)*; meta UX pass mismatches 2 to 4, SHOULD 2.

### 2.1 Every number, with its source

| Number or rule | Where | Source |
|---|---|---|
| at most 2 questions | decision 3, F2 | meta, #3019 (the direction says 3; meta's plan cut it to 2) |
| under 60 s for own data and for accounts | F1b, F1c | agent default (lead, 2026-11-12), matching the Workflows spec's "Connect GitHub under 1 minute" |
| scan root `~`, depth 3, skip dot folders and `node_modules` | §3.3 | meta, #3019 (answer 2) |
| also skip `bin`, `obj`, `target`; fold worktrees | §3.3 | api plan on #3019, 08:14Z |
| `known` = PR, review request or push in 30 days | decision 4 | agent default (lead, 2026-11-12) |
| serve start poll up to 30 s; restart within 15 s | decision 2, F6, §4.5 | agent default (lead, 2026-11-12); ux measured 9.9 s to serve up |
| "about 10 seconds" on the Not running page | §4.1 | ux walk, 9.9 s |
| open-runs poll every 5 s while waiting | decision 11 | agent default (lead, 2026-11-12) |
| `restart --wait` default timeout 30 min | decision 11 | agent default (lead, 2026-11-12) |
| map "stopped" after 10 min without a new progress line | §4.4 | agent default (lead, 2026-11-12), a guess; P0 measures it and sets the number (§11 gap 6) |
| progress chip at most 5 characters | §4.4 | design call (grammar piece) |
| commit shown as 7 characters | decision 12 | agent default (lead, 2026-11-12), git's short form |
| key chords (`x`, `a`, `Ctrl+Enter`, `Ctrl+Shift+R`) | §4.7 | agent default (lead, 2026-11-12); `x`, `a` and `Ctrl+Enter` are on design's frame |
| scan cap 500 repos per run | §3.3 | agent default (lead, 2026-11-12) |
| under 180 s, under 100 ms, first 10 repos under 2 s | F1, F9, F10 | meta (#3051 sets 3 min; #3049 sets 2 s); 100 ms agent default (lead, 2026-11-12) taken from ledger 1 |
| 24 px inset, 32 px state line, one primary per state | §4 | design call (grammar piece on `pr-assets/grammar-2978`) |
| scan root capped to the home folder, no symlinks, origin without userinfo, launched callers refused | §3.3 | agent default (lead, 2026-11-12); round 1 security 3 and 4 |
| timed run exit 4 for slow | §8 | agent default (lead, 2026-11-12); round 1 devex |
| `--apply` waits up to 120 s for the setup pass | decision 10 | agent default (lead, 2026-11-12); round 2 devex 3. It sits inside F1's 180 s: the pass on a fresh home takes seconds, and a timeout is reported, not hidden |
| old serve exit wait up to 30 s | decision 11 | agent default (lead, 2026-11-12); round 2 arch M3, round 3 arch 2 |
| one step list from Core (decision 6) | §2 | ui plan on #3019, 08:03Z, item 1 |
| map step follows the code index (decision 18) | §2 | #3112, filed by ux from the walk |
| failed is not empty, one shared older-serve line (decisions 20, 21) | §2 | design call and qa's sweep on #3019, 08:16Z |
| bare letters never write (decision 23) | §2 | meta's key rule on #2978 |

## 3. The model

### 3.1 The steps

| # | Id | Title | Done when | Action (catalog id) | Computed without serve |
|---|---|---|---|---|---|
| 1 | `serve` (new) | Sindri is current | serve answers and runs the installed stamp | `RestartProcess` (new, owner), else `StartServe` (app-local, new) | yes |
| 2 | `sources` | Connect your accounts | GitHub is connected or the step is skipped, and each connected source read once | `ConnectAccount` (new, owner), with the account as an argument | no |
| 3 | `repo` | Add your repos | the profile names a repo | `AddRepo` | yes |
| 4 | `profile` | Profile saved | the file exists and validates | none; a failed check opens the line it names | yes |
| 5 | `agent` (new) | Agent for workflows | `workflow.agent.target` is set | none: the setup service sets it from the first agent `launcher:agents` finds | yes |
| 6 | `mcp` | Agents can use Sindri's tools | every repo is registered | `RegisterMcp` | no |
| 7 | `map` | Build the map | every repo has a current map | `RefreshContextMap` on a failed build | no |
| 8 | `first-workflow` (new) | Run hello-sindri | one hello-sindri run succeeded, or the step is dismissed | `RunWorkflow` with `hello-sindri` | no |

Rules:

- **Order is fixed.** The list is sorted by this table, whatever state each step is in.
- **Skip and dismiss live in the profile, as scalars.** Two keys on the existing `setup` section (`Core/Profile/SetupSettings.cs`): `setup.skipAccounts` and `setup.skipFirstWorkflow`, each `true` or absent. They are scalars so `IProfileKeyWriter` and `profile set` can write them (both refuse a list). The writer is a new route `POST /sindri/setup/skip` with `{step, skipped}`, catalog row `SkipSetupStep` (Desk; any caller, since a skip is undone in one click), and `sindri setup skip accounts|first-workflow [--undo]`. Skip on question 1 sets `skipAccounts`; Dismiss on the hello-sindri row sets `skipFirstWorkflow`. A skipped step counts as done. `skipAccounts` is cleared when GitHub connects. No new table: the profile already holds the `setup` section and is the owner's own file. *agent default (lead, 2026-11-12)*; round 1 arch F5, product C6, round 2 arch M4, devex 2.
- **`first-workflow` done is derived.** It reads the workflow runs view: a `succeeded` live run of `hello-sindri`. Nothing new is stored.
- **`sources` keeps its id.** The app reads `sources` today, so the step is retitled, not renamed. Its `Items` become one line per account: GitHub, Linear, Slack, each `not connected`, `connected as X`, `waiting on admin`, `reading`, `read failed` or `expired`.
- **A new field, `Cli`.** Each step carries the exact command an agent runs for its action, from the catalog row's form, or null. It is null for an owner-only action. The app shows it only under Details.
- **An older app reads the new ids.** `SetupStepView` reads `Id` as a string, so an app older than serve shows an unknown step by its title and no button, never a parse error. P0 adds that test. *agent default (lead, 2026-11-12)*; round 1 arch suggestion F7.

### 3.2 What the serve step reads

| Fact | Source | Shown as |
|---|---|---|
| running stamp, commit, built | `InstalledBuildStamps.Running()` (`Infrastructure/Setup/InstalledBuildStamps.cs`), then that folder's `build-info` | "running 80f69df, built yesterday 22:10" |
| installed stamp, commit, built | `bin/cs/current` and its `build-info` | "installed 7c81646, built today 10:31" |
| open runs | launched agent sessions (as `sindri_open_runs_guard`), workflow runs `running` or `parked`, jobs `running` (decision 11) | "3 are open: Nightly gate (run 14, an agent step), Review loop on #3120 (run 3, waiting on your approval), Code index of agentic-workflow (a job)" |

The app reads the step once on connect, after each restart, and every 5 s only while "Restart when they finish" shows (decision 11). A running build folder that the install pruned has no `build-info`: the step says "A different Sindri is installed" with no times.

### 3.3 The scan

`GET /sindri/setup/repos?root=~&depth=3` streams NDJSON, one line per checkout as the walk finds it: `{path, host, slug, known, inProfile, worktrees}`. The route is read-only and writes nothing.

- **Walk.** It skips dot folders, `node_modules`, `bin`, `obj` and `target`, and never follows a symlink. A folder whose `.git` is a file is a worktree: the walk folds it into its main checkout's `worktrees` count and never lists it as a row. It stops at 500 repos.
- **Root.** `root` must be the home folder or a folder under it; anything else is `SND-SETUP-008`.
- **Origin.** The route returns only the host and the `owner/name` slug of `origin`. It drops the user and password part of the URL, so a remote URL with a user name and token in it shows only as host `github.com` and slug `x/y`.
- **Callers.** The terminal, the app and an owner-started session may scan. A launched session (inside the Sindri sandbox) gets `SND-AUTH-002`, because serve runs outside the sandbox and would read the home's layout for it.
- **Cancel.** Stop scan closes the stream; serve stops the walk when the client goes away.
- **End.** The last line is `{done: true, folders: 41, repos: 6}`.

Confirm sends the ticked paths through the existing `AddRepo` job, one profile write. The same path already holds `POST /sindri/setup/repos` (`AddRepo`, `Ui/Api/Setup/SetupRoutes.cs`), so the scan is a GET on it. `sindri setup scan` reads the same route and needs serve.

### 3.4 The doctor rule for an older home

Every store doctor opens gets the guard `views.db` has (`SqliteViewReader.Ready()`, added for #2881): all migrations this build ships are applied. A store that is behind gives one fail row: "ledger.db is older than this build (schema 14, this build needs 15); fix: Restart Sindri (`sindri stop && sindri`)". Any `SqliteException` inside a doctor check becomes a fail row with the store's name, never an unhandled throw. Other rows still print; exit 1. #2954's red test comes first and names the store that throws today. Its stack trace names `SqliteViewReader.State` at line 59, which may already be guarded; P1 proves it red on `origin/main` before any fix.

## 4. The screens

Question 1 and question 2 are centred-column screens, about 640 px wide, not RowLists. Every other screen uses the Home spec's row grammar, the design grammar piece "Empty and error states (#3019)" and the 24 px inset. While the profile names no repo, the top bar is reduced to the wordmark and the status pill (*design call* 1). After the first Add, the full nav shows (decision 33).

### 4.1 Sindri isn't running

Frame `not-running`. The app starts serve by itself on open (decision 2a), so a person sees this page only after a start failed. The page empty state: glyph, "Sindri isn't running", one sentence "Start it and Home loads your reviews, runs and messages. This takes about 10 seconds.", and **Start Sindri** (Enter), focused. Details holds "Nothing answered on 127.0.0.1:7410. To start it yourself, run: `sindri`".

The heading and the pill say "Not running" with the red × only after a failed start, or when nothing is starting. The Details line has no host:port: a first run has no `control.json` to read it from (#3663, #3672). The failed state has no Open logs action yet, so the slow state offers none either.

The drawing says "about 2 seconds"; ux measured 9.9 s to serve up, so the sentence says 10 until F1 measures less.

| State | What shows | Primary | Focus |
|---|---|---|---|
| Not running (after a failed start, or serve gone mid-session) | as above | Start Sindri (Enter) | the button |
| Starting (on open, with no click) | Heading "Starting" and the top-bar pill "Starting", both neutral with the busy mark (no red ×); "Starting Sindri…"; Details "Waiting for Sindri · 4 s", moving each second; from 15 s "Still starting · 17 s" | none | the status line |
| Started | the first question not done, or Home (decision 30); no toast | that page's own | its primary |
| Didn't start in 30 s | "Sindri didn't start: <first line of serve.log's last error>." Details: the log path and `sindri serve` | Retry (Enter) | Retry |
| A second serve refused | "Sindri is already starting on this machine." It keeps polling | none | the status line |
| No installed `sindri` beside `sindri-ui` | "The Sindri program isn't installed next to this app." Details: `scripts/install-cs.sh --link` | none: this is a broken install, not a user step | Details |

### 4.2 Question 1 of 2: connect your accounts

Frame `setup-connect`. It shows only with no gh login, or after Change (decision 13). Header "Connect your accounts · Question 1 of 2", sub-line "GitHub lets Sindri tick the repos you work on next". Three rows; each has one button where row actions sit. Only the first row not connected holds the primary (decision 22); the other rows' buttons are secondary.

| Row | Sub-line | Button | What the click does |
|---|---|---|---|
| GitHub, gh login found (only after Change, decision 13) | "pull requests, reviews and CI · gh login found: ada · uses the gh login's scopes" | Use the gh login (ada) | sets the credential to `command:gh auth token`; the row turns `connected as ada` |
| GitHub, no gh login | "pull requests, reviews and CI" | Sign in to GitHub | device flow: shows the 8-character code; the one primary is **Copy code and open GitHub**, which copies it and opens the verification URL from the preset |
| GitHub or Linear, org blocks the app, or the build has no app id (`SND-SETUP-013`, `-014`) | "Your org blocks Sindri's app. Paste a token instead." | Paste a GitHub token / Paste a Linear token | three numbered lines, then a masked field and Save token (decision 17). GitHub: 1 "Open GitHub's new fine-grained token page" (Open in browser), 2 "Pick your repos and give it Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read", 3 "Paste the token here". Linear: 1 "Open Linear, Settings, API, Personal API keys" (Open in browser), 2 "Create a key named Sindri", 3 "Paste the key here" |
| Linear | "issues and projects · opens your browser" | Sign in to Linear | loopback OAuth with PKCE through `POST /sindri/connections/{name}/signin`; creates the connection when it is missing |
| Slack | "messages and mentions · needs a token, 3 steps · optional" | Paste a Slack token (never the primary) | opens three numbered lines: 1 "Open Slack's app page and create the app from Sindri's manifest" (Open in browser), 2 "Install the app to your workspace", 3 "Copy the user token Slack shows you, then paste it here" with a masked field and Save token |

The footer holds one of two buttons. Before GitHub connects: **Skip for now** (secondary; decision 31) with "You can connect these later from Connections." Once GitHub is connected: **Continue** (Enter) is the primary, and the Linear and Slack buttons turn secondary, because neither is required (decision 15). Continue or Skip for now opens question 2, or Home when the profile already names a repo.

| State | What shows |
|---|---|
| Not connected | a todo ring, the button |
| Device code shown (GitHub) | "Enter ABCD-1234 at github.com/login/device"; "Waiting for GitHub… the code works for 14:32 more." with Cancel; the time left counts down from GitHub's `expires_in`. Polling follows GitHub's `interval` and `slow_down`, and stops at `expires_in` or Cancel |
| Device code expired | "The code expired before GitHub saw it.", announced once by the live region; **Get a new code** is the primary and takes focus, and Cancel stays. The click replaces the code in place, restarts polling, and moves focus to Copy code and open GitHub |
| Token refused (GitHub or Linear paste) | the field is cleared and focused; "GitHub refused this token: <reason>. Paste a new one." |
| Signing in | "Waiting for your browser…" with Cancel; the other rows still work |
| Connected | `linked` glyph, "connected as X", no button |
| Sign-in cancelled (device code or browser) | the row goes back to "not connected" with its button; focus returns to that button |
| Paste open | the field takes focus; **Save token** (or Save key) becomes the primary once the field holds text, and Enter in the field presses it; until then the row's opener keeps the primary |
| Waiting on admin | "Waiting on your workspace admin to approve Sindri." with Check again (decision 16) |
| Sign-in failed | "GitHub refused the sign-in: <reason>." with Retry |
| Token refused (Slack) | the field is cleared and focused; "Slack refused this token: <reason>. Paste a new one." |
| Store failed | "Couldn't store the token: <reason>. Nothing was saved." with Retry (`SND-REVIEW-025`'s rule, Reviews spec §9) |
| Expired, later | the same row with **Reconnect GitHub**, on the Setup page and as a Needs you row (#3050 item 3) |

The screen reader announces each result once: "GitHub connected as ada", "Linear sign-in failed".

### 4.3 Question 2 of 2: where are your repos?

Frame `setup-repos`. The page opens with the scan already running from `~`, depth 3. The header says "Where are your repos? Question 2 of 2", sub-line "this is the last question; Home opens next". On a gh-login box the header adds "Signed in as ada through gh · Change" (decision 13). A status line says "Scanning ~ · 3 levels · 41 folders · 6 repos so far" with Stop scan. Rows arrive as the scan finds them and append at the end; a shown row never moves.

Each row: tick box, path, slug, `worktrees: N` when N > 0, and a `known` chip. Known rows come pre-ticked; their sub-line says "you worked on this repo on GitHub in the last 30 days". A repo with no origin shows its path and "no remote" in place of the slug, and can be ticked. Placeholder rows hold the space of the next ones while the scan runs. With no account connected, nothing is pre-ticked and the sub-line of the header says "Tick the repos you want Sindri to watch".

The footer: "2 of 6 ticked · worktrees fold into their main checkout", **Look in another folder…** (a folder picker; the scan restarts from that root), and **Add 2 repos** (Ctrl+Enter), the primary. Dragging a folder onto the window adds it as a ticked row (GitHub Desktop's fallback, ledger 2).

| State | What shows | Primary |
|---|---|---|
| Scanning | rows arrive; first 10 in under 2 s (F10) | Add N repos, off until one is ticked |
| Done, repos found | "6 repos under ~, 3 levels deep" | Add N repos |
| Done, none found | "No git repos under ~, 3 levels deep." | Look in another folder…; a secondary Skip for now (decision 31) |
| Scan failed | inline line "Couldn't read ~: <reason>." | Retry; a secondary Skip for now |
| Adding | "Adding 2 repos…", the rows read-only | none |
| Add failed for one repo | the others are added and leave the list; that row turns red with `SND-SETUP-001` or `-002`'s sentence and its own Retry (secondary) | **Continue to Home** (Enter), the footer primary, because one repo is added. When every add failed: Retry on the first row, and a secondary Skip for now |
| Added | Home opens; hello-sindri is already running (decision 19) | Home's |

Esc goes back to question 1 (on a gh-login box it shows GitHub connected, with Change). Nothing written in question 1 is undone by it.

### 4.4 The Setup page

Frame `setup-steps`. Header "Setup · 6 of 8 steps done". One RowList of §3.1's steps, the same order as `sindri setup`. Done rows are plain. The first row that is not done holds the one primary (decision 22); a failed row further down shows Retry as a secondary.

Example rows at §5.0's numbers:

| Row | Sub-line | Right side |
|---|---|---|
| ✓ Sindri is current | running 7c81646, the installed one | |
| ✓ Connect your accounts | GitHub is connected as ada · Linear and Slack are optional and not connected | Accounts (secondary) |
| ✓ Add your repos | jthorare/agentic-workflow and ledgerly-app/exports | 2 repos · Add repos (secondary, decision 32) |
| ✓ Profile saved | every setting read, none wrong | |
| ✓ Agent for workflows: claude-code | found installed; workflows launch their agent steps with it | |
| ✓ Agents can use Sindri's tools | registered in agentic-workflow and exports | |
| ◌ Build the map | Building the map: 4,720 of 15,223 files in agentic-workflow | 31% |
| ◌ Run hello-sindri | hello-sindri is running: step 2 of 4 (it started by itself, decision 19) | none |

The map row's sub-line ends with "You can keep working; it finishes in the background." while it runs. The map row has two phases (decision 18): "Indexing files: 7,000 of 15,223" while `BuildCodeIndex` runs, then "Building the map: 4,720 of 15,223 files". Progress shows at most 5 characters (`31%`); the full count sits in the sub-line (design grammar). A build that shows no new progress line for 10 minutes turns into a failed row: "The map build stopped at 4,720 of 15,223 files 10 minutes ago." with Retry (ledger 4: a building state needs a timeout). The 10 minutes is a guess with no measured source; P0 checks it against the longest gap between progress lines in the ux walk's `BuildCodeIndex` and `BuildContextMap` logs and sets the number from that.

Each step's own states that are not on the example:

| Step | State | Words | Primary |
|---|---|---|---|
| `agent` | no agent installed | "No coding agent found. Install Claude Code, Codex or Cursor, then check again." | Check again |
| `first-workflow` | the run failed | "hello-sindri failed at <step title>: <the Workflows spec §6.1 sentence>." | Open the run |
| `first-workflow` | running | "hello-sindri is running: step 2 of 4." | none |
| `first-workflow` | succeeded | "hello-sindri ran: made 'Hello from Sindri' in agentic-workflow." | Open item; Run again (secondary) |
| `map` | one repo's build failed fast | "The map of ledgerly-app/exports failed: the disk is full (No space left on device). agentic-workflow is done." The step is Failed when any repo failed; the sub-line names each failed repo | Retry (the primary only when `map` is the first step not done with a button) |
| `map` | stopped | "The map build stopped at 4,720 of 15,223 files 10 minutes ago." | Retry |
| `sources` | an account expired | "Your Linear sign-in expired." An expired optional account (Linear, Slack) keeps the step done and counted; an expired GitHub makes it not done | Reconnect Linear (secondary while the step stays done; the primary for GitHub) |
| `sources` | first read failed | "The last read of jthorare/agentic-workflow failed 2m ago." | Retry (the primary only when `sources` is the first step not done) |

The page's own states:

| State | What shows | Primary |
|---|---|---|
| Loading | the last read from the local home, painted in under 100 ms (F9) | as read |
| Serve down | step 1 is "Sindri isn't running" with Start Sindri; steps 2, 6, 7 and 8 say "Waits for Sindri to run" | Start Sindri |
| Steps can't be read (older serve) | the shared line of decision 21 | Restart Sindri |
| Steps read failed | inline line "Setup can't be read right now." | Retry |
| All done | the tab leaves the nav; Settings holds the list | none |

### 4.5 The stale serve line

Frame `states`, panel "Stale serve under the top bar". One 32 px line under the top bar, tinted `.att`, on every page: "A newer Sindri is installed (7c81646, built today 10:31)." with **Restart Sindri**. It shows while the serve step is not Done. Details holds both stamps and `sindri stop && sindri`.

| State | What shows | Primary |
|---|---|---|
| Stale | as above | Restart Sindri |
| Runs open | "Sindri restarts when 3 finish: Nightly gate (run 14, an agent step), Review loop on #3120 (run 3, waiting on your approval), Code index of agentic-workflow (a job)." Each name links to its page (the run page or the job page), where Stop is | Restart when they finish |
| Waiting for runs | "Restarts when 3 finish. Keep this window open." with the three links and Cancel | none |
| A new run started during the wait | "A new run started: Weekday check (run 7). Sindri restarts when it finishes too." with its link; the wait goes on with the new set | none |
| Cancelled | back to Stale | Restart Sindri |
| Still stopping (the old serve's pid alive after 30 s) | "Sindri is still stopping." Details: the pid and the log path | Start Sindri, which starts nothing while that pid lives |
| Restarting | "Restarting Sindri…"; pages keep their last read and writes are off | none |
| Restarted | the line goes; one toast "Sindri restarted on 7c81646." | none |
| Didn't come back in 30 s | "Sindri stopped but didn't start again: <first line of serve.log's last error>." Details: the log path | Start Sindri |

An agent reading `sindri setup` sees the serve step as `needs-you` with `Cli = null` and exits 3 (decision 9).

### 4.6 Every page's empty and failed state

These follow design's `states` frame, one panel each. Each row has exactly one primary.

| Page (issue) | State | Words | Primary |
|---|---|---|---|
| Shell (#3055) | not running | §4.1 | Start Sindri |
| Shell (#3055) | not responding | "Sindri is not responding." | Refresh |
| Shell (#3055) | unreadable | the shared line | Restart Sindri |
| Home (#3056, #3113) | no profile | not reached in first run (decision 30 opens the questions); shown only when a profile read fails after a restart error: "Finish setting up Sindri: connect your accounts" | Connect your accounts |
| Home | a profile with no repo (question 1 done, question 2 not) | the setup line "Finish setting up Sindri: add your repos" | Find my repos (after Skip for now on question 2) |
| Home | hello-sindri being made (the seconds after the first Add) | "Getting a first workflow ready…" (seconds; it then runs by itself) | none |
| Home | hello-sindri succeeded (the finish line) | one real row "hello-sindri ran: made 'Hello from Sindri' in agentic-workflow", and under it the line "Building the map of agentic-workflow: 31%. The map is still building, you can keep working." linking to Setup | Open item |
| Home | hello-sindri running | "hello-sindri is running: step 2 of 4." | none |
| Home | hello-sindri failed | "hello-sindri failed at <step title>: <sentence>." | Open the run; Run again (secondary) |
| Home | a map build failed | the setup line "The map of ledgerly-app/exports failed. Retry" | Retry |
| Home | own rows present (PRs, issues) | Home's normal rows hold the primary; hello-sindri shows as a secondary line until run or dismissed | Home's own |
| Home (#3056) | queue read failed | "What needs you can't be read right now." with the last list kept | Retry |
| Home (#3056) | truly empty | "Nothing needs you and nothing is running" | Open Workflows |
| Home (#3113) | health strip, no profile | "Profile: not set up", no red mark; the pill does not say Healthy beside it | none: the setup line above holds the primary |
| Home (#3127) | services count | the count `sindri services` lists ("16 services") | none |
| Health (#3057, #3183) | first build running | Code index card "building", with its progress; the headline does not name it | none |
| Health (#3183) | source never read | "waiting for the first read" | none |
| Health (#3057) | different versions | the stale line of §4.5 | Restart Sindri |
| Context (#3060, #3015) | no map, nothing building | "No map of agentic-workflow yet" | Build the map |
| Context (#3015) | building | "Building the map of agentic-workflow · 4,720 / 15,223 files" | none; Stop building is secondary |
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
| Question 1 and Accounts | `j`/`k` move, Enter presses the focused control, `Ctrl+Enter` presses the screen's primary wherever focus is, `Ctrl+V` pastes in a token field; when Continue or Done becomes the primary, focus moves to it, so Enter presses it. Esc does nothing in first run (there is no step before question 1) and presses Done on the Accounts page | the row that holds the primary |
| Question 2 and Repos | `j`/`k` move, `x` ticks the row, `a` ticks all, `Space` peeks the path, `/` filters, `Ctrl+Enter` presses the screen's primary (Add N repos, or Continue to Home), `Esc` stops a running scan first, then goes back to question 1 (presses Done on the Repos page) | row 1 once it arrives, the status line before; after Look in another folder… restarts the scan, row 1 of the new scan |
| Setup page | Home's `RowKeys`: `j`/`k`, Enter presses the row's primary, `.` opens actions | the row that holds the primary (decision 22); the first row not done when no row holds the primary (the running-by-itself panel: the map row) |
| Stale line | `Ctrl+Shift+R` presses the line's primary from any page: Restart Sindri, or Restart when they finish while runs are open | does not take focus |

Every primary is a button with its key chip. Tab moves between the rows, the status-line button (Stop scan, Cancel) and the footer buttons (Look in another folder…, Skip for now, Continue), in that order. `j`/`k` move inside the list, and Enter presses the focused control. Esc cancels a device-code wait or stops a scan before it does anything else. Key hints show only keys that act on that screen. The screen reader names each step row "Build the map, running, 31 percent, 7 of 8". A state change on the step list is announced once, by the page's live region. The scan announces "6 repos found" once at its end, not per row.

## 5. Numbers and agents

### 5.0 The fixture numbers

Every frame and example uses these. A frame with other numbers is wrong.

| Fact | Value |
|---|---|
| Accounts | gh login found as `ada`; Linear and Slack optional and not connected; `sources` is done once GitHub connects |
| Scan | root `~`, depth 3; first frame "41 folders · 6 repos so far"; done "6 repos" |
| Repos found | `~/repos/agentic-workflow` (jthorare/agentic-workflow, worktrees: 104, known), `~/repos/exports` (ledgerly-app/exports, worktrees: 3, known), `~/work/ledgerly-core` (ledgerly-app/core), `~/repos/dotfiles` (ada/dotfiles), `~/repos/prism-mcp-fork` (ada/prism-mcp, worktrees: 1), `~/src/old-site` (no remote) |
| Ticked | 2 of 6 (the two known, because GitHub is connected); button "Add 2 repos" |
| Repo size | agentic-workflow: 15,223 files, 82,825 symbols (ux walk) |
| Index progress | "Indexing files: 7,000 of 15,223" |
| Map progress | "Building the map: 4,720 of 15,223 files", chip `31%` |
| Builds | running `80f69df`, built 2026-11-11 22:10Z; installed `7c81646`, built 2026-11-12 10:31Z |
| Open set | 3: Nightly gate (run 14, an agent step), Review loop on #3120 (run 3, waiting on your approval), Code index of agentic-workflow (a job) |
| hello-sindri | 4 steps: Start (manual), Make a greeting (code, its `timeout` set to 60 s, the longest `CodeBlock` allows, `CodeBlock.cs:13`; the default is 5 s), Tell me (notify), Add an item (`create_item` in agentic-workflow) |
| hello-sindri failure | "hello-sindri failed at Make a greeting: the step ran out of time after 60 s." |
| hello-sindri success | "hello-sindri ran: made 'Hello from Sindri' in agentic-workflow." |
| Map failure | "The map of ledgerly-app/exports failed: the disk is full (No space left on device). agentic-workflow is done." |
| Still stopping | the old serve's pid 48213; log `~/.agentic-workflow/sindri-app/logs/serve.log` |
| Didn't come back | "Sindri stopped but didn't start again: port 7410 is already in use." |
| Setup page | 8 steps, 6 done (GitHub connected counts `sources` done); nav tab "Setup 6/8" |
| Services | 16 built-in services (#3127) |
| Today's times | serve up 9.9 s, app ready +3 s, repo added t+81 s, first code index about 18 min (ux walk) |

### 5.1 Agent parity

| Action | CLI | MCP | A session may |
|---|---|---|---|
| read the steps | `sindri setup [--json]` (works with serve down; exit 0, 1, 2, or 3 when only the owner can finish) | `setup_get` (new, after setup) | yes |
| scan for repos | `sindri setup scan [--root DIR] [--depth N] [--json]` (new; needs serve) | none (decision 25) | an owner-started session yes; a launched one no |
| add repos | `sindri repo add <path>` (exists) | none | yes (a job row, `SetupRows.cs`) |
| do every automatic step | `sindri setup --apply [--repo PATH]... [--timeout D] [--json]` (new) | none | yes |
| skip a step | `sindri setup skip accounts\|first-workflow [--undo]` (new) | none | yes |
| the app's Add repos and Skip for now | `sindri setup scan` then `sindri repo add <path>`; `sindri setup skip accounts` | none | yes |
| start serve | bare `sindri` / `sindri serve` (exist) | none (no serve to answer) | yes |
| restart serve | `sindri restart [--wait] [--timeout D]` (new) | none | an owner-started one yes; a launched one no: `SND-AUTH-002` |
| connect an account | `sindri setup connect github` or `linear` (device flow or browser), `--token-env NAME` on any of the three for the paste fallback, `slack` reads stdin otherwise (new) | none | an owner-started one yes, with the gh login or `--token-env`; a browser or device-code sign-in still needs the person (exit 3); a launched one no: `SND-AUTH-002` |
| run hello-sindri | `sindri wf run hello-sindri` (exists) | `workflow_run` (exists) | yes |

`sindri setup --apply --json` answers one document: `{added: [path], already: [path], failed: [{path, code, message}], serveStarted: bool, setupPass: {state, jobId, seconds}, steps: [SetupStep]}`. `setupPass.state` is `done`, `timeout`, `failed`, `skipped` (`--timeout 0`: no wait) or `refused` (a caller that may not run services; the pass runs on its next tick, within 60 s). Exit 0 when every named repo is added or already there and the pass is `done`, `skipped` or `refused`; 1 when a repo failed, serve didn't start, or the pass is `timeout` or `failed`; 2 on usage or a refusal (`SND-SETUP-010`). On a timeout, stderr gets one line and the repos already added still print:

`SND-SETUP-015 the setup pass didn't finish in 120 s; it keeps running as JOB-01a1… (fix: sindri jobs show JOB-01a1…, then sindri setup)`

A failed pass prints `SND-SETUP-016 the setup pass failed: <first line of its error> (fix: sindri jobs show JOB-01a1…)`. A pass already running when `--apply` asks (`SND-JOB-409`) is followed, not refused. A re-run with the same `--repo` is exit 0 with the repo under `already`.

`sindri restart` exits 0 once the new serve answers; 1 when runs are open (`SND-SETUP-009`, without `--wait`; it prints each open run with its `sindri wf run-show <run>` line) or serve didn't come back (`SND-SETUP-012`); 2 when refused (`SND-AUTH-002`, `SND-SETUP-011`). `--wait` blocks until the runs open at the call have ended or `--timeout` passes (exit 1).

Every free text in a setup result is fenced as untrusted, as every `sindri_*` result is: paths, slugs, account names, run names and the first line of an error.

## 6. Errors, in words

`SND-SETUP-005` to `-007` are taken in the docs (`docs/sindri/errors.md:157-158` lists Onyx codes `-005` and `-006`; `docs/csharp/sindri.md:807` and the setup-once spec use `-007`), though `SindriErrors.cs:233-243` stops at `-004`. The new codes are `-008` to `-016`; P0 also deletes the retired Onyx rows from `docs/sindri/errors.md`. The app shows the sentence and puts the code in Details. The CLI prints `code message (fix: ...)` as today.

| Code | When | Sentence | Button / command | Exit |
|---|---|---|---|---|
| `SND-API-001` | serve is down (exists) | "Sindri isn't running." | Start Sindri / `sindri` | 1 |
| `SND-BUILD-001` | serve runs another stamp (exists, `HealthSummary.cs:71`) | "A newer Sindri is installed (7c81646, built today 10:31)." | Restart Sindri / `sindri stop && sindri` | 1 |
| `SND-SETUP-001` | the folder isn't a repo's top folder (exists) | "That folder isn't the top of a git repo." | Look in another folder… / `sindri repo add <path>` | 2 |
| `SND-SETUP-002` | the repo is already in the profile (exists; `--apply` reports it as `already`) | "That repo is already added." | none | 2 |
| `SND-SETUP-008` (new) | the scan root is missing, unreadable or outside the home folder | "Couldn't read <root>: <reason>." | Look in another folder… / `sindri setup scan --root DIR` | 2 |
| `SND-SETUP-009` (new) | restart refused: runs are open | "Sindri restarts when 3 finish: <names, each a link to its run or job page>." | Restart when they finish / `sindri restart --wait`; `sindri wf run-show <run>` per run | 1 |
| `SND-SETUP-013` (new) | GitHub or Linear refused Sindri's OAuth app (an org policy) | "Your org blocks Sindri's app. Paste a token instead." Under it, for GitHub, the fine-grained set: "Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read" | Paste a GitHub token or Paste a Linear token / `sindri setup connect github --token-env NAME` | 1 |
| `SND-SETUP-015` (new) | `--apply`'s setup pass didn't finish within `--timeout` | "The setup pass didn't finish in 120 s; it keeps running." | `sindri jobs show <id>`, then `sindri setup` | 1 |
| `SND-SETUP-016` (new) | `--apply`'s setup pass failed | "The setup pass failed: <first line>." | `sindri jobs show <id>` | 1 |
| `SND-SETUP-014` (new) | this build ships no client id for the provider and the profile sets none (`setup.oauth.github` or `.linear`) | "This build has no <provider> app id. Paste a token instead." Under it, for GitHub, the fine-grained set: "Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read" | Paste a GitHub token or Paste a Linear token / `sindri setup connect <provider> --token-env NAME` | 1 |
| `SND-SETUP-010` (new) | `--apply` with no repo named and none known | "Name a repo to add: no repo here is one you worked on in GitHub lately." | `sindri setup --apply --repo <path>` | 2 |
| `SND-SETUP-011` (new) | restart: the installed build has no `build-info` | "The installed Sindri isn't finished installing." | `scripts/install-cs.sh --link` | 2 |
| `SND-SETUP-012` (new) | serve didn't answer 30 s after a start or restart | "Sindri didn't start: <first line of its error>." | Retry / `sindri serve` | 1 |
| `SND-AUTH-002` | a launched session calls restart, connect or scan (exists) | "This is for the person at the keyboard." | none | 2 |

Doctor's new fail row (§3.4) has no code of its own: it is a `DoctorCheck` row named after the store, as `build` is today (`DoctorRules.cs:90`). Its text change updates `src/Sindri/aot-smoke/expected.txt`.

## 7. Data and API

| # | Need | Lane | Notes |
|---|---|---|---|
| A1 | `SetupStepId` gains `serve`, `agent`, `first-workflow`; `SetupStep` gains `Cli`; `sources` moves to step 2 and is retitled, one item per account; `setup.skipAccounts`, `setup.skipFirstWorkflow`, `setup.oauth.github`, `setup.oauth.linear` profile keys (owner-written, shape-checked in `ProfileRules`, in `ProfileDiff.Index`); `POST /sindri/setup/skip` and `SkipSetupStep` | api | golden and API scenario row; the app reads the new ids in the same PR (`SetupStepView`), and an older app's test reads an unknown id |
| A2 | `sindri setup` computes steps 1, 3, 4 and 5 in process when serve is down; exit 3; `--apply`, `--repo`, the `--json` document | api | shares `SetupSteps` rules, no second copy |
| A3 | `GET /sindri/setup/repos` (NDJSON stream), `sindri setup scan` | api | read-only; worktree fold; origin without userinfo; launched callers refused; `known` from the GitHub account |
| A4 | `POST /sindri/restart`, `RestartProcess` catalog row checked by the human token, `GET /sindri/runs/open` (agent sessions, workflow runs, jobs, each with its page link), the in-memory start refusal between the check and the stop, the audit entry naming who clicked and the open set, `sindri restart [--wait] [--timeout]`; the caller waits for the old pid and port before the start | api | the open-set read in C#, a superset of `scripts/lib/sindri-open-runs.sh` (its bash cases plus a workflow run with no agent session and a running job) |
| A5 | serve step reads `build-info` of both stamps | api | `IBuildStamps` gains the two `built=` times |
| A6 | Doctor guard per store; `SqliteException` becomes a fail row (#2954) | api | red first; aot-smoke line |
| A7 | Bare `sindri` words and exit 0 on a missing profile (#3114) | api | aot-smoke and `Usage.cs` if text changes |
| A8 | `ConnectAccount` row checked by the human token; `POST /sindri/setup/accounts/{github,linear,slack}`; gh login detection; device flow; sign-in that creates a missing connection; first write creates the profile; the shipped client id is a new `clientId` field in `config/connections/presets/github.json` and `linear.json`, which `setup.oauth.*` overrides | api | the Slack body is a secret type: never logged, recorded or stored outside `ISecretWriter` |
| A9 | Status counts the same services as `GET /sindri/services` (#3127) | api | `GetStatusService.cs:69` |
| C1 | Map step reads `BuildCodeIndex` too (#3112); `MapSetupReader.cs:22` watches one kind today | context | progress through `MapBuildFiles.Newest` (#3433) |
| C2 | Context page reads the same running fact (#3015) | context, ui | `MapSetupReader.cs:71` already has it |
| C3 | Doctor's no-map row says what it waits on (#3216) | context | `MapReader.cs:87`, `CheckStructureService.cs:49`; aot-smoke |
| C4 | Six services read a null `context:` as defaults (#3131) | context | one helper, one test per service |
| C5 | Embeddings: split long bodies, one 400 never parks the layer, doctor names the real cause (#3128) | context | `OllamaEmbedder.Embed`, `CodeIndexDoctor.cs:168` |
| E1 | Setup service sets `workflow.agent.target` from the first agent `launcher:agents` finds | engine | the `agent` step; a line, not a question |
| E2 | `hello-sindri` template (manual, code, notify, `create_item` in the first repo); the setup service creates and activates it once, when the profile first names a repo, and records a `setup.first_workflow.created` ledger event so a deleted or dismissed hello-sindri is never made again; `CreateWorkflow` and `ActivateWorkflow` gain `ServiceGrants: ["setup"]`, as `RegisterMcp` has | engine | needs no map, account, model or network; a test-run case in CI (Workflows spec W4); its first code-node compile is timed in §8 |
| U1 | Desktop host starts serve (decision 2) | ui | `Aw.Sindri.Ui.Desktop`, never `Aw.Sindri.Ui` |
| U2 | `FailedReadLine` and the page empty state; `OpensInApp` true for every action with a route | ui | `ShellViewModel.Setup.cs:20` |

## 8. The timed run

`scripts/context-first-run.sh` writes the profile itself in step 2, so it never walks what a new user walks. A new flag `up --as-new-user` skips steps 2 and 3, then:

1. starts serve with bare `sindri --headless` and records the seconds;
2. runs `sindri setup --apply --repo <checkout>` and records the seconds to the `repo` step Done;
3. `--apply` has already run the setup pass (decision 10), so hello-sindri is active; it runs `sindri wf run hello-sindri` and records the seconds to its run row, the first code-node compile included;
4. polls Home's read route until one row names the item hello-sindri made in this repo;
5. prints one line per step and a total, for example `first-run total 142 s`.

Exit 0 when every step passed within 180 s; 1 when a step failed (its line names the step and the error); 4 when every step passed but the total is over 180 s, so CI can tell "broken" from "slow". CI runs it in the scratch-home job and stores the lines, so a regression shows as a number going up (#3051). The map keeps building after the run ends; the run does not wait for it.

The owner's box runs the app walk with `aw-ui-driver` as well. It adds question 1 with the gh login and prints `first-run accounts N s` (F1c) and `first-run own-data N s` (F1b).

## 9. Goals and non-goals

Goals:

- Fresh home to Home with a row in under 3 minutes, 2 questions at most (F1, F2), and the owner's own data within 1 more minute (F1b).
- Every state with one button (F3).
- An agent can set up a box through the CLI, except restart and sign-in (F8).

Non-goals:

- Import of other tools' settings or history (ledger 6: skip). The Setup page's all-done line says so: "Not imported: agent session history and other tools' settings."
- A Slack one-click sign-in. It needs an https relay; it waits on #2979.
- Upgrades beyond the rule below. A new install migrates the home on serve start; each thing it can't migrate becomes a `setup` Needs you row with its fix as a button, through `SetupNotices` (`Features/GetNeedsYou/SetupNotices.cs`). Known cases: an older store (§3.4) and Onyx leftovers (`sindri onyx down --purge`, the owner's verb).
- A setup wizard window. The questions are pages of the one app.
- A scan deeper than 3 levels. "Look in another folder…" covers a repo further down.

## 10. What happens to today's views

| Today | After |
|---|---|
| `SetupRepoForm` path field | the streamed scan list; the path field stays behind "Look in another folder…" |
| `Labels.Setup*` step copy (`Labels.Setup.cs:41`, `:62`, `:139`) | dropped; the route's text shows |
| `OpensInApp` always false | true for every action with a route |
| "Start it from a terminal:" card | the Not running page with Start Sindri |
| Doctor's `build` warn row | stays; the app adds the stale line |
| `sources` step "needs a token" with `sindri index connect` | step 2, the accounts step with one button per account |
| Model, label and surface choices on the setup page (#3115) | move out of setup until each is a toggle |

## 11. Gaps found

1. **The drawings show build numbers that don't exist.** Stamps are hashes (`install-cs.sh:126`). Every frame uses the commit and the `built=` time (§14).
2. **Three GitHub credentials exist.** The `github` connection (OAuth), the `github` index source (`sindri index connect github`, a read-only token in the keychain) and `review.credential` (`env:` or `command:`, `PrReviewRules.cs:79`). Decision 14 points all three at one entry. P0 must confirm whether the index source can take a `command:` pointer; if it can't, the accounts step stores the token once and points the other two at that entry.
3. **The device flow is new plumbing.** The connection layer has only the loopback sign-in today (`ISignInSession`). P5 adds the device flow and sizes it. The owner's one-time registration is in decision 15a.
4. **`--apply` picks repos when none is named.** Ticking "known" repos without asking is a guess at the owner's intent. Meta picked it (§15 pick 3): it only runs when an agent was asked to set up and named no repo, and it prints each repo it added.
5. **#2954's throw may already be guarded.** The api comment says `views.db` has a guard since #2881, while the issue's stack names `SqliteViewReader.State`. The red test decides.
6. **The 10-minute "stopped" timeout for the map is a guess.** It has no measured source. The rule counts time since the last progress line, not total time, because the first code index ran 18 minutes. P0 measures the longest gap between progress lines on this repo and sets the number.
7. **`known` needs a GitHub read before the scan's rows show.** One call for the account's recent repos (pushes, PRs, review requests) runs when question 2 opens. If it fails, the rows show unticked and a line says "Couldn't read your GitHub repos. Retry". P4 measures whether it fits F10's 2 s.
8. **Ticking many repos starts many first builds.** Each added repo starts its own code index. *agent default (lead, 2026-11-12)*: the first repo in the list builds first and the rest queue behind it, so the first map comes as early as one repo allows. The context lane confirms the queue rule in P6.
9. **A Sindri GitHub App with fine-grained permissions** would replace `repo` with the fine-grained set of decision 15a. It needs an org admin to install it, so it waits until after v1 and a probe of how often the owner's work org allows it.

## 12. Proof

Proof is the app run on scratch homes with `aw-ui-driver`, not tests alone.

1. **The walk, timed.** Fresh home, bare `sindri`, the two questions, Home, Run hello-sindri. Shots of each screen before and after. This is F1, F1b, F1c and F2.
2. **The CLI walk.** `sindri setup` with serve down, `--apply`, then `sindri setup` again, as the terminal and as a session (F5, F8).
3. **State matrix.** One frames test per state row of §4 (F3).
4. **Stale serve.** Two builds in a scratch `bin/cs`; Restart with nothing open and with §5.0's open set (an agent step, an approval wait, a job); close the app mid-wait (F6).
5. **Older home.** A home written by an older build, doctor run (F7).
6. **Adversarial design review** by the ideas lane on the shots, judged against inspora.design (owner rule, 2026-11-10).

## 13. Phases and parallel plan (for /specToProvenPR)

Phases are the PRs. Every lane has work from day one; each phase lands its red and stubs first.

| Phase | What | Lane | Depends on | First red test |
|---|---|---|---|---|
| P0 | Contract: new step ids, order, `Cli`, the `setup` keys, the skip route, exit 3, the scan line record, `POST /sindri/restart` and `GET /sindri/runs/open`, `RestartProcess` and `ConnectAccount` rows with the human-token check, `SND-SETUP-008` to `-016`, the retired Onyx doc rows, goldens, all as stubs; measure the map timeout | api | none | `Setup_lists_eight_steps_in_order` |
| P1 | #2954 doctor on an older home; #3114 bare `sindri` words and exit; aot-smoke lines | api | none (start now) | `Doctor_prints_a_fail_row_for_a_ledger_db_one_migration_behind` |
| P2 | Stale serve: `build-info` read, serve step, restart route and open-runs read (#3052) | api | P0 | `Restart_refuses_and_names_two_open_runs`; `Restart_from_a_session_is_refused` |
| P3 | Headless `sindri setup`, exit 3, `--apply` and its document | api | P0 | `Setup_with_serve_down_reports_not_running_first` |
| P4 | Scan route and `setup scan` (#3049); `known` from the GitHub account | api | P0 | `Scan_folds_a_worktree_into_its_main_checkout`; `Scan_drops_userinfo_from_origin` |
| P5 | Accounts step: gh login, device flow, Linear sign-in that creates the connection, Slack paste, admin-pending state, one credential, first write creates the profile (#3050) | api | P0; gap 2 answered | `Gh_login_sets_review_credential_to_the_gh_command` |
| P6 | Map step follows `BuildCodeIndex` (#3112), Context page building state (#3015), doctor text (#3216), first-builds queue | context | none (start now) | `Map_step_is_running_while_the_first_code_index_builds` |
| P7 | #3131 six services on a default profile; #3128 embeddings | context | none (start now) | `Context_sync_reads_default_sources_on_a_profile_with_no_context_section` |
| P8 | Agent target set by the setup service; `hello-sindri` template, created and activated on the first repo; its CI case | engine | P0 for the step ids | `Setup_creates_and_activates_hello_sindri_when_the_first_repo_is_added` |
| P9 | `FailedReadLine`, page empty state, `OpensInApp`; then #3056, #3055, #3063, #3064, #3065, #3066, #3062, #3059, #3061, #3057, #3058, #3060, #3113, #3183 | ui | X1 for visuals; structure starts now | `Failed_queue_read_never_shows_nothing_running` |
| P10 | Start Sindri in the Desktop host; the Not running page | ui | none | `Start_sindri_launches_the_binary_beside_the_app_once` |
| P11 | Question 1 and question 2 screens; Setup page on the step list; Home's Run hello-sindri state; the stale line and its wait; #3054, #3115 | ui | P0 stubs; P2, P4, P5 for live data | `Setup_page_rows_match_the_route_steps` |
| P12 | Timed run `--as-new-user` and its CI job (#3051) | tools, ux | P3, P8 | bash test with a stub `sindri`: exit 4 on a slow run |
| X1 | Design: redraw the frames of §14 at §5.0's numbers | design | none | |
| X2 | UX: the walk at each phase's end, timed; one issue `Part of #3019` per place it had to read a doc | ux | each phase | |
| X3 | QA: one issue per page that still has a state without a button; F1 to F10 | qa | each phase | |
| X4 | Perf: F9, F10 in the real app | perf | P4, P11 | |
| X5 | Tools: docs for the new verbs in `docs/csharp/sindri.md` and `Usage.cs`, the CLAUDE.md commands block, aot-smoke lines for P0 and P3 | tools | each phase | |

Day one: api P0 and P1; context P6 and P7; engine P8 on the P0 stubs; ui P9 and P10; design X1; ux X2 baseline walk. Nothing waits on a dependency PR to start its red.

Gate notes: P0, P2, P3, P4 and P5 add catalog rows, action ids and API goldens, so each gate runs the Sindri integration lane, `EndpointCqrsTests` for the new POST routes, and `Aw.Sindri.Ui.Tests`. P1 and P6 change doctor text, and P0 and P3 change `sindri setup` output, so they update `src/Sindri/aot-smoke/expected.txt` and run aot-smoke. A `Usage.cs` change runs the whole `Aw.Sindri.Tests` unit lane. P8 adds the `setup.first_workflow.created` event, so it runs `EventCatalogTests` and updates the event catalog golden. P10 adds a process start in the Desktop host only. This spec adds no environment variable; `scripts/check-env-example.sh` still runs.

## 14. Frames for design

Redraw at §5.0's numbers. Each frame shows every state row of its section. The question order is now accounts (question 1), then repos (question 2): swap the two frames' headers and their "Question N of 2" lines. No frame shows a build number: every build is its commit and its `built=` time. One primary per frame state, picked by decision 22.

1. `not-running`: Didn't start, Already starting, Still stopping, and the broken install ("The Sindri program isn't installed next to this app."), each with the reduced top bar (decision 33); Starting as the automatic state on open (decision 2a). The sentence says "about 10 seconds".
2. `setup-connect`, now question 1 (§4.2):
   - each row's states: gh login found and not found, signing in, connected, waiting on admin, sign-in failed;
   - the device code shown (with **Copy code and open GitHub** as the primary) and the device code expired, with **Get a new code** focused and Cancel kept;
   - the token paste open for GitHub, with its three numbered steps, step 2 naming the fine-grained set of decision 15a ("Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read"); the Linear paste with its three steps; token refused; store failed;
   - `SND-SETUP-013`: "Your org blocks Sindri's app. Paste a token instead.", with the paste shown;
   - `SND-SETUP-014`: "This build has no GitHub app id. Paste a token instead.", with the paste shown in place of the sign-in button;
   - the Slack paste open, its button never the primary;
   - the footer: Skip before GitHub connects, then **Continue** as the primary once GitHub connects, with the Linear and Slack buttons gone secondary.
3. `setup-accounts` (new): the same screen as the Accounts page, opened from elsewhere (decision 28). Header "Accounts", sub-line "Connect or reconnect the accounts Sindri reads", no "Question 1 of 2", footer **Done**. Three entries, one frame each:
   - from a Needs you row "Your Linear sign-in expired": Reconnect Linear is the primary and focused; after it connects, Done is the primary and focused;
   - from the Setup page: the first row not connected holds the primary, Done is secondary;
   - Done pressed: back on the page it came from (show the Setup page with the row now done).
4. `setup-repos`, now question 2: Scanning, Done, None found, Scan failed, Adding, one row failed, nothing pre-ticked with no account (§4.3); the "Signed in as ada through gh · Change" header line; Continue to Home after a partial add; Skip for now on none found, scan failed and all adds failed; focus on row 1. Slugs `ada/...` for the made-up repos.
4a. `setup-repos-page` (new): the Repos page from the Setup page's Add repos (decision 32): header "Repos", added repos hidden, Add N repos primary once ticked, Done otherwise.
5. `setup-steps`: 8 steps in §3.1's order, 6 done, focus on the row that holds the primary; Add repos on the done repos row; hello-sindri running, then succeeded; one repo's map failed, and the stopped build; the map in both phases; the `agent` and `first-workflow` states; serve down; the all-done move to Settings (§4.4). "running build 1118" becomes "running 7c81646, the installed one".
6. `stale-line`: Stale, Runs open, Waiting, A new run started during the wait, Cancelled, Restarting, Restarted, Didn't come back, Still stopping (§4.5). Runs open lists §5.0's open set, not only agent sessions: "Nightly gate (run 14, an agent step)", "Review loop on #3120 (run 3, waiting on your approval)" and "Code index of agentic-workflow (a job)", each a link to its run or job page. Replace "build 1112 → 1118" with "7c81646, built today 10:31".
7. `states`: every row of §4.6, with the "Create my profile" panel replaced by "Connect your accounts", and the stale panel's "build 1112 → 1118" replaced by "7c81646, built today 10:31". The Home hello-sindri panel in each of its states: being made, running (step 2 of 4, started by itself), succeeded (the finish line with the map-still-building line), failed (Open the run, Run again), and own rows present (hello-sindri as a secondary line); Home with a failed map build; every panel after the first Add with the full top bar. Home's "a profile with no repo" panel with Find my repos.

`sindri setup --apply` and the other CLI verbs have no frame: they are CLI only, and their output is in §5.1 and §6.

## 15. Open questions for the owner

None open. Meta answered the four of round 1 for the owner (2026-11-12), and each is now a decision:

1. Own OAuth apps for GitHub (device flow, no secret) and Linear, a token paste fallback for a blocking org, the client id as a profile key: decisions 13, 15 and 15a.
2. A restart with runs open waits only, and links each run to its run page: decision 11.
3. `--apply` with no `--repo` adds the pre-ticked repos and prints each: decision 10.
4. Slack is a guided token paste, skippable in one click, never required: decision 15.

The one owner step left is the registration of the two apps (decision 15a).

## Review notes

Round 1 (/autoplan, 2026-11-12; files in `$AW_DIR/plans/first-run-r1/`; spec read at 58c8b4471a). Verdicts: product RETHINK (2 HIGH), arch NEEDS_WORK (5 HIGH, 22 untagged numbers), design NEEDS_WORK (5 MUST), devex NEEDS_WORK (6 HIGH), security NEEDS_WORK (1 HIGH, 3 MUST). 19 MUSTs after merging duplicates; all applied:

- product C1, own-data metric: F1b and two numbers in §1.
- product C2, arch F2 and devex 2, `known` can't pre-tick on a fresh box: accounts move to question 1, `known` reads the GitHub account (decision 4, gap 7).
- product C3, Q2 not timed: F1c and the app walk's accounts line (§8).
- product C4, admin approval: decision 16 and the "Waiting on admin" row.
- arch F1, codes taken: new codes are `-008` to `-012`; P0 retires the Onyx doc rows (§6).
- arch F3, restart second half: the app holds the wait, the runs set is fixed at the click, serve keeps no state (decision 11).
- arch F4 and design 1, hello-sindri never made or run: the setup service creates and activates it; Home's first-run primary runs it (decision 19, E2).
- arch F5, no home for a skip: `setup.skip` in the profile; `first-workflow` done is derived (§3.1).
- arch F6, untagged numbers: §2.1 tags each one.
- design 2, question 2 had no finish: Continue after a connect (§4.2).
- design 3 and 4, two primaries: decision 22; the add-failed row's only primary is Retry.
- design 5, missing states: `agent` with no agent, `first-workflow` running and failed (§4.4).
- devex 1 and security 1, Desk does not refuse sessions: restart and connect check the human token (decision 11, A4, A8).
- devex 3, F8 vs MCP: `--apply` leaves MCP to the setup service (decision 10).
- devex 4, no "owner only" end: exit 3 and `Cli = null` (decision 9).
- devex 5, `--apply` contract: the `--json` document, `already`, no wait for the index, exits (§5.1).
- devex 6, MCP unreachable on a fresh box: CLI is the first-run path; only `setup_get` is new (decision 25).
- security 2, pasted tokens: decision 17 and A8.
- security 3, origin with a token in it: the scan returns host and slug only (§3.3).
- security 4, launched sessions scan the home: refused, root capped, no symlinks (§3.3).

Suggestions, resolved in writing (no second loop):

- product C5, say what is not imported: applied (§9).
- product C6, the tab nags forever: applied, Dismiss on hello-sindri (§3.1).
- product, depth 3 misses deeper repos: not applied; "Look in another folder…" covers it (§9).
- product, many repos start many builds: applied as gap 8.
- product, a "map is ready" row to come back to: not applied; the map row on Home and Setup turns Done, which is that signal.
- arch F7, older app vs new ids: applied (§3.1).
- arch F8, scan cancel and symlinks, MCP can't stream: applied (§3.3); no scan MCP tool.
- arch F9, child serve's stderr: applied, `<home>/logs/serve.log` (decision 2).
- arch F10, pruned build folder and no stamp in dev: applied (§3.2, decision 12).
- arch F11, device flow is new plumbing: applied as gap 3.
- arch F12, re-pin to current main: applied (9483c8d8c0).
- arch F13, aot-smoke for P0 and P3: applied (gate notes).
- design, step title phrasing: not applied; titles are the route's and change in one place when design names them.
- design, device-code state: applied (§4.2).
- design, screen-reader announcements: applied (§4.2, §4.7).
- design, sub-line contrast: not applied here; X1's frames use the grammar's `ink-subtle`, as the Workflows spec does.
- design, Esc after a write: applied (§4.3).
- design, repo with no origin: applied (§4.3).
- devex, exit 1 and 2 mix: applied; each new code names its exit (§6).
- devex, `restart --wait` timeout: applied, `--timeout` (§5.1).
- devex, `setup scan` needs serve: applied (§3.3).
- devex, `setup` with serve down changes from an error to a list: kept; the exit stays 1, only the text grows (decision 8).
- devex, `Cli` null for owner actions: applied (§3.1).
- devex, docs plan: applied, X5.
- devex, timed run failure output and exit for slow: applied, exit 4 (§8).
- security, fence all free text: applied (§5.1).
- security, gh scopes shown and only humans write the credential: applied (decision 13, 14).
- security, resolve `sindri` beside the app: applied (decision 2).
- security, Linear reuses `state` and PKCE: applied (decision 15).
- security, refuse an Origin header on the scan: not applied; serve is loopback only and the threat model is an honest mistake, not a hostile page.
- security, refuse over auto-add for `--apply`: not applied; meta picked auto-add with each added repo printed (§15 pick 3).

Round 2 (/autoplan, 2026-11-12; files in `$AW_DIR/plans/first-run-r2/`; spec read after cf80f4eaa5). Verdicts: product SHIP (C1 to C4 closed, 0 MUST), arch NEEDS_WORK (5 HIGH, 7 MUST), design NEEDS_WORK (6 MUST), devex NEEDS_WORK (4 MUST), security NEEDS_WORK (1 MUST, MEDIUM). 15 MUSTs after merging duplicates; all applied:

- arch M1 and devex 1, the human token does not refuse an agent the owner started: on main that agent is the owner's caller by design; the spec now refuses only launched sessions, and exit 3 covers the browser and paste steps (§0, decisions 9 and 11, F8, §5.1).
- arch M2 and design 4, a run that starts mid-wait: the final restart refuses again and the line waits for the new run; Cancel goes back to Stale (decision 11, §4.5).
- arch M3, the new serve meets the old lease: the caller waits for the old pid and port first (decision 11, A4).
- arch M4 and devex 2, no writer for skip: two scalar keys and `POST /sindri/setup/skip` with `sindri setup skip` (§3.1, §5.1).
- arch M5, profile writers race, an older serve rejects new keys: decision 27.
- arch M6, hello-sindri recreated: a ledger event marks it made once; service grants named (E2).
- arch M7, untagged numbers: §2.1 now tags the rest.
- design 1, Continue never primary: Continue is the primary once GitHub connects (§4.2).
- design 2, a row with no button: decision 22 passes over it.
- design 3, question 1 outside first run: decision 28, the Accounts page with Done.
- design 5, Home's hello-sindri states: five rows in §4.6.
- design 6, paste fallback how-to and states, device code primary and expiry: §4.2.
- devex 3, `--apply` returns before the setup pass: it runs the pass and waits (decision 10, §8 step 3).
- devex 4, empty `setup.oauth.*`: `SND-SETUP-014`, the preset's `clientId` field, keys in A1, A8 and P0.
- security 1, one secret rule: decision 17 covers every token, device code and verifier, with a test per paste path.

Suggestions, resolved in writing:

- product N1, which read feeds own data: applied (F1b names the Reviews queue feed; 60 s marked a guess).
- product N2, F1b and F1c not in CI: not applied; CI holds no GitHub account.
- product N3, map ready notice: not applied, as round 1.
- product N4, a trace of skipped steps: applied in effect; the skip route is a catalog command, so the command audit records who skipped what.
- arch S1, a profile with no repo after question 1: applied; decision 28 shows question 2 while the profile names no repo, and Home's "no profile" row reads "no repo".
- arch, other suggestions (restart and doctor interplay, scan when GitHub is slow, device flow plumbing): already gaps 3 and 7; no change.
- design, two "Paste a token" labels: applied (Paste a GitHub, Linear, Slack token).
- design, contrast: not applied here, as round 1 (X1 uses `ink-subtle`).
- design, stale-line states announced: applied in effect; the page's live region announces state changes (§4.7).
- design, a failed repo row has no remove: not applied; the others are added and Home opens, so the failed row never blocks.
- devex, exit 1 meaning: applied (decision 9).
- devex, `--apply` no-`--repo` on a gh-only box: applied in effect; `known` reads the GitHub account, gh login included.
- devex, `already` only for the same path, `SND-JOB-409` unmapped: applied to P3's contract (same resolved path; a running AddRepo is followed, not refused).
- devex, exit table and `--json` device code for `setup connect`, `setup scan --json` shape: left to P0's contract, which writes the goldens.
- devex, let a session use `--token-env`: applied (§5.1, owner-started sessions).
- devex, a 404 from an older serve maps to `SND-BUILD-001`: applied in effect; the shared older-serve line is that case (decision 21).
- devex, stable step keys for the timed run and §6 sorted: applied in P12's contract; §6 stays grouped by existing then new.
- security, name the owner check: applied (§0).
- security, `setup.oauth.*` owner-only and shape-checked: applied (A1).
- security, pin the gh command: applied in effect; the value is the literal `command:gh auth token`, resolved by `ForgeCredentialSpec` with no shell.
- security, Linear `read` only at first sign-in: not applied; meta's pick 1 takes the preset's scopes, and Linear writes (comments) are used by the Projects page.
- security, device flow polling rules: applied (§4.2).
- security, binary beside `sindri-ui`: already decision 2.
- security addendum (lead's ask, 2026-11-12), is `repo` the smallest GitHub scope: yes for an OAuth app; written into decision 15a with the reason, and the GitHub App path is gap 9. No MUST.
- security, actor on restart and connect events: applied in effect; both are catalog commands, and the command audit records the actor.

Round 3 (/autoplan, 2026-11-12; files in `$AW_DIR/plans/first-run-r3/`; spec read at e98d37e577, which is 4d54e9a4c9 plus the GitHub scope note). Verdicts: product SHIP (0 MUST), arch NEEDS_WORK (4 MUST, 2 HIGH), design NEEDS_WORK (5 MUST), devex NEEDS_WORK (1 MUST), security NEEDS_WORK (1 MUST, HIGH). 10 MUSTs after merging duplicates; all applied:

- arch 1, restart starts the wrong binary: both starts run `bin/cs/current/sindri` (decision 2).
- arch 2, the old pid is read from a removed `control.json`: the pid comes from the 202 body (`StopProcessResponse.Pid`); the wait is 30 s and never starts a second serve (decision 11).
- arch 3 and security 1, "open runs" missed workflow runs and jobs: the open set is agent sessions, workflow runs and jobs, a superset of the bash guard, with two new tests (decision 11, A4).
- arch 4, the CLI's wait: it loops on `SND-SETUP-009` like the app, and the 30 s rule covers a slow old pid (decision 11).
- design 1, the Accounts page primary per entry and Done's return: decision 28.
- design 2, Enter and Esc: focus moves to the primary; `Ctrl+Enter` always presses it; Esc per page (§4.7).
- design 3, expired device code: focus, announcement, in-place new code, Cancel kept (§4.2).
- design 4, the Setup example contradicted §3.1: `sources` is done once GitHub connects; the example, header, nav tab and fixtures say 6 of 8, and Run hello-sindri holds the primary (§4.4, §5.0).
- design 5, a spec pointer in the copy: the permissions are named (§4.2).
- devex 1, `--apply`'s timeout: `setupPass` in the document, `SND-SETUP-015` and `-016`, the exact stderr line, exits (§5.1, §6).

Checked as asked: a run that starts mid-wait is named and waited for, and nothing is stopped (arch, security: holds once the open set covers workflow runs and jobs, now applied). The `--apply` timeout has its line and exit (devex). The Accounts page and the expired code state have words, one primary, focus and next state (design).

Suggestions, resolved in writing:

- product S1, a missed F1b or F1c target: applied in effect; a miss is filed as an issue `Part of #3019` and does not block the PR (X2's rule).
- product S2, the OAuth apps must exist before F1c: applied; registering them (decision 15a) is the precondition of the app walk.
- product S3, a skipped-steps line: applied in effect; `sindri setup` already prints each skipped step with its state.
- arch S1 and security 1, a run started in the seconds of the stop: applied, serve refuses new starts between the check and the stop (decision 11).
- arch S2, the app closed after the 202: applied (decision 11, the Not running page recovers it).
- arch S3, F6's 15 s ignores stop time: kept; F6 times from the click with the measured 9.9 s start inside it, and P2 corrects it from the first run.
- arch S4, "a session" in `SND-AUTH-002`: applied, "a launched session".
- arch S5, lane labels as sources: kept; a lane's plan comment on #3019 is where the rule came from, and §2.1 cites it.
- arch S6, the `setup.first_workflow.created` event needs the event catalog test: applied to P8's gate (EventCatalogTests, as the team rule says).
- design, the Accounts sub-line: applied (decision 28).
- design, L268 against decision 22, and Home after hello-sindri succeeds with no own rows: applied in effect; the page follows decision 22, and Home then shows its truly-empty state with Open Workflows.
- design, contrast: not applied here, as rounds 1 and 2.
- devex, the 120 s against the 180 s budget: applied in §2.1.
- devex, a launched session on `--apply`: applied (`setupPass.state = refused`).
- devex, `Usage.cs` and `docs/csharp/sindri.md` text: already X5.
- security 2, audit entries for restart and connect: applied (A4; connect records the provider and account, never a token, decision 17).

Round 3 security addendum (lead's ask, 2026-11-12): does the fine-grained set match what the setup screen and the `SND-SETUP-014` fallback ask for? It did not. The screen and gap 9 asked for Pull requests read and write, Contents read, Issues read and Metadata read, but the Reviews spec §8 also needs Contents write (merge, update branch) and Checks read (the CI panel), and `-013` and `-014` named no set. Fixed: decision 15a defines the fine-grained set once (Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read), one Core constant feeds the copy and both errors, and gap 9 points at it. 1 MUST, applied.

Frames check (2026-11-12, frames at `pr-assets/first-run-3019` 7931c3baa3; `$AW_DIR/plans/first-run-r3/frames-check.md`): 3 spec-side fixes applied. Decision 28's Done turns primary once GitHub connects (the frame was right). §5.0 gains hello-sindri's steps with the 60 s code timeout, its failure line, the Still stopping pid and log path, and the Didn't come back reason. §4.2's device code row shows the time left. The 7 frame-side fixes went to design.


Meta UX pass (2026-11-12, `~/.agentic-workflow/meta/ux-pass-3236.md`; spec at 1bd63af697, frames at cf1ed88daf): FIX FIRST, 7 MUSTs, 4 picks, 10 mismatches, 9 SHOULDs. Spec side, applied:

- Picks a to d: decisions 2a (serve starts by itself), 13 (no question 1 with a gh login, the "Signed in as ada through gh · Change" line), 19 (hello-sindri runs by itself, Run again stays), 31 (Skip for now on every dead end). The happy path is 2 actions (§1).
- MUST 1, start routing and resume: decision 30.
- MUST 2, question 2's exits: Continue to Home after one add, Skip for now with none, Esc to question 1 (§4.3, §4.7).
- MUST 3, the finish line: the Home "hello-sindri succeeded" row and the map-still-building line (§4.6, §4.4).
- MUST 4, a failed map build: the fast-error sentence and the one-of-two rule (§4.4, Home's setup line).
- MUST 5, re-adding repos: decision 32, the Repos page.
- MUST 6, focus and keys: §4.7 (the primary's row, row 1, the Tab order, Esc first cancels or stops).
- MUST 7 is design's (the centred column); §4 now says question 1 and 2 are centred-column screens.
- Mismatches, spec side: 1 (focus, §4.7), 4 (one top bar per stage, decision 33), 7 (Save token turns primary once the field holds text, §4.2), 8 (Ctrl+Shift+R presses the line's primary, §4.7), 10 (sign-in cancelled, §4.2). Mismatches 2, 3, 5, 6, 9 and the frame half of 10 are design's (`$AW_DIR/plans/first-run-r3/design-redraw-ux.md`).
- SHOULDs taken (one line, plainly right): 2 (the pill says "Sindri running", decision 33), 3 (key hints only for keys that act, §4.7), 9 (CLI lines for Add repos and Skip for now, §5.1).
- SHOULDs open: 1 (fold the four no-action done steps into one line; plain-word step titles), 4 (`worktrees: 104` and `known` as people words), 5 (a no-network sentence for question 1 and the scan), 6 (an 800 px rule and frames), 7 (keep Dismiss on the no-agent panel: design), 8 (screen-reader names for question 2 rows).