# FakeLinear

A stateful fake of Linear's GraphQL API on loopback (spec `2026-11-04-sindri-linear-hub-design.md`, D15). Unlike
`FakeLinearServer` (which replays recordings and is read-only), a write here changes what the next read returns, and
switches make it misbehave. Context, Sindri and the UI tests can all start one. Everything in it is made up.

**Facts marked (Ph0 checks) are assumptions until the real probe (`scripts/linear-probe.sh`) runs against Linear.** When the
probe disagrees, change the one place named next to the fact.

```csharp
await using var linear = await FakeLinear.Start(seed => seed
    .Team("ENG")
    .Member("Ada Fixture")
    .Label("bug")
    .Issue("ENG", "Fix the login timeout", new FakeLinearIssueSeed { State = "In Progress", Assignee = "Ada Fixture" }));
// POST linear.Url with the header  Authorization: <linear.Token>  (FakeLinear.Start(seed, token, writeToken) accepts a second key; both act alike)
// ids for mutation inputs: linear.TeamId("ENG"), StateId("ENG", "Done"), MemberId, LabelId, CycleId, ProjectId
// read-only views: linear.Issues, linear.Comments, linear.Attachments, linear.RequestBodies (bodies only, never a header)
```

## What it serves

- Queries (answered from state): `viewer`, `issues` (filter, first, after, orderBy, includeArchived; newest first, archived
  issues hidden unless `includeArchived: true`), `comment(id:)` (by id; null-and-error "Entity not found" when there is none), `issue(id:)` (by id, identifier, or an identifier the issue had before a team
  move), `comments` (newest first; filter by `issue: { team: { key } }`), `teams` (each with `states`, `cycles`, `members` and `labels` connections, for the lookups step), `users`, `issueLabels` (each with its `team`, null for a workspace label), `workflowStates`, `cycles`, `projects`, empty `documents` and `projectUpdates`.
  A field the fake doesn't have is a 400 that names it.
- A comment's and an attachment's `issue` is the whole issue (without its own comments and attachments), so a mutation can ask for `comment { issue { ... } }` as the write client does.
- `IssueCreateInput` and `IssueUpdateInput` accept `parentId`, but the fake does not apply it: an issue's `parent` is always null.
- Mutations: `issueCreate`, `issueUpdate`, `commentCreate`, `commentUpdate`, `attachmentLinkURL`, `issueArchive`,
  `issueLabelCreate`. Each answers `{ success, lastSyncId, <entity> }`: `issue`, `comment`, `attachment`, `issueLabel`, and
  `entity` for `issueArchive`. Inputs may come through variables or inline.
- Introspection: `__type(name:)` for `IssueCreateInput`, `IssueUpdateInput`, `CommentCreateInput`, `CommentUpdateInput`,
  `IssueLabelCreateInput` (`inputFields { name type { name kind ofType { name kind } } }`), and
  `__type(name: "Query")` and `__type(name: "Mutation")` (`fields { name }`), and `__schema { queryType { fields { name } } mutationType { fields { name } } }`. The input types live in `LinearSchema.cs`;
  the same table refuses unknown input fields. (Ph0 checks: `id` exists on `IssueCreateInput` and `CommentCreateInput`.)
- Rate-limit headers on every answer: `x-ratelimit-requests-limit` (5000), `x-ratelimit-requests-remaining`,
  `x-ratelimit-requests-reset`, `x-complexity`. (Ph0 checks: names and numbers.)

## Repeated client id (Ph0 checks)

A client `id` on `issueCreate` or `commentCreate` that is already used is refused inside an HTTP 200 answer, the field's data
is `null`, and nothing is created. The shape lives in `LinearErrors.DuplicateId`, nowhere else:

```json
{ "data": { "issueCreate": null },
  "errors": [ { "message": "Argument Validation Error",
                "path": ["issueCreate"],
                "extensions": { "type": "invalid input", "code": "INVALID_INPUT", "userError": true,
                                "userPresentableMessage": "Issue id '<id>' is already used; a client id must be unique.",
                                "validationErrors": [ { "property": "id", "constraints": { "isUnique": "id must be unique: the Issue '<id>' already exists" } } ] } } ] }
```

## When updatedAt moves (Ph0 checks)

One table, `LinearUpdatedAt.cs`. A mutation that is `true` sets the updatedAt of the issue it creates, changes or hangs
something on to the fake's clock (one second per write, from 2026-01-01).

