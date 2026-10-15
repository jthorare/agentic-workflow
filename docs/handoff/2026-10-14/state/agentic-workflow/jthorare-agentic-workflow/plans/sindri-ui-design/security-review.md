# Threat Model — sindri-ui-design

**Mode:** plan
**Source:** docs/superpowers/specs/2026-10-11-sindri-ui-design.md (checked against the API surface spec §4.2, §6, P10)
**Reviewed:** 2026-10-11

Threat model: the repo's own. Gates guard against honest mistakes by humans and agents, not against a hostile agent. v0 is loopback-only with no tokens, and a session that deliberately forges `X-Sindri-Caller: human` is out of scope (API §6). The findings below are rated on that basis. None of them assumes an attacker.

## Summary

The client-side posture is sound. The app is a logic-free `human` caller with no terminal. Protected actions stay copy-the-CLI only (§6.5, §7). Agent and tracker text goes through `TextBlock.Text` only (U9). The seed is a fixed template that carries only ids (C-26). No action runs from the tray or from a stream event (§5, §6.5).

The two HIGH findings are both in the launcher:
1. **Session marker missing.** Sessions that Sindri launches carry no marker that the CLI's agent detection can rely on. Codex sets `CODEX_SANDBOX` only when sandboxed, and the spec itself says `CURSOR_AGENT` is unreliable. A launched agent's own `sindri …` calls can therefore be classified `human`.
2. **No once-only rule for launches.** The asynchronous `item.open-requested` → launcher path has no rule that a request is acted on at most once and only while fresh. A restart or a replay could open agent sessions that nobody clicked.

The MEDIUM findings are honest-mistake hazards:
- the shell `exec` line is built by string interpolation from paths;
- an action can land on a different target after a refresh moves focus or rows;
- copy-the-CLI strings get pasted into a shell.

## Coverage

