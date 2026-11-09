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
3. Each returned event goes to each trigger whose `filter` takes it. The run id is `<slug>@<revision>:<occurrence>`, where the occurrence is a number made from the event's delivery id, so the same event gives the same run id and a second start is refused (`SND-GRAPH-100`). The number is a hash, so two deliveries can share it: on `SND-GRAPH-100` the service reads the run that holds the id, and only when that run carries the same delivery id is it a repeat. Otherwise it tries the next number (up to 5 tries) and, if all are held, fails the poll with the cursor kept. A real event is never dropped.
4. After every run for the call's events started, the cursor is saved. A start that fails (the engine is down, a store error) leaves the cursor where it was; the next poll reads the same events, and the runs that did start are refused as duplicates. A trigger skipped because its workflow runs one at a time and a run is in flight counts as handled: the cursor moves.
5. A rate-limited answer (429, 5xx) or an error saves a wait before the next poll and keeps the cursor: the provider's `Retry-After`, else 30 seconds, doubling with each failure in a row, at most an hour.

A trigger added to a workflow starts nothing for old events: the first poll of a (connection, event kind) only records where the provider is now.

## Catch-up

After the control plane starts, and after the machine wakes (the wall clock jumped more than 3 x 30 seconds past the monotonic clock), every group is read forward at once, interval or not (a backoff still holds). Each provider call reads up to 10 pages; a catch-up repeats the call, saving the cursor after each, until a call brings nothing new or `workflow.poll.catchUpPages` calls (default 20) are done. A catch-up that is still getting new events at the cap, or whose poller skipped older events, records one `catchup.capped` and leaves the cursor where it stopped; the next poll goes on from there. A catch-up that overlaps a regular poll starts each event once.

## Events

| Event | When | Public |
|---|---|---|
| `connection.polled` | A poll returned events. Counts only: events, runs started, triggers skipped. A poll with nothing new writes none | no |
| `workflow.trigger.skipped` | A matched trigger started no run: `concurrency` (a run in flight) or `duplicate` (the delivery already had its run). One per workflow, delivery and reason | no |
| `catchup.capped` | A read stopped at its cap | no |