| Mutation | Moves the issue's updatedAt |
|---|---|
| `issueCreate` | yes |
| `issueUpdate` | yes |
| `commentCreate` | yes |
| `commentUpdate` | yes |
| `attachmentLinkURL` | yes (a repeat link for the same url on the same issue renames the attachment, adds none) |
| `issueArchive` | yes |
| `issueLabelCreate` | no (labels aren't on an issue yet) |

## Switches

`linear.Switches` is settable at any time. One-shot switches are used up by the request they act on.

| Switch | Example | Effect |
|---|---|---|
| `AnswerWithNewerField` (one-shot) | `linear.Switches.AnswerWithNewerField = true;` then read the issue you just wrote | The read first applies a teammate's edit to the issue written last: its `estimate` goes up by one and its `updatedAt` moves. |
| `RateLimit` | `linear.Switches.RateLimit = LinearRateLimit.Http429;` (or `GraphQl`) | Every request is refused: HTTP 429, or HTTP 400, both with error code `RATELIMITED` and `x-ratelimit-requests-remaining: 0`. Set `None` to stop. |
| `RevokeKey` | `linear.Switches.RevokeKey = true;` | Every request is a 401 `AUTHENTICATION_ERROR` from now on. |
| `RepeatClientId` (sticky) | `linear.Switches.RepeatClientId = true;` then `issueCreate` with a fresh `id` | The create is refused with the duplicate-id error above and nothing is created. |
| `NoAnswer` (one-shot) | `linear.Switches.NoAnswer = true;` then any mutation | The mutation is applied, then the connection is aborted with no reply: the client sees a transport error and can't know the outcome. |
| `DropBeforeApplying` (one-shot) | `linear.Switches.DropBeforeApplying = true;` then any mutation | The connection is aborted with no reply and the mutation is NOT applied: the client can't know the outcome, and a re-read finds nothing (a real client's `Unknown`). |
| `MoveTeam` (one-shot) | `linear.Switches.MoveTeam = "OPS";` (team seeded) then read | The issue written last moves to `OPS` on the next read: a new identifier (`OPS-1`), the same id, a moved `updatedAt`; the old identifier (`ENG-1`) still resolves. (Ph0 checks: that Linear keeps resolving it.) |

## Other assumptions (Ph0 checks)

- The client `id` is not checked for UUID format.
- `attachmentLinkURL` is idempotent per issue and url.
- There is no conditional update: `issueUpdate` applies whatever fields it is given.
- Issues are served newest first by `orderBy`; `FakeServerOptions.LinearNewestFirst` is the replaying fake's switch for the same.

## Lookups (Ph1, assumed; Ph0's probe doesn't check them)

- `teams(filter: { key: { in: [...] } })` is a real filter, and a team has `states`, `cycles`, `members` and `labels` connections (`LinearRender.TeamWithLookups`; the query is `Lookups.graphql` in the adapter).
- The fake gives every team all members, its own states, and its own labels plus the workspace ones; a cycle seeded without a team belongs to every team. Whether `team.labels` includes workspace labels, and `team.members` includes users who left (`active: false`), is unverified.
- A cycle has `number`, `name`, `startsAt` and `endsAt`; the adapter picks the current, the next and the last three by those times. The fake seeds two-week cycles from 2026-01-01 unless given times.
- An issue also serves `dueDate` (null) and `sortOrder` (-1000 per position); the feed asks for `state.id`, `assignee.id`, `cycle.id` and `labels.nodes.id` too.

## Not probed, unverified

The probe (`scripts/linear-probe.sh`) never checks these, so the fake's behavior for them is a guess until someone checks it by hand:

- `updatedAt` moving on `commentUpdate`, `issueArchive` and `issueLabelCreate` (the probe measures create, comment, attachment and update only).
- The old identifier resolving after a team move (the probe makes no team move; it prints `team_move.old_key_resolves: not probed`).
- The duplicate-id error shape: the probe records the real error code for a repeated client id, but the `validationErrors` shape in `LinearErrors.DuplicateId` is a guess until then.

The probe does read two things the fake serves, so a mismatch shows in the paste block: `archived.hidden_by_default` (an archived issue is left out of `issues` unless
`includeArchived: true`) and `pagination.after_cursor` (`first: 1` then `after: <endCursor>` returns the next issue).
