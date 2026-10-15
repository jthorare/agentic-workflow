# Sindri engine P4 integration: handoff 3 (2026-10-13)

Continues `sindri-wf-p4-integration-2.md` (and `-integration.md`). PR #192 (draft). Worktree
`~/repos/agentic-workflow/.claude/worktrees/sindri-wf-p4`, branch `feat/sindri-wf-p4-interpreter`, head
**`6d4fa838`**, pushed. Common brief: `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-engine/unit-brief.md`.
Heavy: `AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy` (slots are heavily contended: 10–25 min waits are normal;
chain build+test in one `sh -c` per slot). Push: `git push origin HEAD:refs/heads/feat/sindri-wf-p4-interpreter`
(never name origin/main in the same command: the block-push-main hook). Trailers: Co-Authored-By Claude Opus 5.5 +
`Claude-Session: https://claude.ai/code/session_019oQXkfxoLc3prCFtJoNGkM`.
Scratch: `~/.agentic-workflow/jthorare-agentic-workflow/scratch/p4-int3/` — `resolve.py <file> mode,mode…`
(ours|theirs|both|theirs-ours per hunk), `hunks.py <files>` (print conflict hunks), `publicapi.py <build.log>`
(applies RS0016/RS0017), `spec.md` (engine spec from main), `p2-reds-proof.txt`, `build.log`, `sindri-tests.log`.

## Done (SHAs)
- 3: `84d0d48c` test — `cancel` twice with the same `--nonce` after the instance ended prints `already applied
  (-> cancelled)`, records nothing (Temporal keeps the first outcome of an update id on a closed workflow; no code change).
- Merges: `fff1e85f` main 6c4f8720 (#194: main's `IApprovedProfile` replaces our identical `IApprovalReader`, deleted;
  `SindriProfile` keeps Workflow then Context; ApproveProfileService takes IChallenges + IContextEngine), `e675ea68`
  P2 registry cb600055 (clean), `7da077f6` main d4de828b (#196; hotspot unions), `f447b634` catalog golden (+ImportContextMap row).
  No engine code used `IApprovalReader` (coordinator's #190 note is already satisfied).
- 4: red `d3bbd0a0`, green `5f3a578f`. `TemporalSettings.Namespace/TaskQueue` from `AW_SINDRI_TEMPORAL_NAMESPACE` /
  `AW_SINDRI_TEMPORAL_TASK_QUEUE` (default `sindri`, in .env.example); SindriTemporal consts removed; dev server
  (`TemporalServerStart.Namespace`, `DevServerProbe.Ask/RegisterSearchAttributes(frontend, namespace, …)`), worker host,
  frontend, starts, worker options use them; `StepTaskQueue.For(state, instanceQueue)` is the per-Step queue seam on
  RunStep's ActivityOptions. Test `Workflows/TemporalNamesTests` (team-a / queue-b): red verified (2 failed), green 17/17.
- 6: `781f60c2` spec rev 8.2 note (header; §5.3 desk timeout parks `timeout`; §5.1 Repo from fact:item's repo).
- 5 + 5b: red `6333fef0`, green `f98cd948`, then `6d4fa838` (main's ContextApiTests/ContextMapApiTests approve via
  P2's challenge instead of `confirmed:true`).
  - 5: `TemporalUpdates` (Patience 4 s; NoNamespace -> SND-API-001 on every runtime/directory call; Update timeout ->
    SND-API-002 naming the nonce, claim kept). Tests `CompleteStep/DeskReportFailureTests`.
  - 5b: `WorkflowInstanceId.Of(address)` (UUIDv8/SHA-256, P3's child rule, ChildInputs delegates), `InstanceView.Id`,
    `KnownInstances`, `IInstanceDirectory.Resolve` takes WFI ids, routes take only ids (`InstanceRoute.Id`, else 400),
    new `GET /workflow/instances?address=` (ListInstancesRequest.Address, `ListInstancesEndpoint`), CLI
    `InstanceTarget.Resolve` (Host/Commands). **Shape change: `CompleteStepRequest.Address` (InstanceAddress) ->
    `Instance` (string)**; also new `ListInstancesRequest.Address`, `InstanceView.Id`, `WorkflowInstanceId.Of`,
    `DevServerProbe` signatures, `SindriWorker.Options(settings, …)`. Tests `ApplyCommand/InstanceIdRoutesTests`,
    `Workflow/Engine/WorkflowInstanceIdOfTests`; WorkflowRoutesTests use ids.
- Gate so far at 6d4fa838: solution build -warnaserror clean; Sindri non-heavy 1761/1777 before the context fix,
  ContextCalls then 24/24 → expected 1770/1777, the 7 reds being the API P2 stubs. Proof they are pre-existing:
  `scratch/p4-int3/p2-reds-proof.txt` (at 0ecd3bf4 on origin/feat/sindri-api-p2-projector the 3 slice tests end in
  `Assert.Fail("red: …")` and `ProjectorProgress` throws NotSupportedException).

## Left (in order)
1. **Merge `origin/feat/sindri-api-p2-projector`** (e7b591ae; coordinator asked; it implements the 7 stubs and the
   projector). `git merge-tree` shows conflicts in: Core EventCatalog, Core PublicAPI, SindriCoreServiceCollectionExtensions,
   Features PublicAPI + registrations, StreamEventsService, Host CommandTable/Usage/Verbs/EndpointTable,
   Server/SchedulerHost, tests InMemoryViewRows, SchedulerHostTests. Union the hotspots; keep both sides' behaviour.
   Then also re-merge origin/main / P2 registry if they moved. After it the 7 reds should go green (say so; keep the
   proof file for the history).
2. Full AGENTS.md merge gate (items 1–9; scorer/judge need `npm ci --prefix judge && npm ci --prefix scorer` if
   "Cannot find module './database'"; bash tests need .github/ci-tools + judge built): typecheck/test TS packages,
   bash tests, sync-rules --check, check-deps, check-env-example (new vars are in .env.example), setup dry-run, no-any,
   dotnet build, unit + integration lanes (`--ignore-exit-code 8`), lane budgets, dotnet format, verify-packages,
   check-rules, `claude plugin validate/test mods/aw-live`. Also `bash src/Sindri/aot-smoke/run.sh` (expected.txt was
   merged as harness-probe warns then context lines — verify the order).
3. Journeys 1–11 (`~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p4/verification-plan.md`)
   via `scripts/sindri-prove.sh` against `sindri serve`, `AW_SINDRI_HOME=$TMPDIR/sindri-dev-p4` (fresh),
   `AW_SINDRI_PORT=0`, built apphost `artifacts/bin/Aw.Sindri.Host/debug/sindri` with `DOTNET_ROOT=$HOME/.dotnet`;
   `recipe register`, then `human recipe approve workflow.step` piping the typed text; items: whatever `SelfItems`
   accepts (src/Sindri/Aw.Sindri.Workflows/SelfItems.cs) — say so.
4. `manual-test.md` + `evidence.md` in `proof/sindri-wf-p4/`: journey table, raw cross-checks, lenses,
   "/ui-evidence skipped (CLI-only)", Acceptance (Temporal) re-run, the P2-reds proof, the shape changes above,
   nonces still on `HomeCommandNonces` (API P1 has no nonce table).
5. Rewrite PR #192 body (< 60 KB evidence) ending with the 🤖 Generated-with footer + session link. No review loop.
