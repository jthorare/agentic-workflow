---
type: regex
target: trace
flags: s
pattern: '^(?:(?!"name"\s*:\s*"(?:Write|Edit)"\s*,\s*"input"\s*:\s*\{[^{}]*?"file_path"\s*:\s*"[^"]*(?<!/plans)/greeter/)[\s\S])*"name"\s*:\s*"(?:Write|Edit)"\s*,\s*"input"\s*:\s*\{[^{}]*?"file_path"\s*:\s*"[^"]*/tests/'
---

A test under tests/ is written or edited before the first Write or Edit under greeter/. An agent that adds its test to an existing test file with Edit counts.

The pattern relies on `file_path` coming before `content` (Write) or `old_string` (Edit) in the tool input, as the traces
show today: `[^{}]*?` can't step over a `{` in an earlier key, so if a Write ever put `content` first, a greeter write would
be missed and the grader would pass when it shouldn't.
