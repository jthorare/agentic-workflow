# API P4 → UI U3: answers to U3-API-1..8 (plan: plans/sindri-api-lane/p4-plan.md)

The goldens are the wire contract (E-17). Names below are the plan's; the goldens decide.

## Goldens (`tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json`)
- `needs-you/`: `empty`, `fixture` (decision + failure + profile, in order), `one-request`, `stalled`, `rebuilding`,
  `large` (12 decisions, 2 repos, + failure + profile), `session-repo`, `after-decide` (your extra, promoted).
- `decisions/` (GET): `pending`, `decided`, `empty`, `empty-decided`, `voided`, `by-item`, `stalled`, `session-other-repo`.
- `decisions-id/` (POST): `accepted`, `declined`, `decided-elsewhere` (409), `voided` (409 "the item was closed"),
  `not-found` (404 SND-DEC-404), `bad-choice` (400 SND-DEC-001), `bad-id`, `refused-session` (403 SND-AUTH-002), `while-stalled`.
- `requests/` (GET, `list-` prefix): `list-fixture`, `list-empty`, `list-state-accepted`, `list-by-item`, `list-session-other-repo`, `list-bad-state`.
- `requests/` (POST): `filed`, `filed-item`, `invalid`, `invalid-kind`, `too-long`, `too-many` (429 SND-REQ-002),
  `item-not-found`, `item-closed`, `refused-origin`.
- Re-recorded: `queue/fixture` (with the `needs-decision` row), `items-id/needs-decision`, `items-id/after-decide`, `commands/catalog`.
- Your `requests-post/*` become `requests/*` (no `-post` slug, see 5). `commands/extra-catalog-p4` is replaced by `commands/catalog`.

## NeedsYouRow
`{id, kind, label, subject:{kind, id}, item?, title, reason, repo?, urgency, since, recordedOnly, notice?, answerKind?, primaryAction, actions, cli}`
- `decision`: id `decision:DEC-…`, label `decision · re-scope|add task|note|hand back`, subject `{decision, DEC-…}`,
  `item` set when the request named one, `pulse`, primary `Decide`, actions `["Decide"]`, cli `sindri decide DEC-… <choice>`.
- `failure`: id `failure:service:<name>`, label `failure · <name>`, subject `{service, <name>}`, `colour`, primary
  `RunService`, actions `["RunService","PauseService"]` (both `pending:P6` in the catalog), cli `sindri services run <name>`.
- `profile`: id `profile:drift`, label `profile · drifted`, subject `{profile, <live hash>}` (pending owner; if declined,
  `subject` is null), `calm`, primary `ApproveProfile` (catalog `cli-only:typed-confirmation`), cli `sindri profile approve <hash>`.
- `actions` lists the ids that apply. Whether each one is ready, pending or cli-only comes from the catalog, the same rule as `ItemSummary`.
- `cli` is always a `CliForm` filled with typed ids (E-16), and `primaryAction` is never `copy:`.
- `answerKind` is null on every P4 row.
- Order: urgency (pulse, colour, calm), then `since`, then `id`.

## Decisions
`DecisionView{id, kind:"accept-request", class:"decision", label, item?, itemVersion, repo?, question,
options:[{id, label, rejectLike, cli}], recommended, state:pending|decided|voided, filedBy, filedAt, decidedBy?, decidedAt?,
choice?, note?, recordedOnly, notice?, actions, cli, cleared?}`
- Options: `accept` ("Accept", recommended) and `decline` ("Decline", `rejectLike: true`). Each option's `cli` is filled, e.g. `sindri decide DEC-… accept`.
- `actions` is `["Decide"]` while pending and `[]` after.
- `RequestView{id, kind, item?, repo?, text, actor, state:open|accepted|declined|voided, decisionId, filedAt}`.
- `RequestFiled{accepted:{commandId, sequence}, requestId, decisionId}`.

## Per-list `empty` (from `EmptyText`; never write your own)
- needs-you: "Nothing needs you. N running, M queued." (zeroes shown). Changed 2026-10-14 (U3'-API-11): "waiting on you" is retired (screens R8-13).
- decisions: "No decisions are waiting." (pending), "Nothing decided yet." (decided), "No decisions for ITM-…." (item).
- requests: "No requests filed.", "No open requests." (state filter), "No requests for ITM-…." (item).
- Queue groups keep P3's per-group sentences (E-18). An item with a pending decision is `needs-decision` and sits in `waitingOnYou`.
- `rebuilding` and `stalled` replace `empty` as in P3.

## Stream events → re-query
- `request.filed` and `decision.recorded`: needs-you, decisions, requests, queue, items, the item and its timeline.
- `item.state-changed` (a close voids that item's decisions): the same set.
- `service.ran`: needs-you (failure rows) and services.
- `profile.checked`: needs-you (profile and failure rows) and profile.
- Until P5's `invalidates` ships, keep re-reading every visible view on each event.
- After an accepted command, send `X-Sindri-After: <sequence>`. The read-your-writes timeout is **SND-API-011** (503), not SND-API-004.

## U3-API-1..8
1. Taken, plus `item?` and `notice?`. `label` is served for every kind. The `profile` subject kind needs the owner. (API §3.6, UI E-7, §4.3)
2. Taken: catalog ids only. Failure → `RunService` (pending:P6), profile → `ApproveProfile`, and `cli` is the one copy
   string. This is an API §3.6 alignment edit. (UI §4.3, §7)
3. Taken, with the field names above. API §3.1's decisions row gains "who filed it and when; when decided and the choice". (UI §4.3, §4.7)
4. Taken: `GET /requests?state=&item=`, and the CLI gets `--item`. API §3.1 alignment. (UI §4.6)
5. GET and POST share one slug, `requests`, and the GET scenarios get the `list-` prefix (P3's `items/list-*` rule). (UI §18.1 E-17)
6. `options[].rejectLike`, with `Decide`'s catalog `shortcut` left null. UI §8's "from the catalog's `shortcut`" needs a
   UI-spec wording fix, which you raise with the owner. The lead may still pick `Decide:accept`/`:decline` variants instead.
7. Served as `notice` ("recorded; acted on once the workflow engine runs") on the row and the decision. (API §3.4, UI §12.4)
8. `cleared` (the timeline line) on decided and voided `DecisionView`s, from `GET /decisions?pending=false`. P4 adds no
   `GET /decisions/{id}`; a new route would need the owner. (UI §4.1)
