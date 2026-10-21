# PR #215 T0 builder handoff (worktree .claude/worktrees/sindri-ui-r8, branch feat/sindri-ui-r8-contract)
Done: merge origin/main 940237b9 (3251b807); (a) chords + deep links red 80fe569c, green 5dcb8549 (pushed).
Left: (b) ProtectedAction (lock popover), (c) AttachButton, PR body update, final checks (Aw.Architecture.Tests, heavy format, check-deps ok, sync-rules ok).
Facts found: no IClipboard port and no command-catalog client exist in the UI project (ISindriApi has only Ping/Status/Events); plan is to add public Platform/IClipboard (Task<bool> SetText), tests/Aw.Sindri.Ui.Testing FakeClipboard + contract, internal TopLevelClipboard adapter in Views, Icon.Lock in Theme/Icons.axaml (add "Lock" to ThemeResourcesTests list), ViewModels/Components/ProtectedActionViewModel (ctor: ProtectedAction(Verb,Cli,Subject), IClipboard, TimeProvider), Views/Components/ProtectedActionButton (Button + Popup). Label "Accept · terminal" (glyph drawn as icon); Title "Needs a typed confirmation at a terminal"; Copied 1.5 s via clock timer, live flag first then text, resting text Labels.Sindri; refuse cli with control/bidi chars.
Lint gotchas: IDE0058 (use `_ =`), AW1005 300-line file cap, CA1502 complexity 10, IDE0072 populate switch.

## Update: (c) Attach done (head 8bd43d87, red e9d08931, pushed; PR #215 Attach item ticked)
Files: ViewModels/Components/{AttachControlViewModel,AttachInput,AttachStepKind,AttachAction,AttachMenuItem,AttachChosenEventArgs}.cs, ViewModels/Labels.Attach.cs, Views/Components/AttachButton.axaml(.cs); tests Components/AttachControlTests.cs, Views/AttachButtonTests.cs, NamedControlsTests entry.
Tests: unit 509 (1 expected red GoldenContract), integration 205 (203 pass, 2 skipped); Architecture 117 green; format, check-deps, sync-rules ok.
Gotchas: MA0182 needs `#pragma warning disable MA0182 // reason` on unplaced types; MA0046 wants EventHandler<EventArgs-derived>; test methods can't take internal enums (use bool); MA0041/CA1822 for bound constant props.
Left: (b) ProtectedAction lock popover still not done.

## UPDATE 2026-10-15 (lead): #215 @72db7631, main de83b59d (U1) merged (cc845185)
Done: nav, chords + deep links, Attach, lock popover (ProtectedAction record/VM/Button; Open in an agent = pending "Available with API P10a"; Every_lock_button_has_a_popover tree walk). U1 palette mapped to rev-8 pages (Home, Workflows, Work, Recipes, Context, Agents, System, Settings). IClipboard + catalog client now come from main.
Left: goldens (UI mirror records), client (ISindriApi workflow routes; reads by opaque id, address via ?address=), one instance table, one chip table (StateChip; U3' NeedsYouChip rebinds), fake contract — all wait for #213's green sha (take #213's FactType RecipeInputView, drop our string patch). See plans/sindri-ui/u8-prep-2026-10-15.md (T0 red tests + API answers).
