# Sindri engine P4 integration: handoff 4 (2026-10-14)

Continues `sindri-wf-p4-integration-3.md`. PR #192 (draft). Worktree
`~/repos/agentic-workflow/.claude/worktrees/sindri-wf-p4`, branch `feat/sindri-wf-p4-interpreter`, head
**`0c4f07c2`**, pushed. Brief: `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-engine/unit-brief.md`.
Scratch: `~/.agentic-workflow/jthorare-agentic-workflow/scratch/p4-int4/` (logs below; `env.sh` sets the proof env and `pv`).
Push: `git push origin HEAD:refs/heads/feat/sindri-wf-p4-interpreter`.

## Done
- `7e922110` merge origin/feat/sindri-api-p2-projector 8744d4eb (13 conflicts, union): EventCatalog ours (theirs
  duplicated the workflow rows); Core registration = registry planners + P2's IProjectorFeed/ServicesProjection/
  ProfileProjection (dropped a duplicate TemporalRuntime); Features queries ours; StreamEventsService keeps P4's filtered
  paging on P2's `ILedgerReader`; CommandTable/Usage keep `views rebuild` + `doctor --probe`; Verbs usings both;
  EndpointTable ours; SchedulerHost per-service loops + built-ins + P2's service.ran (doc merged);
  SchedulerHostTests both (restored a lost [Fact]); InMemoryViewRows theirs; PublicAPI union minus 41 stale RS0017 lines.
  The 7 API-P2 stub reds are green now (unit lane: only the ARCH-ID-002 failure, since fixed).
- `11e804ab` merge origin/feat/sindri-wf-p2-registry 5ea417cd (smoke heartbeat `(last TIME)` fix + normalize.sh test):
  conflicts take P4's side. (Its message says "AddInstanceCommand's ui stays ready": it is the
  `/workflow/instances/{instanceId}/nonce` row, IssueNonce.) origin/main unchanged (d4de828b).
- Coordinator's 3 items: `4858b214` InstanceView.Id is `required WorkflowInstanceId` (set in InstanceViews.Of,
  RecipeWorkflow.GetInstance, test helper), IDE0370 `!` dropped in InstanceIdRoutesTests; `b1f4009b`
  ProtectedCommandTests waiver for ImportContextMap: owner "context lane (S-3a, §8.7)", lapses 2026-10-27, new test
  `Every_waiver_is_still_open` (X30 says protected with the shared challenge; `ImportContextMapRequest` still has
  `Confirmed` bool — tell the coordinator); `0c4f07c2` expected.txt doctor order = RunDoctor's (context, then probe warns).
- Gate so far (logs in scratch): TS typecheck 5/5 ok, TS tests ok except skills/ui-evidence browser tests timed out under
  load, rerun green (`ts-skills-ui-evidence-test-rerun.log`); bash-tests 62 run, 1 failed
  `config/lib/tests/aw-state-dir-isolation.test.sh` = other live sessions writing ~/.agentic-workflow (digests,
  judge wal) during the run, env not code; sync-rules, check-deps, check-env-example, setup dry-run, check-rules, no-any
  (after `npm ci --prefix .github/ci-tools`), v8-ignore 0, .editorconfig ratchet +0, aw-live validate/test: all ok.
  dotnet at 0c4f07c2 (`run8.txt`): build 0, format 0, unit 0; integration + verify-packages were still running
  (`test-integration.log`, `verify-packages.log`). Earlier integration run had 7 load flakes; rerun of those classes
  51/51 green (`rerun1.log`). Still to run: `node scripts/ci/lane-budgets.mjs` (needs Release build) and
  `bash src/Sindri/aot-smoke/run.sh`.
