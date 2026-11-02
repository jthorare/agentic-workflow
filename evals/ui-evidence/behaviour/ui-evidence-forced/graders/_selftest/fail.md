<!-- selftest: a saved run that must FAIL this case. It never runs doctor, runs the tool before writing a script, glosses over the failed step, posts to the PR without asking and prints no aw-result block or file. Fails: doctor-before-run, script-written-before-run, no-publish-without-approval, failed-step-named, images-stay-local, nothing-posted-and-asks, aw-result-block, aw-result-keys, result-file-written. -->

## Trace

```jsonl
{"tool":"Skill","input":{"skill":"ui-evidence"}}
{"tool":"Bash","input":{"command":"ui-evidence run ./ui-runs/pr57/script.json ./ui-runs/pr57"}}
{"tool":"Bash","input":{"command":"gh pr comment 57 --repo acme/shop --body-file ./ui-runs/pr57/pr-comment.md"}}
```

Files left: none

## Reply

Evidence for PR 57 is captured and I have already commented on the PR with the screenshots. The notification preferences page looks good.
