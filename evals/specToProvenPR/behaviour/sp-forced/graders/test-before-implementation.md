---
type: regex
target: trace
flags: s
pattern: '^(?:(?!"name"\s*:\s*"(?:Write|Edit)"\s*,\s*"input"\s*:\s*\{[^{}]*?"file_path"\s*:\s*"[^"]*(?<!/plans)/greeter/)[\s\S])*"name"\s*:\s*"(?:Write|Edit)"\s*,\s*"input"\s*:\s*\{[^{}]*?"file_path"\s*:\s*"[^"]*/tests/'
---

A test under tests/ is written or edited before the first Write or Edit under greeter/. An agent that adds its test to an existing test file with Edit counts.
