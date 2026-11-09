# Operating Sindri (C#) from the CLI

What you do with Sindri once [SETUP.md](SETUP.md) is done: sync context, build and review the product map and run graph workflows. These are CLI chores today; they move into the Sindri app
as its pages land, and this page shrinks with them.

Every command is `sindri`. **Human-only** commands
are the owner's decisions. Sindri no longer detects an agent session: a CLI that can read the per-start token (an agent you started included) is a full caller, and one that can't is refused with `SND-AUTH-002`
([SETUP.md, Troubleshooting](SETUP.md#9-troubleshooting)). Sections marked **to be confirmed by the demo** are written
from the docs and the code and haven't been run end to end yet.

## 1. Context: sources, the product map, review

**To be confirmed by the demo** (the sync and a full map build, review and promote on a large repo).

### Sync the intent sources

```bash
sindri context sources sync      # Human-only: a job; prints 'job JOB-… started' and follows it (Ctrl-C cancels)
sindri context sources status    # Agent OK: each source's last sync, links, decisions and queued records
```

The sync links the indexed records (issues, threads) to the map and extracts decisions from them. With `context-sync`
named under `services:` it runs on that cadence instead.

### Build the map

```bash
cd ~/code/my-app
sindri context map build         # Human-only, heavy: a job that proposes a draft map from the repo
```

- It takes a slot of the box-wide heavy-job lock. If every slot stays busy for 60 seconds it stops with
  `SND-CONTEXT-040` naming the holder; run it again when that job is done. Don't run a map build alongside a test
  suite or a publish.
- Expect about 15 minutes on a large repo (a 20k-file TypeScript monorepo). `--detach` prints the job id and returns;
  `sindri jobs show <JOB>` follows up.
- It ends with `draft N for my-app · M proposed`, then one `degraded:` line for each part it did without. A typical one
  on a large repo is `degraded: clusters: 1 file over 16 MB left out`: normal, and no limit needs changing.
- An agent session can't read the review queue (`SND-CONTEXT-020`); it reads the draft with `context query --draft` and
  `context lenses --draft` instead.

The cluster pass runs graphify in a sandbox. Its limits are in the profile:

```yaml
context:
  extract:
    graphify: { timeoutMinutes: 60, maxSnapshotMB: 2048, maxFileMB: 16, maxGraphMB: 256 }   # the defaults; 0 is no limit
```

| `degraded:` line | What to do |
|---|---|
| `clusters: timed out after N min` | Raise `timeoutMinutes` |
| `clusters: snapshot truncated at N MB (K files skipped)` | Raise `maxSnapshotMB`, or add `denyPaths` for generated data (clusters are still used) |
| `clusters: K files over N MB left out` | Usually fine; raise `maxFileMB` if those files matter |
| `clusters: graph.json over N MB` | Raise `maxGraphMB` (the graph is held in memory, so don't set `0` on a huge repo) |
| `clusters: graphify not found`, `graphify is not the pin <version>` | `scripts/install-deps.sh` |
| `clusters: no sandbox`, `graphify did not start under the sandbox` | Install `bwrap` (Linux) or check `sandbox-exec` (macOS) |
| `labels` | Labeling is off or no agent CLI answered: clusters stay unnamed |

A limit change is a profile change: validate, then build again (it applies on save). A second
build keeps every decision you already made.

### Review and promote

```bash
sindri context map review --list --limit 200            # Human-only: the draft's queue, read-only (a session gets SND-CONTEXT-020)
sindri context query --draft "how a user exports a report"   # Agent OK: check the draft finds the right features
sindri context lenses --draft feature:reports.export   # Agent OK: one node of the draft and what it reaches
sindri context map review                               # Human-only: the loop (bulk-accept deterministic, then a/r/n/m/s)
sindri context map promote                              # Human-only: type 'promote map vN'
```

`promote` refuses while anything is undecided (`SND-CONTEXT-046`; skipping doesn't decide). Then prove the map
answers (**Agent OK**):

```bash
sindri context query "how a user exports a report"
sindri context lenses feature:reports.export
```

## 2. Recipes and the factory line (retired)

The recipe engine is gone (issue 2258): there is no recipe to register or approve and no factory line, and the old
recipe, scoping and workflow-instance commands are removed. Workflows are graphs now (`docs/csharp/sindri-graph.md`).

What is left of the old verbs:

- `sindri workflow complete <address> --run <token> --outcome <o>` and `sindri workflow takeover|handback` (and the short forms
  `sindri takeover|handback`) answer `SND-GRAPH-104` until the graph engine runs agent blocks.
- An instance the old engine left running gets a `workflow.retired` note the next time Sindri starts, once; the pass ends with a `workflow.retire.done` marker (private, written even when it retired nothing) so a later start reads no ledger page. Its item reads
  cancelled and its Needs-you rows are gone; the ledger keeps the old rows.

## Model choice and what Jev reads

With `workflow.agent.pick: on` in the live profile, each agent step's brief and input files (up to 100,000 characters, secrets and denied paths removed), and for the concept scan its map summary, are sent to Jev to pick a model. The app's Settings shows it. With it off, nothing is sent and every step runs on the agent's default. Retention is the provider's. Settings in the app shows whether model choice is on and how to change it (the profile; it takes effect on save).

## Codes you may meet here

| Code | What it means | Fix |
|---|---|---|
| `SND-CONTEXT-040` | The heavy-job lock stayed busy | Wait for the named job, then build again |
| `SND-CONTEXT-046` | `promote` refused: undecided items or no draft | `context map review`, then promote |
| `SND-CONTEXT-133` | A graphify limit isn't a whole number in range | Write a whole number, or `0` for no limit |
| `SND-JOB-409` | That job is already running | `sindri jobs`; wait for it |

## Gotchas

- **`context map status` and `doctor` can count a map differently** (nodes and edges). Trust `context map status` for
  the map itself.
- **A hand-written map imported by mistake** becomes the current version. Build a draft from the repo, review it and
  promote it to replace it.
