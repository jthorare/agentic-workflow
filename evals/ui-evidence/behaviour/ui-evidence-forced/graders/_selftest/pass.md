<!-- selftest: a saved run that passes every grader of this case. The reply is the text under the Reply heading; the Trace heading lists the tool calls in order (what tool_used / tool_order graders read) and the files the run left in the workspace (what file_exists reads). -->

## Trace

```jsonl
{"tool":"Skill","input":{"skill":"ui-evidence"}}
{"tool":"Bash","input":{"command":"command -v ui-evidence"}}
{"tool":"Bash","input":{"command":"ui-evidence doctor"}}
{"tool":"Write","input":{"file_path":"/work/ui-runs/pr57/script.json"}}
{"tool":"Bash","input":{"command":"ui-evidence run ./ui-runs/pr57/script.json ./ui-runs/pr57 --app-build 4f2a9c1"}}
{"tool":"Bash","input":{"command":"ui-evidence provenance"}}
{"tool":"Write","input":{"file_path":"/work/.aw-state/acme-shop/results/ui-evidence-20261101T101500Z.json"}}
```

Files left: `.aw-state/acme-shop/results/ui-evidence-20261101T101500Z.json`

## Reply

The evidence run for PR 57 failed, so there is nothing worth posting yet.

- Stack: `ui-evidence doctor` said healthy, host `http://localhost:3000`.
- Run (`./ui-runs/pr57`): 2 of 3 steps passed. Step 3, the confirmation toast after Save, failed: the page showed "Something went wrong (500)" where "Preferences saved" should be. The visual check also came back `changed` (an error banner where the toast goes).
- Provenance is `unknown` (the users table holds 14 rows outside the seed domains), so the screenshots stay on this machine and any PR comment would be text-only.

Nothing has been posted or uploaded to PR 57. I need your go-ahead before I post even the text-only comment, and I would fix the save error first: want me to look at why Save returns a 500?

```aw-result
{
  "skill": "ui-evidence",
  "artifacts": ["/work/ui-runs/pr57/summary.json", "/work/ui-runs/pr57/prefs-3-error.png"],
  "summary": "PR 57 evidence run failed at the confirmation toast after Save; provenance unknown; nothing published.",
  "run_dir": "/work/ui-runs/pr57",
  "outcome": "failed",
  "provenance": "unknown",
  "published": false
}
```
