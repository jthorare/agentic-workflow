# API P3 — Items and the queue: implementation plan

Spec: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (API) §2.1 (fact keys, `SND-ITEM-409`, waiting for
the views), §2.2, §3 (typed ids: `ItemId`), §3.1 (`GET /queue`, `GET /items`, `GET /items/{itemId}`,
`GET /items/{itemId}/timeline`, session repo scope), §3.2 (`CreateItem`, `CloseItem`, item versions), §3.4
(`item.created`, `item.state-changed`, `item.linked`), §3.6 (states, groups, `EmptyText`, agent text), §4.2, §7, §9
(`SND-ITEM-404/409`), §12, Phases P3 row, Parallel delivery; amended by the UI spec `2026-10-11-sindri-ui-design.md`
§6.7 E-8 (`CloseItem:done`/`:cancelled`), E-16 (checked `cli`), E-17/§18.1 (goldens), E-18 (per-group `empty`), §18.2
(stall trigger) — UI wins (README X52). README X16 (one timeline), X23 (engine P4 needs items), X42 (`item.linked`,
`CreateItem` `link?`). "§" = API spec unless prefixed.

Base: P2's branch `feat/sindri-api-p2-projector` once its slices are green (units C and D: projections, `AwaitingViews`,
`IViewReader` used by slices). Branch `feat/sindri-api-p3-items`, worktree `.claude/worktrees/sindri-api-p3`. Rebase onto
`main` when #190 merges.

## 0. Goal

Sindri gets work items. A desk human creates an item (`POST /items`, `sindri item add`) and closes it
(`POST /items/{itemId}/close`, `sindri item close`, variants `CloseItem:done` and `CloseItem:cancelled`). The ledger
records `item.created` and `item.state-changed` under fact keys, so a stale close loses with `SND-ITEM-409`. Projections
fold items, aliases and per-item timelines into `views.db`. Four queries read them: the queue in its fixed §3.6 groups,
each with its own `empty` sentence (E-18), the item list with `state` and `repo` filters, item detail with `actions`
(`ItemActions.For`), and the item timeline. Session callers see only their repo's items. Text from humans or trackers
is scrubbed and escaped when the projection writes it.

Phase signals (spec P3 row), each mapped to the verification plan:
1. `human item add "Try the queue"`, then `curl GET /sindri/queue | jq -S 'del(.asOf)'` equals
   `human queue --json | jq -S 'del(.asOf)'` (both human).
2. `item close` with a stale version gets `SND-ITEM-409`.
3. An empty queue prints its `EmptyText`.
4. A `POST /items` with no caller header gets 403.
Plus E-8 (the two `CloseItem` rows ready, `:cancelled` destructive), E-18, session repo scope, the alias route, the
timeline, and `CloseItem` as the first real `ReadsViews` command (P2 G7).

---

## 1. Contract unit (runs first, committed alone; the UI, engine and P5 lanes code against it)

Goal: every shared type, port, row, event, catalog entry, error code and JSON-context entry exists; build at 0
warnings; all P1/P2 tests green; **exactly the six generated slice red tests are red** (CreateItem, CloseItem, GetQueue,
ListItems, GetItem, GetItemTimeline), plus the named contract-binding reds listed in the commit message.

### 1.1 Core — ids (`Core/Ids`, `docs/csharp/typed-ids.md`)
```csharp
// ItemId: always "ITM-" + 32 lowercase hex (Guid v7, "N" format); ^ITM-[0-9a-f]{32}$. String typed id, Vogen like RepoName.
public readonly partial struct ItemId : ITypedId<ItemId, string> { public static ItemId New(IIdGenerator ids); }
// ItemAlias: a tracker key such as ENG-142: ^[A-Z][A-Z0-9]{0,9}-[1-9][0-9]{0,8}$ (Linear's shape). Never an id.
public readonly partial struct ItemAlias : ITypedId<ItemAlias, string>;
// ItemRef: what an {itemId} route segment or ?item= value holds: an ItemId or an ItemAlias (TryParse; else 400 SND-CLI-003).
public readonly record struct ItemRef(ItemId? Id, ItemAlias? Alias) { public static bool TryParse(string? text, out ItemRef value); }
// RecordKey: a tracker record key, "<source>:<kind>/<id>", e.g. linear:issue/<uuid>: ^[a-z][a-z0-9-]{0,19}:[a-z][a-z0-9-]{0,19}/[A-Za-z0-9-]{1,64}$.
public readonly partial struct RecordKey : ITypedId<RecordKey, string>;
```
Each gets its route-binding test and `IdsJsonContext` entry (generator `--id`).

### 1.2 Core — items (`Aw.Sindri.Core.Items`)
```csharp
public enum ItemState { New, Queued, Running, NeedsDecision, NeedsAnswer, Parked, TakenOver, Done, Cancelled }  // kebab JSON
public enum QueueGroup { WaitingOnYou, WithYou, Parked, Running, Queued }                                       // camelCase JSON
public enum CloseReason { Done, Cancelled }                                                                       // kebab JSON
public static class QueueGroups                                    // THE one place the group set lives (PENDING owner, U2-API Q1)
{   public static IReadOnlyList<QueueGroup> Order { get; }         // API §3.6 today: [WaitingOnYou, WithYou, Running, Queued]
                                                                   // UI §4.5 answer: insert Parked after WithYou (one line)
    public static QueueGroup? GroupOf(ItemState state); }          // §3.6 table; Parked → WaitingOnYou today (→ Parked under UI §4.5);
                                                                   // Done/Cancelled → null. A test asserts every Order member has a
                                                                   // sentence and every open state maps into Order
public static class ItemStates
{   public static bool IsClosed(ItemState state); }
public static class ItemActions                                    // the primary action (UI §4.5 "the row's primary action from `actions`")
{   public static ActionId? Primary(ItemState state); }            // always a member of For(state) or null; P3: null for every state
                                                                   // (P4: Decide for needs-decision; P11: engine ids, e.g. handback for taken-over)
public static class ItemActions                                    // §3.6; the ids the UI intersects with the catalog
{   public static IReadOnlyList<ActionId> For(ItemState state); }   // New/Queued/NeedsDecision → [CloseItem:done, CloseItem:cancelled];
                                                                   // closed → []; engine states → [] until P11/engine add theirs
public static class ItemDecisions                                  // pure decisions for CloseItem (§2.1 step 2)
{   public static Result<ItemStateChanged> Close(ItemRow item, CloseReason reason, long seenVersion, CommandId command); }
                                                                   // version != seen → SND-ITEM-409 "changed since you looked";
                                                                   // closed → SND-ITEM-409 "already <state>"
public static class ItemScope                                      // §3.1 session repo scope, pure
{   public static bool Visible(CallerContext caller, RepoName? itemRepo); }  // human/service: all; session: repo == caller.Repo (null == null)
public interface IItemResolver                                     // read port: resolves a route's ItemRef through the views
{   ValueTask<Result<ItemRow>> Resolve(ItemRef reference, CallerContext caller, CancellationToken ct); }
                                                                   // unknown alias/id, or outside a session's repo → SND-ITEM-404
public static class TimelineLine                                   // "one scrubbed line" per event (§3.1)
{   public static string For(LedgerEvent recorded, JsonSerializerOptions json); }   // item.created "Created: <title>", state-changed
                                                                   // "new → done", item.linked "Linked to <record> (<alias>)", other "<type>"
```

