---
type: regex
target: trace
flags: s
pattern: '^(?:(?!"name"\s*:\s*"Edit"\s*,\s*"input"\s*:\s*\{[^{}]*?"file_path"\s*:\s*"[^"]*/greeter/)[\s\S])*"name"\s*:\s*"Write"\s*,\s*"input"\s*:\s*\{[^{}]*?"file_path"\s*:\s*"[^"]*/tests/'
---

A test under tests/ is written before the first change to greeter/.
