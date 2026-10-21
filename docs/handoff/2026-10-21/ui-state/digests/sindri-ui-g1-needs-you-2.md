# sindri-ui-g1 needs-you slices 2 and 4 (builder digest)
Worktrees: slice 2 /home/thor/repos/aw-wt/sindri-ui-needs-you-2 (branch feat/sindri-ui-needs-you-2, from origin/main 914017793); slice 4 /home/thor/repos/aw-wt/sindri-ui-needs-you-4 (create when slice 2 PR is open, chain from slice 2).
Spec: ~/.agentic-workflow/mockups/f3/specs/needs-you.md; rules: sindri-ui-builder-rules.md, sindri-ui-f3f4-guide.md.

## Findings (2026-10-20)
- UI Api has NO CompleteStep or ApplyCommand client methods yet. Server: POST /sindri/workflow/instances/{id}/runs/{run}/complete (body {instance, run, outcome, outputs:{"fact:x": base64}}), POST /sindri/workflow/instances/{id}/commands (body {instance, kind: answer|command|cancel|takeover|handback, answer, text, amount, verb}; nonce null = issued). Response {result, nonce, alreadyApplied}.
- No shared command service exists on any branch (ui-workflows has only red tests). Slice 4 builds it in Ui/ViewModels/Workflows (InstanceCommands) and tells lead.
- Shell Decide() only handles approve-all and decision options; a desk CompleteStep button did nothing before.
- Run token comes from GET instance .Current.Desk.Run; outcomes not served as a list: approve=approve, send back=revise, drop=drop (spec), merge = served completeAction cli containing "--outcome merged" -> terminal copy.
## Plan slice 2
Api: CompleteStepBody + ISindriApi.CompleteStep (+client, FakeSindriApi.Commands, handler route, spec-shaped golden, contract test). VM: DeskReporter (loads run, sends once), NeedsYouBarViewModel (modes Pending/Confirming/SendingBack/ConfirmingDrop/Approved), page VM owns it. Keys A/D/Enter via shell InboxKeyPressed. View: action bar in NeedsYouPage, scroll-to-end flag, More menu (Drop), E chip. Labels in Labels.NeedsYouBar.cs (Ny* prefix NeedsYou*).
## State (2026-10-20 evening)
- Slice 2 branch feat/sindri-ui-needs-you-2: red commit fcffd9e70 pushed (tests + stubs; not run separately: heavy session queue waits ~25 min per job).
- Green work in the tree (uncommitted): real NeedsYouBarViewModel, client CompleteStep, page VM wiring, shell keys A/D/X/Enter, XAML action bar + More menu + done card, code-behind (ScrollChanged -> ReadToEnd), tests (page VM, shell inbox keys, frames). Patch scripts in the builder scratchpad.
- Next: gate (build -warnaserror, unit lane, FULL Ui.Tests integration, Rendering, Arch, scoped format, IDE0001), commit green, push, PR, SendMessage main.
- Decision for lead: no served outcomes list, so outcomes are approve/revise/drop (spec); merge/protected-complete = the instance's served complete command class "protected" -> terminal copy with its cli; send back on a terminal-only desk step sends outcome rework.
