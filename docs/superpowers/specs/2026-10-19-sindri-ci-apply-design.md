# Sindri CI apply: map import and promote, and recipe approval, from CI/CD (spec amendment)

**Status:** proposed, rev 3 (2026-10-19), after `/autoplan` rounds 1 and 2 (§12). The owner reads it before it is
marked ready. It is a docs-only amendment: no code lands until the owner approves it and answers the open questions
in §11.

**Decision it implements (owner, 2026-10-19; handoff `docs/sindri/demo-handoff.md` §9c, PR #368, 6a710eeb9):**
map import and promote, and recipe approval, run from CI/CD with no manual step. Profile approval and token grants
stay human unless the owner says otherwise.

**Amends** (the full list of edits, with the repo docs, is §10):
- the API surface spec (`2026-10-10-sindri-api-surface-design.md`): §3.7 (CI-1), §4.1 (CI-2), §4.2 (CI-3) and §6
  (CI-4). CI-1 states a CI promote/import rule; CI-2 adds the `ci` class; CI-3 its authorization row; CI-4 says no
  header, token or route yields it;
- the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`): W8 and W9 (CI-5: a CI approval stands in
  for the typed one), §4.1 (CI-6: the `ci` approval fields) and the action-class table (CI-7: the `ci` row);
- the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`): §3's worktree rule (CI-8: the mirror,
  never a worktree), §3 "Decisions stay human" (CI-9: the CI variants and the deterministic accepts) and §8.7 (CI-10:
  a CI promote leaves model proposals out).

Each amended spec carries a revision note pointing here. Where this text and an amended spec disagree, this text
wins for the three verbs it covers, and only for those.

## 1. What changes, in one paragraph

Today the three verbs are `protected`: the owner types a server challenge at a terminal, bound to the content's hash.
After this amendment, **a pull request that a listed merger merged, with its required checks green on the merge
commit, is the approval**. Sindri applies it itself: a built-in service, `ci-apply`, fetches that repo into its own
mirror, checks the merge with the forge, reads the file's bytes at the merge commit, and applies exactly those bytes.
The ledger records the approval as `ci:<host>/<owner>/<name>@<sha>` with the PR number and the merger, and the System
screen and `sindri ci status` show it with the revoke command beside it. Revoke stays human, and the terminal path
stays as the fallback.

**What is fully automatic, and what isn't:**

| Verb | Automatic when | Still needs a person when |
|---|---|---|
| Recipe approve | The merged recipe uses no capability beyond its ceiling (§3.4): a new version of a recipe already approved, within that version's capabilities; or a new id within `allowCapabilities` | It widens capabilities past the ceiling: a needs-you row with the terminal command. A recipe id's **first** approval is at the terminal unless `allowCapabilities` covers it |
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
  credential: env:SINDRI_CI_TOKEN # read-only (§7), required, never the same pointer as workflow.forge.credential
  statusCredential: env:SINDRI_CI_STATUS_TOKEN   # optional, Commit statuses: write only; turns on the PR status (§6.3)
  repos:
    - repo: github.com/jdoe/agentic-workflow   # host/owner/name; the host must be in workflow.forge.hosts
      branch: main                # default: the forge's default branch
      since: 4f1c2a9              # optional: the first commit to consider (default: the tip when this entry is first approved)
      mergers: [jdoe]             # required: forge logins whose merges count
      recipes:
        paths: ["recipes/sindri/*.yaml"]
        checks: [recipes]         # check-run names (the job's name), from the github-actions app
        allowCapabilities: []     # the ceiling for a recipe id with no approved version (§3.4)
    - repo: github.com/jdoe/aw-split-panes
      mergers: [jdoe]
      credential: env:SINDRI_CI_TOKEN_SPLIT     # optional: this entry's own credential (another owner or host)
      promote:
        profileRepo: aw-split-panes   # the repos[] entry whose map is built and promoted
        checks: [build]           # that repo's own CI job; confirm its name with `sindri ci check` (§6.4)
        paths: ["src/**"]         # optional: only merges touching these build (default: any path not denied)
        minInterval: 1h           # optional: at most one CI promote per interval (default 1h)
    # maps: not in v1 (O2, CI-P8)
