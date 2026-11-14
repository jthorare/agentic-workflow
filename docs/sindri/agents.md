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

## Stopping a session Sindri started

`POST /sindri/sessions/{provider}/{id}/stop` ends a one-off session (spec §15.2, issue 3147). The CLI is `sindri agents stop <provider>/<id> [--json]`, the MCP tool is `agents_stop`.

1. Only the person may call it. An agent session gets SND-AUTH-002.
2. `{id}` is the tmux name `sindri-<actId>`, or the provider's session id of a row whose tmux session sits on Sindri's own socket.
3. A start must have recorded that act id. Any other session gets SND-AGENT-019 (409) and nothing is killed.
4. Sindri runs `kill-session -t sindri-<actId>`. A session that is already gone counts as stopped.
5. The answer is `{"id":"sindri-<actId>","actId":"<actId>","state":"ended"}`.

The worktree and branch stay. `sindri agents worktree remove <actId>` removes a clean worktree after the stop.
