# The Agents page: rows and reasons

Spec: `docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md` (issue 3018). This page says what exists so far. Phase A0 (issue 3144) added the wire shape. The terminal route below is the one route built so far.

## The row

One Agents row is one live session, one waiting job or one lane with no session. The Core type is `AgentRow` in `Aw.Sindri.Core.Sessions`; the list is `AgentRowList` (`rows` in page order, `degraded` for sources that could not be read). The JSON schema is `schemas/agent-rows.schema.json`. The API goldens `tests/Aw.Sindri.Tests/Golden/Api/agents-rows/needs-you.json` and `every-reason.json` are examples, and `needs-you` is the spec's section 4.3 walk (the fixture is `tests/fixtures/agents/needs-you.json`).

`AgentRow` holds the fields the page adds to today's `SessionRow`: `id`, `version`, `state`, `stateSource`, `stateReadAt`, `estimated`, `title`, `facts`, `reasons`, `parent`, `since`, `lastActivity`, `blocks`, `actions` and `links`. The list route (phase A1) joins them to `SessionRow`. Until it ships, no route returns `reasons[]` on `GET /sindri/sessions`.

## Reasons

`AgentReasons.Evaluate` is a pure function. It takes an `AgentReasonInputs` for one row and returns the reasons that hold, lowest class first. The first is the primary one. It reads nothing: the collector (phase A1) fills the inputs from the session state, `aw-meta lanes-context`, `usage`, `status` and `health`.

| Class | Kind | Holds when | Group |
|---|---|---|---|
| 1 | `permission-prompt`, `owner-question`, `login` | the session asks (approval, input, auth) | Needs you |
| 2 | `usage-limit` | `aw-meta usage` lists the lane as Limited | Waiting |
| 3 | `lane-no-session` | a lane in the team map has no live session and the owner did not stop it from the page | Needs you |
| 4 | `recycle-mark` | the lane's context is at the recycle mark, or the meta lane has an open RecycleSelf obligation | Needs you |
| 5 | `idle-owed` | a lane session is idle for `agents.idleOwedMinutes` (default 10) and work is owed: a PR that failed its gate or conflicts, the meta lane's open obligations, or mail a non-Claude lane was not given | Needs you |
| 6 | `slot-wait` | a heavy waiter waited `agents.slotWaitMinutes` (default 30) or more | Waiting |
| 7 | `pr-ready` | a PR the lane sent is in the merge queue with its gate passed or not run, and Meta has not queued it on GitHub | Needs you |

Each reason carries `word` (the chip), `text` (the words on the row), `since` (when the row first held it, from the source) and `source`. The text golden `tests/Aw.Sindri.Tests/Golden/Agents/walk-reasons.txt` pins the walk.

## Profile keys

`agents.idleOwedMinutes`, `agents.slotWaitMinutes` (1 to 1440) and `agents.page` (`rows` or `legacy`, default `legacy`) are documented in `docs/csharp/sindri-profile.md`. A bad value is `SND-AGENT-010`. The `SND-AGENT-001` to `011` codes are in `docs/csharp/sindri.md`.

## Terminal WebSocket

`GET /sindri/sessions/{provider}/{id}/terminal` is a WebSocket (spec `docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md` §15.8). Every message, in and out, is a WebSocket text message holding JSON with a `t` field. The human token goes in the first message, never in the URL. Before the upgrade the route looks up the pane: a bad ref answers 400, a session with no tmux pane 409 SND-AGENT-015, a gone one 404 SND-AGENT-016, a plain (non-WebSocket) request 400, and a request with an `Origin` header 403: a loopback Origin (a local page) gets SND-API-008, and the loopback guard refuses any other Origin with a bare 403 before the route runs. One socket per open pane, and it targets one pane id `%N`. Closing it detaches and never stops the session. It writes no frame or key to a log or the ledger. Fixture: `tests/fixtures/terminal-ws/claude-session.jsonl` (one line per message: direction, milliseconds from open, message).

### Messages

| Kind | Direction | Payload | Limit |
|---|---|---|---|
| `hello` | in, first message | `{t, v: 1, token, cols, rows}` | must arrive 3 s after open, else close 1008. A bad token closes 1008 with SND-AUTH-002 |
| `ready` | out | `{t, v, host: {socket, session, pane}}` | one, after a good hello |
| `frame` | out | `{t, seq, rows: [ansi lines], cursor: {x, y}, w, h, alt, inMode, dead}` whole screen each time (`dead` is 1 when the pane's process has exited) | at most 30 a second; none when the screen is unchanged |
| `key` | in | `{t, key}` with a name from the foreign list, or `{t, text}` for literal text | `text` at most 4 KiB; one message is one tmux call, a trailing `;` included |
| `paste` | in | `{t, text}` | at most 256 KiB, else close 1009 |
| `size` | in | `{t, cols, rows}`, 20 to 400 columns and 5 to 200 rows | on a foreign socket it only sets how Sindri draws; it never resizes the window |
| `ping` / `pong` | out / in | `{t}` | Serve pings every 15 s; no pong in 10 s closes the socket (view: Reconnecting) |
| `close` | out | WebSocket close code and reason | 4015 "SND-AGENT-015" (no pane), 4016 "SND-AGENT-016" (tmux or session gone, or a tmux call that took over 5 s; both after the upgrade only when the pane went away or tmux stalled in between), 1001 (Serve stopping), 1008 (token), 1009 (message too big) |

Key names (foreign list, spec §15.2): `Enter`, `Escape`, `Tab`, `BTab`, `BSpace`, `DC`, `Up`, `Down`, `Left`, `Right`, `Home`, `End`, `PageUp`, `PageDown`, `F1` to `F12`, `C-a` to `C-z`, `M-a` to `M-z`. Any other name is dropped and gets no echo.

### Later (#3381)

These are in the spec and not built:

- Binary output and input frames.
- Dropping older frames when 1 MiB is unsent (a slow reader today just reads each frame in turn).
- A cap of 200 `key` messages a second.
- Close 4001 "replaced" for a second socket on the same pane, and the **Take it back** view. Today two sockets on one pane both work.

### Latency rule (D14, keystroke to echo p50 <= 30 ms)

After each `key` message Serve runs one capture right away instead of waiting for the next 30/s tick, and skips that next tick so the 30/s cap holds.
