# Sindri UI U8 review run 5 (PR #274) - stopped for restart

Branch feat/sindri-ui-u8, all pushed. Gate NOT run. origin/main (64a4d38) NOT merged yet.

Done (red test commit, then fix):
- r5-0: ReadStrip sends InstanceQuery(Root, Open); chip-word DONE clause removed (terminal-cell belt kept, documented in docs/csharp/sindri-ui.md); N-more test reworked (counts.open - shown).
- r5-3 + coordinator L8 re-proof: ServedCommands now rewrites an answer command whose actionId equals status.answer.actionId to answer.cli; drops bare generic "ApplyCommand:answer" unless it IS the answer's id; dedupes answer CLIs. Tests in ExpandedStateViewModelTests (22/22 pass).
- r5-4: legend "A answers a decision or completes a desk task" (code, test, screens spec line 142). Legend tests not re-run.

Left:
- r5-1: InstanceRow.axaml WorkflowRowInputs wrapper should be Raw when Inputs is empty (peer-level test, red first).
- git fetch, then separately git merge origin/main.
- Full gate: build -warnaserror, Ui.Tests unit+integration, Rendering, Arch (117), format, check-deps, sync-rules.
- r5-2: no code change. Known simplification for U8-min: each column reads as one stop (per-cell navigation dropped); a live AT-SPI re-dump of Workflows is still owed by the proof lane.

## Run 5 finish (second builder) - head 02b590daf, pushed

- Merged origin/main three times (554b9d4, db87fb4/9ff134a, 5cd384d); no conflicts, no new analyzer warnings.
- r5-1 DONE: red tests (InstanceRowInputsTests; first version read all peers, corrected to the control view, where AT-SPI lists stops), then fix: InstanceRow.OnDataContextChanged sets the inputs wrapper Raw when InstanceRowViewModel.HasInputs is false.
- Found by the gate: WorkflowsStripViewModelTests.N_more_counts... failed (the strip counted the rows sent, not the served counts.open). Fixed: HasMore/MoreText use Counts.Open. Also one IDE0001 format fix in StripRequeryTests.
- Gate on 54f4c0634 (before the 9ff134a/5cd384d merges, which touch only bash and Context tests): build -warnaserror 0; Ui.Tests 2163 total, 0 failed (10 skipped); Rendering 21/21; Architecture 119/119 (117 plus #277's two); format 0; check-deps 0; sync-rules 0.
- After 5cd384d: build -warnaserror OK, Ui.Tests unit lane 1580/1580.
- Ui integration lane, Release --no-build: 583 total, 0 failed, 98.7 s test-platform duration (WALL 100 s). Over the 90 s budget, measured on a box shared with other lanes' heavy jobs; not re-measured on a quiet box.
- r5-2 unchanged (known simplification). A live AT-SPI re-dump of Workflows is still owed by the proof lane.
- Incident: a stale gate.sh in the scratchpad (another lane's) ran once from the primary checkout cwd (cd failed): it ran dotnet build/test of Ui.Tests there. No tracked files changed (git status clean); only build output. Killed.

## Recheck1 fixes + main 8529c11 (third builder pass) - head ef6856c64, pushed
- rc-0: added WorkflowsViewModel.Drawn (lists the page drew); the 3 refresh tests wait on shell.Workflows.Drawn; Fake WorkflowsListReads removed; InstanceQueries comment updated. Red check (uncommitted break: Refresh returns early once Drawn > 0): Workflow_events_reread_the_list, Refresh_never_moves_focus_scroll_or_input and A_refresh_keeps_the_selection_and_the_focus_on_it all failed (A_selected_row_that_goes_never_leaves_focus_nowhere failed too). Break reverted, not committed.
- rc-1: test renamed N_more_reads_counts_open_not_the_rows_sent (2 open rows, Counts.Open=9, cancelled assertion dropped).
- rc-2: new test, one InstanceRow reused no-inputs -> inputs: wrapper back in control view with its name.
- Merged main 8529c11 (one conflict in SindriApp.axaml.cs: kept launcher plus main's CA2000 pragma). Style errors: WorkflowsLegend.Name const (CA1802), LauncherContract RealTime.NewGuid (RS0030); one style commit, no suppression, ratchet untouched.
- Gate on ef6856c64: build -warnaserror OK; Ui.Tests 2164 total 0 failed (10 skipped); Rendering 21/21; Arch 119/119; format, check-deps, sync-rules exit 0.
- Ui integration, Release --no-build: 584 total, 0 failed; 3m44s on a saturated box (the Debug whole-project run took 1m34s). Budget 90 s not demonstrable here; re-measure when idle.
