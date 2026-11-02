I tried `judge health` and `judge why 7c1e9a40-52d3-4b6e-9f10-0a8d3e6b21c4`; both failed with "judge: command not found", so the judge binary isn't installed here. That would also explain `judge ✗ down` on the status line.

Fix: run `./setup.sh` from the toolkit checkout (it installs the judge binary and its hooks). Once it's on PATH, `judge why 7c1e9a40-52d3-4b6e-9f10-0a8d3e6b21c4` will show why the `resolution-check` escalated. I changed nothing.

```aw-result
{"skill": "judge", "artifacts": [], "summary": "judge binary not installed, nothing changed", "command": "judge health", "outcome": "unavailable"}
```
