---
type: tool_order
before:
  tool: Write
  input_match: '"file_path"\s*:\s*"[^"]*tests/'
after:
  tool: Edit
  input_match: '"file_path"\s*:\s*"[^"]*greeter/'
---
