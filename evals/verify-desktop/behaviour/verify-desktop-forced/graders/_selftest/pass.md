<!-- selftest: a saved run that passes every grader of this case. The reply is the text under the Reply heading; the Trace heading lists the tool calls in order (what tool_used / tool_order graders read); the Files heading lists what the run left in the workspace, with contents for the ones a regex grader reads. -->

## Trace

```jsonl
{"tool":"Skill","input":{"skill":"verify-desktop"}}
{"tool":"Bash","input":{"command":"ls /w/skills/_shared/desktop-evidence.md /w/skills/_shared/evidence-pack.md /w/skills/_shared/verification-lenses.md"}}
{"tool":"Read","input":{"file_path":"/w/.agentic-workflow/evidence.json"}}
{"tool":"Read","input":{"file_path":"/w/docs/ui/screens.json"}}
{"tool":"Bash","input":{"command":"git diff --name-only HEAD~3..HEAD"}}
{"tool":"Bash","input":{"command":"ui-evidence desktop-run docs/ui/screens.json /w/.aw-state/budget/verification/20261101-101500-settings-footer --repo /w --app-build 9d1c2ab --screens settings"}}
{"tool":"Read","input":{"file_path":"/w/.aw-state/budget/verification/20261101-101500-settings-footer/settings-1440x900/side-by-side.png"}}
{"tool":"Write","input":{"file_path":"/w/.aw-state/budget/verification/20261101-101500-settings-footer/pack.json"}}
{"tool":"Write","input":{"file_path":"/w/.aw-state/budget/verification/20261101-101500-settings-footer/report.md"}}
{"tool":"Write","input":{"file_path":"/w/.aw-state/budget/results/verify-desktop-20261101T101500Z.json"}}
```

## Files

### .aw-state/budget/verification/20261101-101500-settings-footer/pack.json

```json
{"platform":"desktop","skill":"verify-desktop","verdict":"FAIL","mockup_diff":{"screen":"settings","viewport":"1440x900","pct":6.8,"verdict":"FAIL"}}
```

### .aw-state/budget/verification/20261101-101500-settings-footer/report.md

```md
# Desktop verification: settings (FAIL)
```

### .aw-state/budget/results/verify-desktop-20261101T101500Z.json

```json
{"skill":"verify-desktop"}
```

## Reply

[FAIL] Desktop Verification — settings
=====================================================
| screen | size | diff % | verdict | mismatches |
|--------|------|--------|---------|------------|
| settings | 1440x900 | 6.8 (limit 2) | FAIL | the footer action bar with Save and Cancel is missing; the mockup has it at the bottom, the app shows a lone Save next to the currency picker |

The diff touched only SettingsPage.axaml and its view model, so I ran the settings screen alone. Home was not part of the change and was not run. I did not edit the app, the manifest or the mockups. Do not ship until the footer is back and `/verify-desktop` passes.

Artifacts: /w/.aw-state/budget/verification/20261101-101500-settings-footer/

```aw-result
{
  "skill": "verify-desktop",
  "artifacts": ["/w/.aw-state/budget/verification/20261101-101500-settings-footer/pack.json", "/w/.aw-state/budget/verification/20261101-101500-settings-footer/report.md"],
  "summary": "settings differs from its mockup by 6.8% (limit 2%): the footer action bar is missing.",
  "pack": "/w/.aw-state/budget/verification/20261101-101500-settings-footer/pack.json",
  "verdict": "FAIL"
}
```