| Category | Status |
|---|---|
| A01 Broken Access Control | findings: 2 (1 HIGH, 1 MEDIUM) |
| A02 Cryptographic Failures | N/A — v0 is loopback HTTP with no tokens or secrets in the client (API §6). Revisit when §11 tokens arrive |
| A03 Injection | findings: 2 (1 MEDIUM, 1 MEDIUM) |
| A04 Insecure Design | findings: 2 (1 HIGH, 1 LOW) |
| A05 Security Misconfiguration | findings: 2 (LOW) |
| A06 Vulnerable & Outdated Components | No findings. (Avalonia MIT core, Inter, JetBrains Mono go through CPM with locked restore and `nuget.org` only; Warp's undocumented `warpctrl` is explicitly not used) |
| A07 Identification & Authentication Failures | findings: 1 (LOW) |
| A08 Software & Data Integrity Failures | findings: 1 (LOW) |
| A09 Security Logging & Monitoring Failures | findings: 1 (LOW) |
| A10 SSRF | findings: 1 (LOW) |

## OWASP Top 10 findings

### A01 — Broken Access Control

- **F1. Launched agent sessions aren't reliably classified as `session` by the CLI** [HIGH]
  - Where: §6.3 "The launcher's recipe" step 1 (env `AW_SINDRI_ITEM`, `AW_SINDRI_LAUNCH`); §6.3 facts table (`CURSOR_AGENT=1` unreliable, `CODEX_SANDBOX` is a sandbox variable); API §6 "Who sends what".
  - Attack (honest mistake): the CLI sends `human` + `X-Sindri-Terminal: 1` when stdin and stdout are a TTY and none of `CLAUDECODE`, `AW_SINDRI_CHILD`, `CODEX_SANDBOX` or `CURSOR_AGENT` is set. A Codex session running with full access (no seatbelt, so no `CODEX_SANDBOX`) or a Cursor `agent` session (where `CURSOR_AGENT` is unreliable) runs in a real Warp terminal. If its shell tool runs on a PTY, as Codex's unified exec can, then `sindri decide …`, `sindri stop` or a protected verb is classified as a human at a terminal. That defeats the guard "session calls are requests, never decisions", and the app's own launch is what created the session.
  - Mitigation: the launcher sets **`AW_SINDRI_CHILD=1`** as a prefix on the agent command in the `exec` line (`AW_SINDRI_CHILD=1 AW_SINDRI_ITEM=… claude …`). That marker is already in the CLI's list. As a prefix it applies only to the agent and its children, so the human's own shell in the tab is unaffected after the agent exits. Add it to §6.3 step 1 and to E-1/P10's observable signal: inside a launched session, `sindri decide` gets `SND-AUTH-002` for all three targets. Recommend the same marker for the Codex `-c` and Claude `--mcp-config` paths, so the MCP server and any CLI fallback agree.

- **F2. Actions can land on a different subject than the one the human looked at** [MEDIUM]
  - Where: §3.3 "Selection and focus are stable across refreshes" (on clear, focus moves to the next row); §5 "New rows slide in"; §8 `A` (primary action, which for a decision is the recommended option), `X`, `T`, `B`; §4.4 answers via `ApplyCommand:*`.
  - Attack (honest mistake): a stream event clears the focused row. Focus moves to the next row, and the human's next `A` (or a double-tapped `A`) accepts a different decision's recommended option, which a session may have authored. In the same way, a new pulsing row sliding in shifts the rows under the pointer, so a click lands on another row's **Resume**, **Reject** or **Cancel**. Separately, a park whose reason changes between render and click (for example `rounds` → `timeout`) gets an answer the human never chose.
  - Mitigation:
    1. After focus moves because of a refresh, ignore single-key actions on the newly focused row for about 750 ms. The polite announcement already says what cleared.
    2. Don't shift rows under the pointer while it hovers a panel. Insert new rows in place, or hold the insert until the pointer leaves.
    3. Every command the app sends carries the version or sequence the human saw (`itemVersion`, the decision's item version, and the instance's state version for `ApplyCommand`). The server refuses a stale one with `SND-ITEM-409`, which §9.1 already renders as "changed since you looked". Make this explicit for `ApplyCommand:*` and `Decide` in §7.
    4. Optionally, `A` on a session-filed decision requires the detail sheet to be open.

### A02 — Cryptographic Failures

N/A — no secrets, tokens or crypto in the v0 client. The loopback-only binding and `LoopbackGuard` are the network control. When API §11 tokens arrive, `auth/human.token` must never be written to `preferences.json` or to the launcher's files.

### A03 — Injection

- **F3. The launch configuration's `exec` line is a shell string built from paths and ids** [MEDIUM]
  - Where: §6.3 recipe step 1 (`cwd: <repo path>`, the guard `command -v <cli> … echo "<cli> not found …: <fix>"`, then the target command); §6.1 commands (`codex -C <repo> "<seed>"`, `-n "sindri <item>"`); step 4 (`--mcp-config <AW_SINDRI_HOME>/launch/mcp.json`, Codex `-c mcp_servers.sindri.*` overrides).
  - Attack (honest mistake): the repo path comes from the approved profile, which is protected and so isn't attacker-controlled. Even so, a path with a space, a quote, a `$` or a non-ASCII character is ordinary, and on macOS `AW_SINDRI_HOME` under `~/Library/Application Support` already contains a space. Interpolating such a value into `exec` (Codex's `-C <repo>`, the `--mcp-config` path, TOML-valued `-c` overrides) breaks the command, or runs part of it as a separate command. Hand-built YAML breaks the same way on `:` or `#` in a path.
  - Mitigation:
    1. Emit the launch configuration with a YAML serializer, never by string templating.
    2. Put the repo path only in `layout.cwd`, never in `exec`. For Codex, drop `-C <repo>`, because the tab's cwd already is the repo.
    3. Build `exec` from an argv list with POSIX single-quote escaping for every element. That includes the `--mcp-config` path and each `-c key=value`, whose TOML value is quoted first and then shell-quoted.
    4. Before the launcher writes anything, validate `ItemId`, `LaunchId` and the session uuid against their typed-id patterns, and accept the target id only from the closed set.
    5. Add a unit test that launches with a repo path and `AW_SINDRI_HOME` containing a space, a single quote, `$(x)` and `#`, and asserts the generated YAML round-trips and the shell argv is exact.
    6. Also fix the guard: as written, `|| { echo …; }` falls through and still runs the missing CLI. It should end with `exit 127`, or the agent command should run only on success (`&&`).

