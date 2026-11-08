# `vulnerabilities` block

Asks OSV.dev which packages have known vulnerabilities. It needs no connection and no key.

| Config | Meaning |
|---|---|
| `provider` | `osv` (default) |
| `packages` | A list of `{ecosystem, name, version}`. Ecosystems use OSV's names: `npm`, `NuGet`, `Go`, `crates.io`, `PyPI` |
| `repo` | A repo of the approved profile. Its lockfiles are read instead of a package list |

Set exactly one of `packages` and `repo`. Outputs: `vulns[]` (`id`, `aliases`, `summary`, `severity?`, `package`, `version`,
`fixedIn?`, `url`) and `count`.

Lockfiles read from a repo: `packages.lock.json`, `package-lock.json`, `go.sum`, `Cargo.lock` and `poetry.lock`, in the
repo root and its subfolders (not `node_modules`, `.git`, `bin` or `obj`).

The block reads only the hosts in the profile's `workflow.vulnerabilities.hosts` (default `api.osv.dev`). Advisory text comes
from the web: treat it as untrusted wherever it reaches a prompt.

```json
{"packages":[{"ecosystem":"npm","name":"lodash","version":"4.17.20"}]}
```
