# The first real context run

This is the runbook for the first time the C# Sindri indexes and maps a real repo, here this one (`agentic-workflow`),
and shows the result in the app. It is one script, `scripts/context-first-run.sh`, that you run once you decide to. It works
in a scratch home of its own and never touches your real Sindri home (`$AW_STATE_DIR/sindri-app`), its profile or its
port. Nothing in it is installed or pulled for you: it tells you what is missing and stops.

```
scripts/context-first-run.sh up --dry-run     # read this first: every command it would run, nothing run
scripts/context-first-run.sh up               # steps 0 to 7, with no stop: the profile is live as written
scripts/context-first-run.sh status           # read only
scripts/context-first-run.sh down             # stop, close the app, delete the scratch home
```

`up` and `continue` are the same command. Every step first checks whether its outcome already holds and skips itself if it
does, so you can run it again after any stop or failure. Exit codes: 0 done, 1 a step failed (the message names the step),
2 a usage error or a refused setup.

## Before you start

The script needs `git` and `jq`. It indexes the checkout it runs from (override with `FIRST_RUN_REPO_PATH`), never the
shared main checkout by derivation. That checkout must have a clean tree and a HEAD contained in `origin/main` or `main`
(the code index never builds from another branch), so run it from a clean detached worktree:
`git worktree add --detach ../aw-wt/first-run-main origin/main`, then `cd` there and run `scripts/context-first-run.sh`. For the code index's two optional layers it checks, and does not install:

| Needs | Why | Install |
|---|---|---|
| ollama, running, with the pinned model | the embeddings layer | `scripts/install-deps.sh`, then `ollama pull nomic-embed-text:latest` (the model is the one in `context/code-index.lock`) |
| the code-index graphify | the graph layer | `scripts/install-deps.sh` |
| bubblewrap (Linux) | graphify never runs unsandboxed | `scripts/install-deps.sh` (`sudo apt-get install bubblewrap`) |
| the pinned temporal binary | the workflow engine | `scripts/binaries-lock.sh fetch temporal` (`scripts/install-cs.sh` also fetches it) |

If you don't want a layer yet, pass `--no-embeddings` or `--no-graph`: the profile turns that layer off and the check is
skipped.

`./setup.sh` (or `scripts/install-cs.sh`) publishes the C# build into `$AW_STATE_DIR/bin/cs/` when its source changed, which can take several minutes. It also installs the pinned .NET SDK and links `sindri` into
`~/.local/bin` when those are missing. Set `SINDRI` to use a binary you built yourself.

## The steps

**0. Preflight.** Checks the list above and that no other process holds port 7481. Missing things are printed with their
install command, all at once, and the script exits 1 without changing anything.

**1. Scratch home.** Creates `$AW_STATE_DIR/sindri-app-firstrun` (mode 0700) with a marker file, `.first-run-scratch`. The
script refuses any other home name, the real home, port 7410, a home that is a link, and a home that holds files but has no
marker, because those look like a real instance.

**2. Profile.** Runs `sindri profile init`, then appends this repo as
the one repo, the `context-docs-refresh` service, an `index:` section (embeddings and graph on) and
`context: { publish: { waitForIndexMinutes: 15 } }`. It is written only when the home has no profile yet; edit
`$AW_SINDRI_HOME/profile/profile.yaml` first if you want something else (`docs/csharp/sindri-profile.md` has every key).
It then runs `profile validate --json` to get the hash.

**3. Nothing to approve.** Edits apply on save, so the profile written in step 2 is already the live one. The script prints
that and goes on to step 4.

**4. Serve.** `sindri --headless` starts the control plane and its Temporal dev server without opening the app, then
`sindri status --json` must say `running`. Logs are in `<home>/logs/serve.log`. `status` exits 1
while health isn't `healthy`; the script reads the `running` field, not the exit code.

**5. First code index build.** There is no build command by design (the build follows `main` by itself every minute).
The script runs `sindri services run code-index-main-move`, which ticks that service now, starts the `BuildCodeIndex` job and
follows it. Then it asks `sindri code-index status --repo agentic-workflow --json` every 10 seconds, for up to
`FIRST_RUN_INDEX_TIMEOUT` seconds (default 30 minutes), until the index is `ok`. What you see: the job lines, then
`waiting for the index (Ns): missing`, then `ok`. A graph or embeddings layer that can't run doesn't fail the build; its
layer reads `unavailable` with a code, and `sindri doctor` says how to fix it. Check `sindri code-index status --repo
agentic-workflow` afterwards for the layer words (`structure ok, graph ok, embeddings pending 31%`).

**6. Context map.** `sindri context map refresh` builds a draft map, cleans the review queue with the cleanup agent, and
promotes the draft as you (no typed phrase; see `docs/sindri/context.md`, "Refreshing the map with no human step"). With
the index built, the map isn't held for it. The script then reads `context map status --json`, which is the map's status with
the publish fields beside it: `state` must be `published`, `published-without-waiting` or `published-by-hand`. While it is
`held` or `overdue` the script waits up to 17 minutes (the 15 minute wait for the index plus two ticks); `published-stale` or
anything else fails the step. If no model answers, the job still finishes and says `cleanup unavailable`. (`context map build`
only makes a draft that you review and `promote` with a typed phrase; `refresh` is the no-human-step job.)

**7. The app.** Starts `sindri-ui` on the scratch home (`AW_SINDRI_UI`, else the newest
`$AW_STATE_DIR/bin/cs/*/sindri-ui`) and keeps its pid in `<home>/first-run/ui.pid`. Click Context in the rail: you
should see the map and, on Health, a Code index card for `agentic-workflow`. Skip it with `--no-ui`.

## Looking around and stopping

`scripts/context-first-run.sh status` prints the plane, the repo's index and the map, and starts
nothing. It exits 1 when the home doesn't exist.

`scripts/context-first-run.sh down` runs `sindri stop` (which stops the Temporal dev server too), closes the app it opened
by its recorded pid, checks nothing still listens on the port, and deletes the scratch home. It does not remove the
published build under `$AW_STATE_DIR/bin/cs`, the pulled ollama model or the graphify environment. On a missing home
it says `nothing to clean` and exits 0. If something still listens on the port after `sindri stop`, it keeps the home, names
the check to run (`lsof -nP -iTCP:7481 -sTCP:LISTEN`) and exits 1; stop that process by hand.

## Things I could not confirm from the code and docs

These are marked here so the first real run checks them, not assumes them:

- `sindri context map status --json` is read from the code (`ContextMapText.WithPublish`, `MapPublishStatusRules`): the answer
  to "no map yet" is an error answer, assumed here to be a non-zero exit; not run against a live host.
- `sindri code-index status ... --json` is assumed to exit 0 only when the repo is `ok`, as `CodeIndexVerbs` says.
- `sindri-ui` takes the home from `AW_SINDRI_HOME` and the port from `AW_SINDRI_PORT` in its environment, and a bare
  start shows the last page; there is no flag for the Context page, only the deep link `context?item=<ITM-id>`.
- The first `sindri services run code-index-main-move` is assumed to start a build when the repo's checkout is contained in `main`
  and has no index; if the build fails, the service retries it with backoff and the wait loop keeps polling until the timeout.
