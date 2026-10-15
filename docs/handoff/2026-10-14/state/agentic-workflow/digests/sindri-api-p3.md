# Sindri API P3 — contract unit handoff (partial)

Worktree `~/repos/agentic-workflow/.claude/worktrees/sindri-api-p3`, branch `feat/sindri-api-p3-items`, on P2 tip 776b34b2.
Plan: `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-api-lane/p3-plan.md` (§1 contract unit, U2-API, Lead answers,
Owner decisions, and the new **README X61** section). Common brief: scratchpad `p2-common.md` (read "P2" as "P3").
The first agent stopped early because its context grew too large (about 390k tokens). A fresh agent should continue from here.

## Done (committed)
- `c050d4dc feat: ItemId, ItemAlias, ItemRef and RecordKey`: `Core/Ids/{ItemId,ItemAlias,RecordKey,ItemRef,ItemRefJsonConverter}.cs`,
  PublicAPI lines, `tests/Aw.Sindri.Tests/Ids/{ItemId,ItemAlias,RecordKey,ItemRef}{Tests,Holder}.cs` + IdsJsonContext entries.
  The Sindri build is clean, and format was clean. The unit lane ran 1216 tests with 3 failures, all P2's planned reds:
  ListServices, GetProfile and RebuildViews. The all-lanes baseline before P3 was 1388 total, 3 failed (the same three).

## Left (contract unit, in order)
1. Core.Items: `ItemState` (kebab, JsonStringEnumConverter + JsonStringEnumMemberName like `Core/Events/ServiceOutcome.cs`),
   `QueueGroup` (camelCase names), `CloseReason`, `QueueGroups` (Order = [WaitingOnYou, WithYou, Running, Queued], owner decision: four groups;
   GroupOf), `ItemStates.IsClosed`, `ItemActions.For/Primary` (one class), `ItemDecisions.Close`, `ItemScope.Visible`, `IItemResolver`,
   `TimelineLine.For`. Plan: behaviour belongs to unit D, so stub the bodies with `throw new NotSupportedException(...)`. QueueGroups.Order is real data.
2. Events: `EventTypes.ItemCreated/ItemStateChanged/ItemLinked`; `Events/ItemCreated(CommandId, ItemId, Title, RepoName?, RecordKey? Link, string? Body, string? Owner)`
   (**X61: `owner: string?`, always null in P3**), `ItemCreatedPublic` (no body; include owner), `ItemStateChanged`, `ItemLinked`; three
   `EventCatalog.All` rows; `SindriErrors.ItemNotFound` (SND-ITEM-404, NotFound) / `ItemConflict` (SND-ITEM-409, Conflict) in `All`;
   error rows in `docs/csharp/sindri.md` (P2 did the same); every payload in `Host/SindriJsonContext.cs`.
3. Core.Text: `ITextScrubber`, `SafeText` (Escape and Line stubbed for unit A; Clean composes them); `tests/Aw.Sindri.Testing/TextScrubberContract.cs`
   and `PassThroughScrubber`, bound in the Tests project. Skip the secret case for the pass-through with `Assert.SkipWhen` (Judge tests use it).
