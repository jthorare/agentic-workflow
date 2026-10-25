# The first real context run

This is the runbook for the first time the C# Sindri indexes and maps a real repo, here this one (`agentic-workflow`),
and scopes a first item on it. It is one script, `scripts/context-first-run.sh`, that you run once you decide to. It works
in a scratch home of its own and never touches your real Sindri home (`$AW_STATE_DIR/sindri-app`), its profile or its
port. Nothing in it is installed or pulled for you: it tells you what is missing and stops.

```
scripts/context-first-run.sh up --dry-run     # read this first: every command it would run, nothing run
scripts/context-first-run.sh up               # steps 0 to 3, then it stops for your approval
scripts/context-first-run.sh continue         # after you approve: steps 4 to 8, then it may stop once more
scripts/context-first-run.sh continue         # after you approve the recipes: step 9
scripts/context-first-run.sh status           # read only
scripts/context-first-run.sh down             # stop, close the app, delete the scratch home
```

`up` and `continue` are the same command. Every step first checks whether its outcome already holds and skips itself if it
does, so you can run it again after any stop or failure. Exit codes: 0 done, 1 a step failed (the message names the step),
2 a usage error or a refused setup, 3 stopped for you to approve something.

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
skipped. Onyx is optional and not needed here; `sindri onyx status` tells you whether the box could run it.

`./setup.sh` (or `scripts/install-cs.sh`) publishes the C# build into `$AW_STATE_DIR/bin/cs/` when its source changed, which can take several minutes. It also installs the pinned .NET SDK and links `sindri` into
`~/.local/bin` when those are missing. Set `SINDRI` to use a binary you built yourself.

## The steps

**0. Preflight.** Checks the list above and that no other process holds port 7481. Missing things are printed with their
install command, all at once, and the script exits 1 without changing anything.

**1. Scratch home.** Creates `$AW_STATE_DIR/sindri-app-firstrun` (mode 0700) with a marker file, `.first-run-scratch`. The
script refuses any other home name, the real home, port 7410, a home that is a link, and a home that holds files but has no
marker, because those look like a real instance.

**2. Profile.** Runs `sindri profile init` (which lists the effects the shipped recipes declare), then appends this repo as
the one repo, the `context-docs-refresh` service, an `index:` section (embeddings and graph on) and
`context: { publish: { waitForIndexMinutes: 15 } }`. It is written only when the home has no profile yet; edit
`$AW_SINDRI_HOME/profile/profile.yaml` first if you want something else (`docs/csharp/sindri-profile.md` has every key).
It then runs `profile validate --json` to get the hash.

**3. Your approval.** Approving a profile is human-only: a session is refused with `SND-AUTH-002`. The script prints the
exact command and exits 3:

```
AW_SINDRI_HOME=<home> AW_SINDRI_PORT=7481 <sindri> profile approve <hash>
```

Type it at your own terminal. It shows what changed and asks you to type `approve profile <first six characters>`. Then run
`scripts/context-first-run.sh continue`. What to check: the key paths it lists are the repo, the service, `index` and
`context`, and nothing else.

**4. Serve.** `sindri --headless` starts the control plane and its Temporal dev server without opening the app, then
`sindri status --json` must say `running` with the profile approved. Logs are in `<home>/logs/serve.log`. `status` exits 1
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

**8. Your approval of the recipes.** The scoping recipe has to be approved before it can start. If
`sindri recipe list` doesn't show `workflow.scoping@… approved`, the script prints `sindri recipe approve-all` and exits 3.
Type it at your terminal; it prints the plan and asks for a phrase. Then `continue` again.

**9. An item and the scoping recipe.** Writes `<home>/first-run/scope-input.json`
(`{"kind":"brief","repo":"agentic-workflow","path":"docs/sindri/README.md"}`, or your `FIRST_RUN_BRIEF`), runs
`sindri item add "First context run: scope <file>" --repo agentic-workflow --no-start` (`--no-start` means do not start the
control plane for this call; it is running, so a stopped plane fails loudly; it has nothing to do with recipes), then
`sindri workflow start workflow.scoping --item <ITM-id> --fact fact:scopeInput=<file>`. The item id is kept in
`<home>/first-run/item.id`, so a second run doesn't add another. The scoping run reads the brief, gathers the four lenses
from the map and the code index, drafts a scope, and stops at your after-scoping desk step. Open it in the app (Work), or
`sindri-ui 'context?item=<ITM-id>'` for the item's Context page.

## Looking around and stopping

`scripts/context-first-run.sh status` prints the plane, the repo's index, the map and the scoping instance, and starts
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
- `item add` only appends `item.created` (`CreateItemService`); nothing in the code reacts to it by starting a recipe, so the
  scoping recipe is the first workflow on the item. Not run live.
- `sindri-ui` takes the home from `AW_SINDRI_HOME` and the port from `AW_SINDRI_PORT` in its environment, and a bare
  start shows the last page; there is no flag for the Context page, only the deep link `context?item=<ITM-id>`.
- The first `sindri services run code-index-main-move` is assumed to start a build when the repo's checkout is contained in `main`
  and has no index; if the build fails, the service retries it with backoff and the wait loop keeps polling until the timeout.
- `workflow.scoping`'s `scopeInput` is the `brief` shape; the brief path is relative to the repo.
