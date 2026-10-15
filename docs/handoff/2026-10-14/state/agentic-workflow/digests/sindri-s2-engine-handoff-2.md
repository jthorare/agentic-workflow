# Handoff 2: Sindri S2 (workflow engine), lane C. The draft PR is open and review run 1 is done.

Read first: `~/.agentic-workflow/digests/sindri-lane-brief.md` (rules, heavy wrapper, the 5-run review cap, tool
quirks), then this file. Spec §17.28. Contract doc: `docs/csharp/sindri-engine.md`.

**Shared scratchpad warning.** Every lane shares one scratchpad. Keep all your files under
`<scratchpad>/s2-laneC/` (scratchpad = `/private/tmp/claude-501/-Users-thor-repos-agentic-workflow/96adf7a5-06c5-4228-a3d6-0b807b455e57/scratchpad`).
Never touch `scratchpad/hand*`; lane B owns them.

## State

- Worktree `~/repos/agentic-workflow/.claude/worktrees/sindri-s2-engine`, branch `feat/sindri-s2-engine`,
  pushed at `31c0628`. **Draft PR #167** has base `feat/sindri-s1-app-shell`; the orchestrator has its number. The PR body
  carries the block registry contract and the process-deviation note, and states that `LedgerEntry.Item` is now
  `ItemId?`.
- S1 is merged in at `0c3a2f1`. S1 has since moved to `b6e31a28`, so merge `origin/feat/sindri-s1-app-shell` again (a
  merge commit) before the next gate.
- The proof is complete for the code at `31c0628`:
  - `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-s2-engine/{verification-plan,manual-test,evidence}.md`
  - `hand/` holds run 2, the valid one, in `s2-laneC/home2`. `hand-run1-shared-dir/` holds run 1 (void).
  - The journeys are scripted in `s2-laneC/journeys.sh`: copy the published binary to `s2-laneC/bin/` (a fresh copy,
    never overwrite in place: macOS kills it with exit 137) and pick a fresh home name. `s2-laneC/approve.py` approves
    the profile through a pty.
- Local gate at `59b7bc2` (the code is the same as `31c0628` apart from docs):
  - build: 0 warnings
  - unit: 1757/1757
  - integration: 406/406
  - format: clean
  - packages: ok
  - lane budgets: ok
  - AOT smoke: ok
  - deps, env, rules, sync-rules: ok
  - setup dry-run: ok
- Stage-file log: `## S2 log` in `plans/sindri-app/stages.md`. Fill in "ready" and the line counts when you finish.

## Review run 1 (of at most 5): 26 findings

The state file `~/.agentic-workflow/jthorare-agentic-workflow/reviews/167.json` is written but not posted. Run
`/postReview 167`, then fix the items below. The raw reviewer JSON is in `s2-laneC/rev-*.json`.

### Blocking: fix all of these, each red-first

