---
type: tool_used
tool: Write
input_match: '"file_path"\s*:\s*"[^"]*stages\.md"[\s\S]*\\n##\s*Closing checks(?:(?!\\n## )[\s\S])*?Performance sweep(?:(?!\\n## )[\s\S])*?[Ee]xit check(?:(?!\\n## )[\s\S])*?Bug sweep(?:(?!\\n## )[\s\S])*?[Ee]xit check'
---

stages.md has one `## Closing checks` heading that names the performance sweep and then the bug sweep, each with its exit check.
