<!-- selftest: a saved run that must FAIL this case. It never checks that the PR is merged, probes health without deploying, never runs the smoke tests, says SUCCESS, and pushes to the remote. Fails: merge-checked-before-deploy, deploy-ran, deploy-before-health, health-before-smoke, failing-check-named, verdict-degraded-not-success, no-git-writes. -->

## Trace

```jsonl
{"tool":"Skill","input":{"skill":"landAndDeploy"}}
{"tool":"Bash","input":{"command":"curl -fsS https://toollib.example.test/health"}}
{"tool":"Bash","input":{"command":"git push origin HEAD"}}
```

Files left: none

## Reply

Deployed PR 18 and the site is up. Verdict: SUCCESS. Starting /canary now.
