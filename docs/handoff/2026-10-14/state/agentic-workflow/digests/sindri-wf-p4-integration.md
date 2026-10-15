# Sindri engine P4 integration: handoff (2026-10-12)

**Goal:** P4 integrator. Merge units a–e onto main, apply the integration fixes test-first, run the gate and the proof
(journeys 1–11), write `manual-test.md` and `evidence.md`, then report to the coordinator. The review loop comes after that.

- **Worktree:** `~/repos/agentic-workflow/.claude/worktrees/sindri-wf-p4`
- **Branch:** `feat/sindri-wf-p4-interpreter`, PR #192 (draft), head `2a974947`, pushed.
- **Tooling:** every dotnet command runs through `AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy`. Commits end with the
  trailers in `/private/tmp/claude-501/.../scratchpad/p0-unit-brief.md`.
- **Push:** use `git push origin HEAD:refs/heads/feat/sindri-wf-p4-interpreter`. A command that names `origin/main` next to
  `push` trips the block-push-main hook.
- **Scratch:** `/private/tmp/claude-501/-Users-thor-repos-agentic-workflow/a5734686-5b5d-4592-9342-00609c54ed2d/scratchpad/p4-int/`.
  It holds `spec.md` (engine spec rev 8.1 from main) and `resolve.py` (conflict helper).
- **Never touch:** the real `$AW_SINDRI_HOME`, setup.sh, the providers, MCP registration.
- **Disk:** about 6.7 GB free. Stop if it drops under 5 GB.

## Done
Merges: main (e4452c63) → (a) 385b62be → (e) 169dd8bd → (d) 1e6af1e5 → (b) 5c84865b. All are committed; each merge
commit message lists its resolutions.

Fixes (test-first; red commit, then the fix):
- `fa6d2e7b`: SpikeEvalTests' 4h Update timeout runs on its own time-skipping server (F1).
- `48162896`: a cancelled dev-server stop SIGKILLs before it gives up (`TemporalDevServer.StopAsync`, `DevServerTemporal.Stop`).
- `fe672e4a`: `TempHome.Dispose` kills any dev server on its database. The Workflows tests used to leak 7 per run; now 0.
- `55d4a1bf`: the challenge subject is `instance:<address>@v<version>` (owner decision).
- `2c52f300`: `workflow.eventHold` expiry. `WaitSatisfied.OccurredAt` (init-only) is stamped by `DeliveredEvents`.
  `LoadedRecipe.EventHold` comes from `ProfileWorkflowTimes`. `RecipeWorkflow.Hold.cs` adds `WaitHolding` and
  `ExpireHeld`, which writes `workflow.expired`. Tests: `tests/Aw.Sindri.Tests/Workflows/HeldEventTests.cs`.
- `c30434d8`: the Temporal tests' FakeTimeProvider starts at UtcNow, and bridge test events use the host clock.
- `9491f6c7`: the bridge's held window (`HeldItemEvents`) re-sends item events to instances visibility missed. Test:
  `An_item_event_seen_before_its_instance_is_visible_still_reaches_it_within_the_hold`.
- Desk wait wakes on `_submitted` (`ApplySubmitted`). Test:
  `InterpreterStateLoopTests.A_cancel_sent_while_the_desk_task_is_open_...`, green after (b).
- `DeskSettings` removed: `LoadRecipe` reads deskTimeout and eventHold from `ProfileWorkflowTimes`.

## Gate at 2a974947
- `dotnet build AgenticWorkflow.slnx -warnaserror`: 0 warnings, 0 errors.
- `dotnet format --verify-no-changes`: clean.
- check-deps, check-rules and `sync-rules --check`: pass.
- `Aw.Sindri.Tests` without the heavy lane (last full run before the bridge fix): 7 known reds, all API P2 contract
  stubs that came in through (e)'s merge of 0ecd3bf4: ListServices, ProjectorProgress ×4, RebuildViews, GetProfile.
  The workflow, bridge, ApplyCommand and CompleteStep namespaces are all green.
- Not yet run: the other test projects, the lane budgets, the host smoke (`src/Sindri/aot-smoke/run.sh`), the bash tests.

## Left (in this order)
1. **Write verbs through API P1's chain.** `start`, `answer`, `command`, `cancel` and `complete` call their slices
   in-process with the actor hard-coded `human:cli`:
   - `Host/Commands/WorkflowStartCommand.cs`, `ApplyCommandVerbs.cs`, `CompleteCommand.cs`.
   - Map the engine routes from engine spec §6.3 (lines 707–713 of `spec.md`): `POST /workflow/instances`,
     `/{a}/nonce` (issue), `/{a}/commands`, `/{a}/runs/{run}/complete`.
   - Register the slices with `AddCommand` (as StopProcess does) and add catalog rows: `StartInstance` desk,
     `ApplyCommand:answer` / `command` / `cancel` desk, `answer-grant` and `answer-accept` protected, `CompleteStep` desk,
     `IssueNonce` issue. See `Host/Catalog/ShellRows.cs` and `PendingRows.cs`, API spec §3.2 and §4.1, and update the
     golden `tests/Aw.Sindri.Tests/Golden/Api/commands/catalog.json`.
   - Switch the CLI to `ControlPlaneClient` with `CallerHeaders` (see `Verbs.Stop`).
   - Add tests: an agent session gets SND-AUTH-002 for each of the five verbs (Phase 4 signal 5).
2. **Same nonce after the instance ended** (e.g. cancel twice): assert what Temporal returns for an Update on a closed
   workflow, and print `already applied` or refuse cleanly.
3. **Task queue and namespace configurable** (README X61): today they are consts in `Workflows/SindriTemporal.cs`.
   - Move them into `TemporalSettings`, default `sindri`, read like the other settings.
   - The worker, the clients and the activity options take them from settings, with a seam for a per-Step activity queue.
   - Add a test-first test: an instance runs on a non-default queue and namespace.
   - Keep tmux and machine assumptions out of the interpreter and Core.
   - Actors stay open strings (they are now).
4. **Port main's dropped review-run-3 intent onto `TemporalDeskReports`:** a NotFound that names the namespace is
   SND-API-001, not SND-WF-001. Check too that `complete` with no worker running doesn't hang (it has no RPC timeout).
5. **Spec rev 8.2 note** in `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`, plus a header revision line:
   - §5.3: a desk timeout parks with `timeout` (W20, §3.8), not `step.failed(timeout)`.
   - §5.1: `Repo` comes from `fact:item`'s `repo` and is unset otherwise.
6. **P2 approvals:** swap the `HomeApprovedRecipes` stub for P2's `IRecipeApprovals` once #191 merges (still OPEN). Then
   dedupe P2's `IChallenges` / `ProcessChallenges` copies: the 94 PublicAPI lines and the duplicate registration.
   Nonces stay on `HomeCommandNonces`.
7. **Full gate**, then the proof: `scripts/sindri-prove.sh`, journeys 1–11 from
   `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p4/verification-plan.md`. Write
   `manual-test.md` and `evidence.md` in that `proof/sindri-wf-p4/` folder, then report to the coordinator before the
   review loop.