### 1.3 Core — events, catalog, errors
- `EventTypes`: `ItemCreated = "item.created"`, `ItemStateChanged = "item.state-changed"`, `ItemLinked = "item.linked"`.
- `Events/ItemCreated.cs (CommandId CommandId, ItemId Item, string Title, RepoName? Repo, RecordKey? Link, string? Body)`;
  public payload `ItemCreatedPublic(CommandId, ItemId, string Title, RepoName? Repo, RecordKey? Link)` (no body, §5.3).
- `Events/ItemStateChanged.cs (CommandId CommandId, ItemId Item, ItemState From, ItemState To, long Version, string? Reason)`.
- `Events/ItemLinked.cs (CommandId CommandId, ItemId Item, RecordKey Record, ItemAlias? Alias)`.
- `EventCatalog` rows: `item.created` public `ItemCreatedPublic` `item.created:<itemId>`; `item.state-changed` public
  `item.state-changed:<itemId>:v<version>`; `item.linked` public `item.linked:<itemId>:<record>`. (P5 adds `invalidates`
  to these rows; see §9.)
- `SindriErrors`: `ItemNotFound` `SND-ITEM-404` NotFound "No such item"; `ItemConflict` `SND-ITEM-409` Conflict
  "The item changed since you looked"; both in `All`.

### 1.4 Core — text (`Aw.Sindri.Core.Text`; shared with P5 and P4)
```csharp
public interface ITextScrubber { string Scrub(string text); }      // secrets out (adapter over Aw.Redaction in Infrastructure)
public static class SafeText                                       // §3.6 "the shared sanitizer", pure
{   public static string Escape(string text);                      // C0/C1 controls → \xNN, invisible and bidi marks → \u{XXXX},
                                                                   // ESC-introduced CSI/OSC sequences escaped as a whole; never raw
    public static string Line(string text, int max);               // one line: newlines → space, trimmed, cut at max with "…"
    public static string Clean(ITextScrubber scrubber, string text, int max) => Line(Escape(scrubber.Scrub(text)), max); }
```
Contract base `TextScrubberContract` (`Scrub_removes_a_token_shaped_secret`, `Scrub_keeps_plain_text`); the fake
`PassThroughScrubber` binds it in the contract unit (the secret case is skipped for the pass-through and runs for the
real adapter in unit A).

