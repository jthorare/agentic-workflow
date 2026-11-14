# terminal-ws fixture

`claude-session.jsonl` is a recorded terminal WebSocket session (message table: `docs/sindri/agents.md`, spec `docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md` §15.8). One JSON object per line: `{"dir": "in"|"out", "ms": <ms from open>, "msg": <message>}`. `in` is client to Serve, `out` is Serve to client. The fake `ITerminalPaneSource` plays it, and the Ui drawing tests (U7a, U7b) read it.

What the session shows (120x36): `hello`, `ready`, Claude Code's "trust this folder" dialog (Down, Enter), the idle prompt, `say hi` typed one `text` message per character, `Enter`, the answer, a `paste` of 16 bytes, `C-u`, `C-c` twice (the "Press Ctrl-C again to exit" line), then `ping` and `pong`. Frames are whole-screen `capture-pane -e -p` copies, sent only when the screen changed, with the cursor and pane facts from `display-message`. The alternate screen (`alt` 1) is on while the prompt is up.

## How it was recorded

`record.py` starts `claude --setting-sources project --permission-mode default` (so no user statusline or vim mode shows) in a scratch dir, inside a private tmux server `aw-fixture-3381` (never the default socket, `TMUX` dropped from the environment). It polls the pane every 50 ms, sends the keys through `send-keys` and the paste through `load-buffer` plus `paste-buffer -p -d`, then kills its own server. Scrubbed on write: the scratch dir and the home dir become `/home/user/...`, the plan name becomes `Claude`. The token in `hello` is the placeholder `fixture-token`.

## Re-record

```bash
python3 -I tests/fixtures/terminal-ws/record.py /tmp/new.jsonl
```

Needs `tmux` and a signed-in `claude` on PATH. Then read the file before committing: check for your user name, account names, emails and paths (`grep -iE "$USER|@|/home/"`), and keep it under 200 KB. The answer text and timings differ per run; tests must not depend on the exact reply.
