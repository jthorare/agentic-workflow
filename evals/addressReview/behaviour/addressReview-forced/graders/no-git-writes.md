---
type: tool_used
tool: Bash
input_match: 'git\s+(?:-C\s+\S+\s+)?(?:commit|push|checkout\s+-b|branch\s+(?!--show-current|--list|-[avr]\b)\S)|gh\s+pr\s+(?:comment|review|create|merge)|gh\s+api[^"]*(?:-X|--method)\s*(?:POST|PUT|PATCH)'
min: 0
max: 0
arm: both
---
