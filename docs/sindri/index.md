# Sindri code index

One index per repo at `$AW_STATE_DIR/sindri/index/<repo>.db`, built from tracked files only (`git ls-files`, minus the built-in secret globs (`.env*`, `*.pem`, `**/secrets/**` and the like) and `index.denyPaths`, which adds to them and never replaces them, all matched case-insensitively; symlinks never followed). Spec: §6.2.

## First run

```bash
sindri profile init --ring0 && sindri profile approve   # or: profile init, repo add <path>, profile approve
sindri index setup      # checks Ollama, pulls the embedding model, installs the pinned graphify with uv, probes the sandbox
sindri index build      # full build; also creates the bare mirror at $AW_STATE_DIR/sindri/mirrors/<repo>.git
sindri scrub --install-pre-commit   # hook v2: secret scan, then shape recording
sindri repo onboard <path>             # repo add → approval (yours, at a terminal) → pre-commit hook → first index build; idempotent
```

`index setup` reads the **approved** profile, so approve first. It needs Ollama (running, with the model), `uv`, and macOS `sandbox-exec` (or Linux `bwrap`). Without one of them that layer is `unavailable` and everything else works.

### Onboarding a repo

`sindri repo onboard <path>` runs four steps, in order, and prints one line for each, named as below:

1. **repo-add**: put the repo in the live profile. Run from a linked worktree, it adds the main checkout (the parent of `git rev-parse --git-common-dir`), never the worktree path, which goes away when the worktree is removed. It checks that `<parent>/.git` is that common dir; a worktree of a bare repo, of a `--separate-git-dir` checkout or of a submodule has no such checkout and is refused (`SND-PROFILE-016`: pass the main checkout path). `repo status` applies the same check.
2. **approval**: check that the approved profile lists the repo at that path. It never approves: until you run `sindri profile approve` at a terminal, it prints that command and exits 1.
3. **pre-commit**: install the pre-commit hook (secret scan, then shape recording) in the repo's `.git/hooks`.
4. **index-build**: the first `index build --repo`, with one try at a heavy-job lock slot.

Each line is `ok` (the step passed), `done` (it did something just now), `skip` (it did not run: `needs approval`, or skipped on request such as `--no-build`), `warn` (it needs attention, for example the approval is pending, or every heavy-job lock slot was busy so the build is left for the nightly build) or `fail` (the step failed; the line carries the error code and the fix). Only a pending approval stops the later steps (they print `skip  needs approval`); a failed `pre-commit` step still runs `index-build`. An index-build error other than a busy lock stops the command with that error. The exit code is 2 if any step failed, 1 if any warned, else 0. Rerunning is safe.

**core.hooksPath.** When `core.hooksPath` is set (in the repo or globally), that hooks directory is yours (often tracked, maybe public), so neither `sindri repo onboard` nor `sindri scrub --install-pre-commit` writes a hook there, not even into an empty one, and sindri never sets `core.hooksPath`. The step fails with `SND-SCRUB-003` and its fix gives the two lines, with the absolute, quoted path of the installed binary: for husky, add them to `${XDG_CONFIG_HOME:-~/.config}/husky/init.sh`, guarded to this repo by its `--git-common-dir`; otherwise add them to `<hooks dir>/pre-commit` yourself. `sindri doctor` gives the same fix for such a repo, never the installer.

**The nudge.** A SessionStart hook (`aw:sindri-nudge`, installed for every provider by `./setup.sh --with-sindri`) runs `sindri repo status --nudge`, which is read-only and bounded, and names the current repo when it is not onboarded. To silence it, onboard the repo, or remove the `aw:sindri-nudge` entry from the host's hooks.

**The template.** `sindri repo onboard --template` sets git's `init.templateDir` so new clones and `git init` get a pre-commit hook that does nothing until the repo is approved. It sets `init.templateDir` only when it is unset or already points at sindri's own template dir; one that points anywhere else is never overwritten (`SND-SCRUB-006`, with a copy command as the fix). It never touches `core.hooksPath`. For existing repos, `sindri repo onboard` or `sindri scrub --install-pre-commit` installs the full hook. Rerunning `git init` copies the template hook only where no `pre-commit` exists yet.

## Layers

| Layer | What it holds | Built with | Off switch |
|---|---|---|---|
| structure | functions, methods, arrows, classes: name, signature, lines, exported, complexity, callees | TypeScript compiler API (TS and JS) | none |
| clones | normalized AST hash per symbol; MinHash/LSH bands over 5-token shingles | core | none |
| deps | `package.json` dependencies with purpose tags (date, http, id, validation, …) | core | none |
| embeddings | one vector per symbol | Ollama on loopback (`index.embeddings`) | `index.embeddings.enabled: false` |
| graph | module and call graph | graphify in a sandbox, on a snapshot of the tracked source and docs files | `index.graph: none` |

