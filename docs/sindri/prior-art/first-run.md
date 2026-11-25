# Prior art: first run

This is the prior-art ledger for Sindri's first run, written under the rule in #2989. It lists 7 daily problems of a new install, who solves each best, what we copy, and what Sindri does on origin/main today.
Every "Best today" line ends with `source: docs` (a docs page I read, or a search snippet where marked) or `source: capture` (my screenshot). I took no screenshots, so every line is `source: docs`. No size or timing here comes from an image, so there is no `guess`. Anything I could not read says "unconfirmed".
The surface's job: take a person from a fresh box to a first screen with real rows from one repo, with no doc and no YAML. Direction: #3019 (read with its ui plan comment).

Coverage gaps, said up front: Superhuman's help center (help.superhuman.com) returned 403 to my fetcher, so its sync screen is unconfirmed. Cursor's current docs dropped the indexing detail (see problem 4). Raycast's first-launch screens are not in its manual. Linear's empty states are not in its docs.

## 1. Start the app on a fresh box without typing setup commands

**Best today**
- Tailscale: `tailscale up` "connects your device and authenticates if needed"; `--qr` prints a QR code for the web login URL. One command, then a browser. https://tailscale.com/kb/1080/cli (source: docs)
- Conductor: docs open on "Get Conductor running on your Mac". Next screen: add a repo, then chat with an agent. No config file named. https://conductor.build/docs (source: docs)
- Vibe Kanban: `npx vibe-kanban`, prerequisite "authenticated with your favourite coding agent". README now carries a "sunsetting" banner. https://github.com/BloopAI/vibe-kanban (source: docs)

