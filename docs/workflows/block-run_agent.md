# Run agent

Group: AI. Type: `run_agent`. Start one agent session that works in a repo and reports fields back. The session runs in a fresh worktree on its own branch, inside the sandbox.

## Config

| Field | Type | Needed | Meaning |
|---|---|---|---|
| `repo` | text | yes | The profile repo the agent works in. |
| `prompt` | text | yes | What to do. It may hold references. |
| `readOnlyRepos` | list of text | no | Repos the agent may read and never write. Not with agent `codex`. |
| `agent` | text | no | `claude-code`, `codex` or `cursor`. Left out: the profile's `workflow.agent.target`. |
| `outputs` | object | no | The fields the agent reports, by name, each with a type. |
| `timeout` | text | no | How long the node may run, such as `45m`. A whole number and `m`, `h` or `d`. Default `2h`. |
| `connections` | list of text | no | The connections the agent may reach. Each one opens its hosts in the sandbox. |
| `resources` | object | no | Sandbox limits. `wall` is the wall-clock limit, written like `timeout`. |

## Hands on

1. One field for each name in `outputs`. A type is `string`, `number`, `boolean`, `object` or `array`. A trailing `?` makes the field optional.
2. Always: `status`, `branch`, `commit`, `sessionId` and `usage`. Don't use these names in `outputs`.

An output name starts with a lowercase letter, then letters and digits only.

## How a run goes

1. The node starts the session. The agent's brief holds the exact line it uses to report.
2. The agent reports each field as `fact:<name>`. A missing, undeclared or mistyped field is refused, and the refusal names it.
3. A session that ends without a report fails the node.

A person may take the session over. While they hold it, the timeout clock stops. When they hand it back, the clock goes on with the time that was left.

## Notes

- A `run_agent` node can't sit inside a `for_each` body.
- The prompt goes to the agent as written. Text in it that came from a connection is untrusted. Don't ask the agent to obey it.

## Example

```json
{
  "version": 1,
  "name": "Check a branch for contract drift",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "check", "type": "run_agent", "config": { "repo": "web", "prompt": "Check {{trigger.payload}} for contract drift and fix what you can", "outputs": { "drifted": "boolean", "summary": "string" }, "timeout": "45m" } },
    { "id": "tell", "type": "notify", "config": { "title": "Drift check", "body": "{{check.summary}}" } }
  ],
  "edges": [
    { "from": "start", "to": "check" },
    { "from": "check", "to": "tell" }
  ]
}
```