- **F4. Copy-the-CLI strings are pasted into a shell** [MEDIUM]
  - Where: §4.3 (the CLI under a protected row with **Copy**); §7 "A copy-the-CLI block" (the `cli` string with ids filled by the API in `NeedsYouRow.cli` or `allowedCommands`); §4.4 `sindri recipe approve <recipe>`; `Y` copies the selected row's CLI.
  - Attack (honest mistake): the clipboard is the one path where text the app shows becomes text a shell runs, and the human pastes it into the terminal that holds protected authority. If any interpolated field isn't a typed id, a pasted string with a newline runs before the typed confirmation is ever reached. Recipe names come from repo files that agents can write, and choice names or a hash are other examples.
  - Mitigation:
    1. The API builds every `cli` string only from validated tokens (typed ids, hashes, closed enums, recipe names checked against a `[A-Za-z0-9._-]+` pattern), shell-quoting each argument.
    2. The app refuses to render, or to copy, a CLI string that contains a newline, a C0/C1 control or a bidi control. Instead it shows `SND-UI-003` "unreadable command".
    3. The app copies exactly the string it displays.
    4. Add a contract test on the fake and the real API: no catalog or needs-you `cli` contains a control character, and each one parses back to its argv.

### A04 — Insecure Design

- **F5. No rule that a launch request is performed at most once and only while fresh** [HIGH]
  - Where: §6.7 E-1 (the handler appends `item.open-requested`; an in-process `launcher` hosted service performs it and appends `launch.started` or `launch.failed`); §6.5 "never … start an agent without a click".
  - Attack (honest mistake): the launcher is driven by ledger events. Several things could each re-perform old requests and open agent sessions, with full tool permissions on real repos and with takeovers, that no human clicked in this sitting:
    - a restart that resumes the service's cursor from before its last outcome;
    - a views rebuild or replay that feeds the launcher;
    - a crash between `open warp://…` and appending `launch.started`.
    The invariant in §6.5 has no mechanism behind it.
  - Mitigation: specify in E-1:
    1. The launcher acts on an `item.open-requested` only if no `launch.started` or `launch.failed` exists for its `launchId`, **and** the request is younger than a short TTL (for example 30 s).
    2. On start, it appends `launch.failed {code: SND-LAUNCH-007 expired}` for every older open request, and never performs it.
    3. It appends `launch.attempted` before calling `ILauncher`, so a crash leaves an attempted-without-outcome request that is expired, not retried.
    4. Projections and rebuilds never drive the launcher; only the live tail does.
    5. Add an integration test: append an `item.open-requested`, restart the process, and assert no `ILauncher.Launch` call and one `launch.failed expired`.

- **F6. Session-filed decisions surface with a one-key accept of the session's recommended option** [LOW]
  - Where: §4.3 `decision` (options with the recommended one marked); §8 `A`.
  - Attack (honest mistake): a session's `sindri_request` produces a decision whose recommended option the session chose. `A` from the list accepts it without the human reading the question. These are non-protected `decision`-class actions by design, so the impact is bounded.
  - Mitigation: show "filed by <session>" in the row, not only in the sheet. Consider making `A` on session-filed decisions open the sheet first (see F2.4).

### A05 — Security Misconfiguration

- **F7. Launcher file lifecycle and the MCP config file** [LOW]
  - Where: §6.3 recipe steps 1, 3 and 4.
  - Attack (honest mistake):
    - If Sindri stops or crashes within 60 s, the `sindri-<launchId>.yaml` files stay in Warp's launch-configuration folder indefinitely, and they show in Warp's launch-configuration palette, where one click re-runs an old launch with no `OpenItem`.
    - `<AW_SINDRI_HOME>/launch/mcp.json` has no stated permissions. It will hold the API URL now and possibly a session token later.
    - A cold-starting Warp could read the YAML after the 60 s delete.
  - Mitigation:
    - Create the YAML with create-exclusive (`O_CREAT|O_EXCL`, no symlink follow) at 0600.
    - On launcher start, sweep and delete any `sindri-*.yaml` older than the TTL.
    - Delete on `launch.started` plus the delay, not on a fixed timer from write.
    - Write `mcp.json` at 0600 in a 0700 directory, atomically (temp file plus rename).
    - On Windows, state that the ACL is inherited from the user's profile folder rather than relying on chmod.
    - The P10 spike should record whether Warp reads the file synchronously when it handles the URI. If it does, delete right after `open` returns success.

