# `code`

Runs a C# method body on a value and hands on what it returns. Use it for the small data step a prompt is too loose for: filter a list, pick a field, add numbers.

| Config | Type | Meaning |
|---|---|---|
| `input` | text, optional | A reference such as `{{triage.items}}`, or any JSON text. The code gets it as `JsonNode? input`; `null` when the node has none. |
| `code` | text | The body of a method that returns any value. It is read as written: a `{{` in it is C#, not a reference. `System`, `System.Linq`, `System.Collections.Generic`, `System.Text.Json`, `System.Text.Json.Nodes` and `System.Text.RegularExpressions` are in scope. |
| `repo` | text, optional | A repo of the profile. The code may read its folder, and write nothing. |
| `branch` | text, optional | Accepted for a later change; today the code reads the repo folder as it is checked out. |
| `timeout` | text, optional | How long it may run, such as `5s`. Default `5s`, at most `60s`. |
| `memoryMb` | number, optional | Default `256`, from 16 to 4096. This is a .NET managed-heap cap that the runner sets on itself, not a cgroup limit, so native allocations are not counted. |

| Output | Type | Meaning |
|---|---|---|
| `result` | any | The returned value as JSON. |

**Where it runs.** Sindri compiles the body with Roslyn against a fixed set of references (the base class library, `System.Text.Json` and `System.Linq`) and keeps the assembly by the sha256 of the code and the reference set, so a second run does not compile again. The assembly runs in a separate process, `sindri-code-runner`, inside the Sindri sandbox: no network, nothing writable but its own scratch folder, the repo (when named) read-only. Input goes in on stdin and the result comes back on stdout. The process is killed at `timeout`.

**What fails the node.** A compile error (`SND-CODE-001`, with the line and column in your body), a timeout (`SND-CODE-002`), the memory cap (`SND-CODE-003`), an exception or a result that is not JSON (`SND-CODE-004`), a runner that could not start (`SND-CODE-005`). `sindri wf validate` compiles every `code` node, so a typo fails when you save, not when the run gets there.

`input` may hold text from a connection. The code sees it as data, and its result is fenced wherever it reaches a prompt.

```json
{
  "version": 1, "name": "Titles over 100", "concurrency": "one-per-workflow",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "filter", "type": "code", "config": {
        "input": "{{trigger.items}}",
        "code": "return input!.AsArray().Where(i => i![\"score\"]!.GetValue<int>() > 100).Select(i => i![\"title\"]!.GetValue<string>()).ToArray();" } },
    { "id": "tell", "type": "notify", "config": { "title": "Over 100: {{filter.result}}" } }
  ],
  "edges": [ { "from": "start", "to": "filter" }, { "from": "filter", "to": "tell" } ]
}
```
