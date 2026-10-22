# Sindri UI (C#): the app shell

The Sindri app is an [Avalonia UI](https://avaloniaui.net/) desktop app (readiness spec §4a "UI", `.agents/rules/csharp.md`
"The UI package"). Stage S1b ships its shell: one window that shows the control plane's status, the same status
`sindri status` prints, kept live from the event stream, and that says "Not running" with the fix when the control plane
is down. It is its own package, `SindriUi` (tier 4 in `deps.yaml`), and it holds no logic: every verdict comes from
Sindri's handlers (factory addendum §17.27.1). Nothing installs it before the switch.

## Shape

| Project | Holds |
|---|---|
| `src/Sindri/Aw.Sindri.Ui` | The views (`SindriApp`, the shell under `Views/Shell`), the view models, the typed client of Sindri's HTTP API, and the ports for platform services |
| `src/Sindri/Aw.Sindri.Ui.Desktop` | The thin desktop head: `Main`, the composition and one adapter per port. NativeAOT binary `sindri-ui`. No views or view models |

- **No Sindri project reference.** The app reaches Sindri only through its HTTP contract (`docs/csharp/sindri.md`
  "Command layer and API": `GET /sindri/ping`, `GET /sindri/status` and `GET /sindri/events`) and two files under
  Sindri's home ("Files"): the endpoint record it finds the API by (`control.json`) and the per-start human token
  (`auth/human.token`, API spec §6) it sends as `X-Sindri-Token`. `deps.yaml` declares them as the `SindriUi -> Sindri`
  **http** and **file** edges (one file edge covers both files); `node scripts/check-deps.mjs` finds them in the app's
  C# projects (a literal starting with Sindri's route prefix `/sindri/`; the literals `"sindri-app"` with
  `"control.json"`, and with `"auth"` and `"human.token"`) and fails on an undeclared or stale one. The head's path
  rule (`ControlFilePath.From` and `.TokenFrom`) follows the documented one in `docs/csharp/sindri.md` "Files".
- **Compiled bindings only.** `AvaloniaUseCompiledBindingsByDefault=true`, `x:DataType` on the window and every
  template, no `ReflectionBinding`, so the head publishes under NativeAOT.
- **View models carry no Avalonia types** (plain C#, `INotifyPropertyChanged`), so they test in the unit lane.
- **Hand composition, no container.** The app has a handful of view models and one client; the head builds its adapters
  and hands them to `SindriApp.Create(platform, clock)`, which builds the shell view model and the API client (a loopback
  `HttpClient` that never uses a proxy). A service locator outside a Host would fail `ARCH-DI-001`.
- **Packages.** MIT Avalonia packages only, all `[12.1.3]`: `Avalonia`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`
  (the library), `Avalonia.Desktop` (the head), `Avalonia.Headless` and `Avalonia.Skia` (tests).

## U0: theme, ports and preferences (UI spec §14, §15, §18 U0)

U0 turns the S1b window into the shell of the UI spec (`docs/superpowers/specs/2026-10-11-sindri-ui-design.md`). Its
contract unit added:

| Part | Where | What |
|---|---|---|
| Tokens | `Aw.Sindri.Ui/Theme/Tokens.axaml` | Every §14.1 colour as `Color.<Key>` and `Brush.<Key>`; the §14.2 ramp as `Type.<Step>.Size` and `.LineHeight` with one `Type.Scale`; `Space.*` (Double and `.Thickness`), `Radius.*`, `Size.*`, `Motion.*`, `Easing.Standard`; `Font.Text` (Inter) and `Font.Mono` |
| Type ramp | `Theme/Typography.axaml` | `TextBlock` classes `page-title`, `detail-title`, `body`, `body-strong`, `caption`, `eyebrow`, `button`, `mono`, `kbd` (a Styles file: a ResourceDictionary can't hold styles). `TextScale.Apply` scales them 0.9–1.3 from the token bases |
| Fluent | `Theme/FluentOverrides.axaml` | The §14.4 dark palette, button and tooltip brushes, `ControlCornerRadius` = `Radius.MD`, and the §11.1 focus ring: 2 px `PrimaryHover` at full opacity, 2 px outside the control, with the adorner layer's clip off (it clips an adorner to its control's bounds, which hid the ring); a full-width region (`focus-region`: the top bar, the page, the sheet) draws it 2 px inside instead. `Aw.Sindri.Ui.Rendering.Tests/Views/FocusRingTests` checks it as rendered (that project draws with Skia; the main test project does not) on each control the keyboard reaches. No shadows |
| Mono font | `Theme/Fonts/` | JetBrains Mono 2.304 Regular and Medium, unmodified, with `OFL.txt` (SIL OFL 1.1, no Reserved Font Name), embedded as `AvaloniaResource`. From the release asset `JetBrainsMono-2.304.zip` (SHA-256 `6f6376c6ed2960ea8a963cd7387ec9d76e3f629125bc33d1fdcd7eb7012f7bbf`) |

`SindriApp.axaml` merges the tokens and includes the two style files. **No hex colour outside `Theme/`**, and
`InkTertiary` only for disabled text: the unit lane checks both, recomputes every §11.1 contrast pair from the token
values, and holds every Fluent value equal to its token (`tests/Aw.Sindri.Ui.Tests/Theme/`). `docs/sindri/DESIGN.md`
takes the ring, the darker primary hover and pressed fills, and `semantic-error` (X22).

**Ports** (`Aw.Sindri.Ui/Platform`, public because heads implement them; each has a fake and a contract base in
`tests/Aw.Sindri.Ui.Testing` that the head's adapter binds to):

| Port | Desktop adapter (Unit B) | Contract base |
|---|---|---|
| `IMotionPreference` (`ReduceMotion`, `Changed`) | macOS `NSWorkspace`, Windows `SPI_GETCLIENTAREAANIMATION`, GNOME `enable-animations`; bound over a drivable OS seam | `MotionPreferenceContract` |
| `ISingleInstance` (`TryClaim`, `Send`, `Messages`, `Retire`, `HolderProcessId`; `InstanceMessages.Focus`, `MaxLength` 512) | A local channel only the user can open, under `$AW_SINDRI_HOME/ui/`. A message only navigates: the app reads it as a `DeepLink` and ignores anything else. `Send` is true only once the running instance has **handled** the message, never because a lock is held: a holder that doesn't read gives no answer, and the message isn't handed over later. Handling is one decision, `InstanceMessage.TryAct()`: the app claims it on the UI thread just before acting (or the reader coming back for the next message claims it), the first claim wins and is the sender's answer, and the message is withdrawn (`InstanceMessage.Withdrawn` cancelled, `TryAct()` false) as soon as the sender has been answered or has given up, so a message acted on is never answered "not handled" and one whose wait ended is never acted on late (`SingleInstanceContract.A_message_acted_on_is_acknowledged_and_then_withdrawn`) | `SingleInstanceContract` |
| `ITray` (`Show(TrayView)`, `Shown`, `OpenRequested`, `QuitRequested`; `TrayState` Neutral, NotRunning, Stale) | Avalonia `TrayIcon`; menu labels escape `_` | `TrayContract` |
| `IClipboard` (`SetText`: true once the platform holds it, false when it refused) | `Views/AvaloniaClipboard` over the window's `TopLevel.Clipboard`: one adapter every head shares (Avalonia's clipboard belongs to the window), so it is not in `SindriPlatform`; the view models take it as a required constructor argument (no null object) | `ClipboardContract` (fake; the adapter under headless, which accepts a write but keeps nothing, so only the acknowledgement and the refusal are checked there) |
| `IPreferencesStore` (`Location`, `Read`/`Write` of the bytes, like the endpoint record) | `$AW_SINDRI_HOME/ui/preferences.json`, 0600, temp file and rename | `PreferencesStoreContract` |

A head hands its adapters to `SindriApp.Create(SindriPlatform, TimeProvider)` (`SindriPlatform` bundles them, so a
new port doesn't change the signature).

**The desktop adapters** (`Aw.Sindri.Ui.Desktop`, Unit B) keep the app's own files in a `ui/` directory of Sindri's home
(the directory `ControlFilePath` resolves; `UiStatePaths`), created 0700, every file in it 0600 (`OwnerOnly`; on
Windows the profile's ACLs do it):

| Adapter | How |
|---|---|
| `FilePreferencesStore` | `ui/preferences.json`; a write goes to a new 0600 temp file beside it, flushed to disk, then renamed over it |
| `SocketSingleInstance` | The claim is an exclusive lock on `ui/instance.lock` (the OS drops it when the process dies, so a crash leaves no claim); its holder records its process id in `ui/instance.pid` (0600; removed on exit), then removes a crashed instance's `ui/instance.sock` and listens there (a Unix domain socket, 0600; AF_UNIX on Windows 10 and later too; when the path is longer than 100 bytes (under sun_path's 104 on macOS and 108 on Linux), as in a deep temp home, the socket goes to `$TMPDIR/sindri-ui-<hash of the path>/`, 0700). A later launch writes one UTF-8 message ending in a newline, keeps its side open and reads one byte back: 1 once the holder's reader has handled the message (`Views/InstanceListener` reads from the app's framework start-up on: before the window shows it answers `focus` at once and keeps the latest deep link; after, `AppLifetime.Receive` handles each on the UI thread before the next is read), 0 refused. The listener refuses an empty message, one over 512 characters, one that isn't UTF-8 or one without its newline; it waits 1.5 s from a message's arrival for the handling and the sender waits 2 s, so a holder that doesn't handle it (an app whose framework start-up failed or hangs, or a wedged one) gives no answer. A message nothing handled in time, or whose sender has gone, is dropped; one already handed to the app is withdrawn (`InstanceMessage.Withdrawn`), and `Views/InstanceListener` claims it with `InstanceMessage.TryAct()` on the UI thread and acts only when that returns true, so a busy UI thread never acts on it late and an acted-on message is never answered "not handled" |
| `SingleLaunch` | `Program` claims before Avalonia starts; a later launch sends `focus` (or its one argument when it is a deep link, `sindri://` optional) and exits 0 once the running app handled it. With no answer it claims again every 50 ms for up to 3 s (a quitting holder refuses at once and goes within about a second, review run 5): if the holder ended meanwhile, this launch runs the app; otherwise it exits 1 with `sindri-ui: Sindri app is running but not answering (pid N): quit it or kill it` on standard error. Exit codes: 0 handed over (or the app quit), 1 a holder that doesn't answer, 2 the app failed (SND-UI-004) |
| `AvaloniaTray` | Avalonia's `TrayIcon`, created on the first `Show` and registered with the app through `Views/AppTrayIcons` (Avalonia makes the native icon only for an icon in the Application's `TrayIcon.Icons`); `TrayMarks` draws Sindri's mark in the theme's Ink, filled (Neutral), hollow (Stale) and hollow struck through (NotRunning), as macOS template images; menu labels double each `_` |
| `PolledMotionPreference` | The OS reader at start and every 2 s on the injected `TimeProvider`: macOS `NSWorkspace.accessibilityDisplayShouldReduceMotion` through `objc_msgSend` (`MacMotion`), Windows `SPI_GETCLIENTAREAANIMATION` (`WindowsMotion`), GNOME `gsettings get org.gnome.desktop.interface enable-animations` (`GnomeMotion`; no gsettings, a failure or a hang past 3 s reads as off) |

`Views/AppLifetime` (in the app, so every desktop head shares it) is the window's life around these: closing the
window hides it (the app stays in the tray; an OS or app shutdown still closes it), the tray's Open brings it
forward and Quit ends the app (`ShutdownMode.OnExplicitShutdown`) once the window's bounds are recorded and the
preferences queue is written (at most `AppLifetime.SettleWithin`, 1 s on the injected clock); when the OS ends the app
(⌘Q, the Dock, logging out) `ShutdownRequested` isn't cancelled, which would hold up a logout, but the bounds are
recorded and the UI thread waits up to the same second for the write. The tray follows `ShellState` (Normal: Neutral,
"Sindri · running"; NotRunning: NotRunning; every other state: Stale, "Sindri not responding"), and an instance message
is either `focus` (bring forward) or a `DeepLink` (bring forward, then the shell's `Navigate`); anything else is ignored,
and no message runs an action. `SindriApp` starts `Views/InstanceListener` in `OnFrameworkInitializationCompleted`; its
`ShellOpened` (`Views/SindriApp.Lifetime.cs`) builds the `AppLifetime` over the window, the tray, the shell's `Navigate`,
the shutdown and the settle step once the shell window shows, calls its `Start(desktop, shell)`, hands it the listener's messages (`Deliver`, which first applies a deep link kept from
start-up), and disposes it on exit.

**Preferences** (§12.3) are `UiPreferences`: `version` (1), `window` (x, y, width, height), `sheetWidth`, `lastPage`,
`seenIds`, `paletteHistory`, `textScale`, `reduceMotion`, camelCase JSON. A head's store only moves bytes; the app's
`ViewModels/Preferences` (`Load`, `Update`, `Flush`; one instance, built in `SindriApp.Create` and shared by the shell
and Settings, queues every read-change-write behind the one before) parses and writes them through `Api/PreferencesJson`, so clamping is the same
on every head: the window grows to 900 × 600 (bounds that
aren't finite are dropped), the sheet is 420–760, the scale 0.9–1.3, an unknown page reads as Home, and the id lists
drop blanks and repeats and keep their newest 500 and 20. A malformed file, or one of another version, reads as the
defaults.

**Copy** the app owns (§12.4) lives in `ViewModels/Labels.cs`. **Deep links** (§3.3) are `Navigation/DeepLink`.
**The caller headers** `X-Sindri-Caller: human`, `X-Sindri-Client: ui` and `X-Sindri-Token: <the per-start token>`
go on every request (§9.1, API spec §6); never `X-Sindri-Terminal` and never `Origin`. The token comes from the
`IHumanTokenSource` port (the desktop adapter `FileHumanToken` reads `auth/human.token` under the same home as
`control.json`), read at each call like the CLI's, so a restarted control plane's new token is sent with no app
restart. With no token file, or one the platform can't read, the call goes without it (the unreadable file is logged
with its path and the exception's type), and Sindri's 403 says why. The token goes only into its header: never into an
error, the log or the window. The status
carries `apiVersion` from API P1 (`SindriApiClient.AppApiVersion` is 1; absent means no mismatch).

**The shell** (UI spec §3.2, §4.1; `Views/Shell/ShellWindow` over `ViewModels/Shell/ShellViewModel`) replaces S1b's
window. `SindriApp.Create(SindriPlatform, TimeProvider)` builds the view model over the head's endpoint record and
preferences store, restores the preferences (window bounds, sheet width, last page), then shows the window and follows
the control plane; the platform wiring (tray, single instance, close-to-tray) receives the window through the partial
`ShellOpened` hook. The head's `Composition.Compose` passes its `SindriPlatform`; S1b's `Create(records, clock)` is
gone. Every view loads its XAML through the generated `InitializeComponent()` (never `AvaloniaXamlLoader.Load(this)`,
which leaves the `x:Name` fields null; `Views/NamedControlsTests` checks each view's), and `Views/StartupTests` drives
this start-up path the way the head does (`Create`, `Initialize`, `OnFrameworkInitializationCompleted` with a classic
desktop lifetime).

**The UI context.** The app factory (`SindriApp.Create`) installs Avalonia's synchronization context before it builds any view model, because `AppBuilder.Configure` runs it before Avalonia does; `UiContext` throws when it is posted to from another thread with no context, and a test that has pool-thread timers and no UI thread uses `InlineUiContext`.

**Start-up never fails silently.** The start-up and the following run as one task on the UI thread; when it throws,
the app logs `SND-UI-004` through `SindriPlatform.Log` (an `ILogger`; the desktop head's is `Aw.Kernel.Telemetry`'s,
JSON lines on standard error, `AW_LOG_LEVEL`) with the exception's type only (no message, no stack: they can carry paths
and payloads), then ends with exit code 2 (`SindriApp.FailedExitCode`) instead of staying up without a window or tray.
`SingleLaunch` then writes `sindri-ui: failed (SND-UI-004); the log names the cause unless AW_LOG_LEVEL is none`
on standard error. `SingleLaunch` is also the head's boundary: anything that throws before or around the app (composing the
platform, claiming the instance when `ui/` can't be made, a hand-over socket it can't use, ending the instance and the
motion poll after the app quits; `ui/` removed while the app ran counts as already cleaned up) is logged the same way (`SND-UI-004`, the type
only, never the message, which can name the home), with a line that names the exception's type (`sindri-ui: failed (SND-UI-004: IOException)`; neutral words, since the failure can come after a normal run), and exit 2; never an unhandled exception.

