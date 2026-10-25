# Operating Sindri (C#) from the CLI

What you do with Sindri once [SETUP.md](SETUP.md) is done: sync context, build and review the product map, register and
approve recipes, and run the factory line (`workflow.line`). These are CLI chores today; they move into the Sindri app
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

The sync links Onyx's records (issues, threads) to the map and extracts decisions from them. With `context-sync`
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

A limit change is a profile change: validate, then `profile approve` (**Human-only**), then build again. A second
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

## 2. Recipes

The recipes live in `recipes/sindri/`. A recipe runs only once it is registered and approved, and a parent can only be
approved once every child it starts has an approved version to pin, so approve **children first**.

```bash
cd ~/code/agentic-workflow/recipes/sindri
sindri recipe validate workflow.line.yaml      # Agent OK: rule lines, then the hash; checks children beside it
```

The order for the line:

```text
idea-to-brief
evidence-api evidence-db evidence-ui evidence-desktop evidence-other evidence-all evidence
hand-test verify prove
build review-loop pr-babysitter ship phase
design plan docs-sync
scoping
line
```

For each `workflow.<name>.yaml` in that order (**Human-only**):

```bash
sindri recipe register workflow.prove.yaml   # optional ('approve' registers an unknown file itself): 'registered workflow.prove@<hash12> (unapproved)'
sindri recipe approve workflow.prove.yaml    # preview, then type 'approve workflow.prove 1'
```

The approve preview shows the id, version, hash, the diff against the last approved version, the capabilities and
effects it uses, the pinned blocks and children, and the eval files. Read it, then type the action text it asks for.
A small loop that registers and approves the list in order, stopping at the first failure, saves typing; re-running
it is safe (already registered or approved recipes are reported and skipped).

Check (**Agent OK**):

```bash
sindri recipe list              # every recipe 'approved (desk)'
sindri recipe list --stale      # approved ones that drifted or pin a stale child
```

## 3. Run the line

**To be confirmed by the demo.**

`workflow.line` runs four stations as child workflows: scoping, design, plan, and one phase (build, prove, review,
ship, then the PR babysitter). It stops for you after scoping, after design and before the merge.

> **What runs by itself today.** On this build only one block Step has a runtime. Every other block Step (intake,
> check, evidence, freeze, build, prove, ship and the rest) parks with `SND-WF-010` and becomes a **desk task**: you
> do the work, then report it with `sindri workflow complete <address> --run <token> --outcome <o> --output
> fact:<key>=<file>` (**Human-only**). Scoping has a desk helper that runs a block's code and writes the report for
> you (`sindri scoping desk <block> --facts <dir> --out <dir>`, **Agent OK**; it refuses to write inside a git
> worktree); the other stations don't have one yet. Expect to drive the line from your desk, not to watch it run
> hands-off. An agent may prepare a desk task's output files, but only you can submit them (an agent Step's own
> launched session reports its run with its launch secret, class `step-report`).

1. Make an item for the work (**Human-only**):

   ```bash
   cd ~/code/my-app
   sindri item add "Let admins toggle the export step per profile"    # 'ITM-… created (sequence N)'
   ```

2. Write the two input facts as JSON files, outside the repo:

   `scope.json` (`fact:scopeInput`), one of four kinds:

   ```json
   { "kind": "tracker", "repo": "my-app", "key": "APP-123" }
   ```

   The other kinds: `{ "kind": "brief", "repo": "my-app", "path": "docs/briefs/export.md" }`,
   `{ "kind": "idea", "repo": "my-app", "text": "…" }` and `{ "kind": "change", "repo": "my-app", "ref": "1234" }`
   (a PR number or a branch).

   `line.json` (`fact:lineContext`): `{ line, phase, attempt, base, stackParent?, runArea }`; see the line spec
   (`docs/superpowers/specs/2026-10-15-sindri-spec-to-proven-pr-line-design.md` §7.1) for the exact fields.

3. Start it (**Human-only**):

   ```bash
   sindri workflow start workflow.line --item ITM-… \
     --fact fact:scopeInput=scope.json --fact fact:lineContext=line.json
   ```

4. Watch it. The app's Workflows page shows the instance and its children live. From a terminal (**Agent OK**):

   ```bash
   sindri workflow list --needs-me          # what waits for you: decisions, then tasks
   sindri workflow status <address>         # one instance: state, open runs, waits
   sindri workflow events <address> --follow
   ```

5. Answer it when it parks or asks (**Human-only**): `sindri workflow answer <address> --text "…"`, or
   `--accept`, `--reject`, `--resume`; a grant or accept asks you to type a challenge.

6. Take over and hand back. To work on an instance yourself, `sindri workflow takeover <address>` pauses its timer
   (an agent session may take over, so an attached session can). `sindri handback <item-or-address>`
   (**Human-only**) resumes the timer with the time that was left. Sindri reminds you after 2 hours taken over; it never
   hands back by itself.

7. Finish desk tasks. `workflow list --needs-me` lists each parked Step with its run token. Do the work (for scoping,
   `scoping desk <block>` writes the outputs and `report.json`), then submit it (**Human-only**):

   ```bash
   sindri workflow complete <address> --run <token> --outcome <outcome> --output fact:<key>=<file>
   ```

## Codes you may meet here

| Code | What it means | Fix |
|---|---|---|
| `SND-CONTEXT-040` | The heavy-job lock stayed busy | Wait for the named job, then build again |
| `SND-CONTEXT-046` | `promote` refused: undecided items or no draft | `context map review`, then promote |
| `SND-CONTEXT-133` | A graphify limit isn't a whole number in range | Write a whole number, or `0` for no limit |
| `SND-JOB-409` | That job is already running | `sindri jobs`; wait for it |
| `SND-RECIPE-003` | A child has no approved version to pin | Approve the children first (section 2) |
| `SND-RECIPE-008` | The recipe declares no evals, or an eval file is missing | Keep the `evals/` folder beside the recipe |
| `SND-RECIPE-009` | The typed approval was wrong, expired (5 min) or the file changed while you read | Run `recipe approve` again |
| `SND-WF-010` | A Step has no runtime here (no agent target, or a desk-only block) | Do it at your desk and complete it, or set `workflow.agent.target` |

## Gotchas

- **`context map status` and `doctor` can count a map differently** (nodes and edges). Trust `context map status` for
  the map itself.
- **A hand-written map imported by mistake** becomes the current version. Build a draft from the repo, review it and
  promote it to replace it.
