## What and why

Owner q93: remove the arbitrary limits. Context's #912 removed its own 100-item band cap, so a lens is never truncated by the band. Sindri still mirrored that cap in two places; this removes both.

- `LensBuilder` no longer cuts a lens at 100 nodes and items: `Entry` and `Row` carry every one, and `foundCount` and the row's `found.ids` agree. The `truncated` key stays in the lens entry (the packet schema and the checks read it) but is always empty.
- `SCP-LENS-001` no longer fails a truncated lens that holds more than 100 ids. The stray and absent id rules are unchanged (with no truncation an absent id always fails, which is the intended strict behaviour). The rule is still live, so no code is retired.
- The scoping station spec's `SCP-LENS-001`, U7 and SC-V rows no longer promise "first 100 of n".

Depends on #899 and #912.

## Tests to run

- `Aw.Sindri.Tests` class `LensBandUncappedTests` (new)
- `Aw.Sindri.Tests` namespace `Aw.Sindri.Tests.Scoping` (ScopingCheckTests, ScopingCheckRulesTests, ScopingDeskBlocksTests)

## Goldens touched

None expected (a lens was never over 100 in a golden, and `truncated` was already empty there).

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01C9SRGueDUeVP1LfEAYSEwF