| Part | What it shows |
|---|---|
| Top bar (48 px) | Brand; Home · Queue · Decisions · Services (counts empty until their routes); Later ▾ (Agents, Repos, Today, Automatic work, each "not served yet"); the ⌘K field, which opens the palette (U1); the health pill, which opens Health (while Sindri isn't running it is only the state: disabled like the navigation, its word undimmed, since the Not running card is the health state and Health has nothing to show). When they don't all fit (the 900 px minimum, text scale up to 1.3) the bar steps down in `TopBar.axaml.cs`: the ⌘K field folds to its magnifier, then the pill's age drops (the pill's accessible name keeps it), then the brand keeps its mark alone. The pill's word always shows |
| Home panels' bodies | "not served yet", centred in the panel |
| Home | The page title, three frames (Needs you, Running · With you, Queued), each "not served yet", and the Sindri health strip: the health line (§4.8) over the fields the status carries today (`Running · pid · epoch · API v1 · N services · profile <state>`; "Not running" when it isn't), which opens Health on a click or Enter |
| Queue, Decisions, Services, Settings | The title and "not served yet"; Settings is the slot `Views/Settings*` fills (⌘, opens it) |
| Health | S1b's status card (`HealthViewModel`: lease, API, profile, services, last event) |
| Detail sheet | A deep link's subject over the page at the right, 520 px (420–760, dragged by its left edge and remembered); Esc closes it and leaves an edge tab that reopens it; below 1100 px it covers the page with a Back button, focus moves to its heading, and the page (disabled under it) leaves the tab order and F6 until the sheet closes or stops covering |

The pre-API states (`ShellState`, highest first): **version mismatch** (the status's `apiVersion` differs from
`AppApiVersion`: a full-window panel with both versions and "quit both and run" around `sindri` in the command chip, no top bar), **not running**
(S1b's panel and reasons, closing with "Looks again every 2 s", the follower's retry wait), **connecting** (skeleton rows, first load only), **unreadable** (`SND-UI-003`, or Sindri's own 403 after the token retry with its code and detail: a banner with
the code and `sindri doctor`, its parts in a wrap panel so a long reason wraps and the command moves to its own line
rather than past the window's edge at 900 px, as the not-responding banner's does; the page keeps its last data and the pill reads "Unreadable · stale"), **stale** (30 s
without an event, a status or an answered ping, on the injected clock: the pill reads "⌛ stale · updated Ns ago" with
**Refresh** beside it; otherwise "✓ Running · updated Ns ago", re-worded every second). Refresh follows §4.1's command
lifecycle: while it is in flight it is disabled with its label kept and a spinner in its icon's place (still under
reduced motion), "Sending…" is announced after 300 ms through a polite live region (not shown), and a second click does
nothing. Its answer counts only when the follow decided no state while it was in flight, the views are still normal or
stale, and it came from the pid and epoch the follow checked with ping; otherwise it is dropped, so a late answer never
shows Running over Not running or Unreadable. After three failed refreshes the pill keeps "stale" and a banner under
the top bar reads "Sindri not responding: run `sindri doctor`" in full, since the sentence doesn't fit the pill at
900 px. The view models carry no Avalonia types; views bind the app's copy through `ShellText` (compiled bindings
resolve public members only, and `Labels` is internal). F6 / Shift+F6 cycle top bar, page and sheet; on macOS ⌘⌥→ / ⌘⌥← do the same (owner decision, 2026-10-12;
UI spec §8), since with the default "Use F1, F2, etc. keys as standard function keys" off F6 is the Focus key there and
reaches the app only with fn held (fn+F6 and fn+Shift+F6). Ctrl+F6 isn't bound. The window's minimum is 900 × 600.

