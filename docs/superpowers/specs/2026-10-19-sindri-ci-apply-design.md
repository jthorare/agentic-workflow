# Sindri CI apply: map import and promote, and recipe approval, from CI/CD (spec amendment)

**Status:** proposed, rev 2 (2026-10-19), after `/autoplan` round 1 (§12). The owner reads it before it is marked
ready. It is a docs-only amendment: no code lands until the owner approves it and answers the open questions in §11.

**Decision it implements (owner, 2026-10-19; handoff `docs/sindri/demo-handoff.md` §9c, PR #368, 6a710eeb9):**
map import and promote, and recipe approval, run from CI/CD with no manual step. Profile approval and token grants
stay human unless the owner says otherwise.

**Amends** (the full list of edits, with the repo docs, is §10):
- the API surface spec (`2026-10-10-sindri-api-surface-design.md`): §3.7 (CI-1), §4.1 (CI-2), §4.2 (CI-3) and §6 (CI-4);
- the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`): W8 and W9 (CI-5), §4.1 (CI-6) and the
  action-class table (CI-7);
- the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`): §3's worktree rule (CI-8), §3
  "Decisions stay human" (CI-9) and §8.7 (CI-10).

Each amended spec carries a revision note pointing here. Where this text and an amended spec disagree, this text
wins for the three verbs it covers, and only for those.

## 1. What changes, in one paragraph

Today the three verbs are `protected`: the owner types a server challenge at a terminal, bound to the content's hash.
After this amendment, **a pull request that a listed merger merged, with its required checks green on the merge
commit, is the approval**. Sindri applies it itself: a built-in service, `ci-apply`, fetches that repo into its own
mirror, checks the merge with the forge, reads the file's bytes at the merge commit, and applies exactly those bytes.
The ledger records the approval as `ci:<host>/<owner>/<name>@<sha>` with the PR number and the merger, and the owner
gets a line saying so, with the revoke command beside it. Revoke stays human, and the terminal path stays as the
fallback.

**What is fully automatic, and what isn't:**

| Verb | Automatic when | Still needs a person when |
|---|---|---|
| Recipe approve | The merged recipe uses no capability beyond its ceiling (§3.4) | It widens capabilities past the ceiling: a needs-you row with the terminal command |
| Map promote | Always, for deterministic content | Model proposals stay in the review queue; they never reach `current` without a person |
| Map import | Only if the owner accepts maps in a private repo (O2); otherwise it stays at the terminal | O2 is answered "no" |

## 2. Two things the current system makes hard, and how this design answers them

1. **Sindri listens on loopback only** (API §6). A GitHub-hosted runner can't reach it, and the owner's box isn't
   always on. So CI can't *push* the apply. Sindri *pulls* instead: the CI half (GitHub Actions) proves the content,
   and Sindri's CD half applies what merged the next time it runs. A box that was off catches up when it starts.
   §9 compares this with a self-hosted runner that pushes.
2. **Map files are kept outside every repo** (context §3, `SND-CONTEXT-005`), because a map can carry work data
   (decision quotes from intent sources, issue keys). A map in a PR puts that text on the forge **when the PR is
   opened**, long before Sindri sees it, so no check Sindri makes at apply time can keep it off the forge. The only
   real control is the owner choosing, once, a repo for maps. So map import from CI is the owner's call (O2) and its
   phase comes last (§13); promote needs no file at all (§3.5).

## 3. The design

### 3.1 The profile's `ci:` block (human-approved scope)

The CI identity's whole scope lives in the profile, so adding or widening it is a profile change, and profile
approval stays `protected` (human, typed). There is no new credential that grants Sindri anything.

