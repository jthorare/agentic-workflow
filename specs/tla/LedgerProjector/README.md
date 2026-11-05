models: src/Sindri/Aw.Sindri.Host/Server/Projector.cs:54 src/Sindri/Aw.Sindri.Infrastructure/Views/SqliteViewStore.cs src/Sindri/Aw.Sindri.Core/Views/ProjectorProgress.cs src/Sindri/Aw.Sindri.Core/Health/HealthSummary.cs
# LedgerProjector

- **Models:** `src/Sindri/Aw.Sindri.Host/Server/Projector.cs` (`Pass`, `Prepare`, `Fold`, `Finish`, the 30 s stall gate), `src/Sindri/Aw.Sindri.Infrastructure/Views/SqliteViewStore.cs` (`Open`, `Reset`, `Apply`: one transaction per page, a savepoint per event, then `FinishRebuild`), `src/Sindri/Aw.Sindri.Core/Views/ProjectorProgress.cs` (the published status, the in-memory rebuild request) and `src/Sindri/Aw.Sindri.Core/Health/HealthSummary.cs` (the health word). Each SQLite transaction is one step. A crash between steps loses the projector's memory, and serve restarts.
- **Checks:** `ExactlyOnceInOrder` (views.db holds events 1..cursor, each once, in ledger order, across crashes and `views rebuild`), `CursorWithinLedger`, `NoPoisonFolded`, `AllEventsLand` (once every event can fold, the views catch up) and `HealthyMeansProgress` (Healthy while behind leads to caught up or not Healthy).
- **Bounds (`LedgerProjector.cfg`):** 5 events, pages of 2, event 3 fails to fold until a fixed build runs, 2 crashes, 2 rebuild requests, passes never throw. That's about 5k states, under 2 s.

## Known gaps (not in the cfg until their fix lands)

| Check | Turn on | What TLC shows | Issue |
|---|---|---|---|
| `StalledNeverHealthy` | add to INVARIANTS | serve restarts with views.db stalled at 3, and status says Healthy until the first pass publishes | #2053 |
| `HealthyMeansProgress` | `CanThrow = TRUE` | a pass that throws every time publishes nothing, so the last Healthy stands forever | #2053 |
| `RebuildHappens` | add to PROPERTIES | an accepted `views rebuild` is lost when serve crashes before the reset commits | #2054 |

Run: `scripts/tlc-check.sh specs/tla/LedgerProjector`, or `bash skills/formal-verify/scripts/tlc.sh check specs/tla/LedgerProjector/LedgerProjector.tla`.
