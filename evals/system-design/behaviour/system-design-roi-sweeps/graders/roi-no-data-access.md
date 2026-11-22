---
type: tool_used
tool: Write
input_match: '"file_path"\s*:\s*"[^"]*tdd\.html"[\s\S]*id=\\?"roi\\?"(?:(?!</section>)[\s\S])*[Nn]o data access'
---

Planted: the fixture's AGENTS.md documents no read access to data, so the ROI plan's production-data source says "no data access" instead of a guessed baseline.