4. Views: `ViewNames.Items/ItemAliases/ItemTimeline` in `All` (safe: SqliteViewStore.Reset skips views it doesn't know);
   `ViewSchema.Version = 2`; `ItemRow` (+ **`string? Owner`**, X61), `AliasRow`, `TimelineRow`; `IViewReader.Range<TRow>(view, afterKey, beforeKey?, max, ct)`.
   Implement it in `tests/Aw.Sindri.Testing/InMemoryViewStore.cs` and `InMemoryViewRows.cs`. Stub it in `Infrastructure/Views/SqliteViewReader.cs`
   (NotSupportedException; unit B implements it). `ViewStoreContract.Range_returns_keys_between_the_bounds_in_order` uses FixtureProjection
   keys into ViewNames.Services. The Sqlite binding of that test is a planned red, so name it in the commit message.
   `EmptyText.Queue/Group/Items/Timeline` are real, with the plan's exact sentences. Add a test that every QueueGroup member has a sentence.
   The SQL migration `0002_items.sql` (with the X61 nullable `owner` column) belongs to unit B, not the contract unit.
5. Slices: `node scripts/scaffold/new-slice.mjs --slice-only --files-only --package Sindri --slice <S> --behavior <b> --surface http --route "<R>"`
   for CreateItem, CloseItem, GetQueue, ListItems, GetItem and GetItemTimeline (plan §1.6 table). Shape the records by hand: ItemSummary and ItemView gain
   **`string? Owner`** (X61). Put the ItemSummary file in `Features/Items/` with `ItemSummaries.From` stubbed. Register the slices with AddCommand/AddQuery,
   using AcceptAllValidator for the queries, at the anchors in `Features/SindriFeaturesServiceCollectionExtensions.cs`. Write the endpoints in
   `Host/Endpoints/` (`{itemId}` binds as a string and goes through ItemRef.TryParse, giving 400 SND-CLI-003), lines in `EndpointTable.cs`,
   and entries in the JSON context. Then remove the MA0182 SuppressMessage the generator adds.
6. Catalog flip: **put it in the same commit as the slices.** `ARCH-CQRS-003` (`Every_command_and_variant_has_a_catalog_row`) and
   `Every_ready_row_route_is_mapped` would otherwise fail between the two commits. Move the 3 rows from `Host/Catalog/PendingRows.cs` to a new
   `Catalog/ItemRows.cs` (ready, bound types, CLI `sindri item close <itemId> [--version <n>]` and the `--cancelled` form). Then
   `CommandCatalog.Rows = [.. ShellRows.All, .. ItemRows.All, .. PendingRows.All]`, `CatalogRules.ShippedPhases += "P3"`, and re-record
   `tests/Aw.Sindri.Tests/Golden/Api/commands/catalog.json`.

## Notes
- PublicAPI: build `src/Sindri/Aw.Sindri.Core` and the other projects, then grep `RS0016: Symbol '...'` and append the symbols. A record struct's
  `~override ...Equals(object obj)` line keeps its `~` (oblivious).
- Builds and tests go through scratchpad `p3/run.sh <cmd>` (heavy.sh with a retry loop). Slots are contended; one wait took 7 minutes.
- Deviations so far: no RDG route-binding tests for the ids, because they bind as strings (P1 rule). The catalog flip is merged into the slices commit.
- 2026-10-12 lead: c050d4dc pushed (feat/sindri-api-p3-items). Confirmed: the contract unit stubs the pure helpers (ItemStates, ItemActions, ItemDecisions, ItemScope, TimelineLine, SafeText), with bodies that throw NotSupportedException, so each later unit keeps its red commit. QueueGroups.Order (four groups) and the empty-text sentences are real. The catalog flip goes in the same commit as the red slices.


## Agent 2 (2026-10-12): stopped at a clean commit (context guard). Tip 6f5137f1
Commits: `380d6a4c` merge P2 4e8e749e (one PublicAPI conflict, both sides kept; build + format clean) ·
`9fc83f01` Core.Items (ItemState/QueueGroup/CloseReason, QueueGroups Order (4) + GroupOf real; ItemStates, ItemActions,
ItemScope, TimelineLine stubbed) + item events (ItemCreated(+Owner), ItemCreatedPublic(+Owner), ItemStateChanged, ItemLinked),
3 EventCatalog rows, SND-ITEM-404/409 + docs rows, JSON context · `6f5137f1` Core.Text (ITextScrubber, SafeText stubs + Clean),
TextScrubberContract + PassThroughScrubber + binding, ViewNames x3, ViewSchema 2, ItemRow(+Owner)/AliasRow/TimelineRow,
IViewReader.Range (in-memory real, Sqlite stub), ViewStoreContract Range test (Sqlite binding = planned red), EmptyText
Queue/Group/Items/Timeline, ItemDecisions.Close stub, IItemResolver, tests/Aw.Sindri.Tests/Items/QueueGroupsTests.cs.
Each commit: whole-solution build 0 warnings, format clean. Tests NOT yet run on these commits.

### Left: step 5+6 (one commit: red slices + catalog flip), then re-record the catalog golden, then run tests
**All slice files are already written** in scratchpad `p3/slices/new/` (mirrors repo paths; copy with `cp -R new/src new/tests <worktree>/`):
- Features: CreateItem (Request, ItemCreatedResponse, Service stub, Validator), CloseItem (Request : ICommand<CommandAccepted>,
  IClassifiedCommand, Variant from Reason; Service; Validator), GetQueue (Request, QueueResponse, QueueGroupView, Service),
  ListItems, GetItem (ItemView with Owner), GetItemTimeline (TimelineResponse). Query validators deleted (use AcceptAllValidator).
- Core/Items/ItemSummary.cs (+Owner) and ItemSummaries.From (stub). **Deviation:** in Core.Items, not Features/Items, because
  `Features.Items` counts as a slice and GetQueue/ListItems using it would fail ARCH-SLICE-001 / AW1003.
- Host/Endpoints: ItemRoute.cs (Item/State/Repo parsers → SND-CLI-003), CloseItemBody.cs, six *Endpoint.cs. Host/Catalog/ItemRows.cs.
- tests: Aw.Sindri.Testing/A.cs (full replacement: adds `A.FirstItemId`, `A.Item()` + the six builder lines), ItemRowBuilder.cs,
  six request builders; Aw.Sindri.Tests/<Slice>/<Slice>Tests.cs (generated reds). Generated ItemIdRouteBindingTests was deleted (deviation).
Still to do by hand in the tree after copying:
1. `Features/SindriFeaturesServiceCollectionExtensions.cs`: commands `AddScoped<IValidator<CreateItemRequest>, CreateItemValidator>().AddCommand<CreateItemRequest, ItemCreatedResponse, CreateItemService>()`,
   same for CloseItem (CommandAccepted); queries AddQuery with `AcceptAllValidator<…>` for GetItem(ItemView), GetItemTimeline(TimelineResponse),
   GetQueue(QueueResponse), ListItems(ListItemsResponse). Keep lines sorted under the anchors; add usings.
2. `Host/Endpoints/EndpointTable.cs`: queries MapGetItem, MapGetItemTimeline, MapGetQueue, MapListItems; commands MapCloseItem, MapCreateItem (sorted).
3. `Host/SindriJsonContext.cs`: CreateItemRequest, ItemCreatedResponse, CloseItemBody, CloseItemRequest?, QueueResponse, QueueGroupView,
   ItemSummary, ListItemsResponse, ItemView, TimelineResponse (+ GetQueueRequest etc. only if a test needs them).
4. `Host/Catalog/PendingRows.cs`: delete the 3 P3 rows; `CommandCatalog.Rows = [.. ShellRows.All, .. ItemRows.All, .. PendingRows.All]`;
   `tests/Aw.Sindri.Tests/Catalog/CatalogRules.cs` ShippedPhases += "P3".
5. Build whole sln, `python3 p3/api.py <log>` appends RS0016 lines (it also drops RS0017 ones), rebuild until clean; format; commit
   `test: red CreateItem, CloseItem, GetQueue, ListItems, GetItem and GetItemTimeline slices; CreateItem and CloseItem rows ready (P3 shipped)`.
6. Re-record `tests/Aw.Sindri.Tests/Golden/Api/commands/catalog.json`: `AW_GOLDEN_UPDATE=1 dotnet test --project tests/Aw.Sindri.Tests --no-build
   --filter-method "*ApiGoldenTests*"` (check the exact MTP filter), review the diff (only the 3 rows), commit `test: re-record the command catalog golden`.
7. Run Aw.Sindri.Tests `--filter-not-trait "Lane=heavy"` and Aw.Architecture.Tests. Expected reds: P2's 3 (ListServices, GetProfile, RebuildViews),
   the 6 slices, SqliteViewStoreContractTests.Range_…. Watch for: EventCatalog tests over KeyShape, docs-vs-SindriErrors test, JSON context
   coverage tests, CatalogCoverageRules (classified CloseItem has variant rows only), AW1004 on the ternary endpoint lambdas.
Helpers: `p3/run.sh` (heavy slot loop; slots are very contended: a whole-sln build took 10–20 min of waiting), `p3/api.py`.
Deviations so far (add to the plan): Core.Items ItemSummary (above); commit sequence merged (items+events in one, text+views in one);
TextScrubberContract skips through an overridable `SkipUnlessItRecognisesSecrets()` the binding implements with `Assert.Skip`
(Aw.Sindri.Testing has only xunit.v3.extensibility.core, no Assert); ItemRow/ItemView put `Owner` before `Sequence`/after `Actions`.
- 2026-10-12 lead: pushed 6f5137f1. Answer: ItemSummary in Core/Items is accepted; don't exempt a Features folder from the slice rule.
- 2026-10-14 lead: CONTRACT UNIT DONE (c050d4dc..86d1e596, pushed). 86d1e596 merges P2 8744d4eb (with main d4de828b). Fixes made in the merge: AddSindriFeatures split into AddServices/AddCommands/AddQueries; Sindri's RecordKey uses TypeInfoPropertyName "ItemRecordKey". Planned reds: the six slices and SqliteViewStoreContractTests.Range. NEXT: unit A, per p3-plan.md §7.

## UNIT A DONE (2026-10-14). Tip 9d904785
Commits: `9170d3ea` red SafeText theories · `8237dfdc` red RedactingScrubber contract binding (stub registered as ITextScrubber) ·
`fd053b2f` SafeText real · `9d904785` RedactingScrubber over `Redactor.Default` (`Infrastructure/Text/`), registered in
AddSindriInfrastructure, Infrastructure -> Aw.Redaction project ref, deps.yaml Sindri -> Redaction `current`, lock files.
Gate at 9d904785: build 0 warnings, format clean, arch 101/101, Sindri tests 1564 pass / 1 skip / 7 fail = the planned reds
(CreateItem, CloseItem, GetQueue, ListItems, GetItem, GetItemTimeline, SqliteViewStoreContractTests.Range).
SafeText decisions (for review): Escape -> C0/C1/DEL as `\xNN` (uppercase hex); Unicode Format (Cf, so zero-width, bidi, BOM, tags
U+E00xx), LineSeparator/ParagraphSeparator and lone surrogates as `\u{XXXX}`; a CSI/OSC is escaped whole because its ESC/BEL/C1
bytes are; tab, LF, CR are left for Line. Line folds each run of tab/CR/LF to one space, trims, cuts to max with '…' (… counted,
never splits a surrogate pair), max < 1 throws. ZWJ inside emoji sequences is escaped too (safe over pretty).
Notes: the lone-surrogate case is a Fact, because xUnit attribute metadata turns `\uD800` into U+FFFD. RecipeWorkflowTests.
Complete_run_1_1 failed once under slot contention (Temporal timing) and passed on rerun; not caused by this unit.
Background tasks get killed after ~10 min (exit 144); run long heavy.sh waits with nohup and poll the log.
NEXT: unit B (views.db tables and Range), per p3-plan.md §2/§7.

## UNIT B PAUSED (2026-10-14, lead asked to stop; #190/#213 take priority). NOT done.
- `857e3992` red: ViewsFileTests (tables list incl. view_items/aliases/timeline + owner column; schema v1 file gains tables and resets) + the existing Range red.
- Next commit (tip, labelled "wip"): fix, NEVER BUILT OR RUN: Migrations/0002_items.sql (3 tables, nullable `owner` on view_items), ViewSql map entries, ViewTable.SelectAfter/SelectBetween, SqliteViewRows.Range (+Decode helper), SqliteViewReader.Range.
- Build/test never got a lock slot (queued behind the session lock), so green is unproven. Backup branch wip/sindri-api-p3 holds the same fix.
- To resume: `heavy.sh dotnet build tests/Aw.Sindri.Tests -warnaserror -v q -clp:ErrorsOnly`, then `heavy.sh dotnet test --project tests/Aw.Sindri.Tests --no-build --filter-class '*ViewsFileTests' --filter-class '*SqliteViewStoreContractTests' --filter-class '*InMemoryViewStoreContractTests'`. Fix anything red, rename/amend the wip commit to `feat: ...`, then run the whole gate once. Possible gotchas: PublicAPI analyzers (SqliteViewRows.Range is public on an internal class: probably fine), Bind of int `$max`, Migrations[0] list literal type in the test.
- Expected gate reds after B: the six slices only. Next unit: C (projections).
