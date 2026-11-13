# Agents page: terminal WebSocket

`GET /sindri/sessions/{provider}/{id}/terminal` is a WebSocket (spec `docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md` §15.8). Text messages are JSON with a `t` field. Output and input bytes are binary. The human token goes in the first message, never in the URL. Before the upgrade the route looks up the pane: a bad ref answers 400, a session with no tmux pane 409 SND-AGENT-015, a gone one 404 SND-AGENT-016, and a plain (non-WebSocket) request 400. One socket per open pane, and it targets one pane id `%N`. Closing it detaches and never stops the session. It writes no frame or key to a log or the ledger. Fixture: `tests/fixtures/terminal-ws/claude-session.jsonl` (one line per message: direction, milliseconds from open, message).

## Messages

| Kind | Direction | Payload | Limit |
|---|---|---|---|
| `hello` | in, first message | `{t, v: 1, token, cols, rows}` | must arrive 3 s after open, else close 1008. A bad token closes 1008 with SND-AUTH-002 |
| `ready` | out | `{t, v, host: {socket, session, pane}}` | one, after a good hello |
| `frame` | out | `{t, seq, rows: [ansi lines], cursor: {x, y}, w, h, alt, inMode, dead}` whole screen each time (`dead` is 1 when the pane's process has exited) | at most 30 a second; none when the screen is unchanged; if 1 MiB is unsent, older frames are dropped and the latest is kept |
| `key` | in | `{t, key}` with a name from the foreign list, or `{t, text}` for literal text | `text` at most 4 KiB; at most 200 messages a second |
| `paste` | in | `{t, text}` | at most 256 KiB, else close 1009 |
| `size` | in | `{t, cols, rows}`, 20 to 400 columns and 5 to 200 rows | on a foreign socket it only sets how Sindri draws; it never resizes the window |
| `ping` / `pong` | out / in | `{t}` | Serve pings every 15 s; no pong in 10 s closes the socket (view: Reconnecting) |
| `close` | out | WebSocket close code and reason | 4015 "SND-AGENT-015" (no pane), 4016 "SND-AGENT-016" (tmux or session gone; both after the upgrade only when the pane went away in between), 4001 "replaced" (a second socket opened on the same pane; the older view shows "This pane is open in another Sindri window" with **Take it back**, and does not reconnect on its own), 1001 (Serve stopping), 1008 (token), 1009 (message too big) |

Key names (foreign list, spec §15.2): `Enter`, `Escape`, `Tab`, `BTab`, `BSpace`, `DC`, `Up`, `Down`, `Left`, `Right`, `Home`, `End`, `PageUp`, `PageDown`, `F1` to `F12`, `C-a` to `C-z`, `M-a` to `M-z`. Any other name is dropped and gets no echo.

## Latency rule (D14, keystroke to echo p50 <= 30 ms)

After each `key` message Serve runs one capture right away instead of waiting for the next 30/s tick, and skips that next tick so the 30/s cap holds.
