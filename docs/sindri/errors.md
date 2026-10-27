# Sindri error codes

Generated from `sindri/src/errors.ts` by `cd sindri && npm run gen`. Do not edit by hand.

| Code | Meaning | Fix |
|---|---|---|
| `SND-CLI-001` | Unknown command. | sindri help |
| `SND-CLI-002` | Invalid arguments for this command. | Run `sindri help` and check the command's flags. |
| `SND-CLI-900` | Unexpected internal error (a bug). | rerun with SINDRI_DEBUG=1 and report the output |
| `SND-EVOLVE-001` | The toolkit repo couldn't be read as a git repository. | check the ring-0 repo's path in repos/&lt;name&gt;.yaml, then sindri profile approve |
| `SND-EVOLVE-002` | No offline comparison exists for that artifact yet. | only the scope.draft prompt can be compared in this release (spec amendment 4) |
| `SND-EVOLVE-003` | A pull request couldn't be read with gh. | check the PR number and `gh auth status` |
| `SND-EVOLVE-004` | That proposal can't be adopted. | sindri evolve show &lt;id&gt; says what is missing: a won comparison, the safety clause, or a clean leak check |
| `SND-EVOLVE-005` | That channel change isn't allowed yet. | sindri channel status shows why and what to run |
| `SND-EVOLVE-006` | That action needs an interactive terminal. | run it yourself in a terminal |
| `SND-EVOLVE-007` | The confirmation didn't match. | rerun and type the characters shown |
| `SND-EVOLVE-008` | No artifact in the registry has that id. | sindri evolve status lists the registered artifacts; sindri evolve init refreshes the registry |
| `SND-EVOLVE-009` | The judge model must differ from the model that wrote the outputs. | set models.adjudicator to a different model than models.scoping, then sindri profile approve |
| `SND-EVOLVE-010` | The artifact registry is empty. | sindri evolve init |
| `SND-EVOLVE-011` | The toolkit repo has no GitHub remote called origin. | git remote add origin &lt;github url&gt; in the toolkit repo |
| `SND-EVOLVE-012` | That pull request can't be reflected on. | reflect only reads merged PRs by an allowed author (evolve.prAuthors) |
| `SND-EVOLVE-013` | gh returned a reply in an unexpected shape. | update gh, then rerun |
| `SND-EVOLVE-014` | Publishing writes into the working tree, which is on the default branch. | git switch -c docs/sindri-proposals-&lt;week&gt;, then rerun sindri evolve publish |
| `SND-EVOLVE-015` | The privacy gate has no private terms to check against. | add your workplace's names to privacy.denyTerms in the private profile, then sindri profile approve; or pass --no-privacy-terms to publish with the private-terms layer skipped (the other five layers still run) |
| `SND-EVOLVE-016` | That proposal's status doesn't allow a comparison. | sindri evolve show &lt;id&gt; prints its status; a rejected, adopted, merged or staged proposal is never compared again |
| `SND-EVOLVE-017` | A private repo has no usable built code index (none, unbuilt or damaged), so publish can't check proposals for its identifiers. | sindri index build --repo &lt;name&gt; (remove a damaged index file first), then rerun sindri evolve publish |
| `SND-EVOLVE-018` | The English word list publish uses to tell private identifiers from ordinary words is missing. | install a word list at /usr/share/dict/words (or point SINDRI_WORDS at one), then rerun sindri evolve publish |
| `SND-EVOLVE-019` | Publish couldn't list a private repo's tracked files, so it can't check proposals for their names. | check that the repo file's path is that repo's git checkout on this machine, then rerun sindri evolve publish |
| `SND-INDEX-001` | All heavy-job slots are busy. | wait for the holder to finish; `sindri doctor` shows it |
| `SND-INDEX-002` | The repo path is not a git repo. | check repos/&lt;name&gt;.yaml path, then sindri profile approve |
| `SND-INDEX-003` | The index input is larger than index.maxTotalMB. | add generated or vendored paths to index.denyPaths, or raise index.maxTotalMB |
| `SND-INDEX-005` | The embedding URL is not loopback. | set index.embeddings.url to http://127.0.0.1:11434 (or disable embeddings) |
| `SND-INDEX-006` | The local embedding server failed. | sindri index setup (starts Ollama checks and pulls the model) |
| `SND-INDEX-007` | No network sandbox is available for graphify. | macOS: sandbox-exec ships with the OS; Linux: install bubblewrap (bwrap); if `bwrap --ro-bind / / true` fails with "setting up uid map: Permission denied" (Ubuntu 24.04), run `scripts/install-deps.sh` for the AppArmor profile fix; or set index.graph: none |
| `SND-INDEX-008` | graphify is missing, failed or wrote no usable graph. | if graphify or its sandbox is missing: sindri index setup; if graph.json is over the cap: raise index.graphMaxMB (max 512); then sindri index build |
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
| `SND-PROFILE-006` | The hash doesn't match the current profile. | Run `sindri profile approve` to see the current hash and diff, then approve that hash. |
| `SND-PROFILE-007` | A profile already exists there. | Edit it, or pass --force to overwrite it. |
| `SND-PROFILE-008` | This repo has no plan files for --ring0. | Run --ring0 from a repo with docs/superpowers/plans, or run `sindri profile init` without it. |
| `SND-PROFILE-009` | Not inside a git repo. | cd into the repo first. |
| `SND-PROFILE-010` | Approving a profile needs an interactive terminal. | run `sindri profile approve <hash>` yourself, in a terminal |
| `SND-PROFILE-011` | The approval was not confirmed. | rerun and type the first 6 characters of the hash |
| `SND-PROFILE-012` | No profile has been approved yet. | sindri profile approve |
| `SND-PROFILE-013` | That repo name is already used for another path or file. | pass --name &lt;another name&gt; |
| `SND-PROFILE-014` | That repo name is not valid. | use lowercase letters, digits and dashes (max 39) |
| `SND-PROFILE-015` | That repo is in the live profile but not approved yet. | run `sindri profile approve`, review the diff, then approve it at a terminal (or `sindri repo onboard`, which prints both commands) |
| `SND-PROFILE-016` | This worktree has no main checkout to add (a bare repo, a separate git dir or a submodule). | pass the main checkout's path; a bare repo has no checkout to index |
| `SND-SCOPE-001` | No allowed provider can run scoping. | add anthropic to providers.allowed, then sindri profile approve |
| `SND-SCOPE-002` | A model job failed, timed out or returned junk. | rerun; the message carries the CLI's own error. If it repeats, run `claude -p hello` to check the CLI and its login |
| `SND-SCOPE-004` | The model's answer didn't match the required shape. | rerun; the next round gets the reasons. Persistent: try another models.scoping |
| `SND-SCOPE-005` | The run's token budget is used up. | raise scope.maxTokensPerRun (or maxTokensPerBacktest) in the profile, then sindri profile approve |
| `SND-SCOPE-010` | Linear rejected the token. | check sources.linear.token points at a valid read-only Linear API key |
| `SND-SCOPE-011` | Linear could not be read. | check the project URL and your network, then rerun; the message carries Linear's own error text |
| `SND-SCOPE-020` | The brief file can't be read. | check the path you passed to sindri scope |
| `SND-SCOPE-021` | No output directory for the scope map. | pass --out DIR or set sources.notesDir |
| `SND-SCOPE-022` | That section isn't in the document. | check the heading number (## 13. …) |
| `SND-SCOPE-023` | The project has no issues filed after its brief. | pick a project with later issues, or a shorter --window |
| `SND-SCOPE-024` | Linear isn't configured as a source. | add sources.linear.token (a secret pointer), then sindri profile approve |
| `SND-SCOPE-025` | Refusing to write a scope map into a git worktree. | use --sources file,code, or write outside the repo |
| `SND-SCRUB-001` | A profile scrub pattern does not compile. | Fix the regex at the named scrub.extraPatterns index, then run `sindri profile validate`. |
| `SND-SCRUB-002` | Staged changes contain likely secrets. | remove them (use a secret pointer or an env var); for a false positive, commit with --no-verify and say why |
| `SND-SCRUB-003` | A different pre-commit hook is already installed, or core.hooksPath points the hook at a directory sindri didn't create (it never writes there). | Add this line to that hook by hand: `sindri scrub --staged \|\| exit 1`. |
| `SND-SCRUB-004` | `scrub --staged` or `--install-pre-commit` ran outside a git repo. | cd into the repo first, or pass --repo PATH to --install-pre-commit. |
| `SND-SCRUB-005` | `git diff --cached` failed, so `scrub --staged` could not scan the staged changes. | Fix the git error shown in the details, then retry the commit. |
| `SND-SCRUB-006` | git's init.templateDir is set to a directory sindri does not own, or could not be read or set. | copy the named hook into that template dir's hooks/ yourself, or `git config --global --unset init.templateDir` and rerun `sindri repo onboard --template` |
| `SND-SECRET-001` | A secret pointer could not be resolved. | check the pointer in the profile and that the secret exists (env var, file, keychain item or 1Password item) |
| `SND-SECRET-002` | A secret file is readable by other users. | chmod 600 &lt;file&gt; |
| `SND-TRACKER-001` | The tracker's source is missing or unreadable (for plan-file: the repo path or plan dir). | Restore that directory, or fix `path` in repos/&lt;name&gt;.yaml and run `sindri profile approve`. |
| `SND-TRACKER-002` | The tracker's source holds no items to read (for plan-file: no plan file matches tracker.include). | Fix tracker.include in profile.yaml (or restore the plan files), then run `sindri profile approve`. |
| `SND-TRACKER-003` | A plan file is too big to read (over 2 MiB). | Split the plan file into smaller plans. |
| `SND-TRACKER-404` | The tracker has no such item. | Check the id with `sindri observe`. |
| `SND-TRACKER-405` | This tracker can't write. | The plan-file tracker is read-only; edit the plan file. |
