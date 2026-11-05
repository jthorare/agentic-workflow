# #2080 hunt (2026-11-05)

The QueuedUpdateTests takeover-behind-cancel red has only shown in the full Engine non-heavy run, and the history dump (#2091) only prints on a red. So this hunt runs that full suite up to 5 times, one heavy slot at a time, from main 1f57c0c:

    heavy dotnet test --project tests/Aw.Sindri.Engine.Tests --no-build --filter-not-trait "Lane=heavy"

The hunt stops on the first QueuedUpdateTests red and keeps that round's log. A red in any other test is logged and the next round runs.

## Rounds

| Round | Start (UTC) | Result |
|---|---|---|
| 1 | 20:09:36 | 621 passed, 0 failed |
| 2 | 20:10:58 | 621 passed, 0 failed |
| 3 | 20:12:41 | 621 passed, 0 failed |
| 4 | 20:15:26 | 621 passed, 0 failed |
| 5 | 20:17:59 | 621 passed, 0 failed |

No red in 5 rounds, so no dump. The full Engine suite by itself doesn't reproduce it either. The reds so far came from meta gates, where the Engine tests run beside the rest of the solution's tests (#2074's gate). The next dump will come from a gate red.
