---
type: regex
pattern: '"name":"Write"(?=[^\n]*?sindri-result\.json)(?=[^\n]*?\\"waiting\\"\s*:\s*\\"[^\\]{3})(?=[^\n]*?\\"skill\\")(?=[^\n]*?\\"artifacts\\")(?=[^\n]*?\\"summary\\")'
flags: i
target: trace
---

The run wrote the result to AW_RESULT_FILE (sindri-result.json) with `waiting` as a non-empty string and the same base keys (skill, artifacts, summary) as the aw-result block. A null or empty `waiting` does not count.
