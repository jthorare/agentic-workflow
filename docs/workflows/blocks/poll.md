# The `connection_event` block (poll trigger)

A workflow starts when a connection's provider reports an event. Sindri polls for it: there is no webhook and no tunnel (owner decision D6).

## The node

| Field | Required | Meaning |
|---|---|---|
| `connection` | yes | A connection name, `github:work` |
| `event` | yes | A catalog id, `github.pr.merged`. `workflow_blocks` lists the events per connection |
| `filter` | no | A condition over the event's fields, read as `trigger.<field>`: `trigger.repo == "acme/api"`. No filter takes every event |

The run's `trigger` holds `kind` (`connection_event`), `connection`, `event`, `deliveryId`, `occurredAt` and the event's fields (`repo`, `number`, `title`, `url`, `author`, `ref`, `identifier`, `label`, `to`, `channel`, `ts`, `user`, `text`, `start`; each only when the event has it). The values are provider text: untrusted, fenced wherever they reach a prompt.

## What the service does

1. Every 30 seconds `workflow-poll` lists the active workflows and groups their `connection_event` nodes by (connection, event kind). Three workflows on `github:work` and `github.pr.merged` make one provider call.
2. A group is polled when its cursor was last polled at least `workflow.poll.intervalSeconds` ago (default 120, least 30) and no backoff is running.
3. Each returned event goes to each trigger whose `filter` takes it. The run id is `<slug>@<revision>:<occurrence>`, where the occurrence is a number made from the event's delivery id, so the same event gives the same run id and a second start is refused (`SND-GRAPH-100`). The number is a hash, so two deliveries can share it: on `SND-GRAPH-100` the service reads the run that holds the id, and only when that run carries the same delivery id is it a repeat. Otherwise it tries the next number (up to 5 tries) and, if all are held, fails the poll (`SND-CONN-101`) with the cursor kept. A real event is never dropped.
4. After every run for the call's events started, the cursor is saved. A start that fails (the engine is down, a store error) leaves the cursor where it was and saves the wait of step 5; the next poll reads the same events, and the runs that did start are refused as duplicates. A launch that fails for any other reason keeps its run, marked failed, under the event's run id, so on the next poll that event is a duplicate and the rest of the group moves on. A trigger skipped because its workflow runs one at a time and a run is in flight counts as handled: the cursor moves.
5. A rate-limited answer (429, 5xx), a poller error or a failed start saves a wait before the next poll and keeps the cursor: the provider's `Retry-After`, else 30 seconds, doubling with each failure in a row, at most an hour.

A trigger added to a workflow starts nothing for old events: the first poll of a (connection, event kind) only records where the provider is now. When no active workflow watches a (connection, event kind) any more (archived, or its active revision has no such trigger), the next run of the service drops that cursor's position, so a trigger that watches it later starts with the same baseline. The one gap: a workflow archived and another activated on the same group inside one 30-second run can still get the events of the last interval.

## Catch-up

After the control plane starts, and after the machine wakes (the wall clock jumped more than 3 x 30 seconds past the monotonic clock), every group is read forward at once, interval or not (a backoff still holds). Each provider call reads up to 10 pages; a catch-up repeats the call, saving the cursor after each, until a call brings nothing new or `workflow.poll.catchUpPages` calls (default 20) are done. A catch-up that is still getting new events at the cap records one `catchup.capped` and leaves the cursor where it stopped; the next poll goes on from there. A provider call that hit its own 10-page cap before it reached the saved cursor (a gap, on any poll) also records one `catchup.capped`, but moves the cursor to the newest event it read: the events older than those pages start no run. The poller can only read newest first, so there is no position to resume from. A catch-up that overlaps a regular poll starts each event once.

## Events

| Event | When | Public |
|---|---|---|
| `connection.polled` | A poll returned events. Counts only: events, runs started, triggers skipped. A poll with nothing new writes none | no |
| `workflow.trigger.skipped` | A matched trigger started no run: `concurrency` (a run in flight) or `duplicate` (the delivery already had its run). One per workflow, delivery and reason | no |
| `catchup.capped` | A read stopped at its cap | no |
