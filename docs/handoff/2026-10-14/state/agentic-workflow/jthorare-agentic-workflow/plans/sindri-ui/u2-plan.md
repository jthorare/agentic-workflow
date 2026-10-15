# U2 plan — queue, item detail, timeline, Create and Close item (UI spec §18 U2)

Branch `feat/sindri-ui-u2`, worktree `.claude/worktrees/sindri-ui-u2`, from `origin/feat/sindri-ui-u1` @ a5321188
with P2 `c1a95108` merged (b904d7c6, merge not rebase). Stacks on #193 (U1) → #184 (U0). Draft PR base
`feat/sindri-ui-u1`. **API P3 isn't built**: every P3 route is a **spec-shaped fixture** (listed below), to be
replaced by P3's goldens (E-17) when P3 lands. The `CreateItem` and `CloseItem:*` catalog rows stay `pending:P3` in
the real catalog; tests use a spec-shaped `commands/catalog-p3.json` (P1's catalog with those three rows `ready`).

Read first: UI spec rev 7 §2 rules, §3.2 (Home's panels, caps, sheet), §3.3 (pages, `G Q/R/W`, deep link
`item/<ItemId>`, stable selection), §4.1 (states, command lifecycle), §4.2, §4.5, §4.6, §4.9 (Queue, Item detail
rows), §7 (CreateItem, CloseItem:done/cancelled, E-8), §8 (`C`, `J/K`, `Enter`, `Esc`), §9.1 (SND-ITEM-409 refresh +
detail), §9.2, §12.4, §14, §18 U2, §18.1 (E-17, E-18), §18.2; API spec §3.1 (`/queue`, `/items/{id}`,
`/items/{id}/timeline`), §3.2 (`CreateItem`, `CloseItem`, notes on versions), §3.6 (groups, EmptyText), §9
(SND-ITEM-404/409, ProblemDetails). Mockups `queue.html`, `item-detail.html`, `home.html`.

Rules (as U0/U1): TDD — red view-model or client test first (NotSupportedException stubs where the analyzers need
them), then green, then the view, then a headless view test with key frames. View models hold no logic: API fields to
words and visibility; order, groups, actions, empty sentences all from the API. Compiled bindings, no hex outside
`Theme/`, every control named. Timing on the injected `TimeProvider`. All dotnet via
`AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy dotnet`.

## Wire shapes used (spec-shaped; field names the spec doesn't fix are U2-API edits)
- `GET /queue` → `{groups:[{group, items:[ItemSummary], empty}], empty, asOf}`; groups in API order, every group
  present (E-18). API §3.6 names four groups (`waitingOnYou`, `withYou`, `running`, `queued`).
- `ItemSummary` → `{id, title, repo, state, step, since, primaryAction, actions}` (API §3.1 ListItems list).
- `GET /items/{id}` → ItemSummary's fields + `{version, createdBy, waitingOn, instance, createdAt, updatedAt, asOf}`.
- `GET /items/{id}/timeline?after=` → `{entries:[{sequence, time, type, actor, line}], next, asOf}`.
- `POST /items` `{title, repo?, body?, link?}` → `ItemCreated {accepted:{commandId, sequence}, itemId}`.
- `POST /items/{id}/close` `{reason: done|cancelled, itemVersion}` → `CommandAccepted {commandId, sequence}`.
- Refusals: ProblemDetails `SND-ITEM-404` (404), `SND-ITEM-409` (409).

## Unit C — contract (first)
Records in `Api/` + `SindriUiJsonContext`; `ISindriApi.Queue`, `Item`, `Timeline`, `CreateItem`, `CloseItem`;
client POSTs a JSON body and takes `X-Sindri-After` from `ItemCreated.accepted.sequence` too; `FakeSindriApi`
routes (per-item answers) and `FakeSindriHandler` routes; spec-shaped goldens; `GoldenContractTests` places the new
slugs; `SindriApiContract` facts for each route, the 404 and the 409.

## Unit A — view models
- `ViewModels/Queue/QueueViewModel`: groups (label, count, rows, the group's `empty`, hidden when empty), the view's
  `empty` when every group is, Home's panels (Running and With you: With you first then running, cap 5, "Show all N
  in Queue"; Queued: cap 5), header counts, the Queue nav count, a filter (All / Running / With you for `G R`/`G W`),
  selection kept by `ItemId` across refreshes (next row in the group when it clears, announced "<id> cleared").
- `ItemRowViewModel`: id, title, repo, state word, step, since ("12m"), the primary action through the catalog's
  control (pending rows draw disabled with their phase).
- `ViewModels/Item/ItemSheetViewModel`: loads `GET /items/{id}` + the first timeline page; header (id, title, repo,
  state word, version, since), created by, waiting on, instance; the item's `actions` ∩ catalog as controls;
  `CloseItem:done` / `CloseItem:cancelled` send the version shown; `SND-ITEM-409` re-reads the item and keeps the
  inline `detail`; `SND-ITEM-404` → the not-found state (detail + Back); timeline entries and **Load more** (`next`).
- `ViewModels/Item/CreateItemViewModel`: form (title, repo, body), sends `CreateItem` through the catalog control;
  accepted closes the form ("Create item: done", the new id). The row appears from the stream's re-query.
- `ControlViewModel` gains the destructive confirm (catalog `destructive`): a first press asks, Confirm sends,
  Keep/Esc cancels. Reused by U4's Stop.
- Refresh: the follower re-reads the queue and the open item (and its loaded timeline) on connect and on every
  event (U1's 250 ms debounce). No payload folding.

## Unit B — views
`Views/Queue/QueuePage.axaml` (groups, collapsible, counts, rows, empty), Home's Running/With you and Queued panels
(replace "not served yet"), `Views/Item/ItemSheet.axaml` (in the shell's sheet when the subject is an item),
`Views/Item/CreateItemDialog.axaml`, the confirm in `CommandControl`. Keys: `C` (create), `Enter` (open the
selected row's sheet), `J`/`K` (rows), `G Q/R/W/H/D/S/.` chord (1 s, "G …"). Key frames (integration lane):
u2-queue-populated, u2-queue-empty, u2-queue-loading, u2-home-panels, u2-item-sheet, u2-item-timeline,
u2-item-409, u2-item-404, u2-create, u2-close-confirm.

## Unit D — fixture and proof
`scripts/sindri-ui-fixture.sh items [--large]` (`human item add` ×4, `--large` 30) and `close <id> [--stale]`;
its bash test. Real-app proof waits on P3 (verification plan).

## Spec-shaped fixtures (replace with P3 goldens)
| path (tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/…) | route | scenario |
|---|---|---|
| `queue/fixture.json` | GET /queue | 4 queued (`new`) + 1 `needs-decision`, other groups empty with their sentences |
| `queue/empty.json` | GET /queue | every group empty; the view's empty sentence |
| `queue/stalled.json` | GET /queue | stalled asOf + stalled sentence, rows kept |
| `queue/large.json` | GET /queue | 30 queued |
| `items-id/new.json` | GET /items/{id} | a `new` item, version 1, actions CloseItem:done/cancelled |
| `items-id/after-close.json` | GET /items/{id} | the same item `done`, version 2, no actions |
| `items-id/not-found.json` | GET /items/{id} | 404 SND-ITEM-404 |
| `items-id-timeline/fixture.json` | GET /items/{id}/timeline | 2 entries, `next` set |
| `items-id-timeline/page-2.json` | GET /items/{id}/timeline?after=N | 1 entry, `next` null |
| `items/created.json` | POST /items | ItemCreated |
| `items-id-close/accepted.json` | POST /items/{id}/close | CommandAccepted |
| `items-id-close/stale-version.json` | POST /items/{id}/close | 409 SND-ITEM-409 |
| `commands/catalog-p3.json` | GET /commands | P1's catalog with CreateItem, CloseItem:done/cancelled `ready` |

## U2-API edits (for the owner) — see the digest for the live list.
