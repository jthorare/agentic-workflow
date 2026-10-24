# The PR babysitter: Steps, effects and the merge record

Phase **PB-B** (with PB-E's effects) of the PR babysitter and docs sync (`docs/superpowers/specs/2026-10-15-sindri-pr-babysitter-docs-sync-design.md`, §4, §10.1, BD7). This page is what the C# blocks on this branch do; the spec says why. The recipe is `recipes/sindri/workflow.pr-babysitter.yaml`; a block's descriptor is `PrSteps` (`Features/Blocks/Step`) or `PrEffects` (`Features/Blocks/Effect`), and a descriptor change moves the pinned recipe hashes.

Every block reads its inputs as stored facts through the block runtime and talks to the forge only through `IPullRequests` (`Core/Forge`); a forge host outside the profile's list is refused before anything is sent.

## `sindri pr babysit <url>` and `sindri docs sync <url>` (API16)

```
sindri pr babysit <url> [--item <id>] [--json]
sindri docs sync <url> [--item <id>] [--json]
```

The hand start (spec §4.2) in one command. Both go through the running control plane like `workflow start` (same caller class, same `POST /sindri/workflow/instances`; fact contents travel in the request, there are no files to write) and end the same way: the instance's status header, or with `--json` the raw answer. Exit 0 started or found (the same item and attempt answer with the instance they already have), 1 the control plane is not running, 2 invalid input, refused or error.

| Verb | Starts | Facts sent |
|------|--------|------------|
| `pr babysit` | `workflow.pr-babysitter` | `fact:pr` = the link as a JSON string; `fact:handoff`, `fact:phasePacket`, `fact:lineContext`, `fact:workspace` = `{}` |
| `docs sync` | `workflow.docs-sync` | `fact:pr` = the link as a JSON string; `fact:phasePacket` = `{}` |

**The link** is checked before anything is called, for its shape only: https, no credentials or port, `owner/repo/pull/number` (further segments such as `/files` are dropped, and the host is lower-cased). A bad link is exit 2 `SND-CLI-003` with a fix line. Whether the host and repo are ones the approved profile names is the intake Step's to refuse, before any network call (spec §10.2).

**The item** is `--item <id>` when given. Otherwise the verb lists the open items and uses the one whose title is exactly the canonical link, and creates an item titled with the link when none is (the control plane may be started for that, as for `item add`). While the item list is rebuilding the verb refuses rather than risk a second item (fix: pass `--item`). To start another attempt on an item that has ended, use `workflow start --attempt next`.

## `sindri docs check --facts <dir> [--json]` (API17)

A local verb: it runs in this process and needs no running control plane. It runs the rules `step.docs.check` runs, through one shared service (`DocsCheckService`), over a docs worktree.

`<dir>` holds the Step's facts, as the docs agent finds them in `$AW_SINDRI_RUN_DIR`:

| File | Holds |
|------|-------|
| `workspace.json` | `fact:workspace`: the docs worktree's path, one JSON string |
| `docsScope.json` | `fact:docsScope`: `{ "base": "<commit>", "paths": ["docs/**", "*.md"], "candidates": [{ "path": "...", "designNamed": true }] }` |

| Rule | Fails when |
|------|------------|
| `DOCS-PATH-001` | a changed file is neither under the profile's doc globs nor a doc the design names |
| `DOCS-LINK-001` | a relative link in a changed `.md` file does not resolve inside the worktree |
| `DOCS-MD-001` | a changed `.md` file opens a code fence it never closes |
| `DOCS-CHANGE-001` | the worktree holds no change since `base` |

Output: one line per finding with its fix, then the verdict; `--json` prints `{ "findings": [{ rule, item, message, fix }], "passed": bool }`.

Exit codes: 0 no findings, 1 findings, 2 usage or a tool error (a missing directory or fact file, a worktree git cannot read).

The docs agent runs it on its own work. `step.docs.update` declares `fact:workspace` and `fact:docsScope` among its input facts, so the launch writes `workspace.json` and `docsScope.json` into the Step's run directory, and `DocsUpdateInstructions` tells the agent to run `sindri docs check --facts "$AW_SINDRI_RUN_DIR" --json`, fix every finding and run it again before it reports `changed`. `step.docs.check` stays the gate after the Step.

## Steps (`Features/Pr`)

| Block | Outcomes | Does |
|---|---|---|
| `step.pr.intake` | `seeded` | Reads the pull request once, outputs `fact:workspace` (empty when none given) and seeds the empty facts every later Step reads (`decision`, `ownerNote`, `handoffNote`, `fix`, `reply`, `recheck`). Appends the `start` row. An unreadable PR fails the Step |
| `step.pr.snapshot` | `mergedDocsOpen`, `merged`, `closed`, `restack`, `conflict`, `ciRed`, `comments`, `stale`, `docsRed`, `pending`, `green` | One forge read of the PR, its threads, its comment marks and (when the instance holds `fact:docsPr`) the docs PR; the first outcome that holds in §4.3's order. Writes `fact:prState` on every outcome and `fact:mergedSha` when merged (the merge commit `fact:merge` holds, else the head). Appends the `snapshot` row |

`fact:prState` is the PR view in camelCase: `link`, `state` (`green` when the outcome is, else the PR's own), `pullState`, `headSha`, `stale`, `head`, `mergeable`, `reviewDecision`, `unresolvedThreads`, `checks` (`state`, `sha`, `failing`), `queue` (`unhandled`, `held`, `untrusted`, `bots`), `comments` (each with its latest mark), `base`, `docs`, `mergeBlockedBy`, `workspaceAhead`, `outcome` and `at`. `base` is `{ ref, sha, tip, was }`: `ref` and `tip` are the base branch and its head as the forge reports them now (`PullView.Base`, `PullView.BaseSha`); `sha` is the tip of the base the PR was built on, which `effect.pr.restack` replays after. It follows `tip` on the first snapshot and is then kept: a base that moved on keeps the old tip, and a base that merged and was deleted (the forge moves the PR to the default branch, whose tip says nothing about the old base) keeps its last tip with `was` naming the old branch. A restack that settled at this head (`fact:restack` `restacked` or `nothing`, its `head` the PR's head) sets `sha` to `tip` and clears `was`. A PR first seen after its base merged has no earlier tip to keep. A comment with a mark is handled; a thread carrying a Sindri marker is Sindri's own and never queued.

## Effects (`Features/Blocks/Effect`)

| Block | Replies | Does |
|---|---|---|
| `effect.pr.push` | `pushed`, `pushedSource`, `nothing`, `headMoved`, `rejected` | Pushes the workspace head to the PR head with a lease on the head `fact:prState` saw; never forces and never rebases. Carries `fact:push` (`sha`, `commits`, `comments`, `source`, `cause`, `ciFixes`; `ciFixes` counts a `ci` push once, what `gate.pr.autofix-cap` reads). A dispatch repeated after its push landed is `pushed` with `Existed`. Appends the `push` row on `pushed` and `pushedSource` |
| `effect.pr.record` | none | Appends a `recheck` or `docs` row from the fact the child returned (`kind`, `result`), a `docsMerge` row for the owner desk's `done` and `leave` (`kind: docsMerge`, `outcome: doneByHand|left`, `trigger`), or an `end` row on a transition into a terminal (`kind: end`, `terminal`, and `notes` on `rework`) |
| `effect.pr.rerun-checks` | none | Asks the forge to run the failed checks again on the commit they failed on (`checks.sha`, else `head.sha`). Writes no row |
| `effect.pr.merge` | `merged`, `blocked`, `headMoved` | Merges with the forge's expected-head check; the method is the recipe's `method`, else the profile's `workflow.pr.mergeMethod`, else squash. Human caller only (W23). Appends the `merge` row on `merged` |
| `effect.pr.reply` | none | Forms `thread`, `reaction`, `fixed`, `fixedReplies`, `hold` and `none`. A post is made once per effect identity (Sindri's marker is derived from it); every form writes its comment mark (W26) keyed by the identity, so a redelivery writes none twice; reply text is scrubbed first. Appends a `comment` row |
| `effect.pr.restack` | `restacked`, `nothing`, `conflict`, `refused` | Moves a stacked PR onto its new base. The new base is the PR's own base branch while the PR that owns it is still open (the base moved), else the default branch (it merged). Arguments: `pr`, and optionally `workspace` (else `fact:workspace`) and `from` (the old base's tip, else `fact:prState` `base.sha`). The workspace must be at the PR's head (else `refused`). Fetches the new base from the workspace's `origin` (`IWorkspaceRebase`), then rebases: with `from`, only the commits after it are replayed, so the old base's commits are dropped even after a squash merge; without it git drops by patch. Scans the rebased range in process (`SHIP-SECRET-001`, `RebasedRangeScan`; a hit names file and line, never the match), retargets the PR, and pushes with a lease on the head the forge saw. `nothing` when the PR is closed or already on its base and has it; a head that has the new base but sits on another base is only retargeted (`restacked`, no push). `conflict` (git could not merge; the paths are in the fact, the rebase is aborted) and `refused` (scan hit, lost lease or remote refusal) push and retarget nothing and put the workspace back on the head it had. Carries `fact:restack` (`outcome`, `oldBase`, `newBase`, `head`, `reason`, `paths`) and appends a `restack` row for every outcome |
| `effect.pr.create-stacked` | `opened`, `updated`, `rejected` | Pushes the docs workspace's head to `docs/<source head branch>` and opens the docs PR with that branch as its base, or updates Sindri's open one. Arguments: `pr` (the source: `link` and `head.ref`), `on` (the snapshot; `head.sha` is the head the docs describe), `workspace`, `title`. The docs PR is found by its branch and a marker derived from the source (`sindri.docs:<owner>-<name>-<number>`), never by the branch alone. The push is leased: no lease for a new branch (a branch that already exists with no docs PR of ours is `rejected`), else the sha in `fact:docsPr` (the one it last pushed), else the open docs PR's head; a branch someone else moved, a push the remote turns away and a call the forge refuses (`SND-FORGE-002`) are `rejected`. A repeated dispatch after its push landed finds the branch at its own commit and goes on. The docs workspace's `origin` must be the source's repository. Carries `fact:docsPr` (`link`, `number`, `state`, `base`, `head`, `source`, `syncedHead`). Writes no row |
| `effect.pr.merge-docs` | `merged`, `pending`, `none` | After the source merged, finds the docs PR (same branch and marker) and merges it: `none` when there is none or it is closed or merged already; `pending` (left open) when its checks fail or still run, when it conflicts, or when the forge blocks the merge or the head moved; otherwise it moves the base to the default branch if it is elsewhere and merges with the expected-head check, by the method `effect.pr.merge` uses. One look, nothing waits: `workflow.pr.docsMergeWait` is not read and `docs-merge-owner` retries. No rebase and no secret scan (no workspace). Human caller only (W23). Carries `fact:docsMerge` (`docsPr`, `outcome`, `sha`) and appends a `docsMerge` row for every outcome |

## The babysitter's commands in the status (API18)

`sindri workflow status` and `GET /sindri/workflow/instances/{id}` list the babysitter's recipe verbs for its current state as `allowedCommands` entries with `actionId` `ApplyCommand:command` and a `verb`. In `ready` they are `merge`, `rework`, `comments`, `resync-docs` and `check`, so the app shows one button per verb and a terminal user runs `sindri workflow command <address> <verb>`. `merge` and `rework` are human-trigger verbs: they are listed, and a session's call is refused (`SND-AUTH-002`). Details in `docs/csharp/sindri-workflow.md`.

## The merge record (BD7)

`babysit.jsonl` is in the instance's run area (`IRunArea`). It is appended and never edited. One writer, `MergeRecord` (`Features/Blocks/Effect`, internal), implements the port `IMergeRecord` (`Core/Workflow/Blocks`): `Append(MergeRow)` returns true when it wrote the row, false when a row with that identity was already there, and an error for an unknown kind or an unwritable run area. A Step or an effect takes `IMergeRecord`, never the class, so slices share it without crossing (AW1003).

Every row carries `kind`, `identity`, `instance`, `headSha` and `at`; the kind adds its fields, and a field cannot overwrite one of those columns. The identity is what keeps a retry from writing twice:

| Writer | Identity |
|---|---|
| an effect | `EffectContext.Identity`, `<instance>:<transition sequence>:<index>` |
| `step.pr.intake` | `<address>:start` |
| `step.pr.snapshot` | `<address>:snapshot:<previous prState.at, or first>`, so each successive snapshot has its own and a retry has the same |
| `effect.pr.reply` `fixedReplies` | `<identity>:<comment id>`, one per comment answered |

| Row `kind` | Written by | Fields besides the fixed ones |
|---|---|---|
| `start` | `step.pr.intake` | `pr`, `base` (always null for now: `PullView.Base` exists since #674, but the row does not read it yet), `handoffHash` (`sha256:` of `fact:handoff`, null without one), `phase` (`fact:phasePacket` id), `startedBy` (`line` when `fact:lineContext` is non-empty, else `hand`) |
| `snapshot` | `step.pr.snapshot`, when the outcome or the head differs from the previous `fact:prState` | `outcome`, `checks`, `queue` |
| `push` | `effect.pr.push` | `cause`, `commits`, `comments` (the ids of the comments whose latest mark is `fixed` with a commit the push carried), `source` |
| `comment` | `effect.pr.reply` | `comment`, `type` (the `decision` argument's `type`), `outcome` (`replied`, `reacted`, `fixed`, `held`, `skipped`), `commit` |
| `recheck`, `docs` | `effect.pr.record` | `result` |
| `end` | `effect.pr.record` | `terminal` (`merged`, `closed`, `stopped`, `rework`), `reworkNotesHash` (`sha256:` of `fact:reworkNotes`' content, on `rework` only) |
| `restack` | `effect.pr.restack` | `oldBase`, `newBase`, `outcome`. `headSha` is the pushed head (the seen head when nothing was pushed) |
| `merge` | `effect.pr.merge` | `method`, `sha`, `actor` (the optional `actor` argument; null without one) |
| `docsMerge` | `effect.pr.merge-docs` | `docsPr` (null when there is none), `outcome` (`merged`, `pending`, `none`; `doneByHand`, `left` from the desk), `trigger` (the babysitter passes `command.merge` after the click and `ownerDesk` from the owner's desk step) and `actor` (an optional argument; null, since the recipe language has no reference to the caller). `headSha` is the docs PR's head. The owner's desk step writes the `doneByHand` and `left` rows through `effect.pr.record` (`kind: docsMerge`, `outcome`, `trigger: ownerDesk`; `docsPr` from `fact:docsMerge` when held, `headSha` null, since the desk holds no docs PR head, `actor` null) |

A step with no instance address still runs and writes nothing.

## `service:pr-watch` (API15)

The producer of `pr.changed` (spec §6.1, §6.2). It is a built-in scheduled service (`pr-watch`, listed by `GET /sindri/services` and `sindri services`), on the scheduler's shortest cadence; **it polls only every `workflow.pr.pollInterval`** (default `60s`, read from the approved profile each run) and is `Quiet` in between.

A poll:

1. Lists the pull requests of the live instances that wait on `pr.changed` (`TemporalWatchedPulls`): open instances whose current state's wait, in the approved recipe, is for `pr.changed`, with their `fact:pr` (a JSON string, or an object with `url` or `link`) read from the fact store.
2. Reads their digests through `IPullRequests.Digest`, one forge call per host per at most 20 links. A pull request the forge no longer has fails a batch, so then each is read alone and the missing one is left out.
3. Compares each with the last digest in `<home>/workflow/pr-watch.json` (owner-only, written atomically). A pull request seen for the first time is only recorded. A change records one `pr.changed` per item that waits on it (the event's route is `item`), by `service:pr-watch`: the **outcome is the first of** `merged`, `closed`, `updated` (head moved), `review` (comment count or the forge's version moved) and `ci` (checks changed), and `fact:prEvent` is `{ subject, headSha, changes[], at }`, with `subject` the instance's `fact:pr` exactly and `changes[]` every change in that order.
4. Records nothing for an unchanged pull request, except one `idle` per `workflow.pr.remindAfter` (default `14d`) of quiet. The same change found again after a failed save is the same ledger row (idempotency key).
5. Forgets the pull requests nobody waits on any more, and rewrites the file.

A run that recorded nothing is `ScheduledResult.Quiet`.

**Errors.** The forge port maps a refused credential or host to `SND-FORGE-002` (after it re-ran a `command:` credential once on a 401), no forge in the profile to `SND-FORGE-001`, and an unreachable or rate-limiting forge to `SND-FORGE-004`. Waiting instances are left as they are (their own timeouts still run); the service's last run shows the code in `GET /sindri/services`.

| Forge answer | The watcher |
|---|---|
| `SND-FORGE-004` (unreachable, 403, 429) | Backs that host off by itself: one poll interval, then doubling to 15 minutes; the other hosts are polled on. After three unanswered polls in a row the run fails with `SND-FORGE-004`. The forge's `Retry-After` is not visible through the port, so the doubling is the only wait |
| `SND-FORGE-002` | The run fails at once with it (degraded), and the host is backed off |
| `SND-FORGE-001` | The run fails at once with it |

The spec names the 401 case `SND-FORGE-001` and the unreachable case `SND-FORGE-002`; the codes the port gives them are 002 and 004, and the watcher passes the port's code on.

`sindri doctor` has a `pr-watch` check (`PrWatchDoctorChecks`): ok before the first run or after an ok run; after a failed run it names the code and its fix (`001` warn, `002` fail, `004` warn).

## Babysitter stats (API19)

`GET /sindri/workflow/recipes/workflow.pr-babysitter/stats` answers the usual `RecipeStats` plus a `babysitter` section for the window, which PB-P2 reads to tune the caps and Q5, Q9 and Q12. The section is counted on **every read and never cached**: the merge records are files the effects append to, so no ledger head says when they changed. It is absent for every other recipe and for a child-only answer.

| Field | Meaning |
|---|---|
| `pulls` | babysitter instances started in the window |
| `sinceStartToReadySeconds`, `sinceStartToMergeSeconds` | `{ median, p90 }` of seconds **since the babysitter started** to the first `ready` and to `merged`, over the instances that got there. A pull request opened by hand is started when it is opened; one a line opened is started a little after, so these are not "since the PR was opened" |
| `tokensPerPull` | `{ median, p90 }` of the tokens an instance spent (the ledger's running total) |
| `comments` | `{ total, held, withoutOwner, byType[] }` from the merge records' `comment` rows: `held` is outcome `held`, `withoutOwner` every other outcome; `byType[]` is `{ type, fixed, replied, reacted, held, skipped }` by judge type (`none` for a row with no type) |
| `ciFix` | `{ attempts, succeeded, stillRed, rate }`: an attempt is an entry into `ci-fix`; the next snapshot decides it (back to `ci-fix` is still red, anything else succeeded, none yet is open); `rate` is succeeded over succeeded plus still red |
| `rechecks` | `{ runs, resolved, unresolved }` from the `recheck` state: back to `snapshot` is resolved, to `recheck-owner` unresolved |
| `ownerActionsAtReady` | `{ merge, rework, comments, resyncDocs, check }`: the transitions out of `ready`, by where each verb leads |
| `docs` | `{ withMerge, ownerDesk, byOutcome[] }`: entries into `docs-merging` and `docs-merge-owner`, and the `docsMerge` rows by outcome |
| `recordErrors` | merge-record lines that were not JSON with a `kind`, or a record that could not be read; they are left out of the figures and the call does not fail. A missing file is an empty record |

The times, tokens, owner actions, CI fixes, rechecks and docs counts come from the ledger's `workflow.transition` rows; only the comment figures and the docs outcomes need `babysit.jsonl` (read back by `IMergeRecordReader`). The state and command names the counter keys on are the constants of `BabysitterNames`; `BabysitterNamesTests` loads `recipes/sindri/workflow.pr-babysitter.yaml` and fails when one is renamed, so a rename cannot silently zero a figure.

## Not yet

| Gap | Why |
|---|---|
| Reading `PullView.Base` | `PullView` carries `Base` since #674, but the rows don't read it yet, so `start.base` is null, `step.pr.snapshot` never reports `restack` and `head.ref`, `base.defaultRef` and `base.onto` are not in `fact:prState` (`base.ref`, `base.sha`, `base.tip` and `base.was` are) |
| `merge-docs` rebase | `effect.pr.merge-docs` has no workspace, so it neither rebases the docs PR nor scans it before the merge (a conflict shows as `pending`) |
| Hand-off hash check, workspace cut | Intake records the hash but does not check it, and does not cut a workspace from the PR head: no port reaches either |
| `merge.actor` | The engine's effect context carries no actor, so it stays null |