1. **The child instance id collides across parent attempts** (data-integrity-guardian-0 and code-reviewer-0). Attempt 2
   of `workflow.item` hits SND-WF-003 when it enters `running`.
   - Fix: derive the child's id and start key from the parent: id = hash(tenant, childRecipe, parentId,
     parentVersion), key `wf:<parentId>:child@v<n>:start` or similar (`FollowUps.cs:28-30`, `WorkflowKeys`).
   - Edit migration 0004 in place (it hasn't shipped): replace `UNIQUE (tenant, recipe, item, attempt)` with a unique
     expression index that includes `COALESCE(parent,'')`.
   - Test: cancel attempt 1 while it's `running`, start attempt 2, and drive it to `running`.
2. **A changed or invalid recipe strands the instance** (architecture-strategist-0 and code-reviewer-1). Cancel, park
   and takeover all fail with SND-WF-008, though its own message says "cancel it". The ticker also re-reads such an
   instance forever and blocks the 200-row `Due` batch.
   - Fix: cancel, park, takeover, handback and evidence apply without the current recipe (use `store.Get` and the
     command decision; the cancel cascade the same way). On a tick, escalate a stranded instance with the reason "recipe
     changed" so it leaves `Due`. Keeping the started recipe by hash is the bigger alternative.
   - Test: change the catalog's hash, then cancel.
3. **The doc says one correlation per tree, taken from the root, but the code uses the parent's**
   (architecture-strategist-1). The cancel cascade is also one level deep (code-reviewer-5).
   - Fix: either add `Root` to `WorkflowInstance` (set at start to `parent?.Root ?? id`, used in
     `CommitBuilder.Record`) and make the cancel cascade recursive within `ParentPropagation.MaxDepth`, or limit nesting
     to one level and correct the doc.
   - Add a depth-2 test.

### Issues: fix, or resolve in writing only with a strong reason

4. **Gate ladder** (architecture-strategist-2 and code-reviewer-6): check evidence before any gate, evaluate gates in
   rung order, stop at the first failure, and escalate only when the top rung fails after every lower one passed
   (`GateEvaluator`, `SignalApplier.First`, `GateOutcome`).
5. **The recipe hash covers nulls and display text** (data-integrity-guardian-1): build the canonical JSON with
   `WhenWritingNull` and exclude `Description` and `Next`. Add a golden that pins the built-ins' hashes. The AOT
   `expected.txt` recipe hash abbreviations will change.
6. **The effect kind isn't checked at run time** (architecture-strategist-4): `BlockRegistry` refuses a non-allowlisted
   `CommandKind`; `CommitBuilder.Effects` refuses an emitted kind that differs from the spec. Add a test with a block
   that lies about its kind.
7. **Retry detection matches a key suffix** (code-reviewer-2): a timer key `timeout:claimed@v4:-` passes for a retry
   of signal `claimed`. In `SignalApplier` (around lines 89-91), require a `>` in the segment before the suffix, or use
   an unambiguous marker. Test: the timer fires in `claimed`, then `signal claimed --expect-version` with the old
   version returns SND-WF-003.
8. **Park, takeover, unpark, handback loses the timer** (code-reviewer-3): unpark while taken over sets `Paused` to the
   state's full timeout (`CommandDecisions` 45-67, and `ParentDecisions` unpark too).
9. **`child.done` is lost when the parent isn't running, and an escalation on it is swallowed** (code-reviewer-4,
   `ParentDecisions` 24-27 and 79-81). On handback or unpark of a parent in a child wait state whose child is done,
   apply `child.done`; apply a returned escalation as the parent change.
10. **The idempotency claim in the doc is false** (code-reviewer-7). Reword: only signals sent with `expectedVersion`
    are no-ops on retry. Make evidence registration idempotent on (kind, sha, digest), returning AlreadyRecorded.
11. **A bad `--base` sha is dropped silently** (code-reviewer-8): return a usage error, as `--sha` does. Nit: refuse
    `--attempt 0` as input.
12. **`GateContext` is thin** (architecture-strategist-3, issue): add `RecipeId Recipe`, `int Attempt`, `string To`
    and `string On` while the API is unshipped. Answer the rest in writing: prior verdicts, the evidence locator and an
    evidence-provider contract are lane E's extension. Lane E is adding `IEvidenceProvider : IBlock` on its own branch.
    **Optional request from lane E:** add `IBlockRegistry.Provider(BlockId)`, mirroring Gate and Effect, if it's cheap
    (it needs lane E's interface; otherwise leave it).

### Suggestions and nits: reply in writing, folding in a fix only where it's trivial

- data-integrity-guardian-2: the tenant in keys
- data-integrity-guardian-3: fake/SQLite constraint differences
- data-integrity-guardian-4: `Due` head-of-line blocking (covered by the fix for item 2)
- data-integrity-guardian-5: reuse `LedgerSql.EventColumns`
- architecture-strategist-5: inputs absent at run time
- architecture-strategist-6: catalog caching
- architecture-strategist-7: public surface
- architecture-strategist-8: rebase or merge S1
- code-reviewer-9: spend on an escalation
- code-reviewer-10: `TickPace` and stalls

## After the fixes

1. Run the full gate (AGENTS.md; the commands are in this session's history).
2. Republish (the AOT smoke) and run `journeys.sh` again in a fresh home. The behavior changed (gate order, child ids,
   cancel), so update `evidence.md` and `manual-test.md`.
3. Push, then run `/review` again: run 2 of at most 5. Stop at the first run with 0 blocking and 0 issue. Resolve
   suggestions in writing in the state file and the PR.
4. `gh pr ready 167`, fill in the stage-file log, and send the final report to the orchestrator.

Never merge.