```yaml
ci:
  every: 2m                       # ci-apply's cadence (30s..24h; default 2m)
  credential: command:gh-ci-token # read-only, required, never the same as workflow.forge.credential
  statusCredential: env:SINDRI_CI_STATUS_TOKEN   # optional, statuses:write only; turns on the PR status (§6.3)
  repos:
    - repo: github.com/jdoe/agentic-workflow   # host/owner/name; the host must be in workflow.forge.hosts
      branch: main                # default: the forge's default branch
      since: 4f1c2a9              # optional: the first commit to consider (default: the tip when this entry is first approved)
      mergers: [jdoe]             # required: forge logins whose merges count
      recipes:
        paths: ["recipes/sindri/*.yaml"]
        checks: [recipes]         # GitHub check-run names (the job's name), from the github-actions app
        allowCapabilities: []     # the ceiling for a recipe id CI has never approved (§3.4)
    - repo: github.com/jdoe/aw-split-panes
      mergers: [jdoe]
      promote:
        profileRepo: aw-split-panes   # the repos[] entry whose map is built and promoted
        checks: [build]
    - repo: github.com/jdoe/sindri-maps       # only if O2 is "yes"
      mergers: [jdoe]
      maps:
        paths: ["maps/*.yaml"]
        checks: [maps]
        profileRepo: aw-split-panes
```

- Each entry names the verbs it may apply: `recipes`, `promote`, `maps`. A verb not named is never applied from that
  repo. Each verb needs at least one check, and each entry needs `mergers` (profile validate refuses either missing,
  `SND-PROFILE-*` next free codes).
- `credential` is required and is refused when it equals `workflow.forge.credential` (§7).
- A `maps` repo is checked for visibility at validate, at each tick and at doctor: only `private` counts (never
  `internal`, `public` or unknown).
- `ci:` is left out of the canonical JSON when absent, so existing approved profiles keep their hash.

### 3.2 The service `ci-apply`

A **built-in** service: source `built-in`, listed in `sindri services` (this phase adds built-ins to that list,
CI-P1), paused and resumed like any other, run on demand as a `job`. Its cadence is the approved profile's `ci.every`;
without a `ci:` block it has no schedule entry. On each tick, for each `ci.repos` entry:

1. **Fetch** the branch into a Sindri-owned bare mirror, `$AW_SINDRI_HOME/ci/mirrors/<host>/<owner>/<name>.git`
   (0700), hardened like `GitPusher` (no repo or global config, hooks at `/dev/null`, the credential through
   `GIT_CONFIG_*` env, never an argument), with `--prune` and `gc --auto`. The owner's checkouts are never touched.
2. **Walk** the first-parent commits from the entry's cursor to the tip. A commit is a candidate for a verb when it
   changes one of that verb's paths (`promote`: any commit). The cursor starts at `since`, or at the tip when the entry
   is first approved, so history is never applied (the owner applies older files at the terminal). If the cursor is
   no longer an ancestor of the tip (a force-push), it moves to the merge base and raises a needs-you row.