- **F8. Probes run the user's interactive login shell from a background service** [LOW]
  - Where: §6.3 facts table, "PATH from a GUI process" (`$SHELL -lic 'command -v claude'`); E-3 probes every 10 min; §6.6 `git -C <path> rev-parse`.
  - Attack (honest mistake): an interactive login shell sources the user's rc files every 10 minutes. An rc file that prompts (an updater, a `read`, a tmux auto-attach) hangs the probe or the launcher service. Building the command as a string also invites quoting bugs.
  - Mitigation:
    - Start the shell and git through argv (`ProcessStartInfo.ArgumentList`) with the CLI name drawn only from the closed target table.
    - Redirect stdin to null, set a hard timeout of 5 s, and kill the process group on timeout. Record `problem: probe timed out` rather than blocking.
    - Prefer `-lc` without `-i` unless a probe proves `-i` is needed.

### A06 — Vulnerable & Outdated Components

No findings. Packages go through Central Package Management with locked, signature-verified restore from `nuget.org` (merge gate 8). Avalonia is used MIT core only. Warp's undocumented `warpctrl` is deliberately not used (§6.3).

### A07 — Identification & Authentication Failures

- **F9. Single-instance IPC and the later `sindri://` scheme must be navigate-only** [LOW]
  - Where: §15 "single instance (a second launch focuses the running one)"; §3.3 deep links; §12.2 `sindri://`.
  - Attack (honest mistake): the single-instance channel (a named pipe or socket) and a later OS URL scheme are inputs from outside the app with no caller identity. A link clicked in a browser or chat (`sindri://item/…`), or a stray second launch with arguments, should never be able to trigger an action, because the app is `human`.
  - Mitigation: specify in §3.3 and §12.2 that both inputs accept only a deep-link route from the closed list (`home/needs-you`, `item/<ItemId>`, …), validated against typed-id patterns, and only navigate and focus. No route maps to an action. Put the IPC endpoint in a per-user, 0700 location.

### A08 — Software & Data Integrity Failures

- **F10. Local preferences file robustness** [LOW]
  - Where: §12.3 (`$AW_SINDRI_HOME/ui/preferences.json`, 0600: window size, sheet width, last page, seen row ids, palette history); §5 "Seen".
  - Attack (honest mistake): a hand edit, a crash mid-write or a newer app version leaves the file malformed or with out-of-range values (a sheet width of 0, an unbounded seen list, a text scale of 10). The app then fails to start or renders off-screen. Palette history may also accumulate item titles typed as search text.
  - Mitigation:
    - Write atomically (temp file plus rename) into a 0700 directory, and parse with the source-generated JSON context.
    - On any parse error, back the file up as `preferences.json.bad` and start from defaults. Never block start-up.
    - Clamp every value to its documented range (sheet width 420–760, text scale 0.9–1.3, window at least 900 × 600 and on a visible screen).
    - Cap the seen ids and palette history (for example 500 and 50 entries). Store palette history as action ids and routes, not free text.
    - Keep the existing rule that nothing in the file feeds a command.

### A09 — Security Logging & Monitoring Failures

- **F11. The app's own logs must not carry response bodies** [LOW]
  - Where: §9 (the client); AGENTS.md Logging (`Aw.Kernel.Telemetry` scrubbed by `Aw.Redaction`).
  - Attack (honest mistake): debug logging of HTTP responses copies item text, request text, job progress lines and answer text (up to 1 MiB) into stderr logs outside the ledger's scrubbing and retention.
  - Mitigation: the app logs route, status, SND code, ids and timings only, never bodies or form contents, at every `AW_LOG_LEVEL`. The ledger already audits commands as `human:ui` (API P1), so no extra audit is needed in the client.

### A10 — SSRF

