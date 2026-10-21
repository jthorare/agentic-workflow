> **Newer state: read `~/.agentic-workflow/digests/sindri-ui-2026-10-18d.md` first.**

# Sindri UI lane digest, 2026-10-18 b (supersedes sindri-ui-2026-10-18.md for state; rules unchanged there)

Session sindri-ui; meta sindri-meta; lane map v12 (names unchanged). Main 9990ace (#344 CO-3 merged; catalog pin 54).
Builder rules file (brief every builder to read it): /tmp/claude-1000/-home-thor-repos-agentic-workflow/b7a92a30-4136-4f13-bcd9-c06bb00c150a/scratchpad/builder-rules.md
Builders hit the Sonnet session limit at ~00:50 (resets 02:50 PT). Resumed at 01:34 on the meta's word; if they fail again, wait for 02:50.

## #342 U9a0 (worktree aw-wt/sindri-ui-u9a0, branch feat/sindri-ui-u9a0)
- Run 2 fixed at 55d48d42d (16 items: 13 folded, 1 fixed, 1 follow-up, 1 owner re-check). Run 3 (342.json; run2 in 342.run2.json): 0 blocking, 2 issues (live re-check, now DONE; stale PR body, mine to rewrite), 1 suggestion and 2 nits (ContextAnswers.cs:58 not-served answer, :15 order-independent match, sindri-ui.md:800 positional ContextPorts), with the builder folding them, test/docs only.
- Step-9 live re-check: PASS WITH NOTE at 55d48d42d, in proof/sindri-ui-u9a0/evidence-recheck.md. The note is the shell-wide startup focus, which is not this PR.
- Next: the builder reports a head after merging 9990ace and the gate → rewrite the PR body (old in scratchpad/pr342-body-old.md: fix line 28's wrong claim — the guard is the test Every_verb_the_app_can_send_on_the_shared_review_route_is_a_non_protected_catalog_row on closed DeskVerb; drop "empty seam hosts"; new gate; recheck; review runs 1-3) via gh api PATCH → `gh pr ready 342` → `ready #342 <sha>` to the meta.

## #345 answer-child (aw-wt/sindri-ui-answer-child)
- All 9 run-1 issues committed (91bf3c446, 8bf1a3d66), format fix f7b9d777b pushed; 345.json: 11 issues fixed, 9 follow-up, 4 folded, 3 declined.
- Gate on main 34c91d3 merge: build 0 warnings, Ui 2290/0 (10 skipped), integration 599 in 1m31 local, Rendering 21, Arch 126, format/deps/rules ok.
- Merged 9990ace at 0ddfd6b63 (NOT pushed yet); gate re-running (scratchpad/gate-345b.log).
- Next: push; review run 2 (copy 345.json to 345.run1.json first); PR body; ready. Proof is still PARTIAL (engine P7 gap).
- Builder agent for #345 is NOT resumed (I did its last steps).

## U5' (aw-wt/sindri-ui-u5p): builder resumed 01:34. Mid-proof on Xvfb :95; 3 uncommitted files (a fix and a test). It must merge 9990ace. Bin copies under proof/sindri-ui-u5p/raw/bin-* break the rule and need removing after the proof.
## Motion and focus fix (aw-wt/sindri-ui-motion-poll, fix/sindri-ui-motion-poll): a builder started 01:35. GnomeMotion crash when cwd is deleted, plus startup-page initial focus. Draft PR, then review.
## U11a: a planning agent is writing plans/sindri-ui/u11a-plan.md and proof/sindri-ui-u11a/verification-plan.md (CO-3 is merged, so U11a jumps the queue). Then builders per its parallel split.
## Temp leftovers: /tmp/aw-sindri-u9a0rc-146715 (29 MB re-check home), which needs a recursive delete (hook-blocked).
## U11a update (01:50)
- The plan is done: plans/sindri-ui/u11a-plan.md and proof/sindri-ui-u11a/verification-plan.md. Q1–Q10 are provisional defaults; tell the meta.
- Builder A runs T0 (contract and seams) in aw-wt/sindri-ui-u11a on feat/sindri-ui-u11a; its digest is digests/sindri-ui-u11a-t0.md.
- Next, when a builder slot frees (cap 4): builder C does the fixture verb `sessions up|ask|end|back|disable|enable|down` (bash, can run alongside T0). After T0: A does T1, T2 and T6, B does T3 and T4, C does T5. A merges them into one PR.
- Blocked: the Focus send (CO-4a, U11a-f later), recipe groups (CA-6/P10a), the live preview (CO-7b), asking on Home (owner Q3), and the aw:fleet hook (CO-1).
## #345 update (~02:00)
- Merge of 9990ace pushed at 0ddfd6b63. Gate green: build 0 warnings, Ui 2290/0 (10 skipped, 1m35 whole project), Rendering 21, Arch 126, format/deps/rules ok.
- Review run 2 is running (run 1 saved to 345.run1.json).
## ~02:40 update
- U5' is draft PR **#351**, head 0aaa136d4, main 9990ace merged.
  - Gate: build 0 warnings, Ui 1727 unit + 597 integration with 0 failed, Rendering 21, Arch 126, integration 1m34 local.
  - Proof PASS, proof/sindri-ui-u5p/evidence.md; one defect fixed live (ff64d208a).
  - Review run 1 is running and writes 351.json.
  - Bin copies to delete (rm -r is hook-blocked; ask the owner): proof/sindri-ui-u5p/raw/bin-host and raw/bin-ui.
- #345: the run-2 issue (a11y names) is fixed locally, UNCOMMITTED. The labels AnswerInChildOpensName and AnswerInChildPanelName, the ServedChild helper in ExpandedStateViewModel, and the wf alias rename are not committed; tests went red first, and the green run is in progress. The run-2 resolutions are in 345.json. Next: commit, push, the full gate, a narrow review run 3, the PR body, ready.
- U11a builder C is on the fixture verb in aw-wt/sindri-ui-u11a-fixture (feat/sindri-ui-u11a-fixture). Builder A is mid-T0.
- U11a fixture verb DONE: feat/sindri-ui-u11a-fixture @ f4812aa3b (224 fixture tests ok). It exports AW_SESSIONS_PROC_ROOT, AW_SESSIONS_CLAUDE_DIR, AW_FLEET_DIR and AW_LAUNCHER_CONFIG. Builder A merges this branch into feat/sindri-ui-u11a.
  - GAP for the U11a proof: Sindri reads teammate-names.jsonl from the real $AW_STATE_DIR (TeamNameFiles(settings.StateDirectory)), which could show the owner's lane titles. The proof must run serve with a temp AW_STATE_DIR, or crop the screenshots to fixture rows; check that before shooting.
## ~03:00 update
- #351 review run 1: 1 blocking (no IFilePicker contract base), 19 issues, 17 suggestions, 12 nits; in 351.json. The U5' builder is resumed to fix all of them, then gate, a partial re-proof and the body.
  - Asked api (sindri-api) whether fact:item may carry {"id","repo"} (the workflow.item recipe's claim reads fact:item.repo); the builder builds the superset in the meantime.
- #345: review run 3 has 0 blocking and 0 issues (loop done). The folds for test-r3-0 and nit-r3-1 are UNCOMMITTED in the worktree (the ExpandedChildAnswerTests assert and the doc). Resolutions are written in 345.json.
  - Gate at 7fec6d594 is running (scratchpad/gate-345c.log).
  - Next: commit the folds, run ExpandedChildAnswerTests and format, push, write the PR body (review runs 1–3, gate, PARTIAL proof with the engine P7 gap), gh pr ready 345, then send `ready #345 <sha>`.
## ~03:30
- **#342 READY**: `ready #342 75eb9371b` sent to the meta; PR body rewritten, out of draft, 342.json run 3 all resolved. If the meta's gate bounces, fix in aw-wt/sindri-ui-u9a0. After the merge: U9a, U9c and U9d builders per u9-plan §4, and `dotnet clean` the worktree.
- #345 gate at 7fec6d594 so far: build 0, Ui 2293/0, integration 599 in 1m33, Rendering 21; arch/format pending.
- **#345 READY**: `ready #345 01a8e4e08` sent; body rewritten, out of draft. If the meta's gate bounces, fix in aw-wt/sindri-ui-answer-child. Follow-up: a live re-proof after engine P7.
- **#356** (motion crash and startup focus): draft, head 3dfd4b74e, gate green (Ui 2253/0, Rend 21, Arch 126). Review run 1 is running (356.json).
  - After #342 merges: merge main and point StartupFocusTests' Context case at AnchorBox (today it asserts the page region, since AnchorBox comes with #342).
  - F6_cycles test changed to start from wherever focus is.
## U11a (~04:30)
- T0 is done at 57902dabd on feat/sindri-ui-u11a (gate green; integration 66.8 s). Seams are in digests/sindri-ui-u11a-t0.md.
- Builders now:
  - A: T1, T2, T6 in aw-wt/sindri-ui-u11a; then merges B and C, merges main, runs the gate, the proof on Xvfb :94 (with a temp AW_STATE_DIR or cropping), and a draft PR.
  - B: T3/T4 sheet in aw-wt/sindri-ui-u11a-sheet (feat/sindri-ui-u11a-sheet).
  - C: T5 counts/tray in aw-wt/sindri-ui-u11a-fixture.
  - The 4th slot is the U5' (#351) fix builder.
- Gotcha: another session writes scratchpad/gate.sh, so builders use prefixed script names.
- #356 review run 1: 0 blocking, 1 issue, 5 suggestions, 2 nits (356.json). QUEUED for the next free builder slot (resume its builder, the motion-poll agent).
  - Issue cor-0: HasUsableFocus counts the window itself (ShellWindow.axaml.cs:267), so OnOpened may do nothing on a real desktop. Treat window focus as no focus, then one live relaunch check on Xvfb.
  - After #342 merges: OnOpened also runs FocusContextAnchor (a helper shared with OnPageChanged); StartupFocusTests' Context case asserts AnchorBox.
  - Fold: F6 test exact order [Sheet, TopBar, Page, Sheet]; motion tests for a first-read throw and recovery; a no-steal test. Nits: Win32Exception qualification and the duplicated filter. Overlapping ticks are pre-existing; follow-up.
- U11a C (T5) DONE at 0591deafa on feat/sindri-ui-u11a-fixture; gate green (Ui 2299/0, integration 69 s). A got the merge notes; the count live region is A's.
- #356 builder resumed on the run-1 fixes (cor-0 window focus, plus a live relaunch check on Xvfb :93 and the folds). The AnchorBox part waits for #342.
- Context's #359 (S-LB1b subgraph) shape: replied OK with 3 non-blocking asks: conflict decisions at depth 0 and coverage vs empty; a real mixed-kind post-packet-set; optional counts.neighbors. Also asked whether the session-scoping difference is intended. The UI plan: one SubgraphView over the shared LensBands. The U7 POST binding waits for the meta.
- #356 review run 2: 0 blocking, 1 issue (cor-4: skip the landing on IsSheetOpen, not IsSheetShown), 2 nits; builder resumed to fix. Then a narrow run 3 and ready.
- #356 review loop DONE: run 3 had 0 blocking, 0 issues, 1 nit (cor-6: FocusOpenedSheet's HasUsableFocus at ShellWindow.axaml.cs:244/269 should also treat window focus as none; one helper shared with OnOpened). Head 607c31041, gate green.
  - HOLD ready until #342 merges. Then resume the #356 builder: merge main; OnOpened runs FocusContextAnchor through a helper shared with OnPageChanged; StartupFocusTests' Context case asserts AnchorBox; fold cor-6; gate; update the body; mark ready; `ready #356 <sha>`.
- U11a B (T3/T4 sheet) DONE at 3a1c707b1 on feat/sindri-ui-u11a-sheet; gate green (integration 84 s). A has the merge notes: ISessionRef on rows; SheetName to the plan's form; resume Copy on the ended rows is A's. A integrates B and C after T1/T2/T6.
- #351 run-1 fixes DONE at 619037a53. Gate: Ui 2367/0, integration 1m27, Rend 21, Arch 126. Live proof pass 3 at 67fb00573. 351.json: 20 folded, 7 declined, 2 follow-up.
  - Review run 2 is running (351.run1.json saved by the reviewer). It checks the public IFilePicker/PickedFile and whether the tests written after the code are vacuous.
  - Still to delete: proof/sindri-ui-u5p/raw/bin-host and raw/bin-ui (needs the owner's OK).
- #351 review run 2: 0 blocking, 4 issues (live region pattern, click-Start focus loss, a vacuous accepted-start test, no live proof of {id,repo}), plus 9 run-1 items partly fixed. The builder is resumed on all of it (red first this time). Public IFilePicker/PickedFile judged OK.
- #351 run-2 fixes DONE at b8d8ef928 (last code commit 595c1982f). Gate: Ui 2386/0, integration 1m24, Rend 21, Arch 126. Proof pass 4 includes the {id,repo} sha check. Review run 3 is running.
  - a11y-r2-3 is declined until the owner's VoiceOver pass (a refusal that moves focus being announced too): mention it to the owner.
- **#351 READY**: `ready #351 b8d8ef928` sent. Run 3 had 0 and 0; its 7 suggestions and nits are follow-ups (handoff list). If a bounce or conflict comes after #342/#345: merge in aw-wt/sindri-ui-u5p and re-send.
- Ready queue now: #342, #345, #351. #356 is held for #342. U11a A is still on T1/T2/T6.
- Context #365 (S-CL code lens): replied OK, with asks for goldens capped.json and deadline-152.json and a fix to the PR body's mapCommit claim. U9d reuse list: LensEdge, Coverage*, Searched*, SymbolsView; CodeFile gains TestedBy; new records CodeTest/CodeThing/CodeDataFile/counts/truncated/lexical; new ContextWords sets.
- Context #359 answers (head 198606666): bands.product.counts gains optional neighbors and edges (lens goldens too, additive; the UI ignores unmapped members, so no break). post-packet-set is now mixed. Conflict decisions are summary-only unless their keys are in nodes[] or widened. Session scoping is intended. For U9a/U9b: add Neighbors/Edges to ProductCounts for "first 100 of n"; the conflict card shows the summary when its decisions aren't in the bands.
- Meta: DISK OK (60 GB). Keep #342/#345/#351 as sent (the train merges main). #342 failed on a flaky Onyx port test and re-gates in train 21 with #345; #351 follows. Do NOT bind U7 to the subgraph POST: the owner is weighing the HTTP QUERY method.