3. **Check** each candidate with the forge:
   - the commit came from a **merged PR** (`GetCommitPull`) whose merger is in `mergers`. A direct push, or a merge by
     a login not listed, is `SND-CI-003` and is never applied;
   - every named check has a check run on **that commit**, from the `github-actions` app, matched on the check run's
     `name` (the job's name, not the `workflow / job` label the UI shows). Only `success` counts; `skipped`, `neutral`,
     `cancelled`, `timed_out` and `failure` are failures (`SND-CI-001`);
   - a check that is **queued, in progress or not there yet** is *pending*: the cursor stays, nothing is recorded, and
     the next tick looks again. After 30 minutes of pending it becomes `SND-CI-002` (failed, final). A commit message
     with `[skip ci]` therefore ends as `002`.
4. **Bind** the content: read the file at that commit from the mirror (`git cat-file`), and record `(repo, sha, path,
   blob, hash)`, where `hash` is the canonical hash the terminal path uses. What is applied is exactly those bytes; the
   verb hashes them again and refuses a mismatch. Nothing is read from a working tree.
5. **Apply** in-process as caller `service:ci-apply` (§4), through the command chain.
6. **Record** and move the cursor (§3.3).

Only the **newest** candidate per path in one tick is applied; older ones are recorded `superseded`. A file deleted
on the branch applies nothing, and removal never revokes (§3.6).

### 3.3 The cursor and its events

Every outcome is a ledger event, so `views rebuild` rebuilds the cursor and the history:

| Event | When | Payload (public: ids, hashes, codes; no file text) |
|---|---|---|
| `ci.apply.applied` | A verb applied | `repo, sha, path, blob, verb, hash, pr, mergedBy` |
| `ci.apply.failed` | A final failure | `repo, sha, path, verb, code` |
| `ci.apply.superseded` | A newer candidate for the path in the same tick | `repo, sha, path, by` |
| `ci.cursor.moved` | Once per tick per repo, after the commits it settled | `repo, from, to` |

A transient error (forge or network down, the heavy-job slot busy, a pending check) records nothing and leaves the
cursor. Content and authorization failures are final for that commit: the next merge that touches the path tries
again. A failed Temporal wake after an approval row is stored is transient: the next tick sees `AlreadyApproved` and
wakes again.

### 3.4 Recipe approve

Register the file (the desk path, as `service:ci-apply`), run the validator and its evals in-process, then approve
with `via: ci`. Failing or missing evals refuse (`SND-RECIPE-008`, wrapped as `SND-CI-005`), exactly as at the terminal.

**The capability ceiling.** At the terminal the owner reads the capability preview. Under CI nobody does, so CI may
only approve a recipe whose capability uses (block, transition, capabilities, parameters, the effect kinds) are a
subset of:
- the last approved version of the same recipe id, or
- for an id CI has never approved, the entry's `allowCapabilities` (default: none).

A recipe past its ceiling is `SND-CI-007`: a needs-you row with the preview and the terminal command
(`sindri recipe approve <id@hash>`). A crash between register and approve leaves a registered, unapproved recipe; the
next tick finishes it.

### 3.5 Map promote

No file merges. For a merge into a `promote` repo with its checks green, `ci-apply`:
1. asks the context engine to **build the draft at the merge commit**, reading the tree from the mirror (a new
   `commit` on the build request and an `IRepoReader` over the mirror; the owner's checkout is never read for it). A
   busy heavy-job slot is transient;
2. refuses unless the draft's recorded commit is that merge sha (`SND-CI-008`), so a draft from another commit or
   a later scheduled rebuild is never promoted for this merge;
3. promotes it under the **CI promote rule**:
   - deterministic proposals are accepted as `ci:…` (one reversible `map_override` row each; Undo works as for any
     accept). The context engine marks each queue item `deterministic` or `model` (its provenance) and gains a CI mode
     in its promote readiness check;
   - undecided **model** proposals are left out of the promoted version and stay in the queue for a person (O4);
   - everything a person decided stays decided.

So `current` only ever holds deterministic content and what a person accepted. Only the newest green merge in a tick
is built, so a burst of merges costs one build.

### 3.6 Map import (only with O2 "yes"; last phase)

An authored or exported map file under `maps.paths`, in a repo that is `private` right then, is imported as `current`
(all or nothing, `SND-CONTEXT-031`/`032` as today). The import takes the bytes read in §3.2 step 4 (written to a temp
file under `$AW_SINDRI_HOME/ci/tmp/`, 0600, deleted after), so `SND-CONTEXT-005` still refuses a path in any worktree.
The `maps` CI job (§5) prints counts and codes only, never map text, and uploads no artifacts.

### 3.7 Revoke, drift and the terminal fallback

- **Revoke stays `protected`** (human, typed). Revoking a recipe that CI approved parks its instances as today, and a
  later tick never re-approves that hash (`SND-RECIPE-005`). A revert PR doesn't revoke; to back out, revoke at the
  terminal. To stop CI approving from a repo at all, pause `ci-apply` (desk) or remove the entry (profile approval).
- **Drift** (same hash, new block pins after a new build) stays a terminal re-approval in v1 (O5).
- **The terminal path stays**: a human may still approve, import or promote with the typed challenge. Both paths write
  the same events; `via` tells them apart.

## 4. Action classes and callers (API §4)

A new class, **`ci`**: an approval that a merged, checked PR stands in for.

| Class \ caller | `human` + terminal | `human` (app) | `session` | `service` |
|---|---|---|---|---|
| `ci` | no (a human uses the `protected` variant) | no | **no** | **only** `service:ci-apply`, in-process |

- The `ci` commands are **their own request types** (`ApproveRecipeCiRequest`, `ImportMapCiRequest`,
  `PromoteMapCiRequest`) with no HTTP route and no catalog `Http` binding, so no request body can claim them.
- The catalog row names the service: `AllowedServices: [ci-apply]`. `service:engine`, `service:jobs` and the rest are
  refused (`SND-AUTH-002`), and a test says so.
- The deterministic accept inside a CI promote runs as part of `PromoteMapCiRequest`, not as a separate review
  command, so `ReviewMap` keeps its classes.

**What stops a session from using it:**
1. `service` callers never arrive over HTTP (API §4.2), and the `ci` request types have no route.
2. A session can trigger a tick (`sindri services run ci-apply` is a `job`: a session files a request), but a tick only
   re-reads what merged; it can't supply content.
3. A session can't widen the scope: `ci:` is in the profile, and profile approval is human.
4. A merge counts only when a listed merger made it (`mergers`). **The meta's batch merges count if they are made
   under a listed login**, and that is the owner's choice: list only logins that merge on the owner's go, never the
   agent App's bot login.

## 5. The CI half (GitHub Actions)

A workflow in each CI repo produces the checks `ci-apply` requires. It needs no Sindri server: the checks run the
in-process verbs.

```yaml
# .github/workflows/sindri-ci.yml (this repo; recipes)
name: sindri-ci
on:
  pull_request:
  push:
    branches: [main]
permissions: { contents: read }
jobs:
  recipes:                      # the check run's name is the job's name: "recipes"
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<full sha>      # pinned by sha; Dependabot updates them
      - uses: actions/setup-dotnet@<full sha>
        with: { global-json-file: global.json }
      - run: scripts/ci/sindri-recipes-check.sh   # validate + eval every file under recipes/sindri/; exit 1 on any failure
```

- **No path filter on the workflow.** A path-filtered workflow that doesn't run leaves no check, which reads as
  pending and then `002`, and as a required check it blocks unrelated PRs. The job itself exits 0 fast when no recipe
  changed.
- The `push` trigger makes the check run **on the merge commit**, which is what `ci-apply` checks.
- Never `pull_request_target`. `CODEOWNERS` covers `.github/workflows/`, `scripts/ci/` and `recipes/`, so a change to
  the check or to what it checks needs the owner's review where branch protection asks for it.
- `recipe eval` needs no model egress (the eval engine is pure).
- **Outside this repo** (a `promote` repo's `build` check is that repo's own CI; a `maps` repo needs `sindri context
  map validate`): a maps repo needs a released `sindri` CLI, which doesn't exist before the switch. So the maps job, and
  with it map import, waits for a release channel (CI-P8).

`scripts/ci/sindri-recipes-check.sh` is a new script; profile validate warns when an entry's verb paths aren't under
paths the named job checks (a static list the script declares).

## 6. What the owner sees

### 6.1 A CI approval

Every `ci.apply.applied` puts one line in the app's activity on Home and in `sindri ci status`:

> **CI approved** recipe `workflow.step` v3 (`id@hash12`) from PR #512, merged by jdoe. Revoke: `sindri recipe revoke workflow.step@hash12`

> **CI promoted** the map of aw-split-panes to v14 (merge `4f1c2a9`, PR #88): 31 accepted automatically, 6 model proposals waiting for review.

The approval rows in `recipe list` and the map's version history show `via ci` with the PR link.

### 6.2 A failure: the needs-you row

One row per failed `(repo, path)` (needs-you kind `setup`; the existing chip with its word and colour, and its accessible
name, as in the UI spec), cleared by the next successful apply of that path or by Dismiss:

| Part | Text |
|---|---|
| Title | `CI couldn't apply recipes/sindri/workflow.step.yaml` |
| Reason | The code's one line from §6.4, e.g. "The check `recipes` didn't succeed on 4f1c2a9." |
| Actions | **Open PR** (the forge link), **Retry** (runs `ci-apply` now, `job`), **Copy terminal command** (when the terminal path can do it), **Dismiss** |

The System screen (where dependencies and engine health live) gets a **CI** block from `GET /ci`: each repo, its last
fetch, its cursor, the last apply per path, open failures, and whether `ci-apply` is paused. Empty states: "No CI repos
in the profile", "Paused", "Catching up: 12 commits since the box was last on".

### 6.3 Back on the forge

With `statusCredential` set, `ci-apply` posts a commit status `sindri/apply` on the merge commit: `success` with what
it applied, or `failure` with the code and its line. Doctor suggests it when it's off.

### 6.4 `sindri ci status` and the codes

```
$ sindri ci status
github.com/jdoe/agentic-workflow  main  cursor 4f1c2a9 (tip)  fetched 1m ago
  recipes/sindri/workflow.step.yaml  applied v3 id@1a2b3c4d5e6f  PR #512  12m ago
  recipes/sindri/workflow.hand-test.yaml  FAILED SND-CI-007  past its capability ceiling  → sindri recipe approve workflow.hand-test@9f8e7d6c5b4a
github.com/jdoe/aw-split-panes  main  cursor 77ab01c  pending: check build in progress (3m)
```

Exit 0 when nothing is failed or pending past 30 minutes, 1 when something needs attention, 2 on a usage or tool error.
`--json` prints the `GET /ci` body.

| Code | Meaning | Fix line |
|---|---|---|
| `SND-CI-001` | A named check didn't succeed on that commit | Fix the PR's check and merge again; or apply it at the terminal |
| `SND-CI-002` | A named check never finished on that commit within 30 minutes | Check the workflow ran on the merge commit (no path filter, no `[skip ci]`), then merge again or apply at the terminal |
| `SND-CI-003` | Not a PR merge by a listed merger | Merge through a PR as a login in `ci.repos[].mergers`, or apply at the terminal |
| `SND-CI-004` | The maps repo isn't private | Make the repo private, or remove `maps` from the entry |
| `SND-CI-005` | The content was refused by its verb (wraps its code: `SND-RECIPE-008`, `SND-CONTEXT-031`, …) | The wrapped code's fix |
| `SND-CI-006` | The forge or the mirror is unavailable (transient; only shown after 1 h) | Check the network and `ci.credential`; doctor `ci:apply` |
| `SND-CI-007` | The recipe is past its capability ceiling | Approve it at the terminal after reading the preview, or raise `allowCapabilities` |
| `SND-CI-008` | No draft built at that merge commit | Retry; doctor `context:map` |

Doctor `ci:apply` warns on an open failure, a mirror not fetched in 24 h, a cursor more than 20 commits behind, a
`maps` repo that isn't private, and a missing `statusCredential`.

## 7. The credential

`ci.credential` is read-only and separate from the babysitter's `workflow.forge.credential`, which can push and open
PRs. As a fine-grained token on the listed repos only: **Contents: read**, **Metadata: read**, **Pull requests: read**,
**Checks: read** (or as a GitHub App installation, the same four). `statusCredential`, if set, has **Commit statuses:
write** only. Repos under different owners need one credential each: a `credential` on the entry wins over `ci`'s.
Neither is ever in the profile; both use the forge's `env:` / `command:` forms.

## 8. What stays human

| Action | Class | Why |
|---|---|---|
| Profile approve (including the `ci:` block) | `protected` | The scope of everything else, CI's included |
| Token grants: `answer-grant`, `answer-grant-tokens`, `answer-accept` | `protected` | Spending and acceptance decisions on a running instance |
| Recipe revoke, and drift re-approval (v1) | `protected` | The owner keeps revoke; a changed block is reviewed |
| A recipe past its capability ceiling | `protected` | Nobody read its preview |
| Map review decisions (accept, reject, rename, move, merge, add, delete, `--apply`) | `desk` / `protected` as today | README X63; except the deterministic accepts inside a CI promote (§3.5) |
| Onyx `up`/`down` and source connections | owner (Onyx O3) | Unchanged |

## 9. The alternative: a self-hosted runner that pushes

A GitHub Actions job on `push` to `main` runs on a self-hosted runner on the owner's box and calls the loopback API with
a CI token (`sindri ci token mint --repo R --verbs …`, itself `protected`), sent as `X-Sindri-Ci-Token`.
- **What stops a session:** the token file must be unreadable to the agent's OS user, so the runner needs its own OS
  user; the CLI refuses the `ci` caller when an agent variable is set.
- **Why it isn't recommended:** a runner on the owner's laptop runs any workflow the repo defines, and needs a second
  OS user, registration and upkeep. A box that's off misses `push` events (they must be re-run by hand: a manual
  step). And Sindri still has to check the merger, the checks and the bytes itself, so the pull service's logic is
  needed anyway.

## 10. Edits this amendment makes, and the ones its phases make

| File | Section | Change | When |
|---|---|---|---|
| API surface spec | §3.7, §4.1, §4.2, §6 | CI-1…CI-4 | This PR |
| Workflow engine spec | W8, W9, §4.1, class table | CI-5…CI-7 | This PR |
| Context retrieval spec | §3 (worktree rule, decisions), §8.7 | CI-8…CI-10 | This PR |
| `docs/sindri/README.md` | X63 | A line: the CI variants, and the deterministic accepts inside a CI promote | This PR |
| API surface spec | §3.3 catalog, §9 error registry | The `ci` rows, `AllowedServices`, `SND-CI-001…008` | CI-P1 |
| Engine spec | §4.1 `recipe.approved` payload | `repo, sha, pr, mergedBy, blob` | CI-P4 |
| `docs/csharp/sindri-profile.md` | new `ci` section | Every key (`check-profile-doc.mjs` enforces it) | CI-P1 |
| `docs/csharp/forge.md` | The port | The three reads | CI-P2 |
| `deps.yaml` | Sindri.Infrastructure → Forge.Core | A project edge (the port is pure; tier 1 → tier 3) | CI-P2 |
| `docs/sindri/ci.md` (new) | Setup | The owner's setup in order: credential, profile block, workflow, branch protection, first tick | CI-P6 |

## 11. Open questions for the owner

- **O1, pull or push.** Pull (§3, recommended) or the self-hosted runner (§9)?
- **O2, maps in a repo.** Map import from CI needs map files in a repo, which puts work data on the forge when the PR
  is opened. Is one private repo you name for maps acceptable, or does CI cover only promote (built from code, no
  file) and import stays at the terminal? *Recommended: promote only until a release channel exists.*
- **O3, who may approve.** This rev requires `mergers`. Which logins? Batch merges by the meta count only if they're
  made under a listed login.
- **O4, model proposals.** A CI promote leaves undecided model proposals out (recommended), or waits for them?
- **O5, drift.** Drift re-approval stays at the terminal in v1 (recommended), or `ci-apply` re-approves when the
  capability diff is empty and the evals pass?
- **O6, generated recipes.** They're approved by the packet freeze today (W9) and stay that way; confirm.
- **O7, the ceiling for new recipes.** A recipe id CI has never approved goes to the terminal unless its capabilities
  are in `allowCapabilities`. Keep the default empty?

## 12. Review rounds

**Round 1** (`/autoplan`, five lenses, 2026-10-19): product NEEDS_WORK (1 HIGH), architecture NEEDS_WORK (4 HIGH),
design BLOCKED on ratings (owner-facing states missing), devex NEEDS_WORK (5 HIGH), security NEEDS_WORK (3 HIGH).
Resolved in rev 2:

| Finding | Resolution |
|---|---|
| Merge authority optional; direct pushes and agent merges approve (product, security H1) | A merged PR by a listed merger is required; `mergers` is required (§3.1, §3.2, §4) |
| Pending checks lose or loop the commit (arch F1, devex) | Three states: pending (cursor stays, 30 min), failed, never finished (`002`) (§3.2) |
| Promote can't be built: no commit on the build, no forge repo, no draft-to-sha binding (arch F2) | `promote` is a repo entry with checks; build at a commit over the mirror; `SND-CI-008` (§3.1, §3.5) |
| Cursor lost on `views rebuild`; start and force-push undefined (arch F3, security M3) | Cursor events, start at `since` or the tip, merge base on a force-push (§3.2, §3.3) |
| No Sindri → Forge dependency (arch F4) | A `deps.yaml` project edge (§10) |
| A body could claim a `ci` variant; any service passes (arch F5) | Separate request types, no route, `AllowedServices` (§4) |
| Nobody reads the capability preview (arch F7) | The capability ceiling (§3.4, O7) |
| Wrong check names; skipped counts; path filters (devex, arch F8, security H3) | Match the check run's `name` from `github-actions`; only `success`; no path filter (§3.2, §5) |
| Private check too late for maps (security H2, arch F9, product) | Said so in §2; visibility at validate, tick and doctor; import last and only with O2 (§2, §3.6, §13) |
| Write-capable fallback credential (devex, security M4, arch F10) | A required, separate read-only `ci.credential`; fine-grained names (§7) |
| No install path for a maps repo (devex) | Map import waits for a release channel (§5, CI-P8) |
| Owner-facing states missing (design) | §6: the approval line, the needs-you row, the System block, empty states, `ci status`, codes with fix lines |
| Auto drift widens the class (security M2) | Drift stays at the terminal in v1 (§3.7, O5) |
| Built-in cadence not wired (arch F11) | `ci.every` drives it; built-ins listed in CI-P1 (§3.2) |
| Missing doc edits (arch F12, devex) | §10 |
| Blob not recorded (security M6) | `blob` in the events (§3.3) |
| Mirror growth (arch F14) | `--prune`, `gc --auto` (§3.2) |

Not taken: binding checks to an app id beyond `github-actions` (one owner, one CI app); a revert PR revoking (§3.7
explains back-out by revoke).

## 13. Phases (after approval)

| Phase | Lane | What | Depends on |
|---|---|---|---|
| CI-P1 | api | Profile `ci:` section and its doc; the `ci` class, `AllowedServices`, the `ci` request types; `SND-CI-*`; the four events and the cursor projection; built-ins in `sindri services`; `GET /ci`, `sindri ci status` | — |
| CI-P2 | api | Forge port reads (`GetCommitChecks`, `GetCommitPull`, `GetRepoVisibility`) in the GitHub and file adapters, `FakeForge`, the read contract, `forge.md`, the `deps.yaml` edge | — |
| CI-P3 | api | The `ci-apply` service: mirror, walk, the three check states, bind, record, needs-you row, doctor `ci:apply`, the forge status | CI-P1, CI-P2 |
| CI-P4 | engine | `ApproveRecipeCiRequest`: register, evals, the capability ceiling, `via: ci` in approvals, the payload and `recipe list` | CI-P1 |
| CI-P5 | context | Build at a commit over the mirror; `PromoteMapCiRequest` with the CI promote rule and the deterministic/model marks | CI-P1 |
| CI-P6 | api | `sindri-ci.yml`, `scripts/ci/sindri-recipes-check.sh`, CODEOWNERS, `docs/sindri/ci.md`; the proof: a recipe PR merged in a scratch repo on the file forge is approved with no typed step, and one past its ceiling raises the row | CI-P3, CI-P4 |
| CI-P7 | ui | The approval line, the needs-you row, the System CI block, `via ci` on approvals and map versions | CI-P1 |
| CI-P8 | context | `ImportMapCiRequest` and `sindri context map validate`; needs O2 "yes" and a released CLI | CI-P5, the switch |

**Parallel plan:** CI-P1 and CI-P2 start together (two api builders). Once CI-P1 merges, CI-P4 (engine), CI-P5
(context) and CI-P7 (ui) run in parallel with CI-P3 (api). CI-P6 closes the recipe path; CI-P5's own proof closes
promote. CI-P8 waits for O2 and the switch.
