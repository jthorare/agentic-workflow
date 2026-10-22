## What changed

Fixes two reds that #621 (step.prove.holdout-log) left on the base branch:

- `ScopingBlockRuntimesTests.The_registered_Steps_are_the_ones_this_build_runs`: its `_registered` list now names `step.prove.holdout-log`, which #621 registers.
- `WorkflowErrorsTests.Every_code_of_an_area_is_live_or_retired` (SND-BLOCK): the holdout store's two codes were `SND-BLOCK-020` and `-021`, a gap after `-004`. They're renumbered to `SND-BLOCK-005` (no hidden set) and `-006` (store unavailable), in WorkflowErrors, HoldoutFiles, IHoldoutStore, HoldoutStoreContract and the docs error table, and the test's `last` is 6. Nothing else pins the old numbers (no golden or recipe).

## Tests

Build `-warnaserror` (Aw.Sindri.Tests): 0 warnings. WorkflowErrorsTests, ScopingBlockRuntimesTests, the Holdout classes, SindriErrorsTests and ShippedBlocksTests: 114/0.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01FH7fi6qZLKrDbBRsYis5Ao
