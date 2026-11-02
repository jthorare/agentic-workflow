---
type: regex
target:
  source: file
  path: '.aw-state/*/verification/*/pack.json'
pattern: '^(?=[\s\S]*"platform"\s*:\s*"desktop")(?=[\s\S]*"skill"\s*:\s*"verify-desktop")(?=[\s\S]*"verdict"\s*:\s*"FAIL")'
---
