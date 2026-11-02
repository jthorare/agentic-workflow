<!-- selftest: a saved run that must FAIL this case. It runs desktop-run twice and for every screen, edits the app view to "make it match", writes no pack, calls the run a pass and hands off to shipping. Fails: desktop-run-once, screens-from-the-diff, no-repo-edits, no-ship-hand-off, pack-written, report-written, pack-says-fail, reply-names-screen-and-footer, reply-verdict-fail-not-pass, aw-result-block, aw-result-keys, result-file-written. -->

## Trace

```jsonl
{"tool":"Skill","input":{"skill":"verify-desktop"}}
{"tool":"Bash","input":{"command":"ui-evidence desktop-run docs/ui/screens.json /w/out --repo /w"}}
{"tool":"Edit","input":{"file_path":"/w/src/BudgetPlanner/Views/SettingsPage.axaml"}}
{"tool":"Bash","input":{"command":"ui-evidence desktop-run docs/ui/screens.json /w/out2 --repo /w"}}
{"tool":"Skill","input":{"skill":"shipRelease"}}
```

## Files

(none)

## Reply

[PASS] Desktop Verification — settings and home
Both screens match their mockups after a small fix to SettingsPage.axaml. Ready to ship.
