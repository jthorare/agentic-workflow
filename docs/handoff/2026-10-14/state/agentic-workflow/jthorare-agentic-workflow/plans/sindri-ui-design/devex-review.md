# Plan Devex Review — sindri-ui-design

**Plan:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md (worktree `scratchpad/ui-design`)
**Reviewed:** 2026-10-11

## Summary
The spec is unusually concrete for a UI design: every control maps to a catalog action and phase (§7), every screen has a state matrix (§4.9), and the Phases table gives each PR an observable signal. The main problem is **provability**. Several phase signals rely on things that nothing in this spec or its parent specs builds: the "fixture ledger" behind the five-second test, a way to stall the projector on demand, a way to hide Warp from the probe, a way to toggle reduce-motion and read tray/dock state on the OS, and the mockups the design reviewer diffs against (the `mockups/` folder is empty today). The dependency gating is also loose. The API edits E-1…E-11 that U1, U3, U5 and U6 need are not owned by any API phase, and U5 depends on an unnamed "first API phase after WF-P4". Finally, the fake `ISindriApi` is called "contract-tested", but the spec never says what it is contracted against, even though the UI package holds no Sindri reference and so redefines every DTO. Time-to-first-success: about 45 minutes for U0 (S1 merged and S1b rebased assumed), and about 2–3 hours for the first proven U6 launch on a fresh machine.

(Plan layout note: this is a single spec file, not a `plan.md`/`engineering.md`/`TASKS.md` directory. The engineering surface is §6.7 and §9, and the task breakdown is "Phases" and "Parallel delivery".)

## Findings by severity

### CRITICAL
None.

### HIGH

