# sindri-ui-r8 run 4, pass A handoff

Branch feat/sindri-ui-r8-contract, pushed head a434e8f5.

## Done
- ab0e9c83 merge origin/main (d3460f18). One conflict, GoldenContractTests `_notRead`: main's context slugs kept, workflow-* names dropped.
- W1: 6c33ed89 red, 09529ee4 green. Anchor tests moved to LaneAnchorTests (client, switch, XAML via XDocument nesting, markers text); each re-runs over an in-memory sample insertion. Deviation: the brief's "no public member after u8 before Events(" contradicts U8 inserting public sends below its marker, so the client test asserts u8 < Events( instead.
- W2: 1a9b692c red (kinds-list parse + disjoint + real kinds + XAML equals Sheet.cs), then green commit: markers in ShellViewModel.Sheet.cs and ShellWindow.axaml carry `(kinds: …)`; U3' = NeedsYou, Decision; U8 = Workflow; Item/HomeNeedsYou/later kinds explained; docs/csharp/sindri-ui.md "Lane anchors" updated. XAML text says "PR 197" (ContrastTests treats "#197" as a hex colour).
- Cheap: testing-0 (used from golden), testing-1 (SheetName/EdgeTabName raised, loop reads SheetName, EdgeTab Name binding), a11y-4 (ProtectedName = verb + subject; HelpText = Title; tests updated), a11y-5 (_openingSettings flag skips KeepFocus; focus-event count asserted <= 1; no red-first for this one), arch-2 (doc sentence). Commit 14723422; a434e8f5 is a format fix.

## Results on the final tree
UI unit 918/918, UI integration 293 passed, 2 skipped (run on 14723422), Aw.Architecture.Tests 117/117, dotnet format exit 0, check-deps ok, check-rules ok, sync-rules --check ok.
Not run: whole-solution build, Aw.Sindri.Tests (not in this pass's list).

## Left
W3-W6, PR body "Review run 4", MERGEABLE check.
