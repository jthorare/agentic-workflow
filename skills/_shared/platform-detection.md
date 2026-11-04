# Platform Detection (shared)

Canonical web/iOS/desktop detection and dispatch contract for the 6 dispatcher skills: verify-app, design-analyze, design-evolve, design-mockup, design-implement, design-verify.
Referenced via: SHARED_DIR pattern (CD2).

## Detection

**iOS** — any hit from these glob patterns, ignoring `node_modules/`, `.build/`, `Pods/`, `vendor/`, `external-skills/`:
- `**/Package.swift`
- `**/*.xcodeproj`
- `**/*.xcworkspace`

**Web** — a `package.json` whose dependencies (or devDependencies) include any of: `next`, `react`, `vite`, `vue`, `@angular/core`.

**Desktop** — a desktop UI toolkit the evidence recipe can drive. Today: Avalonia, detected by any `**/*.axaml` file or a `*.csproj`/`Directory.Packages.props` referencing a package named `Avalonia` or `Avalonia.*`, ignoring `bin/`, `obj/`, `node_modules/`, `vendor/`, `external-skills/`. The list is open: another toolkit is added here as one more detection line, and the desktop sub-skills do not change. Desktop verification also needs `<repo>/.agentic-workflow/evidence.json` with a `desktop` section (`_shared/desktop-evidence.md`); a desktop project without it is detected but not verifiable, and the sub-skill stops on that.

Only the dispatchers that have a desktop sub-skill (verify-app → `verify-desktop`, design-verify → `design-verify-desktop`) act on desktop; the others (design-analyze, design-evolve, design-mockup, design-implement) ignore it and resolve web/iOS as before.

A change counts as a **desktop UI change** when its diff touches a detected desktop project's view, style or resource files (`*.axaml`, the code-behind and view-model files beside them, theme and resource dictionaries).

## Platform Resolution

| Detected | Action |
|----------|--------|
| iOS only | Invoke the iOS sub-skill with original arguments |
| Web only | Invoke the web sub-skill with original arguments |
| Desktop only | Invoke the desktop sub-skill with original arguments |
| More than one present | If the diff touches exactly one platform's files, that platform. Otherwise **Ask the user**: "<detected platforms> project files detected. Which platform should I verify? (web / ios / desktop)" → invoke chosen (a change that spans platforms is verified once per platform) |
| None present | If the request names the platform, that platform. Otherwise **Ask the user**: "No iOS, web or desktop project files detected. Which platform should I verify? (web / ios / desktop)" → invoke chosen |

All user-supplied arguments are passed through to the sub-skill unchanged.

## Dispatch contract

1. Before dispatching, write `dispatch: <sub-skill> args=<args>` as its own line in the reply text (not a Bash `echo`)
2. Dispatch literally: **Invoke skill `<sub-skill>`** with args `"<original args verbatim>"`
3. If a required argument is empty, **stop** and ask — never dispatch with a blank required arg.
