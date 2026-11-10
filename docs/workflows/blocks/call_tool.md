# `call_tool`

Calls one tool: a running toolkit's tool, or, with `connection` set, a tool of an `mcp` connection or an action of a `rest` connection's preset.

| Config | Type | Meaning |
|---|---|---|
| `tool` | text | `<toolkit>__<tool>` for a toolkit; with `connection`, the MCP tool name or the preset action name. |
| `connection` | text, optional | A connection name such as `github:work`. |
| `arguments` | object, optional | The tool's arguments. A toolkit tool's are checked against its manifest schema before the run. |

| Output | Type | Meaning |
|---|---|---|
| `result` | any | The tool's answer parsed as JSON, or its text when it is not JSON. |

```json
{
  "version": 1, "name": "Note each new issue", "concurrency": "one-per-workflow",
  "nodes": [
    { "id": "hourly", "type": "schedule", "config": { "cron": "0 * * * *" } },
    { "id": "issues", "type": "call_tool", "config": { "connection": "github:work", "tool": "list_issues", "arguments": { "owner": "acme", "repo": "web", "state": "open" } } },
    { "id": "save", "type": "call_tool", "config": { "tool": "notes__add_note", "arguments": { "text": "{{issues.result}}" } } }
  ],
  "edges": [ { "from": "hourly", "to": "issues" }, { "from": "issues", "to": "save" } ]
}
```

**MCP connections**: Sindri connects with the stored key, calls `tools/call` and returns the tool's structured content (else its text parts). A tool that reports an error fails the node (`SND-GRAPH-102`).

**Rest actions** (fixed per preset, in `RestActions.cs`): `github` has `create_issue`, `get_issue`, `list_issues` and `add_comment`; `slack` has `post_message`. Arguments named in the path (`owner`, `repo`, `number`) fill it; the rest is the JSON body of a POST or the query of a GET. The request goes through the same signed path the pollers use: the token goes only to the provider's host over https and a redirect is refused.

The stored token is cut out of every answer by value, then `Aw.Redaction` runs over what is left.