- **Ramp-up / provability:** The five-second test and the U2 signal depend on a "fixture ledger" that no phase builds, and the spec cites the wrong section for it.
  - Evidence: line 58–59: "from a cold start with the fixture ledger (§16)". But §16 (line 790) is Mockups, and it only says the mockups use "the fixture data from the shotgun brief". Line 818: "with the fixture ledger (§12 of the API spec: `sindri-prove.sh`, a temp home)". API §12 (lines 848–866) defines the wrapper and a temp home, but no seeded scenario (absent: searched "fixture" in the API spec, where only the wrapper matches). U2's signal (line 824) asserts "the five-second test passes on the fixture ledger".
  - Fix: Add a deliverable to U0 (or U2's contract unit): `scripts/sindri-ui-fixture.sh` (or `sindri-prove.sh seed ui`). It builds the shotgun-brief scenario through real commands only (`human item add`, `curl` requests filed as a session, a failing test service, a profile edit, and later dev recipes for parks), so it stays inside "no fixture code ships in the binary" (WF W24). List which rows each phase can seed (no park rows before WF-P4). Fix the §2 reference.

- **Error/observability / provability:** U1's signal "Stalling the projector (test projection)" has no mechanism behind it.
  - Evidence: line 823. API P2 (API line 1052) ships "Rebuild and stall handling" but no way to inject a stall (absent: searched "test projection", "poison", "fail-on" in the API spec).
  - Fix: Name the trigger. One option is a dev-only poison event appended through `sindri-prove.sh` that a test projection rejects, gated to a temp home. Another is moving the stalled-banner proof into a headless test against the fake, with the live proof limited to `rebuilding` (which P2 can already produce with `views rebuild`). Add the chosen hook to API P2's file list as an E-edit.

- **API surface / dependency gating:** No API phase owns E-1…E-11, yet UI phases depend on them earlier than P10.
  - Evidence: line 443: "These edits change API P10 and add to its phase". But E-7 (`NeedsYouRow.actions`) is needed by U3 (line 825, "API P4 (+ E-7)"). E-8 (`destructive`, `shortcut`) is needed by U1's control renderer and §8's `X` confirmation. E-9 is needed by U5. E-4 needs S1's closed profile schema. API P4 (API line 1054) and P1 (line 1051) don't mention them.
  - Fix: Give each E-edit an owning phase: E-7 → API P4, E-8 → API P1, E-9 → WF-P4, E-4 → S1/P2, E-1–E-3, E-5, E-6, E-10, E-11 → P10. Make "the API spec revised with E-n" an explicit precondition in the UI wave table. Otherwise a U3 implementer finds `actions` missing from the real `/needs-you` only at proof time.

- **Ramp-up / dependency gating:** U5's dependency names a phase that doesn't exist yet, and the wave notes contradict the phase table.
  - Evidence: line 571: "Item state from the engine … first API phase after WF-P4". Line 827: U5 depends on "X7 projection". Line 823: U1 depends on "API P1, P2, P5", but line 852 says "Waits for API P1 … P2/P5 parts can follow".
  - Fix: Name the API phase that ships the X7 projection, or add one to the API spec. Then split U1 into units with their own gates: catalog and renderer → P1; `asOf`, stalled and rebuild → P2; event table and `Last-Event-ID` → P5. Make the wave board say exactly which unit unblocks on which phase merge.

- **API surface / test strategy:** The fake `ISindriApi` is "contract-tested", but the contract has no source of truth. The UI holds no Sindri reference, so every record is a hand-copied duplicate.
  - Evidence: line 833: "written against the contract-tested fake `ISindriApi` before its API phase merges". Line 860: "their in-memory fake with its contract test". csharp.md: `Aw.Sindri.Ui` "references no `Sindri` project". Nothing says what the contract test runs against, or how JSON drift between `Aw.Sindri` and `SindriUiJsonContext` gets caught.
  - Fix: Specify a shared contract base (`Aw.Sindri.Ui.Testing`) that runs against two implementations: the in-memory fake (unit lane), and the real `HttpSindriApi` against a real `sindri serve` in a temp home (integration lane), which is enabled once the API phase merges. Also add golden JSON response files. The API phase's tests write them (or `curl | jq -S` from `sindri-prove.sh`), the UI's deserialization tests read them, and the C# tests define the contract. A U-phase isn't "proven" until the real-server contract run is green.

- **Provability (evidence):** The OS-screenshot proof and the five-second test have no runnable recipe.
  - Evidence: line 871: "captures **OS window screenshots** (Screen Recording granted …), runs the five-second test and the keyboard-only pass". There is no capture command, no window-id lookup, no definition of who the "fresh observer" is (a subagent reading the PNG? the owner?), and no pass criterion.
  - Fix: Add a prover recipe: `scripts/sindri-ui-capture.sh <name>`. It gets the `sindri-ui` window id (for example through `osascript`/`CGWindowListCopyWindowInfo`), runs `screencapture -o -l<id>`, and writes into the evidence dir. Define the five-second test as a fresh subagent given only the PNG and the four questions, whose answers are compared with the fixture's known truth. Define the keyboard-only pass as a scripted key sequence (sent with `osascript` `System Events`, or `xdotool` on Linux) with a capture after each step. Say which OS the proof runs on (macOS only for v0?).

- **Provability (launcher, U6):** The U6 signal needs real Warp, a logged-in agent CLI, and a probe override that doesn't exist. Its "session's first `sindri_item`" check has no evidence path.
  - Evidence: line 828: "with Warp hidden from the probe, the button reads 'Warp isn't installed'". §6.3 step 2 shells out with `open`. E-3's probe is server-side with no injection point (absent: searched "probe override", "AW_SINDRI_LAUNCH_PROBE", "fake launcher").
  - Fix: (a) Add a recording `ILauncher` fake and an `ILaunchProbe` port, so the integration lane covers "Sent to Warp", every §6.6 failure, and the blocker rendering without Warp. (b) Add a temp-home-only override (`AW_SINDRI_LAUNCH_PROBE=absent:warp`, honoured only when `sindri-prove.sh`'s guard is set) for the live "Warp isn't installed" proof. (c) Prove the session's first `sindri_item` call from the Claude transcript at `~/.claude/projects/<cwd-slug>/<session-id>.jsonl`, which is findable because Sindri picks `--session-id`. Use `claude -p` with a fixed tiny prompt to cap cost. (d) Record which targets the prover must verify (Claude Code required; Codex optional; Cursor explicitly unverified, per line 340).

- **Docs / design-review gate:** The design reviewer and §16 depend on mockups that don't exist yet.
  - Evidence: lines 795–808 list 11 `mockups/*.html` files. The `2026-10-11-sindri-ui-design/mockups/` directory is empty (only `shotgun/` is populated). Line 885: done requires "the design review passed against the mockups".
  - Fix: Either commit the mockups before approval, or mark each one with the phase that needs it and make "mockup exists and is approved" a precondition of that phase's design review (U0 needs `home.html` and `states.html`; U6 needs `launch-flow.html`).

### MEDIUM

- **Avalonia specifics (tray/dock):** `TrayIcon` and the dock badge are hard to prove and differ by platform, but the spec treats them as one-line head adapters.
  - Evidence: line 649: "The tray uses Avalonia's `TrayIcon`; the dock or taskbar badge and `IMotionPreference` are head adapters". Line 825: `Platform/IDockBadge.cs`. Avalonia has no dock-badge API (macOS needs `NSApp.dockTile.badgeLabel` through ObjC interop, Windows needs `ITaskbarList3` overlay icons, and Linux has no standard). A coloured tray icon with a count needs a runtime-rendered bitmap, and macOS template icons are monochrome. GNOME shows no tray without an extension.
  - Fix: Add a per-platform support table (macOS: tray + dock badge; Windows: tray + overlay; Linux: tray where StatusNotifier exists, no badge). Name the interop approach (`LibraryImport` + `objc_msgSend`, AOT-safe, with no new package unless approved). Add a fake `ITrayPresenter`/`IDockBadge` so the view-model tests assert the tray state, menu rows and badge count. Make the OS capture of the menu bar/dock an explicit U3 prover step.

- **Avalonia specifics (live region):** Neither the polite live region nor the "cleared" announcement has a proof, and Avalonia's support varies by platform.
  - Evidence: line 311: "One polite live region announces changes". Line 147. There is no signal in U2/U3 (absent: searched "VoiceOver", "LiveSetting", "announce" in Phases). Avalonia's `AutomationProperties.LiveSetting` reaches macOS accessibility notifications only partially, depending on version.
  - Fix: Name the mechanism (`AutomationProperties.LiveSetting="Polite"` on one `TextBlock` whose text the view model sets). Unit-test the announcement text in the view model, and add a prover step: a VoiceOver on/off check on macOS, or reading the AX tree with `osascript`/Accessibility Inspector. If the Avalonia version in `Directory.Packages.props` doesn't raise the notification on macOS, record it as a known gap.

- **Avalonia specifics (reduced motion):** The prover can't toggle the OS setting deterministically, and the macOS adapter needs ObjC interop that the spec doesn't mention.
  - Evidence: lines 638–640 (`NSWorkspace.accessibilityDisplayShouldReduceMotion`, "re-read on change"). U3 signal (line 825): "reduced motion shows the static outline".
  - Fix: Add an env override honoured by the head (`AW_SINDRI_UI_REDUCE_MOTION=1`) for the proof. Unit-test the motion tokens with a fake `IMotionPreference`. Name the change notification (`NSWorkspaceAccessibilityDisplayOptionsDidChangeNotification`) and the interop approach. Note that `defaults write com.apple.universalaccess reduceMotion` needs privileges the agent sandbox may lack.

- **Hotspots:** The hotspot list leaves out several files that every view unit touches.
  - Evidence: lines 877–879. Missing: the §8 key map/shortcut table, the palette's entry registration, `Theme/FluentOverrides.axaml`, the view-model composition in the head (hand composition, line 783), `preferences.json`'s record and JSON context, and the shared AutomationProperties name helper.
  - Fix: Add these as anchor files with "one line per unit" rules, or make the contract unit pre-register every key and palette section for the phase.

- **Test lane budgets:** Headless frame tests for every state in every phase will push the integration lane's 60 s per-package budget (LANE-PKG-001).
  - Evidence: line 822: "headless frames for every §4.1 state" (10 states × pages). Line 816: each phase adds headless view tests with frames.
  - Fix: Say which frames run in `integration` (one per view and state family) and which go to `heavy`, or plan to split `Aw.Sindri.Ui.Tests` by area so each package keeps its budget.

- **Error messages & observability:** The client has no logging story.
  - Evidence: absent. Searched "log", "Telemetry", "AW_LOG_LEVEL" in the spec. `SND-UI-003`, refresh failures, stream fallbacks (line 549) and launch failures leave no client-side trace.
  - Fix: State that `Aw.Sindri.Ui` logs through `Aw.Kernel.Telemetry` (JSON lines to `$AW_SINDRI_HOME/ui/ui.log` plus stderr) with the SND code, route and correlation id. Also state that `sindri doctor` reads that log's last error.

- **Testability (timing):** The time-based rules (seen after 1 s, stale after 30 s, 250 ms debounce, 3 failed refreshes, the 2 s look-again loop) need an injectable clock, which the spec doesn't name.
  - Evidence: lines 43, 163, 304, 536.
  - Fix: Require `TimeProvider` (with `FakeTimeProvider` in tests) in every view model with a timer, and list those rules as unit-lane tests in U1/U3.

- **Docs completeness:** The spec doesn't list the docs each phase updates, and §17 is still empty while the status says it was revised.
  - Evidence: line 4: "Revised after `/autoplan` (§17 lists every finding …)" versus line 812: "(Filled after `/autoplan`.)". No phase lists `docs/csharp/sindri-ui.md`, DESIGN.md (X22), README X18–X22 or the WF §6.2 edit.
  - Fix: Add a "Docs" column to the Phases table (U0: sindri-ui.md shell, theme and DESIGN.md focus-ring edit; U6: user docs for launch targets and the Screen Recording/Warp prerequisites). Fill in §17 or fix the status line.

### LOW

- **Setup:** Single-instance behaviour (line 787) doesn't name its mechanism (named mutex, a Unix socket in `$AW_SINDRI_HOME/ui/`, or a pid file). Name it, so the U0 prover knows what "focuses the first" depends on. Fix: one sentence in §15.
- **Backwards compatibility:** `preferences.json` (line 669) has no schema version, and "seen" ids accumulate when they're never pruned while offline. Fix: add `"version": 1` and say what happens to an unreadable file (ignore and rewrite).
- **API surface:** E-1 replaces `ItemOpened(accepted, launchedIn)` with `LaunchRequested`. That's harmless because P10 hasn't shipped, but say so explicitly so nobody adds a compatibility shim. Fix: one line in E-1.
- **Ramp-up:** The S1b workaround for `Avalonia.Headless.XUnit` against xUnit v3 4.0.1 lives only in PR #168's doc. Fix: reference it from the Phases preamble.
- **Fonts:** JetBrains Mono "embedded" (line 728) needs its OFL licence file shipped and a `deps`/package decision. Fix: name the asset path and the licence file.

## Per-dimension summary
| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 3 |
| Setup / install (time-to-first-success: ~45 min U0; ~2–3 h first proven U6) | HIGH | 3 |
| Error messages & observability | HIGH | 2 |
| Docs completeness | HIGH | 3 |
| Backwards compatibility | LOW | 2 |
| Ramp-up cost (tribal-knowledge items: 9) | HIGH | 7 |

Time-to-first-success assumptions: .NET 10 SDK per `global.json`, S1 merged and S1b rebased, `sindri` and `sindri-ui` built from the repo. For U6, add installing Warp, a logged-in `claude`, the Screen Recording grant for the prover's terminal, and a profile with a real repo path.

Tribal-knowledge list (unwritten facts a new implementer must be told):
1. How to seed the shotgun-brief scenario into a temp home (the "fixture ledger").
2. How to put the projector into `stalled` on demand.
3. The `Avalonia.Headless.XUnit` / xUnit v3 4.0.1 workaround (`HeadlessUnitTestSession.GetOrStartForAssembly`).
4. Which process needs Screen Recording, and how to get the `sindri-ui` window id for `screencapture -l`.
5. Who the "fresh observer" is in the five-second test, and what passes.
6. How to hide Warp or an agent CLI from the launcher probe in a temp home.
7. Where a launched Claude session's transcript lives (to prove the first `sindri_item`).
8. Which API phase carries each E-edit, and which phase ships the X7 engine item projection.
9. Which platforms must show the tray/dock badge in a proof, and how ObjC/COM interop is done AOT-safely.

## Verdict
The spec's screens, states and action mapping are solid, but eight HIGH findings mean `/specToProvenPR` agents can't currently prove several phases: no fixture-ledger builder, no stall trigger, no probe override, an unowned E-edit gating, an unnamed X7 phase, an unanchored fake contract, no screenshot or five-second recipe, and missing mockups.
verdict: NEEDS_WORK