**What we take**: one start path that leaves the user in a window. Bare `sindri` already does this. Anything the app can't do yet shows as a screen with one button, never as a CLI line to copy (Tailscale prints a URL and waits; it doesn't print a manual).
**Where one app wins**: none of these knows the agent CLIs, repos and chat accounts at once; Sindri can check all of them in one pass.
**Desktop gain**: first window paints from the local home in < 100 ms, before serve answers (perf can time it).
**Sindri today**: setup-once spec (2026-10-19, rev 4) promised 1 command; main has `sindri setup` (read-only step list) and `Views/Setup/`. A home with no `profile.yaml` still needs `sindri profile init` and `repo add` typed (#3019, "Why now"); `doctor` crashes on an older home (#2954).
**Verdict**: take and change. Copy Tailscale's "one command, then the browser does the rest", but our command is the app itself.

## 2. Tell it which repos, without typing paths

**Best today**
- GitHub Desktop: File > Add Local Repository > Choose... > Add Repository; dragging a folder onto the window also adds it. No folder scan documented (unconfirmed). https://docs.github.com/en/desktop/adding-and-cloning-repositories/adding-a-repository-from-your-local-computer-to-github-desktop (source: docs)
- Conductor: three buttons: Open project (existing local repo), Open GitHub project, Quick start (new repo). Needs `gh auth status` to pass. Scan: unconfirmed. https://conductor.build/docs/first-workspace (source: docs)
- Zed: no repo registry; you open a folder. Nothing to scan. https://zed.dev/docs/getting-started (source: docs)

**What we take**: nobody in this set scans for repos, so a scan with checkboxes (#3019 item 1) is ours to design. Take GitHub Desktop's drag-a-folder-onto-the-window as the fallback beside the scan, and Conductor's "from GitHub" as a second source.
**Where one app wins**: the scan can pre-tick repos that have open PRs or Linear issues, because Sindri already sees those.
**Desktop gain**: scan of `~/` to depth 3 under 2 s with a visible list that fills as it finds repos (perf can time the first 10 hits).
**Sindri today**: `SetupRepoForm` (`src/Sindri/Aw.Sindri.Ui/Views/Setup/SetupRepoForm.axaml`) has a path field only; no scan route (#3049). `repo add <path>` and `repo onboard` exist in the CLI.
**Verdict**: take and change (scan is new ground).

## 3. Connect GitHub, Linear and Slack in one sitting

**Best today**
- Linear + GitHub: Settings > Features > Integrations > GitHub > Enable, pick the org, pick All or Select repositories, Install, then authenticate your personal GitHub account. Six clicks, an org owner must do it. https://linear.app/docs/github (source: docs)
- Linear + Slack: same Settings page; "You must be a Linear admin to complete this step". https://linear.app/docs/slack (source: docs)
- Superhuman: add another account from settings, "sign in"; when onboarding ends you land on a Dashboard that asks you to review settings before the inbox. https://help.superhuman.com/hc/en-us/articles/46183279736461-Superhuman-Mail-for-Gmail (source: docs, search snippet; page body unread)
- Raycast: extensions install on Enter, "added to Raycast instantly"; in-place account connect unconfirmed. https://manual.raycast.com/extensions (source: docs)

**What we take**: a connections list, one button per account, each row shows state (not connected / connected as X / expired). Linear's six steps are the number to beat: ours is one button then the browser.
**Where one app wins**: once all three are linked, one sign-in screen can explain what each connection unlocks on Home (a Slack thread on its PR).
**Desktop gain**: the token sits in the OS keychain; reconnect check answers from the local store with no round trip (< 50 ms).
**Sindri today**: the Connections page (`src/Sindri/Aw.Sindri.Ui/Views/Connections/ConnectionsPage.axaml`, `AddConnectionForm`) adds a connection from a preset picker plus a pasted key or server address, and Linear has its own connect section (`Views/Linear/LinearConnectionSection.axaml`). Setup has no connections step, and nothing signs in through the browser. `Turn on for your repos` is an existing setup row (#2938 is a bug on it).
**Verdict**: take and change.

## 4. See that the first map is building, and how far along

**Best today**
- Cursor: older docs put status under Settings > Indexing & Docs, with a progress view that reaches "100% completed" per a forum thread; current docs dropped the indexing detail and a forum thread says the tab was missing in 3.11 and returned in 3.12.10. https://docs.cursor.com/context/codebase-indexing and https://forum.cursor.com/t/indexing-tab-missing-in-settings/165908 (source: docs, via search snippets; the zh-Hant copy was the one with the status wording)
- Cursor (current): Instant Grep "builds and queries its index on your machine". Says nothing on progress or on search before it finishes: unconfirmed. https://cursor.com/docs/context/codebase-indexing (source: docs)
- Docker Desktop: a **Starting** page, and a 4.98.0 fix for it "staying on the Starting page after Docker Engine had started", so the state is a known place users get stuck. https://docs.docker.com/desktop/release-notes/ (source: docs)
- Zed: docs mention no indexing state: unconfirmed. https://zed.dev/docs/finding-navigating (source: docs)

**What we take**: the lesson is negative. Cursor's progress view came and went, and Docker's state sticks. A building state needs a number (files n/N), an age, a way to leave the page, and a timeout that turns it into an error with a button.
**Where one app wins**: Home can say "map building, 6,700/14,016 files" and still list PRs and issues that need no map.
**Desktop gain**: progress lines come from the local ledger; a repaint every 250 ms with no network.
**Sindri today**: `MapSetupReader` already reads a running build and its n/N for the setup row. The Context page still shows "No map yet" with Build map and no progress (#3015); `sindri status` says healthy while the index rebuilds (#2945); `context-first-run.sh` step 6 fails when the map service already started a refresh (#3010).
**Verdict**: ours is behind its own setup row. Fix by reading the same fact (#3015).

## 5. Fix a stale or expired thing with one button

**Best today**
- Tailscale: expired key stops connections; fix is `tailscale up --force-reauth`, or "Reauthorize machine" in the admin console. A tray warning is unconfirmed. https://tailscale.com/kb/1028/key-expiry (source: docs)
- Docker Desktop: 4.96.0 added a **Check for updates** button in the Software update drawer; a bell icon opens a notifications center with "new releases, installation progress". A restart-to-apply wording was not found. https://docs.docker.com/desktop/release-notes/ and https://docs.docker.com/desktop/use-desktop/ (source: docs)
- Docker Desktop: a Troubleshoot icon in the Dashboard header "to debug issues or restart Docker Desktop". https://docs.docker.com/desktop/use-desktop/ (source: docs)

**What we take**: a fixed place (top bar) for "a newer Sindri is installed, Restart", and an expired connection as a row with Reconnect. Docker's Troubleshoot icon in a fixed header spot is the shape for "restart serve".
**Where one app wins**: the restart can say which runs it keeps (open runs guard from `install-cs.sh`).
**Desktop gain**: version mismatch is detected at connect, one local compare, no polling.
**Sindri today**: `doctor` throws when serve is older than the CLI (#2954); no restart route or top-bar notice (#3052); needs-you rows exist for setup.
**Verdict**: take and change.

## 6. Not start from an empty room (import, demo data)

**Best today**
- Zed: "During setup, you have the option to import key settings from VS Code"; extensions and keybindings are not imported. https://zed.dev/docs/migrate/vs-code (source: docs)
- Raycast: first launch of v2 on a Mac with v1 prompts a transfer that carries your account sign-in. https://manual.raycast.com/import-export (source: docs)
- Linear: start guide offers a demo workspace; importer steps are Setup, Review, Choose what to import, Map users, Confirm; admins only. https://linear.app/docs/start-guide and https://linear.app/docs/import-issues (source: docs; demo content unread)

**What we take**: after repos and connections, show real rows from them first (our data is the demo). For what we can import, import Claude Code / Codex session history and existing `.agentic-workflow/` config as a one-click, with the list of what is and isn't carried (Zed names what it skips).
**Where one app wins**: no tool imports from Slack, Linear and PRs together.
**Desktop gain**: first Home paints from rows already fetched during setup, no second load.
**Sindri today**: nothing imports; `sindri agents` lists live sessions read-only.
**Verdict**: skip import for now; take Zed's "say what is not imported" habit.

## 7. Land on one useful screen when setup ends

**Best today**
- Superhuman: ends onboarding on a Dashboard. Sync time and sync screen: unconfirmed (help center 403). https://help.superhuman.com/hc/en-us/articles/46183279736461-Superhuman-Mail-for-Gmail (source: docs, search snippet)
- Conductor: after adding a repo, "creates a workspace from it", then you start a chat. https://conductor.build/docs/first-workspace (source: docs)
- Linear: join and view without a team; no required setup questions counted (unconfirmed). https://linear.app/docs/joining-your-team-on-linear (source: docs)

**What we take**: the last setup step lands on Home with at least one real row and the next action named. The spec's own words: "Open a repo's map" or "Start a work item". Time to that screen is a number CI records.
**Where one app wins**: Home mixes PRs, issues, threads and agents on first open.
**Desktop gain**: target < 3 min from fresh home to Home with rows on this repo, measured by the scratch-home run (#3019 item 5). The setup-once spec's old figure (app opens after 10 to 15 minutes, 45 to 75 minutes total) is from 2026-10-19 and is not re-measured.
**Sindri today**: `context-first-run.sh` is the start of the measurement; it is red (#3010). The timed run is #3051.
**Verdict**: take and change.

## Not solid
- Superhuman's sync screen and 1:1 onboarding: help center blocked, unconfirmed.
- Raycast's first-launch screens: not in its manual.
- Linear's empty states and question count: not in its docs.
- Cursor and Zed: no current doc for a progress view.
- Nobody scans a disk for repos (GitHub Desktop, Conductor: unconfirmed).

| problem | best tool | verdict | Sindri today (issue) |
|---|---|---|---|
| 1 start without commands | Tailscale `up` | take and change | partial; home with no profile still CLI (#3019, #2954) |
| 2 pick repos | GitHub Desktop drag + Conductor buttons | take and change (scan is new) | path field only (#3049) |
| 3 connect accounts | Linear integration pages | take and change | pasted-key form, not in setup (#3050) |
| 4 map-build progress | Cursor (unstable), Docker (sticks) | ours behind own setup row | #3015, #2945, #3010 |
| 5 one-button fix | Docker, Tailscale | take and change | #2954; no restart route (#3052) |
| 6 not an empty room | Zed import | skip, copy "what is not imported" | missing |
| 7 land on a useful screen | Superhuman Dashboard, Conductor | take and change | #3010 red; timing #3051 |

## What we take
- Tailscale-style one start, browser does the rest: bare `sindri`, no YAML. #3019
- Repo scan with checkboxes plus drag-a-folder fallback (GitHub Desktop): #3049
- One connect button per account, state on the row (Linear's six clicks beaten): #3050
- Building state shows n/N files and age, no Build button while running: #3015
- Health says "indexing" while rebuilds run, so status never lies: #2945
- Top-bar "newer Sindri installed, Restart" and doctor on old homes: #3052, #2954
- Fresh home to Home with rows, timed in CI: #3051 (red run: #3010)
