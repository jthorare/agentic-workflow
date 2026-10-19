# Sindri CI apply: map import and promote, and recipe approval, from CI/CD (spec amendment)

**Status:** proposed, rev 1 (2026-10-19). The owner reads it before it is marked ready. It is a docs-only amendment:
no code lands until the owner approves it and answers the open questions in §9.

**Decision it implements (owner, 2026-10-19; handoff `docs/sindri/demo-handoff.md` §9c, PR #368, 6a710eeb9):**
map import and promote, and recipe approval, run from CI/CD with no manual step. Profile approval and token grants
stay human unless the owner says otherwise.

**Amends:**
- the API surface spec (`2026-10-10-sindri-api-surface-design.md`): §3.7 (CI-1), §4.1 (CI-2), §4.2 (CI-3) and §6 (CI-4);
- the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`): W8 and W9 (CI-5), §4.1 (CI-6) and the action-class table (CI-7);
- the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`): §3's worktree rule (CI-8), §3 "Decisions stay human" (CI-9) and
  §8.7 (CI-10).

Each amended spec carries a revision note pointing here. Where this text and an amended spec disagree, this text
wins for the three verbs it covers, and only for those.

## 1. What changes, in one paragraph

Today the three verbs are `protected`: the owner types a server challenge at a terminal, bound to the content's hash.
After this amendment the **reviewed and merged pull request is the approval**. A map file or recipe file that
merges into the default branch of a repo the approved profile lists, with the required checks green on the merge
commit, is applied by Sindri itself: a built-in service, `ci-apply`, reads the merged file from its own mirror of
that repo, checks its hash against the merge commit, and applies it. The ledger records the approval as
`ci:<host>/<owner>/<name>@<sha>` with the PR number and the merger. The owner keeps revoke, and the terminal path stays
as a fallback.

## 2. Two things the current system makes hard, and how this design answers them

1. **Sindri listens on loopback only** (API §6). A GitHub-hosted runner can't reach it, and the owner's box isn't
   always on. So CI can't *push* the apply. Sindri *pulls* instead: the CI half (GitHub Actions) proves the content,
   and Sindri's CD half applies what merged the next time it runs. A box that was off catches up when it starts.
   §7 compares this with a self-hosted runner that pushes.
2. **Map files are kept outside every repo** (context §3, `SND-CONTEXT-005`), because a map can carry work data
   (decision quotes from intent sources, issue keys). A map in a repo's PR puts that text on the forge. This
   design keeps the guard for every other path, and lets a map be imported from a CI repo only when the owner names
   that repo for maps and the forge reports it **private** (§3.2). Promote needs no file at all (§3.3).

## 3. The design

### 3.1 The profile's `ci:` block (human-approved scope)

The CI identity's whole scope lives in the profile, so adding or widening it is a profile change, and profile
approval stays `protected` (human, typed). There is no new credential that grants Sindri anything.

```yaml
ci:
  every: 2m                       # the ci-apply service's cadence (5s..24h; default 2m)
  repos:
    - repo: github.com/jdoe/agentic-workflow   # host/owner/name; host must be in workflow.forge.hosts
      branch: main                # default: the forge's default branch
      recipes:
        paths: ["recipes/sindri/*.yaml"]
        checks: ["sindri-ci / recipes"]        # check runs that must have succeeded on the merge commit
      mergers: [jdoe]             # optional: forge logins whose merges count (§9 O3)
    - repo: github.com/jdoe/sindri-maps
      maps:
        paths: ["maps/*.yaml"]
        checks: ["sindri-ci / maps"]
        repoName: aw-split-panes # the profile repos[] entry the map describes
  promote:
    repos: [aw-split-panes]       # profile repos whose draft ci-apply may promote (§3.3)
```

- A verb not named for a repo is never applied from it. `recipes` and `maps` each need at least one check: CI apply
  with no required check is refused at profile validate (`SND-PROFILE-*`, next free code).
- The block's repos must use the forge adapter `github` (or `file` in tests and proofs).
- `ci:` is left out of the canonical JSON when absent, so existing approved profiles keep their hash.

### 3.2 The service `ci-apply`