```

- Each entry names the verbs it may apply: `recipes`, `promote`, and (CI-P8) `maps`. A verb not named is never applied
  from that repo. Each verb needs at least one check, and each entry needs `mergers`; profile validate refuses either
  missing (`SND-PROFILE-006`, `-007`) and a `credential` pointer equal to `workflow.forge.credential`'s
  (`SND-PROFILE-008`).
- `ci:` is left out of the canonical JSON when absent, so existing approved profiles keep their hash.

### 3.2 The service `ci-apply`

A **built-in** service: source `built-in`, listed in `sindri services` (CI-P1 adds built-ins to that list), paused and
resumed like any other, run on demand as a `job` (`sindri services run ci-apply`, alias `sindri ci run`). Its cadence
is the approved profile's `ci.every`; without a `ci:` block it has no schedule entry. Each tick logs one JSON line per
repo (`Aw.Kernel.Telemetry`: fetched, walked, applied, pending, failed counts) and counts the same as metrics.

For each `ci.repos` entry, a tick:

1. **Fetches** the branch into a Sindri-owned bare mirror (`IGitMirror`, §5.2),
   `$AW_SINDRI_HOME/ci/mirrors/<host>/<owner>/<name>.git` (0700), hardened like `GitPusher`, with `--prune`. `gc` runs
   only after the cursor has moved, never between a fetch and a merge-base computation.
2. **Walks** the first-parent commits from the entry's cursor to the tip and moves the cursor to the tip. The cursor is
   keyed by `(repo, branch, entry hash)`, so removing and re-adding an entry, or changing its branch, starts fresh. It
   starts at `since`, or at the tip when the entry is first approved, so history is never applied (the owner applies
   older files at the terminal). If the cursor is no longer an ancestor of the tip (a force-push), it moves to the
   merge base with reason `force-push` and raises a repo row (§6.2, `SND-CI-009`).
3. **Picks candidates.** A walked commit is a candidate only if it is the `merge_commit_sha` of a PR that is
   `merged`, whose base is the entry's branch (`GetMergedPull`; for a rebase merge that is the PR's last commit, and the
   commits before it are part of the same PR, never candidates). Commits that are no merged PR's merge commit (a direct
   push) are `SND-CI-003`. A candidate counts for a verb when the PR changed one of that verb's paths. **Only the newest
   candidate per path is kept**; older ones are recorded `superseded` before anything is checked.
4. **Checks** each kept candidate:
   - the PR's merger is in `mergers`, else `SND-CI-003`;
   - for each named check, the **newest** check run with that `name` from the `github-actions` app **on that commit**
     (the job's name, not the `workflow / job` label the UI shows) concluded `success`. `skipped`, `neutral`,
     `cancelled`, `timed_out` and `failure` fail (`SND-CI-001`);
   - a check that is queued, in progress or not there yet is **pending**: `ci.apply.pending` is recorded once with its
     start time, and the candidate is looked at again on every tick. A pending candidate becomes `SND-CI-002` when 30
     minutes of **ticks** have seen it pending (time the box was off doesn't count).
5. **Binds** the content: read the file at that commit from the mirror (`git cat-file`), and record `(repo, sha, path,
   blob, hash)`, where `hash` is the canonical hash the terminal path uses. What is applied is exactly those bytes; the
   verb hashes them again and refuses a mismatch. Nothing is read from a working tree.
6. **Applies** in-process as caller `service:ci-apply` (§4), through the command chain, and records the outcome (§3.3).

**Failed checks can be re-run.** A candidate that failed with `001` or `002` stays **watched** until a newer candidate
for the same path supersedes it or 7 days pass: every tick re-reads its checks, so re-running the failed job on GitHub
and getting `success` applies it with no other step. A candidate that failed for any other reason is re-tried by
**Retry** (`RetryCiApply`, `desk`; §6.2), after the owner fixed the cause (added the merger, raised the ceiling, ...).

A file deleted on the branch applies nothing, and removal never revokes (§3.7).

### 3.3 The events

Every state is a ledger event, so `views rebuild` rebuilds the cursor, the watched set and the history:

| Event | When | Payload (public: ids, hashes, codes; no file text) |
|---|---|---|
| `ci.apply.applied` | A verb applied (or the bytes were already approved: `noop: true`) | `repo, sha, path, blob, verb, hash, pr, mergedBy, noop` |
| `ci.apply.pending` | First tick a candidate's check is pending | `repo, sha, path, verb, check, since` |
| `ci.apply.failed` | A failure (watched for `001`/`002`) | `repo, sha, path, verb, code, wrappedCode?, check?, pr?` |
| `ci.apply.superseded` | A newer candidate for the path | `repo, sha, path, by` |
| `ci.apply.retried` | `RetryCiApply` re-opened a failure | `repo, sha, path, by` |
| `ci.cursor.moved` | Once per tick per repo | `repo, branch, entry, from, to, reason (tick, first, force-push)` |

A transient error (forge or network down, the heavy-job slot busy) records nothing and the tick tries again; it is
shown as `SND-CI-006` only after 1 hour. A failed Temporal wake after an approval row is stored is transient: the next
tick sees `AlreadyApproved` and wakes again.

### 3.4 Recipe approve

Register the file (the desk path, as `service:ci-apply`), run the validator and its evals in-process, then approve
with `via: ci`. Failing or missing evals refuse (`SND-RECIPE-008`, wrapped as `SND-CI-005`), exactly as at the terminal.

**The capability ceiling.** At the terminal the owner reads the capability preview. Under CI nobody does, so CI may
only approve a recipe whose capability uses (block, transition, capabilities, parameters, the effect kinds) are a
subset of:
- the **currently approved** (not revoked) version of the same recipe id with the highest version, or
- for an id with no approved version, the entry's `allowCapabilities` (default: none).

A recipe past its ceiling is `SND-CI-007`: a needs-you row with the preview and the terminal command
(`sindri recipe approve <id@hash>`). A crash between register and approve leaves a registered, unapproved recipe; the
next tick finishes it.

**Reverts.** A revert PR whose file bytes equal a hash that is already approved applies as `noop` (no row). If that
hash was revoked, it fails with `SND-CI-005` wrapping `SND-RECIPE-005`; the fix is a new version at the terminal.

### 3.5 Map promote

No file merges. For a candidate in a `promote` entry (changing `promote.paths`, at least `minInterval` after the last
CI promote of that repo; a skipped one is `superseded` by the next), with its checks green, `ci-apply`:
1. asks the context engine to **build the draft with that repo at the merge commit**, reading it from the mirror (the
   build request gains `repo` and `commit`, and an `IRepoReader` over the mirror). Other repos the map covers are
   read at the commit they were last built at, which the draft records per repo; the owner's checkouts are never read
   for a CI build. A busy heavy-job slot is transient;
2. refuses unless the draft's recorded commit for that repo is the merge sha (`SND-CI-008`), so a draft from another
   commit or a later scheduled rebuild is never promoted for this merge;
3. writes **no new version when the promoted content would equal `current`** (`noop`);
4. otherwise promotes under the **CI promote rule**:
   - deterministic proposals are accepted as `ci:…` (one reversible `map_override` row each; Undo works as for any
     accept). The context engine marks each queue item `deterministic` or `model` (its provenance) and gains a CI mode
     in its promote readiness check;
   - undecided **model** proposals are left out of the promoted version and stay in the queue for a person (O4);
   - everything a person decided stays decided.

So `current` only ever holds deterministic content and what a person accepted.

### 3.6 Map import (only with O2 "yes"; CI-P8)

An authored or exported map file under `maps.paths`, in a repo whose visibility is `private` right then (checked at
validate, each tick and doctor; `internal`, `public` and unknown refuse with `SND-CI-004`), is imported as `current`
(all or nothing, `SND-CONTEXT-031`/`032` as today). The import takes the bytes read in §3.2 step 5 through a temp file
under `$AW_SINDRI_HOME/ci/tmp/` (0600, deleted after), so `SND-CONTEXT-005` still refuses a path in any worktree; CI-P8
also adds a guard so `context map export` never writes into the maps repo's checkout. The `maps` CI job prints counts
and codes only, never map text, and uploads no artifacts.

### 3.7 Revoke, drift and the terminal fallback

- **Revoke stays `protected`** (human, typed). Revoking a recipe that CI approved parks its instances as today, and a
  later tick never re-approves that hash (`SND-RECIPE-005`). A revert PR doesn't revoke (§3.4); to back out, revoke at
  the terminal. To stop CI approving from a repo at all, pause `ci-apply` (desk) or remove the entry (profile approval).
- **Drift** (same hash, new block pins after a new build) stays a terminal re-approval in v1 (O5).
- **The terminal path stays**: a human may still approve, import or promote with the typed challenge. Both paths write
  the same events; `via` tells them apart, and a terminal approval of the hash a CI row is about clears that row.

## 4. Action classes and callers (API §4)

A new class, **`ci`**: an approval that a merged, checked PR stands in for.

| Class \ caller | `human` + terminal | `human` (app) | `session` | `service` |
|---|---|---|---|---|
| `ci` | no (a human uses the `protected` variant) | no | **no** | **only** `service:ci-apply`, in-process |

- The `ci` commands are **their own request types** (`ApproveRecipeCiRequest`, `ImportMapCiRequest`,
  `PromoteMapCiRequest`) with no HTTP route and no catalog `Http` binding, so no request body can claim them and a
  `POST` to such a path is a plain 404.
- The catalog row grants the one service through the existing `ServiceGrants: ["ci-apply"]`; `service:engine`,
  `service:jobs` and the rest are refused (`SND-AUTH-002`), and a test says so.
- The deterministic accepts inside a CI promote run as part of `PromoteMapCiRequest`, not as a separate review
  command, so `ReviewMap` keeps its classes.
- `RetryCiApply` and `CheckCi` (§6.4) are ordinary rows: `desk` and query.

**What stops a session from using it:**
1. `service` callers never arrive over HTTP (API §4.2), and the `ci` request types have no route.
2. A session can trigger a tick (`sindri services run ci-apply` is a `job`: a session files a request), but a tick only
   re-reads what merged; it can't supply content.
3. A session can't widen the scope: `ci:` is in the profile, and profile approval is human.
4. A merge counts only when a listed merger made it. **The meta's batch merges count if they are made under a listed
   login**, and that is the owner's choice: list only logins that merge on the owner's go, never the agent App's bot
   login.

## 5. The CI half and the forge

### 5.1 GitHub Actions

A workflow in this repo produces the `recipes` check. It needs no Sindri server: it runs the in-process verbs.

```yaml
# .github/workflows/sindri-ci.yml
name: sindri-ci
on:
  pull_request:
  merge_group:
  push:
    branches: [main]
