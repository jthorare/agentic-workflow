# Agents page: terminal WebSocket

`GET /sindri/sessions/{provider}/{id}/terminal` is a WebSocket (spec `docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md` §15.8). Every message, in and out, is a WebSocket text message holding JSON with a `t` field. The human token goes in the first message, never in the URL. Before the upgrade the route looks up the pane: a bad ref answers 400, a session with no tmux pane 409 SND-AGENT-015, a gone one 404 SND-AGENT-016, a plain (non-WebSocket) request 400, and a request with an `Origin` header 403: a loopback Origin (a local page) gets SND-API-008, and the loopback guard refuses any other Origin with a bare 403 before the route runs. One socket per open pane, and it targets one pane id `%N`. Closing it detaches and never stops the session. It writes no frame or key to a log or the ledger. Fixture: `tests/fixtures/terminal-ws/claude-session.jsonl` (one line per message: direction, milliseconds from open, message).

## Messages

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

## Later (#3381)

These are in the spec and not built:

- Binary output and input frames.
- Dropping older frames when 1 MiB is unsent (a slow reader today just reads each frame in turn).
- A cap of 200 `key` messages a second.
- Close 4001 "replaced" for a second socket on the same pane, and the **Take it back** view. Today two sockets on one pane both work.

## Latency rule (D14, keystroke to echo p50 <= 30 ms)

After each `key` message Serve runs one capture right away instead of waiting for the next 30/s tick, and skips that next tick so the 30/s cap holds.

## Session reads: live push, question, branch, title (#3146, #3082)

- **`session.changed {provider, sessionId, state}`.** A public event on the stream for every state change of a session already known, once the new state has held 5 s (a flip back inside 5 s writes nothing). No text. The page patches one row on it by calling `GET /sindri/sessions/{provider}/{id}`; the 5 s poll stays as the backup. A new session is `fleet.changed` appeared, not `session.changed`. It is a ledger row (the stream reads the ledger).
- **`question`** on every session row (`GET /sindri/sessions`, `GET /sindri/sessions/{provider}/{id}`): the last 3 non-empty lines of the session's tmux pane, scrubbed of escape codes, only while `state` is `asking` and the pane reads. A session outside tmux, a pane that timed out or is gone, or a pane whose first process is not the session's gives null. It is read when the request comes (one shared read per pane, the 1 s pane deadline), never stored, logged, put in an event or the ledger. A session caller gets it inside the untrusted fence; `sindri agents show` prints it as an indented block labelled untrusted.
- **`branch`**: the branch the session's worktree has checked out, read from the worktree's `.git` files (a linked worktree's `.git` file points at the folder with `HEAD`). Null when detached, no repo or unreadable. No git process runs.
- **`title`**: the agent's own, else for a session started through `POST /sindri/sessions` the first non-empty line of the start prompt cut to 80 characters (the tmux session is `sindri-<actId>`). The start keeps it in memory, so a restart of Sindri drops it. No transcript is read.
