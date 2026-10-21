# Sindri UI U8 T5 + T6 handoff

Branch feat/sindri-ui-u8 (worktree ~/repos/aw-wt/sindri-ui-u8). origin/main (baea6b6, docs only) merged in at the end, no conflicts.

## Commit pairs
| Slice | Red | Green |
|---|---|---|
| T5 viewer registry, facts panel VM, five viewers (20 tests) | 526b3e5e0 | c4f92fc7f |
| T5 facts panel view and frames (3 tests; + FrameFactsTests 4, FactsFramesTests) | 8b2233722 | e2def0018 |
| T6 children panel VM, view, frames, stacked Back (18 tests) | 29ff8c1ff | 33147e1ec |
| format fixes | | chore commit before the merge |

Frames: artifacts/bin/Aw.Sindri.Ui.Tests/debug/frames/u8-frame-facts-payload-open, -facts-pending-desk, -children-workstreams, -children-more.

## What changed / decisions
- U13 extension points: FactViewerRegistry (frame.Viewers.Register(type, factory)) and FactViewTemplates.Register<TViewer>(() => view). No U8 file needs editing. The five views are drawn through FactViewTemplates (code registry) instead of FactTemplates.axaml, because x:Static does not resolve an internal type; FactsPanel adds the template to its own DataTemplates.
- Item/File/Link viewers need no body read; text and payload read GET .../facts/{key}. Payload tree is scrubbed (FactText), limited to depth 8 / 100 children.
- InstanceFrameViewModel is now partial (Panels.cs): Facts, Children, Viewers, ItemRequested, WorkRequested.
- Shell: Back() pops a trail (child frames and item sheets opened from a frame); Esc closes everything; sheet Back and frame Back call it. "Open all in Work" closes the sheet and goes to Work, unfiltered.
- extra-link fixture: tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/workflow-instance-fact/extra-link.json.
- Copy feedback uses a new CopyAction (polite live region pattern).

## Not taken
- not taken: item title in the item viewer: the fact serves only "ITM-.. state" in its summary; the viewer shows id plus that remainder, no extra item read.
- not taken: launching a repo-relative file path: Open only opens a rooted path; otherwise the viewer says it can't open it from here.
- not taken: a served CLI on a child's protected grant: child rows serve none, so the lock popover says "no command served"; which actions are protected (answer-grant, answer-accept) is the app's rule, not served.
- not taken: restoring focus to the clicked child row after Back; focus falls to the existing neighbour/opener logic (never lost, not on the row).
- not taken: per-group "N done today", band mark for mirrored park (Q-API-3, Q-API-5b).
- Not done (later): keys and tabs (T7), docs/PublicAPI (T8).

## Gate (on the pre-merge head; merge was docs only)
- dotnet build whole solution -warnaserror: 0 warnings, 0 errors
- Ui.Tests full (unit + integration): 2063 total, 0 failed, 10 skipped
- Aw.Architecture.Tests: 117/117
- dotnet format --verify-no-changes: exit 0
- check-deps ok; sync-rules --check up to date
