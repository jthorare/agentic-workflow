# F3/F4 builder briefs (sindri-ui lane, 2026-10-19)

Common to all four:
- Read ~/.agentic-workflow/digests/sindri-ui-builder-rules.md and ~/.agentic-workflow/digests/sindri-ui-f3f4-guide.md FULLY first.
- Your worktree is /home/thor/repos/aw-wt/sindri-ui-f3-<unit>, on branch feat/sindri-ui-f3-<unit> from origin/main ac3b15472. It's already pushed.
- Your digest is ~/.agentic-workflow/digests/sindri-ui-f3-<unit>.md.
- Goal: your pages show ONE primary thing, put the rest behind disclosure, and use plain words (the guide's table).
- Before editing, take one quick inventory: list every user-visible string and section on your pages (Labels.*.cs plus literals in .axaml). Mark each one keep, rename, or move into Details. Put the table in your digest.
- Gate (per the rules): build -warnaserror; the Aw.Sindri.Ui.Tests unit lane; the touched integration classes once; Aw.Sindri.Ui.Rendering.Tests; Aw.Architecture.Tests; format. Run it as one heavy script.
- Open a PR titled "feat(ui): <your pages> — plain words, one primary thing". Its body has the inventory summary (renamed, moved and kept, with reasons), the tests that ran with counts, and what's left.
- Report: PR number, head sha, the tests with counts, what's left, and any decisions.

## B1 frame: the instance frame
Pages: Views/ViewModels Instance/* (header, states band, expanded state, run line, Attach, children panel, tabs), Facts/* (the fact viewer and scope map), and Recipe sheet only where it opens from the frame.
Labels: Labels.Instance.cs, Labels.Facts.cs, Labels.ScopeMap.cs, Labels.Children.cs, Labels.Run.cs, Labels.Attach.cs, Labels.Protected.cs.
Primary thing: the current stage, and what it needs from you (or which agent is on it). Commands, the full states band detail, facts ("What this run collected"), children and ids go into disclosures. The header shows the workflow's display name and the item title, never the address (the address goes in Details with Copy).

## B2 workflows: the Workflows list, Recipes and Start form
Pages: Workflows/*, Recipes/*, Start/*.
Labels: Labels.Workflows.cs, Labels.Recipes.cs, Labels.Start.cs, Labels.Strip.cs.
Primary thing: the runs that need you or are active, each as one row: display name, item title, stage, and who's on it. Per-state cells are secondary (show the current one; the rest behind a hover or Details). Use the recipe display name everywhere: read `displayName` from the instance and recipe JSON as OPTIONAL, falling back per the guide. Put one shared helper in YOUR area. B1 will want it too, so make it `internal static` in a new file ViewModels/Workflows/WorkflowNames.cs, and say so in your report. The Start form uses plain input labels and hints.

## B3 home-work: Home, needs-you, Work (queue and item sheet), System and Health, Settings
Pages: Shell/Home*, NeedsYou/*, Queue/*, Item/*, Settings/*, and the System/Health page.
Labels: Labels.NeedsYou.cs, Labels.Items.cs, Labels.App.cs, Labels.Control.cs, Labels.Actions.cs.
The owner's example lives here: Work > a row > the sidebar "facts" list. Rename it per the guide, show only what helps decide what to do with the item, and say why each section is there. Home's primary thing is "what needs you now". System and Health lead with one sentence ("Everything is running" or what's wrong); the per-service detail goes behind disclosure.

## B4 agents-shell: Agents, Context, palette, shell and navigation, plus a follow-up
Pages: Agents/*, Context/*, Palette/*, Shell/* EXCEPT Home (B3 owns Home), and Layout.
Labels: Labels.Agents*.cs, Labels.AgentSheet.cs, Labels.Context.cs, Labels.Palette.cs, Labels.cs (shared, you own it).
Primary thing on Agents: who's working, and who's asking you something. On Context: the answer to the anchor you typed. Nav: plain page names and keys. Follow-up: swap tests/Aw.Sindri.Ui.Testing/AgentRunFixtures.cs's agent-run.json copy for the real golden tests/Aw.Sindri.Tests/Golden/Api/workflow-status/agent-run.json now on main (P8 merged), and update GoldenContractTests' _notRead honestly.
