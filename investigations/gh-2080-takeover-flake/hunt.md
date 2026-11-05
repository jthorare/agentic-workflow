# #2080 hunt (2026-11-05)

The QueuedUpdateTests takeover-behind-cancel red has only shown in the full Engine non-heavy run, and the history dump (#2091) only prints on a red. So this hunt runs that full suite up to 5 times, one heavy slot at a time, from main 1f57c0c:

    heavy dotnet test --project tests/Aw.Sindri.Engine.Tests --no-build --filter-not-trait "Lane=heavy"

The hunt stops on the first QueuedUpdateTests red and keeps that round's log. A red in any other test is logged and the next round runs.

## Rounds

(filled in when the hunt ends)
