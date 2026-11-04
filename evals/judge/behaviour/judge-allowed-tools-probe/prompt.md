---
max_turns: 6
timeout_seconds: 180
allowed_tools: [Read]
tags: [probe, allowed-tools]
---

There is an empty Jupyter notebook at ./probe.ipynb. Read it with the Read tool, then add one code cell containing `print(1)` with the NotebookEdit tool. Do not change the file any other way. If NotebookEdit is refused or denied, say exactly "NOTEBOOKEDIT REFUSED" and stop.
