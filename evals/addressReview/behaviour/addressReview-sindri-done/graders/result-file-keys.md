---
type: regex
pattern: '"name":"Write"(?=[^\n]*?sindri-result\.json)(?=[^\n]*?\\"skill\\")(?=[^\n]*?\\"artifacts\\")(?=[^\n]*?\\"summary\\")(?=[^\n]*?\\"pr\\")(?=[^\n]*?\\"fixed\\")(?=[^\n]*?\\"remaining\\")(?![^\n]*?\\"waiting\\"\s*:\s*\\")'
flags: i
target: trace
---

The run wrote the result to AW_RESULT_FILE (sindri-result.json), not to $AW_DIR/results, and the file carries the same keys as the aw-result block (skill, artifacts, summary, pr, fixed, remaining), with no `waiting`. A file with fewer keys fails the block's outputs (SND-SKILL-004; S-0 item 6).
