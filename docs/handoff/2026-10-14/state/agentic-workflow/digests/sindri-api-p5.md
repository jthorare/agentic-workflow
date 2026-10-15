# Sindri API P5 handoff (contract unit, partly done)

Worktree `/Users/thor/repos/agentic-workflow/.claude/worktrees/sindri-api-p5`, branch `feat/sindri-api-p5-stream`, base 86d1e596 (P3 contract).

## Done: commit 55655a60
Whole-solution `dotnet build -warnaserror` passed with 0 warnings. **`dotnet format --verify-no-changes` was NOT clean**: rc=2, one finding in
`src/Sindri/Aw.Sindri.Features/Aw.Sindri.Features.csproj` that was never read in full, because heavy slots were full.
Run `dotnet format AgenticWorkflow.slnx`, check the diff and commit it as `style:`. Do this first.
- Core/Ledger: `ILedgerReader.Scan`, `LedgerFilter`, `LedgerScan`, `ILedgerSignal`, and the `LedgerSignal` stub, registered in `AddSindriCore`.
- Core/Events: `TypeFilter` (stub), `ApiViews`, `StreamLimits` (stub), `IStreamSlots`, `PublicEvent`, `PublicEvents.For` (stub), `IItemRepos`,
  and `EventVisibility` (stub). `EventCatalogEntry.Invalidates` is filled for the G7 rows; the context.* and workflow.* rows are left empty, for unit G to decide.
- `SND-API-012 StreamsFull`, plus its row in `docs/csharp/sindri.md`.
- New typed ids `EventId` (Guid) and `TraceId` (string), each with Holder and Tests files and `IdsJsonContext` entries.
- `StreamEvents`: the request gains `Item` and `Types` (unused for now), the response gains `ScannedTo`, the service maps to `PublicEvent`, and `EventView` is gone.
- Host: `EventStream` uses `SseItem<PublicEvent>`. `StreamErrorFrame` is in `SindriJsonContext`.
- Testing: `InMemorySindriStore` has a `(clock, probe, ILedgerSignal)` overload that pulses after a new append, and its `Scan` is a stub.
  `LedgerReaderContract` gains 4 Scan cases. There are a new `LedgerSignalContract` and `Tests/Ledger/LedgerSignalContractTests`.

## Deviations from the plan, to report to the lead
- The plan's `Features/Events/*` live in `Core/Events` instead, because AW1003 counts `Features/Events` as a slice.
  `EventScope` is renamed `EventVisibility`, because `Core.Events.EventScope` already exists.
- `PublicEvent` uses typed ids (AW1001): `EventId Id`, `ItemId? Item`, `CorrelationId`, `EventId? CausationId` and `TraceId? TraceId`.
  The JSON shape is unchanged.

## Left in the contract unit
1. Format fix (above).
2. Re-record the `events/process-started` golden: `AW_GOLDEN_UPDATE=1`, ApiGoldenTests.
3. Run the red slices through the generator under a heavy slot (it builds Features).
   `node scripts/scaffold/new-slice.mjs --slice-only --package Sindri --surface http --slice ReadLedger --behavior pages_public_envelopes_after_a_cursor_with_sanitized_payloads --route "GET /ledger"`, and the same for
   `GetEventCatalog --behavior lists_each_public_type_with_the_views_it_invalidates --route "GET /events/catalog"`.
   Then edit them to match plan §1.3: `ReadLedgerRequest(long After, ItemRef? Item, string? Type, int Max = 100) : IQuery<LedgerPage>`,
   `LedgerPage(IReadOnlyList<PublicEvent>, long? Next)`, `EventCatalogView` and `EventTypeView`, plus the JSON context entries.
   An earlier generator run failed only because of build errors, all fixed now.
4. Gate: `tools/gate.sh`. The unit lane's planned reds are P3's 7, plus P5's: LedgerSignalContractTests ×4,
   the Scan cases ×4 on each of InMemory and Sqlite (8), ReadLedger ×1 and GetEventCatalog ×1.
   The InMemory Scan cases are red because `InMemorySindriStore.Scan` is a unit B stub; the type-glob cases also need unit A's `TypeFilter`.
5. Commit, then message the lead.

## Next unit after the contract: A (`TypeFilter`, `PublicEvents`), B (`Scan`), C (`LedgerSignal` wiring) in parallel
Heavy slots were very contended: one slot was held from 17:14 and each wait ran 10–30 min.