**Focus never goes nowhere (§11.2).** When the focused control disappears, focus moves to a neighbour: Refresh keeps
focus through its in-flight phase and gets it back when the refresh fails; when Sindri answers and Refresh hides, the
health pill gets it. Reopening the sheet from its edge tab moves focus to the sheet's heading at every width (beside
the page the tab hides as the sheet opens), as opening a covering sheet does. Closing the sheet returns focus to the edge
tab when the tab reopened it, otherwise (a deep link) to the page region. Narrowing the window below 1100 px with the
sheet open moves focus on the page (now disabled under the sheet) to the sheet's heading. A state change that hides the focused control moves focus to its region's neighbour: the pill in
the top bar, the page region in the page (`ShellWindow.KeepFocus`, `TopBar.KeepRefreshFocus`).
The mechanics: the three regions (Page, Sheet, the top bar) are Borders with `AccessibilityView=Control` and
`ControlTypeOverride=Group` (the `focus-region` style) so they have a named peer; the modal cards (shortcuts, palette, create) are Window-typed; `SheetTitle` is a Border typed Text that wraps
the raw `SheetSubjectText`, so its name is the kind noun and the subject. **A page change by a navigation command** (a G-chord,
the palette's Go to, a pointer click on a nav tab) always lands focus on the new page's region wherever focus was (the pill, a nav item, a sheet
beside the page), and `ShellWindow.KeepFocusOrReannounce` does it: Avalonia 12.1.3 has no `ClearFocus`, so when the region
already has focus it pulses `Page.IsEnabled` false and true (focus drops) and focuses it again, which raises the fresh event
the screen reader speaks. From the palette, opened from the region, it raises two (the palette's close hands focus back, then the re-announce); the page changes before the palette closes, so both carry the new page's name. Over a
covering sheet the page is disabled and the usual keep-focus rule applies. ⌘, keeps its own rule (Settings' first control,
once, via `_openingSettings`). The nav tabs are a tabs pattern with automatic activation: Right, Left, Home and End on a focused tab select the neighbouring
tab and focus stays on that tab (`ShellWindow.WatchNavigationKeys`; only a pointer click on a tab moves focus to the region).
Other page changes that are not a navigation command (`LeaveSettings`, the Home health strip, the pill's `GoTo(System)`) take the
same path as a chord; any new one would too unless it says otherwise. Whether the OS screen reader speaks it is not provable headless.

### Settings and reduced motion (§3.3 ⌘,, §11.4, §14.2)

`Views/SettingsView` over `ViewModels/Settings/SettingsViewModel`: the in-app **Reduce motion** switch and **Text size**
(a slider, 90–130 % in 10 % steps, clamped by `UiPreferences.Clamp`). Both are kept in the preferences: a change is
written over whatever else the file holds (read, replace the two fields, write, through the app's one preferences queue, shared with the shell), and a write
the platform refuses leaves the change applied for the session. Motion is reduced when the OS asks
(`IMotionPreference`, re-read on `Changed`, posted back to the UI thread) **or** the switch is on; while the OS asks, the
page says "On in your system settings" and the switch can't turn it off. Settings is local preferences, so it stays
reachable while Sindri is not running: ⌘, shows it over the Not running card, and Esc returns to the card.

The theme is changed in one place each: `Theme/Motion.Apply(resources, reduce)` sets every `Motion.*` duration to 0, or
back to its token value, and `Theme/TextScale.Apply` scales the ramp. `Views/SettingsTheme.For(resources)` is the view
model's apply callback over both. **At start-up** `SindriApp` builds
`new SettingsViewModel(preferences, platform.Motion, SettingsTheme.For(Resources))` (the shared `Preferences` the shell also has) on the UI
thread, awaits `Load` once before the window shows (the stored scale and motion apply before the first frame; an
unreadable preferences file leaves the defaults), puts `new SettingsView { DataContext = settings }` in the shell's
`SettingsSlot`, and disposes it on exit. The shell and Settings each write only their own fields, through the one queue, so neither writes back what it read before the other's change. Stock Fluent controls'
own transitions don't read the `Motion.*` tokens: `SettingsTheme.Follow(window, settings)` keeps the window's
`reduce-motion` class in step with `IsMotionReduced`, and `Theme/FluentOverrides.axaml` turns those transitions off
under it (today the ToggleSwitch knob); another stock control with its own transitions gets its line there.

### The setup checklist in Settings (setup spec §2.4, F3 slice 2)

The first section of `SettingsView` is the six-step checklist over `ViewModels/Setup/SetupViewModel` (`Views/Setup/SetupChecklist`); the local preferences sit below it under "This app".
`SettingsViewModel.Setup` is the shell's `SetupViewModel` (an optional constructor argument: a host without setup shows only the preferences). There is **no Setup page and no card on Home**:
Home gets one 40 px line, "Finish setting up Sindri" with the count (`SetupHomeLink`), while any step is not done or skipped, and Home hides the `setup:*` rows of Needs you meanwhile (the line stands for
them; Needs you elsewhere and the tray keep them). Every way into Settings (the rail, ⌘,, the chord G U, the deep link `setup`, Home's line) goes through `ShellViewModel.ShowPage`, which asks the
checklist to come into view (`SetupViewModel.RevealChecklist`; `SettingsView` brings its first section into view).

`GET /sindri/setup` is read on connect and with every event. The app owns the words: titles by step id (`Labels.SetupTitle`), the line under a title for the step/state pairs the spec has copy
for (else the served `why`; a done row shows the served `detail`), and the five state words To do, Working, Done, Failed, Needs you on `StateChipView` (a skipped step says Skipped; a state this
build doesn't know shows the To do chip with its own word). A row is an `Expander` (`SetupStepRow`): a step that needs the person or failed opens by itself, opening a row never closes another,
and the person's choice survives a read. The progress strip is one 4 px segment per step (`SegmentStrip`: done, working, to do). The row's one action lives in its body: a protected action the app
can't open itself (`ApproveProfile`, `ApproveAllRecipes`) is the existing `ProtectedActionButton` with the catalog's command to copy; `ApproveAllRecipes` and the profile's approval row open in the app
when the shell can; the map shows **Retry** only when it failed, which sends `RefreshContextMap` (`POST /sindri/context/map/refresh`; the client is `ISindriApi.RefreshContextMap`). Retry sends once, the
row shows Working until the next read, and a 409 `SND-JOB-409` (the running job) shows as already building, not as an error. Fallbacks: a 404 says "Setup isn't available in this build yet."; no answer
says "Sindri isn't running" with the launcher command and no Start button. Not built here (slices 1, 3, 4): the typed and held confirmations in their rows, the per-agent list, the Onyx form and the
repo form.

### LiveSetting on macOS (§11.4, §18.2; U0 spike)

**Finding: supported in Avalonia 12.1.3, for a `TextBlock` whose `Text` changes; unconfirmed by ear.** The owner's
VoiceOver pass below confirms or refutes it.

**Method:** read the source at tag `12.1.3` (commit `8eeda4f6`) and checked the shipped binary
(`~/.nuget/packages/avalonia.native/12.1.3/runtimes/osx/native/libAvaloniaNative.dylib`).

- `AutomationProperties.LiveSetting` (`src/Avalonia.Controls/Automation/AutomationProperties.cs`) is read by
  `ControlAutomationPeer.GetLiveSettingCore`. `TextBlockAutomationPeer` raises a Name change when `Text` changes;
  `ControlAutomationPeer` raises none when `AutomationProperties.Name` itself changes, so a bound name on a `Border` or
  panel is **not** announced.
- macOS (`native/Avalonia.Native/src/OSX/automation.mm`): the element reports `AXARIALive` (polite or assertive); on a
  Name change it posts `NSAccessibilityTitleChangedNotification` and, when the live setting isn't Off,
  `raiseLiveRegionChanged`: `AXLiveRegionChanged`, then `NSAccessibilityAnnouncementRequestedNotification` on the
  `AvnWindow` with the new name (Polite = `NSAccessibilityPriorityMedium`, Assertive = `High`). The same shape as
  Windows' UIA `LiveRegionChanged`.
- The dylib imports `_NSAccessibilityAnnouncementRequestedNotification`, `_NSAccessibilityAnnouncementKey` and
  `_NSAccessibilityPostNotificationWithUserInfo`, and holds `AXARIALive`, `AXLiveRegionChanged` and
  `raiseLiveRegionChanged`.
- History: PR #20473 (2026-01) added the attribute ("problems getting VoiceOver to actually handle the events");
  PR #21045 (2026-04) added the announcement. Open issue #22312 (2026-09): setting a live element's name to `""`
  crashes the app (`NSInvalidArgumentException`) once VoiceOver or Accessibility Inspector has read the window.

**Rules for later phases' announcements** ("N now need you", "<id> cleared", §12.4): a dedicated `TextBlock` with
`LiveSetting="Polite"` whose `Text` changes; never set it to `""` (hide it instead); give it no separate
`AutomationProperties.Name` (the peer announces the name, and a second binding may lag the text); only in a top-level
window (popups aren't `AvnWindow`s). Two more from the source: once assistive tech has read the element, every `Text`
change is announced even while it is hidden, so a text that must not be spoken (a resting text, set so the next
announcement is a change again) is set only while `LiveSetting` is Off; and a hidden element has no native element, so
show it a moment before its text changes. Refresh's "Sending…" (`TopBar`'s `RefreshSending`) does both: the region is in
the tree from the click (`!Health.CanRefresh`), is live (`Polite`, through its `live` class) only while
`Health.IsSending`, which turns on before the text becomes "Sending…" and off before it goes back to its resting
"Refresh"; it is zero-width and transparent, so it takes no room and shows nothing. The Unreadable banner, the Not responding banner and the Not running card follow the same rules (WCAG 4.1.3; review run 2): `ShellWindow`'s `UnreadableLive`, `NotRespondingLive` and `NotRunningLive` sit outside the banners, zero-width and transparent, so each is in the tree before its state comes; each is live only while its state holds (`IsUnreadable`, `IsNotRespondingBanner`, `IsNotRunning`), and `ShellViewModel` changes that flag before its announcement ("Unreadable: " or "Not running: " and the reason, or the not-responding sentence), resting at "Sindri" while Off. Two more follow the same rules (review run 5): `MismatchLive` announces the version-mismatch card's sentence, and the card's heading (`VersionMismatchText`, focusable) takes focus when the state is entered, also mid-session, since the card hides the whole shell and leaves no neighbour for `KeepFocus`; `RunningLive` says "Running" (the pill's own label; spec §12 has no recovery sentence) when the state returns to Normal after trouble (Not running, Unreadable, stale or not responding, a mismatch), is silent at the first connect, and rests when trouble comes again (`ShellViewModel.IsRecovered`, `RecoveredAnnouncement`). `CommandOutcomeLive` (review run 5, K2) follows the same rules: it speaks a command the palette started and closed on before its outcome (see "A closed palette" under the palette), flag (`CommandOutcome.IsLive`) then text. If the pass finds it silent, the fallback is a head adapter posting
`NSAccessibilityAnnouncementRequestedNotification` through ObjC interop; no production seam is added before that.

**The owner's VoiceOver pass** (a throwaway probe window: a polite `TextBlock` changing every 3 s, an assertive one, a
`Border` with a bound name and `LiveSetting="Polite"`, and a focused `TextBox`):
1. With focus in the TextBox, VoiceOver speaks each polite change after the current speech, and the assertive one
   interrupts it.
2. With the VoiceOver cursor on the live TextBlock, it still speaks, and nothing is said twice.
3. The Border's name change stays silent (as the source predicts).
4. A change right after launch, before VoiceOver has walked the window, is or isn't spoken (the native element exists
   only once something has read the tree).
5. Accessibility Inspector shows `AXARIALive` on the element and the `AXLiveRegionChanged` and announcement events.
6. The app in the background: is the announcement still spoken?

**Linux (owner question, provisional owner default (2026-10-14 night)).** The Linux real-app proof found that Avalonia's
AT-SPI bridge exposes no live-region attribute and emits no announcement signal, only name changes, so Orca probably speaks
none of the announcements above. macOS VoiceOver is the primary accessibility target; on Linux the announcements are a
known platform limit, tracked for an Avalonia upstream fix or a later AT-SPI workaround. Nothing in U1 depends on it:
every outcome is also shown on screen.

## U1: catalog, headers and freshness (UI spec §18 U1)

U1 gives the app the API's catalog of actions, the headers and token every call carries, and the views' freshness.

**Requests (§9.1, API §6).** Every request sends `X-Sindri-Caller: human` and `X-Sindri-Client: ui`, and
`X-Sindri-Token` read from `<home>/auth/human.token` before each request (a restart mints a new one; a missing file
sends none, so the API answers `SND-AUTH-001`). Never `X-Sindri-Terminal` or `Origin`. After an accepted command the
next view query carries `X-Sindri-After: <sequence>` once (best effort, below). A refusal Sindri explains is that error: a 403 with a `title`,
or any other non-2xx whose ProblemDetails has an SND `title` and a `detail` (a command's 409 `SND-API-010`, for one),
shown as served; anything else stays `SND-UI-003`. The token comes through U0's port (`IHumanTokenSource`, above):
on `SND-AUTH-001` the call re-reads the token and retries once, then a status or ping refusal is the Unreadable banner
with Sindri's code and detail. Every such decision keys off the code, never the detail sentence. Only `SND-UI-002`
(nothing answered) is Not running.

| Route | Read when | Shown as |
|---|---|---|
| `GET /sindri/commands` | on connect and after every reconnect (a new epoch) | the controls, Health's ACTIONS card, the palette's Actions |
| `GET /sindri/status` | on connect and on each event | the pill (`health.word`, `health.problems`, `views.projectedAt`) |
| `GET /sindri/services` | on connect and on each event | the stalled / rebuilding banner (`asOf`, `empty`) |
| `POST /sindri/views/rebuild` | Rebuild views (banner, palette) | the command lifecycle |

**Controls (§7, `ViewModels/Controls/*`, `Views/Controls/CommandControl`).** One control per catalog row, keyed by
action id (labels in `Labels.Actions.cs`): `ready` is a button (it runs only when the app has a sender for it; U1 sends
`RebuildViews`), `pending:<phase>` a disabled button with a focusable `InkSubtle` line "arrives in <phase> · <cli>" and
Copy, `cli-only:<reason>` the copy block (the CLI, Copy, and "Needs a typed confirmation at a terminal." for
`typed-confirmation`). A CLI with a control, bidi or invisible character is neither shown nor copied. Lifecycle (§4.1):
sending (enabled for the keyboard so focus stays on it, the view model refuses a second run; label kept, spinner; "Sending…" announced after 300 ms; a second click does nothing), accepted
("Rebuild views: done", 2 s), refused or failed (the code and `detail` inline, as served, and announced as "<code>: <detail>"; every run first rests the live region on the label so a repeat outcome is a change; a retry sets the phase to Sending before it clears the old outcome, so nothing reads half-cleared), Copy ("Copied", 1.5 s; a second Copy inside the 1.5 s rests the region and says it again). A send cancelled with its token (the window closing) leaves the phase Idle. The control's live region is dark (`IsMuted`) only for a run the command palette started and still announces; otherwise the control speaks its own lifecycle.
The clipboard is a port (`Platform/IClipboard`, `Views/AvaloniaClipboard`; see the ports table).

**Freshness (U10, E-14).** The pill reads `health.word` (Healthy, Degraded, Stalled, Rebuilding), its age from
`views.projectedAt`, then each `health.problems[].text`. Connection states win. While `views` or `health` is null (P1,
or P2 before unit E) it reads "Running" with the age from the last event or ping (Q1; the one decision point is
`HealthPill.Show`). A stalled services view shows the banner under the top bar with the view's own `empty` sentence and
**Rebuild views** (the catalog's row through the control renderer); rebuilding shows a progress banner. Each event
re-reads the status and the services view at most once per view per 250 ms (§9.2; `HealthViewModel.RefreshEvery`,
`ControlPlaneFollower.RefreshOnSignal`): the first event of a burst re-reads at once, the rest collapse into one
re-read when the window ends. A projection still behind the ledger (`cursor < ledgerHead`) or rebuilding is read again
on the same beat with no event needed (a stalled one waits for an event, it won't move by itself). `X-Sindri-After`
(the accepted command's sequence) is best effort and has one lifetime: it rides the view queries only (status, services;
never ping, the catalog, a command or the stream), goes on the stale-token retry too (the server waits up to 2 s when the views
are behind, well inside every call's 120 s deadline, and answers a stalled projection at once), and is used up by the first
answer of any kind, SND-API-011 included. A ping that shows another pid or epoch (another ledger) drops it unused. So
SND-API-011 ("views behind") is not trouble and never repeats: the follower reads the status once more at once, without
the header, and shows what Sindri says (stalled, rebuilding); only if that is behind too does the last data stay and the
next beat read again. On connect, a first status that is behind is read the same way, and the URL is followed once a
status was shown. A manual Refresh that hears SND-API-011 re-reads once more at once, without the header (two status calls: the first carries the sequence, the second does not), exactly as the follower does, and is not a failed refresh (it doesn't count toward "not responding"). A services read that fails for any reason but the
missing route before P2 (SND-UI-003) leaves the banner as it was; a catalog that failed on connect is read again with
the next refresh. The views banner has live regions (stalled sentence, "Rebuilding views", "Views are up to date"), flag
then text. E-15's `invalidates` waits for P5.

**The palette (§8, `ViewModels/Palette/*`, `Views/Palette/CommandPalette*`, `Views/Palette/ShortcutsSheet*`).** ⌘K (Ctrl+K off macOS), a click on the ⌘K field,
or Enter or Space on it opens it with focus in its field. Sections: **Actions**, every catalog row not in Sindri,
ready first, then pending (shown with "arrives in <phase>", never run), then cli-only ("Copy: sindri …"; Enter copies,
"Copied" for 1.5 s, the palette stays open); U1 has no selected row, so Actions lists the whole catalog (Q3). **Go to**:
Home G H, Workflows G W, Work G Q, Recipes G R, Context G C, Agents G A, System G ., Settings ⌘, (G D is a Home filter, so the palette has no entry for it). **Sindri**: Rebuild views, Stop Sindri
(Enter goes to Health, where it lives; never sent from the palette, its confirm is U4: Q4) and Health. Every catalog row
appears once. Typing filters by a fuzzy match (the query's letters in order, spaces ignored) on labels and ids; ↑/↓
move between entries that do something, Enter runs one, Esc or a click on the scrim closes it, and focus returns to
what had it (the ⌘K field when that is gone). Each entry run is remembered in `paletteHistory`, newest last.
It opens, and each refilter shows, at the top with the first heading in view and the first entry that does something
selected; only ↑/↓ scroll the list to follow the selection (`FollowsSelection`; U1 finding F1). Enter on a ready
command (Rebuild views) follows `CommandPaletteViewModel.RunPolicy`, the one place that decision lives (U1 finding F2,
provisional until the owner decides): by default (`StayOpen`) the palette stays open and the entry shows §4.1's
lifecycle in place, a spinner beside its kept label and "Sending…" (disabled, a second Enter does nothing), then
"<label>: done" for 2 s, or Sindri's code as its status with the detail as served under it (an accepted entry's accessible
name is the outcome alone: "Rebuild views: done"). Esc closes the palette at any point and the command keeps running.
`CloseFirst` closes, then sends. **Who speaks a run.** The palette never mutes controls as a whole. When it runs an entry it
mutes that entry's control only, for that run (`CommandPaletteViewModel.Runs.cs`), and it announces the lifecycle of every
entry it ran, whatever the selection or the query became meanwhile: "Sending…" after 300 ms, then the outcome or the
refusal; the selected entry is announced with its position ("Rebuild views: done, 3 of 14"), another ran entry by its name
alone. Nothing is said in a send's first 300 ms and nothing when the outcome settles back to ready. Each run says an entry's status once
(the outcome arrives through several property changes, and moving the selection off the entry after "Sending…" does not
say "Sending…" again), and a run says its outcome even when it equals the last run's (a retry's same refusal, an
accepted retry inside the 2 s, a second Copy inside the 1.5 s): the palette's region rests on a neutral text with its gate
(`IsAnnouncing`) dark, then says it, flag before text. The mute ends when the palette has announced the run's outcome
(accepted or refused) or, for a copy, when "Copied" clears. **A closed palette.** When the palette closes while a send is
still in flight (Esc, or the `CloseFirst` policy), the control stays muted and the shell's command-outcome region
(`ShellWindow`'s `CommandOutcomeLive`, `ShellViewModel.CommandOutcome`, a `CommandOutcomeRegion` in `ViewModels/Palette`; always in the tree, zero-width and transparent,
flag then text, never "") speaks the rest of the run once: "Sending…" if it was not said yet, then the outcome or the
refusal. Only a control whose own view is on show (the Rebuild views control of the stalled banner while the banner
shows) is handed back at the close and speaks itself; the Rebuild views control has no view otherwise, so its outcome
would be spoken by nobody. The decision is made at the close: a view that appears or goes away mid-send is not
re-decided. A cancelled send returns the control to Idle, so the palette lets go of it. Known limit: a catalog
replaced mid-run (a reconnect) releases every run, so a send that was already in flight is spoken by no live region.
A send that did not start from the palette is never muted: its control speaks the outcome even while the palette is
open, and the palette says nothing about it. A ready row the app has no sender for (the catalog's `IssueChallenge`,
`RegisterRecipe`) is shown as "not in the app yet" and skipped by Enter. U2's actions are not among them: `CreateItem`
runs (ready; Enter closes the palette and opens the Create item form, whose own control sends it), and `CloseItem:done` /
`CloseItem:cancelled` read "on the item's sheet" and are skipped, because they send the version the sheet shows. Every action id in the catalog has its own
label, and an id without one shows a humanised form of itself, never the raw `X:y`.

**The `?` sheet.** `?` (not while typing in a field, nor over the palette) opens §8's keys plus each catalog row's own
`shortcut` (none yet); it takes focus, Tab cycles inside it, Esc closes it and returns focus. The sheet lists §8's
table; the keys U1 serves are ⌘K, `?`, ⌘, and F6 (the G-chords, J/K, O, A and the rest arrive with their views).

**Proof fixture (§18.2): `scripts/sindri-ui-fixture.sh`.** Everything goes through `scripts/sindri-prove.sh`, whose
guard runs first (a temp or proof `AW_SINDRI_HOME` only). `up` writes the live profile (a 5 s heartbeat, port 0),
approves it as the owner and starts `sindri --headless`; `stall` inserts one `service.ran` row with an unparseable
payload into `ledger.db` (`STALL_TYPE` overrides the type); `unstall` deletes it; `drift` changes the heartbeat's
cadence so the approval no longer matches; `down` stops it; `--large` is reserved for U2/U3. Test:
`bash scripts/tests/sindri-ui-fixture.test.sh` (the guard, usage, the stall SQL on a temp ledger, a stub SINDRI).

```bash
H=$(mktemp -d "${TMPDIR:-/tmp}/sindri-u1.XXXXXX"); export AW_SINDRI_HOME=$H SINDRI=<built sindri>
scripts/sindri-ui-fixture.sh up
AW_SINDRI_HOME=$H <published sindri-ui> &      # bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <dir>
scripts/sindri-ui-fixture.sh stall             # the stalled banner and Rebuild views
scripts/sindri-ui-fixture.sh unstall           # then click Rebuild views
sqlite3 -readonly $H/ledger.db "select type, actor from events where type in ('command.handled','views.rebuilt') order by seq desc limit 2"
scripts/sindri-ui-fixture.sh down
```

**Context page data (U9).** After `up`: `scripts/sindri-ui-fixture.sh context up` writes the tabs repo `aw-split-panes` under `$H/ui-fixture-context`, adds it to the profile, approves the profile when it is not approved, imports `map-tabs.json` through the typed `import map <hash6>` and prints only the repo name (the app's `repo=`). A repeat `up` imports the map only when the store has no map or the file changed. `context draft` runs `context map build` and builds the next draft version after the current map (v2 on a fresh home; the imported versions stay in `context.db` through `down`, so the number depends on earlier runs). `context down` removes the repo and the profile entry, approves again if needed, and is safe to repeat. The verbs always edit `$H/profile/profile.yaml`; an exported `AW_SINDRI_PROFILE` is ignored.

**Tests.** Unit lane: `Palette/*` (the palette and the sheet), `Controls/ControlViewModelTests`, `Shell/FreshnessTests`
(the pill, the banners, the catalog on connect, the 250 ms re-read), `Api/*` (headers, token, ProblemDetails, and
`GoldenContractTests`, which deserializes every golden through `SindriUiJsonContext`). Integration lane:
`Views/CommandPaletteTests` (⌘K, the field's click, arrows, Enter, Esc returning focus, `?` never while typing; frames
`palette`, `palette-filtered`, `palette-top`, `palette-sending`, `palette-done`, `palette-refused`, `shortcuts`) and `Views/CatalogFramesTests` (frames `u1-banner-stalled`,
`u1-banner-rebuilding`, `u1-control-sending`, `u1-control-accepted`, `u1-health-controls`). The fake serves P1's and
P2's goldens (`tests/Aw.Sindri.Tests/Golden/Api/**`) and U1's spec-shaped ones (`tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/**`, to be replaced by P2's as they land).

**Real-app runs (the Linux proof).** A run of the real app on Linux uses its own Xvfb display (for example `:99`) and a
private AT-SPI bus with a private `XDG_RUNTIME_DIR`. It never inherits `DISPLAY`, never uses the owner's desktop (`:0`,
`:1`) and never touches `/run/user/<uid>/at-spi`. The proof tools under
`~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-ui-u1/raw/tools` enforce this: `session-up.sh` brings the
session up, `env.sh` refuses to load unless the session is private, and `privsession.py` is the private-bus helper.

## U2: queue, item detail, timeline, Create and Close item (UI spec §18 U2)

Built against the fake `ISindriApi` because API P3 isn't built: every P3 route is served from a **spec-shaped
fixture** under `tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/` (listed below), each to be replaced by P3's golden
(E-17). The real catalog keeps `CreateItem`, `CloseItem:done` and `CloseItem:cancelled` at `pending:P3`, so against
today's control plane the Create button and the sheet's Close controls draw disabled with "arrives in P3"; the tests
use `commands/catalog` (P1's catalog with those three rows `ready`).

**Routes (API P3).** `GET /sindri/queue` (`QueueView`: `groups[]` of `{group, items, empty}` in the API's order,
every group present, E-18; the view's `empty`; `asOf`), `GET /sindri/items/{id}` (`ItemView`: the summary fields plus
`version`, `createdBy`, `waitingOn`, `instance`, `createdAt`, `updatedAt`, `asOf`), `GET /sindri/items/{id}/timeline?after=`
(`entries[]` of `{sequence, time, type, actor, line}`, `next`), `POST /sindri/items` (`{title, repo?, body?, link?}` →
`{accepted: {commandId, sequence}, itemId}`), `POST /sindri/items/{id}/close` (`{reason, itemVersion}` →
`{commandId, sequence}`). Field names the API spec leaves open are the U2-API edits in the PR description. The client
takes `X-Sindri-After` from `ItemCreated.accepted.sequence` as from every accepted command.

**Codes.** `SND-ITEM-404` (the sheet shows its `detail` with Back), `SND-ITEM-409` (the sheet re-reads the item; the
pressed control shows the `detail` inline while the item still allows it, else the sheet's own line does, §9.1).
Tests key off the code and the golden's `detail`, never a copy of the sentence.

**What the app shows.**
- **Queue page** (the Work tab, `G Q`; `Views/Queue/QueuePage`): the API's groups as collapsible sections with counts; empty groups
  hidden, a filtered group shows its own sentence; the view's `empty` when every group
  is empty; skeleton rows on the first load only. Rows (`ItemRow`; Home uses `CompactItemRow`): id, title, repo, the state as a word, step, age,
  and the API's `primaryAction` as the catalog's control (pending rows draw disabled with their phase). A row sends
  nothing itself: Enter or a title click opens its sheet.
- **Home** (`HomePage`): Running and With you (With you first, at most five, then "Show all N in Queue", which opens
  the Work page filtered to running) and Queued (five, then "Show all N"), each with its group's own sentence when empty (E-18), in a
  compact row. Needs you stays "not served yet" until U3. The Queue tab carries the queue's count.
- **Item sheet** (`Views/Item/ItemSheet`, in the shell's sheet when the subject is `item/<ItemId>`): title, repo, the
  state as a word, version, age, created by, waiting on, instance; the item's `actions` ∩ the catalog as controls
  (kept across refreshes so their lifecycle survives); `CloseItem:*` send the version shown; the timeline paged by
  `next` with **Load more** (a refresh re-reads the pages loaded so far).
- **Create item** (`C`, the Queue page's button; `Views/Item/CreateItemDialog`): title, repo, body, sent by the
  catalog's `CreateItem` control; accepted shows "Create item: done" for 2 s, then the form closes. The new row comes
  from the stream's re-query, never from the answer. The form is a modal dialog: counted as an overlay (F6, Ctrl+K,
  Ctrl+, and `?` do nothing), Tab cycles in the card, focus returns to the opener (else the Create button) on Esc, Cancel
  and auto-close. The title field is "Title (required)"; Create stays enabled, and a blank submit shows "Title is
  required" under the field, speaks it once, focuses the title and sends nothing. No second create while the outcome shows.
- **Destructive confirm** (E-8, all controls): a destructive row's press asks "<label> <subject>?" with **Confirm**
  and **Keep** (focused; Esc keeps). Controls on a subject are named with it ("Close item ITM-…", §11.2).
- **Refresh:** `HealthViewModel.Follow` registers the queue and the open item; the follower re-reads them after the
  status and services on connect and on every event (U1's 250 ms debounce). No payload is folded.
- **Keys** (`ShellViewModel.Keys`): the `G` chord is rev 8's (`ShellViewModel.Chords`: 1 s on the injected clock, "G …" while it waits; its keys are listed under "Rev 8 contract"), `J`/`K` rows, `Enter` opens the selected row, `C` create; never while typing
  in a field or over a layer. Esc closes the create form, then a confirm, then the sheet.

**Spec-shaped fixtures (replace with P3 goldens of the same names).** `queue/{fixture,empty,stalled,large}`,
`items-id/{new,after-close,not-found}`, `items-id-timeline/{fixture,page-2}` (the fixture a full page of 100),
`items/created`, `items-id-close/{accepted,stale-version}`, `commands/catalog` (P1's catalog with CreateItem and
CloseItem:* `ready`). Marked extras, not P3's: `queue/extra-p4-waiting` (a needs-decision row), `queue/extra-p11-parked`,
`queue/extra-p11-running`, `items-id/extra-p4-needs-decision`. In P3 every `primaryAction` is null, `waitingOn` and
`instance` are null, and `owner` is null (README X61: multiplayer later; the records accept it, no owner UI).

**Owner decisions (2026-10-12).** Four queue groups (API §3.6); a parked item shows in Waiting on you with a "parked"
badge from its `state`. The sheet shows `body` (scrubbed, plain text) and `link`. The sheet stays open after Close.
The destructive confirm reads "<label> <id>?" with Confirm and Keep. Actors (`createdBy`, the timeline's `actor`) are
opaque strings shown exactly as served; the app maps no fixed set (README X61), and assumes nothing about where an
agent session runs. Running rows' agent, place and budget arrive with P11 (open for U5/P11).

**§8's guards and filter.** While the pointer is over the Queue panel or Home's queue panels, only rows the page does
not show yet wait behind the "N new" pill (spoken through the shell's gated queue region); removals and in-place
updates show at once. Leaving, or the pill, shows the held rows. For 500 ms after a refresh moved the selection because
its row cleared, single keys do nothing. `Y` copies the selected row's primary-action CLI (through its control:
"Copied"); `/` opens the Queue page's filter (id, title, repo; "Nothing matches"), the last Esc layer after the sheet;
Home's panels ignore it. The top bar shows "G …" while the chord waits: a visual cue only, with no live region and
not a control element for a screen reader.

**Focus rings on rows, and the page beside the sheet (proof B2, B7, B8, N9).** A sheet beside the page never covers page
content (WCAG 2.2 2.4.11 Focus Not Obscured; owner decision 2026-10-15, UI spec §3.2 "Page beside the sheet"): the page
region is the window minus the sheet, and the page adapts to the narrower width. The shell sets `PageLayout.Tier` (an
inherited view-side class, no view-model logic) on the page region from its width: Wide (1120 px and up) shows every column;
Mid (1000 to 1120) drops step and since; Narrow (790 to 1000) drops repo too and Home stacks its two panels into one column;
Tight (under 790) keeps id (100), title (at least 160) and the primary action (170). The queue's header and its rows use the
same widths so columns line up. The default focus ring is an adorner, drawn above the sheet, so a row and the buttons in it
(`Border.item-row`, `Views/Queue/QueueStyles.axaml`) have no adorner (`FocusAdorner` null) and draw the same 2 px
PrimaryHover ring inside their own template on `:focus-visible`: the row on its own border, a button on its content
presenter. Focusing never changes layout (N9): a title's presenter always has a transparent 2 px border inside the button, which gives the size back with a -2 margin (B9), a
command control's padding gives up the pixel its border gains, and the selected row's padding gives up its 2 px bar.

**Selection is focus (U2 review run 1).** A refresh reconciles in place: groups by name (so a collapsed group stays
collapsed), rows by id and timeline rows by sequence are updated, and the bound lists (`ObservableCollection`, via
`Patch`) change only where they differ, so the focused control keeps focus. One selection is the focus: J / K select
the next or previous row of the page they are pressed on (Home: Running and With you, then Queued, as drawn; the
Queue page: its visible, expanded groups) and focus that row's frame; a row, or a control in it, that gets focus
selects it; Enter and Y act on the selected row only while it is on the page, and J / K / Enter / Y belong to the sheet
while focus is inside it. A filter change that hides the selection clears it silently; a refresh compares against the
unfiltered view, so a row hidden by the filter is not "cleared". When the selected row clears, the selection moves to
the row now at its place, "<id> cleared" is spoken once through the gated queue region, and focus goes there if the
old row had it. The sheet's subject is set by every deep link (before the connection too: the first re-read loads it),
and Esc from the sheet returns focus to the row that opened it, by id.

**Focus and speech, review runs 2 and 3.** Every selection call names its `QueueScope` (`Page`, the Queue page's visible,
expanded groups; `Home`, the two panels as drawn); there is no default, so a caller cannot pick the wrong page. A
selected row that a refresh leaves off the page it was selected on (served in a group Home does not draw, pushed past
Home's cap of five, or in a group the Queue page's filter hides) is treated as cleared from that page: the selection
passes to the row now at its place, "<id> cleared" is spoken once, and focus follows if the old row had it. "N new" and
"<id> cleared" in one refresh are one sentence, spoken once. Pressing the "N new" pill hides it and moves focus once, onto
the first row it showed (the window's keep-focus fallback waits while a row request is pending). Esc from the sheet
returns focus to the opener row by id, else the edge tab that opened it, else the row the selection moved to, else the
page region. Load more is keyed to the item it was sent for (a page in flight for an item the sheet left blocks nothing
on the next); a page that fails is spoken once and the rows stay, and a page that lands for another item, or after a
refresh moved the cursor, is dropped. A Close that meets a 409 does not return until a read has settled the conflict
(the sheet speaks it when the item no longer allows the action, the control when it still does), so it is spoken once.

**The event stream (P5).** API P5 replaces `/events`' body with `PublicEvent`; the follower switches its event record
when P5 merges. U2 doesn't touch the stream: it only re-reads views on each event.

**Proof recipe (once P3 is on main).** Under `scripts/sindri-prove.sh`'s guard:

```bash
H=$(mktemp -d "${TMPDIR:-/tmp}/sindri-u2.XXXXXX"); export AW_SINDRI_HOME=$H SINDRI=<published sindri>
scripts/sindri-ui-fixture.sh up && scripts/sindri-ui-fixture.sh items      # items --large for the fold test
AW_SINDRI_HOME=$H <published sindri-ui> &                                  # bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <dir>
scripts/sindri-prove.sh human item add "Proof: appears live"               # the row appears within 1 s, no click
scripts/sindri-ui-fixture.sh close <id>                                    # then Close in the app's sheet: SND-ITEM-409
scripts/sindri-prove.sh curl GET /sindri/queue | jq -r '.groups[] | "\(.group) \(.items|length)"'
scripts/sindri-ui-fixture.sh down
```

**Tests.** Unit lane: `Queue/QueueViewModelTests`, `Item/ItemSheetViewModelTests`, `Item/CreateItemViewModelTests`,
`Shell/ShellItemsTests` (a new item appears on the next event without a click; the item link; the keys), the confirm
facts in `Controls/ControlViewModelTests`, and the P3 facts in `SindriApiContract` (fake and client). Integration lane:
`Views/QueueFramesTests` (frames `u2-queue-populated`, `u2-queue-empty`, `u2-queue-loading`, `u2-home-panels`,
`u2-item-sheet`, `u2-item-timeline`, `u2-item-404`, `u2-item-409`, `u2-create`, `u2-close-confirm`).

## Rev 8 contract (UI spec rev 8, screens spec §3; PR #215)

The rev-8 contract unit gives the shell its screens' skeleton and the app its first workflow reads; the pages fill in with
U3' and U8.

- **Navigation.** The top bar has seven tabs, in order: Home, Workflows, Work, Recipes, Context, Agents, System. Settings is
  not a tab (⌘, opens it). `PageId` names the eight pages; a page with no screen yet shows a placeholder that says what it
  waits for. The last page is remembered in the preferences.
- **Chords.** `G`, then a page's key within `ShellViewModel.ChordWait` (1 s): `H` or `N` Home, `D` Home filtered to
  decisions (`HomeFilter.Decisions`), `W` Workflows, `Q` Work, `R` Recipes, `C` Context, `A` Agents, `.` or `S` System.
  They carry no modifier, so they are the same on every platform. The wait is a delay on the shell's clock (no timer
  thread), and a generation says which chord a finished wait belongs to. A chord is gated like the other navigation
  (`IsNavigationEnabled` and `IsChromeVisible`, in `PressKey`), never fires while a text field has focus or an overlay
  is open, and any other unmodified key (Esc included) cancels a waiting chord. Any navigation but G D clears the Home
  filter. The top bar shows `G …` while one waits, in a keycap inside the bar over the ⌘K field's right end (drawn in the bar, not below it, where the page region would paint over it).
- **Deep links.** `workflow/<address>` (an address, which may contain `/`), `recipe/<id>`, `node/<key>`, `session/<id>`
  and `system/<dependency>` go to Workflows, Recipes, Context, Agents and System; `item/`, `decision/`, `service/` and
  `job/` open their sheet over the page shown; `needs-you/<row>` (the row's item, decision, service or job) opens that row's sheet;
  `home/needs-you` is Home. A workflow instance's `subject.kind` is never null (P4c, RCP-INPUT-001): it is the first input's declared kind;
  a pre-P4c instance's subject is the item fallback `{kind: item, key: item id, label: title or address}` (engine spec §3.11, API spec §3.8). `ShellViewModel.Navigate` routes them and the
  sheet and its subject live in `ShellViewModel.Sheet.cs`.
- **Workflow reads** (`ISindriApi`, API §3.8; the client's routes are in `Api/Workflow/WorkflowRoutes`):
  `GET /sindri/workflow/instances` (the root rows and the root-wide counts; `root`, `open`, `recipe`, `subject`, `address`
  narrow it), `GET /sindri/workflow/instances/{id}` (by the opaque id; an address is only ever a query value),
  `GET /sindri/workflow/instances/{id}/facts/{key}` and `GET /sindri/workflow/recipes/{id}[@{hash}]`. The records mirror the
  API's goldens (`Api/Workflow`). The views print the figures
  as served (`counts.open`, a group's `done` and `total`) and recount nothing.
- **Test support.** `WorkflowAnswers` serves the API's `workflow-*` goldens, found by enumerating the directory, to the
  fake (`FakeSindriApi.Workflow.cs`) and to the handler; `FixtureInstances` is the one instance table (the root list
  golden and every instance golden) with the one open rule, and `ChildTally` recounts a group's shown rows. They are
  consistency checks on the fixture, not app code. `GoldenContractTests` deserializes every `workflow-instances`,
  `-instance`, `-instance-fact`, `-status` and `-recipe` golden through the app's JSON context, and
  `SindriApiContract.Workflow` runs the same scenarios against the fake and the real client.
  Every recipe an instance runs has a recipe golden keyed by its id, and the fake answers it with that golden's body (the
  contract test asserts both, and that the golden's `hash` is the one every instance running it reports); any other id,
  or a hash nobody runs, is SND-RECIPE-002 (API spec line 771), never another recipe's content.

## Lane anchors (U3', U8, U10, U9 and U11a)

Two lanes add to the same files, so each hotspot carries one marker per lane, `LANE ANCHOR U3'` and `LANE ANCHOR U8`
(a comment: `//` in C#, `<!-- -->` in XAML), adjacent: U3' first, U8 second. **One insertion rule:** U3' adds its lines
immediately above its own marker and U8 adds its lines immediately below its own, so the pair stays together and neither
lane inserts between the markers; every marker's text says so. Two merges then touch different lines. The files:
`Api/ISindriApi.cs`, `Api/SindriApiClient.cs` (right after the public reads, `Recipe(...)`; a lane's routes go in a routes
class of its own, like `Api/Workflow/WorkflowRoutes.cs`), `Api/SindriUiJsonContext.cs`, `ViewModels/Shell/ShellViewModel.cs` and
`.Sheet.cs` (the markers sit inside the `IsSheetPlaceholderShown` switch), `Views/Shell/ShellWindow.axaml` (inside `SheetContent`, the content of the one sheet layer, chosen by the
sheet's link kind), and in `tests/Aw.Sindri.Ui.Testing`: `FakeSindriApi.Workflow.cs`, `FakeSindriHandler.cs` (inside
`Route()`'s switch, above the workflow-read arm, which answers GET only), `SindriApiContract.cs` and
`SindriApiContract.Workflow.cs`. This page has its own marker at the end of "Tests". A lane's new shell state goes in its
own `ShellViewModel.<Slice>.cs` partial (the file cap is 300 lines), and a lane's new window behavior goes in a
`ShellWindow.<Slice>.cs` partial (`ShellWindow.axaml.cs` is near the cap). The sheet keeps the **kind** of the link that opened it
(`SheetLink`: kind and subject; `SheetSubject` is derived), so `item/X` and `workflow/X` are two sheets and the edge tab
reopens the right one. The sheet's content is chosen by that kind: `ShellViewModel.IsSheetPlaceholderShown` is `SheetLink?.Kind switch { …, _ => true }`, and `SheetBody` ("not served yet") binds its `IsVisible` to it. Each lane claims its kinds with one `DeepLinkKind.X or … => false` arm at its own marker inside that switch (U3' above its marker, U8 below its own), then adds its content inside `SheetContent`. The kinds follow screens spec §8, and each marker's text lists them as `(kinds: …)`, which a test parses: **U3'** claims `NeedsYou` and `Decision`; **U8** claims `Workflow` (its re-base of the item sheet, §4.7, belongs to U8b; it is content and adds no arm). U8 makes a second edit outside its markers, which the pair above allows: the sheet header's Back in `ShellWindow.axaml` calls `OnBack` instead of `OnCloseSheet`, so Back pops a trail of frames and item sheets opened from a frame first, and closes the sheet when the trail is empty (every other sheet kind behaves as before). `Item` is U2's item sheet (#197): its arm, `DeepLinkKind.Item => false`, is written above both markers. `HomeNeedsYou` is a page link, not a sheet. **U10** has a third marker of its own, below U8's arms (so U10a, U10b and U9 do not collide with U8's merge): it claims `Recipe` (`(kinds: Recipe)`, a test parses it), and its XAML marker sits after U8's `InstanceSheetScroll` in `SheetContent`; U8's arms sit between the U8 and U10 markers and U10's below its own. **U9** has a fourth marker, below U10's arm (and, in `ISindriApi.cs` and `SindriUiJsonContext.cs`, below U8's): it claims `Node` (`(kinds: Node)`, a test parses it) for the Context page's node card; the arm itself is written by U9b, and until then `Node` still shows the placeholder. **U11a** has a fifth marker, below U9's (`(kinds: Session)`, a test parses it); its arm is written with the session sheet, and its XAML marker sits after U9's. `System`, `Service` and `Job` are claimed later, at this switch, by the phase that renders them (screens spec §8). The sheet's accessible name carries the kind's noun (`SheetName`: "Workflow X", "Recipe X", "Node X", "Session X", "System X", "Service X") and the edge tab's follows it ("Reopen workflow X"); an item, decision, job or needs-you row keeps its bare id, which names itself. The rev-8 links (`workflow/`, `recipe/`, `node/`, `session/`, `system/`) go to their page and open the
sheet on their subject. The desk-complete and nonce sends are **not declared** on `ISindriApi`: their shapes come with
API P11 (Q-API-7/8), and declaring them now would guess.

## How it finds the control plane

Every call carries this start's human token (`IHumanTokenSource`, read at each call; see the caller headers
above). A call Sindri refuses with 403 `SND-AUTH-001` (a missing or stale token) is retried exactly once with the token
read again from `auth/human.token`, since a restarted control plane has minted a new one; there is never a third call.
A follower call (ping, status, the stream) refused after that retry (or any other 403 with Sindri's ProblemDetails) is the **unreadable** banner with
Sindri's code and `detail` as they are ("SND-AUTH-001: A human caller needs this start's token in X-Sindri-Token (fix:
restart the app or rerun the command)"), never the token. The app keeps looking every 2 s and recovers on its own once
the token is good. A ping refused while following ends the follow, so the window names the refusal within one ping
beat (10 s). A refused Refresh click counts only as a failed refresh; the next ping beat shows the refusal.

1. The endpoint-record port (`IEndpointRecordSource`) reads `control.json`. The desktop adapter reads it from
   `AW_SINDRI_HOME`, else `$AW_STATE_DIR/sindri-app`, else `~/.agentic-workflow/sindri-app`, the same place the control
   plane writes it. No record means "Not running". A record whose URL isn't on this machine (loopback: 127.0.0.0/8, ::1
   or localhost) reads as Not running with "The endpoint record can't be read: … isn't on this machine" and the record's path; the app never calls that address.
2. `GET /sindri/ping` must answer the record's pid **and** epoch; otherwise the record is stale and reads as "Not running".
3. `GET /sindri/status` fills the window. A lower epoch than the last one means a new ledger, so the stream cursor
   starts over. Each `/sindri/events` event (server-sent events, resumed from the last
   sequence seen) triggers one status refresh; a burst of events triggers one. When the stream ends (the control
   plane stopped or was killed), a status refresh fails, or `control.json` changes (checked every 2 s; a stopping
   control plane can hold its stream open), the app looks again, every 2 s while nothing answers. A record the
   platform can't read, or one with a relative `url`, reads as "can't be read" with the reason, and the app keeps trying. The port
says why in its own terms, as a value and not an exception (`IEndpointRecordSource.Read` returns an `EndpointRecordRead`: the bytes, none, or an `EndpointRecordFailure`: `NoPermission`, `NotAFile`
or `Unreadable`; `EndpointRecordSourceContract` binds the desktop adapter to a real directory and a mode-000 file), since
.NET on macOS and Linux throws the same `UnauthorizedAccessException` for both; the desktop adapter checks for a
directory at the path first. The reason is the app's own words ("this user has no permission to read it", "it isn't a
file (something else, such as a directory, is in its place)", or "the platform couldn't read it"), never the platform's
message, which names the full path; the path shows shortened (`~/…`) on its own line, as every path in the window does.
4. While it follows, the app pings every 10 s (S1's stream sends no keep-alive, and a control plane with nothing
   scheduled sends no events). A ping answered with the followed pid and epoch keeps the views fresh; failed pings, or
   another process's answer, for 30 s read as stale.

## What the Health page shows

| Part | From the status record | Words |
|---|---|---|
| State pill | `running` | `Running`, `Not running` (with the reason and `sindri` as the fix), `Connecting…` |
| Lease | `pid`, `epoch` | `pid 4242 · epoch 7`, and the API's `127.0.0.1:<port>` |
| Profile | `profile.approved`, `live`, `changedSinceApproval`, `problem`, `source` | `approved` · `changed since approval` · `not approved` · `unreadable`, with the hash and file |
| Profile fix | `profile.needsApproval` (Sindri's verdict; the app doesn't derive it) | `sindri profile approve` when set |
| Services | `services[]` | `heartbeat · every 5s · last run 12:00:05Z` (`not run yet`) |
| Last event | the newest stream event | `service.heartbeat · 12:00:05Z · #42` |

State is always a word, never colour alone; each status text carries an `AutomationProperties.Name`. The look follows
`docs/sindri/DESIGN.md`: the near-black canvas, charcoal cards with hairline borders, Inter, and the lavender accent only
on the brand mark and the state pill.

## Launching

Bare `sindri` opens the app after it starts (or finds) the control plane: the `sindri-ui` binary next to `sindri`, or
the one `AW_SINDRI_UI` names, started detached (`exec nohup`, no pipes to the terminal). The summary's `ui` line says
what happened: `opened (sindri-ui, pid N)`, or `not found at <path>` with the fix. `sindri --headless` opens nothing and
prints no `ui` line. A relative `AW_SINDRI_UI` is resolved against the working directory before it is checked, and a file
without an execute bit reads `not executable at <path>` with the fix. Opening the app never changes `sindri`'s exit
code. A second `sindri-ui` hands over to the running one (`focus`) and exits, so a bare `sindri` while the app runs brings its window forward.

**`sindri` goes framework-dependent; `sindri-ui` stays NativeAOT** (csharp.md "AOT scope", README X54, engine spec
rev 8). The Temporal .NET SDK can't be trimmed, so the Sindri host publishes framework-dependent once its Temporal
adapter lands (the host project still sets `PublishAot` until then, and `src/Sindri/aot-smoke/run.sh` still runs).
Either way `sindri` is the binary in a publish directory (framework-dependent: an apphost beside its assemblies), and
`AppContext.BaseDirectory`, where the launcher looks for `sindri-ui`, is that directory. Copy the NativeAOT `sindri-ui` there together with its native libraries (`libSkiaSharp`, `libHarfBuzzSharp`,
`libAvaloniaNative`; without them it exits 2 with `SND-UI-004: TypeInitializationException`):
`bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <dir>` copies all of them. Or point
`AW_SINDRI_UI` at it; under `dotnet run` the base directory is the host's build output, so set `AW_SINDRI_UI`. The app
depends on nothing else about how `sindri` is built: it talks to the control plane only through the HTTP API and two files under its home, `control.json` and
`auth/human.token`.

**One command before the switch: `sindri-app`** (`scripts/sindri-app.sh`). It builds exactly that layout and runs it:
when the source changed (a content hash of the files the host build reads, committed or not, with the pinned SDK and
the platform), it publishes `sindri` framework-dependent and `sindri-ui` NativeAOT (through
`Aw.Sindri.Ui.AotSmoke/run.sh <dir>`) and the C# `judge` NativeAOT (the publish of `src/Judge/aot-smoke/run.sh`) into `$AW_STATE_DIR/bin/sindri-app/<stamp>/`, keeping the build before it, then
`exec`s that `sindri` with its arguments and `AW_SINDRI_JUDGE=<stamp dir>/judge` (unless the caller set it; `step.judge.choose` needs `judge choose`, which the TypeScript `judge` on the PATH lacks), so bare `sindri-app` starts the control plane and opens the app. It also
installs the pinned SDK into `~/.dotnet` and fetches the pinned `temporal` when either is missing, and links
`~/.local/bin/sindri-app` to itself (from the canonical checkout; `AW_SINDRI_APP_NO_LINK=1` skips). It is a side name:
`sindri` stays the TypeScript CLI until the switch. A control plane already running from an earlier build keeps
running it (bare `sindri` finds it); the script says so and `sindri-app stop` then `sindri-app` moves to the new build.

**`opened` is only "started".** `sindri` starts `sindri-ui` detached with its output to `/dev/null` and reports
`opened (sindri-ui, pid N)` as soon as the process starts, so the line can still claim success when that `sindri-ui`
then exits 1 (a running app that doesn't answer) or 2 (a failed start-up), and N is the launched process, not the
running app's. The UI package can't fix this: the line is `Aw.Sindri.Host`'s `UiLauncher`. Run `sindri-ui` directly to
see its exit code and standard error.

**A declared edge with a cycle exception (owner decision, 2026-10-12).** Starting `sindri-ui` from `sindri` is the
`Sindri -> SindriUi` cli edge in `deps.yaml`. It closes a cycle with the `SindriUi -> Sindri` http edge, so it carries a
`DEPS-CYCLE-001` exception that names that edge: the launch only spawns a separate process, shares no code or data,
and Sindri's HTTP API (with `control.json` and `auth/human.token`) stays the app's only contract. The edge is `status: planned` only because check-deps doesn't
scan C# for cli edges (a current edge its scan can't find fails `DEPS-STALE-001`).

## Errors

The app shows the first three as words in the window, never a dialog or a crash. A call Sindri refuses (a 403 with
its ProblemDetails, such as `SND-AUTH-001`, after the one retry) keeps Sindri's own code: the unreadable banner shows
that code and its detail as they are (API spec §3.6).

| Code | When |
|---|---|
| `SND-UI-001` | No usable endpoint record, or it names a process that isn't the one answering |
| `SND-UI-002` | Nothing answered at the recorded endpoint, or not within 2 s |
| `SND-UI-003` | The API answered, but not with the record this build reads (an error status other than a 403 with Sindri's ProblemDetails, or another body). The banner says it in words ("SND-UI-003: /sindri/status answered with something this build can't read", or "… answered 500"); a body's parser detail (record, JSON path, byte position) goes only to the log, as a `SND-UI-003` warning |
| `SND-UI-004` | The app's start-up, its following of the control plane, the launch before it (claiming the single instance, handing over) or ending the instance and the motion poll after it threw: logged (code and exception type) and the app exits 2. Not shown in a window: there may be none |

## Tests

| Lane | What |
|---|---|
| unit | The view models (the shell, Health, and Settings against the in-memory preferences and a fake OS setting) against the in-memory API, endpoint record and human token; the `ISindriApi` contract for the fake and for the real client over a canned handler that writes Sindri's wire format by hand; Sindri's API goldens (UI spec E-17: `tests/Aw.Sindri.Tests/Golden/Api/{status,ping,events}`, linked into the test output and read through `GoldenFiles`) deserialized through the app's JSON context, the handler's hand-written bodies held to their shape, and the SND-AUTH-001 refusal goldens served verbatim (one retry, then their code and detail; `Api/GoldenContractTests`); the control.json path; the theme (tokens, contrast, Fluent values, no hex outside `Theme/`); the port contracts over their fakes; preferences, deep links, labels |
| integration | The shell window under Avalonia's headless platform (headless drawing, no Skia: building and laying out a window costs the lane's time, and `Aw.Sindri.Ui.Tests` runs with `TieredPGO` and the background GC off for the same reason; a test that reads pixels lives in `Aw.Sindri.Ui.Rendering.Tests`, which draws with Skia: the focus ring as drawn, the tray marks, the sheet Tab walk, and `RealFontTextFitTests`, the real-font canaries: headless drawing measures every glyph at the font size with Avalonia's stub shaper, knowing neither Inter nor the embedded mono face, so the text-fit assertions in `Aw.Sindri.Ui.Tests` (wrap, trimmed, clipped, baseline) check the layout's logic, not the app's fonts; the canaries repeat one assertion of each kind at 900 px and text scale 1.3 with the real fonts), one key frame per pre-API state plus Health, the sheet, its edge tab and its covering form, saved as `frames/shell-*.png` under the test output when `AW_UI_FRAMES=1` turns Skia on for the run (the project's default draws nothing, so `CaptureRenderedFrame` returns null and no PNG is written) (`artifacts/bin/Aw.Sindri.Ui.Tests/debug/frames/`); F6 (and ⌘⌥←/→ on macOS), Esc and ⌘,; the remembered bounds; Settings (frames `settings-motion-off`, `-on`, `settings-system-reduces-motion`, `settings-text-120`); `Motion.Apply` over a token dictionary; the theme as the app resolves it (palette, overrides, the embedded mono font, the text scale); the head's adapters against temp directories (the endpoint-record, human-token, preferences, single-instance and tray contracts, owner-only modes, a crashed instance's socket, the listener's checks), the OS motion readers read-only (GNOME over a stand-in gsettings, macOS against `defaults read com.apple.universalaccess reduceMotion`), and `AppLifetime` on a headless window |

`Avalonia.Headless.XUnit` 12.1.3 is compiled against xUnit v3 3.2.2 and fails discovery under the repo's xUnit v3 4.0.1
(`MissingMethodException` in `TestIntrospectionHelper.GetTestCaseDetails`). The view tests are plain xUnit v3 facts that
dispatch onto `HeadlessUnitTestSession.GetOrStartForAssembly(...)`, with the app builder in `[assembly:
AvaloniaTestApplication]`. Revisit when an `Avalonia.Headless.XUnit` built against xUnit v3 4 is 14 days old.

```bash
dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=unit"
dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=integration"
bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh [<dir>]   # NativeAOT sindri-ui on a copy (CI's AOT step); copies it to <dir> when given
```

## U3': Needs you, decisions and requests (UI spec rev 7 §18 U3, screens spec §8)

What needs the human, drawn from API P4's routes (`GET /sindri/needs-you`, `/decisions`, `/requests`, `POST /sindri/decisions/{id}`
and `/sindri/requests`). The app holds no logic of its own beyond the presentation rules listed below (the "Client presentation rules" bullet, with the live-region sentences): every row, label, option, refusal and "cleared" line is as the API served it,
and the tests read their expected values from the recorded goldens under `tests/Aw.Sindri.Tests/Golden/Api/`.

- **Needs you (Home).** Rows in the API's order, at most five (`NeedsYouViewModel.Cap`, four at large text), then "all N →" expands them in
  place. A row has one state chip (`StateChipView`), the served title, reason, repo and age, and its controls: the primary action first, then
  the rest. A control is ready, pending (the catalog's phase tag) or CLI-only, from `GET /sindri/commands` (the catalog; its `ui` word is read once, by `CatalogReadiness`, and a word this build does not know is CLI-only); a protected action
  shows the served CLI with Copy instead of running. New rows while the pointer is over the panel are held behind "N new" until you leave it.
  The summary line, `stalled` and `rebuilding` banners and the empty sentence are the served ones.
- **Decide.** The decision sheet shows the served question, one button per option (the recommended one is the option the decision serves as
  `recommended`, and the one `A` runs), the note (with a caption saying it is sent with the choice) and what Decide answered. One
  `DecisionSender` serves the decision sheet and the item sheet: a decision is sent once and never retried by the app; the decision and its
  label are taken at the press; only an accepted Decide marks it sent; a refusal or a lost answer leaves it pressable. A refusal (decided
  elsewhere, voided: SND-ITEM-409; SND-DEC-404) shows inline as served and re-reads the rows. **Focus is never lost on a press**: the options
  are one list, replaced only when an option differs, so a re-read keeps the button the user tabbed to; an accepted decision disables the
  options and `DecisionSheet.RestoreFocus` moves focus to the sheet heading (the ring follows the way the press was made). An accepted outcome shows for 2 s. A row that clears under an
  open sheet disables the form and shows the cleared line from `GET /decisions?pending=false`, else "<id> cleared". A needs-you link whose row is a decision (the tray, a deep link) is canonicalized to `decision/<id>` in `Navigate`, so it opens the same sheet as a click.
- **Requests.** The item sheet lists the item's pending decisions (option buttons) and the requests filed against it, or the served
  empty sentence. Filing a request from the UI is not built; the fixture's `request` verb files one as a session.
- **Failure and profile rows** open the sheets built by `FailureDetails` and `ProfileDetails` (static builders, computed when the row or its age changes): the served reason sentence (`reason`, as "Why" and "Changed"; the served `label` is the row's secondary line) and CLI, no action the app may
  not take. A profile row with a null subject keys on its own id.
- **The pulse and Mark all seen.** A new pulse-urgency row tints its row and pulses the header once; a row is seen after 1 s on screen and
  focused (`HomePage` reports focus in a row to `Seen.Watch`; the tracker's timers hop to the UI thread before anything bound changes), and a row under the pointer
  holds new rows (`NeedsYou.PointerOver`). The pulse moves the row's background between `Brush.AttentionSoft` and `Brush.Surface1`, never its opacity (text
  contrast, WCAG 1.4.3); a failure keeps a steady attention tint, a calm row a subtle `Brush.Surface2` one; a stale row has a muted background; a row that
  arrives after the first list slides in over 180 ms (never under reduced motion). Mark all seen marks every row and offers Undo for 10 s (it waits while the
  Undo button has focus or the pointer): the panel header shows "N marked seen · Undo" (the button is named "Undo mark all seen", a plain
  tab stop; a polite live region, `NeedsYouSeenLive`, speaks the line once when it is offered; focus on Undo moves on when the offer goes).
  The word "new" shows on any unseen pulse row, with motion or without (reduced motion replaces the pulse with a steady tint and an outline, and the word stays); Settings'
  switch reaches the rows through `ShellViewModel.ApplyAppearance` like the text scale.
- **A refresh never moves focus.** The Needs-you list and the Workflows strip each hold one `ObservableCollection` that every re-read patches
  in place (`Patch.To`, as U2's queue): a row that stays keeps its view model, so the control with focus is not rebuilt; a row's buttons
  are rebuilt only when the served actions differ, and a strip row is kept while what it draws (its served `since` included) is unchanged. A row that
  clears hands the selection to the next row, and focus follows only when it was in the cleared row or is lost: a refresh never takes focus from the
  search field, the top bar or Mark all seen. There is one selection on Home: choosing a queue row (click, focus, `J`/`K`) clears the Needs-you and
  strip selections, and the open-sheet marker (`SheetRowId`) goes when another sheet replaces the row's.
- **What the Needs-you live region says** (`NeedsYouLive`, one polite region; the flag goes off, the text to its resting word, the flag on, then the
  sentence, so a repeat is spoken again): "N now need you" for each rise in the count (a second rise to the same N speaks again; rows that were
  there before a reconnect do not; the first list at startup does, since its rows are new to the user), "<id> cleared" when the selected row leaves
  and "<address> done" when the selected strip instance finishes. Row names say the urgency, the chip, the title and the id ("needs you now, PARKED,
  Retry storm, ITM-01JC8, new"), controls and options say which row they act on ("Accept DEC-…"), and an incident strip row says "incident".
- **Client presentation rules** (the only decisions the app makes; everything else is as served): `G D` shows the rows whose served subject kind is
  `decision` (the summary and the badge still count them all, and the seen state is kept against the whole feed); the strip asks for `root=true&open=true` (the same served filter as the Workflows page; counts stay root-wide, so "... N more" is `counts.open` less the rows shown) and drops, as a belt, any row whose current state's cell is terminal, never keying on a chip word;
  a row the API serves without a `chip` gets the word for its kind (`NeedsYouMapper.Derived`, a fallback tied to U3'-API-9); the strip's park lookup keys
  on the served kind `park`, never on a chip word.
- **Dock badge and tray.** The count of rows that need you goes to the badge through `IDockBadge`: `MacDockBadge` (NSDockTile),
  `WindowsDockBadge` (ITaskbarList3 overlay on the main window), `NullDockBadge` elsewhere. The tray menu lists at most five rows, then Open,
  the disabled Running line and Quit; picking a row only raises `RowRequested`, which opens that row's sheet. The Desktop head composes
  the adapters (`Composition.DockBadge`); it holds no views or view models (ARCH-UI-001).
- **Keys** (Home is shown and focus is not in the sheet; `Esc` is never held back, the other keys wait 500 ms after a refresh moved the selection on its own): `J`/`K` next and previous row, `A` primary action when the app may, `X` the option that turns a decision
  down, `Y` copy the row's command, `Enter` opens the sheet (again: moves into it; so does `Tab` from the row's last stop), `Esc` closes it and returns to the row (to the strip row when `Answer` opened it from there).
- **The item sheet's decisions.** Its option buttons follow the catalog's Decide readiness, read again when the catalog arrives or changes: a pending or cli-only Decide leaves them disabled with the reason as tooltip and help text, and one focusable line under the decision names the phase and the command (UI spec §7).
- **Deep links.** `needs-you/<row id>` and `decision/<DEC id>` open the sheet; the tray, the palette and the item sheet use them.
- **Focus is never lost**: closing a sheet returns to the row; a refused control keeps focus or, if its action is gone, moves to the sheet title.

Key frames (integration lane, saved under `frames/u3-*.png` only when `AW_UI_FRAMES=1` turns Skia on for the run; without it the project draws headless, no PNG is written and the first skipped frame prints an `aw-ui-frames:` line on stderr; a stage's owner check or an evidence run that expects these PNGs must set `AW_UI_FRAMES=1`): `NeedsYouFramesTests` (fixture, empty, stalled, rebuilding, pulse,
seen, reduced motion, show all, Home large) and `DecisionsFramesTests` (decision sheet, decided, 409 decided elsewhere, voided, cleared,
requests, failure and profile sheets). A decision-filing form is not in this PR, so there is no `u3-file-request` frame.

Fixture (`scripts/sindri-ui-fixture.sh`, guarded temp home only): `request [--item ID] [--repo R] <text>` (a session POST with no
caller header), `decide <DEC> <choice>` (as the owner), `fail` / `unfail` (add or remove a service the build cannot run, then approve),
`items --large` (34 items and 12 requests, all in `FIXTURE_REPO` when it is set and with no repo otherwise: the fixture profile has none, and the API refuses a repo the profile lacks), `workflows` (starts each recipe with the inputs it declares, `fact:item` as JSON and `fact:request` as text, prints what it started and exits non-zero with the CLI's message when a start is refused). A request names its item with `item`. `decide` and `request` need #253's routes; `fail` assumes
`profile validate` accepts a profile naming a service the build lacks. Not yet run against a live P4 server.

Left: the CliText control/bidi backstop (`CliText` does not exist in this tree). `PublicAPI.Unshipped.txt` lists the public members U3' adds to the platform seam (`IDockBadge`, `NullDockBadge`, `SindriPlatform.DockBadge`, `TrayRow`, `TrayRowEventArgs`, `TrayView.Count`/`Rows`/`MenuRows`/`MaxRows`/`RunningLabel`, `ITray.RowRequested`, `TrayState.Attention`/`Calm`, `MainWindowHandle`); the view models are internal.

### The Workflows strip (U3' T15-T20, screens spec §4.1)

Home draws the open root instances between Needs you and the two columns (`WorkflowsStripViewModel`, `StripRowViewModel`, `Views/Workflows/StripRow`). Every event re-reads `GET /workflow/instances?root=true&open=true` (`ShellViewModel.ReadStrip`) whatever page is visible, a 404 marks only the strip "arrives with API P11", a list in a shape this build can't read (SND-UI-003; main still serves engine P4's `{instances, needsYou, empty, rows}` with no counts or `asOf` until API P11 and the UI target #250's shape) shows the sentence "The workflow list can't be read from this Sindri yet (arrives with API P11)." and no rows, and any other failure keeps the rows. The API client logs one unreadable answer once for each route and distinct failure (until the route reads well again), not on every event. The app never sorts: rows come in the API's order (incident, triage, work, oldest). The cap is what is left of eight rows after the Needs-you rows shown, at most five (six and two at text scale 1.3 or more), and "... N more" opens Workflows. Each row is one line (chip, subject, recipe, stage tag, age, the served "now" sentence) with at most one control: the desk Answer, or for a protected answer the lock button and its popover; on a narrow page the detail drops to a second line. J / K walk Needs you, the strip, Running and Queued in that order; Enter opens `workflow/<address>`; A answers a desk row (never a protected one): it opens the Needs-you park row for the same instance (a row of the served kind `park` whose subject is the instance's address or id, else the park row of the same item), and when there is none the instance itself. The selection is kept by address across refreshes; a row that turns terminal leaves and is announced as "<address> done". The fold (provisional owner default): a Needs-you row is two lines (about 61 px at scale 1.0; line 1 the chip, id, title and age, line 2 the served sentence, repo, notice and the row's controls; a protected control shows its CLI beside it), so at 1280 × 800 five rows and three strip rows leave the column tops at y = 712. Settings' text scale reaches both lists through `ShellViewModel.ApplyAppearance` (called from Settings' apply callback in `SindriApp`), so the 1.3 caps hold in the app. Fixture verbs: `sindri-ui-fixture.sh workflows [--large]` and `incident start|stop`.

<!-- LANE ANCHOR U3': document the Needs you screens. Insert immediately above this marker, never between the markers. -->
<!-- LANE ANCHOR U8: document the instance screens. Insert immediately below this marker, never between the markers. -->
<!-- LANE ANCHOR U10: document the recipe sheet (U10a) and the Recipes page (U10b). Insert immediately below this marker, never between the markers. -->
<!-- LANE ANCHOR U11a: document the Agents page, the session sheet and the tray line (U11a). Insert immediately below this marker, never between the markers. -->
<!-- LANE ANCHOR U9: document the Context page (U9a to U9e) under the U9 heading. Insert immediately below this marker, never between the markers. -->

**Fact viewers and their views (the seam U13 registers at).** A fact's viewer is made by `FactViewerRegistry` (keyed by `FactType`) and drawn by the view `FactViewTemplates` holds for the viewer's CLR type. U13 adds its scope-map, packet, evidence and decision viewers by one `registry.Register(FactType.X, factory)` line each in `FactViewerRegistry.CreateDefault()`, and one entry per view in `FactViewTemplates`' table (`Register<TViewer>` for a head or test that adds one later); the frame is built with `CreateDefault()` in `ShellViewModel`. A viewer with no view reads as its row's summary, so `FactViewerRegistryTests` fails for any default viewer without a view. The table is static: a registration made in one test stays for the rest of the process, so a test registers only what it removes again.

## U8: Workflows list and the instance frame (screens spec §4.2, plan T1-T8)

U8-min is **read-only**. It draws what the API serves for workflows and sends nothing: every command shows as the served CLI with a Copy and the chip "Available with U8b" (a protected one is the lock button and its popover). The sends (desk complete, `B` hand back, the confirmed cancel), the Timeline, Decisions and Requests tabs' content, and the item-sheet re-base come later; Start is U5' below.

**Views.** `WorkflowsViewModel` (`Views/Workflows/WorkflowsPage`, the Workflows page) reads `GET /sindri/workflow/instances?root=true&open=true` on connect and on every event (whatever page is visible, as the strip does) and groups the rows by recipe id in the order the API first serves each recipe; a group's rows keep the served order and its columns are the states its rows serve. The head ("8 open · 2 need you · 1 with you") and the tab's count are `counts` as served, root-wide whatever the filters. The Recipe and Subject filters send `recipe=` and `subject=`. `InstanceRowViewModel` maps each served field to words (subject label, a cell per declared input and per state, the served chip and `now`); nothing is worked out from the recipe. `InstanceFrameViewModel` (`Views/Instance/InstanceFrameView`, in the sheet at 760 px, the widest) reads one instance by its opaque id (`GET /sindri/workflow/instances/{id}`; an address is only ever a query value, so a deep link `workflow/<address>` is resolved through the list first) and holds: the header (the status header's served `state`, plus `: <detail>` when the engine serves one; the loop round stays on the band), the subject line (Open in, only `http`/`https`), the states band (`StatesBandViewModel`, in the served order), the expanded state (`ExpandedStateViewModel`: step, gates, the whole `next` sentence, budget, timeout, paused, drift, the commands, the child pointer), the facts panel (`FactsPanelViewModel` with one viewer per fact type through `FactViewerRegistry`; payload and text read `GET /sindri/workflow/instances/{id}/facts/{key}`), the children panel (`ChildrenPanelViewModel`, grouped by the served workstream, "… N more" and Open all in Work) and the tab strip (`FrameTabsViewModel`: Timeline, Decisions, Requests and Context with the served `counts`; a null `context` hides its number; each tab names the phase that makes it live, "arrives with U8b" or "arrives with U9"). Back returns along a trail (a child frame or an item sheet opened from a frame); Esc closes everything and returns focus to the row that opened the frame. The recipe sheet is not here (U10).

**Scope-map viewer (demo slice of U13).** A `scope-map` fact in the facts panel draws as a table instead of the payload tree (`ScopeMapViewer`, `Views/Facts/ScopeMapFactView`, registered in `FactViewerRegistry.CreateDefault` and `FactViewTemplates`). It reads the body as a JSON tree, never a strict record, because two shapes exist: the API golden (`surfaces[]` of `name`, `graph`, `files`, `items`) and the scoping schema's fact (`surfaces[]` of `id`, `key`, `kind`, `title`, `detail`, `citations`, `lens`, `graph`, plus `workstreams`, `flagged`, `excluded`, `unresolved`). A counts line ("4 surfaces · 2 mapped · 1 new · 1 unmapped") sits on top; each surface is a row with its title (`title`, else `name`), id, kind and lens, the graph chip (▣ mapped, ◇ new, ░ unmapped; any other word is drawn raw) and a files or citations count; the four extra sections show a title and one line per entry when present. Show raw swaps the table for the payload tree and back. A body with no `surfaces` array shows the payload tree. Every word is in `Labels.ScopeMap.cs`; each text sits in a Border wrapper named for assistive tech, walked by `FactsFramesTests.u13_frame_facts_scope_map_table`. Read-only: it sends nothing.

**Keys** (Workflows page, `WorkflowsViewModel.Keys.cs`; the window passes a key only when no text field has focus and no layer is open): `J` / `K` walk the drawn rows across the groups (a collapsed group's rows are skipped); the selection is an address and focus follows it. A re-read keeps the selection on the same address wherever the row moved and clears it when the row is gone, so a key never acts on a row the human has left (focus then stays where it is, and the window keeps it on something usable). `Enter` opens the frame. `A` on a desk task opens the frame with focus on the output the desk step awaits (its Copy); `A` on a protected answer opens that row's lock popover (never sends); on any other row it does nothing. `X` opens the frame with the Cancel question showing: the served confirm sentence, Keep focused, and "Cancel it" only says that sending arrives with U8b. `B` and `N` name their phase ("Hand back arrives with U8b", "Start arrives with U5'") in a note under the head. Every key obeys the spec §8 guard (nothing for 500 ms after the app moved the selection itself).

**Pre-P11: "unreadable".** Until engine/API P11 (#272) merges, main serves the workflow routes in the engine's older shape: `GET /workflow/instances` answers `{instances, needsYou, empty, rows}` with no `counts` or `asOf`, and `GET /workflow/instances/{id}` answers `{instance: …}`. The client reads strictly (`AwJson` requires every constructor parameter), so the read fails as SND-UI-003 and the page, the Home strip and the frame say "can't read" (one sentence, `Labels.StripUnreadable`) and show no rows: not "not served", because the route exists. `GET /workflow/recipes/{id}` with no hash and `…/facts/{key}` are unmapped on main and answer a bare 404, which reads as unreadable too. A failed re-read of a frame already shown keeps what was shown. Nothing here is a bug to work around in the app; the goldens are the contract and #272 brings the engine to them.

**Answer in the child (engine #298).** An answer a child instance decides is served with `via: "child"` (status `answer`, a list row's `answer`, a needs-you row's `via`) and an optional `child` address; its cli is a placeholder (`... answer <addr> <the child's answer>`), not a runnable answer. A row or expanded state with `via == "child"` (ordinal compare against `InstanceRules`' one constant, never the cli text) draws "Answer in the child" (`ChildAnswerViewModel`, `ChildAnswerButton`) instead of the Answer button or the lock, builds no `ProtectedAction`, and a needs-you row's `Cli` is emptied so nothing copies the placeholder. Following it: with `answer.child`, the child's frame opens (a list or strip row by that address; the expanded state by the pointer's id, only when `status.child.address` equals `answer.child`); with none, or when no served pointer matches, the parent's frame opens and focus is asked for its Children panel (`FrameIntent.FocusChildren`, `FrameFocus.Children`: the first child's Open button, falling back to the frame's first read action when the frame has no children). A needs-you row with `via == "child"` is served the child's address as `child` (engine P7; omitted when null, and ignored on a row that is not via child): the control opens that child's frame directly (`NeedsYouEntry.Child`, `ChildAnswerViewModel.ForNeedsYouRow`). Served none (an older engine), it keeps the #345 fallback: the parent's frame when a strip or list row knows the row's subject (address or id), else the row's own sheet. The child is never guessed. The control is a Button whose accessible name leads with the drawn words (WCAG 2.5.3) and names where it goes: "Answer in the child <child>" when it opens a served child (a row, a needs-you row or the expanded state); "Answer in the child, opens <parent> children" on an instance row with no child; "Answer in the child, opens <shown id>" on a needs-you row served no child; "Answer in the child, moves to the Children panel" in the expanded state with no matching child. Tests: `ChildAnswerTests`, `NeedsYouChildAnswerTests`, `ExpandedChildAnswerTests`, `ShellChildAnswerTests`, `ChildAnswerButtonTests` A child's served state (`status.child.state`, a child row's `state`) is an open recipe-state string drawn as plain text; `unreachable` is the one fixed value and is its own case, drawn with a warning mark and the word (`Labels.ChildStateWords`). Answers to a mirrored parent are forwarded to the child by the engine; the app sends no command (U8b), and the parent's cancel, takeover and handback stay the parent's served commands, never aimed at the child (`ChildStateTests`). (goldens: `needs-you/park-mirrored-child`, `workflow-status/{escalated-child,mirrored-budget,mirrored-budget-no-child,unreachable-child,parked-child}`, `Layout/Golden/instance-summary-{escalated-child,parked-mirrored-budget}`).

**Provisional defaults** (owner decisions pending; each reversible and recorded in the plan's §3): the page is read-only (U8-min); every recipe group starts expanded and the human collapses one (a re-read keeps it); one root-wide "N finished" line (from `counts.done`, which counts every finished run, cancelled and failed included; owner q25, 2026-10-19) expands to the finished root rows (no per-group figure until the API serves one, Q-API-3); a state that needs a missing required input shows the blocked dash on the instance's current state's cell, since the list does not say which stage needs it; a command is shown as its CLI plus Copy with "Available with U8b"; the header draws `state` plus `: detail`, not the round (Q-OWN-1); the screens spec's drawings are illustrative and the recipe says which states exist (Q-OWN-2); a child's mirrored park shows on the child row only (Q-API-5b); a taken-over cell reads its served reason behind a mark.

**Tests.** `WorkflowsViewModelTests`, `InstanceRowViewModelTests`, `WorkflowsKeysTests`, `InstanceFrameViewModelTests`, `StatesBandViewModelTests`, `ExpandedStateViewModelTests`, `FrameTabsViewModelTests`, `ChildrenPanelViewModelTests` and the facts tests (unit); `ShellWorkflowsTests`, `ShellInstanceTests`, `WorkflowsKeysViewTests` (headless focus tests, in `[Collection(HeadlessWindows.Name)]`). Key frames (integration lane, saved under `frames/u8-*.png`): `WorkflowsFramesTests` (open, recipe filter, empty, stalled, unreadable), `InstanceFramesTests`, `FactsFramesTests`, `ChildrenFramesTests` and `WorkflowsKeysViewTests` (selected, pending output, cancel question, lock popover, tabs). Every literal is read from the API goldens (`tests/Aw.Sindri.Tests/Golden/Api/workflow-instances`, `workflow-instance`, `workflow-instance-fact`), never typed.

**The list (F3 workflows slice 3, `RunListView`).** The `List` view is a sticky column head (Item, Stage, Status, Step, Waiting on, Updated, outside the scrolled rows) over a Surface2 group row per workflow and a six-column row (`InstanceRow`: the title button with the mono reference under it, the stage lane or state, the one status badge, the served `now`, who it waits on, and the age since `since`). Inputs, every stage and the address with Copy no longer sit in the row; they move to the run's drawer (slice 4). The list measures its own width and `ListColumns.For(width)` picks the columns it keeps: from 960 all six, under 960 Stage and Waiting on go, under 560 Step, Updated and the reference go too (a hidden column is 0 wide and its cells are hidden; `ListBands` hands the choice to the head and the rows). J / K / V / `/` walk and switch both views (`Walk` follows the view). The finished runs (the Done lane, and the list's finished line) read `GET /sindri/workflow/instances?root=true&open=false` (finished only), once for each recipe and subject filter: switching back to the board does not read again unless a filter or a refresh passed. `InstanceQuery.Open` is `bool?` (null asks for both).

**Proof recipe** (scripts `scripts/sindri-prove.sh` and `scripts/sindri-ui-fixture.sh`, guarded temp home; evidence is OS screenshots plus a CLI cross-check, since #272 makes `--json` equal the route body). Before #272 (main only): press `G W` and see the Workflows page (not a placeholder) say "can't read" with nothing crashing; open `workflow/<address>` (or the strip's Enter) and see the frame sheet at 760 px in the same state, with Back; the item sheet is unchanged at its own width; a lock popover opens on an existing host. After #272: `sindri-ui-fixture.sh workflows` and check the groups follow the served order and the head equals `counts`; press `J` through every group, `Enter` on one of each recipe (same chrome, band in the served order), `A` on the desk task (focus on the pending output) and on the escalated one (the popover; Copy it and run it at a terminal), `X` then Keep; compare with `sindri workflow list --json` and `sindri workflow status <address>`; open a payload or text fact with View; run `sindri-ui-fixture.sh incident` and see the incident group first with Attach showing the pending chip. The five-second question: which workflow is furthest along, and what is it waiting on?

## U10a: the recipe sheet (screens spec §4.3)

A read-only sheet for `recipe/<id>[@<hash>]` (`RecipeSheetViewModel`, `Views/Recipes/RecipeSheetView`). It reads only `GET /sindri/workflow/recipes/{id}[@{hash}]` (`ISindriApi.Recipe`) and sends nothing. The inspector without stats is this sheet; the Recipes list page, with the stats of E-24 and E-25, is **U10b** and stays a placeholder here.

**Route and ways in.** `DeepLink` accepts one `@` in a `recipe/` subject (`recipe/workflow.project@a8c21e7f0000`; both halves are ordinary subjects, and no other kind takes an `@`). The hash is whole or a prefix of eight or more hex digits, as the API takes it. Three ways in: the deep link (the sheet opens over the Recipes placeholder page), the instance frame's "Recipe ▸" link (opens the version the instance is pinned to, `recipe/<id>@<hash12>`; the sheet stacks on the frame's trail, so Back returns to the frame), and the "Recipe ▸" button beside each Workflows group head (opens `recipe/<id>`, the approved version). Esc ends the whole stack, as it does for a child, and returns focus to the opener (the Workflows row that opened the first frame, or the group head's Recipe button); Back from a stacked recipe returns focus to the frame's Recipe link. A recipe answer in a shape this build can't read says so in its own sentence. The sheet is 760 wide like the instance frame (`ShownSheetWidth`; the width the human chose for other sheets stays). A bare id re-reads on every event like the frame does; a failed re-read keeps what is shown and a not-found one adds the note "no longer served".

**What it shows.** The header is `<id>@<hash12> · v<version> · <urgency>` with the name template when it is served; the sheet's accessible name stays "Recipe X". The states band lists the states in served order, with no link between them unless a loop is served (the recipe serves no transitions, and path order is a list, so a link would invent one, such as done leading to dropped); a state with `branchOf` folds under its source; a loop between neighbouring states is drawn between them with its name and cap (`⇄ rounds ≤2`); any other loop (into the same state, or back over others; two loops can share a name) is a note on the state it leaves; `orderedBy: transition-list` says so; each output sits under the state it comes from. Inputs and outputs follow in declared order: label (the key when the label is null), key, type (a word this build does not know reads as `payload`), kind, required or optional, the generate template when served, and for an output its source state. A recipe with no outputs says so. A not-found shows the served SND-RECIPE-002 detail and any other refusal its code and detail, an unreadable answer its own sentence about the recipe, each with Back. Every name keeps its visible text (WCAG 2.5.3) on a Border wrapper (Control view, Text type, inner text Raw), as the instance frame's band does.

**Gaps (provisional defaults; each is an API ask).** G1: the recipe serves no terminal, desk or child mark on its states, so none is drawn and none is guessed from position. G2: approval standing, date, drift and stale are on the list summary only, so they are not shown. G3: gates per state are not served. G4: schema version, pinned blocks, evals, open instances, Versions, Run evals and Validate are U10 proper. G5: stats are out of scope. G6: `GET /workflow/recipes` has no golden, which blocks U10b. G7: SND-RECIPE-003 (no approved version) has no golden; any refusal shows through the one path. G8: the recipe route serves the whole hash, so the sheet shows its first 12 hex digits, the form the CLI prints.

**Tests.** `RecipeSheetViewModelTests`, `RecipeStatesViewModelTests`, `RecipeFactsViewModelTests`, `ShellRecipeTests` and the `DeepLink` cases (unit); `RecipeFramesTests` (integration; key frames `u10a-recipe-*`, with the automation tree walked from the sheet down). Every literal is read from `tests/Aw.Sindri.Tests/Golden/Api/workflow-recipe/*`; the fake serves a recipe by a hash prefix as the API does.

## U5': the Start form (screens spec §4.2)

`N` on Workflows, the Workflows head's and Home's "Start workflow" buttons (Home's carries no key hint: `N` is Workflows'), Ctrl+N (⌘N) from any page and the palette's "Start workflow" entry open a modal form (`StartDialog`, `StartFormViewModel`). With an item sheet open it starts for that item.

- **Recipes.** The picker lists `GET /sindri/workflow/recipes` and keeps only `standing == "approved"`, one row for each id at its highest version (unapproved and revoked recipes are hidden: a start of one is refused anyway). The list is used only to pick; the inputs come from `GET /sindri/workflow/recipes/{id}`.
- **Item.** The subject is the item, typed or picked from the queue's items. A recipe's `item` input has no field of its own: the picked item is sent as the request's `item` and as the `fact:item` JSON, `{"id": …, "repo": …}` when the item the queue lists has a repo (the engine's item fact is `{id, repo, spec}` and `effect.claim.ref` reads the repo from it) and `{"id": …}` alone for a typed id the queue does not list; the API requires the id to agree (SND-WF-014). The API fills a missing repo itself once the item store lands (P3), and an explicit one still wins.
- **Fields** by the input's fact type: text box (`text`), link box (`link`), file (`file`, at most 1 MiB, name and size shown, chosen through `IFilePicker`: the form's constructor takes it, and the shell receives it with the launcher as one `WindowPorts` value; `AvaloniaFilePicker` chooses through the window's storage provider and `StreamFilePicker` reads the file and applies the size limit, the one code the `FilePickerContract` tests reach without a window), JSON text (`payload` and any type this build has no field for). An input with `generate` is optional and says "generated if empty". Content travels base64 on the wire.
- **Send.** Ctrl+Enter or the button, once: the guard is set before the first await, a refusal leaves the form pressable, only an accepted start marks it sent, nothing is retried and no nonce is sent. Readiness comes from the catalog row `StartInstance` and is re-read when the command list arrives while the form is open. Start is off while the chosen recipe's inputs are read, and says why in its help text. Esc, Cancel or the scrim ends the form's reads and sends: an answer still on its way opens no frame. Choosing another item or recipe starts the form clean (the sent state, the outcome line and the new-attempt offer are about the item and recipe they answered).
- **Refusals** show the served code and detail inline, verbatim, and focus moves to the error line; an input that cannot be sent takes focus itself and carries its sentence as help text, as do the recipe picker and the item box. A field the form can see is wrong (required, not JSON, not a link, over 1 MiB) is refused before anything is sent.
- **Success** opens the instance frame by its address. An attempt that already exists says "already running"; a finished one says so and offers "Start a new attempt", a second explicit click that sends `nextAttempt: true`.
- **Focus.** An accepted new start closes the form and focus goes to the new frame, not back to what opened the form. An existing attempt keeps the form open: the Start button just pressed is off, so focus goes to "Start a new attempt" when it is offered, else to the outcome line (it is never left nowhere), and the instance's frame, opened behind the form, takes focus when the form closes. The outcome, refusals, read failures, readiness and progress are spoken through one polite live region, `StartLive`, on `CreateItemViewModel.Say`'s pattern (always in the tree, live only while it speaks, resting text while dark, so a repeated sentence is spoken again; a new opening starts at rest).
- **Accessibility.** Each field is named by the visible caption (with "(required)"), carries its error or "generated if empty" as help text, and the file button is named by its input and the file chosen; the lines that change are one static `Border` wrapper named by a view-model string with the text inside raw. Esc closes; focus lands on the recipe picker on open.

**Tests (U5').** `StartFormOpenTests`, `StartFormSendTests` and `StartFormStateTests` drive the view model over the fake API (goldens from `tests/Aw.Sindri.Testing/Golden`), including late and stale reads through the fake's `RecipesGate`, `RecipeGate` and `StartGate`; `StartDialogTests` drives the dialog (openers, the picker, the file button, the new-attempt button, focus and live region) and draws two key frames; `FakeFilePickerContractTests` and `StreamFilePickerTests` run `FilePickerContract`.

## U9: the Context page (screens spec §4.4)

U9 builds the Context page in stages that run in parallel after U9a0: the lens stack (U9a), the node card and sources (U9b), the map review queue (U9c), the file map (U9d) and the pinned "as of the stop" mode (U9e, blocked on other lanes). The plan is `u9-plan.md`.

### U9a0: contract, seams and lane anchors (no visible change)

**Contract.** The context routes are served (context spec §8; Sindri's goldens `context-lenses`, `context-filemap`, `context-map`, `context-map-review`, `context-map-review-history`, `context-map-promote`, `context-node`). The app mirrors them in `Api/Context/` and reads them through `SindriUiJsonContext`. `ISindriApi` gains `Lenses`, `FileMap`, `ReviewQueue`, `ReviewDecision`, `ReviewHistory`, `MapStatus`, `Node` and `Sources` (the client's are in `SindriApiClient.Context.cs`, the paths in `ContextRoutes`). A key, an anchor or a path is only ever a query value, escaped, and each anchor of a set is a repeated `anchor=` value; the answers are reads of the context database, so none carries `X-Sindri-After`. `ReviewDecision` is a desk send, once and never retried; the protected verbs (apply-file, merge, add, delete, promote) are never sent, the page shows the catalog's `cli`.

**Words are strings.** Every served word (a group, a status, a conflict kind, a source state) is a string; `ContextWords` lists the ones this build knows and `ContextWords.Known` says whether a word is one of them, so a word added later reads and is drawn raw instead of making the page unreadable. `LensesView.Anchors` is the set in both schema 1 (`anchor`) and schema 2 (`anchors`); a band item belongs to the list it is in (`own`, `neighbors`, `affects`); `mapCommit` is absent when the map has none. A context refusal carries its extra members as `Error.Extensions` (`suggestedAnchors` of SND-CONTEXT-003, `candidates` of -037).

**Not served yet.** `GET /sindri/context/sources` has no golden, so the fake answers it as not served; the lenses have no `draft=true` success golden, so the fake answers 044 for a draft. Neighbors, path and query have no success goldens and are not in the contract.

**Seams.** `ContextPageViewModel` (U9a) holds four stage view models, `MapReviewViewModel` (U9c), `FileMapViewModel` (U9d), `NodeCardViewModel` and `SourcesPanelViewModel` (U9b), each created by an `Init*` partial named for its stage (`.Review.cs`, `.FileMap.cs`, `.Side.cs`) and each taking the page's one `ContextPorts` (`ViewModels/Context/ContextPorts.cs`: the API, its URL, the clipboard, the clock, the catalog and `Follow`, the shell's re-read-on-connect hook; U9a0 owns the record, append-only; it is a positional record, so a new port is a new member there plus a new argument in `InitContext` (`ShellViewModel.Context.cs`), and no stage edits the page's constructor; a stage registers its own refresh with `ports.Follow` from its own view model). `Views/Context/ContextPage.axaml` hosts each stage's seam view, bound to that stage's view model: `Views/Context/Review/MapReviewPanel` (`ReviewHost`), `FileMap/FileMapTree` (`FileMapHost`), `Node/NodeCard` (`NodeHost`) and `Sources/SourcesPanel` (`SourcesHost`); only `LensStackHost` is a bare panel, U9a's own. A stage fills its seam view and its view model and never edits the page. `ShellViewModel.Context.cs` (`InitContext`) builds the ports. The page is mounted in `ShellWindow.axaml` in place of the placeholder; it opens empty (an empty, hidden head: "no map" is for the served no-map case U9a wires) with focus in the anchor box, and reads nothing before an anchor.

**Tests.** `ContextGoldenTests`, `SindriApiClientContextTests`, the context cases of the API contract (`SindriApiContract.Context.cs`, fake and client), `ContextPageViewModelTests`, `LaneAnchorTests` (unit); `ContextPageWindowTests` (integration, one window).

## U11a: the Agents page, the session sheet and the tray line

The contract (T0): `GET /sindri/sessions` and `GET /sindri/sessions/{provider}/{id}` (`ISindriApi.Sessions`, `.Session`), the `Api/Sessions` records, the fake and the handler arms, and the `session/<provider>/<id>` deep link.

**Views.** `Views/Agents/AgentsPage` (the head "N live · M asking · K outside a workflow", the provider, state and repo filters, the problem lines, the groups and the "Recently ended" fold), `SessionGroup`, `SessionRow` (chip, provider and title as a button that opens the sheet, where it runs, how long, and the pending Focus with its copy line), `EndedSessionRow` (with the resume command and Copy) and `SessionSheetView` (the 520 px sheet: cwd, host, socket, state history, the pending Focus, and `LivePreviewHost`, the collapsed seam CO-7c fills). Rows are kept by ref across reads, so a refresh moves neither focus nor scroll. Every named line is a Border wrapper, and a row's name is provider, title, state, what it asks and host.

**Routes and codes.** The page reads `GET /sindri/sessions` every 2 s while it is shown and the window is not minimised; the sheet reads `GET /sindri/sessions/{provider}/{id}` and again on every event while open. `SND-SESSION-002` (the collector cannot see sessions on this system) is the page's one line, with no rows and the filters disabled; `SND-SESSION-404` is the sheet's "This session has ended." (the sheet stays open, Focus disabled); `SND-SESSION-001` and `-003` are problem lines above a list that still draws. None of them is a Not responding banner.

**Keys.** `G A` opens the page; `J` / `K` walk the rows; Enter or the title opens the sheet and Esc returns focus to the row that opened it; `O` moves focus to the row's copy line; `Y` copies it (an ended row's resume command); `E` opens or closes the fold.

**Counts.** The Agents tab shows `status.sessions.live`, and the tray tooltip adds "N agents · M asking". The page's live region says "N agents · M asking" once when the figures change, only while the page is shown and never for the first figures (`AgentsPageViewModel.ObserveCounts`, fed by the list and by the status). The sheet's name is "Session <provider> <title or id8>".

**Pending.** Focus is the catalog's `pending:CO-4a` control: disabled, with the CLI line `sindri agents focus <ref>` to copy; nothing is sent until U11a-f, after CO-4a serves its goldens. The live preview is CO-7c: `LivePreviewHost` exists, is silent, and nothing polls `/preview`. Recipe groups, Attach, Take over and budgets are U11.

**Proof.** The owner's proof row (collector spec CO section 11, U11a): Focus on a lane brings its Warp tab forward or shows the copy line, run on the real box in U11a-f. The app proof runs on a private Xvfb with `scripts/sindri-ui-fixture.sh up` then `sessions up|ask|end|back|disable|enable|down` and AT-SPI dumps; the journeys are in the verification plan (J1 to J15).

**Tests.** `Agents/Page` (view model, keys, poll: unit; `AgentsPageTests`: integration), `Agents/Sheet`, `Shell/AgentCountsTests`, `LaneAnchorTests`.
