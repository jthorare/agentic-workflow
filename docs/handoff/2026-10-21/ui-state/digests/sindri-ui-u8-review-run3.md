# Sindri UI U8 review run 3 fixes (PR #274)

Branch feat/sindri-ui-u8, head ae2c7ae0a (pushed). origin/main merged at start (aa533378d) and again after the coordinator note (#272 P11, clean merge, 18e7c52 included).

## Items
- X1 r3-0: red cd test 3280f1061, fix cd2a54553. Signature keeps only child.Id; ChildPointerViewModel is observable (Text, Name) and ExpandedStateViewModel.Update calls Child.Update. Test (InstanceFrameRereadTests): confirm open, child state changes, same Expanded instance, IsConfirmingCancel true, new text and name.
- r3-1: VM test (InstanceFrameFailureTests): stacked open that 404s sets WantedFocus Back. Unit test (ShellStackedSheetTests): child row OpenItem raises SheetStacked. Headless (folded into ChildrenFramesTests stacked/Back test): Back to a parent no longer drawing the child lands on InstanceOpenIn/ExpandedCopyStatus. Not separately red (tests for existing behaviour).
- r3-2: shared Views/Heard.cs; Heard.Wrapper asserts IsControlElement, Text control type, inner TextBlocks not control elements. Used for legend, stage, input, command wrappers.
- r3-3: ChildRowViewModel.OpenFrameName = OpenInstanceName(Title, Address); pointer name = OpenInstanceName(drawn text, address). Peer asserts in ChildrenFramesTests and InstanceFramesTests.
- r3-4: project-build frame taken before the pointer is injected (pointer arrives on a Refresh afterwards).
- r3-5: AvaloniaLauncherContractTests shares one window per launcher.
- Tests for r3-1..r3-5 and the r3-3 fix went in one commit (5acd84d00), no separate red.

## P11 delta
None. After merging #272 every U8 test passed unchanged; no golden-driven edits and no production change were needed. No place in the strip or Workflows paths was changed.

## Gate (head ae2c7ae0a)
- dotnet build -warnaserror whole solution: 0 warnings, 0 errors
- Ui.Tests unit 1560/0 failed; integration 576 (566 passed, 10 skipped, 0 failed)
- Rendering.Tests 21/21; Architecture.Tests 117/117; Aw.Sindri.Tests 4758/0
- format exit 0; check-deps 0; check-rules 0; sync-rules --check up to date
- Ui integration Release --no-build wall: 87.7 s and 84.6 s (includes heavy-lock wait; debug run 89.7 s). Close to the 90 s budget.
