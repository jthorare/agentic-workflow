# PR champion loop (shared)

The one copy of what happens after a PR opens: review it to zero blocking findings, mark it ready, watch it
until it is merge-ready, and report. Called by `shipRelease`, `bugFixOrchestrator` and `specToProvenPR`.
A caller passes `--no-champion` to skip all of it. A caller's own steps (proof, re-proof) stay in the caller.

The loop **never merges**. Merging is the human's call (`/landAndDeploy` waits for it).

Inputs: the PR number `<n>` and its head branch. Re-source `SHARED_DIR` and `repo-slug.sh` in every shell call.

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything below.
- `readonly`: run step 1's review and step 3's reads, then report. Make no push, no `gh pr ready`, no thread
  resolve, no PR comment and no reply on a thread. Print each refused write under a `Would run:` heading.

## Who counts as a review bot

Try the repo config first:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
RC="$SHARED_DIR/repo-config.sh"
if [ -x "$RC" ]; then BOTS="$("$RC" get repo reviewBots)"; rc=$?; else rc=3; fi
```

The reader arrives with #2909. Exit 0 prints the JSON list of review bot logins; read `skipReviewLoop` the
same way (`get repo skipReviewLoop`, a list of PR kinds). Exit 3 or 4, or no reader, or no `repo.json`: use the
fallback. Any login that ends in `[bot]` is a review bot, and every PR kind runs step 1.

## Steps

1. **Review loop (cap 5).** Skip it when the PR's kind is in `skipReviewLoop`. Otherwise **Invoke skill `review`**,
   then **Invoke skill `addressReview`** on the blocking findings, and repeat until a `/review` run returns zero
   blocking findings. Stop at 5 runs. At the cap with only suggestions left: fix the new ones, answer the
   repeats in writing on their threads. At the cap with a blocking finding still open: stop, report the
   findings that keep coming back, and ask the user. A caller that has stricter review rules (for example
   specToProvenPR's re-proof after review fixes) applies them around this step.
2. **Mark ready.** `gh pr ready <n>`. Skip when the PR is already ready.
3. **Watch.** When `AW_RESULT_SKILL` is set (a Sindri Step), skip steps 3 and 4: run steps 1-2, then report `merge-ready` only if the checks you can read are green, and write "watch skipped: running as a Sindri Step" in the report, because a Step must not wait for hours and Sindri's workflow owns the watching. Otherwise one pass reads three things:
   ```bash
   gh pr view <n> --json headRefOid,mergeable,mergeStateStatus,reviewDecision,state
   gh pr checks <n>
   gh api graphql -f query='query($o:String!,$r:String!,$n:Int!){repository(owner:$o,name:$r){pullRequest(number:$n){reviewThreads(first:100){nodes{id isResolved isOutdated path line comments(first:20){nodes{author{login} body}}}}}}}' -f o=<owner> -f r=<repo> -F n=<n>
   ```
   Then act on what the pass shows:
   - **Merged or closed** (`state`): stop and report.
   - **An unresolved thread** from a review bot or a human: fix it or answer it with the technical reason, push,
     reply on the thread, then resolve it (`resolveReviewThread` mutation with the thread `id`). Reviewers
     are right more often than not; a "no" needs a reason a reader can check.
   - **A red check:** `gh run view <run-id> --log-failed`, find the cause, fix the cause (never skip or loosen
     the check), push.
   - **A conflict with the base** (`mergeable` is `CONFLICTING`): merge the base branch into the PR branch
     (`git fetch origin <base>`, then `git merge origin/<base>`), re-run the tests the merge touched, push. No rebase and no force push.
   - After any push, the next pass re-reads everything: a push restarts the checks.
4. **Wait, then repeat.** Use the **Wait, then run again** capability (`capabilities.md`): every 5 minutes
   (`AW_CHAMPION_INTERVAL` seconds, default 300), for at most 8 hours from the first pass. At the 8 hour mark
   stop with a stop report: what is still open, and the last head sha seen.
5. **Merge-ready report.** When one pass shows all of: every check green, no unresolved thread, no
   conflict, `state` open. Read the sha from git, not from memory:
   ```bash
   git -C "$WT" rev-parse HEAD
   ```
   Report `merge-ready at <sha>` with the PR link and the checks it was verified against, and **stop for the
   human merge**. Never run `gh pr merge`, `gh pr merge --auto` or the merge mutation. If a push lands after
   the report, the report is stale: run another pass first.

## Stop conditions

| Condition | Result |
|-----------|--------|
| merge-ready (step 5) | report with sha |
| PR merged or closed by someone else | report that, with the final state |
| review cap hit with a blocking finding | report and ask |
| 8 hours elapsed | stop report |
| owner says stop | stop report |
| `readonly` | review result and the list of writes not made |
