# U0 plan — theme and shell (UI spec §18 U0)

Branch: `feat/sindri-ui-u0` (stacked on `feat/sindri-s1b-ui-shell` @ 3e5eb8a2). Main worktree:
`.claude/worktrees/sindri-ui-u0` (the integrator's). Each unit works in its own worktree under `.claude/worktrees/`,
on `feat/sindri-ui-u0-<unit>` branched from `feat/sindri-ui-u0`, and the integrator merges units in.

Read first: UI spec (on origin/main: `git show origin/main:docs/superpowers/specs/2026-10-11-sindri-ui-design.md`)
§1, §2 rules, §3.2, §3.3, §4.1, §5 (tray table only), §8 (only F6/Esc/⌘, for U0), §9.1 (logging, TimeProvider),
§11, §12.3, §12.4, §14, §15, §18 U0, §18.2, §19; `docs/csharp/sindri-ui.md`; `.agents/rules/csharp.md` (from
origin/main — it's newer than this branch) and `.agents/rules/csharp-testing.md`; AGENTS.md. Mockups:
`git show origin/main:docs/superpowers/specs/2026-10-11-sindri-ui-design/mockups/home.html` and `states.html`,
`prototype.html`.

Rules for every unit: TDD (red view-model test first, committed red, then green; then the view; then a headless view
test with key frames, using S1b's `HeadlessUnitTestSession` workaround). View models carry no Avalonia types and hold
no logic beyond mapping (spec §2 "No logic"). Compiled bindings only. No hex colour outside `Theme/`. Every control has
`AutomationProperties.Name`. Every timing rule on an injected `TimeProvider`. Builds/tests only through
`~/.agentic-workflow/bin-r1b/heavy <cmd>` (two slots box-wide). `dotnet build -warnaserror` zero warnings;
`dotnet format --verify-no-changes` clean; PublicAPI lines from RS0016. Commits `type: description`, atomic, ending with
the Co-Authored-By and Claude-Session lines. Never touch the real `$AW_SINDRI_HOME`, setup.sh, providers, MCP.
Never push; the lead pushes.

## Unit C — contract (runs alone, first)
Owns every hotspot for U0:
1. `Theme/Tokens.axaml`: every §14.1 colour as `Color.<Key>` + `Brush.<Key>`; §14.2 type ramp as `TextBlock` style
   classes (`page-title`, `detail-title`, `body`, `body-strong`, `caption`, `eyebrow`, `button`, `mono`, `kbd`) with a
   `Double` text-scale resource; §14.3 spacing/radius/size/motion keys. JetBrains Mono embedded with its OFL file
   (check licence/packaging; if adding a font asset is a problem, stop and report).
2. `Theme/FluentOverrides.axaml`: §14.4 palette map + control overrides (button backgrounds, `Radius.MD`, focus adorner
   = 2 px `PrimaryHover` #828fff full opacity offset 2 px, thin scroll bars, tooltip on Surface3, no shadows).
3. Move S1b's inline brushes out of `SindriApp.axaml` into the tokens; merge both dictionaries there.
4. A contrast test (unit lane) over every §11.1 pair, computed from the token resources' values (WCAG relative
   luminance), asserting the ratios/thresholds in §11.1 (and that `InkTertiary` is only used for disabled text if
   checkable). Plus a test that no `.axaml` outside `Theme/` contains a `#rrggbb` literal.
5. X47: edit `docs/sindri/DESIGN.md` — focus ring 2 px `#828fff` at full opacity (Elevation & Depth level 4), button
   primary hover `#535fc6` (pressed `#4c57be`), and an `Error` `#eb5757` token, each with the §11.1 reason. Touch
   nothing else in DESIGN.md.
6. Contracts for the other units (interfaces + records + red tests, no implementation):
   - `ViewModels/Shell/ShellViewModel` surface: pages (Home, Queue, Decisions, Services, Health, Settings; Later ▾ with
     Agents/Repos/Today/Automatic work "not served yet"), current page, `Navigate(DeepLink)`, the window state
     (one of §4.1's pre-API states: VersionMismatch, NotRunning, Connecting, Unreadable, Stale, Normal), health pill
     text, detail sheet open/closed + edge-tab subject, attention band visible (always false in U0).
   - `Platform/IMotionPreference` (`bool ReduceMotion`, change event), `Platform/ISingleInstance` (claim, or send
     "focus"/deep link to the running instance; the message only navigates), `Platform/ITray` (states: neutral,
     not-running, stale; Open Sindri, Quit), `Platform/IPreferencesStore` and the `UiPreferences` record (§12.3:
     version, window bounds, sheet width, last page, seen ids, palette history, text scale, reduce motion) with
     clamp-on-read.
   - `Labels` resource (§12.4 app-owned copy for U0: Not running reasons, "Connecting…", version mismatch sentence,
     "Sindri not responding: run `sindri doctor`", "stale", "updated Ns ago", tray tooltips for neutral/not-running/stale).
   - `ISindriApi` extension for §15: headers `X-Sindri-Caller: human`, `X-Sindri-Client: ui`; an optional
     `apiVersion` on the status record (absent today → no mismatch; the fake can serve a different one).
   - Fakes in `tests/Aw.Sindri.Ui.Testing` for each new port, each with a contract test base.
7. Update `docs/csharp/sindri-ui.md` with the U0 shape (theme files, ports, preferences path) — short.

## Unit A — shell views (after C)
`Views/Shell*`, `ViewModels/Shell*`: §3.2 window (top bar 48 px, segmented nav with counts placeholders,
"⌘K Search or run a command" field as a non-functional placeholder until U1, health pill opening Health), Home's
panel frames (Needs you / Running and With you / Queued / Sindri health strip) each showing "not served yet" text from
Labels since their routes arrive later, detail sheet frame (520 px, 420–760, edge tab, Esc closes, covers below
1100 px), Health page = S1b's status card content, §4.1 state panels (Version mismatch full panel, Not running S1b
panel, Connecting skeleton first load only, Unreadable banner keeping last data marked stale, Stale after 30 s with no
event/ping). `MainWindow` replaced by the shell (§15). F6 cycles top bar/page/sheet. Key frames for every pre-API
state; window min 900×600; size/sheet width/last page remembered via IPreferencesStore.

## Unit B — platform (after C, parallel with A)
`Aw.Sindri.Ui.Desktop` adapters: single instance (a local channel in the temp-home-scoped path, e.g. a Unix socket
or named pipe under `$AW_SINDRI_HOME/ui/`; second launch sends "focus" then exits; message only navigates, never
acts; 0600/owner-only), close-to-tray with Avalonia `TrayIcon` (neutral, not-running, stale icons + tooltips; menu
Open Sindri / Quit; tray labels escape `_`), `IMotionPreference` adapters (macOS `NSWorkspace
accessibilityDisplayShouldReduceMotion` via ObjC interop, Windows `SPI_GETCLIENTAREAANIMATION`, GNOME
`enable-animations` best effort; AOT-safe), the preferences file store (`$AW_SINDRI_HOME/ui/preferences.json`, 0600,
temp + rename, clamp, reset when malformed), and the head's composition. NativeAOT publish must still pass
(`bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh`).

## Unit D — settings + LiveSetting spike (after C, parallel)
`Views/Settings*`, `ViewModels/Settings*`: ⌘, opens Settings: Reduce motion switch, text scale (0.9–1.3). Motion
tokens become 0 when reduce motion is on (one place: a resource the shell swaps). LiveSetting spike: a doc note in
`docs/csharp/sindri-ui.md` recording whether Avalonia 12.1.3's `AutomationProperties.LiveSetting` reaches VoiceOver on
macOS (try it; record the finding and the method; owner's manual VoiceOver pass confirms).
