# G1 setup slice 4 (agents, map, Onyx and repo rows) digest
Worktree /home/thor/repos/aw-wt/sindri-ui-setup-4, branch feat/sindri-ui-setup-4, from #540's head 204f8be9d (it MUST merge after #540).
Spec: ~/.agentic-workflow/mockups/f3/specs/setup.md §2.4, §3, §4, §5, §6, §7, §8 slice 4. Contracts: api #528 AddRepo goldens (copy to SpecShaped/setup-repos), Onyx/RegisterMcp fixtures.

## Findings (2026-10-20)
- Base #540 ALREADY has: map meter, Retry=RefreshContextMap via SetupViewModel.RefreshMap, 409 runningJob = already building, tests in SetupActionTests. Slice 4 adds the rest.
- Plan: ISindriApi AddRepo/RegisterMcp/SetupOnyx/ConnectOnyx/DeferOnyx (+client, fake, handler arms, contract tests, JSON context);
  IFolderPicker (+Null, Avalonia, Fake); VMs SetupRepoFormViewModel, SetupOnyxViewModel, SetupAgentViewModel (agents from mcp item details);
  views SetupRepoForm/SetupOnyxForm/SetupAgentList; Onyx footnote under panel; SpecShaped catalog extra-setup-ready.
- Agents list derives from served mcp items detail ("Claude Code, Codex") until mcp.registered outcomes are served (slice 5).
- Departures to list in PR: no name field on repo form (mock has none); no Install Docker link (no URL served); agents from item details.

## Progress
(nothing committed yet)
