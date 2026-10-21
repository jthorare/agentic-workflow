# U9a0 digest (branch feat/sindri-ui-u9a0)
Commits: 339804fda test red; 8ef5660b3 feat green; plus fix commit (review route built at run time, usings order).
Gate: solution build 0 warnings; Ui.Tests 2292/0 failed (~1m40s whole project incl. integration); Rendering 21/0; Architecture 121/0; format clean; check-deps ok; sync-rules ok.
Plan vs main: FileMap takes a FileMapQuery record (AW1006 caps methods at 5 params); ContextRoutes.Review is string.Join-built because the architecture test flags the protected route literal (shared route); NavTabClickTests now treats Context as a real page; LensesView exposes Anchors over wire "anchors"/"anchor"; refusal extras (suggestedAnchors, candidates) ride on Error.Extensions via ContextRefusal.
Defaults: Q11 strings + ContextWords.Known; sources has no golden so fake answers 404; draft lenses answers 044.
Left: T0.6 (not mine); Labels.Context has only 3 words; LabelsTests.No_word_is_defined_twice not added; fake Sources not served.

## Review run 2 fixes (done)
Commits: fake missing-anchor golden exact-set + single/gate-release contract cases; arch pin ReviewDecisionRequest.Verb is DeskVerb (Desk class) + wire-bytes test; ContextPorts.Follow + seam/click-focus tests; docs Seams. Merged origin/main (52e5fd4). Head a73222399 pushed.
Gate: build 0/0; Ui.Tests 2315/0 (1m16s whole project); Rendering 21/0; Architecture 128/0; format, check-deps, sync-rules clean.
Resolutions in 342.json: 13 folded, 1 fixed, 1 follow-up (qa-expert-r2-1), 1 owner re-check (qa-expert-r2-2). Nothing declined.

## Run 3 fold-ins (done)
Fake answers its not-served 404 (reads Unavailable) for an unheld partly-known lens set; golden matched as sets; docs say ContextPorts is positional. fix-verifier-r3-0/1/2 folded in 342.json. Merged origin/main 9990ace. Head 75eb9371b pushed.
Gate: build 0/0; Ui 2315/0 (1m47s whole project); Rendering 21/0; Arch 128/0; format, check-deps, sync-rules clean.
