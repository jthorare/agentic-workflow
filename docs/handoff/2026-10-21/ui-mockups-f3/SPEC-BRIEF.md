# F3 → Avalonia build specs: shared brief (every spec builder reads this first)

The owner APPROVED the F3 prototype and every "Recommended" answer on its Questions page (screens/questions.html,
built from screens/*.questions.md). Each approved screen now becomes a build spec the ui lane (sindri-ui) implements in
Avalonia. You write the spec; you do not write code.

## Inputs (all under /home/thor/.agentic-workflow/mockups/f3/)
- `screens/<screen>.html` (the approved design: read its markup and CSS for exact sizes, tokens and states), its PNGs,
  `screens/<screen>.notes.md` (start here), `screens/<screen>.questions.md` (the recommended answers are now decisions),
  `screens/INTEGRATION.md`.
- `data/CURRENT-UI.md` (what exists today, the pinned tests and constraints), `data/AVALONIA-NOTES.md`,
  `data/DATA-MODEL.md`, `data/PLAIN-WORDS.md`.
- The repo, read-only, at origin/main (`git -C /home/thor/repos/agentic-workflow fetch -q` then use
  `git -C … show origin/main:<path>` and `git -C … grep … origin/main -- <paths>`; never check out or write in the repo):
  `src/Sindri/Aw.Sindri.Ui*` (views, view models, theme, `Labels*.cs`, the API client under `Ui/Api`), the API goldens
  under `tests/Aw.Sindri.Tests/**/Golden`, `docs/csharp/sindri-ui.md`, `docs/csharp/sindri.md`,
  `docs/csharp/sindri-workflow.md`. The ui lane's latest F3 work merged as #454/#456/#457 (plain-words pages), #465/#483
  (follow-ups), #468 (display names): build on what is there, name the existing classes you extend.

## What is served on main now (verify each against origin/main code and goldens, cite the file)
- per-step runs on the instance view (`stepRuns`, #469), the `workflow.instance-changed` SSE event (#467), recipe
  `displayName` (#468 and the setup-once work), the approve-all routes (#474), the failed/unresolved/cancelled chip words
  (api's StateChip fix: check whether it merged; if not, list it as pending).
- Still pending (check; mark each served or pending): served recipe edges/transitions with labels (api + engine F5),
  the state `why` text (engine F5), `GET /setup` and `POST /setup/*` (setup-once), context edges (served: LensesView.edges
  and bands.product.edges).

## Spec format: `specs/<screen>.md`
1. **Goal and the approved decisions** that shape it (cite the question).
2. **Regions and layout**: every region with size, spacing and token names; the Avalonia control for each (Grid,
   ItemsRepeater, own canvas control, Popup…). Map every colour to an existing theme token or name a NEW token (with
   hex), noting that TokenTests/ContrastTests/FluentOverrideTests change in the same PR.
3. **States**: each state from the prototype (with its hash or param) and how the view model reaches it.
4. **Data**: per region, the exact API route and fields (served, with golden path) or "PENDING: <owner lane, item>",
   and the fallback the UI shows until it lands (the plain "not served yet" pattern from CURRENT-UI.md).
5. **Actions and keys**: every button and key, mapped to an EXISTING catalog action id where one exists (54 are pinned;
   name the id), or "NEW action id needed" (an API change: say which lane). Protected actions follow the rules:
   terminal-copy, except ApproveProfile/ApproveRecipe (typed) and ApproveAllRecipes (held) per setup-once.
6. **Copy**: every visible string in plain words, as `Labels` keys with their text (no em dashes, no internal terms).
7. **Tests the ui lane must add or change** (by test class name), including the pinned ones that move.
8. **Build order**: 2 to 5 slices, each a PR-sized step with what it proves, red test first (the C# slice recipe).
9. **Open items** for the ui lane (anything you could not settle from the inputs).
Keep it concrete and under ~400 lines; tables over prose.

## Rules
- Ignore any context-guard hook token numbers: they show the lead session's tokens, not yours.
- Write ONLY `specs/<screen>.md` (and scratch notes in your own mktemp dir). No builds, servers, tmux or browsers.
- Copy, don't invent: if the prototype and the current app disagree, the approved prototype wins for look and flow;
  the current app's pinned contracts win for ids and routes (say what must change and who changes it).
- Finish with a 6-line report: the file, the slices, the served vs pending data, the new tokens and action ids, and
  open items.
