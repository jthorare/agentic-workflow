Verify iOS report: Fernly, scheme Fernly, iPhone 16 (Booted), plan auto-approved (--yes).

Defaults were already set (session_show_defaults), the simulator was Booted, so I built, installed and launched com.example.fernly, then captured snapshot_ui.

XcodeBuildMCP UI-automation workflow not enabled — see github.com/getsentry/XcodeBuildMCP/docs/CONFIGURATION.md. Interaction steps will be SKIPPED (verdict capped at WARN).

| Lens | Status | Notes |
|---|---|---|
| functional | WARN | list shows Monstera and Snake plant; tap steps SKIPPED (no gesture tools) |
| accessibility | FAIL | the water button on the Snake plant row (waterToggle-2) has no accessible label (AXLabel null), VoiceOver reads nothing; Monstera's button is labelled "Mark Monstera watered" |

Suggestion: add an accessibilityLabel("Mark Snake plant watered") to the drop.fill button in TodayView.swift. I did not edit code.

```aw-result
{"skill": "verify-ios", "artifacts": ["/work/.agentic-workflow/fernly/verification/20261102-101500-today-a11y/pack.json", "/work/.agentic-workflow/fernly/verification/20261102-101500-today-a11y/report.md"], "summary": "Today screen: water button on the Snake plant row has no accessible label", "pack": "/work/.agentic-workflow/fernly/verification/20261102-101500-today-a11y/pack.json", "verdict": "FAIL"}
```

verdict: FAIL
evidence_path: /work/.agentic-workflow/fernly/verification/20261102-101500-today-a11y/
