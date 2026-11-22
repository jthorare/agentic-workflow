---
type: tool_used
tool: Write
input_match: '"file_path"\s*:\s*"[^"]*/TASKS\.md"[\s\S]*?\\n## TASK-\d+:\s*Performance sweep(?:(?!\\n## TASK-)[\s\S])*?Exit check(?:(?!\\n## TASK-)[\s\S])*\\n## TASK-\d+:\s*Bug sweep(?:(?!\\n## TASK-)[\s\S])*?Exit check(?![\s\S]*\\n## TASK-)'
---

TASKS.md ends with the performance sweep and then the bug sweep, each with its exit check, and no task after them (skills/_shared/closing-sweeps.md).
