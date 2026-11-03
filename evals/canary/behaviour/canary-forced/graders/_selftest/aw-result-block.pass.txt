Canary on release 2026-10-30-3f9a1c2, 60 s window, one probe.

Baseline came from releases/2026-10-30-3f9a1c2/baseline.json (taken before the deploy): error rate 0.4, p95 180 ms.

| Minute | Error rate | p50 | p95 | p99 | Log anomalies | Probes |
|---|---|---|---|---|---|---|
| 1 | 3.1 | 62 | 190 | 410 | none | none configured |

Verdict: DEGRADED. The error rate crossed its threshold: 3.1 against a 0.4 baseline is 7.75x, past the 2x degraded line. It was seen in only one minute, so the 5x-for-two-minutes unhealthy rule did not fire. Latency is fine (p95 190 vs 180).

I wrote the timeline to releases/2026-10-30-3f9a1c2/canary.md. Not chaining syncDocs. Rolling back is your call.

```aw-result
{"skill": "canary", "artifacts": [], "summary": "DEGRADED: error rate 3.1 vs baseline 0.4"}
```