**Offline guarantee.** Code never leaves the machine.

- The embedding URL must be the IP literal `127.0.0.1` or `[::1]` with no credentials (not `localhost`), requests refuse redirects, and a model whose name contains `cloud` is refused, because Ollama cloud models forward the text to a remote service. Node's `fetch` ignores `HTTP(S)_PROXY` unless `NODE_USE_ENV_PROXY=1` is set; `doctor` warns if it is. A loopback port that is really an SSH or port-forward tunnel is not this machine; that is documented, not detected.
- graphify runs under `/usr/bin/sandbox-exec` (macOS) or a root-owned `/usr/bin/bwrap` or `/bin/bwrap` (Linux; `--unshare-all --new-session`, with `/run`, `/var/run` and `$XDG_RUNTIME_DIR` masked so D-Bus and docker.sock are unreachable). The sandbox binary is never looked up on PATH. Inside it: no network; on macOS no LaunchServices opens, Apple events or `launchctl` (so it can't start a browser or a launchd job outside the sandbox); writes only to its snapshot dir, with `TMPDIR`, `XDG_CACHE_HOME`, `UV_CACHE_DIR` and `PYTHONPYCACHEPREFIX` pointed at a fresh private dir inside it (an unguessable `.sindri-tmp-*` name made per run; tracked files under `.sindri-tmp*` or `graphify-out/` are never copied into the snapshot, so a repo can't pre-seed it) (`~/.cache` and the system temp dirs hold code other tools run later, so they are not writable); and the credential stores hidden by name and by their real path (a symlinked `~/.ssh` is hidden too): `~/.ssh`, `~/.aws`, `~/.gnupg`, `~/.agentic-workflow`, `~/.config/gh`, `~/.docker`, `~/.kube`, `~/.codex`, `~/.claude`, `~/.claude.json`, `~/.netrc`, `~/.git-credentials`, `~/.npmrc`, `~/.pypirc` and, on macOS, `~/Library/Keychains`. The environment is cleaned. Everything else stays readable, so a compromised graphify could still read other files in your home directory, and on macOS it can still reach other mach services (securityd and the like). `graph.json` is size-capped (`index.graphMaxMB`, default 256, max 512; values near 512 may need more Node heap, e.g. `NODE_OPTIONS=--max-old-space-size=4096`), read only as a regular file and never through a symlink, and treated as untrusted. It doesn't run at all without a sandbox. `sindri index setup` proves the sandbox denies the network with a positive control (the same request must succeed outside it) and, on macOS, that a launchd job can't be submitted from inside it.
- graphify is installed at an exact pin (`sindri.graphifyPin` in `sindri/package.json`, at least 14 days old) with `uv tool install --exclude-newer <cutoff>`, which also age-gates its dependencies. The cutoff (`sindri.graphifyPinDate`) is the next 00:00Z after the last upload of any of the release's files: uv excludes files uploaded at or after it, so an earlier cutoff would exclude the pinned release itself.
- Symbol bodies are scrubbed before they are stored. The state dir holds source text (the index and the mirror): keep it out of cloud sync and backups you don't control. The mirror holds the repo's full, unfiltered history, including deleted secrets and denied paths, so it is never mounted into a guest without a deny filter.

## Commands

```bash
sindri index setup [--dry-run]          # steps: ollama, ollama-server, embedding-model, graphify, sandbox (all reported, none skipped by an earlier failure)
sindri index build [--quick] [--full]   # incremental; --quick: structure, clones and deps only; --full rebuilds; takes a slot of the box-wide heavy-job lock
sindri index status                     # every repo: age, commit, per-layer status with the reason; exit 1 when an index is missing or stale
sindri index query <name>               # a symbol's exact and near clones (for people: output is repo text, never feed it to a session)
sindri repo add <path> [--name NAME]    # profile entry (then sindri profile approve); the next full index build mirrors it
sindri shape report [--recent N]        # read-only: signals by type, outcomes and precision; --recent lists signals with their evidence
sindri shape reconcile                  # move the spool into the ledger and label outcomes now (observe does it hourly)
```

`index build --json` prints `{ "reports": [...], "unapproved": [...] }`: `reports` is one build report per repo built (repo, commit, quick, files, symbols, layers, ms) and `unapproved` lists the repos in the live profile that are not approved yet and were skipped (always `[]` with `--repo`). **Breaking change:** before Plan 4 it printed the bare `reports` array; a script that parsed it reads `.reports` now.

