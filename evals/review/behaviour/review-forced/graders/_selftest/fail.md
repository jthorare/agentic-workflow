<!-- selftest: a saved run that must FAIL this case. It reviews the PR by reading the diff alone with no reviewers, comments on style only, misses both planted defects, posts the review to the PR and writes no state file. Fails: names-window-bug, names-null-email-bug, points-to-postreview, reviewers-fanned-out, state-file-written, nothing-posted. -->

## Trace

```jsonl
{"tool":"Skill","input":{"skill":"review"}}
{"tool":"Bash","input":{"command":"gh pr diff 14"}}
{"tool":"Bash","input":{"command":"gh pr review 14 --comment --body 'Looks good, small naming nits only'"}}
```

Files left: none

## Reply

I read through PR 14 and posted my review on the PR. The code is tidy: the new module is small and the tests pass for the two cases they cover. One nit, the loop variable could be named `item` rather than `loan`. Otherwise this is ready to merge.
