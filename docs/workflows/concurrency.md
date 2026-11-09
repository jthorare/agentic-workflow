# Concurrency

The `concurrency` key says what happens when a trigger fires while a run of the same workflow is still in flight.

| Value | What happens to the second trigger |
|---|---|
| `one-per-workflow` | The default. It is skipped and logged. The first run is not disturbed. |
| `queue` | It is recorded as `queued` and waits for its turn. Runs go one at a time, in order. |
| `unlimited` | It starts a run at once. Runs overlap. |

Any other text behaves like `one-per-workflow`.

## Which one to pick

- A schedule that checks something and may take longer than its period: `one-per-workflow`. A tick that lands mid-run is dropped.
- Work that must not be lost, such as one run per incoming event: `queue`.
- Runs that cannot disturb each other: `unlimited`.

## Example

```json
{
  "version": 1,
  "name": "Hourly check, never overlapping",
  "concurrency": "queue",
  "nodes": [
    { "id": "hourly", "type": "schedule", "config": { "cron": "0 * * * *" } },
    { "id": "tell", "type": "notify", "config": { "title": "Hourly tick" } }
  ],
  "edges": [
    { "from": "hourly", "to": "tell" }
  ]
}
```