Embeddings: if the URL or model is refused (the profile schema normally catches this first), `index build` does not abort. The embeddings layer reports `unavailable` with the reason and the other layers build. A failed layer is `unavailable` with its reason; the other layers stay usable. Builds write a temp copy and rename it, so an interrupted build leaves the previous index in place.

## Freshness

An hourly `sindri index build --quick` (launchd) refreshes structure, clones and deps; the nightly build at 03:15 refreshes everything. Both run at background CPU and I/O priority. The quick build tries the heavy-job lock once and, when other heavy jobs hold every slot (`AW_HEAVY_SLOTS`, default 2) or another build of the same repo is running, skips that repo with exit 0 (the next hour retries); the full build waits up to 10 minutes. The embeddings layer embeds at most 4096 symbols per build and writes each request's vectors as they arrive, so a first build of a big repo finishes over several builds (`embeddings pending: N of M symbols embedded (K this build)`, N counting every build so far) and a failure keeps what was done. Both read the files **as checked out** (the working tree), so keep `main` checked out in the indexed checkout; uncommitted work there becomes base data. Each shape run records the index age. The bare mirror is for a later plan (guest sandboxes).

## Shape signals (record-only)

The pre-commit hook (installed by `sindri scrub --install-pre-commit`) scans for secrets, then runs `sindri shape --record --staged`. That compares the staged changes of the commit's own worktree (an in-memory overlay; denied paths and files over `index.maxFileKB` are skipped) with the index and writes the signals to `$AW_STATE_DIR/sindri/spool/`. It opens the ledger read-only and always exits 0: nothing blocks a commit until rollout step 3b. The hourly `observe` (or `sindri shape reconcile`) moves the spool into the ledger, under the same gates as recording: an approved profile, `hosts.active` and the tick lock. That step is best effort: a broken spool file or an unreachable origin becomes a note in `observe`'s output and never stops plan tasks from being recorded. `sindri shape report` only reads: it never creates or migrates the ledger, takes no lock and makes no git call.

| Signal | Fires when | Default threshold (`shape.thresholds`) |
|---|---|---|
| `reinvented:exact` | a new symbol has the same normalized AST as another file's symbol | none |
| `generalize:near-clone` | MinHash similarity with another symbol, both at least N tokens | Jaccard 0.8, 60 tokens |
| `reinvented:name` | name and signature similar to an exported or utility symbol | 0.85 |
| `reinvented:graph` | call set overlaps an exported or utility symbol's (≥ 3 calls each) | 0.5 |
| `reinvented:embedding` | embedding cosine and AST similarity both high (only within the commit budget) | 0.9 and 0.6 |
| `reinvented:dependency` | a new dependency shares a purpose tag with an existing one | none |
| `simpler:diff-size` | added lines over the size budget (`shape.defaultSize`, default S = 250) | `shape.sizeBudget` |
| `simpler:complexity` | a symbol's complexity grew by more than the limit | 10 |
| `simpler:exports` | more new exports than the size class allows | `shape.exportAllowance` |

Names in signal details are wrapped in `<untrusted>…</untrusted>` with `&`, `<` and `>` escaped: they come from the repo, not from Sindri. To turn recording off, set `shape.record: false` (and approve the profile); to remove the hook step, reinstall without it or delete the `shape --record` line.

## Outcomes and precision (the 3b bar)

Each recorded run keeps `git write-tree` of the staged index. `observe` and `shape reconcile` link a run to the commit with that tree. A retried commit (a later hook refused it, the message editor was closed empty) spools one run per attempt with the same tree: only the latest linked run with that staged tree counts (also after an amend that a tick saw before and after), and the earlier runs' signals are `n/a`. A run still unlinked once it is `shape.outcomeDays` old is closed: its signals are `dropped` and it never links later. Once the commit is `shape.outcomeDays` (14) old, each signal gets an outcome. Kept or acted-on is judged at the default branch as it stood `outcomeDays` after the run (`git rev-list -1 --first-parent --before`, so a merged side branch's own commit is never the as-of state), so a later, unrelated edit of the symbol doesn't count; a change that reached the branch only after that window is judged at the current tip. The fetch of `origin` times out after 20 s. Outcomes:

| Outcome | Meaning |
|---|---|
| `kept` | the flagged symbol (same file, same name, same AST) or dependency is still there |
| `acted-on` | it was changed or removed within the window, for any reason |
| `dropped` | the commit was never made, was amended, or never reached the default branch |
| `n/a` | diff size and export count have no flagged symbol, a symbol signal with no recorded name or AST hash can't be matched to any version, and an earlier run of a retried commit is superseded by the latest |

