# UI driver (`aw-ui-driver`)

A free, generic, Playwright-like driver for **any Avalonia app**, modelled on the tool set of Avalonia's DevTools MCP (which is paid; the repo uses
MIT/free Avalonia only). It lets a script or an agent look at a running desktop app and act on it: read its element tree, find elements, click,
type, press keys, scroll, wait for a state, take a picture. It knows no app: an app opts in with one call, and everything app-specific (which screen,
which element) is in the caller's script. Desktop UI evidence (docs/csharp/evidence.md "Desktop evidence") drives apps through it.

Code: `src/UiDriver/` (package `UiDriver`, tier 1, in `deps.yaml`). Tests: `tests/Aw.UiDriver.Tests` (protocol and rules, the command line against a
stand-in driver) and `tests/Aw.UiDriver.Avalonia.Tests` (the driver on a real window under Avalonia's headless platform with Skia drawing).

## Projects

| Project | What it is |
|---|---|
| `Aw.UiDriver.Core` | The wire protocol (requests, answers, node info, `DriverEndpoint`), the pure rules (how a selector matches, when a wait holds, how a key chord reads), the error catalogue `AW-UIDRIVER-NNN` and the package's source-generated JSON context. No I/O, no Avalonia. Its types are internal; the other two projects see them. |
| `Aw.UiDriver.Avalonia` | The in-app half: a loopback HTTP server and the verbs over the app's window. One public call, `AppBuilder.UseAwUiDriver(args)`. Unshaped (like the UI projects: no Core/Features/Infrastructure layers). |
| `Aw.UiDriver.Cli` | The client and the `aw-ui-driver` command (NativeAOT). Unshaped. |

## Turning it on

```csharp
AppBuilder.Configure<App>().UsePlatformDetect().UseAwUiDriver(args)
```

`UseAwUiDriver` does nothing unless the run asks for it: **`AW_UI_DRIVER=1`** in the environment or **`--ui-driver`** on the command line. A normal launch never
opens a port or writes a file. When asked, after Avalonia is set up:

1. The driver directory is `AW_UI_DRIVER_DIR`, else a new `aw-ui-driver-<pid>` folder in the temp directory (created 0700 on Unix).
2. A random 256-bit secret is written to `driver.token` (0600 from creation) and an `HttpListener` binds `127.0.0.1` on a free port.
3. `driver.json` (`{"schema":"aw-ui-driver/1","url":"http://127.0.0.1:PORT","pid":N}`) is written last, so its presence means the driver answers.
4. `aw-ui-driver: listening, dir <dir>` goes to standard error. Both files are removed when the process ends.

The window driven is the classic desktop lifetime's main window (else its first window), asked for at each call, so the app may open it after the driver starts.

## Security

Loopback only; every call carries the secret in the `X-Ui-Driver-Token` header (compared in constant time) or is answered 401 `AW-UIDRIVER-005`; the secret
exists only in the owner-only file. Nothing is logged with it. The driver can click and type in the app, so it is a development and evidence tool: never
set `AW_UI_DRIVER` for a normal launch, and do not point `AW_UI_DRIVER_DIR` at a shared folder.

## The protocol

`POST /v1/<verb>` with a JSON body; `200` with JSON (a screenshot: `image/png`), else a `DriverFailure` body `{"code","message"}` with the status of its kind
(400 invalid, 401 unauthorized, 404 no match, 408 timeout, 500 failed). `GET /v1/ping` answers `{"ok":true}`. Calls are served one at a time; each runs on the
UI thread through the dispatcher after a layout pass, so it sees the screen as it is now. A tree is cut at 64 levels whatever `depth` asks. The command line
never waits for ever: a call that gets no answer within its budget (60 s; a `wait-for` adds its wait and 5 s) is `AW-UIDRIVER-004`, exit `2`.

A **selector** names an element; every field given must match: `nodeId` (`0/2/1`: child positions in the visual tree, window `0`), `automationId`, `name`
(`AutomationProperties.Name`, else `x:Name`), `type` (type name, exact), `text` (the text it shows, exact), `textContains`, and `index` (which match, from 0,
in tree order). The text an element shows is a `TextBlock`'s or `TextBox`'s text, or a content/header that is a string; a label that is a child `TextBlock`
is that child's text.

**Popups.** A menu, flyout, context menu or popup that Avalonia draws in its own popup window (the default on X11, Win32 and macOS) is not in the window's visual
tree. The driver follows `Popup.IsOpen` from the moment it starts (a popup opened before that is not seen) and lists the content of each open one as an extra child
of the window, node ids `p3`, `p3/0`, ... (`p` plus the popup's open number, then child positions), so `tree`, `search`, `wait-for`, `click`, `props` and `screenshot`
reach a `MenuItem` like any other element. Popups drawn in the window's overlay layer are in the tree already and are not listed twice. Bounds of a popup's
elements are in its own popup window, not the main window. A popup leaves the tree when it closes, so wait for `notVisible` or `gone` to see it close.

| Verb | Request | Answer |
|---|---|---|
| `tree` | `{from?: selector, depth?: 6}` | the element and its children to the depth: id, type, automationId, name, text, visible, enabled, bounds (window coordinates) |
| `search` | `{selector, limit?: 50}` | `{matches: [node...], total}` (matches without children, in tree order) |
| `props` | `{selector}` | the node and its registered Avalonia properties as text (read through the property registry, no reflection) |
| `click` | `{selector}` | acts through the element's automation peer: invoke (a button's command), else toggle, select, expand/collapse; a disabled element or one with none of these is `AW-UIDRIVER-003` |
| `action` | `{selector, action}` | one named action: `invoke`, `toggle`, `select`, `expand`, `collapse`, `focus` |
| `type` | `{text, selector?, replace?}` | sets a text box (appends unless `replace`); the focused element when no selector |
| `key` | `{chord, selector?}` | raises key down and up on the element (focused when none): `Enter`, `Ctrl+K`, `Ctrl+Shift+P` (Avalonia `Key` names) |
| `scroll` | `{dx, dy, selector?}` | moves the scroll viewer holding (or held by) the element, clamped to its extent |
| `wait-for` | `{selector, state?: visible, text?, timeoutMs?: 10000}` | polls (50 ms) until the state holds: `exists`, `visible`, `hidden`, `gone`, `notVisible`, `enabled`, `disabled`, `textEquals`, `textContains`; else `AW-UIDRIVER-004` |
| `screenshot` | `{selector?}` | PNG of the window or one element at scale 1 (`RenderTargetBitmap`) |
| `window` | `{width?, height?}` | sets the window size when given; answers `{width, height, title}` |

Which wait state for something that goes away: `gone` holds when no node matches at all (the control was removed from the tree); `hidden` needs a node that
exists with `IsVisible` false. Many views toggle `IsVisible` and keep the control, so for a spinner or a banner use `notVisible`: it holds when no match is
visible, whether it was removed or hidden (it looks at every match, not only the one the index picks). Use `gone` or `hidden` only when the check cares
which of the two happened. A `gone` wait that times out while a hidden match exists says so in its error.

Click goes through automation peers rather than synthetic pointer events: it runs the same commands a person's click does for buttons, toggles, list items
and expanders. An element that reacts only to raw pointer input needs an `AutomationProperties` peer (or a `Command`) to be clickable; that is the app's one
accessibility fix, and it benefits screen readers too.

## The command line

```
aw-ui-driver [--dir D] [--timeout-ms N] <verb> [flags]   D defaults to $AW_UI_DRIVER_DIR; N (default 60000, else $AW_UI_DRIVER_TIMEOUT_MS) bounds the whole call
  ready [--wait-ms 30000]                  wait until a driver answers (the app may still be starting)
  tree [--depth N] · search --limit N · props · click · type --value T [--replace] · key --chord Ctrl+K
  scroll --dx N --dy N · wait-for --state S [--expect-text T] [--wait-ms N] · screenshot --out file.png
  window [--width N --height N] · action --do invoke · call --path <verb> --json '<request>'
  selector flags: --node-id --automation-id --name --type --text --contains --index
```

Standard output is the answer's JSON (a screenshot writes `--out` and prints `{"path","bytes"}`). A failure is one stderr line
`aw-ui-driver: AW-UIDRIVER-NNN message`. Exit: `0` done · `1` usage or an invalid request · `2` no element, the element can't take the action, or a wait timed
out (a check failed) · `4` the driver is unreachable or the app failed · `130` interrupted.

## Errors

`AW-UIDRIVER-001` invalid request · `002` no element matches · `003` element can't take the action · `004` timed out (a wait, the app's UI thread past 20 s, or the driver not answering the command line within `--timeout-ms`) · `005` unauthorized · `006` driver
unreachable (no `driver.json`, a stale one, a refused connection) · `007` the app failed while the driver acted (a verb that throws answers 500 with this code and the server keeps serving).

## NativeAOT

No reflection: JSON is source-generated (`UiDriverJsonContext`), properties are read through `AvaloniaPropertyRegistry`, enum names with `Enum.TryParse<T>`, and
actions through automation peers. `Aw.UiDriver.Avalonia` is an ordinary library with the trim and AOT analyzers on, so an app that publishes NativeAOT stays
clean; `aw-ui-driver` is a NativeAOT entry point.

## Used by

Desktop UI evidence (`desktop-run`, docs/csharp/evidence.md "Desktop evidence") starts an app with `AW_UI_DRIVER=1`, a fresh `AW_UI_DRIVER_DIR` and a display
(the user's, else `xvfb-run -a`), runs `aw-ui-driver ready`, `window` and then each step of a screen's script (`click`, `type`, `key`, `wait-for`, `screenshot`)
through this command line, and stops the app. Sindri's `sindri-ui` opts in with `UseAwUiDriver(args)`.

## Not in this version

Raw pointer injection, drag, hover and multi-window addressing (the main window only), and a `launch` verb (the caller starts the app with `AW_UI_DRIVER=1`, then
runs `aw-ui-driver ready`; `desktop-run` does this for its scripts).