- **F12. The app trusts the URL in `control.json`** [LOW]
  - Where: §15 "`control.json` discovery … Keep"; §4.1 Not running.
  - Attack (honest mistake): a stale or hand-edited `control.json` (from a prover temp home, a copied home or a different `AW_SINDRI_HOME`) points the app at a non-loopback address. The app then sends `human`-class commands, including **Stop Sindri**, to a process that isn't the local Sindri.
  - Mitigation: the app accepts only `http://127.0.0.1:<port>` or `http://[::1]:<port>` from `control.json`. Anything else is shown as Not running with the reason "control.json names a non-loopback address". It already checks pid and epoch through `/ping`; keep that.

## STRIDE findings

### S — Spoofing
- F1 [HIGH]: launched sessions can present as `human` to the CLI. Fix: set `AW_SINDRI_CHILD=1` in the launcher's `exec`.
- F12 [LOW]: the app could talk to a non-local "Sindri" from a stale `control.json`.
- Note (by design, out of scope for v0): any local process can send `X-Sindri-Caller: human`, and `X-Sindri-Client: ui` is a label, not an identity (API §6). This spec doesn't widen that.

### T — Tampering
- F3 [MEDIUM]: the shell `exec` line is built by interpolation.
- F10 [LOW]: a malformed preferences file.
- F7 [LOW]: stale launch YAML left in Warp's folder can be re-run from Warp's palette.

### R — Repudiation
No findings. Commands from the app are audited in the ledger as `human:ui` (API P1), launches as `item.open-requested`, `launch.started` and `launch.failed` (E-1), and refusals with their SND codes. Copy-the-CLI and "seen" are local presentation state with no effect, so they need no audit. The protected verb that follows a copy is audited as `human:cli`.

### I — Information Disclosure
- F11 [LOW]: app logs must exclude bodies.
- **Job progress lines and desk-task file picks** [LOW]: §4.8 shows "the last 200 progress lines (mono, plain text)" without saying they are scrubbed. §4.4's Complete form sends picked file contents (≤ 1 MiB each) into the ledger, and an honest mis-pick (an `.env`) lands there. Mitigation:
  - state that job progress lines pass through `Aw.Redaction` server-side like other text (API §3.6);
  - show each picked file's name and size, with a confirm, before sending;
  - the server scrubs outputs on append.
- Plain-text rendering (U9) is sound. Make sure the API's sanitization (API §3.6) also strips C0/C1 and bidi override characters (U+202A–202E, U+2066–2069). Otherwise a title can visually alter a neighbouring id in a row, the tray menu or the window title.
- Avalonia `MenuItem` and `NativeMenuItem` headers treat `_` as an access-key marker. Escape it (`__`) for agent text in the tray menu (§5), so titles render literally.

### D — Denial of Service
- F8 [LOW]: an interactive login-shell probe can hang the launcher.
- Stream storms are handled (debounce of 250 ms per view, the 429 fallback to 5 s polling, §9.2). Answer text is capped at 1 MiB. No other findings.

### E — Elevation of Privilege
- F5 [HIGH]: launches can be re-performed without a click (replay, restart or crash).
- F2 [MEDIUM]: actions can land on a different subject after a refresh or row shift.
- F4 [MEDIUM]: copied CLI strings run in the human's terminal, which is the protected-authority surface.
- F6 [LOW], F9 [LOW].
- Confirmed sound:
  - protected actions are `cli-only:typed-confirmation` everywhere (§4.4, §6.5, §7), and `A` never maps to one (§8);
  - no action from the tray (§5) or from a stream event (§6.5);
  - Stop Sindri appears only on Health, with a confirm;
  - the seed and launch file carry ids only (C-26);
  - the server never opens a client path (§4.4).

## Verdict
- **Critical findings:** 0
- **High findings:** 2 (F1, F5)
- **Medium findings:** 3 (F2, F3, F4)
- **Low findings:** 9 (F6–F12, plus the job lines and file picks finding and the sanitization and access-key finding under I)
- **Recommendation:** FIX-CRITICAL-FIRST. Both HIGH findings have small, local fixes to §6.3 and §6.7 E-1: one env prefix, and a TTL plus an outcome-exists rule with a restart test.

verdict: NEEDS_WORK
