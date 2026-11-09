# Sindri resources

A workflow can ask Sindri for the things it needs to keep between runs: a database, a disk folder, a sandbox size and a connection. Sindri makes
them on your machine, hands their paths to the blocks by name, keeps the data when you archive the workflow, and deletes it only when you ask.

## What you can declare

In a workflow's `resources`:

| Key | Gives the run | Rules |
|---|---|---|
| `database: { name }` | one SQLite file, `<name>.db` | `name` is lowercase letters, digits and `-`, 1 to 64 characters, starting with a letter or digit |
| `disk: { size }` | one folder, size-checked before each run | `size` is a whole number and `KiB`, `MiB`, `GiB` or `TiB` (`512MiB`), or the short forms `512m`, `4g` |
| `sandbox: { cpu?, memory, wall }` | the size of the sandbox an agent block runs in | `memory` as a size, `cpu` as a percent (`50%`), `wall` as `45m`, `2h` or `1d` |
| `connection: { name }` | the name of a connection, as an environment value | the name of a connection from the Connections page |

A node and a toolkit declare the same keys. Names are unique per owner and kind: two databases named `state` on one workflow are `SND-RES-002`.

## Where the files go

Under `$AW_SINDRI_HOME/resources/`:

| Owner | Folder |
|---|---|
| `workflow:<slug>` | `workflow/<slug>/` |
| `node:<slug>/<id>` | `node/<slug>/<id>/` |
| `toolkit:<name>` | `toolkit/<name>/` |

Folders are mode `0700` and database files `0600`. Making them again changes no data. A symlink where a resource goes is refused (`SND-RES-004`),
and a size check never follows or counts a symlink.

## When Sindri makes them

1. On **activate** of a workflow revision, Sindri plans the declared resources and creates them. The validator has already refused a bad name, a bad size,
   or a sandbox `memory` or `wall` above the box's cap (`SND-RES-005`, `SND-RES-006`).
2. Before each node runs, the `EnsureResources` step makes sure they still exist and measures each disk. A disk over its `size` fails the node with the bytes used and
   the limit (`SND-RES-001`). A disk with more than 100,000 files is `SND-RES-007`: delete files you don't need.
3. The block gets the paths by environment name on its block context (`Resources`), and an agent Step gets the same names in its environment:
   `SINDRI_DB_<NAME>` (the file), `SINDRI_DISK_<NAME>` (the folder), `SINDRI_CONNECTION_<NAME>` (the connection's name). `<NAME>` is the resource name in capitals,
   `-` as `_`.
4. The sandbox plan binds the owner's folder read-write as a `State` mount (docs/csharp/sindri-sandbox.md, "State mounts from resources").

Bookkeeping lives in the `resource` table of `ledger.db` (owner, kind, name, path, size limit, times). It is only a record: the data is the files.

## Seeing and removing them

```
sindri resources list [<owner>] [--json]
sindri resources purge <owner> [--json]
```

The same two are the MCP tools `resources_list` and `resources_purge`. `sindri doctor` has a `resources` row: how many there are, what they hold in all, and a warning
for any disk at 90% of its size or more.

- **Archiving keeps the data.** Archive and delete never touch a resource folder.
- **Purge is explicit.** It deletes the owner's whole folder. It is refused while the workflow is active or one of its runs is going, and the message names the run
  (`SND-RES-003`). Archive the workflow, or wait for the run, then purge.
- Sindri's own state (`workflow_seen`, memory stores, run outputs) is not a resource and is never purged here.

## Errors

`SND-RES-001` to `SND-RES-007`: see docs/sindri/errors.md.