- Journeys 1–11 ALL RUN, raw in `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p4/raw/` (setup, j1–j11,
  acceptance, serve.log, serve2.log, stop). Host snapshot built at 0c4f07c2 (`scratch/p4-int4/host2`). Home
  `$TMPDIR/sindri-dev-p4` fresh; profile = recipes/dev/profile.yaml with repo path → `$TMPDIR/sindri-dev-repo-p4` and
  deskTimeout 30m (1m would park desk tasks mid-journey). Items: SelfItems stub accepts `T-1`-style keys (used T-1..T-3).
  Results: J1 PASS (layout, `--run 1.1`, inputs sha256, `parks in 3h 59m (desk)`); J2 PASS (`-> checks`, nonce
  38227a07…, alreadyApplied false, id WFI-80c7…); J3 PASS (`-> verify`, `-> done`, `done: resolved`); J4 PASS (4
  events = 4 ledger rows keys :2/:4/:6/:8 = Temporal 1 start + 3 Updates, no dups; mirror activities are markers);
  J5 PASS (same nonce → `alreadyApplied:true`, 0 new rows; verbatim fresh-nonce rerun after end → SND-WF-001 exit 2,
  0 rows); J6 PASS (SND-WF-009, ledger 4→4, Temporal history 53→53); J7 PASS (kill -9 serve, temporal survived and was
  re-adopted, same RunId 01a13b15-1f3d…, HistoryLength 14 both, run 1.1 completes, epoch 1→2, T-2 rows no dups);
  J8 PASS (agent SND-AUTH-002 for both; no workflow rows; refusals audited as `command.handled outcome refused`;
  agent's events --json identical to owner's); J9 PASS (SND-WF-008 undeclared outcome and missing fact:findings, rows
  6→6, still run 2.1); J10 PASS (workflow.item T-3 waits `triaged`; item.routed inserted by sqlite as `human:desk`
  ignored; as `router.stub` notStartable → `dropped: unresolved` within ~3 s, cursor 97); J11 PASS (list, --needs-me
  Tasks, --item, --state, `no instances match`, `nothing needs you`). Acceptance: all listeners 127.0.0.1; RSS temporal
  115 MB, sindri 82 MB; home 1.1 MB; 0 duplicate idempotency keys.

## Findings for the PR body / owner
- service.ran every 5 s for the `workflow-events` bridge (API P2 C8 × S1-D8 cadence): 51 rows in 3.5 min (~14.6k/day).
  Owner item: skip ok-and-idle runs or record by exception.
- `sindri help` lists only `workflow complete` and `workflow list`; start/status/events/answer/command/cancel missing
  from Usage.cs (small follow-up).
- Agent session may issue nonces (IssueNonce accepted for session:cli) — as designed (class issue).

## Left
1. Wait for/inspect integration + verify-packages (`run8.txt`), run lane-budgets and `bash src/Sindri/aot-smoke/run.sh`
   (heavy wrapper); save outputs in scratch.
2. Write `proof/sindri-wf-p4/manual-test.md` + `evidence.md` (journey table above, raw cross-checks, lenses,
   "/ui-evidence skipped (CLI-only)", Acceptance re-run, p2-reds proof now green, shape changes from handoff 3,
   nonces on HomeCommandNonces, SelfItems stub, CompleteStepRequest.Address → Instance).
3. Rewrite PR #192 body (decisions rev 8.1/8.2 desk Update+timeout, Repo from fact:item, challenge bound to address +
   version, opaque WFI- ids, configurable queue/namespace X61; gaps/owner items; evidence < 60 KB; footer
   🤖 Generated with [Claude Code](https://claude.com/claude-code) + blank line + session link). Keep draft, no review.

## Update (after writing): dotnet gate at 0c4f07c2 finished (`run8.txt`)
build 0 · format 0 · unit 0 · verify-packages 0 · integration 945/948: 3 load failures, none in code this PR touches
the logic of: SpikeEvalTests deskByUpdate wall clock 11.3 s > 10 s budget (passed in the targeted rerun), and
Aw.Evidence CliTests signal-while-waiting-on-lock INT/HUP (33 s timeouts; Evidence untouched). Rerun those under a
quieter box (`--filter-class`) before claiming the lane green. Serve for the proof is stopped (temporal too).
