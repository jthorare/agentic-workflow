# G1 confirm controls (setup slice 1) digest
Worktree /home/thor/repos/aw-wt/sindri-ui-confirm-controls, branch feat/sindri-ui-confirm-controls (main a70edd3 merged in, #519 included).
Commits: red 1f9cab288 (tests + stubs, compiles, fails), green 669910de6.
Done: AppConfirm (guard, readiness, terminal fallback), HoldButtonViewModel, TypedConfirmBoxViewModel (ViewModels/Controls), HoldButton (UserControl, code only) and TypedConfirmBox (axaml) in Views/Controls, Color.AttentionFill #624328 + Brush mirror, TokenTests and ContrastTests pins (Ink on fill is 8.4, spec said ~9), ApproveAllViewModel rebuilt on Hold and TypedBox children (public surface kept), dialog view uses the controls (PartName/PartPrefix keep "ApproveHold"/"ApproveTyped*" names).
Gate so far: build -warnaserror ok, Ui unit lane 2355/0 failed, scoped format + IDE0001 clean. Integration/Rendering/Arch running.
Left: PR open; mismatch announcement is on press (not a pause debounce); no SND-SETUP-003 "Hold for the full time" text (dialog keeps served error); HoldButton hosts must hook Held and set Gate.
