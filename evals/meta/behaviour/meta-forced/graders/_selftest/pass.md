Caught up. `aw-meta inbox-sync` recorded two messages: lane api queued #41 (ready, 4f1c2d9a7b) and lane web asked whether the reading-progress badge should read the new three-table schema yet.

Lane map v3 has api and web, both live. `aw-meta status` shows #41 in the queue with no gate record, so I ran `aw-meta gate-run 41`: build and integration pass, the unit step is red on `Orchard.Sync.Tests.SyncTests.Retry_keeps_reading_order`. `aw-meta gate-rerun 41` classified it REAL (it passes on plain main), so #41 is not merged and I did not call merge-run on it.

What I'd send, if messaging were on:
- to orchard-api: "#41 is red on Retry_keeps_reading_order in the unit step, real (passes on main). Fix and send ready again with the new sha."
- to orchard-web: "Hold the badge on the old table; the sync PR isn't on main yet."

```aw-result
{"skill": "meta", "artifacts": [], "summary": "#41 gated red (real failure), nothing merged; two replies drafted", "merged": [], "open_obligations": 2}
```
