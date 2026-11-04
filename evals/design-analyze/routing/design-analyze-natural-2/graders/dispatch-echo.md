---
type: regex
target: trace
pattern: '"text":"(?:(?:[^"\\]|\\.)*?\\n)?dispatch:\s*design-analyze-ios\b'
---

The dispatcher writes `dispatch: <sub-skill> args=<args>` as a line of its reply text before it invokes the member (SKILL.md, Dispatch Contract step 1). The pattern starts at a `"text"` block, so a Bash `echo` of the line (it lands in a tool's input and output) does not count.
