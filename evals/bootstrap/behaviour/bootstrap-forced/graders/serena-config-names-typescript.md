---
type: regex
target:
  source: file
  path: .serena/project.yml
pattern: 'language_servers:[\s\S]*typescript'
---

Planted: package.json and tsconfig.json make this a TypeScript repo, so the Serena config must list it. Reads the file itself, so a
config written through a Bash heredoc counts the same as one written with Write.
