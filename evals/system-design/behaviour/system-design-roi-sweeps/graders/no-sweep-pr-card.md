---
type: tool_used
tool: Write
input_match: '"file_path"\s*:\s*"[^"]*prs/pr-\d+\.md"[\s\S]*?"content"\s*:\s*"(?:---[\s\S]*?---(?:\\n)+)?#\s*PR\s*\d+\s*:\s*(?:Performance|Bug) sweep'
min: 0
max: 0
---

A sweep is a phase that write_packet.py adds, never a `prs/pr-NN.md` card: a card would make it a PR and number it before the real last PR.