A **built-in** service (source `built-in`, listed in `sindri services`, paused and resumed like any other; the API
lane's built-in services follow-up lists it there). On each tick, for each `ci.repos` entry:

1. **Fetch** the branch into a Sindri-owned bare mirror, `$AW_SINDRI_HOME/ci/mirrors/<host>/<owner>/<name>.git`
   (0700), hardened like `GitPusher` (no repo or global config, hooks at `/dev/null`, the forge credential through
   `GIT_CONFIG_*` env, never an argument). The owner's checkouts are never touched.
2. **Walk** the first-parent commits from the repo's stored cursor to the branch tip. A commit is a candidate when
   it changes a declared path. The cursor is per repo and stored in the ledger's projection (`ci_cursor`), so a
   restart resumes and a replayed tick applies nothing twice.
3. **Check** with the forge: every check named for that verb concluded `success` **on that exact commit**; the
   commit is reachable from the branch (no force-push rewrote it); and, when `mergers` is set, the PR that produced it
   was merged by a listed login. The forge port gains three reads for this (§5): `GetCommitChecks`,
   `GetCommitPull` and `GetRepoVisibility`.
4. **Bind** the content: read the file at that commit from the mirror (`git cat-file`), compute the same canonical
   hash the terminal path uses (recipe hash; map file content hash), and record `(repo, sha, path, hash)`. What is
   applied is exactly those bytes; nothing is read from a working tree.
5. **Apply** in-process as caller `service:ci-apply` through the command chain, with the new action class `ci`
   (§4). For a map import, the repo must report `private` at that moment; public or unknown refuses
   (`SND-CI-004`).
6. **Report** (§3.5) and move the cursor past the commit, whether it applied or failed with a content error. A
   transient error (forge or network down, heavy-job slot busy) leaves the cursor and retries next tick.

Only the **newest** candidate per path in one tick is applied; older ones are recorded `superseded`. A file
deleted on the branch applies nothing: removal never revokes (§3.4).

### 3.3 Per verb

| Verb | What merges | What ci-apply does | Bound to |
|---|---|---|---|
| Recipe approve | A recipe file under `recipes.paths` | Register it (desk path, as `service:ci-apply`), run the validator and its evals in-process, and approve with `via: ci`. Failing or missing evals refuse (`SND-RECIPE-008`), exactly as at the terminal | Recipe hash at the merge commit |
| Map import | An authored or exported map file under `maps.paths`, in a repo that is private | Import it as `current` (all or nothing, `SND-CONTEXT-031`/`032` as today) | Map file content hash at the merge commit |
| Map promote | No file. Any merge to a `promote.repos` repo's branch | After `context-rebuild` builds the draft **at the merge commit**, promote it when the CI promote rule holds (below) | Draft hash plus the override set hash, with the merge commit recorded |

**The CI promote rule.** Today `map promote` refuses while any proposal is undecided (`SND-CONTEXT-046`), so a
build with new model proposals can never promote without a person. Under CI:
- deterministic proposals are accepted by `service:ci-apply` (one reversible `map_override` row each, actor
  `ci:…`, Undo works as for any accept);
- undecided **model** proposals are **left out** of the promoted version and stay in the queue for a person; they
  never block (§9 O4 asks the owner to confirm);
- everything a person decided stays decided.

So `current` only ever holds deterministic content and what a person accepted, and model text never reaches
`current` without a person. Review decisions themselves stay human (§4).

### 3.4 Revoke, drift and the terminal fallback

- **Revoke stays `protected`** (human, typed). Revoking a recipe that CI approved parks its instances as today. The
  same hash is never re-approved by a later tick (`SND-RECIPE-005`). To stop CI approving that repo at all, the owner
  pauses `ci-apply` (desk) or removes the repo from `ci:` (profile approval).
- **Drift approvals** (same hash, new block pins after a new build): `ci-apply` re-approves a CI-approved recipe on
  drift only when the capability diff is empty and its evals pass; otherwise it parks as today and lands in
  needs-you (§9 O5).
- **The terminal path stays**: a human may still approve, import or promote with the typed challenge. Both paths write
  the same events; the ledger's `via` tells them apart.

### 3.5 When an apply fails

- Ledger event `ci.apply.failed` `{repo, sha, path, verb, code}` (public: ids and codes, no file text), and
  `ci.apply.applied` `{repo, sha, path, verb, hash, pr?, mergedBy?}` on success. Both go through the chain, so the
  app refreshes.
- A **needs-you row** (category `setup`) per failed `(repo, path)`, cleared by the next successful apply of that path.
- **Doctor** `ci:apply` warns on a failed apply, a mirror that hasn't fetched in 24 h, or a cursor behind the branch
  by more than 20 commits.
- **Back on the forge** (optional, `ci.reportStatus: true`, needs the `statuses: write` scope): a commit status
  `sindri/apply` on the merge commit (`success`, or `failure` with the code), so the PR page shows it.

Codes (new family `SND-CI-*`): `001` required check missing or not successful on that commit; `002` commit not
reachable from the branch; `003` merger not in `mergers`; `004` map repo not private; `005` content error (wraps the
verb's own code: `SND-RECIPE-008`, `SND-CONTEXT-031`, …); `006` forge or mirror unavailable (transient).

## 4. Action classes and callers (API §4)

A new class, **`ci`**: an approval that a merged, checked PR stands in for.

| Class \ caller | `human` + terminal | `human` (app) | `session` | `service` |
|---|---|---|---|---|
| `ci` | no (a human uses the `protected` variant) | no | **no** | **only** `service:ci-apply`, in-process |

Commands with a `ci` variant: `ApproveRecipe:ci`, `ImportMap:ci`, `PromoteMap:ci`, and `ReviewMap:accept-deterministic`
when run by `service:ci-apply` inside a promote. The `protected` variants are unchanged.

**What stops a session from using it:**
1. `service` callers never arrive over HTTP (API §4.2); there is no header, token or route that yields
   `service:ci-apply`. A `ci` variant sent over the wire is `SND-AUTH-002`.
2. A session can trigger a tick (`sindri services run ci-apply` is a `job`: a session files a request), but a tick only
   re-reads what merged; it can't supply content.
3. A session can't widen the scope: `ci:` is in the profile, and profile approval is human.
4. What merges is governed by the forge: branch protection and the required checks. Under this design **a merge is
   an approval**, so whoever may merge to that branch may approve. Today the meta merges batches on the owner's go;
   §9 O3 asks whether that is enough for recipes and maps.

## 5. The CI half (GitHub Actions)

A workflow in each CI repo produces the checks `ci-apply` requires. It needs no Sindri server: the checks run the
in-process verbs.

```yaml
# .github/workflows/sindri-ci.yml
name: sindri-ci
on:
  pull_request:
    paths: ["recipes/sindri/**", "maps/**"]
  push:
    branches: [main]
    paths: ["recipes/sindri/**", "maps/**"]
permissions: { contents: read }
jobs:
  recipes:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<pinned sha>
      - uses: actions/setup-dotnet@<pinned sha>
        with: { global-json-file: global.json }
      - run: |
          for f in recipes/sindri/*.yaml; do
            dotnet run --project src/Sindri/Aw.Sindri.Host -- recipe validate "$f" --json
            dotnet run --project src/Sindri/Aw.Sindri.Host -- recipe eval "$f" --json
          done
  maps:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<pinned sha>
      - run: sindri context map validate maps/*.yaml --json   # new local verb (§6), schema-only
```

- The `push` trigger makes the checks run **on the merge commit**, which is what `ci-apply` checks (step 3). A PR's
  head checks alone aren't enough: the merge commit is what gets applied.
- A map repo outside this toolkit installs the `sindri` CLI from a pinned release instead of building it.
- `recipe eval` needs no model egress (the eval engine is pure); `context map validate` reads only the file.

**The forge credential** `ci-apply` uses is read-only: a fine-grained token or App installation with `contents:
read`, `checks: read`, `pull_requests: read` and `metadata: read` on the listed repos only (plus `statuses: write` if
`reportStatus`). It is the profile's `workflow.forge.credential` (or a `ci.credential` override), never in the profile.

## 6. New surface, summarized

| Kind | Name | Notes |
|---|---|---|
| Service | `ci-apply` (built-in) | §3.2 |
| Action class | `ci` | §4 |
| Command variants | `ApproveRecipe:ci`, `ImportMap:ci`, `PromoteMap:ci`, `ReviewMap:accept-deterministic` (ci) | In-process only |
| Events | `ci.apply.applied`, `ci.apply.failed`; `recipe.approved` gains `via: ci`, `repo`, `sha`, `pr?`, `mergedBy?`; `context.map.imported` and `context.map.promoted` gain the same `ci` fields | Public payloads carry ids, hashes, codes only |
| Query | `GET /ci` (each repo's cursor, last fetch, last apply per path, failures); CLI `sindri ci status [--json]` | query, sessions repo-scoped |
| Local verb | `sindri context map validate <file>… [--json]` | Schema check only; no server, no `SND-CONTEXT-005` (it writes nothing) |
| Forge port | `GetCommitChecks(repo, sha)`, `GetCommitPull(repo, sha)`, `GetRepoVisibility(repo)` | Read-only; file adapter and `FakeForge` seed them |
| Profile | `ci:` (§3.1) | New `SindriProfile` section |
| Doctor | `ci:apply` | §3.5 |
| Codes | `SND-CI-001…006` | §3.5 |

## 7. The alternative: a self-hosted runner that pushes

A GitHub Actions job on `push` to `main` runs on a self-hosted runner on the owner's box and calls the loopback API with
a CI token.
- **Identity:** `sindri ci token mint --repo R --verbs recipe-approve,map-import,map-promote --expires 90d` (itself
  `protected`) writes `auth/ci-<repo>.token`; the runner sends it as `X-Sindri-Ci-Token` with
  `X-Sindri-Caller: ci`. Its scope is the minted verbs and repo, and the server checks the job's claimed sha against
  its own fetch.
- **What stops a session:** the token file must be unreadable to the agent's OS user, so the runner needs its own OS
  user; the CLI refuses the `ci` caller when an agent variable is set (honest-mistake guard, as for `human`).
- **Why it isn't recommended:** a runner on the owner's laptop runs any workflow the repo defines, and needs a second
  OS user, registration and upkeep. A box that's off misses `push` events (they must be re-run by hand: a manual
  step). And Sindri still has to re-verify the merge and checks itself, so the pull service's logic is needed anyway.

## 8. What stays human

| Action | Class | Why |
|---|---|---|
| Profile approve (including the `ci:` block) | `protected` | The scope of everything else, CI's included |
| Token grants: `answer-grant`, `answer-grant-tokens`, `answer-accept` | `protected` | Spending and acceptance decisions on a running instance |
| Recipe revoke | `protected` | The owner keeps revoke |
| Map review decisions (accept, reject, rename, move, merge, add, delete, `--apply`) | `desk` / `protected` as today | README X63; except the deterministic accept inside a CI promote (§3.3) |
| Onyx `up`/`down` and source connections | owner (Onyx O3) | Unchanged |

## 9. Open questions for the owner

- **O1, pull or push.** Pull (§3, recommended) or the self-hosted runner (§7)?
- **O2, maps in a repo.** Map import from CI needs the map file in a repo. Is a private repo the owner names for
  maps acceptable for work data, or should CI cover only promote (built from code, no file) and leave import at the
  terminal?
- **O3, who may approve.** Is "merged into the branch with the required checks green" enough, or should a recipe or
  map change also need `mergers` (only listed logins' merges count) or a CODEOWNERS review on those paths? With
  `mergers: [<owner>]`, a batch merged by an agent token wouldn't apply.
- **O4, model proposals.** Does a CI promote leave undecided model proposals out (recommended), or wait for them?
- **O5, drift.** May `ci-apply` re-approve drift when the capability diff is empty and the evals pass?
- **O6, generated recipes.** They're approved by the packet freeze today (W9) and stay that way; confirm.

## 10. Phases (after approval)

| Phase | Lane | What | Depends on |
|---|---|---|---|
| CI-P1 | api | Profile `ci:` section, the `ci` action class and its authorization row, `SND-CI-*`, events, `GET /ci` + `sindri ci status`, the cursor projection | — |
| CI-P2 | api | Forge port reads (`GetCommitChecks`, `GetCommitPull`, `GetRepoVisibility`) in the GitHub and file adapters, `FakeForge`, the read contract | — |
| CI-P3 | api | The `ci-apply` service: mirror, walk, check, bind, report, doctor `ci:apply`, needs-you row | CI-P1, CI-P2 |
| CI-P4 | engine | `ApproveRecipe:ci`, drift re-approval under O5, `via: ci` in approvals and `recipe list` | CI-P1 |
| CI-P5 | context | `ImportMap:ci`, `PromoteMap:ci` with the CI promote rule, the deterministic accept as `ci`, `context map validate`, build at a given commit | CI-P1 |
| CI-P6 | api | `.github/workflows/sindri-ci.yml` for this repo's recipes; the proof: a recipe PR merged in a scratch repo on the file forge is approved with no typed step | CI-P3, CI-P4 |
| CI-P7 | ui | The `ci` fields on approvals and map versions, `GET /ci` on the Health screen | CI-P1 |

**Parallel plan:** CI-P1 and CI-P2 start together (two api builders). Once CI-P1 merges, CI-P4 (engine), CI-P5
(context) and CI-P7 (ui) run in parallel with CI-P3 (api). CI-P6 closes it. Every lane has a unit from the second
wave on.