permissions: { contents: read }
concurrency:
  group: sindri-ci-${{ github.event_name }}-${{ github.sha }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}   # never cancel a run on the merge commit
jobs:
  recipes:                      # the check run's name is the job's name: "recipes"
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@<full commit sha>      # CI-P6 picks the shas; Dependabot (github-actions) updates them
      - uses: actions/setup-dotnet@<full commit sha>
        with: { global-json-file: global.json }
      - run: scripts/ci/sindri-recipes-check.sh   # validate + eval every changed recipe/sindri/*.yaml; exit 0 fast when none changed
```

- **No path filter.** A path-filtered workflow that doesn't run leaves no check (pending, then `002`), and as a
  required check it blocks unrelated PRs. The job exits 0 fast when no recipe changed (`git diff --name-only` against
  the event's base).
- The `push` trigger makes the check run **on the merge commit**, which is what `ci-apply` checks; `merge_group` keeps a
  merge queue moving if the owner turns one on.
- Never `pull_request_target`. `CODEOWNERS` covers `.github/workflows/`, `scripts/ci/` and `recipes/`. Branch
  protection can't be read with a read-only token, so it is documented in `docs/sindri/ci.md`, not checked.
- `recipe eval` needs no model egress (the eval engine is pure).
- **A promote repo's check is that repo's own CI** (for example its `build` job); `sindri ci check` shows the check
  names it finds on the last merge so the owner can copy the right one.
- **A maps repo** needs a released `sindri` CLI for `sindri context map validate`, which doesn't exist before the
  switch, so map import waits (CI-P8).

### 5.2 The forge port and the dependency edges

- `Aw.Forge.Core` gains: `GetMergedPull(repo, sha)` (the merged PR whose `merge_commit_sha` is `sha` and whose base
  is the branch, with its merger and changed paths), `GetCommitChecks(repo, sha)` (check runs with `name`, app slug,
  status, conclusion, started time), `GetRepoVisibility(repo)`, and one write, `PostCommitStatus(repo, sha, state,
  description)`; plus a new port `IGitMirror` (fetch a branch into a bare mirror, read a blob, list first-parent commits,
  merge base), implemented in `Aw.Forge.Infrastructure` next to `GitPusher`, sharing its hardening.
- `IForgeClients.For(host, credential)` returns a client per host and credential, so the ci read client, the status
  client and the babysitter's client are separate (today one `IForge` is built from the environment).
- **Edges** (`deps.yaml`): `Aw.Sindri.Features → Aw.Forge.Core` (a project edge: Features reach another package through
  its Core) and `Aw.Sindri.Host` composes `Aw.Forge.Infrastructure` (a compose edge, DEPS-COMPOSE-001).
  `Aw.Sindri.Infrastructure` takes no Forge reference.

## 6. What the owner sees

### 6.1 A CI approval

Each `ci.apply.applied` (not `noop`) shows in two places, never as a needs-you row (nothing is owed):
- the System screen's `ci:apply` node (§6.2) lists the last 20 applies:
  > **CI approved** recipe `workflow.step` v3 (`id@hash12`) from PR #512, merged by jdoe · Revoke: `sindri recipe revoke workflow.step@hash12`
  >
  > **CI promoted** the map of aw-split-panes to v14 (merge `4f1c2a9`, PR #88): 31 accepted automatically · [6 model proposals waiting](Context review)
- `recipe list` and the map's version history show `via ci` with the PR link.

### 6.2 A failure: the needs-you row

A failed apply raises a needs-you row of the existing kind **`failure`** (UI spec chips: the word and colour, and the
accessible name, as for any failure):

| Field | Value |
|---|---|
| `id` | `failure:ci:<repo>:<path>` (a path row) or `failure:ci:<repo>` (a repo row: `004`, `006` after 1 h, `009`) |
| `subject` | `{service, ci-apply}` |
| title | `CI couldn't apply recipes/sindri/workflow.step.yaml` (a path inside a denied glob is shown as its verb and repo only) |
| reason | the code's line from §6.4, filled from the event (`check`, `pr`, `wrappedCode`): "The check `recipes` didn't succeed on 4f1c2a9 (PR #512)." |
| `primaryAction` | `RetryCiApply` (`desk`) for `003`, `005`, `007`, `008`; none for `001`/`002` (re-run the check on GitHub; the row says so); `RunService` for a repo row |
| `cli` | the code's terminal command: `sindri recipe approve <id@hash>` for `007`, `sindri ci retry <repo> <sha> <path>` otherwise |
| `link` | the PR (the row's link field; no new action) |

It clears when that path applies (by CI, or by a terminal approval of the same hash), when a newer candidate for that
path supersedes it, or, for a repo row, when the next tick succeeds. There is no Dismiss: needs-you acknowledgement is
deferred (API §11). A failure raises no OS notification (notifications come only from the engine's `notify.desk`).

The **System screen** shows CI as the doctor check node `ci:apply` and its detail view (no new screen): each repo, its
last fetch, cursor, pending and watched candidates, the last applies, open failures, and whether `ci-apply` is paused.
States and chip words: **ok**, **Waiting for checks** (pending), **Catching up** (more than 5 commits walked in one
tick), **Paused**, **Failed**; "No CI repos in the profile" when there is no `ci:` block.

`GET /ci` returns `{ repos: [ { repo, branch, cursor, fetchedAt, state, pending: [ {sha, path, check, since} ],
watched: [ {sha, path, code, check} ], recent: [ {sha, path, verb, hash, pr, mergedBy, at} ], failures: [ {sha?, path?,
code, wrappedCode?} ] } ], paused }` (query; a session sees only its repo).

### 6.3 Back on the forge

With `statusCredential` set, `ci-apply` posts a commit status `sindri/apply` on the merge commit: `pending` while it
waits for checks, then `success` with what it applied, or `failure` with the code and its line. Doctor suggests it when
it's off.

### 6.4 `sindri ci status`, `sindri ci check` and the codes

```
$ sindri ci status
github.com/jdoe/agentic-workflow  main  cursor 4f1c2a9 (tip)  fetched 1m ago  ok
  recipes/sindri/workflow.step.yaml       applied v3 id@1a2b3c4d5e6f  PR #512  12m ago
  recipes/sindri/workflow.hand-test.yaml  FAILED SND-CI-007 past its capability ceiling  → sindri recipe approve workflow.hand-test@9f8e7d6c5b4a
github.com/jdoe/aw-split-panes  main  cursor 77ab01c  Waiting for checks: build on 77ab01c (3m)
```

Exit 0 when nothing is failed or pending past 30 minutes, 1 when something needs attention, 2 on a usage or tool error.
`--json` prints the `GET /ci` body.

`sindri ci check [--repo R]` is a **dry run** (query): it fetches, then for the tip and the last 5 merges of each entry
says what a tick would do: the PR and its merger (listed or not), the check runs it found by name and app, which
named checks are missing, the paths each verb would apply, and the ceiling result for each recipe. It applies nothing.
Run it after editing the `ci:` block to catch a wrong check name before the first real merge.

| Code | Meaning | Fix line |
|---|---|---|
| `SND-CI-001` | A named check didn't succeed on that commit | Re-run the failed check on the merge commit in GitHub; ci-apply re-reads it for 7 days |
| `SND-CI-002` | A named check never finished on that commit within 30 minutes | Run the workflow on the merge commit (no path filter, no `[skip ci]`), or fix the check's name with `sindri ci check` and Retry |
| `SND-CI-003` | Not a PR merge, or merged by a login not in `mergers` | Add the login to `ci.repos[].mergers` (profile approval), then Retry; or apply it at the terminal |
| `SND-CI-004` | The maps repo isn't private | Make the repo private, or remove `maps` from the entry |
| `SND-CI-005` | The content was refused by its verb (wraps its code: `SND-RECIPE-008`, `SND-CONTEXT-031`, …) | The wrapped code's fix, in a new PR |
| `SND-CI-006` | The forge or the mirror is unavailable (transient; shown after 1 h) | Check the network; doctor `ci:apply` |
| `SND-CI-007` | The recipe is past its capability ceiling | Read the preview and approve it at the terminal; or raise `allowCapabilities` (profile approval) and Retry |
| `SND-CI-008` | No draft built for that repo at that merge commit | Retry; doctor `context:map` |
| `SND-CI-009` | The branch was force-pushed; the cursor moved back to the merge base | Check the branch's history; nothing applied from the rewritten commits |
| `SND-CI-010` | The forge refused the credential (401 or 403, expired or missing a permission) | Renew `ci.credential` with the §7 permissions; doctor `ci:apply` names the missing one |

`SND-CI-010` is shown at once, not after an hour. Doctor `ci:apply` warns on an open failure, a mirror not fetched in
24 h, a cursor more than 20 commits behind, a `maps` repo that isn't private, a credential that is refused or lacks a
permission (a probe read per permission), and a missing `statusCredential`. Each code joins `docs/sindri/errors.md`.

## 7. The credential

`ci.credential` is read-only and separate from the babysitter's `workflow.forge.credential`, which can push and open
PRs. Validate refuses the same pointer; it can't compare the tokens behind two pointers, so `docs/sindri/ci.md` says to
use a separate token.

**Setup (GitHub, fine-grained personal access token):**
1. GitHub → Settings → Developer settings → Fine-grained tokens → Generate new token.
2. Resource owner: the repos' owner. Repository access: **Only select repositories**, the `ci.repos` of that owner.
3. Repository permissions: **Contents: Read-only**, **Metadata: Read-only**, **Pull requests: Read-only**,
   **Checks: Read-only**. Nothing else. Expiry: the owner's choice (doctor warns 7 days before, from `SND-CI-010`'s
   probe).
4. Export it where `sindri serve` reads it (`SINDRI_CI_TOKEN`), or point `credential` at a program that prints it
   (`command:<argv>`, for example a keychain read).

A GitHub App installation with the same four permissions works too. `statusCredential`, if set, is a second token with
**Commit statuses: Read and write** only. One credential serves one host and one owner; an entry under another owner or
host sets its own `credential`.

## 8. What stays human

| Action | Class | Why |
|---|---|---|
| Profile approve (including the `ci:` block) | `protected` | The scope of everything else, CI's included |
| Token grants: `answer-grant`, `answer-grant-tokens`, `answer-accept` | `protected` | Spending and acceptance decisions on a running instance |
| Recipe revoke, and drift re-approval (v1) | `protected` | The owner keeps revoke; a changed block is reviewed |
| A recipe past its capability ceiling, and a recipe id's first approval (unless `allowCapabilities` covers it) | `protected` | Nobody read its preview |
| Map review decisions (accept, reject, rename, move, merge, add, delete, `--apply`) | `desk` / `protected` as today | README X63; except the deterministic accepts inside a CI promote (§3.5) |
| Onyx `up`/`down` and source connections | owner (Onyx O3) | Unchanged |

## 9. The alternative: a self-hosted runner that pushes

A GitHub Actions job on `push` to `main` runs on a self-hosted runner on the owner's box and calls the loopback API with
a CI token (`sindri ci token mint --repo R --verbs …`, itself `protected`). The token file must be unreadable to the
agent's OS user, so the runner needs its own OS user. Not recommended: the runner runs any workflow the repo defines,
needs registration and upkeep, misses `push` events while the box is off (re-run by hand: a manual step), and Sindri
still has to check the merger, the checks and the bytes itself.

## 10. Edits this amendment makes, and the ones its phases make

| File | Section | Change | When |
|---|---|---|---|
| API surface spec | §3.7, §4.1, §4.2, §6 | CI-1…CI-4 | This PR |
| Workflow engine spec | W8, W9, §4.1, class table | CI-5…CI-7 | This PR |
| Context retrieval spec | §3 (worktree rule, decisions), §8.7 | CI-8…CI-10 | This PR |
| `docs/sindri/README.md` | X63 | A line: the CI variants, and the deterministic accepts inside a CI promote | This PR |
| API surface spec | §3.3 catalog, §3.6 needs-you, §9 error registry | The `ci` rows with `ServiceGrants`, `RetryCiApply`, `CheckCi`; the `failure:ci:*` rows; `SND-CI-001…010`, `SND-PROFILE-006…008` | CI-P1 |
| UI screens spec | System page, needs-you | The `ci:apply` node and detail view; the `failure:ci:*` rows' fields | CI-P7 |
| Engine spec | §4.1 `recipe.approved` payload | `repo, sha, blob, pr, mergedBy` | CI-P4 |
| Context spec | §8.7, build | Build at a commit per repo; the deterministic/model marks; the CI promote rule | CI-P5 |
| `docs/csharp/sindri-profile.md` | new `ci` section | Every key (`check-profile-doc.mjs` enforces it) | CI-P1 |
| `docs/sindri/errors.md` | `SND-CI-*`, `SND-PROFILE-006…008` | The codes and fix lines | CI-P1 |
| `docs/csharp/forge.md` | The port | The four calls, `IGitMirror`, `IForgeClients` | CI-P2 |
| `deps.yaml` | Sindri.Features → Forge.Core; Sindri.Host composes Forge.Infrastructure | §5.2 | CI-P2 |
| `docs/sindri/ci.md` (new) | Setup | In order: the credential (§7), the profile block, `sindri ci check`, the workflow, CODEOWNERS and branch protection, the first tick | CI-P3 |

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
- **O7, the ceiling for new recipe ids.** A recipe id with no approved version goes to the terminal unless its
  capabilities are in `allowCapabilities`. Keep the default empty (recommended: the first approval is the one
  preview a person reads), or list capabilities CI may approve on a new id?

## 12. Review rounds

**Round 1** (`/autoplan`, five lenses, 2026-10-19): product NEEDS_WORK (1 HIGH), architecture NEEDS_WORK (4 HIGH),
design BLOCKED on ratings (owner-facing states missing), devex NEEDS_WORK (5 HIGH), security NEEDS_WORK (3 HIGH).
Resolved in rev 2:

| Finding | Resolution |
|---|---|
| Merge authority optional; direct pushes and agent merges approve (product, security H1) | A merged PR by a listed merger is required; `mergers` is required (§3.1, §3.2, §4) |
| Pending checks lose or loop the commit (arch F1, devex) | Pending, failed, never finished (`002`) (§3.2) |
| Promote can't be built: no commit on the build, no forge repo, no draft-to-sha binding (arch F2) | `promote` is a repo entry with checks; build at a commit over the mirror; `SND-CI-008` (§3.1, §3.5) |
| Cursor lost on `views rebuild`; start and force-push undefined (arch F3, security M3) | Cursor events, start at `since` or the tip, merge base on a force-push (§3.2, §3.3) |
| No Sindri → Forge dependency (arch F4) | A `deps.yaml` edge (§5.2; corrected in rev 3) |
| A body could claim a `ci` variant; any service passes (arch F5) | Separate request types, no route, a service grant (§4) |
| Nobody reads the capability preview (arch F7) | The capability ceiling (§3.4, O7) |
| Wrong check names; skipped counts; path filters (devex, arch F8, security H3) | Match the check run's `name` from `github-actions`; only `success`; no path filter (§3.2, §5) |
| Private check too late for maps (security H2, arch F9, product) | Said so in §2; visibility at validate, tick and doctor; import last and only with O2 (§2, §3.6, §13) |
| Write-capable fallback credential (devex, security M4, arch F10) | A required, separate read-only `ci.credential` (§7) |
| No install path for a maps repo (devex) | Map import waits for a release channel (§5.1, CI-P8) |
| Owner-facing states missing (design) | §6 |
| Auto drift widens the class (security M2) | Drift stays at the terminal in v1 (§3.7, O5) |
| Built-in cadence not wired (arch F11) | `ci.every` drives it; built-ins listed in CI-P1 (§3.2) |
| Missing doc edits (arch F12, devex) | §10 |
| Blob not recorded (security M6) | `blob` in the events (§3.3) |
| Mirror growth (arch F14) | `--prune`; `gc` after the cursor moves (§3.2) |

**Round 2** (2026-10-19): product PASS (0 HIGH), security PASS (0 HIGH, 5 MEDIUM), architecture NEEDS_WORK (1 HIGH),
design NEEDS_WORK (5 HIGH), devex NEEDS_WORK (3 HIGH). Resolved in rev 3:

| Finding | Resolution |
|---|---|
| A failed check can't be re-run; Retry does nothing on a final failure; "merge again" is impossible (devex H1, design H3, arch N4, product) | `001`/`002` candidates are watched for 7 days and re-read every tick; `RetryCiApply` re-opens the others after the cause is fixed; fix lines rewritten (§3.2, §6.2, §6.4) |
| The newest of several same-name runs; rebase intermediates (devex H1, arch N7) | The newest run per name counts; only a merged PR's `merge_commit_sha` is a candidate, supersede before check (§3.2) |
| "From a merged PR" unspecified; GitHub returns open PRs too (security R2-M1, arch N7) | `GetMergedPull`: merged, `merge_commit_sha` equals the commit, base is the branch (§3.2, §5.2) |
| The 30-minute clock has no stored start (security R2-M2, arch N2, devex) | `ci.apply.pending` with `since`; counted in ticks, so time off doesn't fail a commit (§3.2, §3.3) |
| A pending commit mid-range re-walks applied ones (arch N2b) | The cursor always moves to the tip; pending and watched candidates are their own set (§3.2) |
| The cursor isn't keyed to the entry (security R2-M3, arch N5) | Keyed by repo, branch and entry hash; `gc` never runs before a merge base (§3.2) |
| Every merge promotes (product, security R2-M4) | `promote.paths`, `minInterval`, no version when nothing changed (§3.1, §3.5) |
| The ceiling can follow a revoked version (security R2-M5) | The currently approved version is the baseline (§3.4) |
| `Sindri.Infrastructure → Forge.Core` breaks the tier rules; no compose edge; the fetch has no home (arch N1) | `Sindri.Features → Forge.Core`, Host composes Forge.Infrastructure, `IGitMirror` in Forge (§5.2) |
| One `IForge` can't hold three credentials; the status post isn't in the port (arch N6, devex H3) | `IForgeClients.For(host, credential)`; `PostCommitStatus` (§5.2) |
| The build reads the owner's checkout and records one commit (arch N3) | Per-repo commits on the draft; other repos at their last-built commit (§3.5) |
| `AllowedServices` duplicates `ServiceGrants`; "SND-AUTH-002 over HTTP" contradicts "no route" (arch N9, N10) | `ServiceGrants`; a plain 404; CI-2 and CI-4 corrected (§4) |
| No `setup` needs-you kind; Dismiss has no backing; the row can't hold per-code actions; a terminal fix doesn't clear it (design H1, H2, H4, H5) | Kind `failure`, ids `failure:ci:*`, `primaryAction` and `cli` per code, the PR as the row's link, no Dismiss, cleared by either path (§6.2) |
| Home has no activity area; the System block has no slot; `GET /ci` undefined; chip words; notifications (design) | Applies listed on the System `ci:apply` node; its states and words; `GET /ci` fields; no OS notification (§6.1, §6.2) |
| The sample workflow has no `merge_group` or concurrency; placeholders (devex H2) | Both added, merge-commit runs never cancelled; CI-P6 picks the shas (§5.1) |
| No literal credential recipe; an expired token reads as transient; one host per adapter (devex H3) | §7's steps; `SND-CI-010` shown at once with a permission probe; a credential per entry (§3.1, §6.4, §7) |
| No dry run (product) | `sindri ci check` (§6.4) |
| Reverts unclear (product, security) | `noop` on an approved hash; a revoked hash fails with its fix (§3.4) |
| No tick logging; docs late; `errors.md` missing; profile codes unnamed (devex) | Per-repo log line and metrics (§3.2); `ci.md` in CI-P3; §10; `SND-PROFILE-006…008` |
| First approval of an existing recipe unclear (product) | §1's table and §8 say it |

Not taken (with reasons): binding checks to one workflow file as well as the app (one owner, one CI app; `sindri ci
check` shows the app); checking branch protection (a read-only token can't read it; documented instead); shrinking §9
further (it is the owner's O1 alternative).

## 13. Phases (after approval)

| Phase | Lane | What | Depends on |
|---|---|---|---|
| CI-P1 | api | Profile `ci:` section, its doc and codes; the `ci` class, `ServiceGrants`, the `ci` request types; `SND-CI-*` and `errors.md`; the six events and the projection (cursor, pending, watched); built-ins in `sindri services`; `GET /ci`, `sindri ci status`; `RetryCiApply` | — |
| CI-P2 | api | Forge: `GetMergedPull`, `GetCommitChecks`, `GetRepoVisibility`, `PostCommitStatus`, `IGitMirror`, `IForgeClients` in the GitHub and file adapters and `FakeForge`, the read contract, `forge.md`, the `deps.yaml` edges | — |
| CI-P3 | api | The `ci-apply` service: mirror, walk, candidates, checks with pending and watched, bind, record, the needs-you rows, doctor `ci:apply` with the permission probe, the forge status, `sindri ci check`, `docs/sindri/ci.md` | CI-P1, CI-P2 |
| CI-P4 | engine | `ApproveRecipeCiRequest`: register, evals, the capability ceiling, `noop` on an approved hash, `via: ci` in approvals, the payload and `recipe list` | CI-P1 |
| CI-P5 | context | Build at a commit per repo over the mirror; `PromoteMapCiRequest` with the CI promote rule, the deterministic/model marks, `noop` when unchanged | CI-P1 |
| CI-P6 | api | `sindri-ci.yml`, `scripts/ci/sindri-recipes-check.sh`, Dependabot for actions, CODEOWNERS; the proof (below) | CI-P3, CI-P4 |
| CI-P7 | ui | The System `ci:apply` node and detail view, the `failure:ci:*` rows, `via ci` on approvals and map versions | CI-P1; the System page and the doctor `consumers` list (screens E-29) |
| CI-P8 | context | `ImportMapCiRequest`, `sindri context map validate`, the export guard; needs O2 "yes" and a released CLI | CI-P5, the switch |

**The proof (CI-P6), on the file forge in a scratch home:** a recipe PR that is a new version of an approved recipe,
within its ceiling, is approved with no typed step and shows on the System node; one past its ceiling raises the row
with the terminal command; a failed check re-run to green applies on the next tick; a merge by an unlisted login
raises `003`, and Retry after adding the login applies it. **Measures:** time from merge to apply (target: one tick
after the checks finish), and the share of recipe merges applied without a terminal step, reported by `sindri ci
status --json` over the first two weeks.

**Parallel plan:** CI-P1 and CI-P2 start together (two api builders). Once CI-P1 merges, CI-P4 (engine), CI-P5
(context) and CI-P7 (ui) run in parallel with CI-P3 (api). CI-P6 closes the recipe path; CI-P5's own proof closes
promote. CI-P8 waits for O2 and the switch.
