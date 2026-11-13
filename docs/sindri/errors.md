# Sindri error codes

Generated from `sindri/src/errors.ts` by `cd sindri && npm run gen`. Do not edit by hand.

| Code | Meaning | Fix |
|---|---|---|
| `SND-CLI-001` | Unknown command. | sindri help |
| `SND-CLI-002` | Invalid arguments for this command. | Run `sindri help` and check the command's flags. |
| `SND-CLI-900` | Unexpected internal error (a bug). | rerun with SINDRI_DEBUG=1 and report the output |
| `SND-EVOLVE-001` | The toolkit repo couldn't be read as a git repository. | check the ring-0 repo's path in repos/&lt;name&gt;.yaml |
| `SND-EVOLVE-002` | No offline comparison exists for that artifact yet. | only the scope.draft prompt can be compared in this release (spec amendment 4) |
| `SND-EVOLVE-003` | A pull request couldn't be read with gh. | check the PR number and `gh auth status` |
| `SND-EVOLVE-004` | That proposal can't be adopted. | sindri evolve show &lt;id&gt; says what is missing: a won comparison, the safety clause, or a clean leak check |
| `SND-EVOLVE-005` | That channel change isn't allowed yet. | sindri channel status shows why and what to run |
| `SND-EVOLVE-006` | That action needs an interactive terminal. | run it yourself in a terminal |
| `SND-EVOLVE-007` | The confirmation didn't match. | rerun and type the characters shown |
| `SND-EVOLVE-008` | No artifact in the registry has that id. | sindri evolve status lists the registered artifacts; sindri evolve init refreshes the registry |
| `SND-EVOLVE-009` | The judge model must differ from the model that wrote the outputs. | set models.adjudicator to a different model than models.scoping |
| `SND-EVOLVE-010` | The artifact registry is empty. | sindri evolve init |
| `SND-EVOLVE-011` | The toolkit repo has no GitHub remote called origin. | git remote add origin &lt;github url&gt; in the toolkit repo |
| `SND-EVOLVE-012` | That pull request can't be reflected on. | reflect only reads merged PRs by an allowed author (evolve.prAuthors) |
| `SND-EVOLVE-013` | gh returned a reply in an unexpected shape. | update gh, then rerun |
| `SND-EVOLVE-014` | Publishing writes into the working tree, which is on the default branch. | git switch -c docs/sindri-proposals-&lt;week&gt;, then rerun sindri evolve publish |
| `SND-EVOLVE-015` | The privacy gate has no private terms to check against. | add your workplace's names to privacy.denyTerms in the private profile; or pass --no-privacy-terms to publish with the private-terms layer skipped (the other five layers still run) |
| `SND-EVOLVE-016` | That proposal's status doesn't allow a comparison. | sindri evolve show &lt;id&gt; prints its status; a rejected, adopted, merged or staged proposal is never compared again |
| `SND-EVOLVE-017` | A private repo has no usable built code index (none, unbuilt or damaged), so publish can't check proposals for its identifiers. | sindri index build --repo &lt;name&gt; (remove a damaged index file first), then rerun sindri evolve publish |
| `SND-EVOLVE-018` | The English word list publish uses to tell private identifiers from ordinary words is missing. | install a word list at /usr/share/dict/words (or point SINDRI_WORDS at one), then rerun sindri evolve publish |
| `SND-EVOLVE-019` | Publish couldn't list a private repo's tracked files, so it can't check proposals for their names. | check that the repo file's path is that repo's git checkout on this machine, then rerun sindri evolve publish |
| `SND-INDEX-001` | All heavy-job slots are busy. | wait for the holder to finish; `sindri doctor` shows it |
| `SND-INDEX-002` | The repo path is not a git repo. | check repos/&lt;name&gt;.yaml path |
| `SND-INDEX-003` | The index input is larger than index.maxTotalMB. | add generated or vendored paths to index.denyPaths, or raise index.maxTotalMB |
| `SND-INDEX-005` | The embedding URL is not loopback. | set index.embeddings.url to http://127.0.0.1:11434 (or disable embeddings) |
| `SND-INDEX-006` | The local embedding server failed. | sindri index setup (starts Ollama checks and pulls the model) |
| `SND-INDEX-007` | The profile still has a `context.sources.onyx` section; Sindri no longer runs or reads Onyx. | remove the `onyx` entry under context.sources in profile.yaml |
| `SND-INDEX-008` | graphify is missing, failed or wrote no usable graph. | if graphify or its sandbox is missing: sindri index setup; if graph.json is over the cap: raise index.graphMaxMB (max 512); then sindri index build |
| `SND-INDEX-009` | A source token pointer does not resolve, the source refused the token, or its file is wider than 0600. | run sindri index connect &lt;source&gt; at your terminal; for a file: pointer, chmod 600 the file |
| `SND-INDEX-010` | A repo the profile lists is unreadable with this token; the message names each one. | give the fine-grained token read access to those repos (Contents, Issues, Pull requests, Metadata), then sindri index connect github |
| `SND-INDEX-011` | The source, repo or host is not in the profile; no request was sent. | add it under context.sources in the profile |
| `SND-INDEX-013` | A Slack channel is not in the profile, or is a direct message; no request was sent. | add the channel id to context.sources.slack.channels |
| `SND-INDEX-014` | `sindri onyx down --purge` could not remove a container, volume, keychain entry or the onyx/ directory an old `onyx up` left. | start Docker (or fix the named step) and run `sindri onyx down --purge` again; it resumes from what is left |
| `SND-INDEX-404` | No index has been built for this repo. | sindri index build --repo &lt;name&gt; |
| `SND-ITEM-404` | No such item in the ledger. | Run `sindri observe` to list items. |
| `SND-LEDGER-001` | The ledger was written by a newer sindri. | Upgrade sindri (`git pull` then `scripts/install-cs.sh`), then rerun. |
| `SND-LOCK-001` | Another sindri run holds the lock. | wait a moment and rerun; `sindri doctor` shows the holder |
| `SND-LOCK-003` | This run's fencing epoch is stale; another run took over. | Nothing to do; the newer run continues. Check `sindri doctor` if this repeats. |
| `SND-PROFILE-001` | The profile is invalid. | Run `sindri profile validate` and fix each listed key. |
| `SND-PROFILE-002` | No profile was found. | Run `sindri profile init`, pass --profile &lt;dir&gt;, or set AW_PROFILE_DIR. |
| `SND-PROFILE-003` | No such profile key. | See docs/sindri/profile.md for the keys. |
| `SND-PROFILE-004` | No such repo in the profile. | Use a name listed under repos in profile.yaml. |
| `SND-PROFILE-005` | The profile was written for a newer sindri. | Upgrade sindri (`git pull` then `scripts/install-cs.sh`). |
| `SND-PROFILE-006` | The hash doesn't match the current profile. | Run `sindri profile validate` for the current hash and retry with it. |
| `SND-PROFILE-007` | A profile already exists there. | Edit it, or pass --force to overwrite it. |
| `SND-PROFILE-008` | This repo has no plan files for --ring0. | Run --ring0 from a repo with docs/superpowers/plans, or run `sindri profile init` without it. |
| `SND-PROFILE-009` | Not inside a git repo. | cd into the repo first. |
| `SND-PROFILE-010` | Removed (#2258): there is no profile approval any more. | nothing to do; edits apply on save |
| `SND-PROFILE-011` | The approval was not confirmed. | rerun and type the first 6 characters of the hash |
| `SND-PROFILE-012` | Removed (#2258): a missing profile is `SND-PROFILE-001`. | sindri profile init |
| `SND-PROFILE-013` | That repo name is already used for another path or file. | pass --name &lt;another name&gt; |
| `SND-PROFILE-014` | That repo name is not valid. | use lowercase letters, digits and dashes (max 39) |
| `SND-PROFILE-015` | Removed (#2258): the live profile is what Sindri reads. | nothing to do |
| `SND-PROFILE-016` | This worktree has no main checkout to add (a bare repo, a separate git dir or a submodule). | pass the main checkout's path; a bare repo has no checkout to index |
| `SND-SCOPE-001` | No allowed provider can run scoping. | add anthropic to providers.allowed |
| `SND-SCOPE-002` | A model job failed, timed out or returned junk. | rerun; the message carries the CLI's own error. If it repeats, run `claude -p hello` to check the CLI and its login |
| `SND-SCOPE-004` | The model's answer didn't match the required shape. | rerun; the next round gets the reasons. Persistent: try another models.scoping |
| `SND-SCOPE-005` | The run's token budget is used up. | raise scope.maxTokensPerRun (or maxTokensPerBacktest) in the profile |
| `SND-SCOPE-010` | Linear rejected the token. | check sources.linear.token points at a valid read-only Linear API key |
| `SND-SCOPE-011` | Linear could not be read. | check the project URL and your network, then rerun; the message carries Linear's own error text |
| `SND-SCOPE-020` | The brief file can't be read. | check the path you passed to sindri scope |
| `SND-SCOPE-021` | No output directory for the scope map. | pass --out DIR or set sources.notesDir |
| `SND-SCOPE-022` | That section isn't in the document. | check the heading number (## 13. …) |
| `SND-SCOPE-023` | The project has no issues filed after its brief. | pick a project with later issues, or a shorter --window |
| `SND-SCOPE-024` | Linear isn't configured as a source. | add sources.linear.token (a secret pointer) |
| `SND-SCOPE-025` | Refusing to write a scope map into a git worktree. | use --sources file,code, or write outside the repo |
| `SND-SCRUB-001` | A profile scrub pattern does not compile. | Fix the regex at the named scrub.extraPatterns index, then run `sindri profile validate`. |
| `SND-SCRUB-002` | Staged changes contain likely secrets. | remove them (use a secret pointer or an env var); for a false positive, commit with --no-verify and say why |
| `SND-SCRUB-003` | A different pre-commit hook is already installed, or core.hooksPath points the hook at a directory sindri didn't create (it never writes there). | Add this line to that hook by hand: `sindri scrub --staged \|\| exit 1`. |
| `SND-SCRUB-004` | `scrub --staged` or `--install-pre-commit` ran outside a git repo. | cd into the repo first, or pass --repo PATH to --install-pre-commit. |
| `SND-SCRUB-005` | `git diff --cached` failed, so `scrub --staged` could not scan the staged changes. | Fix the git error shown in the details, then retry the commit. |
| `SND-SCRUB-006` | git's init.templateDir is set to a directory sindri does not own, or could not be read or set. | copy the named hook into that template dir's hooks/ yourself, or `git config --global --unset init.templateDir` and rerun `sindri repo onboard --template` |
| `SND-SECRET-001` | A secret pointer could not be resolved. | check the pointer in the profile and that the secret exists (env var, file, keychain item or 1Password item) |
| `SND-SECRET-002` | A secret file is readable by other users. | chmod 600 &lt;file&gt; |
| `SND-SECRET-004` | A secret couldn't be stored: neither the OS secret store nor the owner-only file took it. | on Linux install libsecret (secret-tool) or check the Sindri home is writable by you; on macOS unlock the login keychain; then run the command again |
| `SND-CONN-001` | No connection has that name. | `sindri connection list` shows the names |
| `SND-CONN-002` | The connection has to be signed in again. | run `sindri connection add &lt;name&gt;` again |
| `SND-CONN-003` | Nobody finished the browser sign-in in 5 minutes. | run the command again and finish the consent page |
| `SND-CONN-004` | The sign-in callback's state did not match this sign-in. | run the command again; do not reuse an old consent link |
| `SND-CONN-005` | The provider refused to refresh the token. | sign in again |
| `SND-CONN-006` | The connection's secret could not be stored, read or deleted. | see `SND-SECRET-004` |
| `SND-CONN-007` | The preset needs a client id and none was given. | pass `--client-id` |
| `SND-CONN-008` | The connection name is not `&lt;service&gt;:&lt;label&gt;` in lowercase. | use letters, digits and `-`, 1 to 32 characters per part |
| `SND-CONN-009` | Consent was denied at the provider. | run the command again and allow access |
| `SND-CONN-010` | A connection with that name exists. | pick another label, or `sindri connection remove` it first |
| `SND-CONN-011` | The connection uses a key, so it has no browser sign-in to run again. | `sindri connection remove` it, then add it with a new `--key-env` |
| `SND-CONN-012` | The provider's answer is not the shape Sindri expects, or a redirect left the preset's host. | run `sindri doctor`; if it repeats, the provider changed its API |
| `SND-CONN-013` | The provider rejected the saved poll cursor. | ask for a full re-read (the owner's call); Sindri does not re-read on its own |
| `SND-CONN-014` | The provider is rate limiting this connection (429 or 5xx on a poll). | nothing: the next poll waits for Retry-After, then doubles the wait on each repeat |
| `SND-CONN-015` | A rename was refused: an active workflow's `connection_event` trigger watches the connection, and the old name would stop it firing. The message names the workflows. | pause those workflows (`sindri wf pause <slug>`), rename, point their triggers at the new name, then activate them again |
| `SND-CONN-101` | A polled event found its run ids, five in a row, held by runs of other events. | nothing: the cursor stays and the next poll tries again; the event is never dropped |
| `SND-CONN-201` | The handoff inbox's answer breaks the inbox contract; the message names the field. | fix the inbox so it answers as `schemas/handoff/inbox.openapi.yaml` says (docs/sindri/handoff.md) |
| `SND-CONN-202` | Another caller claimed the offer first. Informational: nothing starts. | none; the next poll lists what is still open |
| `SND-CONN-203` | The handoff inbox has no such offer any more (unknown or expired). | none; the offer is gone |
| `SND-CONN-204` | The handoff inbox did not take the result. | check the inbox is up and the claim id is the one the claim returned; the submit is retried |
| `SND-CONN-205` | The handoff inbox is out of reach, did not answer in 30 seconds, or kept answering 429 or 5xx. | check the connection's base URL and that the inbox is up; list is tried again on the next poll |
| `SND-CONN-206` | The handoff inbox's host is not on the allowlist, or its URL is not https (http is allowed for loopback only). | use the inbox's https URL and add its host to the allowlist |
| `SND-MEM-001` | No memory has that name. | `sindri memory list` shows the names |
| `SND-MEM-002` | A memory with that name exists. | pick another name |
| `SND-MEM-003` | A fact is empty or longer than 2,000 characters. | shorten the text |
| `SND-MEM-004` | The embedding model did not answer, so recall used keywords (informational). | start Ollama and pull the pinned model (`sindri doctor`) |
| `SND-MEM-005` | The match level is not loose, balanced or strict. | use one of the three |
| `SND-MEM-006` | The memory name is not lowercase letters, digits and `-`. | use 1 to 48 of them, starting with a letter or digit |
| `SND-MEM-007` | A fact's expiry is not in the future. | give a later `--expires-at`, or none |
| `SND-RES-001` | A disk holds more than its size limit (the message names the bytes used and the limit). | delete files from the disk, or raise its `size` |
| `SND-RES-002` | Two databases, disks or connections of one owner share a name. | rename one |
| `SND-RES-003` | A purge was refused: the owner is still active or has a run going. | archive the owner, or wait for the run to end |
| `SND-RES-004` | A resource path leaves its owner's folder, or a symlink sits where a resource folder or file goes. | remove the symlink or the stray path, then run again |
| `SND-RES-005` | A sandbox size is above the box's cap. | lower the `sandbox` memory or wall in the declaration to the cap the message names |
| `SND-RES-006` | A resource owner, name or size is malformed, or a path exists as the wrong type (a folder where a database goes). | fix the declaration, or move the stray path away |
| `SND-RES-007` | A disk holds too many files and folders to measure (over 100,000). | delete files you don't need from the disk |
| `SND-RES-008` | A purge could not move or delete the owner's folder (a file is locked or not writable). The message names the folder; it is put back when it can be. | fix the permission or close what holds the file, then purge again |
| `SND-TK-001` | The toolkit is unbuilt, or its source changed since the last good build. | `sindri toolkit build <name>` |
| `SND-TK-002` | The toolkit's `dotnet build -warnaserror` or its tests failed; it stays `failed` and can't start. | read the build job's output, fix, build again |
| `SND-TK-003` | `toolkit.json` isn't the SDK's manifest, or a tool name can't become a client tool name. | rebuild with the toolkit SDK; keep `<toolkit>__<tool>` within 64 characters |
| `SND-TK-004` | The toolkit's process ended during a call. | `sindri toolkit start <name>`; three crashes in ten minutes mark it `failed` |
| `SND-TK-005` | The toolkit is paused; its process is kept and calls are refused. | `toolkit_resume` (or `sindri toolkit resume <name>`) |
| `SND-TK-006` | A tool call didn't answer within its timeout (60 s by default). | retry, or make the tool faster |
| `SND-TK-007` | The toolkit has no tool with that name. | `sindri toolkit list` shows the tools |
| `SND-TK-008` | This box has no sandbox for a plain process (no bubblewrap or Seatbelt), so a toolkit can't start. | `sindri doctor` names the missing piece |
| `SND-TK-009` | No toolkit has that name. | `sindri toolkit list` |
| `SND-TK-010` | A toolkit name (lowercase letters, digits, `-`) or tool name (lowercase letters, digits, `_`) is malformed, or `<toolkit>__<tool>` is over 64 characters. | shorten the name |
| `SND-TK-011` | A toolkit tool ran and answered with an error; the toolkit's own message is in the result. | read the message; fix the arguments or the toolkit |
| `SND-TK-012` | A toolkit with that name exists already. | pick another name, or `sindri toolkit list` |
| `SND-TK-013` | The toolkit generator failed; its own message is in the result. | read the message (no SDK package in the feed: run `scripts/install-cs.sh`) |
| `SND-TRACKER-001` | The tracker's source is missing or unreadable (for plan-file: the repo path or plan dir). | Restore that directory, or fix `path` in repos/&lt;name&gt;.yaml. |
| `SND-TRACKER-002` | The tracker's source holds no items to read (for plan-file: no plan file matches tracker.include). | Fix tracker.include in profile.yaml (or restore the plan files). |
| `SND-TRACKER-003` | A plan file is too big to read (over 2 MiB). | Split the plan file into smaller plans. |
| `SND-TRACKER-404` | The tracker has no such item. | Check the id with `sindri observe`. |
| `SND-TRACKER-405` | This tracker can't write. | The plan-file tracker is read-only; edit the plan file. |

## Retired (reserved, never reused)

These codes went with Onyx (#1598). They are never given to a new error, so an old log line can't be read as something else.

| Code | Was |
|---|---|
| `SND-ONYX-001` | Docker is missing or not running |
| `SND-ONYX-002` | Docker Compose v2 is missing |
| `SND-ONYX-003` | Not enough disk or Docker memory for Onyx |
| `SND-ONYX-004` | The Onyx pins are malformed or a download failed its pin |
| `SND-ONYX-005` | Onyx isn't ready or runs another version |
| `SND-ONYX-006` | Onyx's port is taken and no fallback port is free |
| `SND-ONYX-007` | The Onyx login given isn't an admin |
| `SND-ONYX-008` | Minting or verifying Sindri's Onyx key failed |
| `SND-ONYX-009` | Writing the Onyx key to a secret store failed (now `SND-SECRET-004`, for any secret) |
| `SND-ONYX-010` | Docker points at another machine |
| `SND-ONYX-011` | A docker compose command failed |
| `SND-ONYX-012` | Sindri can't use its Onyx directory |

## SND-SETUP (first run)

Words from the first-run spec (`docs/superpowers/specs/2026-11-12-sindri-first-run-design.md` §6). The app shows the sentence and puts the code in Details.

| Code | Meaning | Fix |
|---|---|---|
| `SND-SETUP-008` | The scan root is missing, unreadable or outside the home folder. | `sindri setup scan --root DIR` |
| `SND-SETUP-009` | Restart refused: agent sessions, workflow runs or jobs are open. | `sindri restart --wait`; `sindri wf run-show <run>` for each run |
| `SND-SETUP-010` | `sindri setup --apply` named no repo and none here is one you worked on in GitHub lately. | `sindri setup --apply --repo <path>` |
| `SND-SETUP-011` | The installed Sindri has no `build-info`, so it isn't finished installing. | `scripts/install-cs.sh --link` |
| `SND-SETUP-012` | Sindri didn't answer 30 seconds after a start or restart. | `sindri serve` |
| `SND-SETUP-013` | GitHub or Linear refused Sindri's OAuth app (an org policy). | Paste a token: `sindri setup connect <provider> --token-env NAME` |
| `SND-SETUP-014` | This build ships no client id for the provider and the profile sets none (`setup.oauth.github`, `setup.oauth.linear`). | Paste a token: `sindri setup connect <provider> --token-env NAME` |
| `SND-SETUP-015` | The setup pass of `sindri setup --apply` didn't finish within `--timeout`; it keeps running. | `sindri jobs show <id>`, then `sindri setup` |
| `SND-SETUP-016` | The setup pass of `sindri setup --apply` failed. | `sindri jobs show <id>` |

## SND-GRAPH (workflow graphs)

| Code | Meaning |
|---|---|
| `SND-GRAPH-001` | That is not a workflow graph (bad JSON, an unknown member or a wrong type) |
| `SND-GRAPH-002` | The workflow graph has problems; the message lists them |
| `SND-GRAPH-003` | Unknown block type |
| `SND-GRAPH-004` | A reference doesn't point at an upstream output |
| `SND-GRAPH-005` | A condition can't be read |
| `SND-GRAPH-006` | A cron or timezone can't be read |
| `SND-GRAPH-007` | This workflow changed since you read it. Read it again and retry |
| `SND-GRAPH-008` | No such workflow |
| `SND-GRAPH-009` | Another workflow already has that short id |
| `SND-GRAPH-010` | A workflow with that id already exists |
| `SND-GRAPH-011` | This workflow is archived; it can't be activated, edited or paused. Its slug stays reserved, so a copy needs another slug |
| `SND-GRAPH-012` | A graph edit could not be applied (the message names the op's number and kind) |
| `SND-GRAPH-100` | A run with that id already exists |
| `SND-GRAPH-101` | No such workflow run |
| `SND-GRAPH-102` | A block could not finish |
| `SND-GRAPH-103` | The model's answer does not fit the prompt's output schema |
| `SND-GRAPH-104` | Run controls that come back with the graph engine (`sindri workflow takeover` and `handback` of an instance); a `run_agent` node uses `sindri wf takeover` and `wf handback` |
| `SND-GRAPH-105` | This workflow is not active; activate a revision to run it |
| `SND-GRAPH-106` | A manual run's payload must be a JSON object |
| `SND-GRAPH-107` | This run already ended |
| `SND-GRAPH-108` | A referenced node output was cut at 64 KB |
| `SND-GRAPH-109` | A run of this workflow can't start the workflow again |
| `SND-GRAPH-110` | The run is not waiting on an agent at that node, so there is nothing to take over |
| `SND-GRAPH-111` | That agent node is not taken over, so there is nothing to hand back |
| `SND-GRAPH-113` | A prompt field came out empty |
| `SND-GRAPH-119` | That part of workflows is not built yet: the Workflows page contract routes (`POST .../test`, `POST .../pins`, `GET .../trigger-events`, `GET .../templates`) and the `replace_node` op answer this until their phase lands |
| `SND-GRAPH-120` | A workflow call is nested too deep: the call would be past `workflow.callDepth` (default 3) |
| `SND-GRAPH-121` | A run can't start its own workflow, or the workflow that called it |
| `SND-GRAPH-122` | An `http` node's request was refused before it was sent: host not on `workflow.http.hosts`, an address that is this machine, plain http, or a credential header |
| `SND-GRAPH-123` | An `http` node's request got no acceptable answer: status outside `expect`, a redirect loop, an answer over 1 MB, or a network failure |
| `SND-GRAPH-124` | A `workflow_result` node found no result: the run is not that workflow's, is not finished, or the workflow has no succeeded run |
| `SND-GRAPH-130` | No question waits at that node |
| `SND-GRAPH-131` | A reference reads a node that failed and continued |
| `SND-GRAPH-132` | The setup service ran a workflow other than the hello-sindri one it made, or ran it a second time |