### 1.5 Core — views
- `ViewNames`: `Items = "items"`, `ItemAliases = "item-aliases"`, `ItemTimeline = "item-timeline"`; `All` gains them.
- `ViewSchema.Version = 2` (P3's tables; the bump rebuilds an upgraded `views.db`, §2.3).
- Rows: `ItemRow(ItemId Id, string Title, RepoName? Repo, ItemState State, string? Step, long Version, string CreatedBy,
  DateTimeOffset CreatedAt, DateTimeOffset Since, DateTimeOffset UpdatedAt, RecordKey? Link, ItemAlias? Alias,
  string? Body, string? WaitingOn, string? Instance, long Sequence)` (`WaitingOn` is P4's `DEC-…` id, `Instance` P11's;
  both strings and null in P3, so P3 needs no P4 or engine type); `AliasRow(ItemAlias Alias, ItemId Item, RecordKey Record, long Sequence)`;
  `TimelineRow(long Sequence, DateTimeOffset Time, string Type, string Actor, string Line)` (JSON `{sequence, time, type, actor, line}`, U2-API-4).
- `IViewReader` gains `Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken)` (keys
  ordered, exclusive bounds) for the timeline (`<itemId>/<seq:D19>` keys). `ViewStoreContract` gains
  `Range_returns_keys_between_the_bounds_in_order`.
- `EmptyText` gains `Queue()` "Nothing in the queue. Add one with `sindri item add`." (the UI fixtures' sentence, U2-API-9),
  `Group(QueueGroup)`, a switch with a sentence for **every** member of the enum, `Parked` included: "Nothing is waiting on you.",
  "Nothing is with you.", "Nothing parked.", "Nothing running.", "Nothing queued.",
  `Items(ItemState? state, RepoName? repo)` ("No items." / "No <state> items." / "No items in <repo>."),
  `Timeline()` "Nothing has happened to this item yet.".

### 1.6 Features (generator, `--slice-only --package Sindri --surface http`)
| Slice | `--behavior` | Request (marker added by hand) | Response |
|---|---|---|---|
| `CreateItem` | `appends_item_created_with_a_new_item_id` | `CreateItemRequest(string Title, RepoName? Repo, string? Body, RecordKey? Link) : ICommand<ItemCreatedResponse>` | `ItemCreatedResponse(CommandAccepted Accepted, ItemId ItemId)` → `{accepted:{commandId, sequence}, itemId}` (API §3.2 `ItemCreated(accepted, ItemId)`; U2-API-5; G15) |
| `CloseItem` | `closes_the_item_at_the_version_the_human_saw` | `CloseItemRequest(ItemRef Item, CloseReason Reason, long ItemVersion) : ICommand<CommandAccepted>, IClassifiedCommand` (`Variant` = reason kebab; no protected fields) | `CommandAccepted` |
| `GetQueue` | `groups_open_items_in_the_fixed_order_with_their_empty_text` | `GetQueueRequest() : IQuery<QueueResponse>` | `QueueResponse(IReadOnlyList<QueueGroupView> Groups, string? Empty, AsOf AsOf)`, `QueueGroupView(QueueGroup Group, IReadOnlyList<ItemSummary> Items, string? Empty)` → `{group, items, empty}` (U2-API-2), one per `QueueGroups.Order` member |
| `ListItems` | `lists_items_filtered_by_state_and_repo` | `ListItemsRequest(ItemState? State, RepoName? Repo) : IQuery<ListItemsResponse>` | `ListItemsResponse(IReadOnlyList<ItemSummary> Items, string? Empty, AsOf AsOf)` |
| `GetItem` | `shows_the_item_with_its_version_and_actions` | `GetItemRequest(ItemRef Item) : IQuery<ItemView>` | `ItemView(ItemId Id, string Title, RepoName? Repo, ItemState State, string? Step, DateTimeOffset Since, ActionId? PrimaryAction, IReadOnlyList<ActionId> Actions, long Version, string CreatedBy, string? WaitingOn, string? Instance, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? Body, RecordKey? Link, AsOf AsOf)`: the summary's fields flattened, then the detail (U2-API-3, U2-API-7) |
| `GetItemTimeline` | `lists_the_items_events_after_a_cursor` | `GetItemTimelineRequest(ItemRef Item, long? After) : IQuery<TimelineResponse>` | `TimelineResponse(IReadOnlyList<TimelineRow> Entries, long? Next, string? Empty, AsOf AsOf)`; `next` null on the last page (U2-API-4) |

`ItemSummary(ItemId Id, string Title, RepoName? Repo, ItemState State, string? Step, DateTimeOffset Since,
ActionId? PrimaryAction, IReadOnlyList<ActionId> Actions)` → `{id, title, repo, state, step, since, primaryAction, actions}`
(U2-API-1; no `version`, no `body`, no `link`: U2-API-6, -7) (Features, shared by four slices: `Features/Items/ItemSummary.cs`,
with `ItemSummaries.From(ItemRow)`; one folder owned by the contract unit). Validators: `CreateItemValidator`,
`CloseItemValidator` (unit D), `AcceptAllValidator` for the queries. Registration lines at the anchors.

### 1.7 Host
- Endpoint lines: `queries.MapGetItem()`, `MapGetItemTimeline()`, `MapGetQueue()`, `MapListItems()`;
  `commands.MapCloseItem()`, `MapCreateItem()`. `{itemId}` binds as `string` and parses `ItemRef.TryParse` in the
  endpoint (a bad id is 400 `SND-CLI-003` before the chain, P1 binding rule); `?state=` parses the kebab enum.
- `SindriJsonContext`: every new record and enum (ids, payloads, rows, slices, `ItemSummary`, `ItemRef` as a string).
- Catalog: `CreateItem`, `CloseItem:done`, `CloseItem:cancelled` move from `PendingRows` to a new
  `Catalog/ItemRows.cs` (G16; `CommandCatalog.Rows = [.. ShellRows.All, .. ItemRows.All, .. PendingRows.All]`) as
  `ready`, bound to `CreateItemRequest` / `CloseItemRequest` with their variants; CLI forms
  `sindri item add <title> [--repo <repo>]`, `sindri item close <itemId> [--version <n>]`,
  `sindri item close <itemId> --cancelled [--version <n>]`. `CatalogRules.ShippedPhases += "P3"`.
- `ARCH-CQRS-002` unchanged (`IItemResolver` is a read port).

### 1.8 Testing support
`A.cs` builders from the generator; `PassThroughScrubber`; `InMemoryViewStore` gains `Range`; `ItemRowBuilder`;
`InMemoryItemResolver` is not needed (the real `ViewItemResolver` runs over `InMemoryViewStore`).

### 1.9 Commit sequence (contract unit)
`feat: ItemId, ItemAlias, ItemRef and RecordKey` · `feat: item states, groups, actions and decisions contracts` ·
`feat: item.created, item.state-changed and item.linked events; SND-ITEM-404/409` ·
`feat: ITextScrubber and SafeText contracts` · `feat: item views, IViewReader.Range and the item EmptyText` ·
`test: red CreateItem, CloseItem, GetQueue, ListItems, GetItem and GetItemTimeline slices` ·
`feat: CreateItem and CloseItem catalog rows are ready; P3 is shipped`.

---

## 2. Units after the contract (≈ half a day each; B and C can run as parallel worktrees off the contract commit)

### Unit A — text: scrubber adapter and `SafeText`
`Infrastructure/Text/RedactingScrubber.cs` over `Aw.Redaction.Redactor.Default` (registered in
`AddSindriInfrastructure`); `deps.yaml` Sindri → Redaction `planned` → `current`; `SafeText` real. Tests:
`Escape_turns_controls_into_visible_text` (Theory over ESC, BEL, NUL, C1 0x9B, DEL), `Escape_removes_an_osc_8_link_and_a_csi_colour`,
`Escape_marks_zero_width_and_bidi_characters`, `Line_folds_newlines_and_cuts_at_max`, `RedactingScrubberContractTests`.

### Unit B — views.db tables and `Range`
`Views/Migrations/0002_items.sql` (`view_items`, `view_item_aliases`, `view_item_timeline`, same shape as P2's;
G2), `ViewSql` map entries, `ViewTable.Range`, `SqliteViewReader.Range`, `InMemoryViewStore.Range`. Tests:
`SqliteViewStoreContractTests` / `InMemoryViewStoreContractTests` gain Range; `A_views_db_at_version_1_migrates_and_rebuilds`
(schema version 1 → 2 triggers P2's rebuild).

### Unit C — projections (pure folds, Core)
`Core/Items/ItemsProjection.cs` (`item.created` → row v1 `new`; `item.state-changed` → state, version, since;
`item.linked` → link, alias; text through `SafeText.Clean(scrubber, …)`: title 200, body 4,000),
`ItemAliasesProjection.cs` (`item.linked` with an alias → `AliasRow`; a re-link moves the alias),
`ItemTimelineProjection.cs` (every event whose ledger `Item` is set → `TimelineRow` keyed `<item>/<seq:D19>`, line via
`TimelineLine.For`, cleaned). Registered as `IProjection` singletons (`AddSindriCore`). Each has a static `Fold`.
Tests: `Created_item_is_new_at_version_1`, `State_change_moves_state_version_and_since`, `Link_sets_link_and_alias`,
`Title_is_scrubbed_and_escaped`, `Alias_moves_on_relink`, `Timeline_has_one_line_per_item_event`,
`An_event_without_an_item_is_ignored`, `Replaying_the_same_events_gives_the_same_rows`.

### Unit D — the decisions and the resolver
`ItemStates`, `ItemActions`, `ItemDecisions.Close`, `ItemScope.Visible`, `TimelineLine.For`, and
`Features/Items/ViewItemResolver.cs : IItemResolver` (alias → `item-aliases` → `items`; scope check). Tests:
`Groups_follow_the_spec_table` (Theory over every state), `Actions_by_state`, `Close_at_the_seen_version_changes_state`,
`Close_with_a_stale_version_is_SND_ITEM_409`, `Closing_a_closed_item_is_SND_ITEM_409`, `Session_sees_only_its_repo`,
`Session_without_repo_sees_only_repo_less_items`, `Alias_resolves_to_its_item`, `Unknown_alias_is_SND_ITEM_404`,
`Item_in_another_repo_is_SND_ITEM_404_for_a_session`.

### Unit E — `CreateItem`
`CreateItemService(ILedgerStore, HeldLease, EventFactory, IIdGenerator, ICallerContextAccessor)`: repo = body repo,
else the caller's `X-Sindri-Repo` (§3.1); appends `item.created` keyed `item.created:<itemId>` with `item` set on the
ledger row; returns `ItemCreatedResponse`. `CreateItemValidator(IApprovalReader)`: title 1–200 characters after trim, no
control characters; body ≤ 4,000; a repo must name an approved profile repo (G9) → `SND-CLI-003`. Tests: the red slice
test green, `Repo_defaults_to_the_session_header`, `Unknown_repo_is_invalid`, `Empty_title_is_invalid`,
`Create_is_desk_and_refused_for_a_session` (403, only `command.handled`), `Origin_is_refused`, `Link_is_recorded`.

### Unit F — `CloseItem` (first real `ReadsViews` command)
`CloseItemService(IItemResolver, ILedgerStore, HeldLease, EventFactory, ICallerContextAccessor)`: resolve (human, so no
scope), `ItemDecisions.Close`, append `item.state-changed` keyed `item.state-changed:<itemId>:v<version+1>`; on
`AlreadyRecorded`, read the event at that sequence (`ReadAfter(seq − 1, 1)`) and, when its `commandId` isn't ours,
`SND-ITEM-409` "changed since you looked" (§2.1). `CloseItemRequest` is `IClassifiedCommand` (`done`/`cancelled`). The
`AwaitingViews` decorator (P2) runs it after the head. Tests: the red slice test green, `Stale_version_is_SND_ITEM_409`,
`Losing_the_fact_key_race_is_SND_ITEM_409` (pre-append the same key with another commandId),
`Cancelled_is_its_own_variant_and_destructive_in_the_catalog`, `Close_waits_for_the_head`,
`Close_while_stalled_is_SND_VIEW_001`, `Close_while_rebuilding_is_SND_API_010`, `Close_is_refused_for_a_session`,
`Close_by_alias`.

### Unit G — the four queries
`GetQueueService(IViewReader, IProjectorStatus, ICallerContextAccessor)`: items view → visible → open → grouped in
`QueueGroup` order (every group present, E-18), oldest `since` first within a group; per-group `empty`; top-level
`empty` = `EmptyText.For(asOf, allEmpty, Queue)`. `ListItemsService`: filters `state`/`repo` (closed items included
only when `state` asks for them; G11), sorted by `since`. `GetItemService(IItemResolver, IProjectorStatus)`.
`GetItemTimelineService(IItemResolver, IViewReader, IProjectorStatus)`: `Range` after `<item>/<after:D19>`, reading
101 rows for a page of 100; `Next` = the page's last sequence only when the 101st row exists, else null (so the last page
always says null, U2-API-4). `GetQueueService` emits groups in `QueueGroups.Order` and nothing else hard-codes the set. Tests: the four red slice tests green, `Queue_returns_every_group_even_empty`,
`Within_a_group_oldest_first`, `Closed_items_leave_the_queue`, `Session_queue_is_scoped`, `Items_state_filter`,
`Items_repo_filter`, `Unknown_state_is_400`, `Item_actions_follow_its_state`, `Timeline_pages_with_next`, `Timeline_last_page_has_null_next`, `Every_group_has_a_sentence`,
`Body_and_link_only_in_GetItem`, `Session_outside_the_repo_never_sees_the_body` (404),
`Stalled_views_carry_the_stalled_sentence`, `After_a_create_a_query_with_its_sequence_sees_it` (`X-Sindri-After`).

### Unit H — CLI verbs
New `Host/Commands/ItemVerbs.cs` (G17; not in `Verbs.cs`): `queue [--json] [--no-start]`, `items [--state S]
[--repo R] [--json] [--no-start]`, `item add <title> [--repo R] [--no-start]` (prints `ITM-… created (sequence N)`),
`item close <item> [--cancelled] [--version N] [--no-start]` (without `--version` it reads `GET /items/{item}` and sends
that version, G12; prints `ITM-… closed: done (sequence N)`). Text: the queue's groups with one line per item
(`ITM-…  <title>  [<repo>]  <state>  since <time>`), each empty group's sentence; exit 1 when `asOf` is rebuilding or
stalled (§3.6), else 0. Human verbs start the process unless `--no-start`; inside an agent session they never start it
(§7). CommandTable: four arms; Usage: four lines. Tests: `Cli_queue_prints_each_group_and_its_empty_text`,
`Cli_queue_exits_1_while_stalled`, `Cli_queue_json_is_the_raw_body`, `Cli_item_add_posts_as_human`,
`Cli_item_close_reads_the_version_when_none_given`, `Cli_item_close_cancelled_posts_the_cancelled_variant`,
`Cli_items_filters`, `Cli_in_an_agent_session_never_starts_the_process`.

### Unit I — goldens, docs, smoke, gate
Goldens (§6), `docs/csharp/sindri.md` (routes, verbs, events, codes, `views.db` tables, the repo scope, `SafeText`),
`AGENTS.md` (`queue`, `items`, `item add`, `item close` in the C# sindri line), `deps.yaml` (Redaction edge current;
Sindri's note "API P3: items, queue"), `.env.example` unchanged. AOT smoke: `item add`, `queue`, `items --json`,
`item close` (human), `item add` refused as a session; re-record `expected.txt` from a real run. Full merge gate.

---

## 3. Gaps between spec and code (G-numbers) and decisions

| # | Gap | Decision (default unless listed under Questions) |
|---|---|---|
| G1 | §3.4 makes `CreateItem` with `link` write `item.linked`, but §2.1 says one domain event per command | **Q1.** Default: `item.created` carries `link?`; the items projection treats it as linked. `item.linked` is registered, public and folded in P3, written later by context-sync (context Phase 6) |
| G2 | "`views.db` migrations reserved per phase: P3 `V1xx`" vs `MigrationChecks.CheckSequence` (versions must run 1, 2, 3 with no gaps) | P3 ships `0002_items.sql`; the per-phase range can't hold (Q6, a spec text fix). The lane that merges second renumbers (P4/P6 later). P5 adds no `views.db` migration |
| G3 | §3.6 names an `item_view` and a `queue_view` projection | One `items` view; the queue is a pure grouping of item rows at query time (`ItemStates.GroupOf`), so there is one source of truth (Q7) |
| G4 | The timeline has `asOf` (a projection) but §3.1 names no view; P2's `IViewReader` lists a whole view only | `item-timeline` view keyed `<itemId>/<seq:D19>`; `IViewReader.Range` (additive, in the contract unit) |
| G5 | Routes accept an `ItemId` *or* a linked key, but a typed id holds one format | `ItemRef` (id or alias) binds in the endpoint; `IItemResolver` resolves through the `item-aliases` view; responses carry the `ITM-…` id |
| G6 | "Compact GUIDv7" has no format | `ITM-` + 32 lowercase hex (`Guid.ToString("N")` of `IIdGenerator.NewId()`); the alias pattern is Linear's `TEAM-123` |
| G7 | `CreateItem` takes `body?` but `item.created`'s payload lists only item, title, repo | Lead answer 2 (was Q2): `body` is in the full payload, left out of the public payload (§5.3), shown scrubbed in `ItemView.body` |
| G8 | `ItemSummary`'s "its next action" (API §3.1) has no name | `primaryAction: ActionId?` (UI §4.5 "the row's primary action from `actions`"; the same name as `NeedsYouRow.primaryAction`, API §3.6); lead answer 3 sets its semantics; `ItemActions.Primary` |
| G9 | A `repo` that isn't in the approved profile | **Q4.** Default: `SND-CLI-003` "repo X isn't in the approved profile" (validator reads `IApprovalReader`) |
| G10 | Who may close an item that's already closed, or with no `itemVersion` | Closed → `SND-ITEM-409` "already done/cancelled"; `itemVersion` required on the API (400 when missing) |
| G11 | `GET /items` without `state`: closed items included? | Open items only by default; `state=done` or `state=cancelled` lists closed ones. Queue never shows them (§3.6) |
| G12 | `CloseItem` "sends the version the human saw", but the CLI user may not have looked | **Q5.** Default: `--version N` optional; without it the CLI reads the item first and sends its current version |
| G13 | Agent text "scrubbed, then escaped with the shared sanitizer" — none exists; Core/Features may use only Aw.Kernel (ARCH-DIR-005) | `ITextScrubber` port in Core, adapter over Aw.Redaction in Infrastructure; `SafeText` (pure) in Core; projections take the scrubber in their constructor. P5 and P4 reuse both |
| G14 | "Escaped" has no form | Controls become visible ASCII (`\x1b`), invisible/bidi marks `\u{200B}`, OSC/CSI sequences escaped whole; never stripped silently, so a reader can see something was there |
| G15 | Spec response names `ItemCreated(accepted, ItemId)`, `QueueView`, `ItemList`, `Timeline` | Generator type names win (P1 G20): `ItemCreatedResponse`, `QueueResponse`, `ListItemsResponse`, `TimelineResponse`; the **JSON shape follows the spec**: `ItemCreatedResponse(CommandAccepted Accepted, ItemId ItemId)` = `{accepted:{commandId, sequence}, itemId}` (U2-API-5) |
| G16 | P2 put shipped rows in `ShellRows.cs`; P3 and P4 adding there collide | P3's rows go to `Catalog/ItemRows.cs`; each later phase adds its own rows file and one line in `CommandCatalog.Rows` |
| G17 | `Verbs.cs` is one class that P2 (views rebuild, status health) and P5 (status `--follow`, ledger) also edit | P3's verbs live in `Host/Commands/ItemVerbs.cs`; P3 touches `CommandTable` (four arms) and `Usage` (four lines) only |
| G18 | Golden slug for `POST /items` collides with `GET /items` | Both live in `items/` (the UI's `items/created.json`, #197): `POST /items` scenarios start `created`/`refused-`/`invalid-`, `GET /items` scenarios start `list-`, so a file name still names one route and scenario (E-17) |
| G19 | `CloseItem` voids pending decisions (§3.2) and is refused while an instance is active (S2) | No decisions or instances in P3: P4 folds the voiding, P11 adds the refusal. Recorded under "Not in this PR" |
| G20 | `ItemView.instance` and `waitingOn` exist before their sources | Present and null (stable shape for the UI's goldens) |

## 4. Questions for the lead
- **Q1 (G1).** Does `CreateItem` with `link` append one event (`item.created` with `link`, default) or two
  (`item.created` + `item.linked`, against §2.1's one-event rule)?
- ~~Q2~~ answered (lead answer 2; U2-API-7 below).
- ~~Q3~~ answered (lead answer 3; named `primaryAction` by UI §4.5, U2-API-1).
- **Q8 (U2-API-8).** Running rows' agent, place and budget: add `ItemSummary.run?: {agent, where, budget}` in P11 (proposal below), or have the app read `GET /workflow/instances/{address}` per running row?
- **Q9 (U2-API Q1, PENDING with the owner).** Four queue groups (API §3.6) or five with `parked` (UI §4.5)? The plan builds four and keeps the set in `QueueGroups` so five is a one-line change plus re-recorded goldens.
- **Q4 (G9).** Refuse a `repo` that isn't in the approved profile (default), or allow any valid `RepoName`?
- **Q5 (G12).** `item close` without `--version`: read the current version (default), or make `--version` required so
  every CLI close is a "since you looked" check?
- **Q6 (G2).** The per-phase `views.db` migration ranges contradict the Kernel runner; may the spec's Shared files
  section say "next free number; the second lane to merge renumbers"?
- **Q7 (G3).** The queue as a grouping of the `items` view (default), or its own table?

## 5. Not in this PR
- `CloseItem` voiding decisions and `needs-decision` (P4); `Decide`'s `next`; engine item states and the active-instance
  refusal (P11); `ItemView.launch` (E-2, P10a); `item.linked` writes from context-sync (context Phase 6).
- CLI verbs for item detail and the timeline (no spec row; the app and `curl` read the routes).
- `invalidates` on the item catalog rows and the stream's item filter (P5, which stacks on this contract unit).

## 6. Goldens P3 publishes (`tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json`)
| Route | Scenarios |
|---|---|
| `GET /queue` (`queue`) | **#197:** `fixture` (4 queued `new`; every other group empty with its sentence; #197's extra `needs-decision` row is added when P4 re-records it), `empty` (every group empty + `EmptyText.Queue`), `stalled` (stalled `asOf` + sentence, rows kept), `large` (30 queued). P3's own: `session-scoped`, `rebuilding`. `running` (1 with you, 6 running, 4 queued) needs engine states: **P11** publishes it |
| `GET /items` (`items`, `list-` prefix) | `list-empty`, `list-open`, `list-state-done`, `list-repo`, `list-session-other-repo` (empty), `list-bad-state` (400) |
| `POST /items` (`items`) | **#197:** `created`. P3's own: `created-repo`, `created-link`, `invalid-title`, `unknown-repo`, `refused-session`, `refused-origin` |
| `GET /items/{itemId}` (`items-id`) | **#197:** `new` (version 1, `CloseItem:done`/`:cancelled`), `after-close` (`done`, version 2, `actions: []`), `not-found` (404 `SND-ITEM-404`). P3's own: `cancelled`, `alias`, `with-body` (`body` and `link` shown to a human), `session-other-repo` (404), `bad-id` (400). `needs-decision` (waiting on a decision; `Decide` + Close) needs P4's decisions: **P4** publishes it |
| `GET /items/{itemId}/timeline` (`items-id-timeline`) | **#197:** `fixture` (`after=0`, a full page and a non-null `next`), `page-2` (the rest, `next: null`). P3's own: `closed`, `not-found`. The page holds 100, so `fixture` has 100 entries (101 events appended directly, all with the item set) and `page-2` one |
| `POST /items/{itemId}/close` (`items-id-close`) | **#197:** `accepted`, `stale-version` (409 `SND-ITEM-409`). P3's own: `accepted-cancelled`, `already-closed`, `not-found`, `refused-session`, `while-stalled` (503 `SND-VIEW-001`), `while-rebuilding` (503 `SND-API-010`) |
| `GET /commands` (`commands`) | re-recorded `catalog` (the three rows `ready`, new CLI forms); it replaces #197's stand-in `catalog-p3.json` |
Every scenario runs on `FakeTimeProvider(KnownInstants.Epoch)`, `SequentialIdGenerator` (so `ITM-…` ids are stable),
the golden pid, `Projector.CatchUp` before the request; the alias scenario appends an `item.linked` event directly; the
stall appends an `item.created` with an unparseable payload (UI §18.2).

## 7. Task list (red first; each behaviour = red commit, then fix; ≈ half a day per line)
1. Contract unit, part 1: ids, items, events, catalog rows, errors, text contracts (§1.1–§1.4).
2. Contract unit, part 2: views (§1.5), the six slice stubs, Host lines, JSON context, catalog flip, builders (§1.6–§1.9). Gate: build 0 warnings; unit lane shows only the six slice reds + the listed contract-binding reds.
3. A: red `SafeText` theories + `RedactingScrubberContractTests` → `SafeText`, adapter, deps edge.
4. B: red `Range` contract cases + migration test → `0002_items.sql`, `Range` in both stores.
5. C: red folds → three projections.
6. D: red decisions, scope and resolver tests → `ItemStates`, `ItemActions`, `ItemDecisions`, `ItemScope`, `TimelineLine`, `ViewItemResolver`.
7. E: `CreateItem` green + validator tests.
8. F: `CloseItem` green + race, variant, stall and rebuild tests.
9. G1: `GetQueue` and `ListItems` green.
10. G2: `GetItem` and `GetItemTimeline` green; alias route binding test.
11. H: CLI verbs red → `ItemVerbs`, arms, Usage.
12. I1: goldens (all scenarios of §6) and the catalog golden.
13. I2: docs, AGENTS.md, deps.yaml, AOT smoke re-record, full merge gate, prover run.

## 8. File overlap with P5 (run in parallel) and how to merge cleanly

**Sequencing.** P3's contract unit lands **first**, alone, on P2's green tip. P5 branches from that commit (its
filters need `ItemId`, `ItemRef`, `IItemResolver`, `EventTypes.ItemCreated`, `ITextScrubber`/`SafeText` and the item
catalog rows). After that both lanes fan out in parallel. Merge order is the README's lane order: **P3 then P5**; P5
rebases onto `main` after P3 merges (`git rebase --onto main <P3 contract tip>` if P3 was squash-merged).

| File | P3 | P5 | How it merges |
|---|---|---|---|
| `Core/Events/EventCatalog.cs` | three item rows (contract unit) | `with { Invalidates = […] }` on every row, including P3's | P5's edit is on top of P3's contract commit, so it never conflicts; P3 adds no later rows |
| `Core/Events/EventCatalogEntry.cs` | — | `Invalidates` init property | P5 only |
| `Core/Events/EventTypes.cs` | three constants | — | P3 only |
| `Core/SindriErrors.cs` + `All` | `SND-ITEM-404/409` (contract unit) | `SND-API-012` | adjacent lines at the end; P5 rebases |
| `Core/Registration/SindriCoreServiceCollectionExtensions.cs` | three `IProjection` lines (unit C) | `ILedgerSignal` line | different lines; trivial rebase |
| `Core/Ledger/ILedgerReader.cs`, `LedgerReaderContract`, `SqliteLedgerReader`, `LedgerSql`, `SqliteLedger`, `InMemorySindriStore` | — (reads only through `ILedgerStore.ReadAfter`) | filtered read, signal pulse | P5 only |
| `Core/Views/*`, `Infrastructure/Views/*`, `0002_items.sql`, `EmptyText` | items views, `Range` | — | P3 only |
| `Core/Text/*`, `Infrastructure/Text/*` | owner | consumer | P3's contract unit; P5 never edits them |
| `Host/Server/ProjectorHost.cs` / `Projector.cs` | — (projections arrive by DI) | `ProjectorHost` waits on the signal | P5 only |
| `Features/SindriFeaturesServiceCollectionExtensions.cs` | 2 command + 4 query lines | 2 query lines (`ReadLedger`, `GetEventCatalog`); `StreamEvents` line unchanged | sorted anchors; adjacent-line conflict resolved by keeping both |
| `Host/Endpoints/EndpointTable.cs` | 4 query + 2 command lines | 2 query lines | as above |
| `Host/SindriJsonContext.cs` | item types | `PublicEvent`, `LedgerPage`, catalog view; drops `EventView` | each lane its own attribute lines; keep both |
| `Host/Catalog/*`, `tests/…/Catalog/CatalogRules.cs` `ShippedPhases` | `ItemRows.cs`, `CommandCatalog.Rows` line, `"P3"` | `"P5"` (only at its rebase) | P5 appends `"P5"` after P3's `"P3"` when it rebases |
| `Host/Commands/CommandTable.cs` | 4 arms (`queue`, `items`, `item add`, `item close`) | the `status` arm (`--follow`), 1 arm (`ledger`) | different arms; P3 never edits the `status` arm |
| `Host/Commands/Verbs.cs` | — (`ItemVerbs.cs`) | `Status` gains follow (or `FollowVerbs.cs`) | P5 only |
| `Host/Commands/Usage.cs` | 4 lines | 2 lines (`ledger`, `status --follow`) | adjacent lines, keep both |
| `PublicAPI.Unshipped.txt` (Core, Features) | its lines via `--slice-only` | its lines | sorted file: on conflict, the integrator re-derives from `RS0016` |
| `tests/Aw.Sindri.Testing/A.cs`, `tests/…/Ids/IdsJsonContext.cs` | generator lines | generator lines | adjacent lines, keep both |
| `docs/csharp/sindri.md` | routes rows, Files (`views.db` tables), errors table rows, items section | `/events` row rewritten, `/ledger` and `/events/catalog` rows, errors row, stream section | P3 adds a new `## Items` section and its table rows; P5 owns the `/events` row and adds `## Event stream`; error rows adjacent |
| `AGENTS.md` (C# sindri command line) | `queue`, `items`, `item add/close` | `ledger`, `status --follow` | same line: P5 rebases and merges the verb lists |
| `src/Sindri/aot-smoke/run.sh`, `expected.txt` | item steps | ledger steps | P5 re-records `expected.txt` after rebasing (one real run), never hand-merges it |
| `deps.yaml` | Redaction edge `current`, note | note | P5 rebases the note line |
| Goldens | new folders | `events/` re-recorded, `ledger/`, `events-catalog/` | disjoint folders |

## 9. Risks
- **List size:** `ListItems`/`GetQueue` read the whole `items` view and filter in memory; fine at desk scale
  (hundreds). A `state` column index is a later migration if it ever matters.
- **Rebuild on upgrade:** `ViewSchema` 1 → 2 replays the ledger on first start; with the P2 200k-noise test this is
  seconds. The upgrade test covers it.
- **AOT:** enums kebab through the source-gen context; `ItemRef` binding is a manual parse (no reflection binder).
- **Analyzers:** `CloseItemService` has 5 deps; `GetQueueService` grouping in a Core static to stay under CA1502.
- **Lane budgets:** projection folds are unit; one temp home per integration class.
- **Hook contract: unchanged.** No hook reads items, the queue or `views.db`.

## UI U2-API-1..10 (UI PR #197) folded in

UI spec wins where it differs (README X52). The full answers, with citations, are in the lead's scratchpad
`u2-api-answers.md`; this is what changes in the plan.

| # | Request | Plan change |
|---|---|---|
| 1 | `ItemSummary` names | `{id, title, repo, state, step, since, primaryAction, actions}`; `next` renamed `primaryAction` (UI §4.5 "the row's primary action from `actions`"; API §3.6 `NeedsYouRow.primaryAction`); `ItemActions.Primary(state)` ⊆ `For(state)`, null for every P3 state (§1.2, §1.6, G8) |
| 2 | Queue group shape | `QueueGroupView` JSON `{group, items, empty}` (§1.6) |
| 3 | `ItemView` names | Flattened summary + `version, createdBy, waitingOn, instance, createdAt, updatedAt, asOf` (§1.6; API §3.1 `ItemView` "the summary, plus …") |
| 4 | Timeline names | Entries `{sequence, time, type, actor, line}`; `next` null on the last page (read 101 for a page of 100; unit G) |
| 5 | `ItemCreated` shape | `{accepted:{commandId, sequence}, itemId}` (API §3.2 `ItemCreated(accepted, ItemId)`; G15 changed: P3 no longer flattens it) |
| 6 | No `version` on rows | Confirmed: item commands are sheet-only (UI §4.5 "**Close item** from the detail … with the version the human saw"); `ItemSummary` stays without `version`. Row actions that do exist later (Take over, Hand back) are engine commands with a nonce (UI §9.1), not an item version |
| 7 | `body`, `link` in `ItemView` | `ItemView` gains `body` and `link`; never on `ItemSummary`, the queue, `GET /items`, timeline lines or a public payload (lead answer 2). "A caller who may see the repo" is `GetItem`'s existing scope: `IItemResolver` answers 404 for a session outside the repo (API §3.1), so no extra check. Test `Body_and_link_only_in_GetItem` |
| 8 | Running rows' agent, place, budget | Not P3. Owner: the engine's instance data (WF §6.5 `run.runtime`, `run.launch`, the profile's `workflow.agent.target`; budget WF §3.7 and the §6.4 `budget` line on `GET /workflow/instances/{address}`), surfaced on items by **API P11** (the item projection folding `workflow.*`, API Phases P11; UI §10.1 "Item state from the engine — first API phase after WF-P4"); the agent of a human launch comes from `launch.started.target` (UI E-1, P10a). Neither spec names an `ItemSummary` field: **Q8** |
| 9 | `EmptyText` for every group | A sentence for every `QueueGroup` member (incl. `Parked`) and the queue's own, the UI fixtures' wording (§1.5); test `Every_group_has_a_sentence` |
| 10 | Goldens for #197's scenarios | Added to §6 under #197's paths and names; `needs-decision` scenarios move to P4 and `queue/running` to P11, because P3 can't produce those states from real events (API §3.6 "Before S2") |

**Queue groups (PENDING, owner).** UI §4.5 lists five groups (`waitingOnYou`, `withYou`, `parked`, `running`,
`queued`); API §3.6 has four and puts `parked` in `waitingOnYou` (also API §3.6's engine map: "`parked` therefore sits in
`waitingOnYou`; there is no separate `parked` group"). P3 builds the API's four. The set lives only in
`QueueGroups.Order` and `QueueGroups.GroupOf`; the enum and `EmptyText` already carry `Parked`. If the owner picks five:
add `Parked` to `Order`, map `ItemState.Parked → Parked`, re-record the queue goldens. Nothing else changes.

**Spec edits in the P3 PR.**
- API §3.1, `GET /items` row: "its next action" → "`primaryAction` (an action id from `actions`, or null)". Brings the
  API spec in line with UI §4.5 — no owner approval needed.
- API §3.1, `GET /items/{itemId}` row: add `body` and `link`. Not in UI §4.6, so it **needs owner approval** (the lead's
  answer 2 covers the plan, not the spec text).
- API §3.6 `EmptyText`: list the five group sentences and the queue's sentence. Fills a gap neither spec decides →
  **needs owner approval** (or leave the text to `docs/csharp/sindri.md` and the goldens: the default).
- Field names for items 2–4 are not written into the spec: the goldens are the wire contract (E-17) and
  `docs/csharp/sindri.md` records them. No spec edit.
- Already owner-approved: the migration-number text (lead answers, last line).

## Lead answers (2026-10-12)
1. `CreateItem` with a `link` appends one event: `item.created` carries the link. There is no separate `item.linked` on create, so the one-event rule holds. `item.linked` is only for linking an existing item.
2. Keep `body`: stored, scrubbed through the text port, never on a public event or a list row. It appears only in `GetItem` for a caller who may see the item's repo.
3. `ItemSummary.next` is an action id from `ItemActions.For`, or null.
4. Refuse a repo that isn't in the approved profile: 400 `SND-ITEM-…` (or the existing invalid-input code if the spec names none).
5. `item close` without `--version` reads the current version first. The API itself always requires `version`, which is where the 409 comes from.
6. Migrations take the next free number (`0002_items.sql`). The spec's reserved per-phase ranges clash with the Kernel runner's no-gap rule, so the lead has reported that spec text to the orchestrator. Don't edit the spec.
7. The queue is grouped from the items view at query time. No table of its own.
- Owner-approved (2026-10-12): the P3 PR also edits the API spec, replacing the per-phase V-ranges with "the next free migration number".

## Owner decisions (2026-10-12, relayed by the orchestrator)
- **Queue groups: four**, per API §3.6. Q9 is closed. `QueueGroups.Order` has no `parked` entry, and parked items go in `waitingOnYou`. The UI's badge reads the row's `state: "parked"`, so no extra field is needed. If a state is ever not enough, add `parked: true` to ItemSummary in the contract unit. `EmptyText`'s "Nothing parked." stays unused until then.
- **U2-API-7 approved:** in the P3 PR, edit the API §3.1 `GET /items/{itemId}` row to give ItemView, and only ItemView, `body` (scrubbed) and `link`.
- Spec edits in the P3 PR:
  - §3.1 `primaryAction`, which aligns with UI §4.5;
  - §3.1 ItemView `body` and `link`;
  - "the next free migration number" in place of the per-phase V-ranges.

## README X61 (owner direction, main 903fa14a): multiplayer later, not foreclosed
- Reserve an optional `owner` on the item record: `owner: string?`, always null in P3. It lives on ItemSummary, ItemView and the items view table (a nullable column in `0002_items.sql`), and is carried on `item.created` as null.
- The P3 PR adds a note to the API §3 item rows: "`owner` is reserved for multi-user (README X61); always null for now".
- `owner` and `actor` stay open strings. Never parse or switch on actor values. Item code reads `createdBy` from the envelope's actor verbatim.

## Contract-unit deviations (implementer, 2026-10-12)
- The ids have no RDG route-binding tests. `{itemId}` binds as a string and goes through `ItemRef.TryParse` in the endpoint (P1 rule), so each id gets JSON and parse tests instead. `ItemRef` is written as its text through `ItemRefJsonConverter` in Core.Ids.
- The catalog flip (§1.9, last commit) goes in the same commit as the red slices. Otherwise ARCH-CQRS-003 and `Every_ready_row_route_is_mapped` would fail in between.
- X61 `owner` (`ItemCreated`, `ItemCreatedPublic`, `ItemRow`, `ItemSummary`, `ItemView`) is folded into the contract records. Its SQL column goes in unit B's `0002_items.sql`.
