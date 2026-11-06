# Handoff: gh-2080-takeover-flake

status: diagnosed
report: /home/thor/repos/aw-wt/api-2080/investigations/gh-2080-takeover-flake/handoff.md
boundary: tests/Aw.Sindri.Engine.Tests/Workflows (cause site); read-only context: src/Sindri/Aw.Sindri.Workflows
repro: dotnet test --project tests/Aw.Sindri.Engine.Tests --filter-class "*QueuedUpdateTests" (under CPU hogs, ~20x). Investigate-only: nothing was built or run by this investigation; hypotheses are from reading code.

## Root Cause
The test assumes its two Updates reach the second worker in ONE workflow task, using wall-clock sleeps (QueuedUpdateTests.cs:50 500 ms, :52 1 s) as the ordering guarantee. `ExecuteUpdateAsync` (:49, :51) only starts an RPC; under CPU load the takeover RPC can reach the server late (or the second worker's first task can be cut before it is attached). Then the takeover is admitted in a LATER task than the cancel, or after the workflow already closed. Product code answers correctly in every case where the workflow is still open: RecipeWorkflow.Takeover.cs:30 `ValidateTakeover` -> Commands.cs:80 `Validate` -> `Decide` returns Rejected on an ended instance -> `Refusal` (SND-WF-003); Loop.cs:51-74 `SettleLeftovers` handles the same-activation case; and during the Cleanup activity (RecipeWorkflow.cs:88, Cleanup.cs:30) the workflow is still open, so a later validator also rejects. The only gap is an Update that is admitted but not accepted when the run closes (after RecipeWorkflow.cs:89 returns). Temporal does not run any handler for it; the server answers it itself with a "workflow execution already completed" error (an RpcException to the client), and the time-skipping test server evidently never answers the pending poll, so `takingOver.WaitAsync(20 s)` (:62) throws TimeoutException. That is not a code path in RecipeWorkflow.

Bug location: TEST. Cause site: tests/Aw.Sindri.Engine.Tests/Workflows/QueuedUpdateTests.cs:49-52 (sleep-based ordering) and :62 (assertion presumes a WorkflowUpdateFailedException is the only possible outcome). PRODUCT is not at fault: no handler can answer an Update the server never delivers, and production already maps "ended" to InstanceNotFound (TemporalInstanceRuntime.cs:125,188-190). The issue's expected behaviour (SND-WF-003 for a takeover queued behind a cancel) is already met for every delivery the workflow sees; it only needs the test to guarantee the same-activation delivery, or to assert the post-close outcome separately.

## Hypotheses

| # | Hypothesis | Cause-site files | Likelihood | Result |
|---|-----------|------------------|------------|--------|
| 1 | Test timing: under load the takeover reaches the server after the cancel's task closed the run; Temporal never runs the validator and the client waits out its 20 s (TimeoutException) | `tests/Aw.Sindri.Engine.Tests/Workflows/QueuedUpdateTests.cs:49-52,62` | High | untested |
| 2 | Product: an Update delivered while the workflow is open but after the end (Cleanup activity running) is not rejected | `src/Sindri/Aw.Sindri.Workflows/RecipeWorkflow.Takeover.cs:30`, `RecipeWorkflow.Commands.cs:80` | Medium | ruled-out |
| 3 | Product: `SettleLeftovers` loses a takeover queued beside the cancel in the same task | `src/Sindri/Aw.Sindri.Workflows/RecipeWorkflow.Loop.cs:51-74` | Low | ruled-out |
| 4 | The time-skipping server never answers an Update sent to a closed run (vs the dev server returning an error) | `tests/Aw.Sindri.Engine.Tests/Workflows/QueuedUpdateTests.cs:62` | Medium | untested |
| 5 | Time skipping fires the recipe's 1m hold timeout before the late takeover lands | `tests/Aw.Sindri.Engine.Tests/Workflows/QueuedUpdateTests.cs:29-69` | Medium | ruled-out |

Row notes:
- 2: the validator runs `Decide` on the ended instance and throws Refusal SND-WF-003; `_instance` stays ended during Cleanup.
- 3: it drains `_submitted`, decides on the ended instance, loops until `Workflow.AllHandlersFinished`.
- 5 (rc-2080b, time-skipping.md): Temporalio 1.20 advances time only while the client awaits a workflow result (`handle.GetResultAsync`); `ExecuteUpdateAsync` doesn't. The test's only `GetResultAsync` is after `takingOver` settles (:62-63), so the timer can't fire while the Updates are in flight. The class has its own server (`IClassFixture<SindriTimeSkippingServer>`), not shared with other classes.
- Probe (probe/gh-2080-admitted): the time-skipping server rejects WaitPolicy Admitted and writes no Admitted events, so no admission barrier exists there; the dev server fixture (`CompleteStep/DevServer.cs`) is the place for one.

## Ruled Out
- Validator accepts the takeover on an ended instance: Validate throws Refusal when Decide returns Rejected.
- SettleLeftovers exits early: loop condition is AllHandlersFinished.

## Suggested Fix
Test-side (preferred): make the ordering deterministic instead of sleeping.
1. Before starting worker 2, poll the history (`handle.FetchHistoryEventsAsync`) until both WorkflowExecutionUpdateAdmitted events (ids 'c...' and 'd...') exist, replacing the 500 ms and 1 s delays. Keep the cancel first by waiting for its Admitted event before sending the takeover. Then both are queued before any worker polls.
2. Assert with `takingOver` tolerant of the server's own close answer only in the separate test below.

Proposed deterministic red (fails every run, unloaded): `A_takeover_sent_after_the_workflow_ended_fails_with_a_known_error`. Steps: start the instance, run worker 2, send the cancel, await `handle.GetResultAsync()` (state cancelled, run closed), then `handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(takeover), ...)` with a 5 s `WaitAsync`. On the time-skipping server this reaches the server after close and either times out (same symptom as the flake, confirming hypothesis 4) or throws RpcException; the red asserts WorkflowUpdateFailedException (SND-WF-003), which fails every run. The green is to assert the real contract: RpcException (NotFound / already completed) for a post-close Update, and keep the same-activation test with deterministic admission (step 1) asserting SND-WF-003.
Alternative deterministic delay for the loaded case: send the takeover only after worker 2 has completed the cancel task (wait for WorkflowExecutionUpdateCompleted of the cancel in history) but before the run closes is impossible to hold open without an activity; so use the post-close variant above.

Product change only if the owner wants SND-WF-003 even after close: not possible in workflow code (Temporal answers closed-run Updates); it would need the caller (TemporalCommands/TemporalInstanceRuntime) to map the server's already-completed RpcException to WorkflowErrors.TriggerNotAllowed, which TemporalInstanceRuntime.cs:188-190 already does as InstanceNotFound.

## Run request for the lead (nothing run here)
After applying the test change: `dotnet test --project tests/Aw.Sindri.Engine.Tests --filter-class "*QueuedUpdateTests"` 20x under hogs; to prove hypothesis 1/4 dump `handle.FetchHistoryEventsAsync()` event types in the failing run's output (add temporarily).

## Update 2026-11-05: late-takeover theory ruled out; diagnosis added instead
- The only failing run on record (#2074's gate, integration lane): the cancel answered, then the takeover's WaitAsync timed out after the full 20 s (`TimeoutException` at QueuedUpdateTests.cs:62, 25 s total). So the takeover got no answer at all; it did not get a wrong answer.
- Late takeovers tried on a quiet box with worker 2 running, 3 runs each, every one answered at once: takeover sent after the cancel returned -> `WorkflowUpdateFailedException`; cancel and takeover sent back to back -> same; takeover sent after the run closed -> `RpcException` "update ... not found". So row 4 (the time-skipping server never answers an Update sent to a closed run) is false here, and a takeover sent 3 s late (0081e05, reverted) passed 5/5.
- 20 runs of the class under CPU hogs (`yes` per core): 20/20 pass. The red has only ever shown in the full parallel integration run.
- Not ruled out: an Update accepted but never answered when it races the run's close under real contention; worker 2's first task missing the takeover on the `MaxCachedWorkflows = 0` replay path.
- Next: the test now dumps the run's status and history when an Update wait times out, so the next red names which of these it is.
