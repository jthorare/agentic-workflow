# Sindri engine P4 integration: handoff 2 (2026-10-13)

Continues `sindri-wf-p4-integration.md`. PR #192 (draft). Worktree
`/Users/thor/repos/agentic-workflow/.claude/worktrees/sindri-wf-p4`, branch `feat/sindri-wf-p4-interpreter`, head
`f8b0a629`, pushed. Heavy commands: `AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy`. Push with
`git push origin HEAD:refs/heads/feat/sindri-wf-p4-interpreter`. Commit trailers: Co-Authored-By Claude Opus 5.5 +
the session line. Clear only bin/obj/artifacts if disk < 5 GB (never git clean). Scratch:
`/private/tmp/claude-501/-Users-thor-repos-agentic-workflow/a5734686-5b5d-4592-9342-00609c54ed2d/scratchpad/p4-int2/`
(`publicapi.py <build.log>` applies RS0016/RS0017 lines to PublicAPI.Unshipped.txt; it can get wiped, then recreate it).

## Done
1. P2 merged (`c0fb275c`: resolutions in the message; 86 duplicate Core PublicAPI lines removed; IChallenges registered once).
   Approvals: red `104d1cf0`, green `ba5196e8` (+ format `3a010a43`). `RegistryApprovedRecipes` (Core/Workflow/Registry,
   internal, registered in AddSindriCore) replaces `HomeApprovedRecipes` (deleted, with `WorkflowsHome.RecipesDirectory`).
   Start runs RCP-CAP-003 too (revoked pin -005, drift -006). Tests register/approve via `WorkflowsHarness.Register/Approve/Revoke/Recipe`.
2. Write verbs through the chain: red `9ebfb576` (WorkflowRoutesTests), green `f8b0a629`. See the commit message.
   Endpoints in Host/Endpoints (StartInstance, IssueNonce, ApplyCommand, CompleteStep, InstanceRoute). CLI writes use
   `RecipeDesk` (client + input + EnsureRunning). IssueChallenge issues instance answers (`grant n <a>`, `grant-tokens n <a>`,
   `accept <a>`), response has `Instance`. `Served` (tests/Aw.Sindri.Tests/ApplyCommand/Served.cs) is the real HTTP host
   (Cli, Typing, AsSession, RestartWorker, AsHumanAtTerminal). `NoInstances` fake in Aw.Sindri.Testing/Workflow.
   Gate at f8b0a629: tests/Aw.Sindri.Tests build -warnaserror clean, `dotnet format --verify-no-changes` clean, Sindri
   tests non-heavy: 1712/1719 green; the 7 reds are the API P2 stubs (ListServices, ProjectorProgress x4, RebuildViews,
   GetProfile). Full-solution build not re-run since the merge.
   Nonces: API P1 has no nonce table, so `HomeCommandNonces` stays the stand-in behind the IssueNonce route (say so in the PR).
   Known gap: `{instanceId}` is the escaped address, not an opaque WFI id (spec §6.3 last line).

## Left (in order; brief from the coordinator, unchanged)
3. Same nonce after the instance ended (e.g. `cancel` twice with `--nonce`): test that it prints `already applied` or
   refuses cleanly. Code: `TemporalCommands.Execute` / `ApplyCommandService.Send` (Temporal Update on a closed workflow).
4. Task queue and namespace configurable (README X61): remove `SindriTemporal.TaskQueue`/`Namespace` consts
   (Workflows/SindriTemporal.cs); settings in `TemporalSettings` (env, default `sindri`, e.g. AW_SINDRI_TEMPORAL_NAMESPACE /
   AW_SINDRI_TEMPORAL_TASK_QUEUE, add to .env.example), used by the worker, `TemporalFrontend`, the dev server start,
   activity options; a seam for a per-Step activity queue. Test-first: an instance runs on a non-default queue and namespace.
   Users of the consts include tests (WorkflowsHarness.StartDevServer, .Start).
5. Port main's dropped intent to `TemporalDeskReports` (and `TemporalCommands`): RpcException NotFound whose message names
   the namespace -> SND-API-001 (see `git show origin/main:src/Sindri/Aw.Sindri.Workflows/TemporalDeskSteps.cs` and test
   commit `09f52f3a`); `complete` with no worker: give the Update an RPC timeout (below the CLI's 30/10 s HTTP timeout and
   mind the 5 s CommandGate) and fail cleanly.
6. Spec rev 8.2 note in `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`: §5.3 desk timeout parks
   `timeout` (W20/§3.8), not `step.failed(timeout)`; §5.1 `Repo` from `fact:item`'s `repo`, unset otherwise; header revision line.
7. Merge origin/main again; full AGENTS.md gate; journeys 1–11 via `scripts/sindri-prove.sh` against `sindri serve`
   (AW_SINDRI_HOME=$TMPDIR/sindri-dev-p4 fresh, AW_SINDRI_PORT=0; `recipe register` then `human recipe approve` workflow.step
   with the typed text piped; item ids from the SelfItems stub); manual-test.md + evidence.md in
   `/Users/thor/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p4/`; prove the 7 API P2 reds also fail on
   `origin/feat/sindri-api-p2-projector`; update the PR #192 body (footer: Generated with Claude Code + session link).
   Don't run the review loop.
