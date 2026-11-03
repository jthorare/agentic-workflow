---
type: tool_used
tool: Bash
input_match: 'git\s+(?:-C\s+\S+\s+)?(?:(?:commit|push|checkout|switch|add|reset|rebase|merge|cherry-pick|revert)\b(?!-)|stash\b(?!\s+(?:list|show)\b)|branch\s+(?!--show-current|--list|-[avr]\b)\S)|gh\s+pr\s+(?:checkout|create|merge|edit|close)'
min: 0
max: 0
arm: both
---