`sindri shape report` prints, per type, `SIGNALS | LABELED | ACTED-ON | KEPT | PRECISION | TOWARD 3b`. Precision is `acted-on / (acted-on + kept)`, and 3b wants at least 30 labeled signals and precision at least 0.70 per layer (`ready`). Known measurement effects:

- A commit still on an open branch after `outcomeDays` is labeled `dropped` (spec amendment 6: not on the default branch by then counts as dropped, and dropped is excluded from precision), so a long-lived PR's signals never enter precision.
- With squash merges, a signal whose flagged code was reworked later in the same PR has no version with the recorded hash on the default branch, so it is labeled `dropped` and excluded from precision. Measured precision is therefore biased **low** (conservative for the 3b bar). Recovering those signals needs PR data, which a later plan adds.
- AST hashes depend on the parser (the indexer version and the TypeScript version). Each run records its parser, and only runs recorded by the current parser are labeled, so a TypeScript upgrade leaves older runs unlabeled instead of reading them as `acted-on`. The structure layer's stamp names the parser too, so an upgrade re-parses every file on the next build.
- A file renamed on the default branch after the merge, or a symbol moved to another file, still reads as `kept`: when the original path no longer holds it, every file at the tip that mentions the name (`git grep -w`) is parsed for the same name and AST hash. If that search fails, the signal stays unlabeled until the next run.

This is an **outcome proxy, not a human label**: code is changed for other reasons too, and a correct signal can be ignored. It exists so thresholds can be tuned without anyone hand-labeling (spec invariant 9).

## Troubleshooting

| `index status` or `doctor` says | Meaning | Fix |
|---|---|---|
| `no index` | never built | `sindri index build --repo <repo>` |
| `stale` | older than `index.maxAgeHours` | `sindri index build`; if the launchd job should have run, read `~/.agentic-workflow/sindri/index-quick-launchd.log` (hourly quick build) or `~/.agentic-workflow/sindri/index-launchd.log` (nightly full build) |
| `never built` | an empty index file | `sindri index build --repo <repo>` |
| `embeddings unavailable (Ollama not answering …)` | Ollama is down or the model is missing | start Ollama, `sindri index setup` |
| `graph unavailable (…)` | graphify missing, wrong version or failed | `sindri index setup`, then `sindri index build --full` |
| `embeddings pending` / `graph pending` | only quick builds have run | `sindri index build` |
| `uv is not installed` (the `graphify` step of `index setup`) | `uv` is missing; `./setup.sh --with-sindri` installs it with Homebrew (`brew install uv`) when it can, and warns otherwise | `brew install uv` or see the uv install docs, then `sindri index setup` |
| `SND-INDEX-008` (graphify output not usable) | the graphify test fixture (`sindri/tests/fixtures/graphify/graph.json`) is synthetic until the switch-on re-records it from a real sandboxed run, so key names may need a fix then | re-record the fixture from the pinned graphify and fix `parseGraphJson` if a key differs |
| `SND-INDEX-001` / `heavy-lock held` | other heavy jobs (test runs, other builds) hold every slot of the lock (`AW_HEAVY_SLOTS`, default 2), or a build of the same repo is already running; the message names each slot's holder and for how long | wait; a slot whose holder is dead on this host (pid gone, or recorded on another boot) is reclaimed automatically, and so is any slot held over 6 h (a crashed `locks.sh` holder leaves no record, and a reused pid looks alive); otherwise run the fix `sindri doctor` prints for that slot (`heavy-lock` is slot 1, `${AW_HEAVY_JOB_LOCK:-$AW_STATE_DIR/locks/heavy-job.lock}`; `heavy-lock.k` is that path plus `.k`): `rmdir` the slot and delete the `.holder.json` file beside it |
| `SND-PROFILE-015 <name> is in the live profile but not approved yet` | `repo add` ran, `profile approve` didn't | `sindri profile approve`, then rerun |
| `hook is v1: secret scan only, no shape recording` (`pre-commit:<repo>`) | the Plan 2 hook is installed; it never records shape signals | `sindri scrub --install-pre-commit --repo <path>` (with `core.hooksPath` set, add the two lines by hand as the fix says; see Onboarding) |
| `hook doesn't run sindri shape --record` | a hand-merged v2 hook lost the shape line while `shape.record` is on | reinstall it, or add the `"$SINDRI" shape --record --staged \|\| true` line back |
| `N quarantined shape run(s)` (`shape-spool`) | spool files the ledger couldn't read were set aside | look at them, then delete the `quarantine/` dir |
| `sindri-shape: skipped (no index; …)` in a commit | the hook found no index for this repo | `sindri index build` |
