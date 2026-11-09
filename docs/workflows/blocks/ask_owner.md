# `ask_owner`

Asks a question and waits for the answer. The question is a row on the Needs-you page (kind `question`); the run waits on it as a Temporal wait, so nothing polls. The node leaves by `answered` when someone answers and by `timeout` when its `timeout` passes first.

## Config

| Field | Type | Meaning |
|---|---|---|
| `question` | text | The question. |
| `choices` | list of text, optional | The answers to pick from. With choices, an answer must be one of them. |
| `timeout` | text, optional | How long to wait: `90s`, `15m`, `2h`, `1d`, `1h30m`. Without it the node waits until someone answers or the run is cancelled. |
| `default` | text, optional | What `answer` holds when the node times out (empty text when missing). With choices it must be one of them. |

## Outputs

| Field | Type | Meaning |
|---|---|---|
| `answer` | string | The answer, or the `default` at a timeout. |
| `answeredBy` | string | `human:<client>` (a person, e.g. `human:cli`), `session:launch:<launch id>` (a session Sindri launched), `session:<client>` (any other session, e.g. `session:mcp`), or `timeout`. |

Branches: `answered` and `timeout`.

## Answering

Anyone may answer, and the answer is attributed to the one who gave it:

- `sindri answer <run>/<node> --text "ship it"` or `--choice yes` (`POST /sindri/workflow-runs/{run}/nodes/{node}/answer`);
- the MCP tool `workflow_answer` with `run`, `node` and one of `text` or `choice`.

An agent session may answer the question of the run it was launched by; the timeline shows the answer as `session:launch:<launch id>`. The workflow takes an answer only while the node waits: an answer for a node that is not waiting is `SND-GRAPH-130`, one for a run that ended `SND-GRAPH-107`, and one that is not a choice, or has both a text and a choice, `SND-CLI-003`. The first answer wins. Until the app has an answer box, the CLI and the MCP tool are the way.

## Example

```json
{ "version": 1, "name": "Ship gate",
  "nodes": [ { "id": "start", "type": "manual", "config": {} },
             { "id": "ask", "type": "ask_owner", "config": { "question": "Ship the release?", "choices": ["yes", "no"], "timeout": "2h", "default": "no" } },
             { "id": "go", "type": "notify", "config": { "title": "Shipping", "body": "{{ask.answeredBy}} said {{ask.answer}}" } },
             { "id": "stop", "type": "notify", "config": { "title": "Held back", "body": "Nobody answered in 2 hours" } } ],
  "edges": [ { "from": "start", "to": "ask" }, { "from": "ask", "output": "answered", "to": "go" }, { "from": "ask", "output": "timeout", "to": "stop" } ] }
```

Here `go` runs on any answer (add a `condition` on `ask.answer` to tell yes from no) and `stop` runs when nobody answers in 2 hours.
