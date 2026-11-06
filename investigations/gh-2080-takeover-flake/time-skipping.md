# #2080: does time skipping explain the QueuedUpdateTests flake?

Answer: no. (b) is top: under load the takeover reaches the server after the run closed. (a) can't happen in the test as written. Read-only; nothing built or run.

## 1. Recipe and timers
- Test 1 (QueuedUpdateTests.cs:29-69) starts `workflow.dev-timeout` (:33), item T-31. Recipe: recipes/dev/workflow.dev-timeout.yaml, state `hold` with `timeout: { after: 1m, then: park }`.
- That is a workflow timer, armed on entry. Since the `timeout-retries-before-park-v1` patch (RecipeWorkflow.cs:111) it retries once, then parks. No WorkflowExecutionTimeout / RunTimeout is set anywhere: StartWorkflowAsync in WorkflowsHarness.cs:259-260 passes only id + task queue.
- So time skipping CAN fire that 1m timer, but only while the server is unlocked (below). "Workflow timed out" in the earlier probe is that timer path, not a server-side run timeout.

## 2. Environment
- Class fixture `SindriTimeSkippingServer` (SindriTimeSkippingServer.cs:13-24) -> `TemporalServerFixture.Start(ServerKind.TimeSkipping)` -> `WorkflowEnvironment.StartTimeSkippingAsync` with the pinned temporal-test-server (TemporalServerFixture.cs:61-64).
- It is `IClassFixture` (QueuedUpdateTests.cs:17), so ONE server process PER CLASS. No `ICollectionFixture` of it exists (only `WallClockAlone`, which has no fixture and just serializes classes). So no other Engine test class shares this server, and the parallel suite cannot move its clock. Only CPU/IO load reaches it, plus the class's own tests, which xUnit runs one after another.
- Auto time skipping: Temporalio 1.20.0 (Directory.Packages.props:94) `TimeSkippingClientInterceptor` is documented "auto-time-skip when waiting on workflow result" (Temporalio.xml:50207-50247). It only wraps `TimeSkippingWorkflowHandle.GetResultAsync` on handles from `StartWorkflowAsync`. `ExecuteUpdateAsync`, `QueryAsync`, `DescribeAsync` and `WaitAsync` on an update task do NOT unlock.
- In test 1 the only `GetResultAsync` is at :62-63, after `takingOver` already settled. Before that the server is locked, so the 1m timer cannot fire while the two Updates are in flight. Same for test 2 (:90-111).
- Matches the probe: the instance only timed out when the client awaited a result.

## 3. Ranking
(b) Under load, the takeover RPC reaches the server after worker 2 already ran the cancel and the run closed. Top.
- Mechanism: the 500 ms (:50) and 1 s (:52) sleeps order nothing on the server, and `ExecuteUpdateAsync` only starts an RPC. The probe showed the time-skipping server records no UpdateAdmitted events, so the test can't see arrival at all. If the takeover is late, the cancel alone is delivered, the handler runs, `RunAsync` ends, then the takeover hits a closed run.
- Failing history: WorkflowExecutionStarted ... UpdateAccepted/Completed for `cccc...` only, then WorkflowExecutionCompleted; NO events for update `dddd...`. Client: `takingOver` either throws RpcException (not found / already completed) instead of WorkflowUpdateFailedException, or never answers and `WaitAsync(20 s)` throws TimeoutException (:62). The issue symptom is the timeout, so the second shape (hang on a closed run) is likely; probe 2 (post-close takeover, "hung" vs "threw" in its output) settles it.
- A takeover that lands while the run is still open (Cleanup activity, RecipeWorkflow.cs:88) is rejected by the validator and the test passes, which is why it is rare.

(a) Time skipping closes the run first. Excluded for the test as written: locked server, 1m timer, no result await until :62. It would only be possible if some other client call awaited a result on this server. None does.

(c) Something else, low: worker 2's first task (MaxCachedWorkflows = 0, :41) replays and takes the update messages late; same end state as (b) from the client's side, and the same fix applies.

## 4. Red and fix
Red (fails every run unloaded, test-only): add `A_takeover_sent_after_the_run_closed_is_answered_with_SND_WF_003` to QueuedUpdateTests. Start as test 1 does (:33-47), start worker 2, `ExecuteUpdateAsync` the cancel, `await handle.GetResultAsync()` (run closed; this also unlocks time, harmless), then send the takeover with `WaitAsync(5 s)` and assert `WorkflowUpdateFailedException` with ApplicationFailure ErrorType SND-WF-003, the same assertion as :62-64. A closed run can't produce that, so it fails every run (RpcException or TimeoutException). This pins the exact shape the flake takes. Caution: it is only a repro, not a contract. Temporal answers closed-run Updates, not our code (TemporalInstanceRuntime.cs:125,188-190 already maps it to InstanceNotFound). The real fix is below, and the red should be deleted or flipped to assert that RpcException once the fix lands.

Fix, test-only (needs `--allow-test-changes`): make admission deterministic instead of sleeping. The time-skipping server rejects WaitPolicy Admitted ("Admitted stage is not supported", probe), so move QueuedUpdateTests to the dev server, which supports it. Pattern exists: `DevServer` fixture (tests/Aw.Sindri.Engine.Tests/CompleteStep/DevServer.cs:9-21, uses `WorkflowsHarness.StartDevServer`, WorkflowsHarness.cs:106-137). Neither of these tests skips time: the 1m timer never needs to fire, only GetResult is awaited. Then:
1. `IClassFixture<DevServer>` instead of `SindriTimeSkippingServer`; client = `server.Environment.Client`.
2. `await handle.StartUpdateAsync(wf => wf.CancelAsync(cancel), new StartUpdateOptions(new string('c',32), WorkflowUpdateStage.Admitted))`, then the same for the takeover. Each returns once the server has admitted it, so both are queued before worker 2 starts. Delete both Task.Delay lines (:50, :52 and :97, :99).
3. Await the returned `WorkflowUpdateHandle.GetResultAsync` inside worker 2's block, as now.
4. Drop `MaxCachedWorkflows = 0` if the dev server doesn't need it (its comment says time-skipping only; keep it if a run hangs).
Apply the same to the second test (cancel beside a report, :90-111), which has the identical race. The third test (open-request, :113+) has one Update and a 1 s sleep; it is fine either way but can share the fixture.
Alternative if moving servers is unwanted: tolerate closed-run outcome in the assertion (RpcException or WorkflowUpdateFailedException SND-WF-003). That hides the same-activation coverage, so I'd not.

No product change.

## Command for the lead
Red (after adding the new test): `dotnet test --project tests/Aw.Sindri.Engine.Tests --filter-class "*QueuedUpdateTests" --filter-method "*closed*"` (use the real method name you give it; if `--filter-method` is awkward, run the whole class: `dotnet test --project tests/Aw.Sindri.Engine.Tests --filter-class "*QueuedUpdateTests"`).
Probe 2 output decides hang vs throw: run it in /home/thor/repos/aw-wt/api-2080-probe, `dotnet test --project tests/Aw.Sindri.Engine.Tests --filter-class "*UpdateAdmissionProbes" --filter-method "*Probe2*"`, read the "after close" lines.

Unverified: whether the test server hangs or throws on a closed-run Update (needs probe 2), and whether `StartUpdateAsync` with Admitted on the dev server orders two Updates strictly in send order (it should; one RPC returns before the next starts).
